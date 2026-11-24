using UnityEngine;

namespace FiveRivers.Combat
{
    // 소총을 드는 방식이 바뀌는 프레임(앞에총 ↔ 겨눔 ↔ 들기)에 총과 팔이 한 프레임에 튀지 않게, 직전에 보인 자세에서
    // 짧게 넘어간다 (2026-11-24). 고지 돌격에서 엄폐 자리에 닿아 겨눌 때 총이 한 프레임에 40 cm · 130도 뛰던 것.
    // 클립(AllyClipPlayer)이 총을 가진 동안의 드나듦은 클립 쪽이 이미 섞으므로 여기서는 하지 않는다.
    public sealed class PoseFadeClock
    {
        public float Duration;
        float elapsed = float.MaxValue;
        int lastMode = None;

        public PoseFadeClock(float duration) { Duration = duration; }

        // 이번 프레임의 들기 방식. 방식이 바뀌었으면(둘 다 클립이 아닐 때) 넘어가기를 처음부터 시작한다.
        public void Note(int mode, float deltaTime)
        {
            if (lastMode != None && mode != lastMode && mode != Clip && lastMode != Clip && Duration > 0f) elapsed = 0f;
            else if (elapsed < float.MaxValue) elapsed += Mathf.Max(0f, deltaTime);
            lastMode = mode;
        }

        public void Clear() { lastMode = None; elapsed = float.MaxValue; }

        // 직전에 보인 자세를 얼마나 남기나: 바뀐 프레임 1 → Duration 뒤 0 (부드럽게)
        public float HoldWeight
        {
            get
            {
                if (elapsed >= Duration) return 0f;
                float t = Mathf.Clamp01(elapsed / Duration);
                return 1f - t * t * (3f - 2f * t);
            }
        }

        public const int Clip = -1, None = int.MinValue;
        // 들기 방식 번호 (AllyCombatant)
        public const int Aim = 1, Watch = 2, Carry = 3, PortStand = 10, PortMove = 11;
    }

    // 몸통 · 목 · 머리 · 두 팔 · 손가락과 총(몸 기준)의 한 프레임 자세를 기억했다가 섞는다.
    public sealed class AllyPoseFade
    {
        readonly Transform root, weapon;
        readonly Transform[] bones;
        readonly Quaternion[] held, last;
        Vector3 heldGunPos, lastGunPos;
        Quaternion heldGunRot, lastGunRot;
        bool hasLast;
        public readonly PoseFadeClock Clock;

        static readonly HumanBodyBones[] Upper =
        {
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        };

        public AllyPoseFade(Animator animator, Transform root, Transform weapon, float duration)
        {
            this.root = root; this.weapon = weapon;
            Clock = new PoseFadeClock(duration);
            var list = new System.Collections.Generic.List<Transform>();
            if (animator != null && animator.isHuman)
            {
                foreach (var b in Upper) { var t = animator.GetBoneTransform(b); if (t != null) list.Add(t); }
                for (int b = (int)HumanBodyBones.LeftThumbProximal; b <= (int)HumanBodyBones.RightLittleDistal; b++)
                {
                    var t = animator.GetBoneTransform((HumanBodyBones)b); if (t != null) list.Add(t);
                }
            }
            bones = list.ToArray();
            held = new Quaternion[bones.Length];
            last = new Quaternion[bones.Length];
        }

        public void Clear() { Clock.Clear(); hasLast = false; }

        // 모든 자세 계산이 끝난 뒤 한 번 부른다: 방식이 바뀌었으면 직전 프레임 자세를 잡아 두고, 넘어가는 동안 그쪽으로 섞는다.
        public void Apply(int mode, float deltaTime)
        {
            if (weapon == null || bones.Length == 0) return;
            bool wasFading = Clock.HoldWeight > 0f;
            Clock.Note(mode, deltaTime);
            float w = Clock.HoldWeight;
            if (w > 0f && hasLast && (!wasFading || w >= 0.999f))
            {
                // 새로 시작(또는 넘어가는 중에 다시 바뀜): 지금 화면에 있던 자세가 출발점
                for (int i = 0; i < bones.Length; i++) held[i] = last[i];
                heldGunPos = lastGunPos; heldGunRot = lastGunRot;
            }
            if (w > 0f && hasLast)
            {
                for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, held[i], w);
                var pos = Vector3.Lerp(root.InverseTransformPoint(weapon.position), heldGunPos, w);
                var rot = Quaternion.Slerp(Quaternion.Inverse(root.rotation) * weapon.rotation, heldGunRot, w);
                weapon.SetPositionAndRotation(root.TransformPoint(pos), root.rotation * rot);
            }
            for (int i = 0; i < bones.Length; i++) last[i] = bones[i].localRotation;
            lastGunPos = root.InverseTransformPoint(weapon.position);
            lastGunRot = Quaternion.Inverse(root.rotation) * weapon.rotation;
            hasLast = true;
        }
    }
}
