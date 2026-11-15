using UnityEngine;

namespace FiveRivers.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Five Rivers/NPC Rifle Grip")]
    public sealed class NpcRifleGrip : MonoBehaviour
    {
        [SerializeField, InspectorName("오른손 위치 보정 (m)")] Vector3 rightPositionOffset;
        [SerializeField, InspectorName("오른손 회전 보정 (°)")] Vector3 rightRotationOffset;
        [SerializeField, InspectorName("왼손 위치 보정 (m)")] Vector3 leftPositionOffset;
        [SerializeField, InspectorName("왼손 회전 보정 (°)")] Vector3 leftRotationOffset;

#if UNITY_EDITOR
        Pose rightReference, leftReference;
        bool hasRightReference, hasLeftReference;

        public bool TryGetReference(bool right, out Pose rifleLocalPose)
        {
            rifleLocalPose = right ? rightReference : leftReference;
            return right ? hasRightReference : hasLeftReference;
        }
#endif

        internal bool HasPositionOffset(bool right) =>
            isActiveAndEnabled && (right ? rightPositionOffset : leftPositionOffset) != Vector3.zero;

        internal bool HasRotationOffset(bool right) =>
            isActiveAndEnabled && (right ? rightRotationOffset : leftRotationOffset) != Vector3.zero;

        internal bool HasOffsets => HasPositionOffset(true) || HasPositionOffset(false)
            || HasRotationOffset(true) || HasRotationOffset(false);

        internal Vector3 AdjustPosition(bool right, Vector3 position) => HasPositionOffset(right)
            ? position + transform.TransformVector(right ? rightPositionOffset : leftPositionOffset)
            : position;

        public Pose Adjust(bool right, Pose reference)
        {
#if UNITY_EDITOR
            var localReference = new Pose(transform.InverseTransformPoint(reference.position),
                Quaternion.Inverse(transform.rotation) * reference.rotation);
            if (right)
            {
                rightReference = localReference;
                hasRightReference = true;
            }
            else
            {
                leftReference = localReference;
                hasLeftReference = true;
            }
#endif
            reference.position = AdjustPosition(right, reference.position);
            if (HasRotationOffset(right))
            {
                var rifleRotation = transform.rotation;
                reference.rotation = rifleRotation * Quaternion.Euler(right ? rightRotationOffset : leftRotationOffset)
                    * Quaternion.Inverse(rifleRotation) * reference.rotation;
            }
            return reference;
        }

        internal void ApplyToAnimatedArm(bool right, Transform upper, Transform fore, Transform hand)
        {
            if (upper == null || fore == null || hand == null) return;
            var reference = new Pose(hand.position, hand.rotation);
            var adjusted = Adjust(right, reference);
            if (HasPositionOffset(right))
                SolveArm(upper, fore, hand, new Pose(adjusted.position, reference.rotation), fore.position - upper.position);
            if (HasRotationOffset(right)) hand.rotation = adjusted.rotation;
        }

        internal static void SolveArm(Transform upper, Transform fore, Transform hand, Pose target,
            Vector3 bendDirection, float maxForearmRoll = 180f, float maxWristSwing = 180f)
        {
            float a = Vector3.Distance(upper.position, fore.position);
            float b = Vector3.Distance(fore.position, hand.position);
            var delta = target.position - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            var axis = delta.normalized;
            var bend = Vector3.ProjectOnPlane(bendDirection, axis).normalized;
            if (bend.sqrMagnitude < 0.01f) bend = Vector3.ProjectOnPlane(Vector3.down, axis).normalized;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            var elbow = upper.position + axis * along + bend * height;
            upper.rotation = Quaternion.FromToRotation(fore.position - upper.position, elbow - upper.position) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, target.position - fore.position) * fore.rotation;
            // 팔뚝에 먼저 회전을 분산해 손목과 소매가 한 지점에서 비틀리지 않게 한다.
            var foreAxis = (hand.position - fore.position).normalized;
            var currentUp = Vector3.ProjectOnPlane(hand.up, foreAxis);
            var desiredUp = Vector3.ProjectOnPlane(target.rotation * Vector3.up, foreAxis);
            if (currentUp.sqrMagnitude > 0.001f && desiredUp.sqrMagnitude > 0.001f)
            {
                float roll = Mathf.Clamp(Vector3.SignedAngle(currentUp, desiredUp, foreAxis), -maxForearmRoll, maxForearmRoll);
                fore.rotation = Quaternion.AngleAxis(roll, foreAxis) * fore.rotation;
            }
            hand.rotation = maxWristSwing < 180f ? Quaternion.RotateTowards(hand.rotation, target.rotation, maxWristSwing) : target.rotation;
        }
    }
}
