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
    //   shot 3  low on the slope: a shell is heard coming and bursts in front of the lens, two more close by,
    //           then back into the player's eyes under the bars
    //           (2026-11-23, 사용자 지시 "적을 클로즈업하는걸 빼고 그냥 포탄 떨어지는 걸로 클로즈업해서 보여줘도 될거 같아":
    //            달리는 적 근접 컷과 적이 맞아 쓰러지는 컷을 뺐다. 포탄은 ShellingDirector.TryStagedShell, 피해 없음)
    // While it plays the player has no control and cannot be hurt, and the run clock stands still; the enemy keep
    // moving and keep arriving, so the fight is already on when the view comes back. Once per run, expanded run
    // only (the exhibit run's 9:00 limit has no room for it). Space or Enter skips. Added by NarrativeDirector.
    public sealed class FlareRevealDirector : MonoBehaviour, IDebugPanel
    {
        public static bool Enabled = true;
        const float Shot1 = 2.6f, Shot2 = 4.8f, Duration = 10.8f, HandBack = 1.3f;
        const float LongLensFog = 0.4f;   // fog density during the long-lens shot, as a share of the night's
        float fogBefore;
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
        Vector3 flarePoint, eye, slopeFocus, focus;
        Quaternion eyeView;
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
                // low on the lit slope: a shell is heard coming and bursts in front of the lens, then a second close by
                if (!shellSet)
                {
                    shellSet = true;
                    if (shelling == null) shelling = FindFirstObjectByType<ShellingDirector>();
                    if (shelling == null || !shelling.TryStagedShell(slopeFocus, 3f, 16f, ShellLead, out shellPoint))
                    {
                        // nowhere open to put it: no empty shot of the slope, straight back to the player
                        shellPoint = OnGround(slopeFocus, 0f);
                        time = Mathf.Max(time, Duration - HandBack);
                    }
                    lastBurst = Shot2 + ShellLead;
                    shellLook = lookTarget = shellPoint;
                    var toTrench = eye - shellPoint; toTrench.y = 0f; toTrench.Normalize();
                    shellCamera = ClearestShellCamera(toTrench, Vector3.Cross(Vector3.up, toTrench));
                }
                // two more round the first, one after another: the smoke of one burst is gone in about a second,
                // and the shot stood empty until the cut back with a single round
                // (beyond the first crater's keep-out, on the far side of it from the lens, left then right; a round
                // that finds no open ground is tried again the next frame while there is still time to see it land)
                if (followed < FollowAfter.Length && time >= Shot2 + ShellLead + FollowAfter[followed] && time < Duration - HandBack - FollowLead - 0.3f && shelling != null)
                {
                    var away = shellPoint - shellCamera; away.y = 0f;
                    var toward = Quaternion.Euler(0f, followed % 2 == 0 ? -20f : 20f, 0f) * away.normalized;
                    // the second round finds one of them (2026-11-23, 사용자 지시 "포탄 떨어져서 적 죽는 것까지 하나")
                    if (followed == 0 && (struck = PickStruck()) != null)
                    {
                        hasStruck = true;
                        struckAt = struck.transform.position;
                        shelling.StagedIncoming(struckAt);
                        strikeAt = time + FollowLead;
                        followed++;
                        lastBurst = strikeAt;
                    }
                    else
                    {
                        // after a man has gone down the last round falls behind where he lies, as the lens sees it
                        var behind = struckAt - shellCamera; behind.y = 0f;
                        if (shelling.TryStagedShell(hasStruck ? struckAt : shellPoint, 5.3f, 7.5f, FollowLead, out var next, hasStruck ? behind.normalized : toward))
                        {
                            // the lens turns to where it will land while it is heard coming (it fell at the frame's
                            // edge); not away from the man who has just gone down
                            if (!hasStruck) lookTarget = next;
                            followed++;
                            lastBurst = time + FollowLead;
                        }
                    }
                }
                if (hasStruck)
                {
                    // (his agent is gone from the scene soon after he falls: the place is kept)
                    if (struck != null) struckAt = struck.transform.position;
                    lookTarget = struckAt;
                    if (strikeAt >= 0f && time >= strikeAt)
                    {
                        strikeAt = -1f;
                        // beside and a little beyond him as the lens sees it: he goes down next to the burst, not
                        // inside its smoke
                        var beyond = struckAt - shellCamera; beyond.y = 0f; beyond.Normalize();
                        shelling.StagedBlast(struckAt + beyond * 1.0f + Vector3.Cross(Vector3.up, beyond) * 1.9f);
                        if (struck != null && struck.Alive) struck.Kill(EnemyAgent.QuickDeath);
                    }
                }
                // nothing more to come and the smoke has thinned: back to the player rather than hold an empty slope
                if (time > lastBurst + AfterLast && (followed >= FollowAfter.Length || time > Shot2 + ShellLead + FollowAfter[followed] + 0.8f))
                    time = Mathf.Max(time, Duration - HandBack);
                float u = Mathf.InverseLerp(Shot2, Duration - HandBack, time);
                // the lens waits on the bare ground, then lifts with the column of earth
                float risen = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Shot2 + ShellLead, Shot2 + ShellLead + 0.7f, time));
                position = shellCamera + Vector3.up * (0.1f * u);
                shellLook = Vector3.Lerp(shellLook, lookTarget, 1f - Mathf.Exp(-Time.deltaTime * 3.5f));
                rotation = Quaternion.LookRotation(shellLook + Vector3.up * (hasStruck ? 0.9f : Mathf.Lerp(0.4f, 1.7f, risen)) - position);
                // on the man the round is coming for, the lens closes in: he stands a good quarter of the frame high
                float want = hasStruck ? Mathf.Clamp(2f * Mathf.Atan(3.0f / Mathf.Max(6f, Vector3.Distance(position, struckAt))) * Mathf.Rad2Deg, 5f, 38f) : Mathf.Lerp(40f, 36f, u);
                shellFov = shellFov <= 0f ? want : Mathf.Lerp(shellFov, want, 1f - Mathf.Exp(-Time.deltaTime * 3f));
                fov = shellFov;
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
            // 2026-11-16: the two close shots stand right under the flare, where the ground burnt out to plain white
            // (243 of 255). Those shots are stopped down; the trench shots keep the night exposure.
            EnsureStopDown();
            stopDown.weight = time >= Shot2 && back < 1f ? 1f : 0f;
            // 2026-11-16: the long lens looks 50-70 m down the slope through the night haze, which washed it out to a
            // flat grey. A long lens compresses haze rather than adding it, so the shot thins the fog while it is on.
            RenderSettings.fogDensity = fogBefore * (time >= Shot1 && time < Shot2 ? LongLensFog : 1f);
            var own = playerCamera.transform;
            sequenceCamera.transform.SetPositionAndRotation(Vector3.Lerp(position, own.position, back), Quaternion.Slerp(rotation, own.rotation, back));
            sequenceCamera.fieldOfView = Mathf.Lerp(fov, playerCamera.fieldOfView, back);
            bars.alpha = Mathf.Min(Mathf.InverseLerp(0f, BarFade, time), Mathf.InverseLerp(Duration, Duration - BarFade, time));
            if (time >= Duration) Finish();
        }

        // The shell camera stands still on the slope while the enemy keep running: of a few places on the trench's
        // side of the burst, it takes the one with a clear view of where the shell falls that no runner passes
        // close to (a man ran straight through the lens of the earlier low shot, 2026-11-15).
        const float ShellLead = 1.3f, FollowLead = 0.9f, AfterLast = 1.3f, RunSpeed = 3.4f, ShellCameraHeight = 0.9f;
        float lastBurst, strikeAt = -1f, shellFov;
        bool hasStruck;
        Vector3 struckAt;
        Vector3 shellLook, lookTarget;
        EnemyAgent struck;

        // The man the second round finds: alive and coming on, 7-70 m from the lens (the long lens closes in), toward what it
        // sees and not behind cover.
        EnemyAgent PickStruck()
        {
            if (enemies == null) return null;
            var view = shellLook - shellCamera; view.y = 0f;
            EnemyAgent best = null; float bestAngle = 50f;
            foreach (var e in enemies.ActiveEnemies)
            {
                if (e == null || !e.Alive || e.Retreating) continue;
                var to = e.transform.position - shellCamera;
                float distance = to.magnitude;
                to.y = 0f;
                if (distance < 7f || distance > 70f) continue;
                float angle = Vector3.Angle(view, to);
                if (angle >= bestAngle || Covered(shellCamera, e.transform.position + Vector3.up * 1.0f)) continue;
                bestAngle = angle; best = e;
            }
            return best;
        }
        static readonly float[] FollowAfter = { 0.4f, 1.5f };   // seconds after the first burst that the next are sent for
        ShellingDirector shelling;
        Vector3 shellPoint, shellCamera;
        bool shellSet;
        int followed;

        Vector3 ClearestShellCamera(Vector3 toTrench, Vector3 side)
        {
            // about 8 m off. The uphill places come into it too: from below, a fold of the slope hid the ground
            // where the shell fell and only the top of the smoke stood over it (2026-11-23)
            var places = new[]
            {
                toTrench * 5.5f + side * 6f, toTrench * 5.5f - side * 6f,
                toTrench * 2f + side * 8f, toTrench * 2f - side * 8f,
                toTrench * -4f + side * 7f, toTrench * -4f - side * 7f,
                toTrench * 8f + side * 2f,
            };
            int best = 0; float bestScore = float.MaxValue;
            for (int i = 0; i < places.Length; i++)
            {
                var camera = OnGround(shellPoint + places[i], ShellCameraHeight);
                float score = i * 0.01f; // the first is the composed one: it wins a tie
                if (Covered(camera, OnGround(shellPoint, 0.4f)) || Covered(camera, OnGround(shellPoint, 2.5f))) score += 10f;
                // the ground itself between the lens and the foot of the burst
                var foot = OnGround(shellPoint, 0.25f);
                if (Physics.Linecast(camera, foot, out var ground, FiveRivers.Core.WorldQuery.Solid, QueryTriggerInteraction.Ignore)
                    && ground.collider is TerrainCollider && Vector3.Distance(ground.point, foot) > 0.6f) score += 8f;
                if (enemies != null)
                    foreach (var e in enemies.ActiveEnemies)
                    {
                        if (e == null || !e.Alive) continue;
                        var from = e.transform.position; var way = e.transform.forward; way.y = 0f;
                        // how near his run over the rest of the shot takes him to the lens
                        float along = Mathf.Clamp(Vector3.Dot(camera - from, way), 0f, RunSpeed * (Duration - Shot2));
                        var gap = camera - (from + way * along); gap.y = 0f;
                        if (gap.magnitude < 2.5f) score += 2.5f - gap.magnitude;
                    }
                if (score >= bestScore) continue;
                bestScore = score; best = i;
            }
            return OnGround(shellPoint + places[best], ShellCameraHeight);
        }

        // Something solid (not one of the enemy) stands between the camera and his chest, or right in front of the lens.
        static readonly RaycastHit[] coverHits = new RaycastHit[16];
        static bool Covered(Vector3 camera, Vector3 chest)
        {
            var to = chest - camera;
            float distance = to.magnitude;
            if (distance < 0.5f) return false;
            int n = Physics.SphereCastNonAlloc(camera - to / distance * 0.3f, 0.3f, to / distance, coverHits, distance - 0.3f, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = coverHits[i].collider;
                if (c == null || c is TerrainCollider || c.GetComponentInParent<EnemyAgent>() != null) continue;
                return true;
            }
            return false;
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
            fogBefore = RenderSettings.fogDensity;
            eye = playerCamera.transform.position;
            // the slope under the flare, as seen from the trench
            slopeFocus = OnGround(new Vector3(flarePoint.x, 0f, flarePoint.z), 1f);
            var ahead = slopeFocus - eye; ahead.y = 0f;
            eyeView = ahead.sqrMagnitude > 1f ? Quaternion.LookRotation(ahead) : Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
            // the player comes out of it facing the lit slope
            if (ahead.sqrMagnitude > 1f) player.transform.rotation = Quaternion.LookRotation(ahead);
            subject = Nearest();
            focus = subject != null ? subject.transform.position : slopeFocus;
            shellSet = false; followed = 0; struck = null; strikeAt = -1f; hasStruck = false; shellFov = 0f;

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

        // the exposure of the close shots themselves (the night grade is 1.8): about 1.2 stops down
        public static float CloseShotStops = 0.6f;
        UnityEngine.Rendering.Volume stopDown;

        void EnsureStopDown()
        {
            if (stopDown != null) return;
            var holder = new GameObject("FlareRevealStopDown");
            holder.transform.SetParent(transform, false);
            // on the layer of the scene's own grading volume, so the cameras that see that one see this one
            var existing = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            if (existing != null) holder.layer = existing.gameObject.layer;
            stopDown = holder.AddComponent<UnityEngine.Rendering.Volume>();
            stopDown.isGlobal = true;
            stopDown.priority = 100f;
            stopDown.weight = 0f;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var grade = profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(CloseShotStops);
            stopDown.profile = profile;
        }

        void Finish()
        {
            if (stopDown != null) stopDown.weight = 0f;
            if (fogBefore > 0f) RenderSettings.fogDensity = fogBefore;
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
            // (objective may already be gone when this runs from OnDestroy as the scene unloads)
            if (inAssault && copy != null && objective != null) objective.Show(copy.objectiveAssault);
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
