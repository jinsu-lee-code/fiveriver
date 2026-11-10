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
        readonly Dictionary<AudioSource, SoundLibrary.Entry> poolEntries = new Dictionary<AudioSource, SoundLibrary.Entry>();
        readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource ambience, music, voice, breath;
        float sprintTime, breathLevel;
        bool clipRemovePending;
        AudioSource tankEngine, tankDamage;
        // Distant shelling: a few muffled voices so salvos can overlap. Each shell gets its own distance
        // (level + low-pass), side (pan) and pitch so the front sounds wide and far, never in your face.
        readonly AudioSource[] artillery = new AudioSource[3];
        readonly AudioLowPassFilter[] artilleryMuffle = new AudioLowPassFilter[3];
        readonly float[] artilleryLevel = new float[3];
        int artilleryVoice;
        int salvoLeft;
        float duck = 1f;
        float stepDistance;
        float lastMoved;
        float nextArtillery;
        bool wasPaused;

        void Awake()
        {
            for (int i = 0; i < poolSize; i++) pool.Add(NewSource("OneShot_" + i, transform));
            ambience = NewSource("Ambience", transform);
            music = NewSource("Music", transform);
            voice = NewSource("Voice", transform);
            breath = NewSource("Breath", transform);
            for (int i = 0; i < artillery.Length; i++)
            {
                artillery[i] = NewSource("DistantArtillery_" + i, transform);
                artilleryMuffle[i] = artillery[i].gameObject.AddComponent<AudioLowPassFilter>();
            }
        }

        void Start()
        {
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.RetryRequested += _ => ClearTransientAudio();

            weapons.M1.Fired += () => Play2D("SFX_M1_Fire");
            weapons.M1.ClipEjected += () => Play2D("SFX_M1_Ping");
            weapons.M1.DryFired += () => Play2D("SFX_M1_DryFire");
            // the part-used clip is pulled out about a third of the way in (reference reload video), not at the key press
            weapons.M1.ReloadStarted += kind => clipRemovePending = kind == ReloadKind.Partial;
            weapons.M1.ClipInserted += () => Play2D("SFX_M1_ClipInsert");
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
            // The flare is heard once, where it pops open in the sky; no hiss while it burns.
            flares.Burst += p => PlaySpatial("SFX_Flare_Launch", p);
            tank.Stopped += () =>
            {
                PlayAt("SFX_Tank_EngineStall", tank.transform.GetChild(0).position);
                SetTankLoops();
            };
            tank.SideHit += _ => SetTankLoops();

            subtitles.LineStarted += line => PlayVoice(line.voice);
            subtitles.LineEnded += StopVoice;
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
            if (t.To == FlowSection.Title || t.Reason == TransitionReason.RunStart)
                ClearTransientAudio();
            if (t.From == FlowSection.Title && t.To == FlowSection.Tutorial)
                Play2D("UI_Menu_Click");
            switch (t.To)
            {
                case FlowSection.Title:
                    SetLoop(ambience, null);
                    SetLoop(music, null); // Title music is disabled; the authored clip remains in the library.
                    StopVoice();
                    break;
                case FlowSection.Tutorial:
                case FlowSection.Scout:
                    SetLoop(music, null);
                    SetLoop(ambience, "AMB_Night_Hills_Loop");
                    break;
                case FlowSection.Assault:
                case FlowSection.WestAssault:
                    SetLoop(ambience, "AMB_Battle_Distant_Loop");
                    SetLoop(music, "BGM_Combat_Bed_Loop");
                    break;
                case FlowSection.Regroup:
                case FlowSection.Silence:
                    SetLoop(music, null); // 정적 구간에는 음악 없음 (SOUND_LIST 1-6)
                    SetLoop(ambience, "AMB_Silence_Loop");
                    break;
                case FlowSection.Tank:
                    SetLoop(ambience, "AMB_Night_Hills_Loop");
                    break;
                case FlowSection.Dawn:
                    SetLoop(ambience, "AMB_Dawn_Loop");
                    if (director.Flow.IsExpanded) SetLoop(music, null);
                    else SetLoop(music, "BGM_Ending", loop: false);
                    break;
                case FlowSection.HillAssault:
                    SetLoop(ambience, "AMB_Battle_Distant_Loop");
                    SetLoop(music, "BGM_Combat_Bed_Loop");
                    break;
                case FlowSection.Result:
                    if (director.Flow.IsExpanded)
                    {
                        SetLoop(ambience, "AMB_Dawn_Loop");
                        SetLoop(music, "BGM_Ending", loop: false);
                    }
                    break;
            }
            if (ArtilleryAllowed(t.To)) ScheduleArtillery();
            else StopArtillery();
            SetTankLoops();
        }

        void SetTankLoops()
        {
            // Tank loops only while the tank is actually on stage (its own section and the ally-support finish).
            // After the fight the wreck can stay active, but Dawn, the hill assault and the result stay tank-silent.
            var section = director.Flow.Section;
            bool visible = tank.Visible && (section == FlowSection.Tank || section == FlowSection.AllySupport);
            if (!visible)
            {
                tankEngine.Stop();
                tankDamage.Stop();
                return;
            }
            SetLoop(tankEngine, tank.IsStopped ? null : tank.IsMoving ? "SFX_Tank_Move_Loop" : "SFX_Tank_EngineIdle_Loop");
            SetLoop(tankDamage, tank.IsStopped ? "SFX_Tank_Fire_Loop" : tank.SideHits > 0 ? "SFX_Tank_Smoke_Loop" : null);
        }

        void OnEffect(EffectKind kind, Vector3 position)
        {
            switch (kind)
            {
                case EffectKind.ImpactDirt: PlayAt("SFX_Impact_Dirt", position); break;
                case EffectKind.CannonBlast:
                case EffectKind.RocketExplosion:
                case EffectKind.TankHitExplosion: PlayAt("SFX_Rocket_HitSide", position); break;
                case EffectKind.TankRicochet: PlayAt("SFX_Tank_Ricochet", position); break;
                case EffectKind.TankMuzzle: PlayAt("SFX_Tank_Cannon", position); break;
                case EffectKind.EnemyMuzzle: PlayAt("SFX_Enemy_RifleFire", position); break;
            }
        }

        void Update()
        {
            if (wasPaused != director.Flow.Paused)
            {
                wasPaused = director.Flow.Paused;
                Play2D("UI_Menu_Click");
                if (wasPaused) StopArtillery();
                else if (ArtilleryAllowed(director.Flow.Section)) ScheduleArtillery();
            }
            // Approach -> Holding changes without a section transition.
            SetTankLoops();
            // 대사 중 SFX · AMB를 낮춘다
            float target = voice.isPlaying || subtitles.Speaking ? Mathf.Pow(10f, config.duckDb / 20f) : 1f;
            duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, config.duckFade));
            foreach (var source in pool)
            {
                if (!source.isPlaying || !poolEntries.TryGetValue(source, out var entry)) continue;
                float duckable = entry.bus == SoundBus.Sfx || entry.bus == SoundBus.Amb ? duck : 1f;
                poolGain.TryGetValue(source, out float gain);
                source.volume = BusVolume(entry.bus) * entry.volume * duckable * (gain > 0f ? gain : 1f);
            }
            ambience.volume = Volume("amb") * VolumeOf(ambience.clip) * duck;
            music.volume = Volume("bgm") * VolumeOf(music.clip);
            tankEngine.volume = Volume("sfx") * VolumeOf(tankEngine.clip) * duck;
            tankDamage.volume = Volume("sfx") * VolumeOf(tankDamage.clip) * duck;
            for (int i = 0; i < artillery.Length; i++)
                artillery[i].volume = Volume("sfx") * VolumeOf(artillery[i].clip) * artilleryLevel[i] * duck;

            if (clipRemovePending && (!weapons.M1.Reloading || weapons.M1.ReloadProgress >= .31f))
            {
                if (weapons.M1.Reloading) Play2D("SFX_M1_ClipRemove");
                clipRemovePending = false;
            }
            UpdateFootsteps();
            UpdateBreath();
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
                // Rifle, sling and webbing move with every running step; walking only stirs them now and then.
                if (player.Sprinting || Random.value < 0.3f) Play("SFX_Gear_Run", null, gain: player.Sprinting ? Random.Range(0.8f, 1f) : 0.4f);
            }
        }

        // Heavy breathing builds up after a few seconds of running and dies away after stopping.
        void UpdateBreath()
        {
            sprintTime = player.Sprinting ? sprintTime + Time.deltaTime : Mathf.Max(0f, sprintTime - Time.deltaTime * 0.6f);
            float target = Mathf.Clamp01((sprintTime - 2f) / 3f);
            breathLevel = Mathf.MoveTowards(breathLevel, target, Time.deltaTime / (target > breathLevel ? 2f : 3f));
            if (breathLevel > 0f && !breath.isPlaying) SetLoop(breath, "SFX_Player_Breath_Run_Loop");
            if (breathLevel <= 0f && breath.isPlaying) breath.Stop();
            breath.volume = Volume("sfx") * VolumeOf(breath.clip) * breathLevel * duck;
        }

        // Preserve the existing tutorial/silence ambience and include the tank battle.
        static bool ArtilleryAllowed(FlowSection section) => (section >= FlowSection.Tutorial && section <= FlowSection.Tank)
            || section == FlowSection.WestAssault || section == FlowSection.HillAssault;

        void ScheduleArtillery()
        {
            nextArtillery = Time.time + Random.Range(config.artilleryMin, config.artilleryMax);
            salvoLeft = Random.value < 0.4f ? Random.Range(1, 3) : 0; // sometimes 2-3 shells land close together
        }

        void StopArtillery()
        {
            foreach (var a in artillery) a.Stop();
            nextArtillery = float.PositiveInfinity;
            salvoLeft = 0;
        }

        void UpdateArtillery()
        {
            var s = director.Flow.Section;
            if (!ArtilleryAllowed(s) || director.Flow.Paused || Time.time < nextArtillery) return;
            FireDistantShell(s);
            if (salvoLeft > 0)
            {
                salvoLeft--;
                nextArtillery = Time.time + Random.Range(0.35f, 1.6f);
            }
            else ScheduleArtillery();
        }

        void FireDistantShell(FlowSection section)
        {
            int i = artilleryVoice;
            artilleryVoice = (artilleryVoice + 1) % artillery.Length;
            var source = artillery[i];
            Play("SFX_Artillery_Distant", null, targetSource: source);
            float far = Random.value; // 0 = the nearer ridge, 1 = far down the valley
            artilleryLevel[i] = Mathf.Lerp(0.38f, 0.14f, far) * (section == FlowSection.Silence ? 0.6f : 1f);
            artilleryMuffle[i].cutoffFrequency = Mathf.Lerp(1400f, 450f, far);
            source.panStereo = Random.Range(-0.75f, 0.75f);
            source.pitch *= Mathf.Lerp(1f, 0.85f, far);
            source.volume = Volume("sfx") * VolumeOf(source.clip) * artilleryLevel[i] * duck;
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

        // A world-space M1 uses the same non-verbal clip and mixer settings as the player's rifle.
        public void PlaySpatial(string id, Vector3 position) => Play(id, position, true);

        readonly Dictionary<AudioSource, float> poolGain = new Dictionary<AudioSource, float>();

        void Play(string id, Vector3? position, bool forceSpatial = false, AudioSource targetSource = null, float gain = 1f)
        {
            var e = library.Find(id);
            if (e == null || !Ready(e)) return;
            var clip = Pick(e);
            if (clip == null) return;
            var source = targetSource != null ? targetSource : FreeSource();
            if (targetSource == null)
            {
                poolEntries[source] = e;
                poolGain[source] = gain;
            }
            source.clip = clip;
            source.loop = false;
            source.pitch = 1f + Random.Range(-e.pitchVariance, e.pitchVariance);
            bool spatial = (e.spatial || forceSpatial) && position.HasValue;
            source.spatialBlend = spatial ? 1f : 0f;
            source.maxDistance = e.maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.transform.position = position ?? Vector3.zero;
            float duckable = e.bus == SoundBus.Sfx || e.bus == SoundBus.Amb ? duck : 1f;
            source.volume = BusVolume(e.bus) * e.volume * duckable * gain;
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
            StopVoice();
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

        void ClearTransientAudio()
        {
            foreach (var source in pool) source.Stop();
            poolEntries.Clear();
            StopVoice();
            breath.Stop();
            sprintTime = breathLevel = 0f;
            foreach (var a in artillery) a.Stop();
            lastPlayed.Clear();
            lastVariant.Clear();
            stepDistance = 0f;
            lastMoved = player.MovedDistance;
            duck = 1f;
            ScheduleArtillery();
        }

        void StopVoice() => voice.Stop();
    }
}
