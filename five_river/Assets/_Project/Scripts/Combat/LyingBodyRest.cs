using UnityEngine;

namespace FiveRivers.Combat
{
    // A body that has fallen lies on the ground, not on the air above it (2026-11-13). The death clips end lying
    // flat on the plane of the character's origin, but the origin is where the man stood: on the NavMesh (up to
    // 20 cm above a slope) or on a slope that falls away under his legs. Seen in play, Choi Man-su lay stiff with
    // his boots 22 cm off the trench floor and the riflemen's bodies hovered over the hillside.
    // Each frame, after the clip has posed him, the level floor the clip lies on is turned onto the slope under
    // him (head to feet and side to side), then the body is lowered until its lowest part touches. It only acts
    // once the hips are low (he is down), so the fall itself plays as it was made. Added to the Animator object
    // on death.
    public sealed class LyingBodyRest : MonoBehaviour
    {
        const float HeadClear = 0.09f, ChestClear = 0.11f, HipsClear = 0.1f, FootClear = 0.07f, HandClear = 0.03f;
        // MaxRaise: turned onto a slope the downhill side can end up under the ground; it is lifted back out (2026-11-13)
        const float MaxTilt = 32f, MaxLower = 0.6f, MaxRaise = 0.4f;
        Animator animator;
        Transform head, chest, hips, leftFoot, rightFoot, leftHand, rightHand, leftUpperArm, rightUpperArm, leftKnee, rightKnee, leftHip, rightHip;
        // Killed while kneeling (2026-11-16): the death clips all start standing, so he used to spring up to his
        // full height for 0.1-0.4 s before he fell. The hips are held at the height he knelt at and the feet where
        // they were, until the clip comes down past that height by itself; then the legs are handed back to it.
        bool kneelHold;
        float kneelHips, kneelGap, kneelStart, legPin;
        Vector3 leftFootStart, rightFootStart, leftPole, rightPole;
        Quaternion leftFootTurn, rightFootTurn;
        Vector3 clear, cleared; // how far he has to be, and has been, slid out of a rock or sandbag wall he fell into
        bool clearFound;
        Vector3 restPosition;
        Quaternion restRotation;
        Terrain ground;
        float weight;

        public static LyingBodyRest Attach(Animator animator)
        {
            if (animator == null || !animator.isHuman) return null;
            var rest = animator.GetComponent<LyingBodyRest>();
            if (rest == null) rest = animator.gameObject.AddComponent<LyingBodyRest>();
            rest.Bind(animator);
            return rest;
        }

        // Puts the model back where it was and removes the component (a new run brings the man back to his post).
        public void Release()
        {
            transform.SetLocalPositionAndRotation(restPosition, restRotation);
            Destroy(this);
        }

        void Bind(Animator owner)
        {
            animator = owner;
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftHip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightHip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            clear = cleared = Vector3.zero;
            clearFound = false;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            ground = Terrain.activeTerrain;
            weight = 0f;
        }

        // Called by EnemyAgent right after Attach when he was kneeling as he was hit (the pose is still the kneel).
        public void HoldKneel()
        {
            if (ground == null || hips == null || leftHip == null || rightHip == null || leftKnee == null || rightKnee == null) return;
            kneelHold = true;
            legPin = 1f;
            kneelStart = kneelHips = Vector3.Dot(hips.position - transform.position, transform.up);
            kneelGap = Gap(hips.position);
            leftFootStart = leftFoot.position; rightFootStart = rightFoot.position;
            leftFootTurn = leftFoot.rotation; rightFootTurn = rightFoot.rotation;
            leftPole = KneeBend(leftHip, leftKnee, leftFoot);
            rightPole = KneeBend(rightHip, rightKnee, rightFoot);
        }

        // which way the knee points off the hip-to-foot line
        static Vector3 KneeBend(Transform hip, Transform knee, Transform foot)
        {
            var line = foot.position - hip.position;
            var off = Vector3.ProjectOnPlane(knee.position - hip.position, line.normalized);
            return off.sqrMagnitude > 1e-6f ? off.normalized : Vector3.forward;
        }

