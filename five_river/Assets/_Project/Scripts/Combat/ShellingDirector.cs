using System.Collections.Generic;
using FiveRivers.Audio;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.Narrative;
using FiveRivers.UI;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 포격 (2026-11-18, 사용자 지시 "포격 소리도 집어넣고 흔적도 남겨서 전쟁의 비참함을 끌어 올려").
    // 일정은 ShellingPlan(순수 C#)이 정하고 여기서는 보이고 들리게만 한다. 피해는 없다(연출용).
    //  - 먼 포격: 능선 너머 하늘 가장자리가 번쩍이고, 거리 / 소리 속도만큼 늦게 둔한 포성이 온다.
    //  - 가까운 포격: 날아오는 소리 → 폭발(섬광 · 흙기둥 · 연기) → 구덩이와 그을음(ShellCraters) · 남는 연기.
    //    가까우면 화면이 짧게 흔들리고 귀가 먹먹하며 참호 벽에서 흙이 떨어진다.
    //  - 먼 능선 위 예광탄 줄기 · 이웃 진지 총성: 우리만 싸우는 것이 아니다.
    // 효과는 Effects 컨테이너 밑에 만들어 체크포인트 복구 · 새 회차 때 함께 지워지고, 구덩이는 ShellCraters가 되돌린다.
    public sealed class ShellingDirector : MonoBehaviour
    {
        [SerializeField] ShellingConfig config;

        FlowDirector director;
        PlayerController player;
        AudioDirector sound;
        Effects effects;
        TankController tank;
        EnemyDirector enemies;
        SubtitleDisplay subtitles;
        RunCoordinator coordinator;
        LevelPoints points;
        AllyCombatant[] allies;
        Terrain terrain;
        Camera view;
        ShellingPlan plan;
        Rect trench = new Rect(-19.3f, -3.7f, 40.9f, 4.9f);
        Vector3 mapCentre = new Vector3(7.5f, 0f, 30f);
        float lastTankShot = -100f;

        readonly List<ShellingPlan.Event> events = new List<ShellingPlan.Event>();
        readonly List<ShellSafety.Keepout> keepouts = new List<ShellSafety.Keepout>();
        readonly System.Random rng = new System.Random(19500818);

        struct PendingShell { public Vector3 point; public float at; public bool close; }
        struct PendingSound { public float at; public int kind; public Vector3 point; public float far01, bearing, level, gain; public string id; public float cutoff, pitch; public int voice; }
        const int DistantReport = 0, BlastReport = 1, Debris = 2, FarGun = 3, Rain = 4, Incoming = 5, HeadsUp = 6;

        // ---- the big barrage (once a run, after the scouts and before the main assault) and its breather ----
        readonly BarrageTimeline timeline = new BarrageTimeline();
        System.Action bigDone;
        bool bigPlayed, bigLate;
        GameText text;
        float dust, dustFrom, dustBegan;
        float fogDensity;
        Color fogColor;
        bool fogHeld;
        public bool BarrageActive => timeline.Active;
        public BarrageTimeline.Phase BarragePhase => timeline.Now;
        readonly List<PendingShell> shells = new List<PendingShell>();
        readonly List<PendingSound> sounds = new List<PendingSound>();

        public ShellingConfig Config => config;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 점검용 집계
        public int NearFired { get; private set; }
        public int NearSkipped { get; private set; }
        public int DistantFired { get; private set; }
        public int TracersFired { get; private set; }
        public Vector3 LastBlast { get; private set; }
        public float LastBlastDistance { get; private set; }
        public float ClosestBlast { get; private set; } = float.MaxValue;
#endif

        float Rand() => (float)rng.NextDouble();
        float Rand(float a, float b) => a + (b - a) * Rand();

        void Awake()
        {
            if (config == null) { enabled = false; return; }
            plan = new ShellingPlan(config, Rand);
        }

        void Start()
        {
            director = FindFirstObjectByType<FlowDirector>();
            player = FindFirstObjectByType<PlayerController>();
            sound = FindFirstObjectByType<AudioDirector>();
            effects = FindFirstObjectByType<Effects>();
            tank = FindFirstObjectByType<TankController>();
            enemies = FindFirstObjectByType<EnemyDirector>();
            subtitles = FindFirstObjectByType<SubtitleDisplay>();
            coordinator = FindFirstObjectByType<RunCoordinator>();
            points = FindFirstObjectByType<LevelPoints>();
            allies = FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            terrain = Terrain.activeTerrain;
            view = Camera.main;
            var narrative = FindFirstObjectByType<NarrativeDirector>();
            if (narrative != null) text = narrative.Text;
            FindTrench();
            if (terrain != null)
            {
                var size = terrain.terrainData.size;
                mapCentre = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            }
            BuildHorizon();
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.RetryRequested += OnRetry;
            if (tank != null) tank.CannonFired += OnTankShot;
            plan.Enter(flow.Section, flow.IsExpanded);
        }

        void OnDestroy()
        {
            if (director != null && director.Flow != null)
            {
                director.Flow.SectionChanged -= OnSectionChanged;
                director.Flow.RetryRequested -= OnRetry;
            }
            if (tank != null) tank.CannonFired -= OnTankShot;
            if (quad != null) Destroy(quad);
        }

        void OnTankShot() => lastTankShot = Time.time;

        void FindTrench()
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var t = r.transform;
                for (int k = 0; k < 4 && t != null; k++, t = t.parent)
                {
                    if (!t.name.StartsWith("Trench_Sandbag_Walls")) continue;
                    if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
                    break;
                }
            }
            if (any) trench = new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
        }

        void OnSectionChanged(FlowTransition t)
        {
            bool fresh = t.To == FlowSection.Title || t.Reason == TransitionReason.RunStart;
            if (fresh)
            {
                CancelBarrage();
                bigPlayed = bigLate = false;
                sounds.Clear();
                ClearHorizon();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                NearFired = NearSkipped = DistantFired = TracersFired = 0;
                ClosestBlast = float.MaxValue;
#endif
            }
            // a shell already on its way does not land in a section that has no close shelling (the silence, the dawn)
            plan.Enter(t.To, director.Flow.IsExpanded);
            if (fresh || !plan.NearAllowed) shells.Clear();
            // reports still on their way from far off die out with a section that has none
            if (!config.TryRule(t.To, out var rule) || rule.distantMax <= 0f)
                sounds.RemoveAll(s => s.kind == DistantReport || s.kind == FarGun);
            if (t.To == FlowSection.Dawn && director.Flow.IsExpanded) DawnSmoke();
            // The scouts were never beaten (the section ran out): the barrage still comes, once the flare sequence
            // of the main assault is over. Not on a debug jump.
            if (t.To == FlowSection.Assault && t.Reason != TransitionReason.DebugJump && !bigPlayed && !timeline.Active
                && ShellingPlan.BigBarrageAllowed(config, director.Flow.IsExpanded)) bigLate = true;
            if (t.To != FlowSection.Assault && t.To != FlowSection.Scout) bigLate = false;
        }

        // The dawn shows the night: thin smoke still rises from some of the places the shells fell.
        void DawnSmoke()
        {
            if (effects == null || config.smolderPrefab == null || tank == null || tank.Craters == null || config.dawnSmokes <= 0) return;
            var eye = player.transform.position;
            int made = 0;
            // the latest craters first
            for (int i = tank.Craters.Points.Count - 1; i >= 0 && made < config.dawnSmokes; i--)
            {
                var p = tank.Craters.Points[i];
                if ((p - eye).sqrMagnitude < 25f * 25f) continue; // not in the face of the dawn scene on the crest
                var go = Instantiate(config.smolderPrefab, p, Quaternion.identity, effects.Container);
                var smoke = go.GetComponent<Smolder>();
                if (smoke != null) smoke.Burn(config.dawnSmokeSeconds * Rand(0.8f, 1.2f));
                made++;
            }
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            CancelBarrage();
            if (checkpoint == FlowCheckpoint.FirstContact) bigPlayed = false; // the same night again from the scouts
            bigLate = false;
            shells.Clear();
            sounds.Clear();
            plan.Enter(director.Flow.Section, director.Flow.IsExpanded);
        }

        bool NearBlocked(DemoFlow flow) => CutsceneRig.Running > 0 || flow.Paused || flow.CombatLocked
            || subtitles != null && subtitles.Speaking || coordinator != null && coordinator.Dying
            || Time.time - lastTankShot < config.afterTankShot;

        void Update()
        {
            var flow = director.Flow;
            UpdateHorizon();
            if (!flow.IsRunning) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            TickBarrage(flow, dt);
            events.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!DebugHold)
