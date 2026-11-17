using System;
using FiveRivers.Data;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace FiveRivers.Combat
{
    // 북한군 보병 하나. 엄폐 지점으로 달려가 숨었다가 일어나 쏘고, 철수 명령이면 뒤로 달린다.
    // Animator가 있으면 ANIM_SPEC 1-5 매개변수(AC_KPA)를 채운다. 그레이박스에서는 모델 높이로 자세를 보인다.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyAgent : MonoBehaviour, IDamageable
    {
        enum State { Advance, Cover, Retreat, Dead }

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int AimId = Animator.StringToHash("Aim");
        static readonly int FireId = Animator.StringToHash("Fire");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DieId = Animator.StringToHash("Die");
        static readonly int DieIndexId = Animator.StringToHash("DieIndex");
        static readonly int RetreatId = Animator.StringToHash("Retreat");
        static readonly int RunStateId = Animator.StringToHash("Run");
        // A_KPA_Run_Rifle의 디딘 발은 사람 크기 1인 리그 기준 약 5.2 m/s로 움직인다 (2026-11-10 플레이 중 측정,
        // HillAllies.RunClipSpeed와 같은 값. 적 리그 0.88에서는 약 4.6 m/s). 전진(3.4 m/s)을 원래 속도로 재생하면
        // 디딘 발이 초당 1.4 m씩 뒤로 미끄러졌다. 지면 속도에 재생 속도를 맞춘다.
        const float RunClipSpeed = 5.2f, MinRunAnimSpeed = 0.72f, MaxRunAnimSpeed = 1.3f;
        float runFootSpeed = RunClipSpeed;
        // Advancing on the trench (2026-11-13): the upright sprint clip, slowed to 0.75 to keep its feet from
        // sliding, read as a slow-motion hop with the rifle held across the chest like a bar. Coming forward
        // under fire they now run crouched with the rifle at the shoulder (A_KPA_CrouchRun, already in AC_KPA but
        // only reached from crouched cover). A retreat stays the upright run. CrouchRunClipSpeed is how fast its
        // planted foot travels at a human scale of 1, measured in play like RunClipSpeed.
        public static bool CrouchAdvance = true;
        // The enemies play A_KPA_CrouchRun_Charge (shorter contact, longer flight; source/animation/kpa_crouch_run),
        // so the 3.4 m/s advance plays at about 1.27x. The allies keep A_KPA_CrouchRun for their slower advance.
        public static float CrouchRunClipSpeed = 3.61f; // measured in play 2026-11-15: at 3.06 the planted foot slid back 0.8 m/s
        const float CrouchRunFrom = 1.2f, MinCrouchAnimSpeed = 0.7f, MaxCrouchAnimSpeed = 2.0f;
        static readonly int CrouchRunStateId = Animator.StringToHash("CrouchRun");
        static readonly int CrouchAimStateId = Animator.StringToHash("Crouch_Aim");
        static readonly int FireCrouchStateId = Animator.StringToHash("Fire_Crouch");
        float crouchFootSpeed = 2.55f;
        // Advancing, the rifle is carried at port arms by code (CrouchCarryEuler); the clip's own hand line arrives
        // almost level through the humanoid retarget.
        public static bool CrouchCarryOverride = true;
        // crouched run: rock of the carried rifle in degrees (x nod at each footfall, y swing and z roll once a stride)
        public static Vector3 CrouchRock = new Vector3(4f, 3f, 2f);
        // crouched run: where the trigger wrist goes from the right shoulder joint (agent axes: right, up, forward).
        // Measured in play 2026-11-15: this rig's arm is only 0.35 m from shoulder to wrist, so the rifle has to lie
        // close in and across the body or neither hand reaches it (the support hand hung in the air above it).
        public static Vector3 CrouchTriggerHand = new Vector3(0f, -0.27f, 0.08f);
        public static float CrouchTriggerWeight = 0.85f;
        public static Vector3 CrouchCarryEuler = new Vector3(-22f, -35f, 0f); // port arms: across the body, butt by the right hip, muzzle up and left (2026-11-15)
        float crouchTurn, advanceCarry;

        [SerializeField] Transform model;
        [SerializeField] Transform muzzle;
        [SerializeField] Animator animator;
        [SerializeField] float standUpBeforeShot = 0.7f;
        [SerializeField] float stayUpAfterShot = 0.6f;

        EnemyDirector owner;
        WaveConfig config;
        NavMeshAgent agent;
        Collider body;
        State state;
        float health;
        float nextShot;
        float coverUntil;
        float standUntil;
        bool shotPending;
        float deathTime;

        // The Mosin used to sit on a fixed chest socket, so it hung in the air when the rifleman crouched or fell.
        // It now rides in the hands (right hand on the wrist, left hand forward on the stock) and is dropped on death.
        public static float GripForward = 0.07f;   // wrist -> grip, along the rifle
        public static float GripBehindCentre = 0.17f; // grip sits this far behind the rifle's mesh centre
        public static float MuzzleAhead = 0.6f;
        public static float AimAhead = 0.5f;          // barrel passes this far ahead of the left hand       // barrel end ahead of the mesh centre
        public static Vector3 RunCarryEuler = new Vector3(18f, -32f, 0f); // low ready while running, relative to the body
        public static Vector3 TriggerWrist = new Vector3(0.03f, -0.05f, 0f); // trigger wrist from the grip point, rifle space
        Transform rifle, rightHand, leftHand;
        EnemyHandIK handIK;

        // Feet on the ground (2026-11-13). The agent walks on the NavMesh, which lies 13-21 cm above the slopes
        // (it is a simplified surface), and the run clip itself carries the body a little high: measured in play,
        // the lowest foot never came within 24 cm of the ground, so every rifleman ran on air. Each frame the
        // model is lowered (or raised) so the lower sole sits on the real ground under it.
        public static bool GroundFeet = true;
        const float GroundRate = 14f, GroundLowest = -0.6f, GroundHighest = 0.25f;
        Transform leftFoot, rightFoot, leftToes, rightToes, leftKnee, rightKnee;
        // the knee joint sits this far above the ground when the knee rests on it (kneeling to aim and fire)
        const float KneeClear = 0.07f;
        float lastKneel; // how much of a kneeling state was on last frame (a man hit kneeling goes down from there)
        float footSole, toeSole, groundOffset;
        Vector3 modelRest;
        Terrain ground;
        Vector3 rifleCentreLocal;
        bool rifleDropped;

        public Transform Cover { get; private set; }
        public bool Alive => state != State.Dead;
        public bool Retreating => state == State.Retreat;
        public bool IsScout { get; set; }
        public bool Standing { get; private set; }

        public event Action<EnemyAgent> Gone;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            body = GetComponent<Collider>();
            rifle = transform.Find("Socket_Rifle/Rifle");
            if (animator != null && animator.isHuman)
            {
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                runFootSpeed = RunClipSpeed * animator.humanScale * animator.transform.lossyScale.y;
                crouchFootSpeed = CrouchRunClipSpeed * animator.humanScale * animator.transform.lossyScale.y;
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
                leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                // in the prefab's own pose the soles stand on the model's origin: how high the joints sit above them
                if (leftFoot != null && model != null)
                {
                    footSole = Mathf.Clamp(leftFoot.position.y - model.position.y, 0.03f, 0.2f);
                    toeSole = leftToes != null ? Mathf.Clamp(leftToes.position.y - model.position.y, 0.01f, 0.12f) : footSole;
                }
            }
            if (model != null) modelRest = model.localPosition;
            var mesh = rifle != null ? rifle.GetComponentInChildren<MeshFilter>() : null;
            if (mesh != null && mesh.sharedMesh != null)
                rifleCentreLocal = rifle.InverseTransformPoint(mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center));
            if (rifle != null && rightHand != null)
            {
                handIK = animator.GetComponent<EnemyHandIK>();
                if (handIK == null) handIK = animator.gameObject.AddComponent<EnemyHandIK>();
                handIK.Bind(animator, rifle, rifleCentreLocal, GripBehindCentre, MuzzleAhead);
            }
        }

        void LateUpdate()
        {
            if (deathTurning)
            {
                if (state != State.Dead || Time.time - deathTime > DeathTurnFor) deathTurning = false;
                else transform.rotation = Quaternion.RotateTowards(transform.rotation, deathFacing, DeathTurnRate * Time.deltaTime);
            }
            if (handIK != null) handIK.FeetOnSlope = state != State.Dead && GroundFeet;
            if (state != State.Dead) { FollowCrouchRun(); KeepFeetOnGround(); }
            if (rifle == null || rifleDropped || rightHand == null || leftHand == null) return;
            // The clips hold the hands close together, so the bare hand-to-hand line lies across the chest.
            // Aim the barrel through a point ahead of the support hand instead: the rifle points forward and the
            // left hand stays on the stock.
            // aim from the animated (pre-IK) support hand: the IK pulls that hand onto the rifle, so using the IK'd
            // hand here would feed back and droop the muzzle a little more every frame
            // Advancing, the rifle is carried at port arms (set here, not by the clip: through the humanoid retarget
            // the clip's own hand line arrives almost level). The carry is on for the whole advance, the upright
            // walk of the first steps included; the stoop waits for the crouched run.
            float run = Mathf.SmoothStep(0f, 1f, crouchTurn);
            advanceCarry = Mathf.MoveTowards(advanceCarry, CrouchCarryOverride && state == State.Advance && CrouchAdvance ? 1f : 0f, Time.deltaTime * 4f);
            float crouchCarry = CrouchCarryOverride ? Mathf.Max(run, Mathf.SmoothStep(0f, 1f, advanceCarry)) : 0f;
            float stride = animator != null ? animator.GetCurrentAnimatorStateInfo(0).normalizedTime * Mathf.PI * 2f : 0f;
            if (handIK != null && crouchCarry > 0f)
            {
                Stoop(run);
                // the rifle's weight lags the body: the hand sinks a little after each footfall
                handIK.ReachTrigger(CrouchTriggerHand + Vector3.up * (CrouchHandBob * Mathf.Sin(stride * 2f - 0.6f)), crouchCarry * CrouchTriggerWeight);
            }
            var support = handIK != null && handIK.HasAnimatedHand ? handIK.AnimatedLeftHand : leftHand.position;
            var along = support + transform.forward * AimAhead - rightHand.position;
            if (along.sqrMagnitude < 0.0025f) return;
            var rotation = Quaternion.LookRotation(along, transform.up);
            // Running, the rifle is not pointed: it is carried low and ready, muzzle down and to the left, which is
            // where the run clip's own hands lead (held out level in front it looked like a bar pushed ahead of
            // him; a high port puts it across his face because the clip keeps the trigger hand at chest height).
            // The carry eases in and out with the run clip.
            float carry = RunCarryWeight();
            if (carry > 0f) rotation = Quaternion.Slerp(rotation, transform.rotation * Quaternion.Euler(RunCarryEuler), carry);
            // advancing: port arms, and it rocks with the stride. The muzzle dips at each footfall and swings toward the leading leg
            if (crouchCarry > 0f)
            {
                var euler = CrouchCarryEuler + new Vector3(CrouchRock.x * Mathf.Sin(stride * 2f), CrouchRock.y * Mathf.Sin(stride), CrouchRock.z * Mathf.Sin(stride));
                rotation = Quaternion.Slerp(rotation, transform.rotation * Quaternion.Euler(euler), crouchCarry);
            }
            // Running, the wood passes above and to the left of the trigger wrist: the palm, not the wrist bone, lies
            // on it (with the wrist on the axis the hand sat on top of the receiver). Aiming keeps the clip's grip.
            var grip = rightHand.position + rotation * new Vector3(-TriggerWrist.x * carry, -TriggerWrist.y * carry, GripForward);
            // the rifle's grip point (behind its centre on the local z axis) goes to the grip
            var gripLocal = rifleCentreLocal + Vector3.back * GripBehindCentre;
            rifle.SetPositionAndRotation(grip - rotation * Vector3.Scale(gripLocal, rifle.lossyScale), rotation);
            // muzzle flash and shot origin follow the barrel end
            if (muzzle != null) muzzle.SetPositionAndRotation(rifle.TransformPoint(rifleCentreLocal + Vector3.forward * MuzzleAhead), rotation);
            // support hand under the fore-end, palms onto the wood, fingers closed round it. First only while running;
            // seen close in the flare sequence the aiming clips also left the support hand open and floating under
            // the rifle (2026-11-13), so the hands now always hold it while he is alive.
            if (handIK != null) handIK.Shape(1f, 1f - Mathf.Max(carry, crouchCarry), crouchCarry);
        }

        // Period footage and accounts (2026-11-15): a 1950 rifleman going forward under fire stoops low and carries
        // the rifle in both hands across the body (port arms); a butt held in the shoulder on the run is a modern habit.
        // The clip stands him fairly upright, so the chest is bent forward here and the head lifted back to look
        // where he runs.
        public static float CrouchStoop = 12f, CrouchHandBob = 0.012f;
        Transform stoopSpine, stoopNeck;
        void Stoop(float weight)
        {
            if (animator == null || !animator.isHuman || weight <= 0f || CrouchStoop == 0f) return;
            if (stoopSpine == null) { stoopSpine = animator.GetBoneTransform(HumanBodyBones.Spine); stoopNeck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? animator.GetBoneTransform(HumanBodyBones.Head); }
            if (stoopSpine == null) return;
            stoopSpine.rotation = Quaternion.AngleAxis(CrouchStoop * weight, transform.right) * stoopSpine.rotation;
            if (stoopNeck != null) stoopNeck.rotation = Quaternion.AngleAxis(-CrouchStoop * 0.7f * weight, transform.right) * stoopNeck.rotation;
        }

        // how far into the crouched run he is, eased (drives the stoop and the port-arms carry)
        void FollowCrouchRun()
        {
            if (animator == null) return;
            crouchTurn = Mathf.MoveTowards(crouchTurn, StateWeight(CrouchRunStateId), Time.deltaTime * 4f);
        }

        // How much of the given state is on screen (follows the controller's cross-fades).
        float StateWeight(int stateId)
        {
            if (animator == null || state == State.Dead) return 0f;
            bool now = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateId;
            if (!animator.IsInTransition(0)) return now ? 1f : 0f;
            bool next = animator.GetNextAnimatorStateInfo(0).shortNameHash == stateId;
            float t = Mathf.SmoothStep(0f, 1f, animator.GetAnimatorTransitionInfo(0).normalizedTime);
            return now ? (next ? 1f : 1f - t) : next ? t : 0f;
        }

        void KeepFeetOnGround()
        {
            if (!GroundFeet || model == null || leftFoot == null || rightFoot == null) return;
            if (ground == null) ground = Terrain.activeTerrain;
            if (ground == null) return;
            // the bones already include last frame's offset, so this is the gap as it stands now
            float lowest = Mathf.Min(SoleGap(leftFoot, leftToes), SoleGap(rightFoot, rightToes));
            // Kneeling (aim or fire from a crouch), the down knee is on the ground too. With the feet alone, on a
            // slope the front toes touched first and the knee and the rear foot hung 20-25 cm in the air (2026-11-16).
            float kneel = Mathf.Max(StateWeight(CrouchAimStateId), StateWeight(FireCrouchStateId));
            // The clip holds that knee 8-14 cm above the plane of the front sole, so the body is set down on the
            // down knee and the toes of that leg instead, and the front foot is kept on top of the ground by the
            // foot IK (EnemyHandIK.Kneel), bending that knee a little more.
            if (kneel > 0f && leftKnee != null && rightKnee != null)
            {
                float left = KneeGap(leftKnee), right = KneeGap(rightKnee);
                var downToes = left < right ? leftToes : rightToes;
                float rest = Mathf.Min(left, right) - KneeClear;
                if (downToes != null) rest = Mathf.Min(rest, downToes.position.y - toeSole - (ground.SampleHeight(downToes.position) + ground.transform.position.y));
                lowest = Mathf.Lerp(lowest, rest, kneel);
            }
            if (handIK != null) { handIK.Kneel = kneel; handIK.SoleHeight = footSole; }
            lastKneel = kneel;
            float target = Mathf.Clamp(groundOffset - lowest, GroundLowest, GroundHighest);
            groundOffset = Mathf.Lerp(groundOffset, target, 1f - Mathf.Exp(-GroundRate * Time.deltaTime));
            model.localPosition = modelRest + model.parent.InverseTransformVector(Vector3.up * groundOffset);
        }

        float KneeGap(Transform knee) => knee.position.y - (ground.SampleHeight(knee.position) + ground.transform.position.y);

        float SoleGap(Transform foot, Transform toes)
        {
            float sole = foot.position.y - footSole;
            if (toes != null) sole = Mathf.Min(sole, toes.position.y - toeSole);
            var p = foot.position;
            return sole - (ground.SampleHeight(p) + ground.transform.position.y);
        }

        // How much of the Run state is on screen (follows the controller's cross-fades).
        float RunCarryWeight()
        {
            if (animator == null || state == State.Dead) return 0f;
            bool now = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == RunStateId;
            if (!animator.IsInTransition(0)) return now ? 1f : 0f;
            bool next = animator.GetNextAnimatorStateInfo(0).shortNameHash == RunStateId;
            float t = Mathf.SmoothStep(0f, 1f, animator.GetAnimatorTransitionInfo(0).normalizedTime);
            return now ? (next ? 1f : 1f - t) : next ? t : 0f;
        }

        // Let go of the rifle: it falls and settles on the ground with the body, then is cleared with it.
        void DropRifle()
        {
            if (rifle == null || rifleDropped) return;
            rifleDropped = true;
            if (handIK != null) handIK.Holding = false;
            // under the director, so ClearAll removes it with the bodies on retry / checkpoint
            rifle.SetParent(owner != null ? owner.transform : null, true);
            rifle.gameObject.layer = 2; // Ignore Raycast: never blocks sight lines or shots
            var box = rifle.gameObject.AddComponent<BoxCollider>();
            var rb = rifle.gameObject.AddComponent<Rigidbody>();
            rb.mass = 4f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // thrown clear to one side: dropped straight down (as it was until 2026-11-15) the body came down on top of
            // it and it lay run through his chest in the flare's last shot. Only a little upward: tossed up at 1.3 m/s
            // it hung at head height for a third of a second after the man was already down (2026-11-17 evening).
            rb.linearVelocity = transform.right * (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.7f, 2.3f) + transform.forward * Random.Range(0.2f, 0.7f) + Vector3.up * 0.35f;
            rb.angularVelocity = Random.insideUnitSphere * 2.5f;
            if (body != null) Physics.IgnoreCollision(box, body);
            Destroy(rifle.gameObject, 8f);
        }

        public void Init(EnemyDirector director, WaveConfig waveConfig, Transform cover)
        {
            owner = director;
            config = waveConfig;
            health = config.health;
            agent.speed = config.moveSpeed;
            MoveToCover(cover);
        }

        public void MoveToCover(Transform cover)
        {
            if (!Alive || Retreating) return;
            Cover = cover;
            state = State.Advance;
            Standing = true;
            agent.isStopped = false;
            agent.SetDestination(cover.position);
        }

        public void Retreat(Transform point)
        {
            if (!Alive || Retreating) return;
            state = State.Retreat;
            Standing = true;
            agent.speed = config.retreatSpeed;
            agent.isStopped = false;
            agent.SetDestination(point.position);
        }

        void Update()
        {
            if (state == State.Dead)
            {
                CarryOn();
                // 그레이박스 쓰러짐: 0.6초 동안 뒤로 눕는다 (A_KPA_Death_01 대신)
                if (animator == null)
                    model.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -88f, Mathf.Clamp01((Time.time - deathTime) / 0.6f)), 0f, 0f);
                return;
            }

            switch (state)
            {
                case State.Advance:
                    if (!agent.pathPending && agent.remainingDistance <= 0.6f) EnterCover();
                    break;
                case State.Cover:
                    UpdateCover();
                    break;
                case State.Retreat:
                    if (!agent.pathPending && agent.remainingDistance <= 1.5f) Leave();
                    break;
            }

            ApplyPose();
        }

        void EnterCover()
        {
            state = State.Cover;
            agent.isStopped = true;
            Standing = false;
            coverUntil = Time.time + Random.Range(config.coverHoldMin, config.coverHoldMax);
            nextShot = Time.time + Random.Range(config.fireIntervalMin, config.fireIntervalMax) * 0.5f;
        }

        void UpdateCover()
        {
            FacePlayer();
            if (!shotPending && Time.time >= nextShot)
            {
                Standing = true; // 일어나서 쏜다 (엄폐물 위로 몸이 보인다)
                shotPending = true;
                standUntil = Time.time + standUpBeforeShot;
            }
            if (shotPending && Time.time >= standUntil)
            {
                shotPending = false;
                Shoot();
                standUntil = Time.time + stayUpAfterShot;
                nextShot = Time.time + Random.Range(config.fireIntervalMin, config.fireIntervalMax);
            }
            if (!shotPending && Standing && Time.time >= standUntil) Standing = false;
            if (Time.time >= coverUntil && !shotPending)
            {
                coverUntil = Time.time + Random.Range(config.coverHoldMin, config.coverHoldMax);
                owner.RequestAdvance(this);
            }
        }

        void FacePlayer()
        {
            var to = owner.PlayerEye - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 240f * Time.deltaTime);
        }

        void Shoot()
        {
            owner.EnemyShot(this, muzzle.position);
            if (animator != null) animator.SetTrigger(FireId);
        }

        void ApplyPose()
        {
            if (animator != null)
            {
                float speed = agent.velocity.magnitude;
                animator.SetFloat(SpeedId, speed);
                int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
                // 전진은 숙인 달리기, 철수는 선 달리기 (2026-11-13). 멈추면 컨트롤러가 CrouchRun → Crouch_Aim으로 나간다
                if (CrouchAdvance && !animator.IsInTransition(0))
                {
                    if (state == State.Advance && speed > CrouchRunFrom && current != CrouchRunStateId) animator.CrossFade(CrouchRunStateId, 0.2f);
                    else if (state == State.Retreat && current == CrouchRunStateId) animator.CrossFade(RunStateId, 0.2f);
                }
                // 달리기 상태에서만 지면 속도에 재생 속도를 맞춘다 (사격 · 피격 등 다른 동작은 원래 속도)
                animator.speed = current == RunStateId ? Mathf.Clamp(speed / runFootSpeed, MinRunAnimSpeed, MaxRunAnimSpeed)
                    : current == CrouchRunStateId ? Mathf.Clamp(speed / crouchFootSpeed, MinCrouchAnimSpeed, MaxCrouchAnimSpeed) : 1f;
                animator.SetBool(CrouchId, state == State.Cover && !Standing);
                animator.SetBool(AimId, state == State.Cover);
                animator.SetBool(RetreatId, state == State.Retreat);
                return;
            }
            // 그레이박스: 엄폐 중 앉으면 몸을 낮춘다
            float target = state == State.Cover && !Standing ? 0.6f : 1f;
            var s = model.localScale;
            s.y = Mathf.MoveTowards(s.y, target, Time.deltaTime * 3f);
            model.localScale = s;
        }

        public void TakeDamage(float amount, Vector3 from)
        {
            if (!Alive) return;
            health -= amount;
            if (health <= 0f) Die();
            else if (animator != null) animator.SetTrigger(HitId);
        }

        // A staged death (FlareRevealDirector): falls with the given death clip (AC_KPA DieIndex: 0 Death_01 2.7 s,
        // 1 Death_02 1.6 s, 2 Death_03 3.2 s) so a short shot sees him reach the ground.
        public const int QuickDeath = 1;
        int forcedDeath = -1;
        public void Kill(int deathIndex)
        {
            if (!Alive) return;
            forcedDeath = deathIndex;
            health = 0f;
            Die();
        }

        // The death clips all begin with the man standing upright and still (Death_01 for 0.30 s, Death_02 for 0.25 s;
        // Death_03 reacts from 0.05 s). Joined at their start over 0.05 s, a man shot on the run sprang to attention
        // and stood for a third of a second before he went down (seen frame by frame, 2026-11-17). Each clip is now
        // joined where its body starts to give way, cross-faded from the pose he was in.
        static readonly int[] DeathStateIds = { Animator.StringToHash("Death_01"), Animator.StringToHash("Death_02"), Animator.StringToHash("Death_03") };
        static readonly float[] DeathJoin = { 0.27f, 0.22f, 0.04f };
        // Death_03 then staggers on the spot for half a second (hips level until 0.6 s) before the legs go: fine for a
        // man shot standing, but one shot on the run stopped and swayed. Running, it is joined just before the drop.
        // Filmed again frame by frame (2026-11-17 evening): joined at 0.27 / 0.22 / 0.42 s a man hit in the crouched
        // run still straightened up 5-9 cm and stood 0.30-0.57 s before he went down. Joined where each clip's hips
        // start to fall (0.70 / 0.42 / 0.60 s) he folds out of the run within 0.10-0.17 s and never rises.
        static readonly float[] DeathJoinRunning = { 0.70f, 0.42f, 0.60f };
        const float DeathBlendRunning = 0.16f, DeathBlendStill = 0.12f;
        // A running man does not stop dead where he is hit: he is carried on a little way as he falls.
        const float RunningAbove = 1.5f, CarryShare = 0.55f, CarryFade = 0.18f, CarryFor = 0.6f, CarryReach = 0.35f;
        Vector3 carry;
        bool hitRunning;

        void CarryOn()
        {
            if (carry.sqrMagnitude < 0.0004f || Time.time - deathTime > CarryFor) return;
            var from = transform.position;
            var to = from + carry * Time.deltaTime;
            // keep his height over the ground as it was (he stood on the NavMesh, a little above the slope)
            if (ground != null) to.y += ground.SampleHeight(to) - ground.SampleHeight(from);
            transform.position = to;
            carry *= Mathf.Exp(-Time.deltaTime / CarryFade);
        }

        void Die()
        {
            var moving = agent.enabled ? agent.velocity : Vector3.zero; moving.y = 0f;
            hitRunning = moving.magnitude > RunningAbove;
            carry = hitRunning ? moving * CarryShare : Vector3.zero;
            if (ground == null) ground = Terrain.activeTerrain;
            state = State.Dead;
            deathTime = Time.time;
            agent.isStopped = true;
            agent.enabled = false;
            if (body != null) body.enabled = false;
            if (animator == null) model.localScale = Vector3.one;
            if (animator != null)
            {
                animator.speed = 1f; // 달리다 쓰러져도 사망 동작은 원래 속도로
                int clip = forcedDeath >= 0 ? forcedDeath : PickDeath();
                animator.SetInteger(DieIndexId, clip);
                if (clip >= 0 && clip < DeathStateIds.Length && animator.HasState(0, DeathStateIds[clip]))
                    animator.CrossFadeInFixedTime(DeathStateIds[clip], hitRunning ? DeathBlendRunning : DeathBlendStill, 0, hitRunning ? DeathJoinRunning[clip] : DeathJoin[clip]);
                else animator.SetTrigger(DieId); // a controller without these states: its own Any State transition
                var rest = LyingBodyRest.Attach(animator); // the body comes to rest on the ground, not on the NavMesh above it
                if (rest != null && lastKneel > 0.5f) rest.HoldKneel(); // hit kneeling: he falls from there, not from standing
            }
            DropRifle();
            Gone?.Invoke(this);
            Destroy(gameObject, 8f);
        }

        // Where each death clip lays the body, in the man's frame at the moment he is hit (measured 2026-11-16):
        // Death_01 falls back (head 1.23 m behind), Death_02 and Death_03 fall forward (head 1.05 / 1.37 m, hands
        // 1.56 m ahead). A man kneeling behind his rock or sandbags faces it, so falling forward he went through it.
        static readonly Vector3[] DeathReach = { new Vector3(0.2f, 0f, -1.35f), new Vector3(0.45f, 0f, 1.15f), new Vector3(0f, 0f, 1.6f) };
        static readonly Collider[] fallHits = new Collider[8];
        static readonly int[] deathOrder = { 0, 1, 2 };
        static readonly float[] DeathTurns = { 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        const float DeathTurnRate = 300f, DeathTurnFor = 0.6f;
        Quaternion deathFacing;
        bool deathTurning;

        // A clip whose fall stays clear of cover. If every clip is blocked, he twists as he goes down (over the
        // first 0.4 s, which reads as the hit turning him) toward the nearest way that is clear.
        int PickDeath()
        {
            for (int i = deathOrder.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (deathOrder[i], deathOrder[j]) = (deathOrder[j], deathOrder[i]); }
            // shot on the run he pitches forward the way he was going: the fall onto his back (Death_01) comes last
            if (hitRunning)
            {
                int back = System.Array.IndexOf(deathOrder, 0);
                for (int i = back; i < deathOrder.Length - 1; i++) deathOrder[i] = deathOrder[i + 1];
                deathOrder[deathOrder.Length - 1] = 0;
            }
            foreach (int clip in deathOrder)
                if (FallsClear(clip, transform.rotation)) return clip;
            foreach (float turn in DeathTurns)
                foreach (int clip in deathOrder)
                {
                    var facing = transform.rotation * Quaternion.Euler(0f, turn, 0f);
                    if (!FallsClear(clip, facing)) continue;
                    deathFacing = facing;
                    deathTurning = true;
                    return clip;
                }
            return deathOrder[0];
        }

        bool FallsClear(int clip, Quaternion facing)
        {
            var start = transform.position + Vector3.up * 0.45f;
            var end = transform.position + facing * DeathReach[clip] + Vector3.up * 0.35f;
            if (hitRunning) end += carry.normalized * CarryReach; // he is carried on this far as he falls
            int n = Physics.OverlapCapsuleNonAlloc(start, end, 0.22f, fallHits, FiveRivers.Core.WorldQuery.Solid, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = fallHits[i];
                if (c is TerrainCollider || c.attachedRigidbody != null || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<EnemyAgent>() != null || c.GetComponentInParent<CharacterController>() != null
                    || c.GetComponentInParent<AllyCombatant>() != null) continue;
                return false;
            }
            // and the art rocks and sandbags that have no collider of their own (CoverVisuals); the one he knelt
            // right behind does not count against falling the other way
            for (int k = 2; k <= 6; k++)
                if (CoverVisuals.Blocks(Vector3.Lerp(start, end, k / 6f), 0.2f, start)) return false;
            return true;
        }

        void Leave()
        {
            if (rifle != null && rifleDropped) Destroy(rifle.gameObject);
            state = State.Dead;
            Gone?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
