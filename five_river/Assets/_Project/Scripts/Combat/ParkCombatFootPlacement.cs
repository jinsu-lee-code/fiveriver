using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Park's forward boot only. The rear kneeling leg, pelvis, actor root and arms stay authored.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class ParkCombatFootPlacement : MonoBehaviour
    {
        [SerializeField, Range(-.08f, .08f)] float soleHeightAdjustment;
        [SerializeField, Range(0f, .02f)] float soleClearance = .003f;
        [SerializeField, Range(.08f, .25f)] float maximumCorrection = .16f;
        Animator animator;
        FlowDirector director;
        Terrain ground;
        bool isPark;
        public float AppliedWeight { get; private set; }
        public float EstimatedUncorrectedGap { get; private set; }
        public Vector3 TargetSole { get; private set; }

        void Awake()
        {
            animator = GetComponent<Animator>();
            var actor = GetComponentInParent<AllyCombatant>();
            isPark = actor != null && actor.name == "ParkYongpal";
            director = FindFirstObjectByType<FlowDirector>();
            ground = Terrain.activeTerrain;
        }

        static bool Kneeling(AnimatorStateInfo state) => state.IsName("Idle_Trench") || state.IsName("Fire_Crouch");

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || animator == null) return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            AppliedWeight = 0f;
            if (!isPark || !animator.isHuman || ground == null || ground.terrainData == null) return;
            if (director != null && (director.Flow.Section == FlowSection.Title || director.Flow.Section == FlowSection.Tutorial)) return;
            float weight = Kneeling(animator.GetCurrentAnimatorStateInfo(0)) ? 1f : 0f;
            if (animator.IsInTransition(0))
                weight = Mathf.Lerp(weight, Kneeling(animator.GetNextAnimatorStateInfo(0)) ? 1f : 0f,
                    Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
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
    }
}