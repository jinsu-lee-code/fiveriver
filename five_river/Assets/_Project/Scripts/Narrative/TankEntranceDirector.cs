using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.DebugTools;
using FiveRivers.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FiveRivers.Narrative
{
    // Staged entrance of the tank (2026-11-10, trial): from its first appearance until the player is handed the
    // fight. While it plays the player has no control, the run clock stands still and the tank goes only where
    // the take puts it; at the end the view eases back into the player's own eyes and the tank carries on down
    // its road from where it was left. Which take plays is TankConfig.entranceTake (0 = none, the section starts
    // as before). Expanded run only: the exhibit run's 9:00 limit has no room for it. Space or Enter skips.
    // Added at run time by NarrativeDirector, like HillAssaultDirector by DawnDirector.
    public sealed class TankEntranceDirector : MonoBehaviour, IDebugPanel
    {
        public const int None = 0, Fable = 1, Blender = 2, Mixed = 3;
        // Debug: play this take at the next tank entry whatever TankConfig says, once (-1 = follow TankConfig).
        public static int ForceTake = -1;
        const float HandBack = 1.3f, BarFade = 0.4f, BarHeight = 0.105f, SkipAfter = 0.6f;

        FlowDirector director;
        TankController tank;
        PlayerController player;
        LevelPoints points;
        SubtitleDisplay subtitles;
        ObjectiveDisplay objective;
        GameText copy;
        FlareDirector flares;
        Hud hud;
        Camera playerCamera, sequenceCamera;
        Renderer[] playerRenderers;
        PlayerHealth playerHealth;
        bool invulnerableBefore;
        CanvasGroup bars;
        TankEntranceTake take;
        float time, clockBefore;
        int nextCue;
        bool pending, lineSaid;

        public bool Playing { get; private set; }
        public float Elapsed => time;
        public string TakeName => take != null ? take.Name : "";

        public void Configure(FlowDirector flow, TankController tankController, PlayerController playerController, LevelPoints level,
            SubtitleDisplay subtitleDisplay, ObjectiveDisplay objectiveDisplay, GameText text, FlareDirector flareDirector)
        {
            director = flow;
            tank = tankController;
            player = playerController;
            points = level;
            subtitles = subtitleDisplay;
            objective = objectiveDisplay;
            copy = text;
            flares = flareDirector;
        }

        void Start()
        {
            hud = FindFirstObjectByType<Hud>();
            director.Flow.SectionChanged += OnSectionChanged;
            director.Flow.RetryRequested += OnRetry;
        }

        void OnDestroy()
        {
            if (Playing) Finish(false);
            if (director == null || director.Flow == null) return;
            director.Flow.SectionChanged -= OnSectionChanged;
            director.Flow.RetryRequested -= OnRetry;
        }

        int Selected => ForceTake >= 0 ? ForceTake : director.Flow.IsExpanded ? tank.EntranceTake : None;

        // True when a sequence will take the tank's arrival: NarrativeDirector leaves the "tank!" line to it.
        public bool WillPlay(FlowTransition t) => t.To == FlowSection.Tank && t.From != FlowSection.Tank && Selected != None;

        void OnSectionChanged(FlowTransition t)
        {
            if (WillPlay(t)) pending = true; // started in Update, once the tank has been put on its road
            else if (t.To != FlowSection.Tank)
            {
                pending = false;
                if (Playing) Finish(false);
            }
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            pending = false;
            if (Playing) Finish(false);
        }

        void Update()
        {
            if (pending)
            {
                pending = false;
                Begin();
            }
            if (!Playing || director.Flow.Paused) return;
            time += Time.deltaTime;
            var keys = Keyboard.current;
            if (keys != null && time > SkipAfter && time < take.Duration - HandBack
                && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
                time = take.Duration - HandBack; // skip straight to the hand-back
            float at = Mathf.Min(time, take.Duration);
            var frame = take.Evaluate(at);
            tank.SetScriptedPose(frame.tankPosition, frame.hullYaw, frame.turretYaw, frame.gunPitch);
            var cues = take.Cues;
            while (nextCue < cues.Count && cues[nextCue].time <= at) Run(cues[nextCue++]);

            // the last moments ease from the take's camera into the player's own eyes
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(take.Duration - HandBack, take.Duration, at));
            var position = frame.cameraPosition;
            var rotation = frame.cameraRotation;
            if (frame.shake > 0f)
            {
                float t = Time.time * 23f, amount = frame.shake * (1f - back);
                position += new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (0.07f * amount);
                rotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 5.1f) - 0.5f) * 1.4f * amount, (Mathf.PerlinNoise(9.2f, t) - 0.5f) * 1.4f * amount, 0f);
            }
            var eye = playerCamera.transform;
            sequenceCamera.transform.SetPositionAndRotation(Vector3.Lerp(position, eye.position, back), Quaternion.Slerp(rotation, eye.rotation, back));
            sequenceCamera.fieldOfView = Mathf.Lerp(frame.fieldOfView > 1f ? frame.fieldOfView : playerCamera.fieldOfView, playerCamera.fieldOfView, back);
            bars.alpha = Mathf.Min(Mathf.InverseLerp(0f, BarFade, time), Mathf.InverseLerp(take.Duration, take.Duration - BarFade, time));
            if (time >= take.Duration) Finish(true);
        }

        void Begin()
        {
            int which = Selected;
            ForceTake = -1;
            if (which == None || !tank.Visible || tank.IsStopped) return;
            playerCamera = player.CameraPivot.GetComponentInChildren<Camera>();
            if (playerCamera == null) return;

            // the player comes out of it looking up the road the tank is on
            var road = Vector3.Lerp(tank.RouteStart, tank.RouteHold, 0.62f) - player.transform.position;
            road.y = 0f;
            if (road.sqrMagnitude > 1f) player.transform.rotation = Quaternion.LookRotation(road);

            var leader = points.parkSpot != null ? points.parkSpot.position : player.transform.position;
            foreach (var ally in FindObjectsByType<AllyCombatant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (ally.name == "ParkYongpal") leader = ally.transform.position;
            var context = new TankEntranceContext
            {
                tankStart = tank.RouteStart,
                tankHold = tank.RouteHold,
                playerEye = playerCamera.transform.position,
                playerView = playerCamera.transform.rotation,
                squadLeader = leader,
                mound = points.moundTop.position,
                terrain = Terrain.activeTerrain,
            };
            take = which == Blender ? new TankEntranceBakedTake() : which == Mixed ? new TankEntranceMixedTake() : (TankEntranceTake)new TankEntranceFableTake();
            if (!take.Begin(context))
            {
                Debug.LogWarning("[TankEntrance] take " + take.Name + " cannot play (data missing): the section starts as usual.");
                subtitles.Say(copy.tankArrives);
                take = null;
                return;
            }

            EnsureCamera();
            EnsureBars();
            time = 0f;
            nextCue = 0;
            lineSaid = false;
            Playing = true;
            tank.BeginScripted();
            director.InputGate.Block(this);
            Cursor.lockState = CursorLockMode.Locked; // a blocked gate frees the cursor: keep it away during the sequence
            Cursor.visible = false;
            clockBefore = director.ClockScale;
            director.ClockScale = 0f; // the sequence does not eat into the attempt time
            if (hud != null) hud.Cinematic = true;
            // nobody can hurt a player who cannot move or shoot back
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                invulnerableBefore = playerHealth.Invulnerable;
                playerHealth.Invulnerable = true;
            }
            objective.Hide();
            // the first-person arms and rifle are not part of the staged shots
            playerRenderers = player.GetComponentsInChildren<Renderer>(false);
            foreach (var r in playerRenderers) r.forceRenderingOff = true;
            playerCamera.enabled = false;
            sequenceCamera.enabled = true;
            bars.gameObject.SetActive(true);
            bars.alpha = 0f;
        }

        void Run(TankEntranceCue cue)
        {
            switch (cue.kind)
            {
                case TankEntranceCueKind.Flare:
                    if (flares != null) flares.Launch();
                    break;
                case TankEntranceCueKind.Line:
                    lineSaid = true;
                    subtitles.Say(copy.tankArrives);
                    break;
                case TankEntranceCueKind.Fire:
                    tank.ScriptedFire(cue.point);
                    break;
            }
        }

        // completed: the take ran (or was skipped) to its end; false when the section was left or retried under it
        void Finish(bool completed)
        {
            Playing = false;
            tank.EndScripted();
            director.InputGate.Unblock(this);
            director.ClockScale = clockBefore;
            if (hud != null) hud.Cinematic = false;
            if (playerHealth != null) playerHealth.Invulnerable = invulnerableBefore;
            if (playerRenderers != null) foreach (var r in playerRenderers) if (r != null) r.forceRenderingOff = false;
            if (sequenceCamera != null) sequenceCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (bars != null) bars.gameObject.SetActive(false);
            if (!completed) return;
            if (!lineSaid) subtitles.Say(copy.tankArrives);
            objective.Show(copy.objectiveTankMove);
        }

        void EnsureCamera()
        {
            if (sequenceCamera != null) return;
            var holder = new GameObject("TankEntranceCamera");
            holder.transform.SetParent(transform, false);
            sequenceCamera = holder.AddComponent<Camera>();
            sequenceCamera.CopyFrom(playerCamera);
            sequenceCamera.depth = playerCamera.depth + 1f;
            var from = playerCamera.GetUniversalAdditionalCameraData();
            var to = sequenceCamera.GetUniversalAdditionalCameraData();
            to.renderPostProcessing = from.renderPostProcessing;
            to.antialiasing = from.antialiasing;
            to.antialiasingQuality = from.antialiasingQuality;
            to.volumeLayerMask = from.volumeLayerMask;
            to.renderShadows = from.renderShadows;
            sequenceCamera.enabled = false;
        }

        // Two black bars, under every other canvas so subtitles stay on top of the lower one.
        void EnsureBars()
        {
            if (bars != null) return;
            var holder = new GameObject("TankEntranceBars", typeof(Canvas), typeof(CanvasGroup));
            holder.transform.SetParent(transform, false);
            var canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;
            bars = holder.GetComponent<CanvasGroup>();
            bars.interactable = false;
            bars.blocksRaycasts = false;
            for (int i = 0; i < 2; i++)
            {
                var bar = new GameObject(i == 0 ? "Top" : "Bottom", typeof(Image)).GetComponent<Image>();
                bar.transform.SetParent(holder.transform, false);
                bar.color = Color.black;
                bar.raycastTarget = false;
                var rect = bar.rectTransform;
                rect.anchorMin = new Vector2(0f, i == 0 ? 1f - BarHeight : 0f);
                rect.anchorMax = new Vector2(1f, i == 0 ? 1f : BarHeight);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            holder.SetActive(false);
        }

        // Debug: show a take now, from the tank's first appearance.
        public void Preview(int which)
        {
            ForceTake = which;
            var flow = director.Flow;
            if (flow.Section == FlowSection.Tank) flow.DebugJumpTo(FlowSection.Silence);
            flow.DebugJumpTo(FlowSection.Tank);
        }

        public string DebugTitle => "전차 등장 연출";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label(Playing ? $"재생 중: {take.Name}판 {time:0.0}/{take.Duration:0.0}초 (Space · Enter 건너뛰기)"
                : $"재생 안 함 · 기본값(TankConfig.entranceTake) {tank.EntranceTake} (0 없음 · 1 Fable · 2 Blender · 3 합본)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fable판 보기")) Preview(Fable);
            if (GUILayout.Button("Blender판 보기")) Preview(Blender);
            if (GUILayout.Button("합본 보기")) Preview(Mixed);
            GUILayout.EndHorizontal();
#endif
        }
    }
}
