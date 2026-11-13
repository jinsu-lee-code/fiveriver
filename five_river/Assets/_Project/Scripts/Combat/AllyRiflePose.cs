using UnityEngine;

namespace FiveRivers.Combat
{
    // World-character constraints: keep the stock shouldered and solve both arms without
    // moving/scaling the skeleton or replacing the existing skinned adult character.
    public sealed class AllyRiflePose
    {
        readonly Transform rightUpper, rightFore, rightHand, leftUpper, leftFore, leftHand, head, neck;
        readonly Transform root, weapon, rightClavicle, leftClavicle;
        readonly AlliedRifleFit fit;
        readonly NpcRifleGrip grip;
        readonly Transform[][] leftFingers, rightFingers;
        readonly Transform leftIndex, leftLittle, rightIndex, rightLittle;
        readonly Quaternion leftPalmFrame, rightPalmFrame;
        readonly Quaternion rightReadyUpperFrame, rightReadyForeRest, rightReadyHandRest;
        readonly bool isPark, rightReadyElbowFrameValid;
        readonly Quaternion parkNeckRest, parkHeadRest, parkFaceInHead;
        readonly Quaternion? parkSupportHandRest;
        readonly Transform[] leftThumb, rightThumb;
        readonly System.Collections.Generic.Dictionary<Transform, Quaternion> fingerRest = new System.Collections.Generic.Dictionary<Transform, Quaternion>();
        static readonly Vector3 Butt = new Vector3(0f, -0.02f, -0.375f);
        static readonly Vector3 RightGrip = new Vector3(0f, -0.035f, -0.16f);
        static readonly Vector3 LeftGrip = new Vector3(0f, -0.015f, 0.22f);
        static readonly Vector3 ParkSupportThumb = new Vector3(-.020f, .010f, .235f);

