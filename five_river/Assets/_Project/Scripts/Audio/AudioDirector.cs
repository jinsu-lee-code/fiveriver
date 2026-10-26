using System.Collections.Generic;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using UnityEngine;

namespace FiveRivers.Audio
{
    // 소리 연결 (SOUND_LIST 1-6): 게임 사건 → SoundLibrary의 소리. 버스 볼륨, 대사 중 SFX · AMB 덕킹,
    // 변형 무작위(같은 변형 연속 금지) · 음높이 ±5%, 적 · 전차 · 조명탄은 3D, 플레이어 소리는 2D.
    // 정적 구간(S4)은 음악 없이 환경음 + 대사만. 처치 보상음은 없다.
    public sealed class AudioDirector : MonoBehaviour
    {
        [SerializeField] SoundLibrary library;
        [SerializeField] AudioConfig config;
        [SerializeField] FlowDirector director;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth health;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] Resupplier resupplier;
        [SerializeField] Effects effects;
        [SerializeField] EnemyDirector enemies;
        [SerializeField] TankController tank;
        [SerializeField] FlareDirector flares;
        [SerializeField] SubtitleDisplay subtitles;
        [SerializeField] ObjectiveDisplay objective;
        [SerializeField] int poolSize = 24;

        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource ambience, music, flareBurn, voice;
        AudioSource tankEngine, tankDamage;
        float duck = 1f;
        float stepDistance;
        float lastMoved;
        float nextArtillery;

        void Awake()
        {
            for (int i = 0; i < poolSize; i++) pool.Add(NewSource("OneShot_" + i, transform));
            ambience = NewSource("Ambience", transform);
            music = NewSource("Music", transform);
            flareBurn = NewSource("FlareBurn", transform);
            voice = NewSource("Voice", transform);
        }

        void Start()
        {
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;

            weapons.M1.Fired += () => Play2D("SFX_M1_Fire");
            weapons.M1.ClipEjected += () => Play2D("SFX_M1_Ping");
            weapons.M1.DryFired += () => Play2D("SFX_M1_DryFire");
            weapons.M1.ReloadStarted += kind => { if (kind == ReloadKind.Partial) Play2D("SFX_M1_ClipRemove"); };
            weapons.M1.Reloaded += () => Play2D("SFX_M1_ClipInsert");
            weapons.Bazooka.Fired += () => Play2D("SFX_Bazooka_Fire");
            weapons.Bazooka.Reloaded += () => Play2D("SFX_Bazooka_Load");
            weapons.Switched += _ => Play2D("SFX_Weapon_Switch");
            resupplier.Resupplied += () => Play2D("SFX_Ammo_Pickup");
            player.StanceChanged += () => Play2D("SFX_Stance_Change");
            health.Damaged += (_, __) =>
            {
                Play2D("SFX_Player_Hit");
                Play2D("VO_OSG_Hurt");
            };

            effects.Spawned += OnEffect;
            enemies.ShotMissedNearPlayer += p => PlayAt("SFX_Bullet_Whiz", p);
            enemies.EnemySpawned += p => { if (director.Flow.Section == FlowSection.Assault) PlayAt("VO_KPA_Shout", p); };
            flares.Launched += () => Play2D("SFX_Flare_Launch");
            tank.Stopped += () =>
            {
                PlayAt("SFX_Tank_EngineStall", tank.transform.GetChild(0).position);
                SetTankLoops();
            };
            tank.SideHit += _ => SetTankLoops();

            subtitles.LineStarted += line => PlayVoice(line.voice);
            objective.Shown += () => Play2D("UI_Objective_Show");

            var body = tank.transform.GetChild(0);
            tankEngine = NewSource("TankEngine", body);
            tankDamage = NewSource("TankDamage", body);
            OnSectionChanged(new FlowTransition(FlowSection.Title, flow.Section, TransitionReason.RunStart, 0));
        }

        static AudioSource NewSource(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            return s;
        }

