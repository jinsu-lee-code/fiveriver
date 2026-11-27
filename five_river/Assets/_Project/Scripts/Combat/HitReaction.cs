using UnityEngine;

namespace FiveRivers.Combat
{
    // 맞는 순간 계산 (부족한 점 6번, 2026-11-26). 순수 계산만: 맞은 부위 · 꺾임 곡선 · 맞은 방향에 맞는 쓰러짐 순서.
    // 화면에 붙이는 일은 EnemyAgent(꺾임) · EnemyRagdoll(물리)이 한다.
    public static class HitReaction
    {
        // 값은 끝에만 추가
        public enum Part { Chest, Head, Hips, LeftArm, RightArm, LeftLeg, RightLeg }

        // 맞은 점에서 가장 가까운 뼈 마디(시작 → 끝 선분). segments[i] 는 parts[i] 의 마디
        public static Part Classify(Vector3 point, Vector3[] from, Vector3[] to, Part[] parts)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < parts.Length; i++)
            {
                float d = DistanceToSegment(point, from[i], to[i]);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return parts[best];
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        // 꺾임 세기 0~1: rise 초에 1, 그 뒤 fade 초 동안 부드럽게 0
        public static float Flinch(float t, float rise, float fade)
        {
            if (t <= 0f) return 0f;
            if (t < rise) return Mathf.SmoothStep(0f, 1f, t / Mathf.Max(1e-4f, rise));
            float k = (t - rise) / Mathf.Max(1e-4f, fade);
            return k >= 1f ? 0f : Mathf.SmoothStep(1f, 0f, k);
        }

        // 앞(0) · 뒤(1) · 옆(2): facing 은 몸이 보는 쪽, towardShooter 는 몸에서 쏜 쪽
        public static int Side(Vector3 facing, Vector3 towardShooter)
        {
            facing.y = 0f; towardShooter.y = 0f;
            if (facing.sqrMagnitude < 1e-6f || towardShooter.sqrMagnitude < 1e-6f) return 2;
            float dot = Vector3.Dot(facing.normalized, towardShooter.normalized);
            return dot > 0.35f ? 0 : dot < -0.35f ? 1 : 2;
        }

        // 사망 동작 순서를 맞은 방향에 맞게 앞세운다. backFall = 뒤로 넘어지는 동작 번호.
        // 앞에서 맞음 → 뒤로 넘어지는 동작을 맨 앞, 뒤에서 맞음 → 맨 뒤. 옆이면 그대로. roll >= bias 면 그대로(변화).
        public static void PreferBySide(int[] order, int backFall, int side, float roll, float bias)
        {
            if (side == 2 || roll >= bias) return;
            int at = System.Array.IndexOf(order, backFall);
            if (at < 0) return;
            if (side == 0)
            {
                for (int i = at; i > 0; i--) order[i] = order[i - 1];
                order[0] = backFall;
            }
            else
            {
                for (int i = at; i < order.Length - 1; i++) order[i] = order[i + 1];
                order[order.Length - 1] = backFall;
            }
        }
    }
}
