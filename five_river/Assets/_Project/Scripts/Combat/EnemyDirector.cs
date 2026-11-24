using System.Collections.Generic;
using System.Linq;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.DebugTools;
using FiveRivers.Narrative;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Combat
{
    // S2 정찰병 · S3 주 공세. 구간 시작에 적을 내보내고, 다 쓰러지면 구간 완료를 보고한다.
    // 철수 명령 · 체크포인트 복구 · 새 회차를 받아 적을 정리한다 (FLOW.md 4번 중복 스폰 방지).
    // 2026-11-18: 일반판은 구간별 밀도(EnemyDensity)를 쓰고, 동시에 플레이어를 겨누는 수를 제한하며,
    // 쓰러진 적은 그 회차 동안 들판에 남는다.
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

        // ---- 2026-11-18: density, who aims at the player, the fallen ----
        EnemyDensity.Rule rule;
        readonly AimSlots<EnemyAgent> aim = new AimSlots<EnemyAgent>();
        readonly List<EnemyAgent> fallen = new List<EnemyAgent>();
        readonly List<Vector3> fallenAt = new List<Vector3>();
        readonly Dictionary<FlowCheckpoint, float> checkpointBegan = new Dictionary<FlowCheckpoint, float>();
        readonly List<Transform> openSpots = new List<Transform>();
        AllyCombatant[] allies;
        ShellingDirector shelling;
        // No enemy fires while this is set (the big barrage and the breather after it: ShellingDirector).
        public bool HoldFire { get; set; }
        Camera view;
        readonly Plane[] frustum = new Plane[6];
        float nextBodyCheck;

        public Vector3 PlayerEye => player.CameraPivot.position;
        public int AliveCount => alive.Count;
        // Read-only targets for allied presentation.
        public IReadOnlyList<EnemyAgent> ActiveEnemies => alive;
        public IReadOnlyList<EnemyAgent> Fallen => fallen;
        public int AimingAtPlayer => aim.Count;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static int DebugShotsAtPlayer, DebugClearShots;
#endif
        public EnemyDensity.Rule Density => rule;

        public event System.Action<Vector3> ShotMissedNearPlayer; // 총알 스침 소리 위치
        public event System.Action<Vector3> EnemyFired;
        public event System.Action<Vector3> EnemySpawned;

        void Start()
        {
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.EnemiesRetreatRequested += RetreatAll;
            flow.RetryRequested += OnRetry;
            allies = FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            shelling = FindFirstObjectByType<ShellingDirector>();
            view = Camera.main;
            SetRule(flow.Section);
        }

        void SetRule(FlowSection section)
        {
            rule = EnemyDensity.For(section, director.Flow.IsExpanded, config);
            aim.Max = rule.aimAtPlayer;
        }

        void OnSectionChanged(FlowTransition t)
        {
            SetRule(t.To);
            var checkpoint = CheckpointState.For(t.To);
            if (checkpoint != FlowCheckpoint.None && t.From != t.To) checkpointBegan[checkpoint] = Time.time;
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
                    checkpointBegan.Clear();
                    break;
                default:
                    plan = null;
                    scoutsActive = false;
                    RetreatAll();
                    break;
            }
        }

        // 체크포인트 복구: 살아 있는 적은 모두 치우고, 그 구간에서 쓰러진 몸도 치운다(같은 싸움을 다시 하므로).
        // 앞선 구간에서 쓰러진 몸은 남는다.
        void OnRetry(FlowCheckpoint checkpoint)
        {
            ClearLiving();
            if (checkpointBegan.TryGetValue(checkpoint, out float since)) ClearFallenSince(since);
            checkpointBegan[checkpoint] = Time.time;
            SetRule(director.Flow.Section);
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
            plan = rule.HasWaves
                ? new WavePlan(rule.waveCounts, rule.waveStarts, rule.maxActive, rule.interval, joined, Time.time)
                : new WavePlan(rule.total, rule.maxActive, rule.interval, joined, Time.time);
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
            ClearLiving(); // the dead of the main assault stay on the field
            WestWaveCleared = false;
            westSpawn = points.enemySpawns.OrderBy(p => p.position.x).First();
            westCovers = points.assaultCovers.Concat(points.scoutCovers)
                .OrderBy(p => p.position.x).Take(4).ToArray();
            plan = new WavePlan(rule.total, rule.maxActive, rule.interval, 0, Time.time);
        }

        // Hill defenders hold their authored cover. The hill director owns their objective.
        public EnemyAgent SpawnDefender(Transform cover)
        {
            if (cover == null || !NavMesh.SamplePosition(cover.position, out var hit, 4f, NavMesh.AllAreas))
                return null;
            return SpawnAt(cover, hit.position, cover.rotation);
        }

        // More men on the ridge (general run): beside an authored cover, on open ground a few metres off.
        public EnemyAgent SpawnDefenderBeside(Transform cover)
        {
            if (cover == null) return null;
            var spot = OpenSpotNear(cover.position, 4f, 7.5f);
            if (spot == null) return null;
            spot.rotation = cover.rotation;
            return SpawnAt(spot, spot.position, cover.rotation);
        }

        public int HillExtraDefenders => director.Flow.IsExpanded ? config.hillExtraDefenders : 0;

        void Update()
        {
            var flow = director.Flow;
            if (!flow.IsRunning) return;

            if (flow.Section == FlowSection.Scout && scoutsActive && alive.Count(e => e.Alive) == 0)
            {
                scoutsActive = false;
                // 2026-11-18 (general run): the scouts are down, and before the main assault the barrage comes in
                // across the field up to the parapet. The section is reported complete when the breather after it
                // is over, so the new objective, the flare and the assault all come after the quiet.
                if (shelling == null || !shelling.TryOpeningBarrage(ScoutsDone)) flow.ReportGoalMet(FlowSection.Scout);
            }

            if ((flow.Section == FlowSection.Assault || flow.Section == FlowSection.WestAssault) && plan != null)
            {
                bool west = flow.Section == FlowSection.WestAssault;
                if (west)
                {
                    if (NavMesh.SamplePosition(Scatter(westSpawn.position), out var hit, 4f, NavMesh.AllAreas)
                        && plan.TrySpawn(Time.time))
                        SpawnAt(PickCover(float.PositiveInfinity, far: true), hit.position, westSpawn.rotation);
                }
                else if (plan.TrySpawn(Time.time))
                {
                    if (plan.HasWaves && rule.HasWaves) SpawnFromSide(rule.waveSides[plan.SpawnWave]);
                    else Spawn(PickCover(float.PositiveInfinity, far: true));
                }
                if (plan.Done)
                {
                    plan = null;
                    if (west) WestWaveCleared = true;
                    else flow.ReportGoalMet(FlowSection.Assault);
                }
            }
            TendFallen();
        }

        void ScoutsDone()
        {
            var flow = director.Flow;
            if (flow.Section == FlowSection.Scout) flow.ReportGoalMet(FlowSection.Scout);
        }

        // 일반판: 출현 지점에서 옆으로 흩어져 나온다 (다섯 지점에서 같은 줄로 달려오지 않게)
        Vector3 Scatter(Vector3 position)
        {
            if (!director.Flow.IsExpanded || config.spawnSpread <= 0f) return position;
            return position + new Vector3(Random.Range(-config.spawnSpread, config.spawnSpread), 0f, Random.Range(-config.spawnSpread * 0.5f, config.spawnSpread * 0.5f));
        }

        public float SpeedScale() => director.Flow.IsExpanded ? 1f + Random.Range(-config.speedSpread, config.speedSpread) : 1f;

        EnemyAgent Spawn(Transform cover)
        {
            var spawn = points.enemySpawns[spawnIndex++ % points.enemySpawns.Length];
            var at = Scatter(spawn.position);
            if (at != spawn.position && NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
            else at = spawn.position;
            return SpawnAt(cover, at, spawn.rotation);
        }

        // 2026-11-24 general run: the main assault comes in waves from the front, the right slope, the left slope.
        // side -1 / 0 / 1: start points and covers left of x -12, between, right of x 12 (the field runs along z).
        static int SideOf(Vector3 p) => p.x < -12f ? -1 : p.x > 12f ? 1 : 0;

        EnemyAgent SpawnFromSide(int side)
        {
            var starts = points.enemySpawns.Where(s => SideOf(s.position) == side).ToArray();
            if (starts.Length == 0) starts = points.enemySpawns;
            var spawn = starts[spawnIndex++ % starts.Length];
            var at = Scatter(spawn.position);
            if (at != spawn.position && NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
            else at = spawn.position;
            return SpawnAt(PickCoverOnSide(side), at, spawn.rotation);
        }

        // a free cover on that side, the farther ones first; when they are taken he goes to ground near one of them
        Transform PickCoverOnSide(int side)
        {
            var mine = points.assaultCovers.Where(c => SideOf(c.position) == side).ToArray();
            if (mine.Length == 0) return PickCover(float.PositiveInfinity, far: true);
            var free = mine.Where(c => !usedCovers.Contains(c)).ToList();
            if (free.Count > 0) return free.OrderByDescending(c => c.position.z).Take(2).OrderBy(_ => Random.value).First();
            var near = mine[Random.Range(0, mine.Length)];
            var open = OpenSpotNear(near.position, 3.5f, 7f);
            return open != null ? open : PickCover(float.PositiveInfinity, far: true);
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
            aim.Release(e);
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
            if (free.Count == 0)
            {
                if (!far) return null;
                // 2026-11-18: more men than covers. Rather than two men in one spot, he goes to ground in the open
                // a few metres from a cover (the general run fields more than the ten authored covers).
                var near = covers[Random.Range(0, covers.Length)];
                var open = director.Flow.IsExpanded ? OpenSpotNear(near.position, 3.5f, 7f) : null;
                return open != null ? open : near;
            }
            if (far) return free.OrderByDescending(c => c.position.z).Take(3).OrderBy(_ => Random.value).First();
            var closer = free.Where(c => c.position.z < currentZ - 4f).OrderByDescending(c => c.position.z).FirstOrDefault();
            return closer;
        }

        // A spot on walkable ground between min and max metres from the centre, at least 2.5 m from every enemy,
        // every other spot in use and every cover.
        Transform OpenSpotNear(Vector3 centre, float min, float max)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f), distance = Random.Range(min, max);
                var p = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(p, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                bool crowded = false;
                foreach (var e in alive)
                    if (e != null && (e.transform.position - hit.position).sqrMagnitude < 6.25f) { crowded = true; break; }
                if (!crowded)
                    foreach (var c in usedCovers)
                        if (c != null && (c.position - hit.position).sqrMagnitude < 6.25f) { crowded = true; break; }
                if (!crowded)
                    foreach (var c in points.assaultCovers)
                        if (c != null && (c.position - hit.position).sqrMagnitude < 6.25f) { crowded = true; break; }
                if (crowded) continue;
                Transform spot = null;
                foreach (var s in openSpots)
                    if (s != null && !usedCovers.Contains(s)) { spot = s; break; }
                if (spot == null)
                {
                    spot = new GameObject("OpenGround").transform;
                    spot.SetParent(transform, false); // cleared with everything else on a new run
                    openSpots.Add(spot);
                }
                spot.position = hit.position;
                return spot;
            }
            return null;
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

        // ---- who he fires at ----
        public void ReleaseAim(EnemyAgent e) => aim.Release(e);
        public bool AimsAtPlayer(EnemyAgent e) => aim.Max != EnemyDensity.Unlimited && aim.Holds(e);

        // A man lying in the open beside a cover (one of the extra men of the general run) is not one of those who
        // fire at the player: he fights the squad. The player's opponents are the men at the authored covers, as
        // before 2026-11-18. Measured: with the open-ground men also taking places, a player standing still went
        // down in 28 s instead of 70 s (the open ground gives them a clear line where a cover blocks it).
        bool MayAimAtPlayer(EnemyAgent e) => aim.Max == EnemyDensity.Unlimited || e.Cover == null || !openSpots.Contains(e.Cover);

        // The player, if he holds (or can take) one of the places; otherwise the nearest man in the trench.
        public Vector3 AimPointFor(EnemyAgent e)
        {
            if (aim.Max == EnemyDensity.Unlimited || aim.Holds(e) || aim.Count < aim.Max && MayAimAtPlayer(e)) return PlayerEye;
            var ally = NearestAlly(e.transform.position);
            return ally != null ? ally.transform.position + Vector3.up * 1.2f : PlayerEye;
        }

        AllyCombatant NearestAlly(Vector3 from)
        {
            AllyCombatant best = null;
            float distance = float.MaxValue;
            foreach (var a in allies)
            {
                if (a == null || !a.isActiveAndEnabled) continue;
                float d = (a.transform.position - from).sqrMagnitude;
                if (d < distance) { distance = d; best = a; }
            }
            return best;
        }

        // 적 사격 판정: 시야가 막히면 빗나감. 어두울수록 · Z 자세일수록 덜 맞는다. 실패 보조 배율 적용.
        // 2026-11-18: 플레이어를 겨누는 자리가 다 찼으면 이 한 발은 참호의 아군 쪽으로 간다 (플레이어 피해 없음).
        public void EnemyShot(EnemyAgent e, Vector3 muzzle)
        {
            if (HoldFire) return;
            effects.Spawn(EffectKind.EnemyMuzzle, muzzle, Quaternion.identity);
            EnemyFired?.Invoke(muzzle);
            if (aim.Max != EnemyDensity.Unlimited && (!MayAimAtPlayer(e) || !aim.TryTake(e)))
            {
                ShotAtAllies(e, muzzle);
                return;
            }
            var eye = PlayerEye;
            if (Vector3.Distance(muzzle, eye) > config.sightRange) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugShotsAtPlayer++;
#endif

            bool clear = !Physics.Linecast(muzzle, eye, out var hit, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore)
                         || hit.collider.GetComponentInParent<PlayerHealth>() != null;
            float accuracy = Mathf.Lerp(config.accuracyDark, config.accuracyLit, flares.Illumination) * director.EnemyAccuracyScale;
            if (player.Crouched) accuracy *= config.crouchedFactor;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (clear) DebugClearShots++;
#endif
            if (clear && Random.value < accuracy)
            {
                float damage = director.Flow.IsExpanded ? config.expandedDamage : config.damage;
                playerHealth.TakeDamage(damage * director.EnemyDamageScale, muzzle);
                return;
            }
            // 빗나간 탄: 플레이어 근처 흙 · 모래주머니에 튄다
            var miss = eye + new Vector3(Random.Range(-2f, 2f), Random.Range(-1.4f, -0.4f), Random.Range(0.5f, 2.5f));
            if (Physics.Linecast(muzzle, miss, out var dirt, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore))
                effects.Spawn(EffectKind.ImpactDirt, dirt.point, Quaternion.LookRotation(dirt.normal));
            ShotMissedNearPlayer?.Invoke(miss);
        }

        // A round at the men in the trench: it strikes the earth or the sandbags near one of them. Nobody is hurt.
        void ShotAtAllies(EnemyAgent e, Vector3 muzzle)
        {
            var ally = NearestAlly(e.transform.position);
            if (ally == null || Random.value > 0.6f) return;
            var near = ally.transform.position + new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(0.1f, 0.9f), Random.Range(0.4f, 2.2f));
            // not into the player's face: a round landing right beside him would read as fire at him
            if ((near - PlayerEye).sqrMagnitude < 9f) return;
            if (Physics.Linecast(muzzle, near, out var dirt, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore)
                && dirt.collider.GetComponentInParent<AllyCombatant>() == null && dirt.collider.GetComponentInParent<PlayerHealth>() == null)
                effects.Spawn(EffectKind.ImpactDirt, dirt.point, Quaternion.LookRotation(dirt.normal));
        }

        // Called by AllyCombatant as one of its rounds goes at the target. The squad's fire thins the assault, but
        // only down to the player's share, and never a man who is firing at the player (those are the player's).
        // No score, no sound of its own: he just goes down like any other.
        public void AllyShotAt(EnemyAgent target)
        {
            if (target == null || CutsceneRig.Running > 0) return;
            int living = 0;
            foreach (var e in alive) if (e != null && e.Alive && !e.Retreating) living++;
            if (!EnemyDensity.AllyMayKill(rule, living, false) || Random.value >= config.allyKillChance) return;
            // The man he aimed at, unless that one is the player's. Then the round is taken to have found another
            // man who is up and in the open (coming forward, or risen to fire): from the trench the two cannot be
            // told apart, and the squad's fire still thins the assault at the rate the numbers ask for.
            var victim = target.Alive && !target.Retreating && EnemyDensity.AllyMayKill(rule, living, aim.Holds(target)) ? target : Exposed();
            if (victim != null)
            {
                victim.Kill(-1);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                DebugAllyKills++;
#endif
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static int DebugAllyKills; // review tools: how many of the fallen the squad's fire took (2026-11-24)
#endif

        EnemyAgent Exposed()
        {
            EnemyAgent standing = null;
            int seen = 0;
            foreach (var e in alive)
            {
                if (e == null || !e.Alive || e.Retreating || aim.Holds(e)) continue;
                // one of those coming forward, picked evenly; failing that, one who has stood up to fire
                if (e.Advancing) { if (Random.Range(0, ++seen) == 0) standing = e; }
                else if (seen == 0 && e.Standing && standing == null) standing = e;
            }
            return standing;
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

        // ---- the fallen ----
        public void KeepBody(EnemyAgent e)
        {
            fallen.Add(e);
        }

        void TendFallen()
        {
            if (Time.time < nextBodyCheck) return;
            nextBodyCheck = Time.time + 0.5f;
            for (int i = fallen.Count - 1; i >= 0; i--)
            {
                var e = fallen[i];
                if (e == null) { fallen.RemoveAt(i); continue; }
                if (!e.Settled && Time.time - e.FellAt >= config.bodySettleAfter) e.Settle();
            }
            // over the limit: the furthest one out of view goes first; one in view only if it is far away
            int guard = 4;
            while (fallen.Count > config.maxBodies && guard-- > 0)
            {
                fallenAt.Clear();
                foreach (var e in fallen) fallenAt.Add(e.transform.position);
                if (view != null) GeometryUtility.CalculateFrustumPlanes(view, frustum);
                var eye = view != null ? view.transform.position : PlayerEye;
                int pick = FallenKeeper.PickToRemove(fallenAt, InView, eye, config.bodyCullDistance);
                if (pick < 0) break;
                fallen[pick].Remove();
                fallen.RemoveAt(pick);
            }
        }

        bool InView(int i) => view != null && GeometryUtility.TestPlanesAABB(frustum, new Bounds(fallenAt[i] + Vector3.up * 0.4f, new Vector3(2.4f, 1.2f, 2.4f)));

        void ClearFallenSince(float time)
        {
            for (int i = fallen.Count - 1; i >= 0; i--)
            {
                var e = fallen[i];
                if (e == null) { fallen.RemoveAt(i); continue; }
                if (e.FellAt < time) continue;
                e.Remove();
                fallen.RemoveAt(i);
            }
        }

        // 살아 있는 적만 치운다 (쓰러진 몸은 남는다)
        void ClearLiving()
        {
            plan = null;
            scoutsActive = false;
            foreach (var e in alive.ToArray())
                if (e != null) { e.Gone -= OnGone; e.gameObject.SetActive(false); Destroy(e.gameObject); }
            alive.Clear();
            usedCovers.Clear();
            aim.Clear();
            HoldFire = false;
        }

        // 새 회차: 적 · 쓰러진 몸 · 떨어진 소총까지 전부
        public void ClearAll()
        {
            ClearLiving();
            fallen.Clear();
            openSpots.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        }

        public string DebugTitle => "적";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Label($"활동 {alive.Count(e => e.Alive)} (상한 {rule.maxActive}) · 플레이어 겨눔 {aim.Count} · 남은 출현 {(plan != null ? plan.Remaining : 0)} · 쓰러진 몸 {fallen.Count} · 조명 {flares.Illumination:0.00}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("적 전부 쓰러뜨리기"))
                foreach (var e in alive.ToArray()) if (e != null && e.Alive) e.TakeDamage(9999f, e.transform.position);
            if (GUILayout.Button("조명탄 발사")) flares.Launch();
            GUILayout.EndHorizontal();
#endif
        }
    }
}
