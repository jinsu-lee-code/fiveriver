using UnityEngine;

namespace FiveRivers.Combat
{
    // World-character constraints: keep the stock shouldered and solve both arms without
    // moving/scaling the skeleton or replacing the existing skinned adult character.
    public sealed class AllyRiflePose
    {
        readonly Transform rightUpper, rightFore, rightHand, leftUpper, leftFore, leftHand, head, neck, hips, spine, chest;
        readonly Transform root, weapon;
        readonly AlliedRifleFit fit;
        readonly Transform[][] leftFingers, rightFingers;
        readonly Transform leftIndex, leftLittle, rightIndex, rightLittle;
        readonly Quaternion leftPalmFrame, rightPalmFrame;
        readonly Quaternion rightReadyUpperFrame, rightReadyForeRest, rightReadyHandRest;
        readonly bool rightReadyElbowFrameValid;
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
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
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
            // Park (carry profile) needs the same native-hinge elbow rebuild when he shoulders the rifle to fire.
            if (fit != null && (fit.correctStandingReady || fit.correctCarryPose) && rightUpper != null && rightFore != null && rightHand != null
                && rightFore.parent == rightUpper && rightHand.parent == rightFore)
            {
                rightReadyForeRest = SkeletonRotation(rightFore, skeleton);
                rightReadyHandRest = SkeletonRotation(rightHand, skeleton);
                var nativeUpper = rightFore.localPosition.normalized;
                var nativeFore = rightReadyForeRest * rightHand.localPosition.normalized;
                var nativeHinge = Vector3.Cross(nativeUpper, nativeFore);
                if (nativeUpper.sqrMagnitude > .001f && nativeFore.sqrMagnitude > .001f && nativeHinge.sqrMagnitude > .0001f)
                {
                    rightReadyUpperFrame = Quaternion.LookRotation(nativeUpper, nativeHinge.normalized);
                    rightReadyElbowFrameValid = true;
                }
            }
            foreach (var chain in leftFingers) CacheFingerRest(chain, skeleton);
            foreach (var chain in rightFingers) CacheFingerRest(chain, skeleton);
            CacheFingerRest(leftThumb, skeleton); CacheFingerRest(rightThumb, skeleton);
        }

        public void Apply(Vector3 aim, float recoil)
        {
            // Park (carry profile) shoulders the rifle with the same contacts as the others: his delivered fire-arm
            // pose held the rifle at chest height once the crouch clip was straightened (2026-11-09).
            var currentFit = fit != null && fit.isActiveAndEnabled && (fit.correctShoulderedHands || fit.correctCarryPose) ? fit : null;
            var shoulder = rightUpper.position + (currentFit != null ? root.TransformDirection(currentFit.shoulderOffset) : root.up * 0.025f);
            var forward = (aim - shoulder).normalized;
            var rotation = Quaternion.LookRotation(forward, root.up);
            var animatedAxis = leftHand.position - rightHand.position;
            var handDelta = Quaternion.FromToRotation(animatedAxis, forward);
            var rightRotation = handDelta * rightHand.rotation;
            var leftRotation = handDelta * leftHand.rotation;
            weapon.SetPositionAndRotation(shoulder - rotation * Butt - forward * recoil, rotation);
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(currentFit != null ? currentFit.triggerWristGrip : RightGrip), rightRotation, currentFit != null ? root.TransformDirection(currentFit.rightElbowDirection) : Vector3.zero, currentFit);
            // His rig keeps a large upper-arm/forearm roll from the solve; rebuild the native elbow hinge as for the ready actor.
            if (currentFit != null && currentFit.correctCarryPose && !currentFit.correctShoulderedHands) AlignStandingRightElbow(rightRotation);
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

        // Standing carry: the ready pose for the opt-in actor, Park's carry profile otherwise.
        public bool TryApplyCarry()
        {
            if (fit == null || !fit.isActiveAndEnabled) return false;
            if (fit.correctStandingReady)
            {
                ApplyStandingReady();
                return true;
            }
            if (!fit.correctCarryPose) return false;
            ApplyCarryContacts();
            return true;
        }

