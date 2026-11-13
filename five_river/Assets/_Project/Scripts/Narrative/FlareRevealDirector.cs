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
    //   shot 3  close on a rifleman running down: rifle in his hands, face lit by the flare (2026-11-13)
    //   shot 4  low on the slope: one of them is hit by the trench's fire and goes down, then back into the
    //           player's eyes under the bars (2026-11-13)
    // While it plays the player has no control and cannot be hurt, and the run clock stands still; the enemy keep
    // moving and keep arriving, so the fight is already on when the view comes back. Once per run, expanded run
    // only (the exhibit run's 9:00 limit has no room for it). Space or Enter skips. Added by NarrativeDirector.
    public sealed class FlareRevealDirector : MonoBehaviour, IDebugPanel
    {
        public static bool Enabled = true;
        const float Shot1 = 2.6f, Shot2 = 4.8f, Shot3 = 7.2f, HitAt = 7.6f, Duration = 10.8f, HandBack = 1.3f;
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
        bool playedThisRun, pending, cutDone;
        Vector3 flarePoint, eye, slopeFocus, focus, lowCamera;
        Quaternion eyeView;
        bool lowCameraSet, closeSet, victimHit;
        FiveRivers.Data.GameText copy;
        EnemyAgent subject, victim;
        Vector3 closeOffset, victimFocus;
        FiveRivers.Audio.AudioDirector audio;

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
            else if (time < Shot3)
            {
                // close on him as he runs: a little ahead and to the side, at chest height, carried along with him
                if (!closeSet)
                {
                    closeSet = true;
                    subject = Nearest();
                    if (subject != null) focus = subject.transform.position;
                    var toTrench = eye - focus; toTrench.y = 0f; toTrench.Normalize();
                    var side = Vector3.Cross(Vector3.up, toTrench);
                    closeOffset = toTrench * 2.4f + side * 1.3f;
                }
                float u = Mathf.InverseLerp(Shot2, Shot3, time);
                var chest = Chest(subject, focus);
                position = OnGround(focus + closeOffset, 0f);
                position.y = Mathf.Max(position.y + 0.3f, chest.y - 0.15f); // never under the slope
                rotation = Quaternion.LookRotation(chest + Vector3.down * 0.1f - position);
                fov = Mathf.Lerp(36f, 32f, u);
            }
            else
            {
                // low on the slope beside another of them: the trench's fire finds him and he goes down
                if (!lowCameraSet)
                {
                    lowCameraSet = true;
                    victim = Victim();
                    victimFocus = victim != null ? victim.transform.position : focus;
                    var toTrench = eye - victimFocus; toTrench.y = 0f; toTrench.Normalize();
                    var side = Vector3.Cross(Vector3.up, toTrench);
                    lowCamera = OnGround(victimFocus + toTrench * 3.5f + side * 5.5f, 0.55f); // ahead and beside him
                }
                if (victim != null) victimFocus = Vector3.Lerp(victimFocus, Hips(victim), 1f - Mathf.Exp(-Time.deltaTime * 6f));
                if (!victimHit && time >= HitAt)
                {
                    victimHit = true;
                    if (victim != null && victim.Alive)
                    {
                        // the shot from the trench that finds him (heard from the trench, as the allies' own fire is)
                        if (audio == null) audio = FindFirstObjectByType<FiveRivers.Audio.AudioDirector>();
                        if (audio != null) audio.PlaySpatial("SFX_M1_Fire", eye + (victimFocus - eye).normalized * 6f);
                        victim.Kill(EnemyAgent.QuickDeath);
                    }
                }
                float u = Mathf.InverseLerp(Shot3, Duration - HandBack, time);
                position = lowCamera + Vector3.up * (0.1f * u);
                rotation = Quaternion.LookRotation(victimFocus + Vector3.up * Mathf.Lerp(0.5f, 0.15f, u) - position);
                fov = Mathf.Lerp(40f, 34f, u);
            }

            // the last shot is ~70 m out on the slope: cut back into the player's eyes under the bars instead of
            // flying the camera back to the trench (2026-11-13, as CutsceneRig.CutBack)
            float back = time >= Duration - HandBack * 0.5f ? 1f : 0f;
            if (back >= 1f && !cutDone)
            {
                // the cut goes to the player's own camera, rifle and hands included
                cutDone = true;
                CutsceneRig.ShowPlayer(player, playerRenderers);
                sequenceCamera.enabled = false;
                playerCamera.enabled = true;
            }
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

        // the one who goes down: someone other than the man in the close shot if there is anyone, nearest the trench
        EnemyAgent Victim()
        {
            EnemyAgent best = null; float bestDistance = float.MaxValue;
            if (enemies == null) return subject;
            foreach (var e in enemies.ActiveEnemies)
            {
                if (e == null || !e.Alive || e == subject || e.Retreating) continue;
                float d = (e.transform.position - eye).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d; best = e;
            }
            return best != null ? best : subject;
        }

        static Vector3 Chest(EnemyAgent e, Vector3 fallback)
        {
            var a = e != null ? e.GetComponentInChildren<Animator>() : null;
            var b = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Chest) : null;
            return b != null ? b.position : fallback + Vector3.up * 1.2f;
        }

        static Vector3 Hips(EnemyAgent e)
        {
            var a = e.GetComponentInChildren<Animator>();
            var b = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
            return b != null ? b.position : e.transform.position + Vector3.up * 0.9f;
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
            lowCameraSet = false; closeSet = false; victimHit = false; victim = null;

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
            playerRenderers = CutsceneRig.HidePlayer(player);
            cutDone = false;
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
            CutsceneRig.ShowPlayer(player, playerRenderers);
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
