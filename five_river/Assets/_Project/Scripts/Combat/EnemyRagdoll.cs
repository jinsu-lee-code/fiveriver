using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 부분 래그돌 · 맞는 순간 꺾임 (부족한 점 3 · 6번, 2026-11-26).
    // 살아 있는 동안: 래그돌 몸 조각은 Kinematic, 충돌체 꺼짐 (사격 · 길찾기 · 엄폐 판정에 영향 없음).
    // 죽는 순간(Arm): 맞은 부위를 총알 반대쪽으로 0.25초 꺾고(애니메이션 위에 덧붙임), 사망 동작은 그대로 재생한다.
    // 엉덩이가 땅 가까이 내려오면(또는 정한 시간) 물리로 넘긴다: 동작을 끄고, 뼈가 움직이던 속도를 이어받아
    // 비탈 · 참호 가장자리 · 모래주머니 위에 걸쳐 눕는다. 폭발로 죽으면 처음부터 물리 + 폭발 힘.
    // 멈추면 Kinematic · 충돌체 끔(Freeze). EnemyAgent.Settle 이 나머지(동작 계산 멈춤)를 한다.
    // 관절 제한은 메뉴 FiveRivers/Setup/Enemy Ragdoll 이 만든 CharacterJoint(Unity 래그돌 기본 각도).
    [DefaultExecutionOrder(100)] // after the animator, FallenPose and LyingBodyRest have posed him this frame
    public sealed class EnemyRagdoll : MonoBehaviour
    {
        [SerializeField] BodyPhysicsConfig config;
        [SerializeField] Animator animator;
        [SerializeField] Rigidbody[] bodies;

        enum Phase { Alive, Armed, Physics, Frozen }
        Phase phase;
        Collider[] colliders;
        Vector3[] lastPosition;
        Quaternion[] lastRotation;
        float lastTime, armedAt, physicsAt, stillFor;
        Transform hips, chest, head, neck, spine, lUpperArm, lLowerArm, lHand, rUpperArm, rLowerArm, rHand, lUpperLeg, lLowerLeg, lFoot, rUpperLeg, rLowerLeg, rFoot;
        Transform flinchBone;
        float flinchAngle;
        Vector3 shotDirection;
        HitReaction.Part part;
        bool blast;
        Vector3 blastPoint;
        Terrain ground;

        public bool Simulating => phase == Phase.Physics;
        public bool SkippedNearCover { get; private set; }
        public bool HasPhysics => phase == Phase.Physics || phase == Phase.Frozen && !SkippedNearCover;
        public HitReaction.Part LastPart => part;
        public Rigidbody[] Bodies => bodies;
        public BodyPhysicsConfig Config => config;

        public void Configure(BodyPhysicsConfig bodyConfig, Animator owner, Rigidbody[] parts)
        {
            config = bodyConfig;
            animator = owner;
            bodies = parts;
        }

        void Awake()
        {
            if (animator == null || bodies == null || bodies.Length == 0 || config == null) { enabled = false; return; }
            colliders = new Collider[bodies.Length];
            lastPosition = new Vector3[bodies.Length];
            lastRotation = new Quaternion[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                colliders[i] = bodies[i].GetComponent<Collider>();
                bodies[i].isKinematic = true;
                if (colliders[i] != null) colliders[i].enabled = false;
            }
            if (animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                lUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                lLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                lUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                lLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                rLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            enabled = false; // nothing to do until he dies
        }

        // the part a point on him belongs to (nearest bone segment)
        public HitReaction.Part PartAt(Vector3 point)
        {
            if (hips == null || head == null) return HitReaction.Part.Chest;
            var headTop = head.position + (head.position - neck.position) * 2.2f;
            var from = new[] { spine.position, neck.position, hips.position, lUpperArm.position, lLowerArm.position, rUpperArm.position, rLowerArm.position, lUpperLeg.position, lLowerLeg.position, rUpperLeg.position, rLowerLeg.position };
            var to = new[] { neck.position, headTop, spine.position, lLowerArm.position, lHand.position, rLowerArm.position, rHand.position, lLowerLeg.position, lFoot.position, rLowerLeg.position, rFoot.position };
            var parts = new[] { HitReaction.Part.Chest, HitReaction.Part.Head, HitReaction.Part.Hips, HitReaction.Part.LeftArm, HitReaction.Part.LeftArm,
                HitReaction.Part.RightArm, HitReaction.Part.RightArm, HitReaction.Part.LeftLeg, HitReaction.Part.LeftLeg, HitReaction.Part.RightLeg, HitReaction.Part.RightLeg };
            return HitReaction.Classify(point, from, to, parts);
        }

        // He is dead. shotFrom: where the round came from; point: where it struck (or the blast centre)
        public void Arm(Vector3 shotFrom, Vector3 point, bool fromBlast)
        {
            if (phase != Phase.Alive || bodies == null || bodies.Length == 0 || colliders == null) return;
            ground = Terrain.activeTerrain;
            blast = fromBlast;
            blastPoint = point;
            var centre = chest != null ? chest.position : transform.position + Vector3.up;
            shotDirection = centre - shotFrom;
            if (shotDirection.sqrMagnitude < 1e-4f) shotDirection = -transform.forward;
            shotDirection.Normalize();
            part = blast ? HitReaction.Part.Chest : PartAt(point);
            flinchBone = null;
            if (!blast)
            {
                switch (part)
                {
                    case HitReaction.Part.Head: flinchBone = head; flinchAngle = config.flinchHead; break;
                    case HitReaction.Part.Chest: flinchBone = chest; flinchAngle = config.flinchChest; break;
                    case HitReaction.Part.Hips: flinchBone = hips; flinchAngle = config.flinchHips; break;
                    case HitReaction.Part.LeftArm: flinchBone = lUpperArm; flinchAngle = config.flinchArm; break;
                    case HitReaction.Part.RightArm: flinchBone = rUpperArm; flinchAngle = config.flinchArm; break;
                    case HitReaction.Part.LeftLeg: flinchBone = lUpperLeg; flinchAngle = config.flinchLeg; break;
                    case HitReaction.Part.RightLeg: flinchBone = rUpperLeg; flinchAngle = config.flinchLeg; break;
                }
            }
            phase = Phase.Armed;
            armedAt = Time.time;
            lastTime = -1f;
            enabled = true;
        }

        void LateUpdate()
        {
            if (phase == Phase.Armed)
            {
                Flinch();
                if (blast || ReadyToFall()) { Handover(); return; }
                Record();
                return;
            }
            if (phase == Phase.Physics)
            {
                HoldAboveGround();
                HoldHinges();
                float most = 0f;
                foreach (var b in bodies) most = Mathf.Max(most, b.linearVelocity.magnitude);
                stillFor = most < config.restSpeed ? stillFor + Time.deltaTime : 0f;
                if (stillFor >= config.restHold || Time.time - physicsAt >= config.physicsMost) Freeze();
            }
        }

        void Flinch()
        {
            if (flinchBone == null) return;
            float w = HitReaction.Flinch(Time.time - armedAt, config.flinchRise, config.flinchFade);
            if (w <= 0f) return;
            // the bone tips away from the shooter about the axis across its length and the shot
            var along = flinchBone == hips ? Vector3.up : flinchBone == head ? (head.position - neck.position) : FirstChildDirection(flinchBone);
            var axis = Vector3.Cross(along, shotDirection);
            if (axis.sqrMagnitude < 1e-6f) return;
            flinchBone.rotation = Quaternion.AngleAxis(flinchAngle * w, axis.normalized) * flinchBone.rotation;
        }

        static Vector3 FirstChildDirection(Transform bone)
        {
            for (int i = 0; i < bone.childCount; i++)
            {
                var d = bone.GetChild(i).position - bone.position;
                if (d.sqrMagnitude > 1e-6f) return d;
            }
            return bone.up;
        }

        bool ReadyToFall()
        {
            float t = Time.time - armedAt;
            if (t >= config.handoverLatest) return true;
            if (chest == null || t < config.handoverEarliest) return false;
            float floor = ground != null ? ground.SampleHeight(chest.position) + ground.transform.position.y : chest.position.y - 1f;
            return chest.position.y - floor < config.handoverChest;
        }

        // An art rock or sandbag with no collider of its own close to him: physics would let him sink into it.
        // There the death clip and LyingBodyRest (which slide him out of it) keep the last word.
        readonly Collider[] near = new Collider[16];
        bool NearHollowCover()
        {
            var far = transform.position + Vector3.up * 1000f;
            foreach (var bone in new[] { hips, chest, head, lLowerLeg, rLowerLeg, lFoot, rFoot, lHand, rHand })
            {
                if (bone == null) continue;
                var p = bone.position;
                if (!CoverVisuals.Blocks(p, config.coverClear, far)) continue;
                bool solid = false;
                int n = Physics.OverlapSphereNonAlloc(p, config.coverClear + 0.3f, near, FiveRivers.Core.WorldQuery.Solid, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n && !solid; i++)
                {
                    var c = near[i];
                    if (c is TerrainCollider || c.attachedRigidbody != null || c.transform.IsChildOf(transform)) continue;
                    solid = true;
                }
                if (!solid) return true;
            }
            return false;
        }

        void Record()
        {
            for (int i = 0; i < bodies.Length; i++) { lastPosition[i] = bodies[i].position; lastRotation[i] = bodies[i].rotation; }
            lastTime = Time.time;
        }

        void Handover()
        {
            // far off (100 m out in the field) the animator updates his bones seldom: the handed-over speeds were stale and
            // the bodies flew (20-45 m/s) and sank (up to 1.5 m), full run 2026-11-26. Out there no one sees the difference.
            // measured from the player's own eye: Camera.main is null while a cutscene has his camera off, and the check
            // was then skipped (two men 101 m out went to physics, one knee bent 54 degrees the wrong way, 2026-11-26)
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null || hips == null || (player.CameraPivot.position - hips.position).sqrMagnitude > config.physicsRange * config.physicsRange)
            { SkippedNearCover = true; phase = Phase.Frozen; enabled = false; return; }
            // on a steep hillside a man folded on his knees keeps a shin in the slope (37 cm on 20-22 degrees)
            if (!blast && ground != null && SlopeUnder(hips.position) > config.maxSlope)
            { SkippedNearCover = true; phase = Phase.Frozen; enabled = false; return; }
            if (!blast && NearHollowCover()) { SkippedNearCover = true; phase = Phase.Frozen; enabled = false; return; }
            // folded on a steep slope the clip can have a shin deep in the hillside: physics would keep it buried
            // (seen 36-39 cm on a 22-degree slope, every run). Leave him to the clip and LyingBodyRest there.
            if (!blast && -LowestClearance() > config.sunkSkip) { SkippedNearCover = true; phase = Phase.Frozen; enabled = false; return; }
            // flares and the most men at once: no more than so many bodies in the physics together; the rest keep the clip
            if (simulating >= config.maxSimulating) { SkippedNearCover = true; phase = Phase.Frozen; enabled = false; return; }
            var rest = animator.GetComponent<LyingBodyRest>();
            if (rest != null) rest.enabled = false;
            var fallen = animator.GetComponent<FallenPose>();
            if (fallen != null) fallen.enabled = false;
            var hands = animator.GetComponent<EnemyHandIK>();
            if (hands != null) hands.enabled = false;
            animator.enabled = false;
            // the clip may have laid a hand or a boot a little into the slope: lift him out of it before the physics
            // starts (a collider that begins under the ground can be pushed further in, seen 37 cm on a 32-degree slope)
            float sunk = -LowestClearance();
            if (sunk > 0f) animator.transform.position += Vector3.up * Mathf.Min(sunk + 0.01f, config.liftMost);
            float dt = lastTime > 0f ? Mathf.Max(1f / 120f, Time.time - lastTime) : 0f;
            IgnoreTheLiving();
            for (int i = 0; i < bodies.Length; i++)
            {
                var b = bodies[i];
                b.isKinematic = false;
                if (colliders[i] != null) colliders[i].enabled = true;
                if (dt > 0f)
                {
                    b.linearVelocity = Vector3.ClampMagnitude((b.position - lastPosition[i]) / dt, config.inheritMaxSpeed);
                    (b.rotation * Quaternion.Inverse(lastRotation[i])).ToAngleAxis(out float angle, out var axis);
                    if (angle > 180f) angle -= 360f;
                    if (!float.IsNaN(axis.x)) b.angularVelocity = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / dt), config.inheritMaxSpin);
                }
                b.WakeUp();
            }
            if (blast)
                foreach (var b in bodies) b.AddExplosionForce(config.blastSpeed, blastPoint, config.blastRadius, config.blastLift, ForceMode.VelocityChange);
            else
            {
                var struck = BodyFor(part);
                if (struck != null) struck.AddForce(shotDirection * config.bulletPush, ForceMode.VelocityChange);
            }
            phase = Phase.Physics;
            physicsAt = Time.time;
            stillFor = 0f;
            simulating++;
            counted = true;
        }

        // How many bodies are in the physics now (the cap is BodyPhysicsConfig.maxSimulating)
        static int simulating;
        bool counted;
        public static int SimulatingNow => simulating;

        void Uncount()
        {
            if (!counted) return;
            counted = false;
            simulating = Mathf.Max(0, simulating - 1);
        }

        void OnDestroy() => Uncount();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnEnterPlayMode] static void ResetCount() => simulating = 0; // domain reload may be off
