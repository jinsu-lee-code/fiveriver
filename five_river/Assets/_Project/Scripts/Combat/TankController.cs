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

        public int SideHits { get; private set; }
        public bool IsStopped => state == State.Stopped;
        public bool IsMoving => state == State.Approach;
        public bool Visible => state != State.Hidden;

        public event Action Ricocheted;
        public event Action<int> SideHit;
        public event Action Stopped;
        public event Action CannonFired;
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
            Hide();
        }

        void Start()
        {
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
            body.gameObject.SetActive(true);
            body.SetPositionAndRotation(at.position, at.rotation);
            if (driveTerrain != null) AlignWithGround();
            turret.localRotation = turretRest;
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
            nextFire = Time.time + config.fireInterval * 0.6f;
            allyBlastsLeft = 0;
        }

        void Hide()
        {
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
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(toPlayer, turretUp) * turretBasis;
                turret.rotation = Quaternion.RotateTowards(turret.rotation, want, config.turretTurnSpeed * Time.deltaTime);
            }
            AimGun(turretUp);

            if (!director.Flow.CombatLocked && director.Flow.Section == FlowSection.Tank && Time.time >= nextFire)
            {
                // Establish sight of the gun once on each appearance. Later shots still threaten cover.
                // The player's own rig never blocks the sight line, and the tank stops waiting after 2 s.
                if (!openedFire && Physics.Linecast(player.CameraPivot.position, gunMuzzle.position,
                        out var obstruction, ~0, QueryTriggerInteraction.Ignore)
                    && !obstruction.transform.IsChildOf(body) && !obstruction.transform.IsChildOf(player.transform))
                {
                    if (sightWaitUntil < 0f) sightWaitUntil = Time.time + 2f;
                    if (Time.time < sightWaitUntil)
                    {
                        nextFire = Time.time + 0.25f;
                        return;
                    }
                }
                openedFire = true;
                nextFire = Time.time + config.fireInterval;
                FireCannon();
            }
        }

        // 포탑이 좌우를 맡고, 주포는 포탑 오른쪽 축으로만 위아래를 겨눈다 (포탑과 같은 속도).
        void AimGun(Vector3 turretUp)
        {
            if (gun == null) return;
            var aim = player.transform.position + Vector3.up * aimHeight - gun.position;
            var flat = Vector3.ProjectOnPlane(aim, turretUp);
            float want = Mathf.Clamp(Mathf.Atan2(Vector3.Dot(aim, turretUp), flat.magnitude) * Mathf.Rad2Deg, gunDepression, gunElevation);
            gunPitch = Mathf.MoveTowards(gunPitch, want, config.turretTurnSpeed * Time.deltaTime);
            gun.rotation = Quaternion.AngleAxis(-gunPitch, turret.right) * (turret.rotation * gunRest);
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

        // 위협 사격: 플레이어 근처(빗나감 거리)에 떨어진다. 폭발 반경 안이면 피해 (실패 보조 배율 적용)
        void FireCannon()
        {
            var target = player.transform.position;
            var offset = Random.insideUnitCircle.normalized * config.nearMissOffset * Random.Range(0.5f, 1.2f);
            var impact = target + new Vector3(offset.x, 0f, offset.y);
            if (Physics.Raycast(impact + Vector3.up * 20f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                impact = hit.point;
            effects.Spawn(EffectKind.TankMuzzle, gunMuzzle.position, gunMuzzle.rotation);
            effects.Spawn(EffectKind.CannonBlast, impact, Quaternion.identity);
            if (Vector3.Distance(impact, target) <= config.cannonRadius)
                playerHealth.TakeDamage(config.cannonDamage * director.EnemyDamageScale, impact);
            CannonFired?.Invoke();
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
