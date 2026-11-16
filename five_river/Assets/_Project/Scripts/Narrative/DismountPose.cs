using UnityEngine;

namespace FiveRivers.Narrative
{
    // The jump down off the dawn truck's tailgate (2026-11-16): there is no dismount clip, so the crouch, the tuck in the
    // air and the squat on landing are laid over the animated pose here. The hips drop by Crouch, each leg is bent with
    // a two-bone solve so its foot stays where the animation put it (knees forward), Tuck draws the feet up toward the
    // body in the air, and Lean tips the spine forward over the knees. Presentation only; set by DawnTruckSequence.
    [DefaultExecutionOrder(130)]
    public sealed class DismountPose : MonoBehaviour
    {
        public float Crouch { get; set; } // 0..1, 1 = a deep squat
        public float Tuck { get; set; }   // 0..1, feet drawn up (in the air)
        public float Lean { get; set; }   // 0..1
        const float MaxDrop = .46f, MaxTuck = .32f, MaxLean = 28f;

        Animator animator;
        Transform hips, spine, lUpper, lLower, lFoot, rUpper, rLower, rFoot;

        public void Configure(Animator actorAnimator)
        {
            animator = actorAnimator;
            if (animator == null || !animator.isHuman) { enabled = false; return; }
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            enabled = false;
        }

        void LateUpdate() => Apply();

        // public so an editor check can pose a sampled frame
        public void Apply()
        {
            if (hips == null || (Crouch <= 0f && Tuck <= 0f && Lean <= 0f)) return;
            // the body's own facing from the hip joints (the model may be turned inside its prefab)
            var forward = Vector3.Cross(rUpper.position - lUpper.position, Vector3.up); forward.y = 0f; forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            // where the animation planted the feet, and how they are turned
            Vector3 lTarget = lFoot.position, rTarget = rFoot.position;
            Quaternion lRot = lFoot.rotation, rRot = rFoot.rotation;
            // in the air the feet come up under the body, the left a little ahead
            float tuck = Tuck * MaxTuck;
            lTarget += Vector3.up * tuck + forward * (Tuck * .10f);
            rTarget += Vector3.up * tuck * .85f - forward * (Tuck * .04f);
            hips.position -= Vector3.up * (Crouch * MaxDrop);
            // the hips go back as they go down, the chest comes over the knees
            hips.position -= forward * (Crouch * .10f);
            if (spine != null && Lean > 0f) spine.rotation = Quaternion.AngleAxis(Lean * MaxLean, right) * spine.rotation;
            Solve(lUpper, lLower, lFoot, lTarget, forward);
            Solve(rUpper, rLower, rFoot, rTarget, forward);
            lFoot.rotation = lRot; rFoot.rotation = rRot;
        }

        // two-bone solve with the knee bending toward 'pole'
        static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            if (a == null || b == null || c == null) return;
            float ab = Vector3.Distance(a.position, b.position), bc = Vector3.Distance(b.position, c.position);
            Vector3 toTarget = target - a.position;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(ab - bc) + 1e-3f, ab + bc - 1e-3f);
            Vector3 dir = toTarget.normalized;
            // the knee: in the plane of the leg and the pole, at the law-of-cosines angle from the hip
            Vector3 bendAxis = Vector3.Cross(dir, pole).normalized;
            if (bendAxis.sqrMagnitude < 1e-6f) bendAxis = Vector3.Cross(dir, Vector3.forward).normalized;
            float cosA = Mathf.Clamp((ab * ab + d * d - bc * bc) / (2f * ab * d), -1f, 1f);
            float angleA = Mathf.Acos(cosA) * Mathf.Rad2Deg;
            Vector3 kneeDir = Quaternion.AngleAxis(angleA, bendAxis) * dir; // bendAxis = dir x pole, so + turns toward the pole
            Vector3 knee = a.position + kneeDir * ab;
            // rotate the thigh so the knee goes there, then the shin so the foot reaches the target
            a.rotation = Quaternion.FromToRotation(b.position - a.position, knee - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
        }
    }
}
