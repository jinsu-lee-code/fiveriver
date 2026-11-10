using UnityEngine;

namespace FiveRivers.Combat
{
    // Puts the KPA rifleman's support hand on the rifle he is carrying. The clips hold the hands for a shorter
    // weapon, so after EnemyAgent lines the Mosin up with the hands the left palm could hover up to 10 cm off it.
    // Lives on the Animator object (OnAnimatorIK is only sent there); EnemyAgent adds it and supplies the rifle.
    public sealed class EnemyHandIK : MonoBehaviour
    {
        public static float Weight = 0.85f, Below = 0.03f;
        Animator animator;
        Transform rifle;
        Vector3 axisLocalStart, axisLocalEnd;
        public bool Holding { get; set; } = true;
        // where the clip put the left hand this frame, before IK (EnemyAgent aims the rifle from this)
        public Vector3 AnimatedLeftHand { get; private set; }
        public bool HasAnimatedHand { get; private set; }

        public void Bind(Animator owner, Transform weapon, Vector3 centreLocal, float behind, float ahead)
        {
            animator = owner;
            rifle = weapon;
            axisLocalStart = centreLocal + Vector3.back * behind;
            axisLocalEnd = centreLocal + Vector3.forward * ahead;
        }

        void OnAnimatorIK(int layer)
        {
            if (animator == null || rifle == null || !Holding)
            {
                if (animator != null) animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
                return;
            }
            var hand = animator.GetIKPosition(AvatarIKGoal.LeftHand);
            AnimatedLeftHand = hand;
            HasAnimatedHand = true;
            var a = rifle.TransformPoint(axisLocalStart);
            var b = rifle.TransformPoint(axisLocalEnd);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(hand - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            var onStock = a + ab * t - rifle.up * Below; // palm under the stock
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, Weight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, onStock);
        }
    }
}
