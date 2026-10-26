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

        Material[] normalMaterials;
        State state = State.Hidden;
        float nextFire;
        float nextAllyBlast;
        int allyBlastsLeft;

        public int SideHits { get; private set; }
        public bool IsStopped => state == State.Stopped;
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
            turret.localRotation = Quaternion.identity;
            state = at == points.tankHold ? State.Holding : State.Approach;
            SideHits = 0;
            smoke.SetActive(false);
            fire.SetActive(false);
            for (int i = 0; i < bodyRenderers.Length; i++) bodyRenderers[i].sharedMaterial = normalMaterials[i];
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

            if (state == State.Stopped) return;

            if (state == State.Approach)
            {
                body.position = Vector3.MoveTowards(body.position, points.tankHold.position, config.approachSpeed * Time.deltaTime);
                if (Vector3.Distance(body.position, points.tankHold.position) < 0.05f) state = State.Holding;
            }

            var toPlayer = player.transform.position - turret.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                var want = Quaternion.LookRotation(toPlayer);
                turret.rotation = Quaternion.RotateTowards(turret.rotation, want, config.turretTurnSpeed * Time.deltaTime);
            }

            if (!director.Flow.CombatLocked && director.Flow.Section == FlowSection.Tank && Time.time >= nextFire)
            {
                nextFire = Time.time + config.fireInterval;
                FireCannon();
            }
        }

        // 위협 사격: 플레이어 근처(빗나감 거리)에 떨어진다. 폭발 반경 안이면 피해 (실패 보조 배율 적용)
        void FireCannon()
        {
            var target = player.transform.position;
            var offset = Random.insideUnitCircle.normalized * config.nearMissOffset * Random.Range(0.5f, 1.2f);
            var impact = target + new Vector3(offset.x, 0f, offset.y);
            if (Physics.Raycast(impact + Vector3.up * 20f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                impact = hit.point;
            effects.Spawn(EffectKind.EnemyMuzzle, gunMuzzle.position, gunMuzzle.rotation);
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
            foreach (var r in bodyRenderers) r.sharedMaterial = burntMaterial;
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
