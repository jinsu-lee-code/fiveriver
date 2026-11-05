using UnityEngine;

namespace FiveRivers.Narrative
{
    // Opt-in visual correction for the four Dawn truck passengers only.
    // Does not translate actor roots, change meshes, clips, controllers, or gameplay.
    [DefaultExecutionOrder(120)]
    public sealed class DawnFootPlacement : MonoBehaviour
    {
        Animator animator;
        Transform truck, lip, foot, hips;
        Terrain terrain;
        Transform leftUpper, leftLower, leftFoot, rightUpper, rightLower, rightFoot;
        float leftLength, rightLength;
        bool hasPose;
        Vector3 cachedHipsPosition;
        readonly Quaternion[] cachedLegRotations = new Quaternion[6];
        Transform[] legBones;
        public float LastPelvisDrop { get; private set; }
        public float LastMaximumFootCorrection { get; private set; }

        public void Configure(Animator actorAnimator, Transform truckRoot, Transform rampLip,
            Transform rampFoot, Terrain ground)
        {
            animator = actorAnimator; truck = truckRoot; lip = rampLip; foot = rampFoot; terrain = ground;
            if (animator == null || !animator.isHuman) { enabled = false; return; }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftLength = Length(leftUpper, leftLower, leftFoot);
            rightLength = Length(rightUpper, rightLower, rightFoot);
            legBones = new[] { leftUpper, leftLower, leftFoot, rightUpper, rightLower, rightFoot };
            hasPose = false;
            enabled = false;
        }

        static float Length(Transform a, Transform b, Transform c)
        {
            return a == null || b == null || c == null ? 0f : Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
        }

        void LateUpdate()
        {
            if (animator == null || hips == null || leftLength <= 0f || rightLength <= 0f
                || !animator.enabled) return;
            // A zero-delta Animator may still write its uncorrected pose. Hold the last
            // rendered leg pose while paused instead of letting the feet snap back.
            if (Time.deltaTime <= 0f)
            {
                if (hasPose)
                {
                    hips.localPosition = cachedHipsPosition;
                    for (int i = 0; i < legBones.Length; i++) legBones[i].localRotation = cachedLegRotations[i];
                }
                return;
            }
            Vector3 left = leftFoot.position, right = rightFoot.position;
            Vector3 leftNormal, rightNormal;
            float leftSurface = Surface(left, out leftNormal), rightSurface = Surface(right, out rightNormal);
            // Keep the authored swing clearance. Only replace the plane beneath each foot.
            Vector3 leftTarget = left + Vector3.up * Mathf.Clamp(leftSurface - transform.position.y, -.27f, .27f);
            Vector3 rightTarget = right + Vector3.up * Mathf.Clamp(rightSurface - transform.position.y, -.27f, .27f);
            if (Mathf.Abs(leftSurface - transform.position.y) < .002f && Mathf.Abs(rightSurface - transform.position.y) < .002f) { LastPelvisDrop = 0f; LastMaximumFootCorrection = 0f; CachePose(); return; }
            float drop = Mathf.Max(RequiredDrop(leftUpper.position, leftTarget, leftLength), RequiredDrop(rightUpper.position, rightTarget, rightLength));
            drop = Mathf.Clamp(drop, 0f, .14f);
            LastPelvisDrop = drop;
            LastMaximumFootCorrection = Mathf.Max(Mathf.Abs(leftTarget.y - left.y), Mathf.Abs(rightTarget.y - right.y));
            // Both legs bend at the knee when the downhill foot would otherwise overextend.
            hips.position -= Vector3.up * drop;
            Quaternion leftRotation = SlopeRotation(leftNormal, left.y - transform.position.y - animator.leftFeetBottomHeight) * leftFoot.rotation;
            Quaternion rightRotation = SlopeRotation(rightNormal, right.y - transform.position.y - animator.rightFeetBottomHeight) * rightFoot.rotation;
            Solve(leftUpper, leftLower, leftFoot, leftTarget, leftRotation);
            Solve(rightUpper, rightLower, rightFoot, rightTarget, rightRotation);
            CachePose();
        }

        // Preserve the clip's lifted-foot pitch instead of adding the ramp pitch to it.
        // A planted foot retains full surface alignment; fade only the added swing rotation.
        static Quaternion SlopeRotation(Vector3 normal, float lift)
        {
            float contact = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lift / .06f));
            return Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), contact);
        }
        void CachePose()
        {
            cachedHipsPosition = hips.localPosition;
            for (int i = 0; i < legBones.Length; i++) cachedLegRotations[i] = legBones[i].localRotation;
            hasPose = true;
        }

        float Surface(Vector3 at, out Vector3 normal)
        {
            normal = Vector3.up;
            if (truck != null && lip != null && foot != null)
            {
                Vector3 local = truck.InverseTransformPoint(at);
                Vector3 a = truck.InverseTransformPoint(lip.position), b = truck.InverseTransformPoint(foot.position);
                if (Mathf.Abs(local.x) <= .6f && local.z <= a.z && local.z >= b.z)
                {
                    float t = Mathf.InverseLerp(a.z, b.z, local.z);
                    normal = Vector3.Cross(truck.right, foot.position - lip.position).normalized;
                    if (normal.y < 0f) normal = -normal;
                    float edge = Mathf.Min(t, 1f - t) * Vector3.Distance(lip.position, foot.position);
                    normal = Vector3.Slerp(Vector3.up, normal, Mathf.Clamp01(edge / .15f));
                    return Mathf.Lerp(lip.position.y, foot.position.y, t) + .018f;
                }
                if (Mathf.Abs(local.x) < 1.05f && local.z > a.z && local.z < .65f)
                    return lip.position.y;
            }
            if (terrain != null && terrain.terrainData != null)
                return terrain.SampleHeight(at) + terrain.transform.position.y + .008f;
            return transform.position.y;
        }

        static float RequiredDrop(Vector3 hip, Vector3 target, float legLength)
        {
            Vector3 delta = hip - target;
            float horizontal = delta.x * delta.x + delta.z * delta.z;
            float maxVertical = Mathf.Sqrt(Mathf.Max(.01f, legLength * legLength * .970225f - horizontal));
            return Mathf.Max(0f, delta.y - maxVertical);
        }

        static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Quaternion rotation)
        {
            Vector3 a = upper.position, b = lower.position, c = end.position;
            float first = Vector3.Distance(a, b), second = Vector3.Distance(b, c);
            Vector3 delta = target - a;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + .001f, first + second - .001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(b - a, direction);
            if (bend.sqrMagnitude < .00001f) bend = Vector3.ProjectOnPlane(upper.forward, direction);
            bend.Normalize();
            float along = (first * first + distance * distance - second * second) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, first * first - along * along));
            Vector3 knee = a + direction * along + bend * height;
            upper.rotation = Quaternion.FromToRotation(b - a, knee - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
            end.rotation = rotation;
        }
    }
}