        // Ridge assault and the dawn on the crest: every rifleman walks and waits at port arms with Park's carry
        // contacts. Otherwise the plain soldiers' rifles hang between the hands of an unarmed walk/idle clip.
        // advance, run: how much of the crouched advance (CrouchRun_Rifle) and of the rifle run (Run_Rifle) is
        // playing; both 0 while he stands, which keeps the level port arms above.
        // Moving with that standing pose looked wrong: the rifle stayed frozen in front of the root like a tray
        // while the body bobbed and bent under it, and the face looked at the ground. While he moves the rifle is
        // carried on the chest so it moves with the body, and he looks where he is going. The run also squares
        // the chest to the slope and leans into it.
        public bool ApplyPortArms(float advance = 0f, float run = 0f)
        {
            if (fit == null || !fit.isActiveAndEnabled) return false;
            run = Mathf.Clamp01(run);
            advance = Mathf.Clamp(advance, 0f, 1f - run);
            if (run > 0f) LeanIntoRun(run);
            ApplyCarryContacts(advance, run);
            if (advance + run > 0f) FaceAhead(advance, run);
            return true;
        }

        // Moving poses, tuned on Ally_1 from side/front/three-quarter film strips (2026-11-10).
        // Static (like HillAllies' speeds) so they can be adjusted while playing.
        public static float RunLean = 8f;        // extra forward lean of the chest, degrees
        public static float RunSquare = .5f;     // share of the clip's sideways chest turn that is taken out
        public static float RunFollow = .6f;     // how far the rifle follows the chest (twist and lean) instead of the root
        public static Vector3 RunOffset = new Vector3(.02f, -.25f, .15f); // rifle from the mid-shoulder point, metres
        // High port: muzzle up past the left shoulder. The roll keeps the support forearm untwisted
        // (at 0 and below its sleeve corkscrewed into a dark band).
        public static Vector3 RunEuler = new Vector3(-38f, -68f, 25f);
        // Crouched advance: low ready, the rifle in front of the belt pointing ahead and a little left, level
        // once the bent chest has tipped it (a wider yaw carried it sideways like a bar).
        public static float AdvanceFollow = .6f;
        public static Vector3 AdvanceOffset = new Vector3(.02f, -.27f, .17f);
        public static Vector3 AdvanceEuler = new Vector3(-14f, -30f, 20f);
        public static float AdvanceLook = -.16f, RunLook = -.08f; // how far below level he looks
        static readonly Vector3 CarryRightElbow = new Vector3(.4f, -.4f, -.1f), CarryLeftElbow = new Vector3(-.3f, -.5f, .05f);
        // Moving: both elbows down and in, close to how the arms hang. With flatter hints Park's longer arms
        // winged the trigger elbow out and pushed the bare upper arm through his rolled sleeve.
        public static Vector3 RunRightElbow = new Vector3(.15f, -.9f, -.2f), RunLeftElbow = new Vector3(-.3f, -.8f, .15f);

        void LeanIntoRun(float weight)
        {
            if (spine == null) return;
            var across = Vector3.ProjectOnPlane(rightUpper.position - leftUpper.position, root.up);
            float turn = across.sqrMagnitude > .0001f ? Vector3.SignedAngle(across, root.right, root.up) * RunSquare : 0f;
            // half at the waist and half at the chest, so the correction is a curve and not a hinge
            float share = chest != null ? .5f : 1f;
            var delta = Quaternion.AngleAxis(turn * share * weight, root.up) * Quaternion.AngleAxis(RunLean * share * weight, root.right);
            spine.rotation = delta * spine.rotation;
            if (chest != null) chest.rotation = delta * chest.rotation;
        }