#endif

        // The body is no one's to bump: not the player walking past, not the squad, not the men still coming on
        // (all on the Default layer with the ground). NavMesh agents do not use colliders at all.
        void IgnoreTheLiving()
        {
            var keep = new System.Collections.Generic.List<Collider>();
            foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None)) keep.Add(cc);
            foreach (var ally in FindObjectsByType<AllyCombatant>(FindObjectsSortMode.None)) keep.AddRange(ally.GetComponentsInChildren<Collider>());
            foreach (var enemy in FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None))
                if (enemy != null && enemy.Alive) { var c = enemy.GetComponent<Collider>(); if (c != null) keep.Add(c); }
            foreach (var mine in colliders)
                if (mine != null) foreach (var other in keep) if (other != null) Physics.IgnoreCollision(mine, other, true);
        }

        // the lowest point of any of his colliders above the ground under it (negative: in the ground)
        float LowestClearance()
        {
            if (ground == null) return 0f;
            float least = float.MaxValue;
            foreach (var b in bodies)
            {
                var c = b.GetComponent<Collider>();
                float s = b.transform.lossyScale.x;
                if (c is CapsuleCollider cap)
                {
                    var dir = b.transform.TransformDirection(cap.direction == 0 ? Vector3.right : cap.direction == 1 ? Vector3.up : Vector3.forward);
                    var centre = b.transform.TransformPoint(cap.center);
                    float r = cap.radius * s, half = Mathf.Max(0f, cap.height * 0.5f * s - r);
                    least = Mathf.Min(least, Clear(centre + dir * half, r), Clear(centre - dir * half, r));
                }
                else if (c is SphereCollider sphere) least = Mathf.Min(least, Clear(b.transform.TransformPoint(sphere.center), sphere.radius * s));
                else if (c is BoxCollider box)
                    for (int i = 0; i < 8; i++)
                        least = Mathf.Min(least, Clear(b.transform.TransformPoint(box.center + Vector3.Scale(box.size * 0.5f,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))), 0f));
            }
            return least == float.MaxValue ? 0f : least;
        }

        static PlayerController player;

        // Knees and elbows never bend the wrong way. The joint limits hold nearly always, but a hard landing can push a
        // hinge past its stop (one knee 13 degrees, hill run 2026-11-26): each frame in the physics a hinge read more
        // than hingeSlack the wrong way is turned back to straight, and its spin is taken off.
        // The bend is read about the joint's own axis (+ = the way it flexes; checked against the kneeling clip's knees).
        public static int HingeFixes; // review tools: how many times a hinge was turned back
        CharacterJoint[] hinges;
        void HoldHinges()
        {
            if (hinges == null)
            {
                var list = new System.Collections.Generic.List<CharacterJoint>();
                foreach (var b in bodies) { var j = b.GetComponent<CharacterJoint>(); if (j != null && j.lowTwistLimit.limit > 0f) list.Add(j); }
                hinges = list.ToArray();
            }
            bool moved = false;
            foreach (var j in hinges)
            {
                var lower = j.transform; var upper = j.connectedBody != null ? j.connectedBody.transform : null;
                if (upper == null || lower.childCount == 0) continue;
                var end = lower.GetChild(0).position;
                var axis = lower.TransformDirection(j.axis);
                float bend = -Vector3.SignedAngle(lower.position - upper.position, end - lower.position, axis);
                if (bend >= -config.hingeSlack) continue;
                lower.rotation = Quaternion.AngleAxis(bend, axis) * lower.rotation;
                var rb = lower.GetComponent<Rigidbody>();
                if (rb != null && j.connectedBody != null) rb.angularVelocity = j.connectedBody.angularVelocity;
                moved = true;
                HingeFixes++;
            }
            if (moved) Physics.SyncTransforms();
        }

        float SlopeUnder(Vector3 p)
        {
            var d = ground.terrainData; var o = ground.transform.position;
            var n = d.GetInterpolatedNormal((p.x - o.x) / d.size.x, (p.z - o.z) / d.size.z);
            return Vector3.Angle(n, Vector3.up);
        }

        // In the physics: no part may lie deeper than sinkHold in the ground; if one does, the whole body is lifted
        // back to that depth (the lowest point of a rigid ragdoll sank 8-37 cm on slopes and after a near blast)
        void HoldAboveGround()
        {
            float sunk = -LowestClearance();
            if (sunk <= config.sinkHold) return;
            // move the whole model at once (its transforms move now, all parts together, no joint is stretched).
            // Moving each Rigidbody.position instead only took effect at the next physics step: every frame in between
            // read the same depth and lifted again, the lifts piled up, an elbow was torn 22 degrees the wrong way and
            // the physics step spiked to 5.7 ms (full run 2026-11-26).
            animator.transform.position += Vector3.up * (sunk - config.sinkHold);
            Physics.SyncTransforms();
            foreach (var b in bodies) if (b.linearVelocity.y < 0f) b.linearVelocity = new Vector3(b.linearVelocity.x, 0f, b.linearVelocity.z);
        }

        float Clear(Vector3 p, float radius) => p.y - radius - (ground.SampleHeight(p) + ground.transform.position.y);

        Rigidbody BodyFor(HitReaction.Part p)
        {
            Transform bone = p == HitReaction.Part.Head ? head : p == HitReaction.Part.Hips ? hips : p == HitReaction.Part.LeftArm ? lUpperArm
                : p == HitReaction.Part.RightArm ? rUpperArm : p == HitReaction.Part.LeftLeg ? lUpperLeg : p == HitReaction.Part.RightLeg ? rUpperLeg : chest;
            for (var t = bone; t != null; t = t.parent)
                foreach (var b in bodies) if (b.transform == t) return b;
            return null;
        }

        // Comes to rest: no more physics, the pose stays as it lies
        public void Freeze()
        {
            if (phase == Phase.Frozen || phase == Phase.Alive && bodies == null) return;
            Uncount();
            if (bodies != null)
                for (int i = 0; i < bodies.Length; i++)
                {
                    if (bodies[i] == null) continue;
                    if (!bodies[i].isKinematic) { bodies[i].linearVelocity = Vector3.zero; bodies[i].angularVelocity = Vector3.zero; }
                    bodies[i].isKinematic = true;
                    if (colliders[i] != null) colliders[i].enabled = false;
                }
            phase = phase == Phase.Alive ? Phase.Alive : Phase.Frozen;
            enabled = false;
        }
    }
}