        void HoldKneelPose()
        {
            float clipHips = Vector3.Dot(hips.position - transform.position, transform.up);
            // no higher than he knelt: the clip's standing start is taken out of the hips, the upper body keeps its
            // motion. Held both over his own origin and over the ground under the hips, and never back up once he has
            // sunk lower: the clips carry the hips back over a hillside, Death_01 over ground that falls away (held over
            // the origin only, 5 cm up off the ground by 0.6 s), Death_00 over ground that rises (held over the ground
            // only, 5 cm up in the air by 0.9 s) (11-16)
            float gap = Gap(hips.position);
            float over = Mathf.Max(0f, Mathf.Max(clipHips - kneelHips, gap - kneelGap));
            if (over > 0f) transform.position -= Vector3.up * over;
            kneelHips = Mathf.Min(kneelHips, clipHips - over);
            kneelGap = Mathf.Min(kneelGap, gap - over);
            // the legs stay as he knelt while the clip is still above that height, and are let go over the 20 cm it
            // takes the clip to come down past it (never faster than 3 per second, so they do not snap)
            float below = kneelStart - clipHips;
            float pin = Mathf.Clamp01(1f - below / 0.2f);
            legPin = Mathf.Min(legPin, Mathf.MoveTowards(legPin, pin, Time.deltaTime * 3f));
            if (legPin > 0f)
            {
                PinLeg(leftHip, leftKnee, leftFoot, leftFootStart, leftFootTurn, leftPole, legPin);
                PinLeg(rightHip, rightKnee, rightFoot, rightFootStart, rightFootTurn, rightPole, legPin);
            }
            // well down by the clip's own fall and no longer held down: letting go changes nothing on screen
            else if (below > 0.25f && over <= 0.005f) kneelHold = false;
        }

        static void PinLeg(Transform hip, Transform knee, Transform foot, Vector3 start, Quaternion turn, Vector3 pole, float w)
        {
            var goal = Vector3.Lerp(foot.position, start, w);
            var footTurn = Quaternion.Slerp(foot.rotation, turn, w);
            float a = Vector3.Distance(hip.position, knee.position), b = Vector3.Distance(knee.position, foot.position);
            var toGoal = goal - hip.position;
            float d = Mathf.Clamp(toGoal.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
            var dir = toGoal.normalized;
            var bendAxis = Vector3.Cross(dir, pole);
            if (bendAxis.sqrMagnitude < 1e-6f) return;
            bendAxis.Normalize();
            float angle = Mathf.Acos(Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f)) * Mathf.Rad2Deg;
            var kneeAt = hip.position + Quaternion.AngleAxis(angle, bendAxis) * dir * a;
            hip.rotation = Quaternion.FromToRotation(knee.position - hip.position, kneeAt - hip.position) * hip.rotation;
            knee.rotation = Quaternion.FromToRotation(foot.position - knee.position, goal - knee.position) * knee.rotation;
            foot.rotation = footTurn;
        }

        void LateUpdate()
        {
            if (animator == null || ground == null || head == null || hips == null || leftFoot == null || rightFoot == null) return;
            // start from the model as the clip left it, so nothing builds up from frame to frame
            transform.SetLocalPositionAndRotation(restPosition, restRotation);
            // he is down when his hips are low over his own origin (the clip's floor), whatever the slope does;
            // ease in over that last part of the fall
            float hipsUp = Vector3.Dot(hips.position - transform.position, transform.up);
            float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.25f, hipsUp));
            weight = Mathf.MoveTowards(weight, Mathf.Max(weight, down), Time.deltaTime * 3f);
            if (kneelHold) HoldKneelPose();
            if (weight <= 0f) return;
            // slid out of the cover he fell into (found below, once he is still) before he is laid on the ground there
            cleared = Vector3.MoveTowards(cleared, clear, 1.5f * Time.deltaTime);
            if (cleared.sqrMagnitude > 0f) transform.position += cleared;

            // the clip lies on a level floor: turn that floor onto the slope under the body (head to feet and
            // side to side), about the hips
            var middle = (head.position + hips.position + leftFoot.position + rightFoot.position) * 0.25f;
            var slope = GroundNormal(middle);
            var level = Quaternion.FromToRotation(Vector3.up, slope);
            level.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            angle = Mathf.Clamp(angle, -MaxTilt, MaxTilt);
            if (Mathf.Abs(angle) > 0.1f) transform.RotateAround(hips.position, axis, angle * weight);

            // then down (or up, if the turn has sunk part of him) until the lowest part rests on the ground
            float lower = Mathf.Min(Gap(head.position) - HeadClear, Gap(hips.position) - HipsClear);
            lower = Mathf.Min(lower, Mathf.Min(Gap(leftFoot.position), Gap(rightFoot.position)) - FootClear);
            if (chest != null) lower = Mathf.Min(lower, Gap(chest.position) - ChestClear);
            if (leftHand != null) lower = Mathf.Min(lower, Gap(leftHand.position) - HandClear);
            if (rightHand != null) lower = Mathf.Min(lower, Gap(rightHand.position) - HandClear);
            lower = Mathf.Clamp(lower, -MaxRaise, MaxLower);
            transform.position -= Vector3.up * (lower * weight);

            // An arm the clip leaves raised (Death_0x ends with a hand up off the ground) would stay up in the air
            // once he is still: swing it down from the shoulder onto the ground beside him, same reach, elbow as
            // the clip has it (2026-11-16). The rest of the body keeps the clip's shape.
            // the same for a lower leg the clip leaves up (falling forward, a bent knee kept the boot in the air)
            // whole leg about the hip first (face down, a leg can rise from the hip like a scorpion's tail), then the shin
            RestLimb(leftHip, leftFoot, FootClear, hips);
            RestLimb(rightHip, rightFoot, FootClear, hips);
            RestLimb(leftKnee, leftFoot, FootClear, hips);
            RestLimb(rightKnee, rightFoot, FootClear, hips);
            RestLimb(leftUpperArm, leftHand, HandClear, hips);
            RestLimb(rightUpperArm, rightHand, HandClear, hips);

