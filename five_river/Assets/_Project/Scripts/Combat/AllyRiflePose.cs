using UnityEngine;

namespace FiveRivers.Combat
{
    // World-character constraints: keep the stock shouldered and solve both arms without
    // moving/scaling the skeleton or replacing the existing skinned adult character.
    public sealed class AllyRiflePose
    {
        readonly Transform rightUpper, rightFore, rightHand, leftUpper, leftFore, leftHand, head, neck;
        readonly Transform root, weapon;
        readonly AlliedRifleFit fit;
        readonly Transform[][] leftFingers, rightFingers;
        readonly Transform leftIndex, leftLittle, rightIndex, rightLittle;
        readonly Quaternion leftPalmFrame, rightPalmFrame;
        readonly Transform[] leftThumb, rightThumb;
        readonly System.Collections.Generic.Dictionary<Transform, Quaternion> fingerRest = new System.Collections.Generic.Dictionary<Transform, Quaternion>();
        static readonly Vector3 Butt = new Vector3(0f, -0.02f, -0.375f);
        static readonly Vector3 RightGrip = new Vector3(0f, -0.035f, -0.16f);
        static readonly Vector3 LeftGrip = new Vector3(0f, -0.015f, 0.22f);

        public AllyRiflePose(Animator animator, Transform actor, Transform rifle)
        {
            root = actor; weapon = rifle;
            fit = animator.GetComponent<AlliedRifleFit>();
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightFore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftFore = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            leftFingers = Fingers(animator, true);
            rightFingers = Fingers(animator, false);
            leftIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            leftLittle = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            rightIndex = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            rightLittle = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            leftPalmFrame = PalmFrame(leftHand, leftIndex, leftLittle, true);
            rightPalmFrame = PalmFrame(rightHand, rightIndex, rightLittle, false);
            leftThumb = new[] { animator.GetBoneTransform(HumanBodyBones.LeftThumbProximal), animator.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate), animator.GetBoneTransform(HumanBodyBones.LeftThumbDistal) };
            rightThumb = new[] { animator.GetBoneTransform(HumanBodyBones.RightThumbProximal), animator.GetBoneTransform(HumanBodyBones.RightThumbIntermediate), animator.GetBoneTransform(HumanBodyBones.RightThumbDistal) };
            var skeleton = animator.avatar.humanDescription.skeleton;
            foreach (var chain in leftFingers) CacheFingerRest(chain, skeleton);
            foreach (var chain in rightFingers) CacheFingerRest(chain, skeleton);
            CacheFingerRest(leftThumb, skeleton); CacheFingerRest(rightThumb, skeleton);
        }