        void OnSectionChanged(FlowTransition t)
        {
            switch (t.To)
            {
                case FlowSection.Title:
                    SetLoop(ambience, null);
                    SetLoop(music, "BGM_Title_Loop");
                    StopVoice();
                    break;
                case FlowSection.Tutorial:
                case FlowSection.Scout:
                    SetLoop(music, null);
                    SetLoop(ambience, "AMB_Night_Hills_Loop");
                    ScheduleArtillery();
                    break;
                case FlowSection.Assault:
                    SetLoop(ambience, "AMB_Battle_Distant_Loop");
                    SetLoop(music, "BGM_Combat_Bed_Loop");
                    break;
                case FlowSection.Silence:
                    SetLoop(music, null); // 정적 구간에는 음악 없음 (SOUND_LIST 1-6)
                    SetLoop(ambience, "AMB_Silence_Loop");
                    break;
                case FlowSection.Tank:
                    SetLoop(ambience, "AMB_Night_Hills_Loop");
                    break;
                case FlowSection.Dawn:
                    SetLoop(ambience, "AMB_Dawn_Loop");
                    SetLoop(music, "BGM_Ending", loop: false); // 새벽 20 + 결과 20 = 40초
                    break;
            }
            SetTankLoops();
        }

        void SetTankLoops()
        {
            bool visible = tank.Visible;
            if (!visible)
            {
                tankEngine.Stop();
                tankDamage.Stop();
                return;
            }
            SetLoop(tankEngine, tank.IsStopped ? null : "SFX_Tank_Move_Loop");
            SetLoop(tankDamage, tank.IsStopped ? "SFX_Tank_Fire_Loop" : tank.SideHits > 0 ? "SFX_Tank_Smoke_Loop" : null);
        }

        void OnEffect(EffectKind kind, Vector3 position)
        {
            switch (kind)
            {
                case EffectKind.ImpactDirt: PlayAt("SFX_Impact_Dirt", position); break;
                case EffectKind.RocketExplosion:
                case EffectKind.TankHitExplosion: PlayAt("SFX_Rocket_HitSide", position); break;
                case EffectKind.TankRicochet: PlayAt("SFX_Tank_Ricochet", position); break;
                case EffectKind.CannonBlast: PlayAt("SFX_Tank_Cannon", position); break;
                case EffectKind.EnemyMuzzle: PlayAt("SFX_Enemy_RifleFire", position); break;
            }
        }

