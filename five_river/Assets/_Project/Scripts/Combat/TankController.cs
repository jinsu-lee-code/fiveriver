using System;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.DebugTools;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FiveRivers.Combat
{
    // T-34-85 (S5). 도로를 따라 내려와 참호 정면에 멈추고, 포탑을 돌려 플레이어 근처로 포격한다.
    // 로켓 판정: 정면 도탄 / 측면 1발 연기 / 측면 2발 정지 · 화재 (GDD 5-5). 아군 지원이면 포격에 맞아 멈춘다.
    public sealed class TankController : MonoBehaviour, IRocketTarget, IDebugPanel
    {
        enum State { Hidden, Approach, Holding, Stopped }

        [SerializeField] FlowDirector director;
        [SerializeField] TankConfig config;
        [SerializeField] LevelPoints points;
        [SerializeField] Effects effects;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] Transform body;      // PF_VH_T34_85 인스턴스 (보였다 숨겼다 하는 쪽)
        [SerializeField] Transform turret;
        [SerializeField] Transform gunMuzzle;
        [SerializeField] GameObject smoke;
        [SerializeField] GameObject fire;
        [SerializeField] Renderer[] bodyRenderers;
        [SerializeField] Material burntMaterial;
        [Header("아군 지원 연출 (5초 안에 끝나게)")]
        [SerializeField] int allySupportBlasts = 3;
        [SerializeField] float allyBlastFirstDelay = 0.6f;
        [SerializeField] float allyBlastInterval = 1.2f;

        // Terrain contact preserves authored approach and final turn.
        [SerializeField] Terrain driveTerrain;
        [SerializeField] float groundClearance = 0f;
        [SerializeField] Vector2 trackHalfFootprint = new Vector2(1.22f, 2.6f);

        [Header("주포 고각 (T-34-85 ZiS-S-53 실제 범위)")]
        [SerializeField] float gunDepression = -5f;
        [SerializeField] float gunElevation = 22f;
        [SerializeField] float aimHeight = 1.4f;   // 플레이어 발 기준 겨누는 높이

        [Header("파괴 연출: 폭발 섬광 뒤 파괴 모델로 교체 (없으면 그을린 재질)")]
        [SerializeField] GameObject intactModel;     // PF_VH_T34_85/Model
        [SerializeField] GameObject destroyedModel;  // SM_VH_T34_85_Destroyed 인스턴스
        [SerializeField] float destroyedSwapDelay = 0.15f;

        Material[] normalMaterials;
        ShellCraters craters;
        public ShellCraters Craters => craters;
        Transform gun;
        Quaternion gunRest;
        Quaternion turretBasis;
        Quaternion turretRest;
        float gunPitch;
        float destroyedSwapAt = -1f;
        State state = State.Hidden;
        float nextFire;
        bool openedFire;
        float sightWaitUntil = -1f;
        float nextAllyBlast;
        int allyBlastsLeft;
        float approachElapsed;
        // 조준해 들어옴 (2026-11-24): 노출된 채 쏘면 멀리 → 가까이 → 직격
        TankGunnery gunnery;
        Vector3 lastShotPlayerAt;
        bool hasLastShot;
        static readonly RaycastHit[] sightHits = new RaycastHit[24];
        // 쏘기 전 예고 (2026-11-24): 포탑 회전 소리 + 마지막 조정. 판정은 쏘는 순간 그대로
        TankFireWarning warning;
        RunCoordinator coordinator;
        float appliedNudgeYaw;
        public bool WarningActive => warning != null && warning.Active;

        public int ShotsFired { get; private set; }
        public GunneryShot LastShot { get; private set; }
        public bool LastShotExposed { get; private set; }
        public float LastShotMissDistance { get; private set; }   // 플레이어에서 착탄점까지 수평 m
        public Vector3 LastImpact { get; private set; }
        public int AimStage => gunnery != null ? gunnery.Stage : 0;

        public int SideHits { get; private set; }
        public bool IsStopped => state == State.Stopped;
        public bool IsMoving => state == State.Approach;
        public bool Visible => state != State.Hidden;

        public event Action Ricocheted;
        public event Action<int> SideHit;
        public event Action Stopped;
        public event Action CannonFired;
        // 포탄이 떨어진 자리 · 가까이(센 흔들림) 여부. 몸으로 느끼는 효과는 ShellingDirector가 맡는다.
        public event Action<Vector3, bool> ShellLanded;
        public event Action AllySupportStarted;

        void Awake()
        {
            normalMaterials = new Material[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) normalMaterials[i] = bodyRenderers[i].sharedMaterial;
            // 포구 소켓은 Gun 밑에 있다 (TankBuilder). Gun이 Turret 밑이면 고각을 준다.
            gun = gunMuzzle != null && gunMuzzle.parent != turret ? gunMuzzle.parent : null;
            if (gun != null) gunRest = gun.localRotation;
            turretBasis = Quaternion.Inverse(body.rotation) * turret.rotation;
            turretRest = turret.localRotation;
            // wheels turn and the track belts run with the distance the hull travels (TankRunningGear)
            body.gameObject.AddComponent<TankRunningGear>().Build(body);
            // where its rounds land the ground is blown out (ShellCraters, undone on a new run or a retry)
            gunnery = new TankGunnery(new GunneryRules
            {
                ShotsToDirect = config.shotsToDirect, FarMin = config.farMin, FarMax = config.farMax, MidMin = config.midMin, MidMax = config.midMax,
                DirectScatter = config.directScatter, Lateral = config.aimLateral, MoveReset = config.aimMoveReset,
            });
            warning = new TankFireWarning(config.warnLead, config.warnNudgeDelay, config.warnNudgeSeconds, config.warnNudgeYaw, config.warnNudgePitch);
            craters = gameObject.AddComponent<ShellCraters>();
            craters.Bind(director, config, driveTerrain);
            Hide();
        }

        void Start()
        {
            coordinator = FindFirstObjectByType<RunCoordinator>();
            director.Flow.SectionChanged += OnSectionChanged;
            director.Flow.RetryRequested += OnRetry;
        }

        void OnSectionChanged(FlowTransition t)
        {
            switch (t.To)
            {
                case FlowSection.Tank:
                    if (t.From != FlowSection.Tank) Appear(points.tankStart);
                    break;
                case FlowSection.AllySupport:
                    if (state == State.Hidden) Appear(points.tankHold);
                    if (state != State.Stopped)
                    {
                        allyBlastsLeft = allySupportBlasts;
                        nextAllyBlast = Time.time + allyBlastFirstDelay;
                        AllySupportStarted?.Invoke();
                    }
                    break;
                case FlowSection.Title:
                case FlowSection.Tutorial:
                case FlowSection.Scout:
                case FlowSection.Assault:
                case FlowSection.Silence:
                    Hide();
                    break;
            }
        }

        // CP3 복구: 손상 · 연기 · 화재를 지우고 정지 위치에서 다시 (FLOW.md 4번)
        void OnRetry(FlowCheckpoint checkpoint)
        {
            if (checkpoint == FlowCheckpoint.Tank) Appear(points.tankHold);
        }

        void Appear(Transform at)
        {
            Scripted = false;
            body.gameObject.SetActive(true);
            body.SetPositionAndRotation(at.position, at.rotation);
            if (driveTerrain != null) AlignWithGround();
            turret.localRotation = turretRest;
            appliedNudgeYaw = 0f;
            warning.Cancel();
            gunPitch = 0f;
            if (gun != null) gun.localRotation = gunRest;
            destroyedSwapAt = -1f;
            if (intactModel != null) intactModel.SetActive(true);
            if (destroyedModel != null) destroyedModel.SetActive(false);
            state = at == points.tankHold ? State.Holding : State.Approach;
            approachElapsed = 0f;
            SideHits = 0;
            smoke.SetActive(false);
            fire.SetActive(false);
            for (int i = 0; i < bodyRenderers.Length; i++) bodyRenderers[i].sharedMaterial = normalMaterials[i];
            openedFire = false;
            sightWaitUntil = -1f;
            // a full interval before the first shot (it was 0.6 of one, 5.4 s): from getting control back he needs
            // about 6 s to reach the trench end, change to the bazooka and aim, so the first shell, which kills about
            // every other time, always came before he could fire (played through by hand, 2026-11-17 night)
            nextFire = Time.time + config.fireInterval;
            allyBlastsLeft = 0;
            gunnery.Reset();
            hasLastShot = false;
        }

        void Hide()
        {
            Scripted = false;
            warning.Cancel();
            appliedNudgeYaw = 0f;
            state = State.Hidden;
            allyBlastsLeft = 0;
            body.gameObject.SetActive(false);
        }

        void Update()
        {
            if (state == State.Hidden) return;

            if (allyBlastsLeft > 0 && Time.time >= nextAllyBlast)
            {
                allyBlastsLeft--;
                var p = body.position + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                effects.Spawn(EffectKind.CannonBlast, p, Quaternion.identity);
                nextAllyBlast = Time.time + allyBlastInterval;
                if (allyBlastsLeft == 0) Stop();
            }

            if (destroyedSwapAt >= 0f && Time.time >= destroyedSwapAt)
            {
                destroyedSwapAt = -1f;
                intactModel.SetActive(false);
                destroyedModel.SetActive(true);
            }

            if (state == State.Stopped) return;
            // the entrance sequence is driving the tank: no approach, no turret tracking, no fire of its own
            if (Scripted) return;

            if (state == State.Approach)
            {
                approachElapsed += Time.deltaTime;
                float routeLength = Vector3.Distance(points.tankStart.position, points.tankHold.position);
                float duration = routeLength / config.approachSpeed;
                float progress = duration > 0f ? approachElapsed / duration : 1f;
                SampleApproach(progress, points.tankStart.position, points.tankStart.rotation,
                    points.tankHold.position, points.tankHold.rotation, out var position, out var rotation);
                body.SetPositionAndRotation(position, rotation);
                if (driveTerrain != null) AlignWithGround();
                if (progress >= 1f) state = State.Holding;
            }

            var toPlayer = player.transform.position - turret.position;
            var turretUp = driveTerrain != null ? body.up : Vector3.up;
            toPlayer = Vector3.ProjectOnPlane(toPlayer, turretUp);
            // 지난 프레임의 예고 조정을 벗기고 추적한 뒤 이번 조정을 얹는다 (조정이 추적 상태에 쌓이지 않게)
            UndoNudge(turretUp);
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(toPlayer, turretUp) * turretBasis;
                turret.rotation = Quaternion.RotateTowards(turret.rotation, want, config.turretTurnSpeed * Time.deltaTime);
            }
            // 쏘기 warnLead 초 전 예고 (2026-11-24): 소리는 AudioDirector 가 WarningActive 로 튼다. 같은 조건에서만 알린다.
            // 등장 연출(Scripted)은 위에서 이미 빠졌고, 재시도 · 사망으로 화면이 어두운 동안에는 알리지 않는다.
            bool mayFire = !director.Flow.CombatLocked && director.Flow.Section == FlowSection.Tank;
            bool dark = coordinator != null && coordinator.Fade > 0.05f;
            warning.Tick(Time.time, nextFire, mayFire && !dark);
            float nudge = warning.NudgeYaw(Time.time);
            if (nudge != 0f)
            {
                turret.rotation = Quaternion.AngleAxis(nudge, turretUp) * turret.rotation;
                appliedNudgeYaw = nudge;
            }
            AimGun(turretUp);

            if (mayFire && Time.time >= nextFire)
            {
                // 2026-11-24: 시선이 막혀 있어도 기다리지 않는다. 쏘는 순간 노출 여부로 조준이 정해진다 (FireCannon).
                openedFire = true;
                nextFire = Time.time + config.fireInterval;
                FireCannon();
                warning.Fired();
            }
        }

        // 예고 때 얹은 포탑 조정을 벗긴다 (추적 상태에 쌓이지 않게)
        void UndoNudge(Vector3 turretUp)
        {
            if (appliedNudgeYaw == 0f) return;
            turret.rotation = Quaternion.AngleAxis(-appliedNudgeYaw, turretUp) * turret.rotation;
            appliedNudgeYaw = 0f;
        }

        // 포탑이 좌우를 맡고, 주포는 포탑 오른쪽 축으로만 위아래를 겨눈다 (포탑과 같은 속도).
        void AimGun(Vector3 turretUp)
        {
            if (gun == null) return;
            var aim = player.transform.position + Vector3.up * aimHeight - gun.position;
            var flat = Vector3.ProjectOnPlane(aim, turretUp);
            float want = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(aim, turretUp), flat.magnitude) * Mathf.Rad2Deg, gunDepression, gunElevation);
            gunPitch = Mathf.MoveTowards(gunPitch, want, config.turretTurnSpeed * Time.deltaTime);
            gun.rotation = Quaternion.AngleAxis(-(gunPitch + warning.NudgePitch(Time.time)), turret.right) * (turret.rotation * gunRest);
        }

        // 기존 전체 접근 시간은 유지한다. 앞 75%에 직진하고, 마지막 25%는
        // 정지 지점에서 선회하여 차체를 옆으로 미끄러뜨리지 않는다.
        public static void SampleApproach(float progress, Vector3 startPosition, Quaternion startRotation,
            Vector3 holdPosition, Quaternion holdRotation, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.Lerp(startPosition, holdPosition, Mathf.Clamp01(progress / 0.75f));
            rotation = Quaternion.Slerp(startRotation, holdRotation, Mathf.InverseLerp(0.75f, 1f, progress));
        }

        // 지형을 바꾸거나 차체 형상을 고치지 않고, 양 궤도 접지점의 높이로 차체만 정렬한다.
        void AlignWithGround()
        {
            var driveHeading = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
            if (driveHeading.sqrMagnitude < 0.0001f) return;
            var p = body.position;
            var right = Vector3.Cross(Vector3.up, driveHeading);
            var front = GroundPoint(p + driveHeading * trackHalfFootprint.y);
            var back = GroundPoint(p - driveHeading * trackHalfFootprint.y);
            var left = GroundPoint(p - right * trackHalfFootprint.x);
            var rightPoint = GroundPoint(p + right * trackHalfFootprint.x);
            var forward = (front - back).normalized;
            var up = Vector3.Cross(forward, rightPoint - left).normalized;
            p.y = (front.y + back.y + left.y + rightPoint.y) * 0.25f + groundClearance;
            body.SetPositionAndRotation(p, Quaternion.LookRotation(forward, up));
        }

        Vector3 GroundPoint(Vector3 p)
        {
            p.y = driveTerrain.SampleHeight(p) + driveTerrain.transform.position.y;
            return p;
        }

        // 포구에서 플레이어 머리(카메라)까지 시선이 지형 · 흉벽 · 엄폐물에 막히지 않으면 노출.
        // 전차 자신과 플레이어 몸체는 막지 않고, 플레이어만 막는 벽(레이어 8)은 WorldQuery.All이 이미 뺀다.
        public bool PlayerExposed()
        {
            var from = gunMuzzle.position;
            var head = player.CameraPivot.position;
            var dir = head - from;
            float distance = dir.magnitude;
            if (distance < 0.05f) return true;
            int n = Physics.RaycastNonAlloc(from, dir / distance, sightHits, distance, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = sightHits[i].transform;
                if (t.IsChildOf(body) || t.IsChildOf(player.transform)) continue;
                return false;
            }
            return true;
        }

        // 위협 사격 (2026-11-24 규칙): 노출된 채면 조준이 좁혀 들어와 셋째 발이 직격, 직격이면 즉사.
        // 빗나간 포탄은 아무리 가까워도 체력 피해 0 (흔들림 · 먹먹함 · 흙만). 수치는 TankConfig, 순서는 TankGunnery.
        void FireCannon()
        {
            var target = player.transform.position;
            bool exposed = PlayerExposed();
            float moved = hasLastShot ? Vector2.Distance(new Vector2(target.x, target.z), new Vector2(lastShotPlayerAt.x, lastShotPlayerAt.z)) : 0f;
            var shot = gunnery.Next(exposed, moved, () => Random.value);
            lastShotPlayerAt = target;
            hasLastShot = true;

            var axis = target - body.position; axis.y = 0f;
            axis = axis.sqrMagnitude > 0.01f ? axis.normalized : Vector3.forward;
            var side = Vector3.Cross(Vector3.up, axis);
            float along = shot.Beyond ? shot.Distance : -shot.Distance;
            var impact = target + axis * along + side * shot.Lateral;
            if (Physics.Raycast(impact + Vector3.up * 20f, Vector3.down, out var hit, 40f, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore))
                impact = hit.point;
            if (shot.Direct) impact = new Vector3(impact.x, Mathf.Max(impact.y, target.y - 0.05f), impact.z);

            ShotsFired++;
            LastShot = shot;
            LastShotExposed = exposed;
            LastImpact = impact;
            LastShotMissDistance = Vector2.Distance(new Vector2(impact.x, impact.z), new Vector2(target.x, target.z));

            effects.Spawn(EffectKind.TankMuzzle, gunMuzzle.position, gunMuzzle.rotation);
            effects.Spawn(EffectKind.CannonBlast, impact, Quaternion.identity);
            craters.Make(impact);
            CannonFired?.Invoke();
            ShellLanded?.Invoke(impact, shot.Direct || LastShotMissDistance <= config.feelCloseRange);
            if (shot.Direct) playerHealth.Kill(impact);
        }

        // ---- Entrance sequence (TankEntranceDirector) ----
        // While Scripted the tank only goes where it is put. It can still be hit and stopped.
        public bool Scripted { get; private set; }
        public int EntranceTake => config.entranceTake;
        public Vector3 RouteStart => points.tankStart.position;
        public Vector3 RouteHold => points.tankHold.position;
        public Quaternion RouteStartRotation => points.tankStart.rotation;
        public Vector3 MuzzlePosition => gunMuzzle.position;
        public Vector3 TurretPosition => turret.position;

        public void BeginScripted()
        {
            if (state == State.Hidden || state == State.Stopped) return;
            UndoNudge(driveTerrain != null ? body.up : Vector3.up);
            warning.Cancel();
            Scripted = true;
        }

        // position: on the ground plane (height comes from the terrain). hullYaw, turretYaw: world headings in
        // degrees. gunPitch: degrees above the turret's own level, within the real gun's range.
        public void SetScriptedPose(Vector3 position, float hullYaw, float turretYaw, float pitch)
        {
            if (!Scripted) return;
            body.SetPositionAndRotation(position, Quaternion.Euler(0f, hullYaw, 0f));
            if (driveTerrain != null) AlignWithGround();
            var turretUp = driveTerrain != null ? body.up : Vector3.up;
            var heading = Vector3.ProjectOnPlane(Quaternion.Euler(0f, turretYaw, 0f) * Vector3.forward, turretUp);
            if (heading.sqrMagnitude > 0.0001f) turret.rotation = Quaternion.LookRotation(heading, turretUp) * turretBasis;
            if (gun == null) return;
            gunPitch = Mathf.Clamp(pitch, gunDepression, gunElevation);
            gun.rotation = Quaternion.AngleAxis(-gunPitch, turret.right) * (turret.rotation * gunRest);
        }

        // A staged shot: flash, report and blast, no damage (the player has no control during the sequence).
        public void ScriptedFire(Vector3 impact)
        {
            if (!Scripted) return;
            effects.Spawn(EffectKind.TankMuzzle, gunMuzzle.position, gunMuzzle.rotation);
            effects.Spawn(EffectKind.CannonBlast, impact, Quaternion.identity);
            craters.Make(impact);
            CannonFired?.Invoke();
        }

        // Hands the tank back where the sequence left it: it carries on down the same road from there.
        public void EndScripted()
        {
            if (!Scripted) return;
            Scripted = false;
            if (state == State.Hidden || state == State.Stopped) return;
            var route = points.tankHold.position - points.tankStart.position;
            float along = route.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(body.position - points.tankStart.position, route) / route.sqrMagnitude) : 1f;
            // SampleApproach drives the straight run over the first 75% of the time and turns on the spot after
            float duration = route.magnitude / config.approachSpeed;
            approachElapsed = along * 0.75f * duration;
            // The take can leave the tank on its stopping place still heading down the road. Handed straight to Holding
            // it never made the turn that shows its side: from the trench end the game sends the player to, he stood 35
            // degrees off its front and every rocket glanced off, until a death or the attempt limit put the tank back in
            // its hold pose (played through by hand, 2026-11-17 night). It holds only once it has turned.
            var holdHeading = Vector3.ProjectOnPlane(points.tankHold.forward, Vector3.up);
            var heading = Vector3.ProjectOnPlane(body.forward, Vector3.up);
            bool turned = Vector3.Angle(heading, holdHeading) < 2f;
            state = along >= 1f && turned ? State.Holding : State.Approach;
            openedFire = false;
            sightWaitUntil = -1f;
            // a full interval before the first shot (it was 0.6 of one, 5.4 s): from getting control back he needs
            // about 6 s to reach the trench end, change to the bazooka and aim, so the first shell, which kills about
            // every other time, always came before he could fire (played through by hand, 2026-11-17 night)
            nextFire = Time.time + config.fireInterval;
            gunnery.Reset();
            warning.Cancel();
            hasLastShot = false;
        }

        public void OnRocketHit(Vector3 point, Vector3 shooterPosition)
        {
            if (state == State.Hidden || state == State.Stopped) return;
            var result = TankHitJudge.Judge(body.position, body.forward, shooterPosition, config.sideAngle);
            if (result == TankHitResult.Ricochet)
            {
                effects.Spawn(EffectKind.TankRicochet, point, Quaternion.identity);
                Ricocheted?.Invoke();
                return;
            }
            SideHits++;
            effects.Spawn(EffectKind.TankHitExplosion, point, Quaternion.identity);
            SideHit?.Invoke(SideHits);
            if (SideHits >= config.hitsToStop) Stop();
            else smoke.SetActive(true);
        }

        void Stop()
        {
            if (state == State.Stopped) return;
            UndoNudge(driveTerrain != null ? body.up : Vector3.up);
            warning.Cancel();
            state = State.Stopped;
            allyBlastsLeft = 0;
            smoke.SetActive(false);
            fire.SetActive(true);
            // 측면 2발째: 큰 폭발 → 섬광에 가려지는 사이 파괴 모델로 교체 (모델이 없으면 그을린 재질)
            effects.Spawn(EffectKind.TankDestroyed, body.position + body.up * 1.6f, body.rotation);
            if (intactModel != null && destroyedModel != null) destroyedSwapAt = Time.time + destroyedSwapDelay;
            else foreach (var r in bodyRenderers) r.sharedMaterial = burntMaterial;
            Stopped?.Invoke();
            director.Flow.ReportGoalMet(FlowSection.Tank);
        }

        public string DebugTitle => "전차";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var flow = director.Flow;
            GUILayout.Label($"상태 {state} · 측면 명중 {SideHits}/{config.hitsToStop} · 시도 {flow.TankFailures + 1} ({flow.TankAttemptTime:0}/{flow.TankAttemptLimit:0}초)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("정면 명중")) OnRocketHit(body.position + body.forward * 3f, body.position + body.forward * 30f);
            if (GUILayout.Button("측면 명중")) OnRocketHit(body.position - body.right * 1.5f, body.position - body.right * 30f);
            if (GUILayout.Button("즉시 정지")) Stop();
            GUILayout.EndHorizontal();
#endif
        }
    }
}