        public void Apply(Vector3 aim, float recoil)
        {
            var currentFit = fit != null && fit.isActiveAndEnabled && fit.correctShoulderedHands ? fit : null;
            var shoulder = rightUpper.position + (currentFit != null ? root.TransformDirection(currentFit.shoulderOffset) : root.up * 0.025f);
            var forward = (aim - shoulder).normalized;
            var rotation = Quaternion.LookRotation(forward, root.up);
            var animatedAxis = leftHand.position - rightHand.position;
            var handDelta = Quaternion.FromToRotation(animatedAxis, forward);
            var rightRotation = handDelta * rightHand.rotation;
            var leftRotation = handDelta * leftHand.rotation;
            weapon.SetPositionAndRotation(shoulder - rotation * Butt - forward * recoil, rotation);
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(currentFit != null ? currentFit.triggerWristGrip : RightGrip), rightRotation, currentFit != null ? root.TransformDirection(currentFit.rightElbowDirection) : Vector3.zero, currentFit);
            if (currentFit != null)
            {
                leftRotation = SupportRotation(currentFit.supportPalmDirection);
                Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(currentFit.supportWristGrip), leftRotation, root.TransformDirection(currentFit.leftElbowDirection), currentFit, true);
            }
            else Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(LeftGrip), leftRotation, Vector3.zero, null);
            if (currentFit != null)
            {
                if (currentFit.refineSupportGrip) SupportGrip();
                else Grip(leftFingers, leftThumb, leftIndex, leftLittle, false);
                Grip(rightFingers, rightThumb, rightIndex, rightLittle, true);
            }
            else { Curl(leftFingers, leftIndex, leftLittle, false); Curl(rightFingers, rightIndex, rightLittle, true); }

            LookAt(aim);
            if (currentFit != null && head != null)
            {
                // Settle the head onto the stock instead of moving the butt through the harness.
                if (currentFit.shoulderedHeadLean != 0f)
                {
                    var lean = Quaternion.AngleAxis(currentFit.shoulderedHeadLean, head.forward);
                    if (neck != null) neck.rotation = lean * neck.rotation;
                    else head.rotation = lean * head.rotation;
                }
                if (currentFit.shoulderedHeadPitch != 0f)
                    head.rotation *= Quaternion.Euler(currentFit.shoulderedHeadPitch, 0f, 0f);
            }
        }

        // Park's low ready is a separate opt-in. His existing shouldered pose stays on its legacy path.
        public bool TryApplyCarry()
        {
            if (fit == null || !fit.isActiveAndEnabled || !fit.correctCarryPose) return false;
            weapon.SetPositionAndRotation(root.TransformPoint(fit.carryPosition), root.rotation * Quaternion.Euler(fit.carryEuler));
            var rightRotation = Quaternion.LookRotation(weapon.TransformDirection(new Vector3(0f, .35f, 1f)), -weapon.right) * Quaternion.Inverse(rightPalmFrame);
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(new Vector3(.03f, -.065f, -.19f)), rightRotation, root.TransformDirection(new Vector3(.4f, -.4f, -.1f)), fit, true);
            Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(fit.supportWristGrip), SupportRotation(fit.supportPalmDirection), root.TransformDirection(new Vector3(-.3f, -.5f, .05f)), fit, true);
            Grip(leftFingers, leftThumb, leftIndex, leftLittle, false);
            Grip(rightFingers, rightThumb, rightIndex, rightLittle, true);
            return true;
        }

        static Quaternion PalmFrame(Transform hand, Transform index, Transform little, bool left)
        {
            if (hand == null || index == null || little == null) return Quaternion.identity;
            var along = hand.InverseTransformDirection((index.position + little.position) * .5f - hand.position);
            var across = hand.InverseTransformDirection(index.position - little.position);
            return Quaternion.LookRotation(along, left ? Vector3.Cross(across, along) : Vector3.Cross(along, across));
        }

        Quaternion SupportRotation(Vector3 direction)
        {
            var along = weapon.TransformDirection(direction);
            return Quaternion.LookRotation(along, Vector3.Cross(weapon.forward, along)) * Quaternion.Inverse(leftPalmFrame);
        }

        void CacheFingerRest(Transform[] chain, SkeletonBone[] skeleton)
        {
            foreach (var bone in chain)
            {
                if (bone == null) continue;
                var rotation = bone.localRotation;
                foreach (var rest in skeleton) if (rest.name == bone.name) { rotation = rest.rotation; break; }
                fingerRest[bone] = rotation;
            }
        }

        // The support hand cradles the fore-end with a loose grip. The old full-fist curl
        // and distal-thumb target produced hooked fingertips and a thumb/index ring.
        // Keep the source hand's neutral proportions; Park's carry still uses Grip below.
        void SupportGrip()
        {
            if (leftIndex == null || leftLittle == null) return;
            foreach (var chain in leftFingers)
                foreach (var bone in chain) if (bone != null) bone.localRotation = fingerRest[bone];
            foreach (var bone in leftThumb) if (bone != null) bone.localRotation = fingerRest[bone];
            var axis = (leftIndex.position - leftLittle.position).normalized;
            foreach (var chain in leftFingers)
                for (int joint = 0; joint < chain.Length; joint++)
                    if (chain[joint] != null)
                        chain[joint].rotation = Quaternion.AngleAxis(joint == 0 ? 6f : joint == 1 ? 14f : 4f, axis) * chain[joint].rotation;
            if (leftThumb[0] != null && leftThumb[2] != null)
                leftThumb[0].rotation = Quaternion.FromToRotation(
                    leftThumb[2].position - leftThumb[0].position,
                    weapon.TransformDirection(new Vector3(-.05f, 1.3f, 1f))) * leftThumb[0].rotation;
        }

        void Grip(Transform[][] fingers, Transform[] thumb, Transform index, Transform little, bool triggerHand)
        {
            if (index == null || little == null) return;
            // Rebuild from this avatar's neutral joints; never add a second curl to an already gripping clip.
            foreach (var chain in fingers) foreach (var bone in chain) if (bone != null) bone.localRotation = fingerRest[bone];
            foreach (var bone in thumb) if (bone != null) bone.localRotation = fingerRest[bone];
            var axis = (index.position - little.position).normalized;
            for (int finger = 0; finger < fingers.Length; finger++)
            {
                var chain = fingers[finger];
                if (chain[0] == null || chain[1] == null || chain[2] == null) continue;
                var local = weapon.InverseTransformPoint(chain[0].position);
                var inward = weapon.TransformPoint(new Vector3(0f, .015f, local.z)) - chain[0].position;
                float sign = triggerHand ? -1f : 1f;
                bool trigger = triggerHand && finger == 0;
                chain[0].rotation = Quaternion.AngleAxis(sign * (trigger ? 18f : 30f), axis) * chain[0].rotation;
                chain[1].rotation = Quaternion.AngleAxis(sign * (trigger ? 25f : 60f), axis) * chain[1].rotation;
                chain[2].rotation = Quaternion.AngleAxis(sign * (trigger ? 10f : 25f), axis) * chain[2].rotation;
            }
            if (thumb[0] != null && thumb[1] != null && thumb[2] != null)
            {
                var contact = weapon.TransformPoint(new Vector3(-.012f, .015f, triggerHand ? -.16f : .25f));
                var a = Vector3.Distance(thumb[0].position, thumb[1].position); var b = Vector3.Distance(thumb[1].position, thumb[2].position);
                var delta = contact - thumb[0].position; var along = delta.normalized; float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
                float x = (a * a - b * b + d * d) / (2f * d);
                var bend = Vector3.ProjectOnPlane(weapon.up, along).normalized;
                var middle = thumb[0].position + along * x + bend * Mathf.Sqrt(Mathf.Max(0, a * a - x * x));
                thumb[0].rotation = Quaternion.FromToRotation(thumb[1].position - thumb[0].position, middle - thumb[0].position) * thumb[0].rotation;
                thumb[1].rotation = Quaternion.FromToRotation(thumb[2].position - thumb[1].position, contact - thumb[1].position) * thumb[1].rotation;
            }
        }

        public void LookAt(Vector3 aim)
        {
            // These delivered Mixamo rigs use +Z as the face direction. The source crouch
            // clip looks at the ground, so distribute the gaze correction over neck/head.
            if (head != null)
            {
                var direction = (aim - head.position).normalized;
                var correction = Quaternion.FromToRotation(head.forward, direction);
                if (neck != null) neck.rotation = Quaternion.Slerp(Quaternion.identity, correction, 0.3f) * neck.rotation;
                head.rotation = Quaternion.LookRotation(direction, root.up);
            }
        }

        static Transform[][] Fingers(Animator animator, bool left)
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

        void Curl(Transform[][] fingers, Transform index, Transform little, bool triggerHand)
        {
            if (index == null || little == null) return;
            var axis = (index.position - little.position).normalized;
            for (int finger = 0; finger < fingers.Length; finger++)
            {
                // Keep the shooting index at its authored trigger pose.
                if (triggerHand && finger == 0) continue;
                var chain = fingers[finger];
                if (chain[0] == null || chain[1] == null || chain[2] == null) continue;
                var local = weapon.InverseTransformPoint(chain[0].position);
                var inward = weapon.TransformPoint(new Vector3(0f, 0.015f, local.z)) - chain[0].position;
                float sign = Vector3.Dot(Vector3.Cross(axis, chain[1].position - chain[0].position), inward) >= 0f ? 1f : -1f;
                chain[0].rotation = Quaternion.AngleAxis(sign * 20f, axis) * chain[0].rotation;
                chain[1].rotation = Quaternion.AngleAxis(sign * 40f, axis) * chain[1].rotation;
                chain[2].rotation = Quaternion.AngleAxis(sign * 20f, axis) * chain[2].rotation;
            }
        }

        static void Solve(Transform upper, Transform fore, Transform hand, Vector3 target, Quaternion rotation, Vector3 elbowDirection, AlliedRifleFit fit, bool palmContact = false)
        {
            float a = Vector3.Distance(upper.position, fore.position);
            float b = Vector3.Distance(fore.position, hand.position);
            var delta = target - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            var axis = delta.normalized;
            var bend = Vector3.ProjectOnPlane(fit != null ? elbowDirection : fore.position - upper.position, axis).normalized;
            if (bend.sqrMagnitude < 0.01f) bend = Vector3.ProjectOnPlane(Vector3.down, axis).normalized;
            float along = (a * a - b * b + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            var elbow = upper.position + axis * along + bend * height;
            upper.rotation = Quaternion.FromToRotation(fore.position - upper.position, elbow - upper.position) * upper.rotation;
            fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, target - fore.position) * fore.rotation;
            // A world-space wrist snap twists the low-poly sleeve/skin blend into a corkscrew.
            // Put axial roll on the forearm first, leaving only the small palm swing at the wrist.
            var foreAxis = (hand.position - fore.position).normalized;
            var currentUp = Vector3.ProjectOnPlane(hand.up, foreAxis);
            var desiredUp = Vector3.ProjectOnPlane(rotation * Vector3.up, foreAxis);
            if (currentUp.sqrMagnitude > 0.001f && desiredUp.sqrMagnitude > 0.001f)
                {
                float roll = Vector3.SignedAngle(currentUp, desiredUp, foreAxis);
                if (fit != null && !palmContact) roll = Mathf.Clamp(roll, -fit.maxForearmRoll, fit.maxForearmRoll);
                fore.rotation = Quaternion.AngleAxis(roll, foreAxis) * fore.rotation;
                }
            hand.rotation = fit != null && !palmContact ? Quaternion.RotateTowards(hand.rotation, rotation, fit.maxWristSwing) : rotation;
        }
    }
}
