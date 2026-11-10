using System.Collections.Generic;
using System.Linq;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.DebugTools;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Combat
{
    // S2 정찰병 · S3 주 공세. 구간 시작에 적을 내보내고, 다 쓰러지면 구간 완료를 보고한다.
    // 철수 명령 · 체크포인트 복구 · 새 회차를 받아 적을 정리한다 (FLOW.md 4번 중복 스폰 방지).
    public sealed class EnemyDirector : MonoBehaviour, IDebugPanel
    {
        [SerializeField] FlowDirector director;
        [SerializeField] WaveConfig config;
        [SerializeField] LevelPoints points;
        [SerializeField] EnemyAgent enemyPrefab;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] FlareDirector flares;
        [SerializeField] Effects effects;

        readonly List<EnemyAgent> alive = new List<EnemyAgent>();
        readonly HashSet<Transform> usedCovers = new HashSet<Transform>();
        Transform[] westCovers;
        Transform westSpawn;
        public bool WestWaveCleared { get; private set; }
        WavePlan plan;
        int spawnIndex;
        bool scoutsActive;

        public Vector3 PlayerEye => player.CameraPivot.position;
        public int AliveCount => alive.Count;
        // Read-only targets for allied presentation; only player damage changes this roster.
        public IReadOnlyList<EnemyAgent> ActiveEnemies => alive;

        public event System.Action<Vector3> ShotMissedNearPlayer; // 총알 스침 소리 위치
        public event System.Action<Vector3> EnemyFired;
        public event System.Action<Vector3> EnemySpawned;

        void Start()
        {
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.EnemiesRetreatRequested += RetreatAll;
            flow.RetryRequested += OnRetry;
        }

        void OnSectionChanged(FlowTransition t)
        {
            switch (t.To)
            {
                case FlowSection.Scout:
                    BeginScouts();
                    break;
                case FlowSection.Assault:
                    // 상한으로 넘어오면 생존 정찰병이 주 공세에 합류한다 (GDD 4번 S2)
                    int joined = alive.Count(e => e.Alive && !e.Retreating);
                    BeginAssault(joined);
                    break;
                case FlowSection.WestAssault:
                    BeginWest();
                    break;
                case FlowSection.Title:
                case FlowSection.Tutorial:
                    ClearAll();
                    break;
                default:
                    plan = null;
                    scoutsActive = false;
                    RetreatAll();
                    break;
            }
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            ClearAll();
            if (checkpoint == FlowCheckpoint.FirstContact) BeginScouts();
            else if (checkpoint == FlowCheckpoint.MainAssault) BeginAssault(0);
            else if (checkpoint == FlowCheckpoint.WestAssault) BeginWest();
        }

        void BeginScouts()
        {
            plan = null;
            scoutsActive = true;
            for (int i = 0; i < config.scouts; i++)
            {
                var cover = points.scoutCovers[i % points.scoutCovers.Length];
                var e = Spawn(cover);
                e.IsScout = true;
            }
        }

        void BeginAssault(int joined)
        {
            scoutsActive = false;
            plan = new WavePlan(config.mainAssault, config.maxActive, config.spawnInterval, joined, Time.time);
            // 합류한 정찰병은 주 공세 엄폐 지점으로 옮긴다
            foreach (var e in alive.Where(e => e.Alive && e.IsScout))
            {
                Release(e.Cover);
                var cover = PickCover(e.transform.position.z, far: true);
                usedCovers.Add(cover);
                e.MoveToCover(cover);
            }
        }

        void BeginWest()
        {
            ClearAll();
            WestWaveCleared = false;
            westSpawn = points.enemySpawns.OrderBy(p => p.position.x).First();
            westCovers = points.assaultCovers.Concat(points.scoutCovers)
                .OrderBy(p => p.position.x).Take(4).ToArray();
            plan = new WavePlan(config.westAssault, config.maxActive, config.spawnInterval, 0, Time.time);
        }

        // Hill defenders hold their authored cover. The hill director owns their objective.
        public EnemyAgent SpawnDefender(Transform cover)
        {
            if (cover == null || !NavMesh.SamplePosition(cover.position, out var hit, 4f, NavMesh.AllAreas))
                return null;
            return SpawnAt(cover, hit.position, cover.rotation);
        }

        void Update()
        {
            var flow = director.Flow;
            if (!flow.IsRunning) return;

            if (flow.Section == FlowSection.Scout && scoutsActive && alive.Count(e => e.Alive) == 0)
            {
                scoutsActive = false;
                flow.ReportGoalMet(FlowSection.Scout);
            }

            if ((flow.Section == FlowSection.Assault || flow.Section == FlowSection.WestAssault) && plan != null)
            {
                bool west = flow.Section == FlowSection.WestAssault;
                if (west)
                {
                    if (NavMesh.SamplePosition(westSpawn.position, out var hit, 4f, NavMesh.AllAreas)
                        && plan.TrySpawn(Time.time))
                        SpawnAt(PickCover(float.PositiveInfinity, far: true), hit.position, westSpawn.rotation);
                }
                else if (plan.TrySpawn(Time.time)) Spawn(PickCover(float.PositiveInfinity, far: true));
                if (plan.Done)
                {
                    plan = null;
                    if (west) WestWaveCleared = true;
                    else flow.ReportGoalMet(FlowSection.Assault);
                }
            }
        }

        EnemyAgent Spawn(Transform cover)
        {
            var spawn = points.enemySpawns[spawnIndex++ % points.enemySpawns.Length];
            return SpawnAt(cover, spawn.position, spawn.rotation);
        }

        EnemyAgent SpawnAt(Transform cover, Vector3 position, Quaternion rotation)
        {
            var e = Instantiate(enemyPrefab, position, rotation, transform);
            e.Gone += OnGone;
            e.Init(this, config, cover);
            usedCovers.Add(cover);
            alive.Add(e);
            EnemySpawned?.Invoke(position);
            return e;
        }

        void OnGone(EnemyAgent e)
        {
            alive.Remove(e);
            Release(e.Cover);
            plan?.OnEnemyGone();
        }

        void Release(Transform cover)
        {
            if (cover != null) usedCovers.Remove(cover);
        }

        // 빈 엄폐 지점 고르기. far면 먼 쪽부터, 아니면 지금보다 가까운(플레이어 쪽) 곳.
        Transform PickCover(float currentZ, bool far)
        {
            var covers = director.Flow.Section == FlowSection.WestAssault ? westCovers : points.assaultCovers;
            var free = covers.Where(c => !usedCovers.Contains(c)).ToList();
            if (free.Count == 0) return far ? covers[Random.Range(0, covers.Length)] : null;
            if (far) return free.OrderByDescending(c => c.position.z).Take(3).OrderBy(_ => Random.value).First();
            var closer = free.Where(c => c.position.z < currentZ - 4f).OrderByDescending(c => c.position.z).FirstOrDefault();
            return closer;
        }

        public void RequestAdvance(EnemyAgent e)
        {
            if (director.Flow.Section == FlowSection.HillAssault || (e.IsScout && director.Flow.Section == FlowSection.Scout)) return;
            var next = PickCover(e.transform.position.z, far: false);
            if (next == null || Random.value < 0.4f) return;
            Release(e.Cover);
            usedCovers.Add(next);
            e.MoveToCover(next);
        }

        // 적 사격 판정: 시야가 막히면 빗나감. 어두울수록 · Z 자세일수록 덜 맞는다. 실패 보조 배율 적용.
        public void EnemyShot(EnemyAgent e, Vector3 muzzle)
        {
            effects.Spawn(EffectKind.EnemyMuzzle, muzzle, Quaternion.identity);
            EnemyFired?.Invoke(muzzle);
            var eye = PlayerEye;
            if (Vector3.Distance(muzzle, eye) > config.sightRange) return;

            bool clear = !Physics.Linecast(muzzle, eye, out var hit, ~0, QueryTriggerInteraction.Ignore)
                         || hit.collider.GetComponentInParent<PlayerHealth>() != null;
            float accuracy = Mathf.Lerp(config.accuracyDark, config.accuracyLit, flares.Illumination) * director.EnemyAccuracyScale;
            if (player.Crouched) accuracy *= config.crouchedFactor;

            if (clear && Random.value < accuracy)
            {
                float damage = director.Flow.IsExpanded ? config.expandedDamage : config.damage;
                playerHealth.TakeDamage(damage * director.EnemyDamageScale, muzzle);
                return;
            }
            // 빗나간 탄: 플레이어 근처 흙 · 모래주머니에 튄다
            var miss = eye + new Vector3(Random.Range(-2f, 2f), Random.Range(-1.4f, -0.4f), Random.Range(0.5f, 2.5f));
            if (Physics.Linecast(muzzle, miss, out var dirt, ~0, QueryTriggerInteraction.Ignore))
                effects.Spawn(EffectKind.ImpactDirt, dirt.point, Quaternion.LookRotation(dirt.normal));
            ShotMissedNearPlayer?.Invoke(miss);
        }

        void RetreatAll()
        {
            plan?.StopSpawning();
            foreach (var e in alive.ToArray())
            {
                if (!e.Alive) continue;
                var p = points.retreatPoints.OrderBy(r => Vector3.Distance(r.position, e.transform.position)).First();
                Release(e.Cover);
                e.Retreat(p);
            }
        }

        public void ClearAll()
        {
            plan = null;
            scoutsActive = false;
            foreach (var e in alive.ToArray())
                if (e != null) { e.Gone -= OnGone; e.gameObject.SetActive(false); Destroy(e.gameObject); }
            alive.Clear();
            usedCovers.Clear();
            // 쓰러진 몸도 지운다
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        }

        public string DebugTitle => "적";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label($"활동 {alive.Count(e => e.Alive)} · 남은 출현 {(plan != null ? plan.Remaining : 0)} · 조명 {flares.Illumination:0.00}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("적 전부 쓰러뜨리기"))
                foreach (var e in alive.ToArray()) if (e != null && e.Alive) e.TakeDamage(9999f, e.transform.position);
            if (GUILayout.Button("조명탄 발사")) flares.Launch();
            GUILayout.EndHorizontal();
#endif
        }
    }
}
