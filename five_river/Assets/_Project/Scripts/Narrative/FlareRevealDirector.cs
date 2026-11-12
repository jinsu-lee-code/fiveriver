using System.Collections.Generic;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.DebugTools;
using FiveRivers.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FiveRivers.Narrative
{
    // Staged moment of the main assault (2026-11-12): the first flare of the assault opens over the slope and the
    // view leaves the player for a few seconds to show what it lights up — the enemy coming down toward the trench.
    //   shot 1  from behind the trench wall: the flare hanging in the sky, then down to the lit slope
    //   shot 2  long lens from the trench on the enemy nearest the trench, following them down
    //   shot 3  low on the slope beside them, the flare behind, then back into the player's eyes
    // While it plays the player has no control and cannot be hurt, and the run clock stands still; the enemy keep
    // moving and keep arriving, so the fight is already on when the view comes back. Once per run, expanded run
    // only (the exhibit run's 9:00 limit has no room for it). Space or Enter skips. Added by NarrativeDirector.
    public sealed class FlareRevealDirector : MonoBehaviour, IDebugPanel
    {
        public static bool Enabled = true;
        const float Shot1 = 2.6f, Shot2 = 5.9f, Duration = 9.2f, HandBack = 1.3f;
        const float BarFade = 0.4f, BarHeight = 0.105f, SkipAfter = 0.6f;

        FlowDirector director;
        PlayerController player;
        FlareDirector flares;
        EnemyDirector enemies;
        ObjectiveDisplay objective;
        Hud hud;
        Camera playerCamera, sequenceCamera;
        Renderer[] playerRenderers;
        PlayerHealth playerHealth;
        bool invulnerableBefore;
        CanvasGroup bars;
        Terrain terrain;
        float time, clockBefore;
        bool playedThisRun, pending;
        Vector3 flarePoint, eye, slopeFocus, focus, lowCamera;
        Quaternion eyeView;
        bool lowCameraSet;
        FiveRivers.Data.GameText copy;
        EnemyAgent subject;

        public bool Playing { get; private set; }
        public float Elapsed => time;

        public void Configure(FlowDirector flow, PlayerController playerController, FlareDirector flareDirector, ObjectiveDisplay objectiveDisplay, FiveRivers.Data.GameText text)
        {
            copy = text;
            director = flow;
            player = playerController;
            flares = flareDirector;
            objective = objectiveDisplay;
        }

        void Start()
        {
            hud = FindFirstObjectByType<Hud>();
            enemies = FindFirstObjectByType<EnemyDirector>();
            terrain = Terrain.activeTerrain;
            flares.Burst += OnFlare;
            director.Flow.SectionChanged += OnSectionChanged;
            director.Flow.RetryRequested += OnRetry;
        }

        void OnDestroy()
        {
            if (Playing) Finish();
            if (flares != null) flares.Burst -= OnFlare;
            if (director == null || director.Flow == null) return;
            director.Flow.SectionChanged -= OnSectionChanged;
            director.Flow.RetryRequested -= OnRetry;
        }

        void OnFlare(Vector3 point)
        {
            var flow = director.Flow;
            if (!Enabled || playedThisRun || Playing || !flow.IsExpanded || flow.Section != FlowSection.Assault) return;
            flarePoint = point;
            pending = true;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Title || t.To == FlowSection.Tutorial) playedThisRun = false;
            if (t.To != FlowSection.Assault)
            {
                pending = false;
                if (Playing) Finish();
            }
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            pending = false;
            if (Playing) Finish();
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
            if (keys != null && time > SkipAfter && time < Duration - HandBack
                && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
                time = Duration - HandBack;

            // what the long lens and the low camera look at: the enemy nearest the trench, followed smoothly
            // (one of them is kept through a shot, so the camera does not jump between men)
            if (subject == null || !subject.Alive) subject = Nearest();
            if (subject != null) focus = Vector3.Lerp(focus, subject.transform.position, 1f - Mathf.Exp(-Time.deltaTime * 7f));

            Vector3 position; Quaternion rotation; float fov;
            if (time < Shot1)
            {
                // behind the wall, a little to the side of the player: the flare, then down to the slope
                float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, Shot1, time));
                position = eye - eyeView * Vector3.forward * 1.6f + eyeView * Vector3.right * 0.9f + Vector3.up * 0.25f;
                var look = Vector3.Lerp(flarePoint, slopeFocus, u);
                rotation = Quaternion.LookRotation(look - position);
                fov = Mathf.Lerp(52f, 46f, u);
            }
            else if (time < Shot2)
            {
                // long lens from the trench, creeping in
                float u = Mathf.InverseLerp(Shot1, Shot2, time);
                position = eye + Vector3.up * 0.35f;
                rotation = Quaternion.LookRotation(focus + Vector3.up * 1.0f - position);
                // lens chosen by distance: a man stands about a third of the frame high, a little closer by the end
                float distance = Mathf.Max(8f, Vector3.Distance(position, focus));
                fov = Mathf.Clamp(2f * Mathf.Atan(Mathf.Lerp(3.4f, 2.6f, u) / distance) * Mathf.Rad2Deg, 4f, 20f);
            }
            else
            {
                // low on the slope beside them, looking across with the flare behind
                if (!lowCameraSet)
                {
                    lowCameraSet = true;
                    subject = Nearest();
                    if (subject != null) focus = subject.transform.position;
                    var toTrench = eye - focus; toTrench.y = 0f;
                    var side = Vector3.Cross(Vector3.up, toTrench.normalized);
                    lowCamera = OnGround(focus + toTrench.normalized * 10f + side * 3f, 0.45f); // ahead of him: he comes down past the lens
                }
                float u = Mathf.InverseLerp(Shot2, Duration - HandBack, time);
                position = lowCamera + Vector3.up * (0.15f * u);
                rotation = Quaternion.LookRotation(focus + Vector3.up * 1.1f - position);
                fov = 38f;
            }

            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Duration - HandBack, Duration, time));
            var own = playerCamera.transform;
            sequenceCamera.transform.SetPositionAndRotation(Vector3.Lerp(position, own.position, back), Quaternion.Slerp(rotation, own.rotation, back));
            sequenceCamera.fieldOfView = Mathf.Lerp(fov, playerCamera.fieldOfView, back);
            bars.alpha = Mathf.Min(Mathf.InverseLerp(0f, BarFade, time), Mathf.InverseLerp(Duration, Duration - BarFade, time));
            if (time >= Duration) Finish();
        }

        EnemyAgent Nearest()
        {
            EnemyAgent best = null; float bestDistance = float.MaxValue;
            if (enemies == null) return null;
            foreach (var e in enemies.ActiveEnemies)
            {
                if (e == null || !e.Alive) continue;
                float d = (e.transform.position - eye).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d; best = e;
            }
            return best;
        }

        Vector3 OnGround(Vector3 p, float above)
        {
            if (terrain != null) p.y = terrain.SampleHeight(p) + terrain.transform.position.y + above;
            return p;
        }

        void Begin()
        {
            playerCamera = player.CameraPivot.GetComponentInChildren<Camera>();
            if (playerCamera == null) return;
            playedThisRun = true;
            eye = playerCamera.transform.position;
            // the slope under the flare, as seen from the trench
            slopeFocus = OnGround(new Vector3(flarePoint.x, 0f, flarePoint.z), 1f);
            var ahead = slopeFocus - eye; ahead.y = 0f;
            eyeView = ahead.sqrMagnitude > 1f ? Quaternion.LookRotation(ahead) : Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            // the player comes out of it facing the lit slope
            if (ahead.sqrMagnitude > 1f) player.transform.rotation = Quaternion.LookRotation(ahead);
            subject = Nearest();
            focus = subject != null ? subject.transform.position : slopeFocus;
            lowCameraSet = false;

            EnsureCamera();
            EnsureBars();
            time = 0f;
            Playing = true;
            director.InputGate.Block(this);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            clockBefore = director.ClockScale;
            director.ClockScale = 0f;
            if (hud != null) hud.Cinematic = true;
            playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                invulnerableBefore = playerHealth.Invulnerable;
                playerHealth.Invulnerable = true;
            }
            objective.Hide();
            playerRenderers = player.GetComponentsInChildren<Renderer>(false);
            foreach (var r in playerRenderers) r.forceRenderingOff = true;
            playerCamera.enabled = false;
            sequenceCamera.enabled = true;
            bars.gameObject.SetActive(true);
            bars.alpha = 0f;
        }

        void Finish()
        {
            bool inAssault = director.Flow.Section == FlowSection.Assault;
            Playing = false;
            director.InputGate.Unblock(this);
            director.ClockScale = clockBefore;
            if (hud != null) hud.Cinematic = false;
            if (playerHealth != null)
            {
                // a moment's grace after the view comes back, as in CutsceneRig
                if (invulnerableBefore || !isActiveAndEnabled) playerHealth.Invulnerable = invulnerableBefore;
                else StartCoroutine(ReleaseAfterGrace());
            }
            if (playerRenderers != null) foreach (var r in playerRenderers) if (r != null) r.forceRenderingOff = false;
            if (sequenceCamera != null) sequenceCamera.enabled = false;
            if (playerCamera != null) playerCamera.enabled = true;
            if (bars != null) bars.gameObject.SetActive(false);
            if (inAssault && copy != null) objective.Show(copy.objectiveAssault);
        }

        System.Collections.IEnumerator ReleaseAfterGrace()
        {
            yield return new WaitForSeconds(CutsceneRig.Grace);
            if (playerHealth != null && !Playing) playerHealth.Invulnerable = false;
        }

        void EnsureCamera()
        {
            if (sequenceCamera != null) return;
            var holder = new GameObject("FlareRevealCamera");
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

        void EnsureBars()
        {
            if (bars != null) return;
            var holder = new GameObject("FlareRevealBars", typeof(Canvas), typeof(CanvasGroup));
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

        public string DebugTitle => "조명탄 연출";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label(Playing ? $"재생 중 {time:0.0}/{Duration:0.0}초 (Space · Enter 건너뛰기)" : playedThisRun ? "이번 회차에 재생함" : "주 공세 첫 조명탄에서 재생");
            Enabled = GUILayout.Toggle(Enabled, "조명탄 연출 켜기");
            if (GUILayout.Button("다시 보기 (주 공세로 이동)"))
            {
                playedThisRun = false;
                var flow = director.Flow;
                if (flow.Section == FlowSection.Assault) flow.DebugJumpTo(FlowSection.Scout);
                flow.DebugJumpTo(FlowSection.Assault);
            }
#endif
        }
    }
}