        public AllyRiflePose(Animator animator, Transform actor, Transform rifle)
        {
            root = actor; weapon = rifle;
            fit = animator.GetComponent<AlliedRifleFit>();
            grip = rifle != null ? rifle.GetComponent<NpcRifleGrip>() : null;
            isPark = AlliedRifleFit.IsParkAvatar(animator);
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
            if (isPark)
            {
                parkSupportHandRest = ParkBindRotation(leftFore, leftHand);
                rightClavicle = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
                leftClavicle = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                if (neck != null && head != null)
                {
                    parkNeckRest = SkeletonRotation(neck, skeleton);
                    parkHeadRest = SkeletonRotation(head, skeleton);
                    parkFaceInHead = ParkFaceFromBindPose(animator, head);
                }
            }
            // Park (carry profile) needs the same native-hinge elbow rebuild when he shoulders the rifle to fire.
            if (fit != null && (fit.correctStandingReady || fit.correctCarryPose) && rightUpper != null && rightFore != null && rightHand != null
                && rightFore.parent == rightUpper && rightHand.parent == rightFore)
            {
                var foreRest = SkeletonRotation(rightFore, skeleton);
                var handRest = SkeletonRotation(rightHand, skeleton);
                if (isPark)
                {
                    foreRest = ParkBindRotation(rightUpper, rightFore) ?? foreRest;
                    handRest = ParkBindRotation(rightFore, rightHand) ?? handRest;
                }
                rightReadyForeRest = foreRest;
                rightReadyHandRest = handRest;
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
            var currentFit = fit != null && fit.isActiveAndEnabled && (fit.correctShoulderedHands || fit.correctCarryPose) ? fit : null;
            bool alignRightElbow = currentFit != null && currentFit.correctCarryPose && !currentFit.correctShoulderedHands;
            if (isPark && alignRightElbow)
            {
                LevelParkShoulders();
                var bodyRight = ParkShoulderLine();
                var bodyForward = Vector3.Cross(bodyRight, root.up);
                // 견착점은 상완 관절의 앞쪽이자 가슴 쪽에 있다.
                var pocket = rightUpper.position + bodyForward * .130f - bodyRight * .045f + root.up * .043f;
                var sight = (aim - pocket).normalized;
                var rifleRotation = Quaternion.LookRotation(sight, root.up);
                weapon.SetPositionAndRotation(pocket - rifleRotation * Butt - sight * recoil, rifleRotation);
                ApplyParkContacts();
                LookAt(aim);
                return;
            }
            LookAt(aim);
            if (currentFit != null && head != null)
            {
                if (currentFit.shoulderedHeadLean != 0f)
                {
                    var lean = Quaternion.AngleAxis(currentFit.shoulderedHeadLean, head.forward);
                    if (neck != null) neck.rotation = lean * neck.rotation;
                    else head.rotation = lean * head.rotation;
                }
                if (currentFit.shoulderedHeadPitch != 0f)
                    head.rotation *= Quaternion.Euler(currentFit.shoulderedHeadPitch, 0f, 0f);
            }

            bool refineGrip = currentFit != null && currentFit.refineSupportGrip;
            var shoulder = rightUpper.position + (currentFit != null ? root.TransformDirection(currentFit.shoulderOffset) : root.up * 0.025f);
            var forward = (aim - shoulder).normalized;
            var rotation = Quaternion.LookRotation(forward, root.up);
            if (refineGrip && head != null)
            {
                // 턱이 개머리판에 가까워지면 견착점을 오른쪽 앞과 아래로 옮긴다.
                var headInRifle = Quaternion.Inverse(rotation) * (head.position - shoulder) + Butt;
                float headLimit = -.069f + .70f * Mathf.Clamp(headInRifle.y - .040f, -.050f, .018f);
                float clearance = Mathf.Max(0f, headInRifle.x - headLimit);
                shoulder += rotation * new Vector3(.78f, -.25f, .40f) * clearance;
                forward = (aim - shoulder).normalized;
                rotation = Quaternion.LookRotation(forward, root.up);
            }
            var animatedAxis = leftHand.position - rightHand.position;
            var handDelta = Quaternion.FromToRotation(animatedAxis, forward);
            var rightRotation = handDelta * rightHand.rotation;
            var leftRotation = handDelta * leftHand.rotation;
            weapon.SetPositionAndRotation(shoulder - rotation * Butt - forward * recoil, rotation);
            var rightContact = currentFit != null ? currentFit.ShoulderedTriggerWristGrip : RightGrip;
            if (refineGrip)
            {
                // M1 개머리판의 목을 잡을 때는 손바닥을 방아쇠울 뒤쪽의 목재 옆면에 댄다.
                rightRotation = Quaternion.LookRotation(weapon.TransformDirection(new Vector3(0f, .28f, 1f)),
                    weapon.TransformDirection(new Vector3(-1f, -.12f, 0f))) * Quaternion.Inverse(rightPalmFrame);
            }
            var rightTarget = weapon.TransformPoint(rightContact);
            var rightPole = currentFit != null ? root.TransformDirection(currentFit.rightElbowDirection) : Vector3.zero;
            Solve(rightUpper, rightFore, rightHand, rightTarget, rightRotation, rightPole, currentFit,
                alignRightElbow: alignRightElbow, lockWristRotation: refineGrip);
            if (currentFit != null)
            {
                leftRotation = SupportRotation(currentFit.supportPalmDirection);
                var leftTarget = weapon.TransformPoint(currentFit.supportWristGrip);
                if (refineGrip)
                {
                    float reach = Vector3.Distance(leftUpper.position, leftFore.position)
                        + Vector3.Distance(leftFore.position, leftHand.position) - .001f;
                    var toGrip = leftTarget - leftUpper.position;
                    if (toGrip.sqrMagnitude > reach * reach)
                    {
                        // 견착으로 전환할 때 팔이 닿는 범위 안에서 앞쪽 목재를 받친다.
                        float along = Vector3.Dot(toGrip, forward);
                        float acrossSquared = (toGrip - forward * along).sqrMagnitude;
                        float reachableAlong = Mathf.Sqrt(Mathf.Max(0f, reach * reach - acrossSquared));
                        leftTarget -= forward * Mathf.Clamp(along - reachableAlong, 0f, .10f);
                    }
                }
                Solve(leftUpper, leftFore, leftHand, leftTarget, leftRotation, root.TransformDirection(currentFit.leftElbowDirection), currentFit, true);
            }
            else Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(LeftGrip), leftRotation, Vector3.zero, null);
            if (currentFit != null)
            {
                if (refineGrip) SupportGrip();
                else Grip(leftFingers, leftThumb, leftIndex, leftLittle, false);
                Grip(rightFingers, rightThumb, rightIndex, rightLittle, true, wrapRegularGrip: refineGrip);
            }
            else { Curl(leftFingers, leftIndex, leftLittle, false); Curl(rightFingers, rightIndex, rightLittle, true); }
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
            ShareParkStandingWristTwist();
            return true;
        }

