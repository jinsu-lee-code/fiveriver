using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Combat
{
    /// <summary>애니메이션 평가 뒤 부츠의 침투를 보정하고 아군의 대기 접지를 맞춥니다.</summary>
    [DefaultExecutionOrder(130), DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class AlliedTerrainFootPlacement : MonoBehaviour
    {
        const float Clearance = .003f, MaximumLift = .2f, MaximumSlope = 35f, MaximumPelvisDrop = .03f;
        const float HillWalkPelvisDrop = .115f, HillLandingHeight = .30f, HillContactOverlap = .10f;
        Animator animator;
        AllyCombatant actor;
        FlowDirector director;
        Terrain ground;
        SkinnedMeshRenderer skin;
        Mesh mesh;
        Transform[] skinBones;
        Leg left, right;
        bool isPark, isClimber, ready;
        Transform actorRoot, hips, spine;
        float standingDrop;
        float MaximumCorrection => isClimber ? .3f : MaximumLift;
        internal bool UsesHillWalkContact => ready && !isPark && !isClimber;
        internal bool ClearingHillWall { get; set; }
        internal bool PreparingHillLanding { get; set; }

        public float PelvisDrop { get; private set; }
        public float LeftCorrection { get; private set; }
        public float RightCorrection { get; private set; }
        public float LeftMinimumGap { get; private set; }
        public float RightMinimumGap { get; private set; }

        sealed class Leg
        {
            internal Transform Upper, Lower, Foot;
            internal ContactPoint[] Points;
        }

        readonly struct Influence
        {
            internal readonly int Bone;
            internal readonly Vector3 Position;
            internal readonly float Weight;

            internal Influence(int bone, Vector3 position, float weight)
            { Bone = bone; Position = position; Weight = weight; }
        }

        sealed class ContactPoint
        {
            internal readonly Influence[] Weights;
            internal ContactPoint(params Influence[] weights) { Weights = weights; }

            internal Vector3 World(Transform[] bones)
            {
                var position = Vector3.zero;
                foreach (var weight in Weights)
                    position += bones[weight.Bone].TransformPoint(weight.Position) * weight.Weight;
                return position;
            }
        }

        sealed class BootProfile
        {
            internal string MeshName;
            internal int VertexCount;
            internal string[] BoneNames;
            internal ContactPoint[] Left, Right;
        }

        void Awake()
        {
            animator = GetComponent<Animator>();
            actor = GetComponentInParent<AllyCombatant>();
            director = FindAnyObjectByType<FlowDirector>();
            ground = Terrain.activeTerrain;
            actorRoot = actor != null ? actor.transform : null;
            Initialize();
        }

        /// <summary>실제 고지 합류 병사만 지정합니다. 트럭 승객에는 적용하지 않습니다.</summary>
        public void ConfigureClimber(Transform root)
        {
            if (root == null || !transform.IsChildOf(root) || actor != null) return;
            actorRoot = root;
            isClimber = true;
            Initialize();
        }

        void Initialize()
        {
            ready = false;
            skin = null;
            if (actorRoot == null || !animator.isHuman || animator.avatar == null) return;
            isPark = animator.avatar.name == "SK_CH_ROK_ParkYongpalAvatar";
            if (!isClimber && !isPark && animator.avatar.name != RegularAvatarName) return;
            var profile = Profile(isPark, isClimber);
            if (profile == null) return;
            foreach (var candidate in GetComponentsInChildren<SkinnedMeshRenderer>())
                if (candidate.sharedMesh != null && candidate.sharedMesh.name == profile.MeshName
                    && candidate.sharedMesh.vertexCount == profile.VertexCount) { skin = candidate; break; }
            if (skin == null) return;
            mesh = skin.sharedMesh;
            skinBones = skin.bones;
            if (skinBones.Length != profile.BoneNames.Length) return;
            for (int i = 0; i < skinBones.Length; i++)
                if (skinBones[i] == null || skinBones[i].name != profile.BoneNames[i]) return;
            left = ReadLeg(true, profile.Left);
            right = ReadLeg(false, profile.Right);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            ready = left != null && right != null && hips != null && spine != null;
        }

        Leg ReadLeg(bool isLeft, ContactPoint[] points)
        {
            if (points == null || points.Length == 0) return null;
            foreach (var point in points)
            {
                float sum = 0f;
                foreach (var weight in point.Weights)
                {
                    if (weight.Bone < 0 || weight.Bone >= skinBones.Length || !float.IsFinite(weight.Weight)
                        || weight.Weight < 0f || !Finite(weight.Position)) return null;
                    sum += weight.Weight;
                }
                if (Mathf.Abs(sum - 1f) > .001f) return null;
            }
            var leg = new Leg
            {
                Upper = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg),
                Lower = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg),
                Foot = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot),
                Points = points
            };
            return leg.Upper != null && leg.Lower != null && leg.Foot != null ? leg : null;
        }

        static bool Kneeling(AnimatorStateInfo state) => state.IsName("Idle_Trench") || state.IsName("Fire_Crouch");
        static bool Moving(AnimatorStateInfo state) => state.IsName("Run_Rifle") || state.IsName("Walk_Tired");
        static bool Locomotion(AnimatorStateInfo state) => Moving(state) || state.IsName("Idle_Stand");

        void LateUpdate()
        {
            LeftCorrection = RightCorrection = PelvisDrop = 0f;
            LeftMinimumGap = RightMinimumGap = float.NaN;
            if (!ready || (actor != null && !actor.isActiveAndEnabled) || actorRoot == null || !animator.isActiveAndEnabled || skin.sharedMesh != mesh
                || director == null || director.Flow == null || (!isPark && director.Flow.Paused) || !director.Flow.IsExpanded || ground == null || ground.terrainData == null
                || (director.Flow.Section != FlowSection.HillAssault && director.Flow.Section != FlowSection.Dawn)) return;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool transition = animator.IsInTransition(0);
            var next = transition ? animator.GetNextAnimatorStateInfo(0) : current;
            if (Kneeling(current) || Kneeling(next) || (!Locomotion(current) && !Locomotion(next))) return;
            var forward = Vector3.Cross(right.Upper.position - left.Upper.position, Vector3.up);
            forward = forward.sqrMagnitude > .000001f ? forward.normalized : actorRoot.forward;
            if (Moving(current) || Moving(next))
            {
                KeepKneeForward(left, forward);
                KeepKneeForward(right, forward);
            }
            if (ClearingHillWall && !PreparingHillLanding) return;
            if (!GroundHeight(actorRoot.position, out float rootHeight)) return;
            float rootGap = actorRoot.position.y - rootHeight;
            if (rootGap < -.16f || rootGap > (PreparingHillLanding ? HillLandingHeight : .16f)) return;
            bool settle = !transition && current.IsName("Idle_Stand");
            bool hillWalker = UsesHillWalkContact && director.Flow.Section == FlowSection.HillAssault;
            bool fromWalk = current.IsName("Walk_Tired");
            float walkContact = !transition && fromWalk ? 1f : 0f;
            if (hillWalker && transition)
            {
                float blend = animator.GetAnimatorTransitionInfo(0).normalizedTime;
                // 달리기의 체공을 살리되, 걷기 발이 먼저 지면을 떠나지 않게 합니다.
                if (fromWalk && next.IsName("Run_Rifle"))
                    walkContact = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, blend));
                else if (current.IsName("Run_Rifle") && next.IsName("Walk_Tired"))
                    walkContact = Mathf.SmoothStep(0f, 1f, blend);
            }
            bool walking = !isPark && walkContact > 0f;
            float leftContact = 0f, rightContact = 0f;
            if (walking)
            {
                float separation = SoleHeight(left) - SoleHeight(right);
                if (hillWalker)
                {
                    // 발 높이가 가까운 동안 두 발의 보정을 겹쳐 지지발이 바뀌는 순간의 튐을 줄입니다.
                    leftContact = walkContact * (1f - Mathf.SmoothStep(0f, 1f, separation / HillContactOverlap));
                    rightContact = walkContact * (1f - Mathf.SmoothStep(0f, 1f, -separation / HillContactOverlap));
                }
                else if (separation <= 0f) leftContact = 1f;
                else rightContact = 1f;
            }
            float leftLimit = settle || leftContact == 1f ? Clearance : float.PositiveInfinity;
            float rightLimit = settle || rightContact == 1f ? Clearance : float.PositiveInfinity;
            if (!isPark)
            {
                float wantedDrop = 0f;
                float leftAllowedDrop = leftContact > 0f ? float.PositiveInfinity : 0f;
                float rightAllowedDrop = rightContact > 0f ? float.PositiveInfinity : 0f;
                if ((settle || walking) && MinimumGap(left, out float leftBefore) && MinimumGap(right, out float rightBefore))
                {
                    if (walking)
                    {
                        if (leftContact > 0f)
                            leftLimit = Mathf.Lerp(Mathf.Max(Clearance, leftBefore), Clearance, leftContact);
                        if (rightContact > 0f)
                            rightLimit = Mathf.Lerp(Mathf.Max(Clearance, rightBefore), Clearance, rightContact);
                    }
                    float dropLimit = walking ? (hillWalker ? HillWalkPelvisDrop : .04f) : MaximumPelvisDrop;
                    wantedDrop = Mathf.Max(SupportDrop(left, leftBefore, leftLimit, dropLimit),
                        SupportDrop(right, rightBefore, rightLimit, dropLimit));
                    if (hillWalker && walking)
                    {
                        leftAllowedDrop = Mathf.Max(0f, leftBefore - leftLimit);
                        rightAllowedDrop = Mathf.Max(0f, rightBefore - rightLimit);
                    }
                    // 골반 하강 때문에 반대 발의 상향 보정 한도를 넘지 않게 합니다.
                    float liftRoom = Mathf.Max(0f, MaximumCorrection - Mathf.Max(0f, Clearance - Mathf.Min(leftBefore, rightBefore)));
                    wantedDrop = Mathf.Min(wantedDrop, liftRoom);
                }
                float dropSpeed = walking ? .75f : .15f;
                if (hillWalker && (walking || standingDrop > MaximumPelvisDrop))
                    dropSpeed = PreparingHillLanding && rootGap > .025f ? 1f : 2.4f;
                standingDrop = Mathf.MoveTowards(standingDrop, wantedDrop, dropSpeed * Time.deltaTime);
                if (hillWalker && fromWalk && transition && next.IsName("Run_Rifle"))
                    standingDrop = Mathf.Min(standingDrop, HillWalkPelvisDrop * walkContact);
                if (!current.IsName("Idle_Stand") && !next.IsName("Idle_Stand")
                    && !current.IsName("Walk_Tired") && !next.IsName("Walk_Tired")) standingDrop = 0f;
                PelvisDrop = standingDrop;
                // 앞서 맞춘 상체와 소총 파지를 보존하고 골반과 다리만 낮춥니다.
                var upperBodyPosition = spine.position;
                hips.position -= Vector3.up * standingDrop;
                spine.position = upperBodyPosition;
                if (walking && standingDrop > 0f)
                {
                    // 들고 있는 발은 골반이 낮아져도 원래 높이를 유지합니다.
                    float leftRestore = standingDrop - leftAllowedDrop;
                    float rightRestore = standingDrop - rightAllowedDrop;
                    if (leftRestore > .0001f)
                        Solve(left, left.Foot.position + Vector3.up * leftRestore, left.Foot.rotation, forward);
                    if (rightRestore > .0001f)
                        Solve(right, right.Foot.position + Vector3.up * rightRestore, right.Foot.rotation, forward);
                }
            }
            LeftCorrection = Correct(left, leftLimit, forward, out float leftGap);
            RightCorrection = Correct(right, rightLimit, forward, out float rightGap);
            LeftMinimumGap = leftGap;
            RightMinimumGap = rightGap;
        }

        float Correct(Leg leg, float maximumGap, Vector3 forward, out float gap)
        {
            gap = float.NaN;
            float total = 0f;
            // 혼합 가중치 정점은 무릎 굽힘에도 움직이므로 첫 풀이 뒤 한 번 더 확인합니다.
            for (int pass = 0; pass < 2; pass++)
            {
                if (!MinimumGap(leg, out gap)) return total;
                float targetGap = Mathf.Clamp(gap, Clearance, maximumGap);
                float correction = RequiredCorrection(gap, targetGap, MaximumCorrection - Mathf.Abs(total), true);
                if (correction == 0f) return total;
                var before = leg.Foot.position;
                var target = before + Vector3.up * correction;
                // 대기 발을 내릴 때 다리를 늘리거나 수평 위치를 바꾸지 않습니다.
                if (correction < 0f && !CanReach(Vector3.Distance(leg.Upper.position, leg.Lower.position),
                    Vector3.Distance(leg.Lower.position, before), Vector3.Distance(leg.Upper.position, target))) return total;
                Solve(leg, target, leg.Foot.rotation, forward);
                total += leg.Foot.position.y - before.y;
            }
            MinimumGap(leg, out gap);
            return total;
        }

        void OnDisable()
        {
            standingDrop = 0f;
            ClearingHillWall = false;
            PreparingHillLanding = false;
        }

        float SupportDrop(Leg leg, float gap, float targetGap, float maximum)
        {
            if (gap <= targetGap) return 0f;
            var target = leg.Foot.position + Vector3.up * (targetGap - gap);
            return RequiredPelvisDrop(Vector3.Distance(leg.Upper.position, leg.Lower.position),
                Vector3.Distance(leg.Lower.position, leg.Foot.position), leg.Upper.position - target, maximum);
        }

        internal static float RequiredPelvisDrop(float upperLength, float lowerLength, Vector3 hipFromTarget, float maximum)
        {
            if (!float.IsFinite(upperLength) || !float.IsFinite(lowerLength) || upperLength < .001f || lowerLength < .001f || !Finite(hipFromTarget)) return 0f;
            float reach = upperLength + lowerLength - .002f;
            float horizontal = hipFromTarget.x * hipFromTarget.x + hipFromTarget.z * hipFromTarget.z;
            if (horizontal >= reach * reach) return 0f;
            return Mathf.Clamp(hipFromTarget.y - Mathf.Sqrt(reach * reach - horizontal), 0f, maximum);
        }

        internal static float RequiredLift(float gap, float clearance, float maximum)
            => RequiredCorrection(gap, clearance, maximum, false);

        internal static float RequiredCorrection(float gap, float clearance, float maximum, bool allowLower)
        {
            float correction = clearance - gap;
            return float.IsFinite(correction) && Mathf.Abs(correction) > .0001f
                && Mathf.Abs(correction) <= maximum && (correction > 0f || allowLower) ? correction : 0f;
        }

        internal static bool CanReach(float upperLength, float lowerLength, float targetDistance)
            => upperLength >= .001f && lowerLength >= .001f && float.IsFinite(targetDistance)
                && targetDistance >= Mathf.Abs(upperLength - lowerLength) + .0001f
                && targetDistance <= upperLength + lowerLength - .0001f;

        float SoleHeight(Leg leg)
        {
            float height = float.PositiveInfinity;
            foreach (var point in leg.Points) height = Mathf.Min(height, point.World(skinBones).y);
            return height;
        }

        bool MinimumGap(Leg leg, out float gap)
        {
            gap = float.PositiveInfinity;
            foreach (var point in leg.Points)
            {
                var world = point.World(skinBones);
                // 지형 끝을 넘은 발끝 때문에 지형 안에 남은 발바닥의 보정을 취소하지 않습니다.
                if (!GroundHeight(world, out float height)) continue;
                gap = Mathf.Min(gap, world.y - height);
            }
            return float.IsFinite(gap);
        }

        bool GroundHeight(Vector3 world, out float height)
        {
            height = 0f;
            if (!Finite(world)) return false;
            var local = ground.transform.InverseTransformPoint(world);
            var size = ground.terrainData.size;
            if (size.x <= 0f || size.z <= 0f || local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) return false;
            var normal = ground.transform.TransformDirection(ground.terrainData.GetInterpolatedNormal(local.x / size.x, local.z / size.z));
            // 실제 고지 합류 경로는 NavMesh의 45도 경사까지 허용합니다.
            if (Vector3.Angle(normal, Vector3.up) > (isClimber ? 45f : MaximumSlope)) return false;
            height = ground.SampleHeight(world) + ground.transform.position.y;
            return float.IsFinite(height);
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static void KeepKneeForward(Leg leg, Vector3 forward)
        {
            var hip = leg.Upper.position;
            var foot = leg.Foot.position;
            var bend = Vector3.ProjectOnPlane(leg.Lower.position - hip, foot - hip);
            if (Vector3.Dot(bend, forward) < -.0001f)
                Solve(leg, foot, leg.Foot.rotation, forward);
        }

        static void Solve(Leg leg, Vector3 target, Quaternion rotation, Vector3 forward)
        {
            Vector3 a = leg.Upper.position, b = leg.Lower.position, c = leg.Foot.position;
            float first = Vector3.Distance(a, b), second = Vector3.Distance(b, c);
            var delta = target - a;
            if (first < .001f || second < .001f || delta.sqrMagnitude < .000001f) return;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + .0001f, first + second - .0001f);
            var direction = delta.normalized;
            var originalAxis = (c - a).normalized;
            // 발목 목표가 이동해도 원래 다리의 굽힘 방향을 새 축으로 옮깁니다.
            var bend = Quaternion.FromToRotation(originalAxis, direction) * Vector3.ProjectOnPlane(b - a, originalAxis);
            var front = Vector3.ProjectOnPlane(forward, direction).normalized;
            float forwardBend = Vector3.Dot(bend, front);
            if (forwardBend < 0f) bend -= 2f * forwardBend * front;
            if (bend.sqrMagnitude < .000001f) bend = front;
            if (bend.sqrMagnitude < .000001f) return;
            bend.Normalize();
            float along = (first * first + distance * distance - second * second) / (2f * distance);
            var knee = a + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, first * first - along * along));
            leg.Upper.rotation = Quaternion.FromToRotation(b - a, knee - a) * leg.Upper.rotation;
            leg.Lower.rotation = Quaternion.FromToRotation(leg.Foot.position - leg.Lower.position, target - leg.Lower.position) * leg.Lower.rotation;
            leg.Foot.rotation = rotation;
        }

        static BootProfile Profile(bool park, bool student) => new BootProfile
        {
            MeshName = student ? StudentMeshName : park ? ParkMeshName : RegularMeshName,
            VertexCount = student ? StudentVertexCount : park ? ParkVertexCount : RegularVertexCount,
            BoneNames = student ? StudentBoneNames : park ? ParkBoneNames : RegularBoneNames,
            Left = student ? StudentLeft : park ? ParkLeft : RegularLeft,
            Right = student ? StudentRight : park ? ParkRight : RegularRight
        };

        // shortcut: 현재 메시의 가중치와 bind pose로 추출한 표본입니다. 부츠나 가중치를 바꾸면 다시 추출합니다.
        // 박용팔의 새 FBX를 가져온 뒤 부츠의 정점과 바인드 행렬에서 다시 뽑았습니다.
        const string ParkMeshName = "ROK_ParkYongpal_SkinFix";
        const int ParkVertexCount = 148895;
        static readonly string[] ParkBoneNames = {
            "mixamorig:Hips",
            "mixamorig:Spine",
            "mixamorig:Spine1",
            "mixamorig:Spine2",
            "mixamorig:Neck",
            "mixamorig:Head",
            "mixamorig:LeftShoulder",
            "mixamorig:LeftArm",
            "mixamorig:LeftForeArm",
            "mixamorig:LeftHand",
            "mixamorig:LeftHandIndex1",
            "mixamorig:LeftHandIndex2",
            "mixamorig:LeftHandIndex3",
            "mixamorig:LeftHandMiddle1",
            "mixamorig:LeftHandMiddle2",
            "mixamorig:LeftHandMiddle3",
            "mixamorig:LeftHandRing1",
            "mixamorig:LeftHandRing2",
            "mixamorig:LeftHandRing3",
            "mixamorig:LeftHandPinky1",
            "mixamorig:LeftHandPinky2",
            "mixamorig:LeftHandPinky3",
            "mixamorig:LeftHandThumb1",
            "mixamorig:LeftHandThumb2",
            "mixamorig:LeftHandThumb3",
            "mixamorig:RightShoulder",
            "mixamorig:RightArm",
            "mixamorig:RightForeArm",
            "mixamorig:RightHand",
            "mixamorig:RightHandIndex1",
            "mixamorig:RightHandIndex2",
            "mixamorig:RightHandIndex3",
            "mixamorig:RightHandMiddle1",
            "mixamorig:RightHandMiddle2",
            "mixamorig:RightHandMiddle3",
            "mixamorig:RightHandRing1",
            "mixamorig:RightHandRing2",
            "mixamorig:RightHandRing3",
            "mixamorig:RightHandPinky1",
            "mixamorig:RightHandPinky2",
            "mixamorig:RightHandPinky3",
            "mixamorig:RightHandThumb1",
            "mixamorig:RightHandThumb2",
            "mixamorig:RightHandThumb3",
            "mixamorig:LeftUpLeg",
            "mixamorig:LeftLeg",
            "mixamorig:LeftFoot",
            "mixamorig:LeftToeBase",
            "mixamorig:RightUpLeg",
            "mixamorig:RightLeg",
            "mixamorig:RightFoot",
            "mixamorig:RightToeBase",
        };
        static readonly ContactPoint[] ParkLeft = {
            new ContactPoint(new Influence(47, new Vector3(-2.19943468e-05f, 0.000989007764f, 0.000270421297f), 1f)), // mesh vertex 13952
            new ContactPoint(new Influence(46, new Vector3(4.49391082e-05f, -0.000137789f, 0.00149274012f), 1f)), // mesh vertex 13813
            new ContactPoint(new Influence(47, new Vector3(-0.000588646857f, 0.000401082332f, 0.000444699777f), 0.863060653f), new Influence(46, new Vector3(-0.000571587007f, 0.00228436454f, 0.000239751011f), 0.136939332f)), // mesh vertex 30962
            new ContactPoint(new Influence(47, new Vector3(-9.91427805e-05f, 0.000973988557f, 0.00027394839f), 1f)), // mesh vertex 13908
            new ContactPoint(new Influence(47, new Vector3(0.000408723135f, 0.000672023278f, 0.000361146551f), 1f)), // mesh vertex 13906
            new ContactPoint(new Influence(46, new Vector3(3.64223961e-05f, -0.000257340958f, 0.00150007801f), 1f)), // mesh vertex 13691
            new ContactPoint(new Influence(47, new Vector3(1.70730054e-05f, 0.00103219855f, 0.000213135587f), 1f)), // mesh vertex 13901
            new ContactPoint(new Influence(46, new Vector3(-0.00070301455f, 0.00171385403f, 0.00056730106f), 1f)), // mesh vertex 38636
            new ContactPoint(new Influence(47, new Vector3(0.000681901933f, 4.2167725e-05f, 0.000401414727f), 0.567649961f), new Influence(46, new Vector3(0.000661924481f, 0.00182714453f, 0.000359713682f), 0.432350039f)), // mesh vertex 13526
            new ContactPoint(new Influence(46, new Vector3(-1.66073442e-05f, -0.000739109993f, 0.00062704162f), 1f)), // mesh vertex 13393
            new ContactPoint(new Influence(47, new Vector3(0.000280786306f, 0.000845407601f, -0.000103390601f), 1f)), // mesh vertex 13597
            new ContactPoint(new Influence(46, new Vector3(-0.000685953186f, 0.00200101291f, 0.000426764134f), 0.681469023f), new Influence(47, new Vector3(-0.000675014453f, 5.80769265e-05f, 0.00050153106f), 0.318530977f)), // mesh vertex 13611
            new ContactPoint(new Influence(47, new Vector3(0.000579357264f, 0.000391564216f, 0.000409981876f), 1f)), // mesh vertex 13613
            new ContactPoint(new Influence(47, new Vector3(0.000638373895f, 6.14424935e-05f, 0.000455139321f), 0.583624899f), new Influence(46, new Vector3(0.000621442799f, 0.00186988339f, 0.000400788907f), 0.416375071f)), // mesh vertex 13614
            new ContactPoint(new Influence(46, new Vector3(-0.000351620954f, 9.54195275e-05f, 0.00138609903f), 1f)), // mesh vertex 13619
            new ContactPoint(new Influence(46, new Vector3(-0.000319684041f, -7.53030763e-05f, 0.00141982897f), 1f)), // mesh vertex 13622
            new ContactPoint(new Influence(46, new Vector3(0.000234675244f, -0.000224399555f, 0.00146854925f), 1f)), // mesh vertex 13696
            new ContactPoint(new Influence(47, new Vector3(0.000154659734f, 0.000889007933f, -9.4806368e-05f), 1f)), // mesh vertex 13708
            new ContactPoint(new Influence(47, new Vector3(-0.000302746426f, 0.000894031487f, 0.000252728816f), 1f)), // mesh vertex 13713
            new ContactPoint(new Influence(46, new Vector3(-0.000169600011f, -0.000198642607f, 0.00148220477f), 1f)), // mesh vertex 13722
            new ContactPoint(new Influence(47, new Vector3(0.000516436645f, 0.00061741937f, 0.000308092014f), 1f)), // mesh vertex 13715
            new ContactPoint(new Influence(47, new Vector3(0.000290346565f, 0.000901915133f, 0.00028422894f), 1f)), // mesh vertex 13895
            new ContactPoint(new Influence(46, new Vector3(0.000392454443f, -0.000115454895f, 0.00139777421f), 1f)), // mesh vertex 30937
            new ContactPoint(new Influence(47, new Vector3(0.000207117526f, 0.000974370167f, 0.000230430713f), 1f)), // mesh vertex 31141
            new ContactPoint(new Influence(47, new Vector3(-0.000129499938f, 0.00100769033f, 0.000219346926f), 1f)), // mesh vertex 31150
            new ContactPoint(new Influence(47, new Vector3(-0.000446126796f, 0.000673336908f, 0.000382192811f), 1f)), // mesh vertex 13826
            new ContactPoint(new Influence(46, new Vector3(-0.000118281576f, -0.000128059008f, 0.00149338692f), 1f)), // mesh vertex 13892
            new ContactPoint(new Influence(46, new Vector3(0.00046101585f, 0.000104028732f, 0.00128423097f), 1f)), // mesh vertex 38587
            new ContactPoint(new Influence(46, new Vector3(0.000327238464f, -0.000104678562f, 0.00145362411f), 1f)), // mesh vertex 38851
        };
        static readonly ContactPoint[] ParkRight = {
            new ContactPoint(new Influence(51, new Vector3(-0.00037830777f, 0.000747966929f, 0.000309023424f), 1f)), // mesh vertex 13859
            new ContactPoint(new Influence(51, new Vector3(-1.13670249e-05f, 0.00108725904f, 0.000145657425f), 1f)), // mesh vertex 13767
            new ContactPoint(new Influence(51, new Vector3(-9.75279836e-05f, 0.000968799694f, 0.000231137092f), 1f)), // mesh vertex 13934
            new ContactPoint(new Influence(51, new Vector3(0.000429094536f, 0.000449511223f, 0.000369057816f), 1f)), // mesh vertex 13872
            new ContactPoint(new Influence(50, new Vector3(0.000131745939f, -0.000157345086f, 0.00145919924f), 1f)), // mesh vertex 13771
            new ContactPoint(new Influence(50, new Vector3(-0.00064458116f, 0.00174506637f, 0.00040459336f), 0.58313477f), new Influence(51, new Vector3(-0.000625979505f, -1.89989805e-05f, 0.000425043836f), 0.41686523f)), // mesh vertex 13483
            new ContactPoint(new Influence(51, new Vector3(0.000655041309f, 0.000320737832f, 0.000377169054f), 0.794332743f), new Influence(50, new Vector3(0.000574297155f, 0.00222515874f, 0.000198248366f), 0.205667272f)), // mesh vertex 13849
            new ContactPoint(new Influence(50, new Vector3(-0.000419897842f, -3.58723919e-05f, 0.00136517093f), 1f)), // mesh vertex 13383
            new ContactPoint(new Influence(50, new Vector3(-0.000492628082f, 0.000133752066f, 0.00118648238f), 1f)), // mesh vertex 13385
            new ContactPoint(new Influence(50, new Vector3(-0.000271661789f, -0.000211573613f, 0.00143851712f), 1f)), // mesh vertex 13488
            new ContactPoint(new Influence(51, new Vector3(-0.000572914258f, 0.00024732668f, 0.000394510716f), 0.821249127f), new Influence(50, new Vector3(-0.000629529008f, 0.0019811769f, 0.000267834694f), 0.178750843f)), // mesh vertex 13561
            new ContactPoint(new Influence(51, new Vector3(-0.000629455317f, 0.000178393209f, 0.000323478365f), 0.727742851f), new Influence(50, new Vector3(-0.000672323629f, 0.00188181805f, 0.000231906772f), 0.272257119f)), // mesh vertex 13562
            new ContactPoint(new Influence(50, new Vector3(-6.5443106e-05f, -0.000272344565f, 0.00145523599f), 1f)), // mesh vertex 13571
            new ContactPoint(new Influence(51, new Vector3(-0.000491224113f, 0.000501123024f, 0.000362919527f), 1f)), // mesh vertex 13658
            new ContactPoint(new Influence(51, new Vector3(-0.000556133455f, 0.00043745758f, 0.000320705061f), 1f)), // mesh vertex 13660
            new ContactPoint(new Influence(50, new Vector3(0.000168869388f, -0.000217800552f, 0.00143674389f), 1f)), // mesh vertex 13681
            new ContactPoint(new Influence(50, new Vector3(-0.000321789994f, -0.000151722925f, 0.00144156488f), 1f)), // mesh vertex 13683
            new ContactPoint(new Influence(50, new Vector3(0.000426857849f, 0.000142458593f, 0.00122958724f), 1f)), // mesh vertex 13729
            new ContactPoint(new Influence(51, new Vector3(0.000188898528f, 0.00105638499f, 0.000138203264f), 1f)), // mesh vertex 13759
            new ContactPoint(new Influence(51, new Vector3(-0.000358856749f, 0.00081188418f, 8.18023982e-05f), 1f)), // mesh vertex 13762
            new ContactPoint(new Influence(51, new Vector3(0.000188297359f, 0.00105140987f, 0.000183581578f), 1f)), // mesh vertex 13768
            new ContactPoint(new Influence(51, new Vector3(0.000334296143f, 0.000961789396f, 0.000190093619f), 1f)), // mesh vertex 13855
            new ContactPoint(new Influence(51, new Vector3(-0.000229200465f, 0.000962813152f, 0.000228904129f), 1f)), // mesh vertex 13860
            new ContactPoint(new Influence(51, new Vector3(0.000608213479f, 0.000237745582f, 0.000418037496f), 0.669307172f), new Influence(50, new Vector3(0.000538310152f, 0.00215966022f, 0.000270120974f), 0.330692828f)), // mesh vertex 13873
            new ContactPoint(new Influence(51, new Vector3(-0.000152251683f, 0.00102396449f, 9.35222488e-05f), 1f)), // mesh vertex 13666
            new ContactPoint(new Influence(50, new Vector3(0.000310923322f, -3.17312661e-05f, 0.00139293098f), 1f)), // mesh vertex 13887
            new ContactPoint(new Influence(50, new Vector3(0.000665522763f, 0.00195302628f, 0.000391698675f), 0.732595921f), new Influence(51, new Vector3(0.000700497068f, -1.65075762e-05f, 0.000440420059f), 0.26740405f)), // mesh vertex 13913
            new ContactPoint(new Influence(51, new Vector3(0.000440106494f, 0.000740507734f, 0.000273838494f), 1f)), // mesh vertex 13919
            new ContactPoint(new Influence(51, new Vector3(-6.26747496e-05f, 0.000762546668f, -0.00028746418f), 1f)), // mesh vertex 13566
            new ContactPoint(new Influence(51, new Vector3(0.00033676927f, 0.00069966272f, -0.000155366375f), 1f)), // mesh vertex 38747
            new ContactPoint(new Influence(50, new Vector3(0.000668646186f, 0.00167760486f, 0.000521286041f), 1f)), // mesh vertex 13648
        };

        // Assets/_Project/Art/Models/Characters/ROK_Soldier/SK_CH_ROK_Soldier_Person1.fbx
        const string RegularAvatarName = "SK_CH_ROK_Soldier_Person1Avatar";
        const string RegularMeshName = "Body_Low";
        const int RegularVertexCount = 151323;
        static readonly string[] RegularBoneNames = {
            "mixamorig:Hips",
            "mixamorig:Spine",
            "mixamorig:Spine1",
            "mixamorig:Spine2",
            "mixamorig:Neck",
            "mixamorig:Head",
            "mixamorig:LeftShoulder",
            "mixamorig:LeftArm",
            "mixamorig:LeftForeArm",
            "mixamorig:LeftHand",
            "mixamorig:LeftHandThumb1",
            "mixamorig:LeftHandThumb2",
            "mixamorig:LeftHandThumb3",
            "mixamorig:LeftHandIndex1",
            "mixamorig:LeftHandIndex2",
            "mixamorig:LeftHandIndex3",
            "mixamorig:LeftHandMiddle1",
            "mixamorig:LeftHandMiddle2",
            "mixamorig:LeftHandMiddle3",
            "mixamorig:LeftHandRing1",
            "mixamorig:LeftHandRing2",
            "mixamorig:LeftHandRing3",
            "mixamorig:LeftHandPinky1",
            "mixamorig:LeftHandPinky2",
            "mixamorig:LeftHandPinky3",
            "mixamorig:RightShoulder",
            "mixamorig:RightArm",
            "mixamorig:RightForeArm",
            "mixamorig:RightHand",
            "mixamorig:RightHandThumb1",
            "mixamorig:RightHandThumb2",
            "mixamorig:RightHandThumb3",
            "mixamorig:RightHandIndex1",
            "mixamorig:RightHandIndex2",
            "mixamorig:RightHandIndex3",
            "mixamorig:RightHandMiddle1",
            "mixamorig:RightHandMiddle2",
            "mixamorig:RightHandMiddle3",
            "mixamorig:RightHandRing1",
            "mixamorig:RightHandRing2",
            "mixamorig:RightHandRing3",
            "mixamorig:RightHandPinky1",
            "mixamorig:RightHandPinky2",
            "mixamorig:RightHandPinky3",
            "mixamorig:LeftUpLeg",
            "mixamorig:LeftLeg",
            "mixamorig:LeftFoot",
            "mixamorig:LeftToeBase",
            "mixamorig:RightUpLeg",
            "mixamorig:RightLeg",
            "mixamorig:RightFoot",
            "mixamorig:RightToeBase",
        };
        static readonly ContactPoint[] RegularLeft = {
            new ContactPoint(new Influence(46, new Vector3(-0.000292904f, -0.0007239359f, -0.00029430876f), 0.6961502f), new Influence(45, new Vector3(-0.0002717286f, 0.00429525226f, -0.0007691262f), 0.3038498f)), // mesh vertex 53173
            new ContactPoint(new Influence(46, new Vector3(-0.000171777981f, -0.000790022546f, -0.000357537181f), 0.6656499f), new Influence(45, new Vector3(-0.00014470602f, 0.00427693f, -0.000850159035f), 0.3343501f)), // mesh vertex 53670
            new ContactPoint(new Influence(46, new Vector3(-1.5697351E-05f, -0.000793425f, -0.0003754607f), 0.630326867f), new Influence(45, new Vector3(1.04829032E-05f, 0.0042522545f, -0.0008513798f), 0.369673133f)), // mesh vertex 54174
            new ContactPoint(new Influence(46, new Vector3(-0.000474375382f, -0.000525194162f, -0.000658910547f), 1f)), // mesh vertex 55046
            new ContactPoint(new Influence(46, new Vector3(-0.000612095057f, -0.0003595379f, -0.000685384148f), 1f)), // mesh vertex 55048
            new ContactPoint(new Influence(46, new Vector3(-0.0001759693f, -0.0006514294f, -0.000655254f), 0.952493548f), new Influence(45, new Vector3(-6.832286E-05f, 0.00458986266f, -0.00091418816f), 0.0475064367f)), // mesh vertex 55362
            new ContactPoint(new Influence(46, new Vector3(-0.0003579485f, -0.0005831976f, -0.0007453406f), 1f)), // mesh vertex 55367
            new ContactPoint(new Influence(46, new Vector3(-0.000574433f, -0.0003652355f, -0.000900172046f), 1f)), // mesh vertex 55375
            new ContactPoint(new Influence(46, new Vector3(-0.000223367155f, -0.000593014469f, -0.0007960549f), 1f)), // mesh vertex 55619
            new ContactPoint(new Influence(46, new Vector3(-0.0007073376f, -0.0001831515f, -0.0009242797f), 1f)), // mesh vertex 55626
            new ContactPoint(new Influence(46, new Vector3(-0.000452495529f, -0.000420914672f, -0.0009810468f), 1f)), // mesh vertex 55813
            new ContactPoint(new Influence(46, new Vector3(-0.000616284262f, -0.0002665727f, -0.00101646187f), 1f)), // mesh vertex 55819
            new ContactPoint(new Influence(46, new Vector3(-0.0003051435f, -0.00043263298f, -0.00103301893f), 1f)), // mesh vertex 56002
            new ContactPoint(new Influence(46, new Vector3(-0.000489338068f, -0.0003134929f, -0.00109551754f), 1f)), // mesh vertex 56007
            new ContactPoint(new Influence(46, new Vector3(-0.000151738248f, -0.0004053838f, -0.001053125f), 1f)), // mesh vertex 56177
            new ContactPoint(new Influence(46, new Vector3(-0.000348047324f, -0.000331168034f, -0.001152579f), 1f)), // mesh vertex 56182
            new ContactPoint(new Influence(46, new Vector3(-0.000795655f, 0.0001444338f, -0.00124618993f), 1f)), // mesh vertex 56189
            new ContactPoint(new Influence(46, new Vector3(-0.000194309672f, -0.000306208269f, -0.00116941461f), 1f)), // mesh vertex 56409
            new ContactPoint(new Influence(46, new Vector3(-0.0007068297f, 6.936798E-05f, -0.00131992344f), 1f)), // mesh vertex 56418
            new ContactPoint(new Influence(46, new Vector3(-0.000802747032f, 0.000202916373f, -0.00128376344f), 1f)), // mesh vertex 56421
            new ContactPoint(new Influence(46, new Vector3(-0.000465145073f, 0.001741867f, -0.000471279316f), 1f)), // mesh vertex 56471
            new ContactPoint(new Influence(46, new Vector3(-0.000524261734f, 0.00187220611f, -0.00033232983f), 1f)), // mesh vertex 56474
            new ContactPoint(new Influence(46, new Vector3(-0.0006153676f, 0.001937666f, -0.0001096003f), 0.9168343f), new Influence(47, new Vector3(-0.000507607f, 0.0003510768f, -0.0005511596f), 0.08316569f)), // mesh vertex 56490
            new ContactPoint(new Influence(46, new Vector3(-0.000603503955f, 2.9685214E-05f, -0.001401576f), 1f)), // mesh vertex 56630
            new ContactPoint(new Influence(46, new Vector3(-0.0004947434f, 0.00195242139f, -0.000290028169f), 0.9910921f), new Influence(47, new Vector3(-0.000345063425f, 0.000268600881f, -0.0006699001f), 0.008907921f)), // mesh vertex 56683
            new ContactPoint(new Influence(46, new Vector3(-0.000480928546f, 1.70700841E-05f, -0.00145631714f), 1f)), // mesh vertex 56870
            new ContactPoint(new Influence(46, new Vector3(-0.000367583969f, 0.00194682472f, -0.000340624829f), 0.9984664f), new Influence(47, new Vector3(-0.000210674276f, 0.0002423239f, -0.000673046045f), 0.00153367163f)), // mesh vertex 56936
            new ContactPoint(new Influence(46, new Vector3(-0.000468951534f, 0.00204193685f, -0.000225815151f), 0.8754334f), new Influence(47, new Vector3(-0.0003266033f, 0.000379927747f, -0.0006617086f), 0.124566667f)), // mesh vertex 56938
            new ContactPoint(new Influence(46, new Vector3(-0.0003443599f, 2.00718678E-05f, -0.00146947289f), 1f)), // mesh vertex 57098
            new ContactPoint(new Influence(46, new Vector3(-0.0008298784f, 0.0004742883f, -0.0012625797f), 1f)), // mesh vertex 57115
            new ContactPoint(new Influence(46, new Vector3(-0.0003173668f, 0.00203824579f, -0.000270515418f), 0.8802759f), new Influence(47, new Vector3(-0.000169961553f, 0.000359810278f, -0.000654761156f), 0.119724065f)), // mesh vertex 57163
            new ContactPoint(new Influence(46, new Vector3(-0.000450958061f, 0.00214709342f, -0.000123499936f), 0.513727546f), new Influence(47, new Vector3(-0.000323444256f, 0.000525110634f, -0.0006341036f), 0.486272424f)), // mesh vertex 57170
            new ContactPoint(new Influence(47, new Vector3(-0.000380149024f, 0.0006521214f, -0.00057642383f), 0.7995743f), new Influence(46, new Vector3(-0.000483564625f, 0.00221420871f, 7.29180056E-06f), 0.2004257f)), // mesh vertex 57175
            new ContactPoint(new Influence(46, new Vector3(-0.000765242556f, 0.0002849103f, -0.00144379633f), 1f)), // mesh vertex 57322
            new ContactPoint(new Influence(46, new Vector3(-0.000808141835f, 0.00053034554f, -0.00125664519f), 1f)), // mesh vertex 57326
            new ContactPoint(new Influence(47, new Vector3(-0.000279847882f, 0.0006528702f, -0.0005928678f), 0.85288626f), new Influence(46, new Vector3(-0.000391332374f, 0.00223464961f, -3.02142435E-05f), 0.147113681f)), // mesh vertex 57380
            new ContactPoint(new Influence(46, new Vector3(-0.0006775624f, 0.000251445774f, -0.00151837955f), 1f)), // mesh vertex 57515
            new ContactPoint(new Influence(46, new Vector3(-0.000770886953f, 0.000305121474f, -0.00144761766f), 1f)), // mesh vertex 57521
            new ContactPoint(new Influence(46, new Vector3(0.000702285441f, 0.00194946246f, -0.0005828101f), 1f)), // mesh vertex 57592
            new ContactPoint(new Influence(46, new Vector3(0.000673731731f, 0.00181607238f, -0.000685579f), 1f)), // mesh vertex 57593
            new ContactPoint(new Influence(47, new Vector3(0.0008824641f, 0.000367571076f, -0.000542014139f), 0.5274224f), new Influence(46, new Vector3(0.0007261276f, 0.00209516427f, -0.0004386024f), 0.472577631f)), // mesh vertex 57597
            new ContactPoint(new Influence(46, new Vector3(-0.000433033449f, 0.000229160054f, -0.00160798535f), 1f)), // mesh vertex 57737
            new ContactPoint(new Influence(46, new Vector3(-0.0006893695f, 0.000292583136f, -0.0015034941f), 1f)), // mesh vertex 57747
            new ContactPoint(new Influence(46, new Vector3(-0.0007701707f, 0.0003452091f, -0.00143197866f), 1f)), // mesh vertex 57749
            new ContactPoint(new Influence(46, new Vector3(-0.0005699222f, 0.0002906734f, -0.00155015138f), 1f)), // mesh vertex 57763
            new ContactPoint(new Influence(46, new Vector3(-0.000442799152f, 0.000294828817f, -0.00159319537f), 1f)), // mesh vertex 57764
            new ContactPoint(new Influence(46, new Vector3(-0.000298882864f, 0.000322360254f, -0.001621527f), 1f)), // mesh vertex 57768
            new ContactPoint(new Influence(46, new Vector3(5.023606E-05f, 0.0008159314f, -0.00133717142f), 1f)), // mesh vertex 57782
            new ContactPoint(new Influence(47, new Vector3(-0.000278689316f, 0.00111861457f, -0.000253019331f), 1f)), // mesh vertex 57840
            new ContactPoint(new Influence(47, new Vector3(-4.43954377E-05f, 0.001345145f, -8.962956E-05f), 1f)), // mesh vertex 57931
            new ContactPoint(new Influence(46, new Vector3(-0.000314642966f, 0.0002518073f, -0.00164150656f), 1f)), // mesh vertex 57939
            new ContactPoint(new Influence(46, new Vector3(2.40285249E-06f, 0.000529299432f, -0.00147966063f), 1f)), // mesh vertex 57945
            new ContactPoint(new Influence(46, new Vector3(-3.7898084E-05f, 0.0005256509f, -0.0015024374f), 1f)), // mesh vertex 57953
            new ContactPoint(new Influence(46, new Vector3(-0.000440313626f, 0.0002587265f, -0.00161410577f), 1f)), // mesh vertex 57961
            new ContactPoint(new Influence(47, new Vector3(-0.000185356024f, 0.0012650243f, -0.0001867076f), 1f)), // mesh vertex 58018
            new ContactPoint(new Influence(47, new Vector3(0.000238114342f, 0.001314009f, -5.33161165E-06f), 1f)), // mesh vertex 58060
            new ContactPoint(new Influence(47, new Vector3(9.834816E-05f, 0.00133320061f, -0.000101319332f), 1f)), // mesh vertex 58112
            new ContactPoint(new Influence(47, new Vector3(-5.553483E-06f, 0.0013183807f, -0.000150417312f), 1f)), // mesh vertex 58116
            new ContactPoint(new Influence(46, new Vector3(-0.000226911841f, 0.000363950618f, -0.00160304992f), 1f)), // mesh vertex 58118
            new ContactPoint(new Influence(46, new Vector3(-0.000217697219f, 0.00033207392f, -0.00162363192f), 1f)), // mesh vertex 58121
            new ContactPoint(new Influence(46, new Vector3(-0.000315514946f, 0.000282816764f, -0.00164222962f), 1f)), // mesh vertex 58122
            new ContactPoint(new Influence(47, new Vector3(-0.000133482448f, 0.00124232215f, -0.000210784085f), 1f)), // mesh vertex 58201
            new ContactPoint(new Influence(47, new Vector3(0.0003659701f, 0.00126139366f, -4.68629551E-05f), 1f)), // mesh vertex 58241
            new ContactPoint(new Influence(47, new Vector3(0.000227553464f, 0.00129259261f, -0.000114435046f), 1f)), // mesh vertex 58243
            new ContactPoint(new Influence(46, new Vector3(0.0006047108f, 0.00157869456f, -0.000838935433f), 1f)), // mesh vertex 58269
            new ContactPoint(new Influence(47, new Vector3(9.54326242E-05f, 0.00126935367f, -0.000172377317f), 1f)), // mesh vertex 58282
            new ContactPoint(new Influence(47, new Vector3(0.000484832417f, 0.00118873734f, -8.497675E-05f), 1f)), // mesh vertex 58374
            new ContactPoint(new Influence(47, new Vector3(0.000350895745f, 0.00124480727f, -0.00013328233f), 1f)), // mesh vertex 58376
            new ContactPoint(new Influence(47, new Vector3(0.000418675336f, 0.0011037963f, -0.00021336558f), 1f)), // mesh vertex 58434
            new ContactPoint(new Influence(47, new Vector3(0.0007391209f, 0.000503889634f, -0.0005041848f), 0.97067976f), new Influence(46, new Vector3(0.000605334353f, 0.00217165332f, -0.00029675875f), 0.0293202586f)), // mesh vertex 58450
            new ContactPoint(new Influence(47, new Vector3(0.000848184f, 0.000479959126f, -0.0004972487f), 0.941799939f), new Influence(46, new Vector3(0.0007109126f, 0.00215971866f, -0.000331778254f), 0.05820009f)), // mesh vertex 58452
            new ContactPoint(new Influence(47, new Vector3(0.0005917033f, 0.0010974278f, -0.000143405312f), 1f)), // mesh vertex 58466
            new ContactPoint(new Influence(47, new Vector3(0.000464050681f, 0.00117141614f, -0.0001591635f), 1f)), // mesh vertex 58471
            new ContactPoint(new Influence(47, new Vector3(0.000676799f, 0.000964293955f, -0.000201203176f), 1f)), // mesh vertex 58510
            new ContactPoint(new Influence(47, new Vector3(0.0005819867f, 0.00107041234f, -0.0001950622f), 1f)), // mesh vertex 58516
            new ContactPoint(new Influence(47, new Vector3(0.000817683467f, 0.000712441863f, -0.0002787552f), 1f)), // mesh vertex 58529
            new ContactPoint(new Influence(47, new Vector3(0.0008929733f, 0.0004614007f, -0.000431507855f), 0.9477976f), new Influence(46, new Vector3(0.000770508952f, 0.00211288058f, -0.00030133457f), 0.0522023924f)), // mesh vertex 58536
        };
        static readonly ContactPoint[] RegularRight = {
            new ContactPoint(new Influence(50, new Vector3(4.20283832E-05f, 0.0005480302f, -0.001517924f), 1f)), // mesh vertex 479
            new ContactPoint(new Influence(50, new Vector3(1.28147667E-05f, 0.0005303647f, -0.0015130234f), 1f)), // mesh vertex 480
            new ContactPoint(new Influence(50, new Vector3(0.0001795595f, 0.000179292474f, -0.00152905635f), 1f)), // mesh vertex 50755
            new ContactPoint(new Influence(50, new Vector3(0.000210842816f, 0.000286034541f, -0.00161085429f), 1f)), // mesh vertex 51493
            new ContactPoint(new Influence(50, new Vector3(0.000710131f, 0.0002829321f, -0.00149408355f), 1f)), // mesh vertex 51533
            new ContactPoint(new Influence(50, new Vector3(0.0006085799f, 0.000267674448f, -0.001546045f), 1f)), // mesh vertex 51534
            new ContactPoint(new Influence(50, new Vector3(0.00045759746f, 0.0002724844f, -0.00159825734f), 1f)), // mesh vertex 51536
            new ContactPoint(new Influence(50, new Vector3(0.0003138642f, 0.0003059966f, -0.00162454392f), 1f)), // mesh vertex 51537
            new ContactPoint(new Influence(50, new Vector3(-0.000624724664f, 0.00155387679f, -0.0008247246f), 1f)), // mesh vertex 51817
            new ContactPoint(new Influence(51, new Vector3(-0.000171349311f, 0.00114669721f, 0.000376391225f), 0.995844364f), new Influence(50, new Vector3(-0.000323601562f, 0.002156808f, 0.0008880773f), 0.00415557f)), // mesh vertex 51856
            new ContactPoint(new Influence(50, new Vector3(0.0003364899f, 0.000232040285f, -0.00163455075f), 1f)), // mesh vertex 51955
            new ContactPoint(new Influence(50, new Vector3(0.000213477091f, 0.000323842076f, -0.00161782675f), 1f)), // mesh vertex 51959
            new ContactPoint(new Influence(50, new Vector3(-0.0005317846f, 0.001572826f, -0.0008449633f), 1f)), // mesh vertex 52249
            new ContactPoint(new Influence(50, new Vector3(-0.0006828063f, 0.00171580468f, -0.000724712f), 1f)), // mesh vertex 52251
            new ContactPoint(new Influence(51, new Vector3(-0.000203015108f, 0.00122820714f, 0.00025371532f), 1f)), // mesh vertex 52277
            new ContactPoint(new Influence(51, new Vector3(-1.03589791E-05f, 0.00117932388f, 0.000347855064f), 0.9876482f), new Influence(50, new Vector3(-0.000162728626f, 0.00218227948f, 0.00092368375f), 0.0123518026f)), // mesh vertex 52280
            new ContactPoint(new Influence(50, new Vector3(0.0003299122f, 0.0002660536f, -0.00163858256f), 1f)), // mesh vertex 52398
            new ContactPoint(new Influence(50, new Vector3(0.000222518516f, 0.000354056159f, -0.001612354f), 1f)), // mesh vertex 52404
            new ContactPoint(new Influence(50, new Vector3(-0.0005973461f, 0.00179681659f, -0.0006916264f), 1f)), // mesh vertex 52765
            new ContactPoint(new Influence(50, new Vector3(-0.0007167574f, 0.00188334042f, -0.0006379021f), 1f)), // mesh vertex 52766
            new ContactPoint(new Influence(51, new Vector3(-0.000229606056f, 0.00129216956f, 0.000124449478f), 1f)), // mesh vertex 52802
            new ContactPoint(new Influence(51, new Vector3(0.000142325574f, 0.0011724123f, 0.0002827427f), 0.9860672f), new Influence(50, new Vector3(1.92063226E-06f, 0.00219591148f, 0.0009062277f), 0.0139328558f)), // mesh vertex 52812
            new ContactPoint(new Influence(50, new Vector3(0.0006095146f, 0.00196599425f, -9.615935E-05f), 0.8869366f), new Influence(51, new Vector3(0.0005022881f, 0.000392619782f, -0.000547418138f), 0.11306341f)), // mesh vertex 52822
            new ContactPoint(new Influence(50, new Vector3(0.000569010037f, 0.00206960551f, -1.60229756E-05f), 0.6597588f), new Influence(51, new Vector3(0.000472153537f, 0.0005252253f, -0.00052994187f), 0.340241164f)), // mesh vertex 52825
            new ContactPoint(new Influence(51, new Vector3(0.0004468368f, 0.0006826053f, -0.0005008425f), 0.6869276f), new Influence(50, new Vector3(0.0005287718f, 0.00218687137f, 8.83115863E-05f), 0.313072354f)), // mesh vertex 52827
            new ContactPoint(new Influence(50, new Vector3(0.000472002226f, 0.000238234512f, -0.00159663823f), 1f)), // mesh vertex 52910
            new ContactPoint(new Influence(50, new Vector3(-0.0006124516f, 0.001930958f, -0.000587869261f), 1f)), // mesh vertex 53238
            new ContactPoint(new Influence(50, new Vector3(-0.0007431846f, 0.002013839f, -0.0005188619f), 0.9522468f), new Influence(51, new Vector3(-0.000910144765f, 0.000266963936f, -0.0005489281f), 0.0477532074f)), // mesh vertex 53240
            new ContactPoint(new Influence(51, new Vector3(-0.000403382757f, 0.001261218f, 1.6430673E-05f), 1f)), // mesh vertex 53271
            new ContactPoint(new Influence(51, new Vector3(-4.898236E-05f, 0.00133672811f, 0.000109119253f), 1f)), // mesh vertex 53275
            new ContactPoint(new Influence(51, new Vector3(-0.000233881816f, 0.001325878f, 3.210963E-05f), 1f)), // mesh vertex 53277
            new ContactPoint(new Influence(50, new Vector3(0.000643654144f, 0.00183158775f, -0.000199737333f), 1f)), // mesh vertex 53301
            new ContactPoint(new Influence(50, new Vector3(0.0005062998f, 0.002117471f, -8.32158039E-05f), 0.6499437f), new Influence(51, new Vector3(0.0003899391f, 0.000530772f, -0.0005927793f), 0.350056261f)), // mesh vertex 53305
            new ContactPoint(new Influence(50, new Vector3(0.000822616857f, 0.0005163073f, -0.00126442872f), 1f)), // mesh vertex 53383
            new ContactPoint(new Influence(50, new Vector3(0.0007112601f, 0.000246085925f, -0.00149518612f), 1f)), // mesh vertex 53388
            new ContactPoint(new Influence(50, new Vector3(0.0007996926f, 0.0002980612f, -0.0014130784f), 1f)), // mesh vertex 53394
            new ContactPoint(new Influence(50, new Vector3(0.000600016f, 0.000234220483f, -0.00154903566f), 1f)), // mesh vertex 53399
            new ContactPoint(new Influence(50, new Vector3(-0.000743143959f, 0.002025867f, -0.0005079227f), 0.906003654f), new Influence(51, new Vector3(-0.0009086704f, 0.000283023284f, -0.000546863652f), 0.09399639f)), // mesh vertex 53758
            new ContactPoint(new Influence(50, new Vector3(-0.000620038656f, 0.00206543016f, -0.000453765271f), 0.579862535f), new Influence(51, new Vector3(-0.0007809257f, 0.000339608785f, -0.000558375847f), 0.4201375f)), // mesh vertex 53759
            new ContactPoint(new Influence(51, new Vector3(-0.000870886f, 0.000470034924f, -0.0004917293f), 0.96534425f), new Influence(50, new Vector3(-0.000731097651f, 0.00214667176f, -0.0003507543f), 0.0346557759f)), // mesh vertex 53764
            new ContactPoint(new Influence(51, new Vector3(-6.131448E-05f, 0.001355264f, 2.682359E-05f), 1f)), // mesh vertex 53792
            new ContactPoint(new Influence(51, new Vector3(0.000132106492f, 0.001316527f, 7.186865E-05f), 1f)), // mesh vertex 53797
            new ContactPoint(new Influence(51, new Vector3(-0.00022666069f, 0.00131460431f, -8.0380305E-05f), 1f)), // mesh vertex 53798
            new ContactPoint(new Influence(51, new Vector3(-0.000371704751f, 0.0012608493f, -9.276556E-05f), 1f)), // mesh vertex 53799
            new ContactPoint(new Influence(51, new Vector3(0.00037163368f, 0.000685894745f, -0.0005611467f), 0.7003601f), new Influence(50, new Vector3(0.000472253276f, 0.002230725f, 2.36162941E-05f), 0.299639881f)), // mesh vertex 53816
            new ContactPoint(new Influence(50, new Vector3(0.000471123058f, 0.00203770562f, -0.000229777637f), 0.903907955f), new Influence(51, new Vector3(0.000328136084f, 0.000384890474f, -0.0006558605f), 0.09609207f)), // mesh vertex 53824
            new ContactPoint(new Influence(50, new Vector3(0.000436152972f, 0.00215531327f, -0.000130859175f), 0.6260623f), new Influence(51, new Vector3(0.000306505681f, 0.0005392681f, -0.0006326435f), 0.3739377f)), // mesh vertex 53825
            new ContactPoint(new Influence(50, new Vector3(0.000775575638f, 0.0003394257f, -0.001417752f), 1f)), // mesh vertex 53898
            new ContactPoint(new Influence(50, new Vector3(0.000536461652f, 0.00185456278f, -0.000356437668f), 1f)), // mesh vertex 54194
            new ContactPoint(new Influence(50, new Vector3(0.000498666661f, 0.00180214411f, -0.0004489254f), 1f)), // mesh vertex 54197
            new ContactPoint(new Influence(51, new Vector3(-0.000773137959f, 0.0005044996f, -0.0005016891f), 0.9863543f), new Influence(50, new Vector3(-0.0006361372f, 0.00217025029f, -0.00031514134f), 0.0136457225f)), // mesh vertex 54273
            new ContactPoint(new Influence(51, new Vector3(-0.000508125057f, 0.00117846113f, -0.000112566362f), 1f)), // mesh vertex 54290
            new ContactPoint(new Influence(51, new Vector3(-7.038179E-05f, 0.00134346855f, -8.53873353E-05f), 1f)), // mesh vertex 54294
            new ContactPoint(new Influence(51, new Vector3(0.0001236896f, 0.00134639384f, -2.33533847E-05f), 1f)), // mesh vertex 54298
            new ContactPoint(new Influence(51, new Vector3(0.000277137966f, 0.00122094958f, -3.16955E-06f), 1f)), // mesh vertex 54304
            new ContactPoint(new Influence(51, new Vector3(-0.000207669276f, 0.00127531029f, -0.000147364743f), 1f)), // mesh vertex 54308
            new ContactPoint(new Influence(51, new Vector3(-0.000350193819f, 0.00123102264f, -0.000150783235f), 1f)), // mesh vertex 54309
            new ContactPoint(new Influence(51, new Vector3(0.000271532917f, 0.0006713068f, -0.0005782511f), 0.710225642f), new Influence(50, new Vector3(0.00038120072f, 0.00223872554f, -2.29775469E-05f), 0.289774358f)), // mesh vertex 54329
            new ContactPoint(new Influence(50, new Vector3(0.0005011051f, 0.00194566208f, -0.000288875046f), 0.9917428f), new Influence(51, new Vector3(0.000352140371f, 0.0002741278f, -0.0006602035f), 0.008257205f)), // mesh vertex 54330
            new ContactPoint(new Influence(50, new Vector3(0.000344649976f, 0.00203621225f, -0.00027610088f), 0.9223132f), new Influence(51, new Vector3(0.000195023764f, 0.000364409178f, -0.000658121367f), 0.0776868f)), // mesh vertex 54334
            new ContactPoint(new Influence(51, new Vector3(-0.000620314f, 0.00106617587f, -0.000159229472f), 1f)), // mesh vertex 54769
            new ContactPoint(new Influence(51, new Vector3(-0.0004816559f, 0.001164279f, -0.000165359088f), 1f)), // mesh vertex 54772
            new ContactPoint(new Influence(51, new Vector3(0.000105622079f, 0.00133551587f, -0.000120732679f), 1f)), // mesh vertex 54776
            new ContactPoint(new Influence(51, new Vector3(-6.776277E-05f, 0.00131217274f, -0.000147167608f), 1f)), // mesh vertex 54783
            new ContactPoint(new Influence(51, new Vector3(0.000265402516f, 0.0012442515f, -0.000119259144f), 1f)), // mesh vertex 54784
            new ContactPoint(new Influence(50, new Vector3(0.000393773f, 0.00195211545f, -0.0003450193f), 0.9977311f), new Influence(51, new Vector3(0.000234157444f, 0.0002537836f, -0.000679699937f), 0.002268907f)), // mesh vertex 54810
            new ContactPoint(new Influence(51, new Vector3(-0.0005993378f, 0.00104772928f, -0.000203538832f), 1f)), // mesh vertex 55137
            new ContactPoint(new Influence(51, new Vector3(7.19036761E-05f, 0.00128923077f, -0.00018587285f), 1f)), // mesh vertex 55146
            new ContactPoint(new Influence(51, new Vector3(9.234438E-05f, 0.00131747243f, -0.000167004837f), 1f)), // mesh vertex 55147
            new ContactPoint(new Influence(51, new Vector3(0.0002415197f, 0.00122637744f, -0.0002000061f), 1f)), // mesh vertex 55148
        };

        // Assets/_Project/Art/Models/Characters/ROK_Soldier/SK_CH_ROK_Soldier.fbx SHA256 276e821b82b8dbaf3e81bea620ab212876c588cdeb0d50161c1b8f742f9071f6
        const string StudentMeshName = "StudentSoldier_Rigged_Mesh";
        const int StudentVertexCount = 84296;
        static readonly string[] StudentBoneNames = {
            "mixamorig:Hips",
            "mixamorig:Spine",
            "mixamorig:Spine1",
            "mixamorig:Spine2",
            "mixamorig:Neck",
            "mixamorig:Head",
            "mixamorig:LeftShoulder",
            "mixamorig:LeftArm",
            "mixamorig:LeftForeArm",
            "mixamorig:LeftHand",
            "mixamorig:LeftHandThumb1",
            "mixamorig:LeftHandThumb2",
            "mixamorig:LeftHandThumb3",
            "mixamorig:LeftHandIndex1",
            "mixamorig:LeftHandIndex2",
            "mixamorig:LeftHandIndex3",
            "mixamorig:LeftHandMiddle1",
            "mixamorig:LeftHandMiddle2",
            "mixamorig:LeftHandMiddle3",
            "mixamorig:LeftHandRing1",
            "mixamorig:LeftHandRing2",
            "mixamorig:LeftHandRing3",
            "mixamorig:LeftHandPinky1",
            "mixamorig:LeftHandPinky2",
            "mixamorig:LeftHandPinky3",
            "mixamorig:RightShoulder",
            "mixamorig:RightArm",
            "mixamorig:RightForeArm",
            "mixamorig:RightHand",
            "mixamorig:RightHandThumb1",
            "mixamorig:RightHandThumb2",
            "mixamorig:RightHandThumb3",
            "mixamorig:RightHandIndex1",
            "mixamorig:RightHandIndex2",
            "mixamorig:RightHandIndex3",
            "mixamorig:RightHandMiddle1",
            "mixamorig:RightHandMiddle2",
            "mixamorig:RightHandMiddle3",
            "mixamorig:RightHandRing1",
            "mixamorig:RightHandRing2",
            "mixamorig:RightHandRing3",
            "mixamorig:RightHandPinky1",
            "mixamorig:RightHandPinky2",
            "mixamorig:RightHandPinky3",
            "mixamorig:LeftUpLeg",
            "mixamorig:LeftLeg",
            "mixamorig:LeftFoot",
            "mixamorig:LeftToeBase",
            "mixamorig:RightUpLeg",
            "mixamorig:RightLeg",
            "mixamorig:RightFoot",
            "mixamorig:RightToeBase",
        };
        static readonly ContactPoint[] StudentLeft = {
            new ContactPoint(new Influence(46, new Vector3(0.0006020669f, 0.00183142431f, -0.000253383128f), 0.5f), new Influence(47, new Vector3(-0.000488857855f, -2.483123e-05f, -0.0004365419f), 0.5f)), // mesh vertex 15787
            new ContactPoint(new Influence(46, new Vector3(0.0007827459f, 0.0002681779f, 0.0008937456f), 0.529857457f), new Influence(45, new Vector3(-0.000323006941f, 0.00549861137f, 0.000184877921f), 0.470142573f)), // mesh vertex 14964
            new ContactPoint(new Influence(46, new Vector3(0.00112334872f, -0.000137410156f, 0.000478130183f), 0.50000006f), new Influence(45, new Vector3(0.000305065652f, 0.00545144826f, 0.000422655925f), 0.49999994f)), // mesh vertex 15108
            new ContactPoint(new Influence(47, new Vector3(0.000322536333f, 0.0007878553f, -0.000184816658f), 0.8763553f), new Influence(46, new Vector3(-0.000195596542f, 0.00261427579f, 0.000111408459f), 0.123644732f)), // mesh vertex 15612
            new ContactPoint(new Influence(46, new Vector3(0.000720958458f, 0.000147933126f, 0.000901887659f), 0.509438f), new Influence(45, new Vector3(-0.0003309909f, 0.00541607942f, 0.000291965029f), 0.490562022f)), // mesh vertex 14965
            new ContactPoint(new Influence(47, new Vector3(-0.000543689937f, 0.0003673522f, -0.000345448119f), 0.596166f), new Influence(46, new Vector3(0.0004483317f, 0.00214858726f, -0.000455582282f), 0.403833956f)), // mesh vertex 16138
            new ContactPoint(new Influence(46, new Vector3(0.0006011531f, 0.0006028749f, 0.000874076f), 0.6541597f), new Influence(45, new Vector3(-0.000503759f, 0.00544065144f, -0.000145809216f), 0.3458403f)), // mesh vertex 14399
            new ContactPoint(new Influence(46, new Vector3(0.00109930825f, 0.000509395439f, 0.000172916421f), 0.6092519f), new Influence(45, new Vector3(0.000348382775f, 0.00547544565f, -0.00029123656f), 0.3907481f)), // mesh vertex 14773
            new ContactPoint(new Influence(46, new Vector3(0.00107224239f, 0.000465040648f, 2.89190612e-05f), 0.595286f), new Influence(45, new Vector3(0.000456954585f, 0.00536925765f, -0.000310507632f), 0.404714f)), // mesh vertex 14950
            new ContactPoint(new Influence(46, new Vector3(0.0009232905f, 7.0653994e-05f, 0.000813130639f), 0.502027333f), new Influence(45, new Vector3(-0.000125910214f, 0.005514786f, 0.000346610846f), 0.4979727f)), // mesh vertex 14963
            new ContactPoint(new Influence(46, new Vector3(0.00106778683f, -1.28023312e-05f, 0.000657470955f), 0.5000001f), new Influence(45, new Vector3(9.99213e-05f, 0.005531463f, 0.000374803669f), 0.499999881f)), // mesh vertex 14974
            new ContactPoint(new Influence(46, new Vector3(0.001203607f, 4.80973358e-05f, 0.0003887295f), 0.5009139f), new Influence(45, new Vector3(0.0003683933f, 0.00552868936f, 0.0002254996f), 0.4990861f)), // mesh vertex 14979
            new ContactPoint(new Influence(46, new Vector3(0.0012175407f, 0.000143679266f, 0.000268515141f), 0.508249462f), new Influence(45, new Vector3(0.000442937919f, 0.00550962f, 9.185415e-05f), 0.4917505f)), // mesh vertex 14986
            new ContactPoint(new Influence(46, new Vector3(0.000119195131f, 0.00154685f, 0.000815808366f), 0.530335546f), new Influence(47, new Vector3(0.0006211609f, -0.000486853474f, -0.000328413269f), 0.469664454f)), // mesh vertex 15006
            new ContactPoint(new Influence(46, new Vector3(0.0008566402f, -3.25835e-05f, 0.0008051465f), 0.5f), new Influence(45, new Vector3(-0.000128877f, 0.005425444f, 0.000431305933f), 0.49999997f)), // mesh vertex 15112
            new ContactPoint(new Influence(46, new Vector3(0.00120779593f, 8.517285e-05f, 0.000132208646f), 0.5029808f), new Influence(45, new Vector3(0.000559308f, 0.00541713974f, 9.021038e-05f), 0.497019172f)), // mesh vertex 15153
            new ContactPoint(new Influence(46, new Vector3(0.00119825592f, -7.9227415e-05f, 0.00029400154f), 0.50000006f), new Influence(45, new Vector3(0.0004741393f, 0.005439632f, 0.000303602341f), 0.49999994f)), // mesh vertex 15154
            new ContactPoint(new Influence(47, new Vector3(0.0006936229f, 0.0001640192f, -0.000245973817f), 0.518049836f), new Influence(46, new Vector3(-0.000152783963f, 0.00212470186f, 0.0006490894f), 0.481950164f)), // mesh vertex 15166
            new ContactPoint(new Influence(46, new Vector3(-5.575235e-06f, 0.00192554819f, 0.0007213402f), 0.5f), new Influence(47, new Vector3(0.0006569616f, -7.866957e-05f, -0.0003254169f), 0.5f)), // mesh vertex 15168
            new ContactPoint(new Influence(46, new Vector3(0.000208841855f, 0.00154355692f, 0.0007399461f), 0.533369f), new Influence(47, new Vector3(0.0005131426f, -0.000489941332f, -0.0003745087f), 0.466631f)), // mesh vertex 15172
            new ContactPoint(new Influence(47, new Vector3(0.000322125183f, 0.0007905206f, 9.644643e-05f), 0.885465145f), new Influence(46, new Vector3(-0.0004349483f, 0.00251352112f, 3.36104131e-06f), 0.114534862f)), // mesh vertex 15335
            new ContactPoint(new Influence(47, new Vector3(0.000524355f, 0.0006455471f, -0.000116375231f), 0.770418644f), new Influence(46, new Vector3(-0.000309710245f, 0.00249122037f, 0.0003050644f), 0.229581341f)), // mesh vertex 15471
            new ContactPoint(new Influence(47, new Vector3(0.0006442784f, 0.000412603928f, -0.000229659534f), 0.6128402f), new Influence(46, new Vector3(-0.000208337166f, 0.002338579f, 0.0005239275f), 0.387159824f)), // mesh vertex 15473
            new ContactPoint(new Influence(47, new Vector3(9.671286e-05f, 0.0008472686f, 0.000126839572f), 0.9298955f), new Influence(46, new Vector3(-0.0003717424f, 0.00251814048f, -0.00022233637f), 0.07010447f)), // mesh vertex 15474
            new ContactPoint(new Influence(47, new Vector3(0.000126121638f, 0.0008814859f, -6.147968e-05f), 0.9345269f), new Influence(46, new Vector3(-0.000234217325f, 0.00262327446f, -0.000135543247f), 0.06547306f)), // mesh vertex 15617
            new ContactPoint(new Influence(47, new Vector3(0.000591776858f, 0.000406648818f, -0.000277045765f), 0.614160359f), new Influence(46, new Vector3(-0.0001423962f, 0.00234205672f, 0.0004979074f), 0.3858396f)), // mesh vertex 15620
            new ContactPoint(new Influence(46, new Vector3(0.0005854569f, 0.00168355776f, -5.74538171e-05f), 0.506716967f), new Influence(47, new Vector3(-0.000333905919f, -0.000215830936f, -0.0004426688f), 0.493283063f)), // mesh vertex 15784
            new ContactPoint(new Influence(47, new Vector3(0.0004680069f, 0.0006346245f, -0.00023774826f), 0.7675739f), new Influence(46, new Vector3(-0.000177882335f, 0.00251665665f, 0.000305315421f), 0.232426122f)), // mesh vertex 15797
            new ContactPoint(new Influence(47, new Vector3(-0.000460257317f, 0.00072704f, -0.0001816843f), 0.822865f), new Influence(46, new Vector3(0.00017768424f, 0.00243155472f, -0.000554745f), 0.177134976f)), // mesh vertex 16140
            new ContactPoint(new Influence(47, new Vector3(-5.739087e-05f, 0.0008572484f, -0.000204579221f), 0.9227006f), new Influence(46, new Vector3(-2.19776866e-05f, 0.00262403279f, -0.000234010877f), 0.07729943f)), // mesh vertex 16143
            new ContactPoint(new Influence(47, new Vector3(-0.000448387524f, 0.000374033058f, -0.000383413339f), 0.606894255f), new Influence(46, new Vector3(0.0004349899f, 0.0021839512f, -0.000359978469f), 0.393105716f)), // mesh vertex 16157
            new ContactPoint(new Influence(47, new Vector3(-0.0005009864f, 0.0005543547f, -0.00031988023f), 0.705472469f), new Influence(46, new Vector3(0.000358521269f, 0.00231743813f, -0.000485088327f), 0.29452756f)), // mesh vertex 16304
            new ContactPoint(new Influence(47, new Vector3(-0.0003987226f, 0.000746317441f, -0.000268589036f), 0.834757864f), new Influence(46, new Vector3(0.000218176065f, 0.00249097752f, -0.0004738755f), 0.1652421f)), // mesh vertex 16306
            new ContactPoint(new Influence(47, new Vector3(-0.000300296844f, 0.0008516846f, -0.000124018843f), 0.901579f), new Influence(46, new Vector3(2.28190347e-05f, 0.002550345f, -0.000475026667f), 0.09842099f)), // mesh vertex 16308
        };
        static readonly ContactPoint[] StudentRight = {
            new ContactPoint(new Influence(51, new Vector3(-0.0005332456f, 0.0004143874f, -0.000289109274f), 0.6253107f), new Influence(50, new Vector3(0.000107224638f, 0.002344162f, 0.0004490203f), 0.3746893f)), // mesh vertex 16488
            new ContactPoint(new Influence(50, new Vector3(-0.00116570119f, -5.28395e-06f, 0.0004920873f), 0.50000006f), new Influence(49, new Vector3(-0.0002815912f, 0.00553250546f, 0.000311639218f), 0.49999994f)), // mesh vertex 15993
            new ContactPoint(new Influence(50, new Vector3(-0.0008036942f, 0.000199291477f, 0.000901765656f), 0.5163249f), new Influence(49, new Vector3(0.000298205938f, 0.00549905933f, 0.0002529455f), 0.4836751f)), // mesh vertex 16320
            new ContactPoint(new Influence(51, new Vector3(0.000488955469f, 0.0005425459f, -0.0003295687f), 0.6998224f), new Influence(50, new Vector3(-0.000364272739f, 0.002312104f, -0.0004672974f), 0.300177544f)), // mesh vertex 16242
            new ContactPoint(new Influence(50, new Vector3(-0.0012186513f, -3.07591545e-05f, 0.000283874979f), 0.50000006f), new Influence(49, new Vector3(-0.000480103277f, 0.00546584744f, 0.000257288979f), 0.49999994f)), // mesh vertex 15639
            new ContactPoint(new Influence(51, new Vector3(-0.000493384665f, 0.0006523071f, -0.000213443083f), 0.7739649f), new Influence(50, new Vector3(0.00021478039f, 0.00252802833f, 0.000312841963f), 0.2260351f)), // mesh vertex 16528
            new ContactPoint(new Influence(50, new Vector3(-0.00108522444f, 0.000469228165f, 6.56775446e-05f), 0.5957337f), new Influence(49, new Vector3(-0.000434666785f, 0.00539919455f, -0.000298494764f), 0.4042663f)), // mesh vertex 15357
            new ContactPoint(new Influence(50, new Vector3(-0.00106951548f, 0.000509137637f, 0.00022973775f), 0.6108062f), new Influence(49, new Vector3(-0.000287503673f, 0.00547893066f, -0.000271289755f), 0.389193743f)), // mesh vertex 15361
            new ContactPoint(new Influence(50, new Vector3(-0.00122269662f, 0.000115587238f, 0.000160266514f), 0.505403936f), new Influence(49, new Vector3(-0.00053757f, 0.00545222266f, 7.501194e-05f), 0.494596034f)), // mesh vertex 15497
            new ContactPoint(new Influence(50, new Vector3(-0.00061325f, 0.0017812954f, -0.000293716876f), 0.5f), new Influence(51, new Vector3(0.000537280052f, -6.135338e-05f, -0.000412331254f), 0.5f)), // mesh vertex 15694
            new ContactPoint(new Influence(50, new Vector3(-0.0006527453f, 0.001605053f, -0.000109665445f), 0.520891964f), new Influence(51, new Vector3(0.000423076242f, -0.000289266056f, -0.000451185857f), 0.479108036f)), // mesh vertex 15699
            new ContactPoint(new Influence(50, new Vector3(-0.001141044f, -0.000120398392f, 0.00046823296f), 0.5f), new Influence(49, new Vector3(-0.000318054954f, 0.005466149f, 0.000404889463f), 0.49999997f)), // mesh vertex 15817
            new ContactPoint(new Influence(50, new Vector3(-0.000585624f, 0.0005706351f, 0.00088743167f), 0.639643967f), new Influence(49, new Vector3(0.0005139713f, 0.00542486273f, -0.000112562157f), 0.360356033f)), // mesh vertex 15892
            new ContactPoint(new Influence(51, new Vector3(6.9350026e-05f, 0.0008363672f, 0.00016726325f), 0.925568f), new Influence(50, new Vector3(0.000326835754f, 0.002466632f, -0.000379373465f), 0.07443204f)), // mesh vertex 16036
            new ContactPoint(new Influence(51, new Vector3(0.0003444057f, 0.000824759947f, -0.0001541589f), 0.8849716f), new Influence(50, new Vector3(-7.568583e-05f, 0.002529633f, -0.0004938713f), 0.115028389f)), // mesh vertex 16065
            new ContactPoint(new Influence(50, new Vector3(-0.00101972092f, -0.000117849355f, 0.0006511336f), 0.50000006f), new Influence(49, new Vector3(-0.000106727035f, 0.00545758661f, 0.000463581033f), 0.49999994f)), // mesh vertex 16189
            new ContactPoint(new Influence(50, new Vector3(2.11563129e-05f, 0.00178016268f, 0.0007639357f), 0.5f), new Influence(51, new Vector3(-0.0006779523f, -0.000220859569f, -0.000275061931f), 0.49999997f)), // mesh vertex 16209
            new ContactPoint(new Influence(50, new Vector3(-6.207602e-05f, 0.00181627669f, 0.000731981534f), 0.5f), new Influence(51, new Vector3(-0.000617559766f, -0.000199577014f, -0.00034684423f), 0.49999997f)), // mesh vertex 16210
            new ContactPoint(new Influence(51, new Vector3(9.5950476e-05f, 0.0008916496f, -5.11540748e-05f), 0.9380687f), new Influence(50, new Vector3(0.000143437428f, 0.00259310124f, -0.000336479919f), 0.0619313344f)), // mesh vertex 16220
            new ContactPoint(new Influence(51, new Vector3(0.00040264448f, 0.0007491745f, -0.000264738541f), 0.8356479f), new Influence(50, new Vector3(-0.000215970838f, 0.00249155355f, -0.0004796354f), 0.1643521f)), // mesh vertex 16248
            new ContactPoint(new Influence(50, new Vector3(-0.0009316027f, 4.55267145e-05f, 0.00081013306f), 0.5008393f), new Influence(49, new Vector3(0.000111891946f, 0.00551258726f, 0.000369151938f), 0.4991607f)), // mesh vertex 16317
            new ContactPoint(new Influence(50, new Vector3(-0.000196113848f, 0.00143053359f, 0.0008034937f), 0.567566335f), new Influence(51, new Vector3(-0.0005563179f, -0.0006096105f, -0.000346404529f), 0.432433635f)), // mesh vertex 16332
            new ContactPoint(new Influence(51, new Vector3(0.000139203228f, 0.000868435f, -0.000184077027f), 0.920970559f), new Influence(50, new Vector3(4.64215736e-06f, 0.00261361385f, -0.0003166536f), 0.07902946f)), // mesh vertex 16353
            new ContactPoint(new Influence(51, new Vector3(0.000530377845f, 0.0003197739f, -0.000387941429f), 0.573141456f), new Influence(50, new Vector3(-0.000490619f, 0.00212271f, -0.000413289439f), 0.4268585f)), // mesh vertex 16359
            new ContactPoint(new Influence(51, new Vector3(-0.000682429f, 0.000218974717f, -0.0002602925f), 0.5320771f), new Influence(50, new Vector3(0.000149711486f, 0.00217851135f, 0.000627981557f), 0.467922926f)), // mesh vertex 16431
            new ContactPoint(new Influence(51, new Vector3(-0.0006112935f, 0.0004605945f, -0.000240707435f), 0.641415536f), new Influence(50, new Vector3(0.000196211142f, 0.00238130381f, 0.000484675082f), 0.358584464f)), // mesh vertex 16433
            new ContactPoint(new Influence(51, new Vector3(-0.000369786838f, 0.000761979842f, 8.83684042e-05f), 0.8660607f), new Influence(50, new Vector3(0.00044262415f, 0.00249799225f, 5.675945e-05f), 0.1339393f)), // mesh vertex 16441
            new ContactPoint(new Influence(51, new Vector3(-0.000106140891f, 0.000877246261f, -0.000130213884f), 0.930932462f), new Influence(50, new Vector3(0.000165555408f, 0.0026413803f, -0.000125578546f), 0.06906753f)), // mesh vertex 16444
            new ContactPoint(new Influence(51, new Vector3(-0.000352097675f, 0.000796523f, -9.98739852e-05f), 0.880182862f), new Influence(50, new Vector3(0.000283577741f, 0.00259582512f, 0.000102298545f), 0.119817123f)), // mesh vertex 16495
            new ContactPoint(new Influence(51, new Vector3(-4.393833e-05f, 0.0008282331f, -0.0002233792f), 0.916379035f), new Influence(50, new Vector3(4.511931e-05f, 0.00262062787f, -0.0001295316f), 0.08362095f)), // mesh vertex 16500
            new ContactPoint(new Influence(51, new Vector3(-0.000304366666f, 0.000783071155f, -0.0001992452f), 0.876835942f), new Influence(50, new Vector3(0.000173743581f, 0.00261226157f, 0.000102485596f), 0.123164035f)), // mesh vertex 16533
        };

    }
}