#endif
            plan.Tick(dt, NearBlocked(flow), events, timeline.ShellingHeld);
            foreach (var e in events)
            {
                switch (e.kind)
                {
                    case ShellingPlan.Kind.DistantFlash: DistantFlash(e); break;
                    case ShellingPlan.Kind.NearShell: NearShell(e); break;
                    case ShellingPlan.Kind.Tracer: TracerBurst(); break;
                    case ShellingPlan.Kind.Gunfire: Gunfire(); break;
                }
            }

            for (int i = shells.Count - 1; i >= 0; i--)
            {
                if (Time.time < shells[i].at) continue;
                var p = shells[i].point;
                bool close = shells[i].close;
                shells.RemoveAt(i);
                Blast(p, close);
            }
            for (int i = sounds.Count - 1; i >= 0; i--)
            {
                if (Time.time < sounds[i].at) continue;
                var s = sounds[i];
                sounds.RemoveAt(i);
                if (s.kind == Rain) { if (CutsceneRig.Running == 0) DirtRain(s.point); continue; }
                if (s.kind == HeadsUp) { foreach (var a in allies) if (a != null) a.LookUp(); continue; }
                if (s.kind == Incoming) { if (sound != null) sound.PlayAt("SFX_Artillery_Incoming", s.point + Vector3.up * 12f, s.gain); continue; }
                if (sound == null) continue;
                switch (s.kind)
                {
                    case DistantReport: sound.PlayDistantShell(s.far01, Pan(s.bearing), s.level * Behind(s.bearing)); break;
                    case BlastReport: sound.PlayAt("SFX_Artillery_Near", s.point, s.gain); break;
                    case Debris: sound.PlayAt("SFX_Debris_Fall", s.point, s.gain); break;
                    case FarGun: sound.PlayDistantGunfire(s.id, Pan(s.bearing), s.level * Behind(s.bearing), s.cutoff, s.voice, s.pitch); break;
                }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 점검용: 정한 자리에 한 발 (날아오는 소리부터), 정한 방위에 섬광 · 예광탄
        public void DebugShellAt(Vector3 point)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            shells.Add(new PendingShell { point = point, at = Time.time + config.incomingMin });
            AllyClipCues.ShellIncoming(point, Time.time + config.incomingMin);
            if (sound != null) sound.PlayAt("SFX_Artillery_Incoming", point + Vector3.up * 12f, 1f);
        }
        public void DebugFlash(float bearing, float far01) => ShowFlash(bearing, Mathf.Lerp(config.flashBright, config.flashDim, far01), Mathf.Lerp(1f, 0.55f, far01));
        public void DebugTracers(float bearing, bool ours) => StartTracers(bearing, ours ? config.tracerOurs : config.tracerTheirs, 1f);
        public int TrenchDustSpawned { get; private set; }
        public Vector3 LastTrenchDust { get; private set; }
        public static bool DebugHold;
        public int BigRounds { get; private set; }
        public float BigBegan { get; private set; }
        public float Dust => dust;
        public bool DebugBarrage() => BeginBig(null, 2f); // 일정을 멈춘다 (정한 자리 한 발만 볼 때)
        public int PendingShells => shells.Count;
        public int PendingSounds => sounds.Count;
