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
        Transform head, chest, hips, leftFoot, rightFoot, leftHand, rightHand;
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
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            ground = Terrain.activeTerrain;
            weight = 0f;
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
            if (weight <= 0f) return;

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
