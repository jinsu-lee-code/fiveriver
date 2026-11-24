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
        public const int Aim = 1, Watch = 2, Carry = 3, Slung = 4, PortStand = 10, PortMove = 11;
    }

    // 몸통 · 목 · 머리 · 두 팔 · 손가락과 총(몸 기준)의 한 프레임 자세를 기억했다가 섞는다.
    public sealed class AllyPoseFade
    {
        readonly Transform root, weapon;
        readonly Transform[] bones;
        readonly Quaternion[] held, last;
        Vector3 heldGunPos, lastGunPos;
        Quaternion heldGunRot, lastGunRot;
        bool hasLast, lastHandsOn, heldHandsOn;
        public readonly PoseFadeClock Clock;
        // 2026-11-24 (아군 동작 검수): 팔 뼈와 총을 따로 섞으면 넘어가는 동안 두 손이 총에서 18~24 cm 떨어져
        // 총이 혼자 떠서 돌았다. 넘어가는 동안 손은 총에 대한 손 자리(전 자세 ↔ 새 자세)를 섞은 곳으로 팔을 다시 푼다.
        // (여기 오는 들기 방식은 모두 두 손으로 총을 쥔다. 새 자세의 손이 아직 닿지 못하는 프레임도 총을 놓지 않는다.)
        readonly Hand[] hands;
        sealed class Hand
        {
            public Transform upper, fore, hand;
            public Vector3 lastPos, heldPos; public Quaternion lastRot, heldRot;
        }

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
            var arms = new System.Collections.Generic.List<Hand>();
            if (animator != null && animator.isHuman)
            {
                AddHand(arms, animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
                AddHand(arms, animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            }
            hands = arms.ToArray();
            held = new Quaternion[bones.Length];
            last = new Quaternion[bones.Length];
        }

        public void Clear() { Clock.Clear(); hasLast = false; }

        static void AddHand(System.Collections.Generic.List<Hand> list, Animator animator, HumanBodyBones upper, HumanBodyBones fore, HumanBodyBones hand)
        {
            var u = animator.GetBoneTransform(upper); var f = animator.GetBoneTransform(fore); var h = animator.GetBoneTransform(hand);
            if (u != null && f != null && h != null) list.Add(new Hand { upper = u, fore = f, hand = h });
        }

        void Note(Hand h)
        {
            h.lastPos = weapon.InverseTransformPoint(h.hand.position);
            h.lastRot = Quaternion.Inverse(weapon.rotation) * h.hand.rotation;
        }

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
                foreach (var h in hands) { h.heldPos = h.lastPos; h.heldRot = h.lastRot; }
                heldHandsOn = lastHandsOn;
            }
            if (w > 0f && hasLast)
            {
                foreach (var h in hands) Note(h);       // the new pose's hands on the rifle, before mixing
                for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, held[i], w);
                var pos = Vector3.Lerp(root.InverseTransformPoint(weapon.position), heldGunPos, w);
                var rot = Quaternion.Slerp(Quaternion.Inverse(root.rotation) * weapon.rotation, heldGunRot, w);
                weapon.SetPositionAndRotation(root.TransformPoint(pos), root.rotation * rot);
                if (heldHandsOn && mode != PoseFadeClock.Slung) foreach (var h in hands)
                {
                    AllyRiflePose.SolveArm(h.upper, h.fore, h.hand, weapon.TransformPoint(Vector3.Lerp(h.lastPos, h.heldPos, w)),
                        weapon.rotation * Quaternion.Slerp(h.lastRot, h.heldRot, w));
                }
            }
            for (int i = 0; i < bones.Length; i++) last[i] = bones[i].localRotation;
            foreach (var h in hands) Note(h);
            lastHandsOn = mode != PoseFadeClock.Slung;
            lastGunPos = root.InverseTransformPoint(weapon.position);
            lastGunRot = Quaternion.Inverse(root.rotation) * weapon.rotation;
            hasLast = true;
        }
    }
}