#endif

        // ---- bearings: degrees clockwise from north (+z), as seen from the middle of the map ----
        static Vector3 Direction(float bearing)
        {
            float r = bearing * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        float Pan(float bearing)
        {
            if (view == null) return 0f;
            return Vector3.Dot(view.transform.right, Direction(bearing));
        }

        // a little quieter from behind the head
        float Behind(float bearing)
        {
            if (view == null) return 1f;
            var f = view.transform.forward; f.y = 0f;
            return Mathf.Lerp(0.8f, 1f, Vector3.Dot(f.normalized, Direction(bearing)) * 0.5f + 0.5f);
        }

        // ---- far shelling ----
        void DistantFlash(ShellingPlan.Event e)
        {
            float bearing = e.front ? Rand(-100f, 100f) : Rand(0f, 360f);
            float bright = Mathf.Lerp(config.flashBright, config.flashDim, e.far01) * Mathf.Lerp(0.55f, 1f, e.level) * Rand(0.75f, 1.15f);
            float size = Mathf.Lerp(1f, 0.55f, e.far01) * Rand(0.85f, 1.2f);
            ShowFlash(bearing, bright, size);
            sounds.Add(new PendingSound { at = Time.time + e.soundDelay, kind = DistantReport, far01 = e.far01, bearing = bearing, level = e.level });
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DistantFired++;
#endif
        }

        // ---- fighting on the neighbouring positions ----
        // The player's own rifle report, far off through a low-pass: single shots, or the same report run together
        // as a heavy machine gun. (The graybox enemy-rifle and machine-gun clips are bare noise and are not used.)
        const string FarShot = "SFX_M1_Fire";
        int gunVoice;

        void Gunfire()
        {
            float bearing = (Rand() < 0.5f ? -90f : 90f) + Rand(-30f, 30f);
            float level = config.gunfireLevel * Rand(0.55f, 1f), cutoff = Rand(700f, 1500f);
            if (Rand() < 0.4f) { MachineGun(bearing, level, cutoff, Time.time); return; }
            int shots = 2 + (int)(Rand() * 4f), voice = gunVoice++;
            float at = Time.time, pitch = Rand() < 0.5f ? Rand(0.95f, 1.02f) : Rand(0.84f, 0.9f); // theirs sound a little lower
            for (int i = 0; i < shots; i++)
            {
                sounds.Add(new PendingSound { at = at, kind = FarGun, id = FarShot, bearing = bearing, level = level * Rand(0.7f, 1f), cutoff = cutoff, voice = voice, pitch = pitch * Rand(0.98f, 1.02f) });
                at += Rand(0.3f, 1.2f);
            }
        }

        void MachineGun(float bearing, float level, float cutoff, float from)
        {
            int rounds = 5 + (int)(Rand() * 5f), voice = gunVoice++;
            float gap = Rand(0.105f, 0.125f), pitch = Rand(0.76f, 0.84f);
            for (int i = 0; i < rounds; i++)
                sounds.Add(new PendingSound { at = from + i * gap, kind = FarGun, id = FarShot, bearing = bearing, level = level * Rand(0.8f, 1f), cutoff = cutoff, voice = voice, pitch = pitch });
        }

        void TracerBurst()
        {
            // along the ridges to either side, where the neighbouring companies are; never straight ahead
            float bearing = (Rand() < 0.5f ? -1f : 1f) * Rand(55f, 140f);
            bool ours = Rand() < 0.6f;
            StartTracers(bearing, ours ? config.tracerOurs : config.tracerTheirs, Rand() < 0.5f ? 1f : -1f);
            // the gun is heard a couple of seconds after its tracers are seen
            MachineGun(bearing, config.gunfireLevel * 0.8f, Rand(650f, 1000f), Time.time + Rand(1.6f, 2.6f));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TracersFired++;
#endif
        }

        // ---- close shelling ----
        void NearShell(ShellingPlan.Event e)
        {
            if (terrain == null || player == null) return;
            config.TryRule(plan.Section, out var rule);
            var at = player.transform.position;
            var here = new Vector2(at.x, at.z);
            CollectKeepouts(rule.playerMin);

            var size = terrain.terrainData.size;
            var origin = terrain.transform.position;
            Rect area = e.rear ? config.rearArea
                : e.zone == ShellingConfig.Zone.AroundPlayer ? new Rect(origin.x + 8f, origin.z + 8f, size.x - 16f, size.z - 16f)
                : config.fieldArea;
            float min = Mathf.Max(rule.playerMin, e.distance - 6f), max = Mathf.Max(min + 4f, e.distance + 6f);
            var look = view != null ? view.transform.forward : player.transform.forward;
            Vector2 picked = default;
            bool found = false;
            for (int attempt = 0; attempt < 3 && !found; attempt++)
            {
                if (!ShellSafety.TryPick(here, min, max, area, keepouts, trench, config.keepTrench, Rand, 16, out picked,
                        new Vector2(look.x, look.z), e.rear ? 0f : config.inViewShare)) break;
                // on open ground only: never on a rock, a wall, a wreck or a man
                var from = new Vector3(picked.x, origin.y + size.y + 5f, picked.y);
                found = Physics.Raycast(from, Vector3.down, out var hit, size.y + 20f, WorldQuery.Solid, QueryTriggerInteraction.Ignore)
                        && hit.collider is TerrainCollider;
            }
            if (!found)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                NearSkipped++;
#endif
                return;
            }
            // not one shell but a few, one after another round the first (2026-11-18)
            var rounds = Barrage.Small(config, picked, Mathf.Max(1, e.rounds), area, keepouts, trench, config.keepTrench, Rand);
            foreach (var round in rounds) Schedule(round, Time.time, false);
        }

        // One round of a barrage: the sound of it coming, then the burst. Dropped if it would not land on open ground.
        bool Schedule(Barrage.Round round, float from, bool close)
        {
            var origin = terrain.transform.position;
            var size = terrain.terrainData.size;
            var above = new Vector3(round.point.x, origin.y + size.y + 5f, round.point.y);
            if (!Physics.Raycast(above, Vector3.down, out var hit, size.y + 20f, WorldQuery.Solid, QueryTriggerInteraction.Ignore) || !(hit.collider is TerrainCollider))
                return false;
            float lead = Rand(config.incomingMin, config.incomingMax);
            float at = from + lead + round.time;
            shells.Add(new PendingShell { point = hit.point, at = at, close = close });
            AllyClipCues.ShellIncoming(hit.point, at); // 가까운 낮춘 아군이 떨어지기 직전에 숙인다 (AllyClipPlayer)
            sounds.Add(new PendingSound { at = at - lead, kind = Incoming, point = hit.point, gain = 1f });
            return true;
        }

        // ---- the big barrage ----
        // Called by EnemyDirector when the scouts are down. Returns false if there is to be none (exhibit run, already
        // played this run): the caller then goes on at once. done is called when the breather after it is over.
        public bool TryOpeningBarrage(System.Action done)
        {
            if (bigPlayed || timeline.Active || !ShellingPlan.BigBarrageAllowed(config, director.Flow.IsExpanded) || terrain == null) return false;
            return BeginBig(done, config.bigDelay);
        }

        bool BeginBig(System.Action done, float delay)
        {
            var at = player.transform.position;
            // the barrage's own, tighter keep-outs: it is meant to land right in front of the position
            keepouts.Clear();
            keepouts.Add(new ShellSafety.Keepout(at, config.bigKeepPlayer));
            foreach (var a in allies)
                if (a != null && a.gameObject.activeInHierarchy) keepouts.Add(new ShellSafety.Keepout(a.transform.position, config.bigKeepAlly));
            if (enemies != null)
            {
                foreach (var e in enemies.ActiveEnemies) if (e != null) keepouts.Add(new ShellSafety.Keepout(e.transform.position, config.keepEnemy));
                foreach (var e in enemies.Fallen) if (e != null) keepouts.Add(new ShellSafety.Keepout(e.transform.position, config.keepBody));
            }
            if (points != null)
            {
                foreach (var c in points.assaultCovers) if (c != null) keepouts.Add(new ShellSafety.Keepout(c.position, config.keepCover));
                foreach (var c in points.scoutCovers) if (c != null) keepouts.Add(new ShellSafety.Keepout(c.position, config.keepCover));
                foreach (var c in points.ammoCrates) if (c != null) keepouts.Add(new ShellSafety.Keepout(c.position, config.bigKeepCrate));
                if (points.target != null) keepouts.Add(new ShellSafety.Keepout(points.target.position, 3.5f));
            }
            if (tank != null && tank.Craters != null)
                foreach (var p in tank.Craters.Points) keepouts.Add(new ShellSafety.Keepout(p, 3f));

            var rounds = Barrage.Walk(config, new Vector2(at.x, at.z), trench, keepouts, Rand);
            float start = Time.time + delay, last = 0f;
            int scheduled = 0;
            foreach (var round in rounds)
            {
                if (!Schedule(round, start, round.close)) continue;
                scheduled++;
                last = Mathf.Max(last, shells[shells.Count - 1].at);
            }
            if (scheduled == 0) return false;
            bigPlayed = true;
            bigLate = false;
            bigDone = done;
            timeline.Begin(last - Time.time + 0.35f, config.breatherSeconds);
            if (enemies != null) enemies.HoldFire = true;
            foreach (var a in allies) if (a != null) a.TakeCover(true);
            // the section leader's shout, half lost under the first shells
            if (subtitles != null && text != null && !string.IsNullOrEmpty(text.shellingIncoming.text)) subtitles.Say(text.shellingIncoming);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BigRounds = scheduled;
            BigBegan = Time.time;
#endif
            return true;
        }

        void TickBarrage(DemoFlow flow, float dt)
        {
            // the late start (the scouts' section ran out): after the first flare sequence, in the open
            if (bigLate && !timeline.Active && flow.Section == FlowSection.Assault && flow.SectionTime > 20.0 && CutsceneRig.Running == 0
                && !(subtitles != null && subtitles.Speaking) && !(coordinator != null && coordinator.Dying))
            {
                bigLate = false;
                BeginBig(null, 0.6f);
            }
            if (!timeline.Active) return;
            switch (timeline.Tick(dt))
            {
                case BarrageTimeline.Step.BreatherBegan:
                    // it stops dead: nothing more falls, the far guns are not heard, only breath and settling earth
                    sounds.RemoveAll(s => s.kind == DistantReport || s.kind == FarGun || s.kind == Incoming);
                    if (sound != null)
                    {
                        sound.HushDistant();
                        sound.ForceBreath(config.breatherSeconds + 1.5f, config.breatherBreath);
                    }
                    var at = player.transform.position + player.transform.forward * 7f;
                    sounds.Add(new PendingSound { at = Time.time + 0.5f, kind = Debris, point = at, gain = 0.5f });
                    sounds.Add(new PendingSound { at = Time.time + config.breatherSeconds * 0.45f, kind = Debris, point = at + player.transform.right * 5f, gain = 0.3f });
                    // the men beside him lift their heads as the dust thins
                    sounds.Add(new PendingSound { at = Time.time + config.breatherSeconds * 0.55f, kind = HeadsUp });
                    break;
                case BarrageTimeline.Step.Finished:
                    EndBarrage(true);
                    break;
            }
        }

        void EndBarrage(bool report)
        {
            if (enemies != null) enemies.HoldFire = false;
            foreach (var a in allies) if (a != null) a.TakeCover(false);
            var done = bigDone;
            bigDone = null;
            if (report) done?.Invoke();
        }

        void CancelBarrage()
        {
            bool was = timeline.Active;
            timeline.Cancel();
            dust = 0f;
            RestoreFog();
            if (was || bigDone != null) EndBarrage(false);
        }

        // ---- dust: for a few seconds after a round right in front, the air is full of it ----
        void RaiseDust(float amount)
        {
            if (config.dustSeconds <= 0f || config.dustDensity <= 0f) return;
            if (!fogHeld)
            {
                fogDensity = RenderSettings.fogDensity;
                fogColor = RenderSettings.fogColor;
                fogHeld = true;
            }
            dustFrom = Mathf.Max(dust, amount);
            dust = dustFrom;
            dustBegan = Time.time;
        }

        void RestoreFog()
        {
            if (!fogHeld) return;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
            fogHeld = false;
        }

        void LateUpdate()
        {
            if (!fogHeld) return;
            // a sequence camera takes the fog over while it runs: hand it back clean
            if (CutsceneRig.Running > 0) { dust = 0f; RestoreFog(); return; }
            float u = (Time.time - dustBegan) / Mathf.Max(0.1f, config.dustSeconds);
            // thick for the first third, then it thins and is gone
            dust = dustFrom * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, u)));
            if (u >= 1f) { dust = 0f; RestoreFog(); return; }
            RenderSettings.fogDensity = Mathf.Lerp(fogDensity, config.dustDensity, dust);
            RenderSettings.fogColor = Color.Lerp(fogColor, config.dustColor, dust * 0.85f);
        }

        void CollectKeepouts(float playerKeep)
        {
            keepouts.Clear();
            keepouts.Add(new ShellSafety.Keepout(player.transform.position, playerKeep));
            foreach (var a in allies)
                if (a != null && a.gameObject.activeInHierarchy) keepouts.Add(new ShellSafety.Keepout(a.transform.position, config.keepAlly));
            if (enemies != null)
            {
                foreach (var e in enemies.ActiveEnemies)
                    if (e != null) keepouts.Add(new ShellSafety.Keepout(e.transform.position, config.keepEnemy));
                // the fallen and their rifles lie under the director
                var root = enemies.transform;
                for (int i = 0; i < root.childCount; i++)
                    keepouts.Add(new ShellSafety.Keepout(root.GetChild(i).position, config.keepBody));
            }
            if (points != null)
            {
                foreach (var c in points.assaultCovers) if (c != null) keepouts.Add(new ShellSafety.Keepout(c.position, config.keepCover));
                foreach (var c in points.scoutCovers) if (c != null) keepouts.Add(new ShellSafety.Keepout(c.position, config.keepCover));
                foreach (var c in points.ammoCrates) if (c != null) keepouts.Add(new ShellSafety.Keepout(c.position, config.keepCrate));
                if (points.target != null) keepouts.Add(new ShellSafety.Keepout(points.target.position, config.keepCover));
            }
            if (tank != null && tank.Visible)
            {
                keepouts.Add(new ShellSafety.Keepout(tank.transform.GetChild(0).position, config.keepTank));
                foreach (var p in tank.Craters.Points) keepouts.Add(new ShellSafety.Keepout(p, config.keepCrater));
            }
            else if (tank != null && tank.Craters != null)
                foreach (var p in tank.Craters.Points) keepouts.Add(new ShellSafety.Keepout(p, config.keepCrater));
            foreach (var s in shells) keepouts.Add(new ShellSafety.Keepout(s.point, config.keepCrater));
        }

        void Blast(Vector3 point, bool close = false)
        {
            var eye = view != null ? view.transform.position : player.CameraPivot.position;
            float distance = Vector3.Distance(eye, point);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NearFired++;
            LastBlast = point;
            LastBlastDistance = distance;
            ClosestBlast = Mathf.Min(ClosestBlast, distance);
#endif
            if (effects != null && config.blastPrefab != null)
            {
                var go = Instantiate(config.blastPrefab, point, Quaternion.Euler(0f, Rand(0f, 360f), 0f), effects.Container);
                var flash = go.GetComponentInChildren<ShellFlash>();
                if (flash != null) flash.Fire(config.blastLight, config.blastLightRange, config.blastLightSeconds);
                Destroy(go, 9f);
            }
            if (effects != null && config.smolderPrefab != null && config.smolderSeconds > 0f)
            {
                var go = Instantiate(config.smolderPrefab, point, Quaternion.identity, effects.Container);
                var smoke = go.GetComponent<Smolder>();
                if (smoke != null) smoke.Burn(config.smolderSeconds * Rand(0.75f, 1.2f));
            }
            if (tank != null && tank.Craters != null)
                tank.Craters.Make(point, config.craterRadius * Rand(0.85f, 1.15f), config.craterDepth, config.craterRim, 0f);

            // the report and the falling earth arrive with the speed of sound; the jolt through the ground is at once
            float travel = distance / Mathf.Max(1f, config.soundSpeed);
            // the entry's own roll-off is gentle (it carries 400 m): fall off faster across the field
            float loud = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(distance / 110f));
            sounds.Add(new PendingSound { at = Time.time + travel, kind = BlastReport, point = point, gain = loud });
            if (distance < 90f)
                sounds.Add(new PendingSound { at = Time.time + travel + Rand(0.55f, 0.9f), kind = Debris, point = point, gain = Mathf.Lerp(1f, 0.4f, distance / 90f) });

            bool inCut = CutsceneRig.Running > 0;
            if (close && !inCut)
            {
                // right in front of the position: the hardest jolt of the night (still short), the ears gone for
                // a few seconds, earth thrown over the parapet and the air thick with dust
                player.AddShake(config.closeShakeDegrees, config.closeShakeSeconds);
                if (sound != null) sound.Deafen(config.closeDeafenSeconds, 1f, true);
                RaiseDust(1f);
                if (effects != null && config.dirtShowerPrefab != null)
                {
                    var toward = eye + Vector3.up * 2.2f - point;
                    var shower = Instantiate(config.dirtShowerPrefab, point + Vector3.up * 0.3f, Quaternion.LookRotation(toward.normalized, Vector3.up), effects.Container);
                    Destroy(shower, 4f);
                }
            }
            else if (!inCut && distance < config.shakeRange)
            {
                float k = 1f - distance / config.shakeRange;
                player.AddShake(config.shakeDegrees * k * k * 1.6f, config.shakeSeconds * Mathf.Lerp(0.6f, 1f, k));
            }
            if (!close && !inCut && sound != null && distance < config.deafenRange)
                sound.Deafen(config.deafenSeconds * Mathf.Lerp(0.5f, 1f, 1f - distance / config.deafenRange), Mathf.Lerp(0.45f, 0.9f, 1f - distance / config.deafenRange), false);
            // the thrown earth takes a moment to come down, longer the further it has to fly
            if (!inCut && distance < config.trenchDustRange)
                sounds.Add(new PendingSound { at = Time.time + 0.55f + distance / 45f, kind = Rain, point = point });
        }

        // Earth thrown up by a near burst comes down again around the player a moment later: a patter of small
        // clods falling in from above the view. (First tried as earth slipping off the lip of the trench wall, but
        // the wall's colliders are stepped and the spot could not be found reliably; it was also barely seen.)
        void DirtRain(Vector3 blast)
        {
            if (config.trenchDustPrefab == null || effects == null) return;
            var at = player.transform.position;
            var away = at - blast; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) return;
            var look = view != null ? view.transform.forward : player.transform.forward; look.y = 0f;
            var spot = at + look.normalized * 2.4f + Vector3.up * 4.2f;
            var go = Instantiate(config.trenchDustPrefab, spot, Quaternion.LookRotation(away.normalized, Vector3.up), effects.Container);
            Destroy(go, 4f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TrenchDustSpawned++;
            LastTrenchDust = spot;
#endif
        }

        // ---- horizon: flashes beyond the far ridge and tracers above it ----
        const int FlashCount = 6, TracerCount = 16;
        // the middle of the glow lies below the far crest: only its upper fall-off shows above the ridge line
        const float HorizonRadius = 700f, FlashHeight = 62f;
        static readonly int GlowId = Shader.PropertyToID("_Glow"), GlowColorId = Shader.PropertyToID("_GlowColor"), CoreId = Shader.PropertyToID("_Core"), HaloId = Shader.PropertyToID("_Halo");
        Mesh quad;
        Transform horizon;
        MaterialPropertyBlock block;
        struct Flash { public MeshRenderer renderer; public float born, bright, second; public bool on; }
        struct Tracer { public MeshRenderer renderer; public Vector3 start, velocity; public float born, life; public Color colour; public bool on; }
        readonly Flash[] flashes = new Flash[FlashCount];
        readonly Tracer[] tracers = new Tracer[TracerCount];
        int nextFlash, nextTracer;

        void BuildHorizon()
        {
            if (config.horizonGlow == null) return;
            quad = new Mesh { name = "HorizonGlowQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            // it stands beyond the camera's far plane (the shader keeps it from being cut): never cull it away
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            horizon = new GameObject("[Horizon]").transform;
            horizon.SetParent(transform, false);
            block = new MaterialPropertyBlock();
            for (int i = 0; i < FlashCount; i++) flashes[i].renderer = NewQuad("Flash_" + i);
            for (int i = 0; i < TracerCount; i++) tracers[i].renderer = NewQuad("Tracer_" + i);
        }

        MeshRenderer NewQuad(string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(horizon, false);
            go.GetComponent<MeshFilter>().sharedMesh = quad;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = config.horizonGlow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        void ClearHorizon()
        {
            for (int i = 0; i < FlashCount; i++) { flashes[i].on = false; if (flashes[i].renderer != null) flashes[i].renderer.enabled = false; }
            for (int i = 0; i < TracerCount; i++) { tracers[i].on = false; if (tracers[i].renderer != null) tracers[i].renderer.enabled = false; }
        }

        void ShowFlash(float bearing, float bright, float size)
        {
            if (horizon == null) return;
            int i = nextFlash;
            nextFlash = (nextFlash + 1) % FlashCount;
            var dir = Direction(bearing);
            var t = flashes[i].renderer.transform;
            t.position = mapCentre + dir * HorizonRadius + Vector3.up * (FlashHeight * Rand(0.7f, 1.2f));
            t.rotation = Quaternion.LookRotation(dir, Vector3.up);
            t.localScale = new Vector3(1000f * size, 330f * size, 1f);
            flashes[i].born = Time.time;
            flashes[i].bright = bright;
            flashes[i].second = Rand() < 0.35f ? Rand(0.09f, 0.2f) : -1f; // some bursts flicker twice
            flashes[i].on = true;
        }

        void StartTracers(float bearing, Color colour, float sideways)
        {
            if (horizon == null) return;
            int rounds = 3 + (int)(Rand() * 4f);
            var dir = Direction(bearing);
            var across = Vector3.Cross(Vector3.up, dir) * sideways;
            // from behind the crest, flat across the sky, falling away again
            var start = mapCentre + dir * (HorizonRadius - 40f) + Vector3.up * Rand(95f, 150f);
            var velocity = across * Rand(75f, 120f) + Vector3.up * Rand(14f, 30f);
            float life = Rand(2.2f, 3.4f);
            for (int k = 0; k < rounds; k++)
            {
                int i = nextTracer;
                nextTracer = (nextTracer + 1) % TracerCount;
                tracers[i].start = start + Random.insideUnitSphere * 2f;
                tracers[i].velocity = velocity + Random.insideUnitSphere * 4f;
                tracers[i].born = Time.time + k * Rand(0.1f, 0.14f);
                tracers[i].life = life * Rand(0.9f, 1.1f);
                tracers[i].colour = colour;
                tracers[i].on = true;
            }
        }

        const float TracerDrop = 13f; // how fast the far tracers sink, as seen from here (m/s2 at the horizon's distance)

        void UpdateHorizon()
        {
            if (horizon == null) return;
            float now = Time.time;
            for (int i = 0; i < FlashCount; i++)
            {
                if (!flashes[i].on) continue;
                float age = now - flashes[i].born;
                float glow = Pulse(age);
                if (flashes[i].second > 0f) glow += 0.6f * Pulse(age - flashes[i].second);
                if (age > 1.4f) { flashes[i].on = false; flashes[i].renderer.enabled = false; continue; }
                block.SetFloat(GlowId, glow * flashes[i].bright);
                block.SetColor(GlowColorId, config.flashColor);
                block.SetFloat(CoreId, 0.5f);
                block.SetFloat(HaloId, 0.3f);
                flashes[i].renderer.SetPropertyBlock(block);
                flashes[i].renderer.enabled = true;
            }
            var eye = view != null ? view.transform.position : mapCentre;
            for (int i = 0; i < TracerCount; i++)
            {
                if (!tracers[i].on) continue;
                float age = now - tracers[i].born;
                if (age < 0f) { tracers[i].renderer.enabled = false; continue; }
                if (age > tracers[i].life) { tracers[i].on = false; tracers[i].renderer.enabled = false; continue; }
                var velocity = tracers[i].velocity + Vector3.down * (TracerDrop * age);
                var position = tracers[i].start + tracers[i].velocity * age + Vector3.down * (0.5f * TracerDrop * age * age);
                var t = tracers[i].renderer.transform;
                var toEye = (eye - position).normalized;
                var along = Vector3.ProjectOnPlane(velocity, toEye).normalized;
                t.position = position;
                t.rotation = Quaternion.LookRotation(-toEye, Vector3.Cross(toEye, along));
                t.localScale = new Vector3(15f, 3.4f, 1f);
                float fade = Mathf.Clamp01(age / 0.08f) * Mathf.Clamp01((tracers[i].life - age) / 0.5f);
                block.SetFloat(GlowId, 1.5f * fade);
                block.SetColor(GlowColorId, tracers[i].colour);
                block.SetFloat(CoreId, 0.5f);
                block.SetFloat(HaloId, 0f);
                tracers[i].renderer.SetPropertyBlock(block);
                tracers[i].renderer.enabled = true;
            }
        }

        // a burst seen over the hill: up in a couple of frames, away in a third of a second
        static float Pulse(float age)
        {
            if (age < 0f) return 0f;
            return age < 0.05f ? age / 0.05f : Mathf.Exp(-(age - 0.05f) / 0.16f);
        }
    }
}
