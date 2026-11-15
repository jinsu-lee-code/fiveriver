using UnityEngine;

namespace FiveRivers.Combat
{
    // The fall clip ends spread-eagled: both arms flung up beside the head at the same angle, the face straight up.
    // Held for five seconds in Choi Man-su's scene it read as a mannequin laid out (2026-11-15). Once he is on his
    // back the arms lose their strength and come down: the left along his side, the right across his belly, and the
    // head rolls to one side. Directions are given in his own lying frame (toward his right hand, toward his head,
    // up off his chest), so it holds wherever he lies and however the ground slopes.
    [DefaultExecutionOrder(-40)] // after PoseBlendIn, before LyingBodyRest settles the body on the ground
    public sealed class FallenPose : MonoBehaviour
    {
        public static float From = 0.75f, Until = 1.75f; // seconds after the hit: the arms drop over this stretch
        public static Vector3 LeftUpper = new Vector3(-0.35f, -0.93f, -0.10f), LeftFore = new Vector3(-0.12f, -0.99f, 0.02f);
        public static Vector3 RightUpper = new Vector3(0.62f, -0.78f, -0.08f), RightFore = new Vector3(-0.78f, -0.30f, 0.52f);
        public static float HeadRoll = 24f, HeadTurn = 18f;

        Animator animator;
        Transform hips, head, neck, leftShoulder, rightShoulder;
        Transform leftUpper, leftFore, leftHand, rightUpper, rightFore, rightHand;
        float time;

        public static FallenPose Attach(Animator animator)
        {
            if (animator == null || !animator.isHuman) return null;
            var pose = animator.gameObject.AddComponent<FallenPose>();
            pose.animator = animator;
            pose.hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            pose.head = animator.GetBoneTransform(HumanBodyBones.Head);
            pose.neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            pose.leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            pose.leftFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            pose.leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            pose.rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            pose.rightFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            pose.rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            return pose;
        }

        public void Release() => Destroy(this);

        void LateUpdate()
        {
            time += Time.deltaTime;
            float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(From, Until, time));
            if (w <= 0f || hips == null || head == null || leftUpper == null || rightUpper == null) return;
            // his lying frame, from the body itself
            var headward = (head.position - hips.position).normalized;
            var right = Vector3.ProjectOnPlane(rightUpper.position - leftUpper.position, headward).normalized;
            var chest = Vector3.Cross(headward, right);
            if (chest.y < 0f) chest = -chest; // on his back or on his face, 'up off the body' is toward the sky
            Aim(leftUpper, leftFore, LeftUpper, right, headward, chest, w);
            Aim(leftFore, leftHand, LeftFore, right, headward, chest, w);
            Aim(rightUpper, rightFore, RightUpper, right, headward, chest, w);
            Aim(rightFore, rightHand, RightFore, right, headward, chest, w);
            // the head falls to his left and the chin comes down a little
            var turn = Quaternion.AngleAxis(-HeadRoll * w, headward) * Quaternion.AngleAxis(HeadTurn * w, right);
            if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.4f) * neck.rotation;
            head.rotation = Quaternion.Slerp(Quaternion.identity, turn, 0.6f) * head.rotation;
        }

        // turns 'bone' so that the segment to 'end' points along the given direction of the lying frame
        static void Aim(Transform bone, Transform end, Vector3 local, Vector3 right, Vector3 headward, Vector3 chest, float weight)
        {
            if (bone == null || end == null) return;
            var now = end.position - bone.position;
            if (now.sqrMagnitude < 1e-6f) return;
            var want = (right * local.x + headward * local.y + chest * local.z).normalized;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now.normalized, want), weight) * bone.rotation;
        }
    }
}
