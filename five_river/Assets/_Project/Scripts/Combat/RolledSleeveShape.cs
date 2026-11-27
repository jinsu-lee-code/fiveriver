using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 걷은 소매의 반지름 (순수 계산, 2026-11-27). M1ArmRig 의 천 튜브가 링마다 부른다. 바주카 팔(roll_sleeves.py)과 같은 식.
    // f: 팔뚝 비율(팔꿈치 0 · 손목 1, 음수는 팔꿈치 위), angle: 단면 둘레 각도(rad), clothRadius: 원래 소매 반지름,
    // wristSkin: 손목 맨살 반지름. 돌려주는 값: 그 점의 반지름, isSkin: 맨살 구간인지.
    public static class RolledSleeveShape
    {
        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }

        public static float SkinRatio(RolledSleeveConfig c, float f)
        {
            var p = c.skinProfile;
            if (p == null || p.Length == 0) return 1f;
            if (f <= p[0].x) return p[0].y;
            for (int i = 1; i < p.Length; i++)
                if (f <= p[i].x) return Mathf.Lerp(p[i - 1].y, p[i].y, (f - p[i - 1].x) / Mathf.Max(1e-5f, p[i].x - p[i - 1].x));
            return p[p.Length - 1].y;
        }

        // the forearm's section: a wide, thin oval toward the wrist and two soft muscle masses below the elbow
        public static float SkinShape(RolledSleeveConfig c, float f, float angle)
        {
            float q = 1f + c.ellipse * Smooth(0.35f, 0.92f, f);
            float cs = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            float shape = 1f / Mathf.Sqrt(cs * cs / q + sn * sn * q);
            float Lobe(Vector4 l, float power) => l.w * Mathf.Exp(-Mathf.Pow((f - l.x) / l.y, 2f)) * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle - l.z)), power);
            return shape * (1f + Lobe(c.extensors, 2f) + Lobe(c.flexors, 2f) + Lobe(c.ulna, 8f));
        }

        // the rolled band: the sleeve kept, folded thicker, a ridge per roll
        public static float BandScale(RolledSleeveConfig c, float f)
        {
            float ridge = 0f;
            var g = c.folds;
            if (g != null)
                for (int i = 0; i + 1 < g.Length; i++)
                    if (f >= g[i] && f < g[i + 1]) ridge = Mathf.Sin(Mathf.PI * (f - g[i]) / Mathf.Max(1e-5f, g[i + 1] - g[i]));
            return c.bandScale * (1f + c.ridge * ridge);
        }

        public static float Radius(RolledSleeveConfig c, float f, float angle, float clothRadius, float wristSkin, out bool isSkin)
        {
            isSkin = f >= c.bandBottom;
            if (f < c.bandTop - 0.04f) return clothRadius;                          // the upper arm's sleeve as it was
            if (f < c.bandBottom)
                return clothRadius * (1f + (BandScale(c, f) - 1f) * Smooth(c.bandTop - 0.04f, c.bandTop, f));
            float skin = Mathf.Min(clothRadius, wristSkin * SkinRatio(c, f) * SkinShape(c, f, angle));
            float lip = Smooth(c.bandBottom, c.bandBottom + c.lipLength, f);
            return Mathf.Lerp(clothRadius * BandScale(c, c.bandBottom), skin, lip);
        }

        // the M1 sleeve's tube length t (shoulder 0, elbow 0.5, wrist 1) as forearm fraction f
        public static float ForearmFraction(float t, float upperLength, float lowerLength)
            => t >= 0.5f ? (t - 0.5f) * 2f : -(0.5f - t) * 2f * upperLength / Mathf.Max(1e-4f, lowerLength);
    }
}