        // Ridge assault and the dawn on the crest: every rifleman walks and waits at port arms with Park's carry
        // contacts. Otherwise the plain soldiers' rifles hang between the hands of an unarmed walk/idle clip.
        public bool ApplyPortArms()
        {
            if (fit == null || !fit.isActiveAndEnabled) return false;
            ApplyCarryContacts();
            return true;
        }

        public void ApplyClipCarryGrip()
        {
            if (grip == null) return;
            grip.ApplyToAnimatedArm(true, rightUpper, rightFore, rightHand);
            grip.ApplyToAnimatedArm(false, leftUpper, leftFore, leftHand);
        }

        void ApplyCarryContacts()
        {
            if (isPark)
            {
                LevelParkShoulders();
                var bodyRight = ParkShoulderLine();
                var bodyForward = Vector3.Cross(bodyRight, root.up);
                // 몸통 앞의 대각선 자세에서 개머리판과 오른팔 사이의 공간을 유지한다.
                var rotation = Quaternion.LookRotation(bodyForward, root.up) * Quaternion.Euler(-29f, -60f, 0f);
                var carriedButt = rightUpper.position + bodyForward * .115f - bodyRight * .055f - root.up * .170f;
                weapon.SetPositionAndRotation(carriedButt - rotation * Butt, rotation);
                ApplyParkContacts(carrying: true);
                return;
            }
            weapon.SetPositionAndRotation(root.TransformPoint(fit.carryPosition), root.rotation * Quaternion.Euler(fit.carryEuler));
            var rightRotation = Quaternion.LookRotation(weapon.TransformDirection(new Vector3(0f, .35f, 1f)), -weapon.right) * Quaternion.Inverse(rightPalmFrame);
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(new Vector3(.03f, -.065f, -.19f)), rightRotation, root.TransformDirection(new Vector3(.4f, -.4f, -.1f)), fit, true);
            Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(fit.supportWristGrip), SupportRotation(fit.supportPalmDirection), root.TransformDirection(new Vector3(-.3f, -.5f, .05f)), fit, true);
            Grip(leftFingers, leftThumb, leftIndex, leftLittle, false);
            Grip(rightFingers, rightThumb, rightIndex, rightLittle, true);
        }

        void ApplyParkContacts(bool carrying = false)
        {
            var palmAlong = carrying ? new Vector3(-.09f, -.055f, 1f) : new Vector3(0f, .16f, 1f);
            var rightRotation = Quaternion.LookRotation(weapon.TransformDirection(palmAlong),
                weapon.TransformDirection(new Vector3(-1f, -.16f, 0f))) * Quaternion.Inverse(rightPalmFrame);
            var rightPole = weapon.TransformDirection(carrying ? new Vector3(1f, -.9f, -.16f) : new Vector3(.85f, -.58f, -.16f));
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(AlliedRifleFit.ParkRightGrip), rightRotation,
                rightPole, fit, true, alignRightElbow: true);

            var leftRotation = Quaternion.AngleAxis(-10f, weapon.forward) * SupportRotation(fit.supportPalmDirection);
            var leftContact = carrying ? AlliedRifleFit.ParkLeftGrip : AlliedRifleFit.ParkAimedLeftGrip;
            Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(leftContact), leftRotation,
                root.TransformDirection(fit.leftElbowDirection), fit, true);
            var thumbContact = ParkSupportThumb + Vector3.forward * (leftContact.z - AlliedRifleFit.ParkLeftGrip.z);
            Grip(leftFingers, leftThumb, leftIndex, leftLittle, false, thumbTarget: thumbContact);
            Grip(rightFingers, rightThumb, rightIndex, rightLittle, true, refineTrigger: true);
        }

        void ShareParkStandingWristTwist()
        {
            if (!parkSupportHandRest.HasValue) return;
            // 총을 받치는 손은 고정하고 손목에 몰린 축 회전만 팔뚝에 나눈다.
            var contact = leftHand.rotation;
            var axis = (leftHand.position - leftFore.position).normalized;
            var delta = contact * Quaternion.Inverse(leftFore.rotation * parkSupportHandRest.Value);
            float roll = Mathf.Clamp(TwistAngle(delta, axis) * .5f, -45f, 45f);
            var localAxis = Vector3.Scale(leftHand.localPosition, leftFore.localScale).normalized;
            leftFore.localRotation *= Quaternion.AngleAxis(roll, localAxis);
            leftHand.rotation = contact;
        }

        Vector3 ParkShoulderLine()
        {
            var line = Vector3.ProjectOnPlane(rightUpper.position - leftUpper.position, root.up);
            return line.sqrMagnitude > .001f ? line.normalized : root.right;
        }

        void LevelParkShoulders()
        {
            if (rightClavicle == null || leftClavicle == null || rightUpper == null || leftUpper == null) return;
            var up = root.up;
            float mean = Vector3.Dot((rightUpper.position + leftUpper.position) * .5f, up);
            LevelClavicle(rightClavicle, rightUpper, up, mean);
            LevelClavicle(leftClavicle, leftUpper, up, mean);
        }

        static void LevelClavicle(Transform clavicle, Transform upper, Vector3 up, float height)
        {
            var offset = upper.position - clavicle.position;
            float length = offset.magnitude;
            if (length < .001f) return;
            var level = Vector3.ProjectOnPlane(offset, up).normalized;
            float wantedY = Mathf.Clamp(height - Vector3.Dot(clavicle.position, up), -length * .85f, length * .85f);
            var wanted = level * Mathf.Sqrt(length * length - wantedY * wantedY) + up * wantedY;
            clavicle.rotation = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(offset, wanted), 20f) * clavicle.rotation;
        }

        void ApplyStandingReady()
        {
            // Only the opt-in standing actor uses these contacts. The source grip placed
            // his MCPs 8 cm ahead of the stock wrist, crushing the palm under the receiver.
            var shoulder = rightUpper.position + root.TransformDirection(new Vector3(.025f, -.02f, .025f) + fit.standingReadyOffset);
            var rotation = root.rotation * Quaternion.Euler(30f, -10f, 0f);
            weapon.SetPositionAndRotation(shoulder - rotation * Butt, rotation);
            var rightRotation = Quaternion.LookRotation(weapon.TransformDirection(new Vector3(0f, .15f, 1f)), -weapon.right)
                * Quaternion.Inverse(rightPalmFrame);
            Solve(rightUpper, rightFore, rightHand, weapon.TransformPoint(new Vector3(.035f, -.035f, -.22f)), rightRotation,
                root.TransformDirection(fit.rightElbowDirection), fit, true, alignRightElbow: true);
            Solve(leftUpper, leftFore, leftHand, weapon.TransformPoint(fit.standingReadySupportGrip),
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
            rightUpper.rotation = Quaternion.Slerp(rightUpper.rotation, nativeUpper, isPark ? .9f : .7f);
            rightFore.rotation = rightUpper.rotation * rightReadyForeRest;
            rightFore.rotation = Quaternion.FromToRotation(rightHand.position - rightFore.position,
                wrist - rightFore.position) * rightFore.rotation;
            rightHand.localRotation = rightReadyHandRest;
            // Pronation starts at the mesh's native wrist frame, not the previous
            // animation's rolled palm. Keep the final palm/stock contact exactly fixed.
            var axis = (rightHand.position - rightFore.position).normalized;
            if (isPark)
            {
                // 손목의 실제 기준 자세에서 팔뚝 축 회전만 분리해 피부가 반 바퀴 꼬이지 않게 한다.
                var delta = wristRotation * Quaternion.Inverse(rightHand.rotation);
                rightFore.rotation = Quaternion.AngleAxis(TwistAngle(delta, axis), axis) * rightFore.rotation;
            }
            else
            {
                var currentUp = Vector3.ProjectOnPlane(rightHand.up, axis);
                var desiredUp = Vector3.ProjectOnPlane(wristRotation * Vector3.up, axis);
                if (currentUp.sqrMagnitude > .001f && desiredUp.sqrMagnitude > .001f)
                    rightFore.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(currentUp, desiredUp, axis), axis) * rightFore.rotation;
            }
            rightHand.rotation = wristRotation;
        }

        static float TwistAngle(Quaternion delta, Vector3 axis)
        {
            float axial = Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), axis);
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(axial, delta.w) * Mathf.Rad2Deg);
        }

        Quaternion? ParkBindRotation(Transform parent, Transform bone)
        {
            if (parent == null || bone == null || bone.parent != parent) return null;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != "Body_Low" || renderer.sharedMesh == null) continue;
                var bones = renderer.bones;
                int parentIndex = System.Array.IndexOf(bones, parent);
                int boneIndex = System.Array.IndexOf(bones, bone);
                var bind = renderer.sharedMesh.bindposes;
                if (parentIndex < 0 || boneIndex < 0 || bind.Length != bones.Length) continue;
                return (bind[parentIndex] * bind[boneIndex].inverse).rotation;
            }
            return null;
        }

        static Quaternion ParkFaceFromBindPose(Animator animator, Transform headBone)
        {
            foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name != "Body_Low" || skin.sharedMesh == null) continue;
                int index = System.Array.IndexOf(skin.bones, headBone);
                var bind = skin.sharedMesh.bindposes;
                if (index < 0 || index >= bind.Length) continue;
                // 박용팔의 얼굴은 모델 정면을 향해도 머리뼈의 로컬 축은 기울어져 있다.
                var headInModel = animator.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix * bind[index].inverse;
                return Quaternion.Inverse(headInModel.rotation);
            }
            return Quaternion.identity;
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
            var axis = (leftIndex.position - leftLittle.position).normalized;
            foreach (var chain in leftFingers)
                for (int joint = 0; joint < chain.Length; joint++)
                    if (chain[joint] != null)
                        chain[joint].rotation = Quaternion.AngleAxis(joint == 0 ? 7f : joint == 1 ? 27f : 12f, axis) * chain[joint].rotation;
            // 총구 쪽으로 엄지를 벌리는 대신 검지 옆의 목재 측면에 가깝게 둔다.
            var indexDistal = leftFingers[0][2];
            float indexZ = weapon.InverseTransformPoint(indexDistal != null ? indexDistal.position : leftIndex.position).z;
            ReadyThumb(leftThumb, new Vector3(-.025f, .012f, indexZ + .012f), 36f);
        }

        void Grip(Transform[][] fingers, Transform[] thumb, Transform index, Transform little, bool triggerHand,
            bool refineTrigger = false, bool wrapRegularGrip = false, Vector3? thumbTarget = null)
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
                float sign = triggerHand ? -1f : 1f;
                bool trigger = triggerHand && finger == 0;
                float proximal = trigger ? (refineTrigger ? 2f : 18f) : (refineTrigger ? 32f : 30f);
                float middle = trigger ? (refineTrigger ? 10f : 25f) : (refineTrigger ? 54f : 60f);
                float distal = trigger ? (refineTrigger ? 0f : 10f) : (refineTrigger ? 14f : 25f);
                var flex = wrapRegularGrip ? finger switch
                {
                    0 => new Vector3(3f, 7f, 3f),
                    1 => new Vector3(32f, 54f, 14f),
                    2 => new Vector3(37f, 58f, 15f),
                    _ => new Vector3(49f, 60f, 15f),
                } : new Vector3(proximal, middle, distal);
                chain[0].rotation = Quaternion.AngleAxis(sign * flex.x, axis) * chain[0].rotation;
                chain[1].rotation = Quaternion.AngleAxis(sign * flex.y, axis) * chain[1].rotation;
                chain[2].rotation = Quaternion.AngleAxis(sign * flex.z, axis) * chain[2].rotation;
                if (!wrapRegularGrip) continue;

                // 검지는 방아쇠울 안으로 향하고 나머지 손가락은 목재의 좁아지는 아래쪽을 감싼다.
                if (trigger)
                {
                    var acrossPalm = Vector3.Cross(axis, chain[1].position - chain[0].position);
                    chain[0].rotation = Quaternion.AngleAxis(13f, acrossPalm) * chain[0].rotation;
                }
                else if (finger == 1 || finger == 3)
                {
                    chain[0].rotation = Quaternion.AngleAxis(finger == 1 ? 7f : -28f, weapon.forward) * chain[0].rotation;
                }
            }
            if (wrapRegularGrip)
            {
                // 엄지 끝마디가 개머리판의 목 윗면을 가로지르도록 눕힌다.
                SetThumbContact(thumb, weapon.TransformPoint(new Vector3(.010f, .026f, -.154f)), weapon.right);
                var distal = thumb[2];
                if (distal != null && distal.childCount > 0)
                {
                    var tip = distal.GetChild(0).position - distal.position;
                    var acrossStock = weapon.TransformDirection(new Vector3(-1f, .12f, .10f));
                    var target = Quaternion.FromToRotation(tip, acrossStock) * distal.rotation;
                    distal.rotation = Quaternion.RotateTowards(distal.rotation, target, 55f);
                }
                return;
            }
            var thumbContact = refineTrigger ? new Vector3(.025f, .019f, -.156f)
                : new Vector3(-.012f, .015f, triggerHand ? -.16f : .25f);
            SetThumbContact(thumb, weapon.TransformPoint(thumbTarget ?? thumbContact), refineTrigger ? weapon.right : weapon.up);
        }

        static void SetThumbContact(Transform[] thumb, Vector3 contact, Vector3 bendDirection)
        {
            if (thumb[0] == null || thumb[1] == null || thumb[2] == null) return;
            float a = Vector3.Distance(thumb[0].position, thumb[1].position);
            float b = Vector3.Distance(thumb[1].position, thumb[2].position);
            var delta = contact - thumb[0].position;
            var along = delta.normalized;
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            float x = (a * a - b * b + d * d) / (2f * d);
            var bend = Vector3.ProjectOnPlane(bendDirection, along).normalized;
            var middle = thumb[0].position + along * x + bend * Mathf.Sqrt(Mathf.Max(0, a * a - x * x));
            thumb[0].rotation = Quaternion.FromToRotation(thumb[1].position - thumb[0].position, middle - thumb[0].position) * thumb[0].rotation;
            thumb[1].rotation = Quaternion.FromToRotation(thumb[2].position - thumb[1].position, contact - thumb[1].position) * thumb[1].rotation;
        }

        public void LookAt(Vector3 aim)
        {
            if (isPark && neck != null && head != null)
            {
                // 가슴을 기준으로 얼굴이 돌 수 있는 범위를 정한다.
                var neutral = neck.parent.rotation * parkNeckRest * parkHeadRest * parkFaceInHead;
                var direction = Quaternion.Inverse(neutral) * (aim - head.position);
                if (direction.sqrMagnitude < .001f) return;
                float yaw = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -60f, 60f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg, -16f, 19f);
                var forward = neutral * Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
                var currentFace = head.rotation * parkFaceInHead;
                var currentForward = currentFace * Vector3.forward;
                var currentUp = Vector3.ProjectOnPlane(root.up, currentForward);
                // 클립의 작은 기울기는 유지하고, 조준 방향이 바뀔 때 추가되는 좌우 기울기는 막는다.
                var roll = Mathf.Clamp(Vector3.SignedAngle(currentUp, currentFace * Vector3.up, currentForward), -6f, 6f);
                var targetFace = Quaternion.AngleAxis(roll, forward) * Quaternion.LookRotation(forward, root.up);
                var turn = targetFace * Quaternion.Inverse(currentFace);
                // 목이 먼저 시선을 따라가고 머리뼈는 목에 대해 남은 각도만 보정한다.
                var neckTurn = Quaternion.RotateTowards(Quaternion.identity, Quaternion.Slerp(Quaternion.identity, turn, .70f), 40f);
                neck.rotation = neckTurn * neck.rotation;
                head.rotation = targetFace * Quaternion.Inverse(parkFaceInHead);
                return;
            }
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

        void Solve(Transform upper, Transform fore, Transform hand, Vector3 target, Quaternion rotation,
            Vector3 elbowDirection, AlliedRifleFit fit, bool palmContact = false, bool alignRightElbow = false,
            bool lockWristRotation = false)
        {
            bool right = hand == rightHand;
            var targetPosition = grip != null ? grip.AdjustPosition(right, target) : target;
            var bend = fit != null ? elbowDirection : fore.position - upper.position;
            bool limitWrist = fit != null && !palmContact;
            NpcRifleGrip.SolveArm(upper, fore, hand, new Pose(targetPosition, rotation), bend,
                limitWrist ? fit.maxForearmRoll : 180f, limitWrist && !lockWristRotation ? fit.maxWristSwing : 180f);
            if (alignRightElbow) AlignStandingRightElbow(rotation);
            if (grip != null)
            {
                var adjusted = grip.Adjust(right, new Pose(target, hand.rotation));
                if (grip.HasRotationOffset(right)) hand.rotation = adjusted.rotation;
            }
        }
    }
}
