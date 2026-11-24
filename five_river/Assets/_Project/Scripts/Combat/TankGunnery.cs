using System;

namespace FiveRivers.Combat
{
    // 전차포가 조준을 좁혀 들어오는 규칙 (결정 2026-11-24). 엔진 의존 없음.
    // 노출된 채 쏘면 첫 발 멀리 → 둘째 발 가까이 → 셋째 발 직격. 노출되지 않은 채 쏜 발은 처음으로 되돌리고 멀리 떨어진다.
    // 지난 발보다 크게 옮겨 서 있으면 한 단계 뒤로 물러난다. 직격만 체력과 상관없이 사망시킨다 (빗나간 포탄은 피해 0).
    public enum GunneryKind { Far = 0, Mid = 1, Direct = 2 }

    public readonly struct GunneryShot
    {
        public readonly GunneryKind Kind;
        public readonly bool Direct;
        public readonly float Distance;   // 플레이어에서 착탄점까지 (축 방향 성분, 직격이면 0 근처)
        public readonly bool Beyond;      // true = 플레이어 너머(전차 반대쪽), false = 전차 쪽
        public readonly float Lateral;    // 전차-플레이어 축에서 옆으로 벗어난 m
        public readonly int Stage;        // 이 발을 쏠 때의 조준 단계 (0부터)
        public GunneryShot(GunneryKind kind, float distance, bool beyond, float lateral, int stage)
        {
            Kind = kind; Direct = kind == GunneryKind.Direct; Distance = distance; Beyond = beyond; Lateral = lateral; Stage = stage;
        }
    }

    public struct GunneryRules
    {
        public int ShotsToDirect;      // 3
        public float FarMin, FarMax;   // 9 ~ 13
        public float MidMin, MidMax;   // 3 ~ 5
        public float DirectScatter;    // 0.4
        public float Lateral;          // 1.5
        public float MoveReset;        // 4

        public static GunneryRules Default => new GunneryRules
        {
            ShotsToDirect = 3, FarMin = 9f, FarMax = 13f, MidMin = 3f, MidMax = 5f, DirectScatter = 0.4f, Lateral = 1.5f, MoveReset = 4f,
        };
    }

    public sealed class TankGunnery
    {
        readonly GunneryRules rules;
        int stage;

        public TankGunnery(GunneryRules rules)
        {
            this.rules = rules;
            if (this.rules.ShotsToDirect < 1) this.rules.ShotsToDirect = 1;
        }

        // 다음에 쏠 발의 조준 단계 (0 = 아직 아무것도 안 좁혔음)
        public int Stage => stage;

        // 새 회차 · 재시도 · 전차가 다시 나타날 때
        public void Reset() => stage = 0;

        // exposed: 쏘는 순간 포구에서 플레이어 머리가 보이는가. moved: 지난 발을 쏠 때 선 자리에서 지금까지 옮긴 m.
        // unit: [0,1) 난수 (테스트에서는 고정값). 직격이든 아니든 호출 한 번이 한 발이다.
        public GunneryShot Next(bool exposed, float moved, Func<float> unit)
        {
            int last = rules.ShotsToDirect - 1;
            if (!exposed) stage = 0;
            else if (moved >= rules.MoveReset && stage > 0) stage--;

            int k = Math.Min(stage, last);
            bool beyond = unit() < 0.5f;
            float lateral = (unit() * 2f - 1f) * rules.Lateral;
            GunneryShot shot;
            if (k >= last)
                shot = new GunneryShot(GunneryKind.Direct, unit() * rules.DirectScatter, beyond, (unit() * 2f - 1f) * rules.DirectScatter, k);
            else if (k == 0)
                shot = new GunneryShot(GunneryKind.Far, rules.FarMin + unit() * (rules.FarMax - rules.FarMin), beyond, lateral, k);
            else
                shot = new GunneryShot(GunneryKind.Mid, rules.MidMin + unit() * (rules.MidMax - rules.MidMin), beyond, lateral, k);

            // 노출되지 않았으면 단계는 처음(0)에 머문다. 노출됐으면 한 단계 앞으로.
            stage = exposed ? Math.Min(k + 1, last) : 0;
            return shot;
        }
    }
}
