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
        // (2026-11-19) Shouldered in cover, the support hand palm up under the magazine turned 146-161 degrees from
        // rest: with the video's 120-degree limit and the last band at 6/7 of it, some 50 degrees were left to the
        // wrist alone and the cuff pinched into a star. A forearm turns palm-down to palm-up through about 170
        // degrees, so the limit stays short of the half turn (where the twist's sign would flip) and the bands now
        // run evenly from the elbow to the wrist, the last turning with the hand.
        public static float Limit = 175f;

        sealed class Arm
        {
            public Transform fore, hand;
            public Transform[] twist;
            public float[] share;
            public Quaternion handRest;   // the hand's rest turn in the forearm's frame
            public Vector3 axis;          // along the forearm, in its own frame
            public float last;            // last frame's roll (for the half-turn hysteresis)
        }

        readonly List<Arm> arms = new List<Arm>();

        // check values: each hand's measured roll about its forearm before the limit (degrees; left, right)
        public readonly float[] LastTwist = new float[2];

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
            // band k of n (the forearm bone itself is band 0, at the elbow) turns k/n of the roll: the last with the hand
            for (int k = 0; k < twist.Count; k++) arm.share[k] = (k + 1f) / twist.Count;
            arms.Add(arm);
        }

        void LateUpdate()
        {
            for (int a = 0; a < arms.Count; a++)
            {
                var arm = arms[a];
                // the hand's change from rest, in the forearm's frame, and its twist part about the forearm
                var change = Quaternion.Inverse(arm.fore.rotation) * arm.hand.rotation * Quaternion.Inverse(arm.handRest);
                // (2026-11-20) Read alone, a roll hovering about the half turn reads +179 one frame and -179 the next,
                // and the bands between elbow and wrist swing from one side to the other. It stays on last frame's side
                // for up to 30 degrees past the half turn; further than that the shorter way round is taken again (a
                // turn carried on from last frame without that bound wound up to 246 degrees, past the limit).
                float raw = TwistAngle(change, arm.axis);
                if (arm.last > 150f && raw < -150f) raw += 360f; else if (arm.last < -150f && raw > 150f) raw -= 360f;
                if (raw > 210f) raw -= 360f; else if (raw < -210f) raw += 360f;
                arm.last = raw;
                float angle = Mathf.Clamp(raw, -Limit, Limit);
                for (int k = 0; k < arm.twist.Length; k++)
                    arm.twist[k].localRotation = Quaternion.AngleAxis(angle * arm.share[k], arm.axis);
                if (a < LastTwist.Length) LastTwist[a] = raw;
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
