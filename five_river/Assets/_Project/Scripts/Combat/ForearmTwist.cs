using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Forearm twist shared along the arm (2026-11-18, after the "elbow twisting" rigging video): the enemy's forearm
    // is skinned to seven bands - the forearm bone and six twist bones (children of it, named "...ForeArmTwist1..6",
    // made by build_hybrid.py own-joints / apply_hybrid.cs). Each frame, after the hands have been placed on the
    // rifle, the hand's roll about the forearm is measured and each twist bone takes its share of it, more toward the
    // wrist (the video's Copy Rotation on the local Y axis at about 1/7 each). With the whole roll on the hand bone,
    // as before, the wrist wrung like a sweet wrapper when the trigger hand turned onto the stock.
    [DefaultExecutionOrder(500)]   // after EnemyAgent / EnemyHandIK have turned the hands
    public sealed class ForearmTwist : MonoBehaviour
    {
        public static float Limit = 120f;   // the video's twist limit (degrees)

        sealed class Arm
        {
            public Transform fore, hand;
            public Transform[] twist;
            public float[] share;
            public Quaternion handRest;   // the hand's rest turn in the forearm's frame
            public Vector3 axis;          // along the forearm, in its own frame
        }

        readonly List<Arm> arms = new List<Arm>();

        public float LastTwist { get; private set; }   // check value: the right hand's measured roll (degrees)

        void Awake()
        {
            // Awake runs before the first animation pass, so the transforms are still in the rest pose
            var animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;
            Add(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), animator.GetBoneTransform(HumanBodyBones.LeftHand));
            Add(animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.RightHand));
        }

        void Add(Transform fore, Transform hand)
        {
            if (fore == null || hand == null) return;
            var twist = new List<Transform>();
            foreach (Transform child in fore)
                if (child.name.Contains("ForeArmTwist")) twist.Add(child);
            if (twist.Count == 0) return;
            twist.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var arm = new Arm
            {
                fore = fore, hand = hand, twist = twist.ToArray(), share = new float[twist.Count],
                handRest = Quaternion.Inverse(fore.rotation) * hand.rotation,
                axis = fore.InverseTransformDirection(hand.position - fore.position).normalized,
            };
            // band k of n+1 (the forearm bone itself is band 0) turns k/(n+1) of the roll
            for (int k = 0; k < twist.Count; k++) arm.share[k] = (k + 1f) / (twist.Count + 1f);
            arms.Add(arm);
        }

        void LateUpdate()
        {
            foreach (var arm in arms)
            {
                // the hand's change from rest, in the forearm's frame, and its twist part about the forearm
                var change = Quaternion.Inverse(arm.fore.rotation) * arm.hand.rotation * Quaternion.Inverse(arm.handRest);
                float angle = Mathf.Clamp(TwistAngle(change, arm.axis), -Limit, Limit);
                for (int k = 0; k < arm.twist.Length; k++)
                    arm.twist[k].localRotation = Quaternion.AngleAxis(angle * arm.share[k], arm.axis);
                LastTwist = angle;
            }
        }

        // swing-twist split: the signed angle of q's turn about axis
        static float TwistAngle(Quaternion q, Vector3 axis)
        {
            var along = Vector3.Project(new Vector3(q.x, q.y, q.z), axis);
            var twist = new Quaternion(along.x, along.y, along.z, q.w);
            float m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            if (m < 1e-6f) return 0f;
            twist.x /= m; twist.y /= m; twist.z /= m; twist.w /= m;
            twist.ToAngleAxis(out float angle, out var a);
            if (angle > 180f) angle -= 360f;
            return Vector3.Dot(a, axis) < 0f ? -angle : angle;
        }
    }
}
