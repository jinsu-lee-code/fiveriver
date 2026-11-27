using UnityEngine;

namespace FiveRivers.Narrative
{
    // Does a box stand between a camera and what it is showing (2026-11-27)? Pure arithmetic for CutsceneSightline,
    // tested by SightlineMathTests. A box blocks when the straight line from the eye to a point of the subject passes
    // through it before reaching the subject (its near side short of the subject by `clearOfSubject`), and in front
    // of the eye (beyond `near`).
    public static class SightlineMath
    {
        // Slab test: where the segment eye -> target first enters the box, as a distance from the eye; false if it never does.
        public static bool Enters(Vector3 eye, Vector3 target, Bounds box, out float enter)
        {
            enter = 0f;
            var d = target - eye;
            float length = d.magnitude;
            if (length < 1e-5f) return false;
            d /= length;
            float tMin = 0f, tMax = length;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = eye[axis], dir = d[axis], lo = box.min[axis], hi = box.max[axis];
                if (Mathf.Abs(dir) < 1e-7f)
                {
                    if (o < lo || o > hi) return false;
                    continue;
                }
                float t1 = (lo - o) / dir, t2 = (hi - o) / dir;
                if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return false;
            }
            enter = tMin;
            return true;
        }

        public static bool Blocks(Vector3 eye, Vector3 target, Bounds box, float clearOfSubject, float near)
        {
            if (!Enters(eye, target, box, out float enter)) return false;
            // the eye is inside the box (a camera among the branches): it is in the way too, unless the subject is too
            float toTarget = Vector3.Distance(eye, target);
            if (enter < near && box.Contains(eye)) return !box.Contains(target);
            return enter >= near && enter < toTarget - clearOfSubject;
        }

        // The points of a subject the lines are drawn to: its middle, its top and its two sides as the camera sees them.
        public static int SubjectPoints(Vector3 eye, Vector3 centre, float radius, float height, Vector3[] into)
        {
            if (into == null || into.Length < 4) return 0;
            var flat = centre - eye; flat.y = 0f;
            var side = flat.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.right;
            into[0] = centre;
            into[1] = centre + Vector3.up * (height * 0.5f);
            into[2] = centre + side * radius;
            into[3] = centre - side * radius;
            return 4;
        }
    }
}
