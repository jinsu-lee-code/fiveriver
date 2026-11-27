using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Where men who have stopped together stand (2026-11-27, 검수 캡처 itch_20261126/raw/0430_Dawn_079.8 · 1050_Dawn_107.3 ·
    // dawn/after_1cut_07: on the crest at dawn the squad stood in one heap, all in the same pose). Pure arithmetic, no
    // engine objects (tests: StandSpacingTests). The caller says which spots are standable (ground, trench, cover).
    //  - Spread: in order, each man keeps his own spot if it is at least minGap from those already placed (and from the
    //    others, e.g. the player); otherwise he takes the nearest standable spot on rings round it that is.
    //  - Unit: a fixed -1..1 per man and purpose (his own facing offset, his own pace), so a run looks the same each time.
    // Presentation only: no judgement, flow or balance value depends on it.
    public static class StandSpacing
    {
        public static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public static bool FarEnough(Vector3 p, IReadOnlyList<Vector3> placed, int count, float gap)
        {
            for (int i = 0; i < count; i++) if (Flat(p, placed[i]) < gap) return false;
            return true;
        }

        // allowed(spot, man): may this man stand there. His own spot is never asked: he is standing on it.
        public static Vector3[] Spread(IReadOnlyList<Vector3> from, float minGap, System.Func<Vector3, int, bool> allowed,
            IReadOnlyList<Vector3> others = null, float otherGap = 0f, float step = 0.3f, float reach = 2.4f, int around = 12)
        {
            var placed = new Vector3[from.Count];
            int otherCount = others != null ? others.Count : 0;
            for (int i = 0; i < from.Count; i++)
            {
                var own = from[i];
                placed[i] = own;
                if (FarEnough(own, placed, i, minGap) && (otherCount == 0 || FarEnough(own, others, otherCount, otherGap))) continue;
                bool found = false;
                int ring = 0;
                for (float r = step; r <= reach + 1e-4f && !found; r += step, ring++)
                {
                    // every other ring is turned half a step, so the spots do not line up on spokes
                    float offset = (ring & 1) == 1 ? 180f / around : 0f;
                    for (int k = 0; k < around && !found; k++)
                    {
                        float a = (offset + k * 360f / around) * Mathf.Deg2Rad;
                        var spot = new Vector3(own.x + Mathf.Cos(a) * r, own.y, own.z + Mathf.Sin(a) * r);
                        if (!FarEnough(spot, placed, i, minGap)) continue;
                        if (otherCount > 0 && !FarEnough(spot, others, otherCount, otherGap)) continue;
                        if (allowed != null && !allowed(spot, i)) continue;
                        placed[i] = spot;
                        found = true;
                    }
                }
                // nowhere clear within reach: he stays where he is (better close than inside a rock)
            }
            return placed;
        }

        // A fixed value in -1..1 for this name and purpose.
        public static float Unit(string name, int salt)
        {
            unchecked
            {
                int hash = 17 + salt * 7919;
                if (name != null) foreach (char c in name) hash = hash * 31 + c;
                hash ^= hash >> 15; hash *= (int)0x2c1b3c6d; hash ^= hash >> 12; hash *= 0x297a2d39; hash ^= hash >> 15;
                return (hash & 0xffff) / 65535f * 2f - 1f;
            }
        }

        // Which of `kinds` poses each of `count` men takes: in turn from a starting kind, so that two men already show
        // two different poses and three show three. Men marked fixed (the squad leader, who talks) take kind 0.
        public static int[] Poses(int count, int kinds, int start, System.Func<int, bool> isFixed = null)
        {
            var poses = new int[count];
            if (kinds <= 0) return poses;
            int next = ((start % kinds) + kinds) % kinds;
            for (int i = 0; i < count; i++)
            {
                if (isFixed != null && isFixed(i)) { poses[i] = 0; continue; }
                poses[i] = next;
                next = (next + 1) % kinds;
            }
            return poses;
        }
    }
}
