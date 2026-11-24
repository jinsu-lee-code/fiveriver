using System;

namespace FiveRivers.Combat
{
    // 크게 맞은 뒤 몸에 남는 것 (결정 2026-11-24, "결과는 되돌릴 수 없게"). 엔진 의존 없음.
    // 한 번에 최대 체력의 bigHitFraction 이상 맞으면 minSeconds ~ maxSeconds 동안 손 떨림 · 가쁜 숨이 남는다.
    // 크게 맞을수록 세고 오래 간다. 다시 맞으면 세기가 쌓이되 intensityCap 에서 멈추고, 남는 시간도 maxSeconds 를 넘지 않는다.
    // 끝나기 fadeSeconds 전부터 서서히 풀린다. 사망 · 체크포인트 복구 · 새 회차는 Reset 으로 바로 0.
    public struct WoundRules
    {
        public float BigHitFraction;     // 0.4: 한 번에 이 비율 이상 맞으면 크게 맞은 것
        public float SevereFraction;     // 0.75: 이 비율 이상이면 귀 울림 · 먹먹함
        public float MinSeconds, MaxSeconds;   // 10 ~ 20
        public float FadeSeconds;        // 4
        public float BaseIntensity;      // 0.55: 문턱에서 맞았을 때 쌓이는 세기 (가득 맞으면 1)
        public float IntensityCap;       // 1
        public float TremorHipDegrees;   // 0.35
        public float TremorAimDegrees;   // 0.10
        public float TremorHz;           // 3 (2~4 Hz)

        public static WoundRules Default => new WoundRules
        {
            BigHitFraction = 0.4f, SevereFraction = 0.75f, MinSeconds = 10f, MaxSeconds = 20f, FadeSeconds = 4f,
            BaseIntensity = 0.55f, IntensityCap = 1f, TremorHipDegrees = 0.35f, TremorAimDegrees = 0.10f, TremorHz = 3f,
        };
    }

    public readonly struct WoundHit
    {
        public readonly bool Big;       // 효과가 켜졌거나 늘어났다
        public readonly bool Severe;    // 귀 울림도 낸다
        public WoundHit(bool big, bool severe) { Big = big; Severe = severe; }
    }

    public sealed class WoundAftermath
    {
        readonly WoundRules rules;
        float intensity, remaining;

        public WoundAftermath(WoundRules rules)
        {
            this.rules = rules;
            if (this.rules.MaxSeconds < this.rules.MinSeconds) this.rules.MaxSeconds = this.rules.MinSeconds;
            if (this.rules.IntensityCap <= 0f) this.rules.IntensityCap = 1f;
            if (this.rules.BigHitFraction <= 0f) this.rules.BigHitFraction = 0.01f;
        }

        public float Remaining => remaining;
        public bool Active => remaining > 0f && intensity > 0f;

        // 0~1: 지금 몸에 남은 세기. 끝나기 fadeSeconds 전부터 줄어든다.
        public float Level
        {
            get
            {
                if (!Active) return 0f;
                float fade = rules.FadeSeconds <= 0f ? 1f : Math.Min(1f, remaining / rules.FadeSeconds);
                return Math.Min(rules.IntensityCap, intensity) * fade;
            }
        }

        // damage: 이번에 잃은 체력, maxHealth: 최대 체력. 크게 맞았으면 효과를 켜거나 키운다.
        public WoundHit Hit(float damage, float maxHealth)
        {
            if (maxHealth <= 0f || damage <= 0f) return new WoundHit(false, false);
            float fraction = damage / maxHealth;
            if (fraction < rules.BigHitFraction) return new WoundHit(false, false);
            float span = Math.Max(0.0001f, 1f - rules.BigHitFraction);
            float t = Math.Min(1f, Math.Max(0f, (fraction - rules.BigHitFraction) / span));
            float add = rules.BaseIntensity + (1f - rules.BaseIntensity) * t;
            intensity = Math.Min(rules.IntensityCap, intensity + add);
            float seconds = rules.MinSeconds + (rules.MaxSeconds - rules.MinSeconds) * t;
            // 겹치면 더 오래 가지만 상한을 넘지 않는다 (남은 시간에 이번 시간의 절반을 더하고, 이번 시간보다는 길게)
            remaining = Math.Min(rules.MaxSeconds, Math.Max(seconds, remaining + seconds * 0.5f));
            return new WoundHit(true, fraction >= rules.SevereFraction);
        }

        public void Tick(float dt)
        {
            if (remaining <= 0f) return;
            remaining -= dt;
            if (remaining <= 0f) Reset();
        }

        public void Reset()
        {
            intensity = 0f;
            remaining = 0f;
        }

        // 손 떨림(도). x = 좌우, y = 위아래. 조준 중에는 약하다. 세 사인의 합이라 어느 때든 |값| ≤ 진폭.
        public (float yaw, float pitch) Tremor(float time, bool aiming)
        {
            float level = Level;
            if (level <= 0f) return (0f, 0f);
            float amplitude = level * (aiming ? rules.TremorAimDegrees : rules.TremorHipDegrees);
            double w = 2.0 * Math.PI * Math.Max(0.1f, rules.TremorHz);
            // 2~4 Hz 의 불규칙한 흔들림(서로 맞지 않는 세 사인) + 약 0.3 Hz 숨쉬기 흔들림(주로 위아래). 9 Hz 는 떨림이 아니라 진동으로 보여 쓰지 않는다 (Opus 검수 2026-11-24)
            double breathing = Math.Sin(2.0 * Math.PI * 0.3 * time + 0.8);
            double yaw = 0.40 * Math.Sin(w * time) + 0.25 * Math.Sin(w * 1.37 * time + 1.3) + 0.15 * Math.Sin(w * 0.71 * time + 0.4) + 0.20 * Math.Sin(w * 0.23 * time + 2.2);
            double pitch = 0.30 * Math.Sin(w * 1.13 * time + 2.1) + 0.20 * Math.Sin(w * 0.77 * time + 4.2) + 0.15 * Math.Sin(w * 1.61 * time + 3.3) + 0.35 * breathing;
            return ((float)(yaw * amplitude), (float)(pitch * amplitude));
        }

        // 가쁜 숨 세기 0~1 (AudioDirector.ForceBreath 의 level)
        public float BreathLevel(float max) => Math.Min(1f, Level * Math.Max(0f, max));
    }
}