            // Fallen against cover (they die beside the rock or the sandbags they fought from) part of him went
            // into it. Once he lies still, find how far out of it he has to be and slide him clear (2026-11-16).
            if (weight >= 1f && !clearFound) { clearFound = true; clear = ClearOfCover(); }
        }

        static readonly Collider[] overlaps = new Collider[8];

        Vector3 ClearOfCover()
        {
            var parts = new[] { head, chest, hips, leftFoot, rightFoot, leftHand, rightHand };
            // which way: away from the middle of whatever he lies in, along the ground
            var away = Vector3.zero;
            foreach (var part in parts)
            {
                if (part == null) continue;
                if (!CoverAt(part.position, out var centre)) continue;
                var d = part.position - centre; d.y = 0f;
                away += d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
            }
            if (away.sqrMagnitude < 0.0001f) return Vector3.zero;
            away.Normalize();
            // then 10 cm at a time until nothing of him is inside it (at most 1.2 m)
            for (float step = 0.1f; step <= 1.2f; step += 0.1f)
            {
                bool inside = false;
                foreach (var part in parts)
                    if (part != null && CoverAt(part.position + away * step, out _)) { inside = true; break; }
                if (!inside) return away * (step + 0.05f);
            }
            return away * 1.2f;
        }

        static readonly RaycastHit[] above = new RaycastHit[8];

        bool CoverAt(Vector3 p, out Vector3 centre)
        {
            var c = CoverCollider(p);
            if (c != null) { centre = c.bounds.center; return true; }
            // the art rocks and sandbags with no collider of their own (CoverVisuals)
            return CoverVisuals.Inside(p, out centre);
        }

        Collider CoverCollider(Vector3 p)
        {
            // buried inside a rock mesh there is no surface for an overlap test to cross: look straight down onto
            // the point from above instead; anything but the ground over it means it lies under or inside cover
            int hitCount = Physics.RaycastNonAlloc(p + Vector3.up * 3f, Vector3.down, above, 2.95f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var c = above[i].collider;
                if (IsCover(c)) return c;
            }
            int n = Physics.OverlapSphereNonAlloc(p, 0.1f, overlaps, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (IsCover(overlaps[i])) return overlaps[i];
            return null;
        }

        bool IsCover(Collider c)
        {
            if (c == null || c is TerrainCollider || c.attachedRigidbody != null || c.transform.IsChildOf(transform.root)) return false;
            return c.GetComponentInParent<EnemyAgent>() == null && c.GetComponentInParent<CharacterController>() == null
                && c.GetComponentInParent<AllyCombatant>() == null;
        }

        const float LimbUpFrom = 0.08f, LimbUpFull = 0.25f;

        // Swing a limb down about its upper joint (shoulder, or knee for the lower leg) until its end lies on the
        // ground beside him at the same reach, along the way it already points (or away from the body if it points
        // straight up). Only for an end that is clearly off the ground; the elbow or ankle bend is the clip's.
        void RestLimb(Transform joint, Transform end, float clearance, Transform body)
        {
            if (joint == null || end == null) return;
            float gap = Gap(end.position) - clearance;
            float w = weight * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LimbUpFrom, LimbUpFull, gap));
            if (w <= 0f) return;
            var root = joint.position;
            var reach = end.position - root;
            float length = reach.magnitude;
            var flat = new Vector3(reach.x, 0f, reach.z);
            if (flat.sqrMagnitude < 0.01f * length * length) flat = Vector3.ProjectOnPlane(root - body.position, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) return;
            flat.Normalize();
            var target = end.position;
            for (int i = 0; i < 2; i++)
            {
                float floor = ground.SampleHeight(target) + ground.transform.position.y + clearance;
                float drop = Mathf.Clamp(root.y - floor, 0f, length);
                float along = Mathf.Sqrt(Mathf.Max(0f, length * length - drop * drop));
                target = root + flat * along + Vector3.down * drop;
            }
            var turn = Quaternion.FromToRotation(reach, target - root);
            joint.rotation = Quaternion.Slerp(Quaternion.identity, turn, w) * joint.rotation;
        }

        float Gap(Vector3 p) => p.y - (ground.SampleHeight(p) + ground.transform.position.y);

        Vector3 GroundNormal(Vector3 p)
        {
            var data = ground.terrainData;
            var local = p - ground.transform.position;
            return data.GetInterpolatedNormal(local.x / data.size.x, local.z / data.size.z);
        }
    }
}
