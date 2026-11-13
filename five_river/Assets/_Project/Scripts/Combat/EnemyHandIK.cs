using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Puts the KPA rifleman's hands on the rifle he is carrying. The clips hold the hands for a shorter weapon,
    // so after EnemyAgent lines the Mosin up with the hands the left palm could hover up to 10 cm off it.
    // Lives on the Animator object (OnAnimatorIK is only sent there); EnemyAgent adds it and supplies the rifle.
    // 2026-11-10: while he runs the hands are also shaped to hold it (Shape, called by EnemyAgent once the rifle
    // is placed). Seen close, the run clip's trigger hand lay on the receiver with a pointing finger and its
    // support hand swung open, away from the rifle. The aiming and firing clips grip correctly on their own.
    public sealed class EnemyHandIK : MonoBehaviour
    {
        public static float Weight = 0.85f, Below = 0.03f;
        // Running: where the support wrist sits from the middle of the fore-end wood (rifle space: left of it and
        // below, so the wood lies across the palm), how far ahead of the grip the support hand holds, and how far
        // the trigger palm may be turned from the clip's own pose so the wrist is never wrung round.
        public static Vector3 SupportWrist = new Vector3(-0.09f, -0.047f, 0f);
        public static float SupportAhead = 0.36f, MaxTriggerTurn = 75f;
        // Aiming (2026-11-13, against a 1944 photo of a Mosin held in the aim): the support hand cradles the fore-end
        // about a hand's span ahead of the trigger hand, just in front of the magazine, with the elbow straight under
        // the rifle. With the running values the arm reached almost straight out along the barrel, elbow to the side.
        public static float AimSupportAhead = 0.27f;
        public static Vector3 RunElbow = new Vector3(-0.4f, -0.8f, 0.1f), AimElbow = new Vector3(-0.1f, -1f, 0.15f);
        // The stock axis runs through the middle of the whole mesh, and the butt drops: at the fore-end the wood
        // itself is this far above that axis (2026-11-11, measured on the mesh: the wood there spans 0 to 5.5 cm
        // above the axis line the support wrist used to be hung 3 cm under, which left the palm 6 cm off it).
        public static float ForeEndAbove = 0.061f;
        // How far each joint of the support hand's fingers bends (knuckle, middle, tip; degrees): up the far side
        // of the wood and over the top of it. The trigger hand keeps the shallower curl of a hand on the stock wrist.
        public static Vector3 SupportCurl = new Vector3(75f, 70f, 35f);
        Animator animator;
        Transform rifle;
        Vector3 axisLocalStart, axisLocalEnd;
        Transform leftHand, rightHand, leftUpper, leftFore, leftIndex, leftLittle, rightIndex, rightLittle;
        Transform[][] leftFingers, rightFingers;
        Quaternion leftPalmFrame, rightPalmFrame;
        readonly Dictionary<Transform, Quaternion> fingerRest = new Dictionary<Transform, Quaternion>();
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
            if (!animator.isHuman) return;
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            leftLittle = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            rightIndex = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            rightLittle = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            leftFingers = Fingers(true);
            rightFingers = Fingers(false);
            leftPalmFrame = PalmFrame(leftHand, leftIndex, leftLittle, true);
            rightPalmFrame = PalmFrame(rightHand, rightIndex, rightLittle, false);
            var skeleton = animator.avatar.humanDescription.skeleton;
            foreach (var chain in leftFingers) CacheRest(chain, skeleton);
            foreach (var chain in rightFingers) CacheRest(chain, skeleton);
        }

        // Feet on a slope (2026-11-13, "다리 질질끌면서 내려오는 부분"): the clips lift the feet over flat ground, so
        // running down the hillside the rear foot (uphill, where the ground stands higher) went through the slope
        // and seemed to drag. Each foot is moved up or down by how much the ground under it differs from the ground
        // under the body, and turned a little to lie with the slope. EnemyAgent turns it off when he dies.
        public bool FeetOnSlope { get; set; }
        public static float FootTilt = 0.6f, MaxFootShift = 0.45f;
        public static bool SlopeFeet = true; // off = feet as the clip puts them (debug / comparison)
        Terrain ground;

        void PlaceFeet()
        {
            if (ground == null) ground = Terrain.activeTerrain;
            if (ground == null) return;
            var root = animator.transform.position;
            float under = Ground(root);
            foreach (var goal in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
            {
                var p = animator.GetIKPosition(goal);
                float shift = Mathf.Clamp(Ground(p) - under, -MaxFootShift, MaxFootShift);
                animator.SetIKPosition(goal, p + Vector3.up * shift);
                animator.SetIKPositionWeight(goal, 1f);
                var normal = ground.terrainData.GetInterpolatedNormal(
                    (p.x - ground.transform.position.x) / ground.terrainData.size.x, (p.z - ground.transform.position.z) / ground.terrainData.size.z);
                animator.SetIKRotation(goal, Quaternion.FromToRotation(Vector3.up, normal) * animator.GetIKRotation(goal));
                animator.SetIKRotationWeight(goal, FootTilt);
            }
        }

        float Ground(Vector3 p) => ground.SampleHeight(p) + ground.transform.position.y;

        void OnAnimatorIK(int layer)
        {
            if (animator != null)
            {
                if (FeetOnSlope && SlopeFeet) PlaceFeet();
                else
                {
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f); animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
                    animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f); animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
                }
            }
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

        // Off = the hands are left as the clip and the IK pass put them (how it was before 2026-11-10).
        public static bool ShapeHands = true;

        bool Shaped => ShapeHands && leftHand != null && rightHand != null && leftUpper != null && leftFore != null
            && leftIndex != null && leftLittle != null && rightIndex != null && rightLittle != null;

        // The run clip does not hold a rifle: the trigger hand points a finger and the support hand swings open.
        // While he runs (weight = how much of the run carry is on) the support hand is brought under the
        // fore-end, both palms are turned onto the wood and the fingers closed round it. The aiming and firing
        // clips already grip correctly and are left exactly as they are (weight 0).
        // Call in LateUpdate, after the rifle has been placed.
        // aim: 1 when the rifle is in the aim (cover, firing), 0 while it is carried running
        public void Shape(float weight, float aim = 0f)
        {
            if (weight <= 0f || !Holding || rifle == null || !Shaped) return;
            weight = Mathf.Clamp01(weight);
            aim = Mathf.Clamp01(aim);
            var a = rifle.TransformPoint(axisLocalStart);
            var along = (rifle.TransformPoint(axisLocalEnd) - a).normalized;
            // the wrist goes under and to the left of the wood, so the palm (not the wrist bone) lies under it
            var wrist = a + along * Mathf.Lerp(SupportAhead, AimSupportAhead, aim) + rifle.up * ForeEndAbove + rifle.TransformDirection(SupportWrist);
            Reach(leftUpper, leftFore, leftHand, Vector3.Lerp(leftHand.position, wrist, weight),
                animator.transform.TransformDirection(Vector3.Lerp(RunElbow, AimElbow, aim)));
            // trigger hand: palm against the right of the stock wrist, fingers along the wood and slightly up
            var trigger = Quaternion.LookRotation(rifle.TransformDirection(new Vector3(0f, .35f, 1f)), -rifle.right) * Quaternion.Inverse(rightPalmFrame);
            rightHand.rotation = Quaternion.Slerp(rightHand.rotation, Quaternion.RotateTowards(rightHand.rotation, trigger, MaxTriggerTurn), weight);
            // support hand: palm up under the fore-end, fingers wrapping across to the right
            var across = rifle.TransformDirection(new Vector3(1f, .12f, .05f));
            var support = Quaternion.LookRotation(across, Vector3.Cross(rifle.forward, across)) * Quaternion.Inverse(leftPalmFrame);
            leftHand.rotation = Quaternion.Slerp(leftHand.rotation, support, weight);
            Close(rightFingers, rightIndex, rightLittle, true, weight);
            Close(leftFingers, leftIndex, leftLittle, false, weight);
        }

        // Two-bone reach: the wrist goes to the target, the elbow bends towards the hint.
        static void Reach(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 elbowHint)
        {
            float a = Vector3.Distance(upper.position, fore.position);
            float b = Vector3.Distance(fore.position, hand.position);
            var delta = target - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            var axis = delta.normalized;
            var bend = Vector3.ProjectOnPlane(elbowHint, axis).normalized;
            if (bend.sqrMagnitude < 0.01f) bend = Vector3.ProjectOnPlane(Vector3.down, axis).normalized;
            float alongAxis = (a * a - b * b + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - alongAxis * alongAxis));
            var elbow = upper.position + axis * alongAxis + bend * height;
            upper.rotation = Quaternion.FromToRotation(fore.position - upper.position, elbow - upper.position) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, target - fore.position) * fore.rotation;
        }

        // Fingers start from the avatar's own straight joints (never a second curl on top of the clip's) and
        // bend towards the wood. The trigger finger stays long, alongside the guard.
        void Close(Transform[][] fingers, Transform index, Transform little, bool triggerHand, float weight)
        {
            var axis = (index.position - little.position).normalized;
            // the support hand closes round the fore-end wood, the trigger hand round the stock wrist
            var stockA = rifle.TransformPoint(axisLocalStart) + (triggerHand ? Vector3.zero : rifle.up * ForeEndAbove);
            var stockDirection = rifle.forward;
            for (int finger = 0; finger < fingers.Length; finger++)
            {
                var chain = fingers[finger];
                if (chain[0] == null || chain[1] == null || chain[2] == null) continue;
                Quaternion clip0 = chain[0].localRotation, clip1 = chain[1].localRotation, clip2 = chain[2].localRotation;
                foreach (var bone in chain) bone.localRotation = fingerRest[bone];
                var knuckle = chain[0].position;
                var onAxis = stockA + stockDirection * Vector3.Dot(knuckle - stockA, stockDirection);
                var inward = onAxis - knuckle;
                float sign = Vector3.Dot(Vector3.Cross(axis, chain[1].position - knuckle), inward) >= 0f ? 1f : -1f;
                bool triggerFinger = triggerHand && finger == 0;
                var curl = triggerFinger ? new Vector3(12f, 18f, 8f) : triggerHand ? new Vector3(32f, 58f, 24f) : SupportCurl;
                chain[0].rotation = Quaternion.AngleAxis(sign * curl.x, axis) * chain[0].rotation;
                chain[1].rotation = Quaternion.AngleAxis(sign * curl.y, axis) * chain[1].rotation;
                chain[2].rotation = Quaternion.AngleAxis(sign * curl.z, axis) * chain[2].rotation;
                if (weight >= 1f) continue;
                // easing in and out of the run: part of the way from the clip's own fingers
                chain[0].localRotation = Quaternion.Slerp(clip0, chain[0].localRotation, weight);
                chain[1].localRotation = Quaternion.Slerp(clip1, chain[1].localRotation, weight);
                chain[2].localRotation = Quaternion.Slerp(clip2, chain[2].localRotation, weight);
            }
        }

        Transform[][] Fingers(bool left)
        {
            var result = new Transform[4][];
            // HumanBodyBones keeps each finger's proximal/intermediate/distal triplet contiguous.
            int first = (int)(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            for (int finger = 0; finger < 4; finger++)
            {
                result[finger] = new Transform[3];
                for (int joint = 0; joint < 3; joint++)
                    result[finger][joint] = animator.GetBoneTransform((HumanBodyBones)(first + finger * 3 + joint));
            }
            return result;
        }

        void CacheRest(Transform[] chain, SkeletonBone[] skeleton)
        {
            foreach (var bone in chain)
            {
                if (bone == null) continue;
                var rotation = bone.localRotation;
                foreach (var rest in skeleton) if (rest.name == bone.name) { rotation = rest.rotation; break; }
                fingerRest[bone] = rotation;
            }
        }

        // Same construction as AllyRiflePose.PalmFrame: forward runs from the wrist to the knuckles, up is the way
        // the palm faces.
        static Quaternion PalmFrame(Transform hand, Transform index, Transform little, bool left)
        {
            if (hand == null || index == null || little == null) return Quaternion.identity;
            var along = hand.InverseTransformDirection((index.position + little.position) * .5f - hand.position);
            var across = hand.InverseTransformDirection(index.position - little.position);
            return Quaternion.LookRotation(along, left ? Vector3.Cross(across, along) : Vector3.Cross(along, across));
        }
    }
}
