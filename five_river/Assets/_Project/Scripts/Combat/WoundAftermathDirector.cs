using FiveRivers.Audio;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 크게 맞은 뒤 몸에 남는 것 (2026-11-24): 순수 규칙 WoundAftermath 를 엔진에 잇는 얇은 어댑터. PlayerHealth.Awake 가 붙인다.
    //  - 손 떨림: PlayerController.AimTremor(도)를 정한다. 조준(시선) 자체가 약간 흔들리므로 화면의 조준점과 실제 탄도가 같다.
    //    M1 · 바주카 1인칭 화면은 시점을 따라가므로 따로 고치지 않는다 (M1Presentation · BazookaPresentation 을 건드리지 않음).
    //  - 숨소리 · 귀 울림: AudioDirector.ForceBreath / Deafen (대사 중 덕킹 · 먹먹함 복구는 그쪽 규칙)
    //  - 화면 가장자리 효과는 Hud 가 이미 맡고 있어 더하지 않는다.
    // 사망 · 체크포인트 복구 · 새 회차에서 바로 0.
    [DefaultExecutionOrder(-100)]
    public sealed class WoundAftermathDirector : MonoBehaviour
    {
        PlayerHealth health;
        PlayerConfig config;
        PlayerController player;
        AudioDirector sound;
        FlowDirector director;
        WoundAftermath wound;

        public float Level => wound != null ? wound.Level : 0f;
        public float Remaining => wound != null ? wound.Remaining : 0f;
        public Vector2 LastTremor { get; private set; }

        public void Bind(PlayerHealth health, PlayerConfig config)
        {
            this.health = health;
            this.config = config;
        }

        void Start()
        {
            if (health == null || config == null) { enabled = false; return; }
            wound = new WoundAftermath(new WoundRules
            {
                BigHitFraction = config.woundBigHitFraction, SevereFraction = config.woundSevereFraction,
                MinSeconds = config.woundMinSeconds, MaxSeconds = config.woundMaxSeconds, FadeSeconds = config.woundFadeSeconds,
                BaseIntensity = config.woundBaseIntensity, IntensityCap = config.woundIntensityCap,
                TremorHipDegrees = config.woundTremorHipDegrees, TremorAimDegrees = config.woundTremorAimDegrees, TremorHz = config.woundTremorHz,
            });
            player = FindFirstObjectByType<PlayerController>();
            sound = FindFirstObjectByType<AudioDirector>();
            director = FindFirstObjectByType<FlowDirector>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (director != null)
            {
                director.Flow.RetryRequested += OnRetry;
                director.Flow.SectionChanged += OnSectionChanged;
            }
        }

        void OnDestroy()
        {
            if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; }
            if (director != null && director.Flow != null)
            {
                director.Flow.RetryRequested -= OnRetry;
                director.Flow.SectionChanged -= OnSectionChanged;
            }
        }

        void OnDamaged(float amount, Vector3 from)
        {
            // 쓰러진 한 방(직격 · 마지막 한 발)은 죽음이 맡는다: 몸에 남길 것이 없다
            if (health.Current <= 0f) return;
            var hit = wound.Hit(amount, health.Max);
            if (!hit.Big) return;
            if (sound == null) return;
            sound.ForceBreath(wound.Remaining, wound.BreathLevel(config.woundBreathMax));
            if (hit.Severe) sound.Deafen(config.woundDeafSeconds, config.woundDeafAmount);
        }

        void OnDied() => Clear();
        void OnRetry(FlowCheckpoint checkpoint) => Clear();

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Title || t.Reason == TransitionReason.RunStart) Clear();
        }

        void Clear()
        {
            if (wound == null) return;
            bool was = wound.Active;
            wound.Reset();
            LastTremor = Vector2.zero;
            if (player != null) player.AimTremor = Vector2.zero;
            // 다른 곳(포격 직후의 숨)이 건 숨은 건드리지 않는다
            if (was && sound != null) sound.ForceBreath(0f, 0f);
        }

        void Update()
        {
            if (wound == null || player == null) return;
            if (!wound.Active)
            {
                if (LastTremor != Vector2.zero) { LastTremor = Vector2.zero; player.AimTremor = Vector2.zero; }
                return;
            }
            // 게임 시계(Time.time)를 쓴다: 일시정지 중에는 멎는다
            float dt = Time.deltaTime;
            wound.Tick(dt);
            var (yaw, pitch) = wound.Tremor(Time.time, player.Aiming);
            LastTremor = new Vector2(yaw, pitch);
            player.AimTremor = LastTremor;
            // 효과가 끝나는 프레임: 숨 시간이 줄어들며 같이 풀린다 (ForceBreath 가 건 시간이 끝나면 AudioDirector 가 3초에 걸쳐 가라앉힌다)
        }
    }
}