        void FaceAhead(float advance, float run)
        {
            if (head == null) return;
            float weight = advance + run;
            var direction = (root.forward + root.up * ((AdvanceLook * advance + RunLook * run) / weight)).normalized;
            if (neck != null)
                neck.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(head.forward, direction), .3f * weight) * neck.rotation;
            // not the full turn: a little of the clip's head bob stays
            head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(direction, root.up), .85f * weight);
        }

        void ApplyCarryContacts(float advance = 0f, float run = 0f)
        {
            var position = root.TransformPoint(fit.carryPosition);
            var rotation = root.rotation * Quaternion.Euler(fit.carryEuler);
            float moving = advance + run;
            if (moving > 0f && hips != null)
            {
                // The chest frame: across the shoulders, up along the spine. The rifle is placed in a frame part
                // of the way from the root to it, so it rides the body's bob, bend and twist without whipping.
                float gait = run / moving; // 0 = advance, 1 = run
                var mid = (rightUpper.position + leftUpper.position) * .5f;
                var right = (rightUpper.position - leftUpper.position).normalized;
                var up = Vector3.ProjectOnPlane(mid - hips.position, right).normalized;
                var torso = Quaternion.Slerp(root.rotation, Quaternion.LookRotation(Vector3.Cross(right, up), up), Mathf.Lerp(AdvanceFollow, RunFollow, gait));
                var movingRotation = torso * Quaternion.Slerp(Quaternion.Euler(AdvanceEuler), Quaternion.Euler(RunEuler), gait);
                position = Vector3.Lerp(position, mid + torso * Vector3.Lerp(AdvanceOffset, RunOffset, gait), moving);
                rotation = Quaternion.Slerp(rotation, movingRotation, moving);
            }
            weapon.SetPositionAndRotation(position, rotation);
            var rightRotation = Quaternion.LookRotation(weapon.TransformDirection(new Vector3(0f, .35f, 1f)), -weapon.right) * Quaternion.Inverse(rightPalmFrame);
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(new Vector3(.03f, -.065f, -.19f)), rightRotation, root.TransformDirection(Vector3.Lerp(CarryRightElbow, RunRightElbow, moving)), fit, true);
            Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(fit.supportWristGrip), SupportRotation(fit.supportPalmDirection), root.TransformDirection(Vector3.Lerp(CarryLeftElbow, RunLeftElbow, moving)), fit, true);
            Grip(leftFingers, leftThumb, leftIndex, leftLittle, false);
            Grip(rightFingers, rightThumb, rightIndex, rightLittle, true);
        }

        void ApplyStandingReady()
        {
            // Only the opt-in standing actor uses these contacts. The source grip placed
            // his MCPs 8 cm ahead of the stock wrist, crushing the palm under the receiver.
            var shoulder = rightUpper.position + root.TransformDirection(new Vector3(.025f, -.02f, .025f));
            var rotation = root.rotation * Quaternion.Euler(30f, -10f, 0f);
            weapon.SetPositionAndRotation(shoulder - rotation * Butt, rotation);
            var rightRotation = Quaternion.LookRotation(weapon.TransformDirection(new Vector3(0f, .15f, 1f)), -weapon.right)
                * Quaternion.Inverse(rightPalmFrame);
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(new Vector3(.035f, -.035f, -.22f)), rightRotation,
                root.TransformDirection(fit.rightElbowDirection), fit, true);
            AlignStandingRightElbow(rightRotation);
            Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(new Vector3(-.055f, -.05f, .14f)),
                SupportRotation(new Vector3(1f, .12f, .05f)), root.TransformDirection(fit.leftElbowDirection), fit, true);
            ReadyFingers(leftFingers, leftIndex, leftLittle, false);
            ReadyFingers(rightFingers, rightIndex, rightLittle, true);
            ReadyThumb(leftThumb, new Vector3(-.025f, .015f, .178f), 30f);
            ReadyThumb(rightThumb, Vector3.zero, 0f);
            if (head != null) LookAt(head.position + root.forward * 60f);
        }

        void AlignStandingRightElbow(Quaternion wristRotation)
        {
            // Position-only IK retained an authored upper-arm twist of about 102 degrees,
            // then rolled the forearm from that twisted frame. The mixed elbow vertices
            // folded into a dark strip. Keep every contact and rebuild the native hinge.
            if (!rightReadyElbowFrameValid) return;
            var elbow = rightFore.position;
            var wrist = rightHand.position;
            var upperAxis = (elbow - rightUpper.position).normalized;
            var foreAxis = (wrist - elbow).normalized;
            var hinge = Vector3.Cross(upperAxis, foreAxis);
            if (hinge.sqrMagnitude < .0001f) return;
            var nativeUpper = Quaternion.LookRotation(upperAxis, hinge.normalized) * Quaternion.Inverse(rightReadyUpperFrame);
            // Full bind alignment removes the collapse but overinflates the sleeve silhouette.
            // Retain part of the authored cloth orientation while fixing the elbow's axial twist.
            rightUpper.rotation = Quaternion.Slerp(rightUpper.rotation, nativeUpper, .7f);
            rightFore.rotation = rightUpper.rotation * rightReadyForeRest;
            rightFore.rotation = Quaternion.FromToRotation(rightHand.position - rightFore.position,
                wrist - rightFore.position) * rightFore.rotation;
            rightHand.localRotation = rightReadyHandRest;
            // Pronation starts at the mesh's native wrist frame, not the previous
            // animation's rolled palm. Keep the final palm/stock contact exactly fixed.
            var axis = (rightHand.position - rightFore.position).normalized;
            var currentUp = Vector3.ProjectOnPlane(rightHand.up, axis);
            var desiredUp = Vector3.ProjectOnPlane(wristRotation * Vector3.up, axis);
            if (currentUp.sqrMagnitude > .001f && desiredUp.sqrMagnitude > .001f)
                rightFore.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(currentUp, desiredUp, axis), axis) * rightFore.rotation;
            rightHand.rotation = wristRotation;
        }

        static Quaternion SkeletonRotation(Transform bone, SkeletonBone[] skeleton)
        {
            foreach (var rest in skeleton) if (rest.name == bone.name) return rest.rotation;
            return bone.localRotation;
        }
        void ReadyFingers(Transform[][] fingers, Transform index, Transform little, bool triggerHand)
        {
            if (index == null || little == null) return;
            foreach (var chain in fingers)
                foreach (var joint in chain) if (joint != null) joint.localRotation = fingerRest[joint];
            var axis = (index.position - little.position).normalized;
            for (int finger = 0; finger < fingers.Length; finger++)
            {
                var chain = fingers[finger];
                if (chain[0] == null || chain[1] == null || chain[2] == null) continue;
                var p = weapon.InverseTransformPoint(chain[0].position);
                var inward = weapon.TransformPoint(new Vector3(0f, triggerHand ? -.012f : .015f, p.z)) - chain[0].position;
                // Choose flexion towards the wood from this hand's actual anatomy.
                float sign = Vector3.Dot(Vector3.Cross(axis, chain[1].position - chain[0].position), inward) >= 0f ? 1f : -1f;
                bool indexFinger = triggerHand && finger == 0;
                if (indexFinger)
                {
                    // At low ready the index lies along the stock outside the guard.
                    // The source rest curl otherwise sends its fingertip through the wood.
                    for (int joint = 0; joint < chain.Length; joint++)
                    {
                        var child = joint < 2 ? chain[joint + 1] : chain[joint].childCount > 0 ? chain[joint].GetChild(0) : null;
                        if (child == null) continue;
                        var along = weapon.TransformDirection(new Vector3(0f, -.08f * joint, 1f));
                        chain[joint].rotation = Quaternion.FromToRotation(child.position - chain[joint].position, along) * chain[joint].rotation;
                    }
                    continue;
                }
                float proximal = indexFinger ? 0f : triggerHand ? 16f + finger * 2f : 10f + finger * 2f;
                float middle = indexFinger ? 8f : triggerHand ? 33f + finger * 2f : 32f + finger * 2f;
                float distal = indexFinger ? 8f : triggerHand ? 13f + finger * 2f : 15f + finger * 2f;
                chain[0].rotation = Quaternion.AngleAxis(sign * proximal, axis) * chain[0].rotation;
                chain[1].rotation = Quaternion.AngleAxis(sign * middle, axis) * chain[1].rotation;
                chain[2].rotation = Quaternion.AngleAxis(sign * distal, axis) * chain[2].rotation;
            }
        }

        void ReadyThumb(Transform[] thumb, Vector3 contact, float maxSwing)
        {
            foreach (var joint in thumb) if (joint != null) joint.localRotation = fingerRest[joint];
            if (thumb[0] == null || thumb[2] == null || maxSwing <= 0f) return;
            // Rest the thumb across the stock; do not pull its distal joint into the
            // same trigger contact as the index or fold both thumb segments by IK.
            var wanted = Quaternion.FromToRotation(thumb[2].position - thumb[0].position,
                weapon.TransformPoint(contact) - thumb[0].position) * thumb[0].rotation;
            thumb[0].rotation = Quaternion.RotateTowards(thumb[0].rotation, wanted, maxSwing);
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
