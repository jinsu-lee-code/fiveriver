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

        // 2026-11-25 (고지 출발 손): 끊긴 프레임 하나(고지 돌격 시작에 141 ms)가 섞기를 한 번에 40 % 가까이 넘겨서 총이
        // 한 프레임에 30~37 cm · 30도 튀고 손이 36~45 cm 떨어졌다. 섞는 시계는 한 프레임에 이만큼만 간다(30 fps 한 프레임).
        // 아군 자세를 섞는 시계(이 클래스, AllyClipPlayer의 클립 드나듦 · 클립끼리 섞기 · 달리기 총 들기)가 모두 쓴다.
        public static float MaxStep = 1f / 30f;
        public static float Step(float deltaTime) => Mathf.Clamp(deltaTime, 0f, MaxStep);

        // 이번 프레임의 들기 방식. 방식이 바뀌었으면(둘 다 클립이 아닐 때) 넘어가기를 처음부터 시작한다.
        public void Note(int mode, float deltaTime)
        {
            if (lastMode != None && mode != lastMode && mode != Clip && lastMode != Clip && Duration > 0f) elapsed = 0f;
            else if (elapsed < float.MaxValue) elapsed += Step(deltaTime);
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
        // 두 손으로 쥐고, 팔이 닿지 않으면 총을 어깨 쪽으로 당기는 방식 (AllyRiflePose.KeepInReach): 겨눔 · 살핌 · 앞에총
        public static bool BringsIntoReach(int mode) => mode == Aim || mode == Watch || mode >= PortStand;
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

        // 2026-11-25 (인계 남은 일 3): the rifle was mixed in the body's root frame (at his feet) while the hips crouched or stood up
        // under it, so for 0.1-0.4 s it hung where the arms could not reach (grips 1.2-1.5 arm lengths off the shoulder, measured in
        // play: the hill start, and stopping at cover to aim). It is mixed in the chest's frame: it rides up and down with the body.
        readonly Transform chest;
        Transform Frame => chest != null ? chest : root;

        public AllyPoseFade(Animator animator, Transform root, Transform weapon, float duration)
        {
            this.root = root; this.weapon = weapon;
            if (animator != null && animator.isHuman)
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
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

        // asked: the grip the hand was asked for this frame instead of where it stopped (a hold KeepInReach brings in).
        // 2026-11-25 (고지 출발 손, 1배속 측정): from port arms to watching, the new pose's rifle was out of reach for the 0.4 s
        // fade, the left hand stopped 13 cm short and that short place was mixed in; the frame the fade ended KeepInReach
        // pulled the rifle in to the true grip and it jumped 14 cm, the right elbow 100 -> 56 degrees.
        // (only a grip the hand is still on its way to: AllyRiflePose.AskedThisFrame)
        void Note(Hand h, bool asked)
        {
            var at = h.hand.position;
            if (asked && AllyRiflePose.BringIntoReach && AllyRiflePose.AskedThisFrame(h.upper, h.hand, out var target)) at = target;
            h.lastPos = weapon.InverseTransformPoint(at);
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
            bool asked = PoseFadeClock.BringsIntoReach(mode);
            if (w > 0f && hasLast)
            {
                foreach (var h in hands) Note(h, asked);       // the new pose's hands on the rifle, before mixing
                // the new pose's rifle in the frame it is mixed in (before the bones are mixed: its own chest)
                var newPos = Frame.InverseTransformPoint(weapon.position);
                var newRot = Quaternion.Inverse(Frame.rotation) * weapon.rotation;
                for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, held[i], w);
                var pos = Vector3.Lerp(newPos, heldGunPos, w);
                var rot = Quaternion.Slerp(newRot, heldGunRot, w);
                weapon.SetPositionAndRotation(Frame.TransformPoint(pos), Frame.rotation * rot);
                if (heldHandsOn && mode != PoseFadeClock.Slung) foreach (var h in hands)
                {
                    AllyRiflePose.SolveArm(h.upper, h.fore, h.hand, weapon.TransformPoint(Vector3.Lerp(h.lastPos, h.heldPos, w)),
                        weapon.rotation * Quaternion.Slerp(h.lastRot, h.heldRot, w));
                }
            }
            for (int i = 0; i < bones.Length; i++) last[i] = bones[i].localRotation;
            foreach (var h in hands) Note(h, asked);
            lastHandsOn = mode != PoseFadeClock.Slung;
            lastGunPos = Frame.InverseTransformPoint(weapon.position);
            lastGunRot = Quaternion.Inverse(Frame.rotation) * weapon.rotation;
            hasLast = true;
        }
    }
}