        void Update()
        {
            // 대사 중 SFX · AMB를 낮춘다
            float target = voice.isPlaying || subtitles.Speaking ? Mathf.Pow(10f, config.duckDb / 20f) : 1f;
            duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, config.duckFade));
            ambience.volume = Volume("amb") * VolumeOf(ambience.clip) * duck;
            music.volume = Volume("bgm") * VolumeOf(music.clip);
            tankEngine.volume = Volume("sfx") * VolumeOf(tankEngine.clip) * duck;
            tankDamage.volume = Volume("sfx") * VolumeOf(tankDamage.clip) * duck;

            float illum = flares.Illumination;
            if (illum > 0f && !flareBurn.isPlaying) SetLoop(flareBurn, "SFX_Flare_Burn_Loop");
            if (illum <= 0f && flareBurn.isPlaying) flareBurn.Stop();
            flareBurn.volume = Volume("sfx") * illum * 0.6f * duck;

            UpdateFootsteps();
            UpdateArtillery();
        }

        void UpdateFootsteps()
        {
            float moved = player.MovedDistance;
            float delta = moved - lastMoved;
            lastMoved = moved;
            if (delta <= 0f || delta > 2f) return; // 순간이동(복구) 무시
            stepDistance += delta;
            float stride = player.Sprinting ? config.runStride : config.walkStride;
            if (stepDistance >= stride)
            {
                stepDistance = 0f;
                Play2D(player.Sprinting ? "SFX_Foot_Dirt_Run" : "SFX_Foot_Dirt");
            }
        }

        void ScheduleArtillery() => nextArtillery = Time.time + Random.Range(config.artilleryMin, config.artilleryMax);

        void UpdateArtillery()
        {
            var s = director.Flow.Section;
            if (s < FlowSection.Tutorial || s > FlowSection.Silence || Time.time < nextArtillery) return;
            Play2D("SFX_Artillery_Distant");
            ScheduleArtillery();
        }

        float Volume(string bus)
        {
            float v;
            switch (bus)
            {
                case "amb": v = config.amb; break;
                case "bgm": v = config.bgm; break;
                case "vo": v = config.vo; break;
                case "ui": v = config.ui; break;
                default: v = config.sfx; break;
            }
            return v * config.master;
        }

        float BusVolume(SoundBus bus)
        {
            switch (bus)
            {
                case SoundBus.Amb: return Volume("amb");
                case SoundBus.Bgm: return Volume("bgm");
                case SoundBus.Vo: return Volume("vo");
                case SoundBus.Ui: return Volume("ui");
                default: return Volume("sfx");
            }
        }

        float VolumeOf(AudioClip clip)
        {
            if (clip == null) return 0f;
            foreach (var e in library.entries)
                if (System.Array.IndexOf(e.clips, clip) >= 0) return e.volume;
            return 1f;
        }

        AudioClip Pick(SoundLibrary.Entry e)
        {
            if (e.clips.Length == 0) return null;
            if (e.clips.Length == 1) return e.clips[0];
            lastVariant.TryGetValue(e.id, out int last);
            int i = Random.Range(0, e.clips.Length - 1);
            if (i >= last) i++; // 같은 변형 연속 금지
            lastVariant[e.id] = i;
            return e.clips[i];
        }

        bool Ready(SoundLibrary.Entry e)
        {
            if (e.cooldown <= 0f) return true;
            if (lastPlayed.TryGetValue(e.id, out float t) && Time.unscaledTime - t < e.cooldown) return false;
            lastPlayed[e.id] = Time.unscaledTime;
            return true;
        }

        public void Play2D(string id) => Play(id, null);

        public void PlayAt(string id, Vector3 position) => Play(id, position);

        void Play(string id, Vector3? position)
        {
            var e = library.Find(id);
            if (e == null || !Ready(e)) return;
            var clip = Pick(e);
            if (clip == null) return;
            var source = FreeSource();
            source.clip = clip;
            source.loop = false;
            source.pitch = 1f + Random.Range(-e.pitchVariance, e.pitchVariance);
            bool spatial = e.spatial && position.HasValue;
            source.spatialBlend = spatial ? 1f : 0f;
            source.maxDistance = e.maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.transform.position = position ?? Vector3.zero;
            float duckable = e.bus == SoundBus.Sfx || e.bus == SoundBus.Amb ? duck : 1f;
            source.volume = BusVolume(e.bus) * e.volume * duckable;
            source.Play();
        }

        AudioSource FreeSource()
        {
            foreach (var s in pool)
                if (!s.isPlaying) return s;
            return pool[Random.Range(0, pool.Count)];
        }

        void SetLoop(AudioSource source, string id, bool loop = true)
        {
            var e = id != null ? library.Find(id) : null;
            var clip = e != null ? Pick(e) : null;
            if (source.clip == clip && (source.isPlaying || clip == null)) return;
            source.Stop();
            source.clip = clip;
            if (clip == null) return;
            source.loop = loop;
            source.spatialBlend = e.spatial ? 1f : 0f;
            source.maxDistance = e.maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.volume = BusVolume(e.bus) * e.volume;
            source.Play();
        }

        void PlayVoice(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var e = library.Find(id);
            if (e == null || e.clips.Length == 0) return;
            voice.Stop();
            voice.clip = Pick(e);
            voice.spatialBlend = 0f;
            voice.volume = BusVolume(SoundBus.Vo) * e.volume;
            voice.Play();
            if (voice.clip != null) subtitles.ExtendCurrent(voice.clip.length + 0.3f);
        }

        void StopVoice() => voice.Stop();
    }
}
