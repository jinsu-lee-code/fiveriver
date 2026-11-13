using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class ParkCombatFootPlacement : MonoBehaviour
    {
        [SerializeField, Range(-.08f, .08f)] float soleHeightAdjustment;
        [SerializeField, Range(0f, .02f)] float soleClearance = .003f;
        [SerializeField, Range(.08f, .25f)] float maximumCorrection = .16f;
        Animator animator;
        AllyCombatant actor;
        FlowDirector director;
        Terrain ground;
        bool isPark, hasBootProbes, standingOwnsRightFoot;
        float standingBlend, standingDrop;
        // Rigid sole vertices 13906 / 13872 of ROK_ParkYongpal_SkinFix, in each toe bone's bind-local space.
        static readonly Vector3 LeftBootToe = new Vector3(0.000408723135f, 0.000672023278f, 0.000361146551f);
        static readonly Vector3 RightBootToe = new Vector3(0.000429094536f, 0.000449511223f, 0.000369057816f);
        public float AppliedWeight { get; private set; }
        public float EstimatedUncorrectedGap { get; private set; }
        public Vector3 TargetSole { get; private set; }

        struct FootSample
        {
            internal bool Valid, HasToe;
            internal Vector3 Position, Sole, Normal, TargetPosition, ToeOffset;
            internal Quaternion Rotation, TargetRotation;
            internal float GroundHeight, Gap;
        }

        void Awake()
        {
            animator = GetComponent<Animator>();
            actor = GetComponentInParent<AllyCombatant>();
            isPark = actor != null && actor.name == "ParkYongpal";
            var skin = GetComponentInChildren<SkinnedMeshRenderer>();
            hasBootProbes = skin != null && skin.sharedMesh != null && skin.sharedMesh.name == "ROK_ParkYongpal_SkinFix";
            director = FindFirstObjectByType<FlowDirector>();
            ground = Terrain.activeTerrain;
        }

        static bool Kneeling(AnimatorStateInfo state) => state.IsName("Idle_Trench") || state.IsName("Fire_Crouch");
        static bool Standing(AnimatorStateInfo state) => state.IsName("Idle_Stand") || state.IsName("Walk_Tired");

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || animator == null) return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            if (standingOwnsRightFoot)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                standingOwnsRightFoot = false;
            }
            AppliedWeight = 0f;
            if (!isPark || !animator.isHuman || ground == null || ground.terrainData == null) { standingBlend = standingDrop = 0f; return; }
            if (director != null && (director.Flow.Section == FlowSection.Title || director.Flow.Section == FlowSection.Tutorial)) { standingBlend = standingDrop = 0f; return; }
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var next = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : current;
            float transition = animator.IsInTransition(0) ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0f;
            float kneelingWeight = Mathf.Lerp(Kneeling(current) ? 1f : 0f, Kneeling(next) ? 1f : 0f, transition);
            float standingWeight = Mathf.Lerp(Standing(current) ? 1f : 0f, Standing(next) ? 1f : 0f, transition);
            float walkingWeight = Mathf.Lerp(current.IsName("Walk_Tired") ? 1f : 0f, next.IsName("Walk_Tired") ? 1f : 0f, transition);
            bool scope = standingWeight > 0f && director != null && director.Flow.IsExpanded
                && (director.Flow.Section == FlowSection.HillAssault || director.Flow.Section == FlowSection.Dawn)
                && animator.avatar != null && animator.avatar.name == "SK_CH_ROK_ParkYongpalAvatar"
                && actor.GetComponentInChildren<Animator>() == animator;
            // Save both unsolved goals before the original kneeling pass writes the left goal.
            bool checkToes = hasBootProbes && walkingWeight > 0f && kneelingWeight == 0f;
            var left = scope ? ReadStandingFoot(AvatarIKGoal.LeftFoot, animator.leftFeetBottomHeight, checkToes) : default;
            var right = scope ? ReadStandingFoot(AvatarIKGoal.RightFoot, animator.rightFeetBottomHeight, checkToes) : default;
            ApplyKneeling(kneelingWeight);
            if (!scope || !left.Valid || !right.Valid
                || !SampleGround(actor.transform.position, out float rootHeight)
                || Mathf.Abs(actor.transform.position.y - rootHeight) > maximumCorrection
                || GroundSeparation(new Vector3(left.Sole.x, left.GroundHeight, left.Sole.z), left.Normal,
                    new Vector3(right.Sole.x, right.GroundHeight, right.Sole.z), right.Normal) > .12f)
            { standingBlend = standingDrop = 0f; return; }
            float lowest = Mathf.Min(left.Gap, right.Gap);
            standingBlend = Mathf.Min(standingWeight, Mathf.MoveTowards(standingBlend, standingWeight, Time.deltaTime / .12f));
            float walk = walkingWeight / standingWeight;
            float leftStance = Mathf.Lerp(1f, StandingStanceWeight(left.Gap - lowest), walk) * CorrectionWeight(left.Gap);
            float rightStance = Mathf.Lerp(1f, StandingStanceWeight(right.Gap - lowest), walk) * CorrectionWeight(right.Gap);
            float drop = 0f;
            if (walk > 0f && kneelingWeight == 0f)
            {
                float wantedDrop = Mathf.Min(Mathf.Max(SupportDrop(left, true, leftStance), SupportDrop(right, false, rightStance)), maximumCorrection);
                // Limit how quickly the pelvis moves when support shifts between feet.
                standingDrop = Mathf.MoveTowards(standingDrop, wantedDrop, .75f * Time.deltaTime);
                drop = standingDrop * standingBlend * walk;
                animator.bodyPosition -= Vector3.up * drop;
            }
            else standingDrop = 0f;
            ApplyStandingFoot(AvatarIKGoal.LeftFoot, left, standingBlend * leftStance, drop > 0f, standingBlend * walk);
            standingOwnsRightFoot = ApplyStandingFoot(AvatarIKGoal.RightFoot, right, standingBlend * rightStance, drop > 0f, standingBlend * walk);
        }

        // Keep the existing unscaled kneeling calibration, including the serialized -.024 value.
        void ApplyKneeling(float weight)
        {
            if (weight <= 0f) return;

            // Read the animation's IK goal inside its pass, not last frame's already solved bone.
            var position = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            var rotation = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            float soleHeight = Mathf.Max(.01f, animator.leftFeetBottomHeight + soleHeightAdjustment);
            var soleOffset = Vector3.down * soleHeight;
            var sole = position + rotation * soleOffset;
            var local = ground.transform.InverseTransformPoint(sole);
            var size = ground.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) return;
            float height = ground.SampleHeight(sole) + ground.transform.position.y;
            EstimatedUncorrectedGap = sole.y - height;
            if (Mathf.Abs(EstimatedUncorrectedGap) > maximumCorrection) return;
            var normal = ground.transform.TransformDirection(ground.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z)).normalized;
            var aligned = Quaternion.FromToRotation(rotation * Vector3.up, normal) * rotation;
            var targetRotation = Quaternion.RotateTowards(rotation, aligned, 18f);
            TargetSole = new Vector3(sole.x, height + soleClearance, sole.z);
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, TargetSole - targetRotation * soleOffset);
            animator.SetIKRotation(AvatarIKGoal.LeftFoot, targetRotation);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, weight);
            AppliedWeight = weight;
        }

        FootSample ReadStandingFoot(AvatarIKGoal goal, float bottomHeight, bool checkToe)
        {
            var rotation = animator.GetIKRotation(goal);
            var sample = new FootSample { Position = animator.GetIKPosition(goal), Rotation = rotation };
            var offset = Vector3.down * bottomHeight * animator.transform.lossyScale.y;
            sample.Sole = sample.Position + rotation * offset;
            sample.Valid = SampleGround(sample.Sole, out sample.GroundHeight, out sample.Normal);
            sample.Gap = sample.Sole.y - sample.GroundHeight;
            var aligned = Quaternion.FromToRotation(rotation * Vector3.up, sample.Normal) * rotation;
            sample.TargetRotation = Quaternion.RotateTowards(rotation, aligned, 35f);
            sample.TargetPosition = new Vector3(sample.Sole.x, sample.GroundHeight + soleClearance, sample.Sole.z) - sample.TargetRotation * offset;
            if (checkToe)
            {
                bool left = goal == AvatarIKGoal.LeftFoot;
                var toe = animator.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                if (toe != null)
                {
                    sample.HasToe = true;
                    sample.ToeOffset = Quaternion.Inverse(rotation) * (toe.TransformPoint(left ? LeftBootToe : RightBootToe) - sample.Position);
                }
            }
            return sample;
        }

        float SupportDrop(FootSample sample, bool left, float stance)
        {
            if (stance <= 0f) return 0f;
            var hip = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var knee = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var foot = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            if (hip == null || knee == null || foot == null) return 0f;
            float length = Vector3.Distance(hip.position, knee.position) + Vector3.Distance(knee.position, foot.position);
            var target = Vector3.Lerp(sample.Position, sample.TargetPosition, stance);
            float drop = RequiredStandingDrop(hip.position, target, length);
            return drop;
        }

        internal static float RequiredStandingDrop(Vector3 hip, Vector3 target, float length)
        {
            var delta = hip - target;
            float horizontal = delta.x * delta.x + delta.z * delta.z;
            return horizontal >= length * length ? 0f : Mathf.Max(0f, delta.y - Mathf.Sqrt(length * length - horizontal));
        }

        bool SampleGround(Vector3 position, out float height) => SampleGround(position, out height, out _);

        bool SampleGround(Vector3 position, out float height, out Vector3 normal)
        {
            height = 0f;
            normal = Vector3.up;
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return false;
            var local = ground.transform.InverseTransformPoint(position);
            var size = ground.terrainData.size;
            if (size.x <= 0f || size.z <= 0f || local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) return false;
            normal = ground.transform.TransformDirection(ground.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z)).normalized;
            height = ground.SampleHeight(position) + ground.transform.position.y;
            return float.IsFinite(height) && Vector3.Angle(normal, Vector3.up) <= 35f;
        }

        internal static float StandingStanceWeight(float heightAboveLowest)
        {
            float t = Mathf.Clamp01((heightAboveLowest - .015f) / .045f);
            return 1f - t * t * (3f - 2f * t);
        }

        float CorrectionWeight(float gap) => Mathf.InverseLerp(maximumCorrection, maximumCorrection - .04f, Mathf.Abs(gap));

        // A long stride on a continuous slope is not a step between two support surfaces.
        internal static float GroundSeparation(Vector3 first, Vector3 firstNormal, Vector3 second, Vector3 secondNormal)
            => Mathf.Abs(Vector3.Dot(second - first, (firstNormal + secondNormal).normalized));

        bool ApplyStandingFoot(AvatarIKGoal goal, FootSample sample, float weight, bool preserveWorldPose, float walkWeight)
        {
            // Solve the same world-space blend after lowering the body, including the unplanted foot.
            if (preserveWorldPose || sample.HasToe)
            {
                var position = Vector3.Lerp(sample.Position, sample.TargetPosition, weight);
                var rotation = Quaternion.Slerp(sample.Rotation, sample.TargetRotation, weight);
                float lift = 0f;
                if (sample.HasToe)
                {
                    var tip = position + rotation * sample.ToeOffset;
                    if (SampleGround(tip, out float height))
                        lift = Mathf.Clamp(height + soleClearance - tip.y, 0f, maximumCorrection) * walkWeight;
                }
                if (preserveWorldPose || lift > 0f)
                {
                    sample.TargetPosition = position + Vector3.up * lift;
                    sample.TargetRotation = rotation;
                    weight = 1f;
                }
            }
            if (weight <= 0f) return false;
            float previous = animator.GetIKPositionWeight(goal);
            animator.SetIKPosition(goal, Vector3.Lerp(animator.GetIKPosition(goal), sample.TargetPosition, weight / (previous + weight)));
            animator.SetIKPositionWeight(goal, Mathf.Clamp01(previous + weight));
            animator.SetIKRotation(goal, Quaternion.Slerp(animator.GetIKRotation(goal), sample.TargetRotation, weight / (previous + weight)));
            animator.SetIKRotationWeight(goal, Mathf.Clamp01(previous + weight));
            return true;
        }
    }
}
