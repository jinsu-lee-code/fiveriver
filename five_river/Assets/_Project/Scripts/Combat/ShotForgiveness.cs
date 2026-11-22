using UnityEngine;

namespace FiveRivers.Combat
{
    // A generous hit judgement for the player's rifle (2026-11-22, asked for along with the bare front-post sight).
    // A shot that struck no one still hits a man if some part of his body that can be seen from the eye lies within
    // a small angle of where the sights were laid. The parts are his own bones, not his capsule: a man hidden behind
    // cover has none in view and is never hit through it, though his capsule may stand up over the top.
    public static class ShotForgiveness
    {
        static readonly RaycastHit[] between = new RaycastHit[32];
        static readonly HumanBodyBones[] Parts =
        {
            HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.Chest, HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
        };
        const float PartRadius = 0.14f;   // a body's thickness round a bone
        const int IgnoreRaycastLayer = 1 << 2;

        // aim: from the eye through the front post. degrees: how far off it may be; that is never less than least
        // nor more than most, in metres at the man (close up an angle is nothing, far off it is several men wide).
        public static bool TryFind(Ray aim, float range, float degrees, float least, float most, int mask, Transform shooter, out RaycastHit best)
        {
            best = default;
            if (degrees <= 0f || most <= 0f) return false;
            float tan = Mathf.Tan(degrees * Mathf.Deg2Rad), bestOff = float.MaxValue;
            mask &= ~IgnoreRaycastLayer;   // (the rifle in his hands)
            // The men are taken from the scene, not swept for: a 250 m capsule along the ground cost 1.5-3.6 ms a
            // shot against the terrain's heightfield.
            var men = Object.FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None);
            var boards = Object.FindObjectsByType<TutorialTarget>(FindObjectsSortMode.None);
            for (int i = 0; i < men.Length + boards.Length; i++)
            {
                bool man = i < men.Length;
                var target = man ? (Component)men[i] : boards[i - men.Length];
                var collider = man ? target.GetComponent<Collider>() : target.GetComponentInChildren<Collider>();
                if (collider == null || !collider.enabled || (man && !men[i].Alive)) continue;
                // no part of him can come near enough: not looked at further
                var middle = collider.bounds.center;
                float ahead = Vector3.Dot(middle - aim.origin, aim.direction);
                float within = most + collider.bounds.extents.magnitude + PartRadius;
                if (ahead < -within || ahead > range + within || (aim.GetPoint(ahead) - middle).sqrMagnitude > within * within) continue;
                var animator = target.GetComponentInChildren<Animator>();
                bool human = animator != null && animator.isHuman;
                int parts = human ? Parts.Length : 1;
                for (int p = 0; p < parts; p++)
                {
                    Vector3 point;
                    float radius;
                    if (human)
                    {
                        var bone = animator.GetBoneTransform(Parts[p]);
                        if (bone == null) continue;
                        point = bone.position;
                        radius = PartRadius;
                    }
                    else
                    {
                        // the practice target
                        point = collider.bounds.center;
                        radius = Mathf.Min(collider.bounds.extents.y, Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z));
                    }
                    float along = Vector3.Dot(point - aim.origin, aim.direction);
                    if (along <= 0f || along > range) continue;
                    float beside = Mathf.Max(0f, Vector3.Distance(point, aim.GetPoint(along)) - radius);
                    if (beside > Mathf.Clamp(along * tan, least, most)) continue;
                    float off = beside / along;   // of several men, the one nearest the post
                    if (off >= bestOff || !Seen(aim.origin, point, target.transform, shooter, mask)) continue;
                    if (!OnBody(aim.origin, point, collider, out var hit)) continue;
                    bestOff = off;
                    best = hit;
                }
            }
            return bestOff < float.MaxValue;
        }

        // Nothing but he himself between the eye and that part. His capsule does not count as seen: pressed to his
        // cover it pokes out through the front of it, where the first thing struck would be him.
        static bool Seen(Vector3 eye, Vector3 point, Transform target, Transform shooter, int mask)
        {
            var to = point - eye;
            float length = to.magnitude;
            int count = Physics.RaycastNonAlloc(eye, to / length, between, length, mask, QueryTriggerInteraction.Ignore);
            if (count == between.Length) return false;   // (that much in the way is cover)
            for (int i = 0; i < count; i++)
            {
                var t = between[i].collider.transform;
                if (!t.IsChildOf(target) && !t.IsChildOf(shooter)) return false;
            }
            return true;
        }

        // where that shot strikes his collider (a part outside it: toward its middle at that height)
        static bool OnBody(Vector3 eye, Vector3 point, Collider body, out RaycastHit hit)
        {
            float reach = Vector3.Distance(eye, point) + 2f;
            if (body.Raycast(new Ray(eye, point - eye), out hit, reach)) return true;
            var bounds = body.bounds;
            var inside = new Vector3(bounds.center.x, Mathf.Clamp(point.y, bounds.min.y + 0.1f, bounds.max.y - 0.1f), bounds.center.z);
            return body.Raycast(new Ray(eye, inside - eye), out hit, reach);
        }
    }
}
