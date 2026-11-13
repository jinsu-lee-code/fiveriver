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
        // 2026-11-13 플레이 중 측정: 1배속으로 3.4 m/s 전진하면 디딘 발이 0.85 m/s 앞으로 미끄러짐 → 적 리그(0.876)에서 2.55 m/s
        // re-keyed clip (2026-11-13 evening): measured in play the planted foot travels 2.00 m/s at 1x on the enemy rig
        // (human scale x 0.876) -> 2.28 at human scale 1
        public static float CrouchRunClipSpeed = 2.28f;
        const float CrouchRunFrom = 1.2f, MinCrouchAnimSpeed = 0.7f, MaxCrouchAnimSpeed = 2.0f;
        static readonly int CrouchRunStateId = Animator.StringToHash("CrouchRun");
        float crouchFootSpeed = 2.55f;
        // The crouched run clip runs on a diagonal: measured in play its hips face 26 deg to the left of the way it
        // goes, so driven straight ahead the legs crossed over like a side-step coming down the slope (2026-11-13).
        // While it plays the model turns that much to the right, so the hips and stride face the way he runs.
        // 2026-11-13 evening: the clip itself was re-keyed in Blender (source/animation/kpa_crouch_run): hips and feet
        // now face the way he runs and the hands carry the Mosin like a combat idle (butt by the right hip, muzzle
        // forward and ~17 deg up). So no turn and no carry override are needed any more; both stay as switches.
        public static float CrouchRunYaw = 0f;
        public static bool CrouchCarryOverride = false;
        public static Vector3 CrouchCarryEuler = new Vector3(24f, -6f, 0f); // only with CrouchCarryOverride
        Quaternion modelRestRotation = Quaternion.identity;
        float crouchTurn;

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
        Transform leftFoot, rightFoot, leftToes, rightToes;
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
                // in the prefab's own pose the soles stand on the model's origin: how high the joints sit above them
                if (leftFoot != null && model != null)
                {
                    footSole = Mathf.Clamp(leftFoot.position.y - model.position.y, 0.03f, 0.2f);
                    toeSole = leftToes != null ? Mathf.Clamp(leftToes.position.y - model.position.y, 0.01f, 0.12f) : footSole;
                }
            }
            if (model != null) { modelRest = model.localPosition; modelRestRotation = model.localRotation; }
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
            if (handIK != null) handIK.FeetOnSlope = state != State.Dead && GroundFeet;
            if (state != State.Dead) { TurnCrouchRun(); KeepFeetOnGround(); }
            if (rifle == null || rifleDropped || rightHand == null || leftHand == null) return;
            // The clips hold the hands close together, so the bare hand-to-hand line lies across the chest.
            // Aim the barrel through a point ahead of the support hand instead: the rifle points forward and the
            // left hand stays on the stock.
            // aim from the animated (pre-IK) support hand: the IK pulls that hand onto the rifle, so using the IK'd
            // hand here would feed back and droop the muzzle a little more every frame
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
            // Crouched run: the clip keeps the rifle shouldered and level, so he came down the slope aiming at the sky
            // line the whole way. Running he holds it at the low ready instead: butt at the shoulder, muzzle down
            // and forward along the way he runs (2026-11-13).
            float crouchCarry = Mathf.SmoothStep(0f, 1f, crouchTurn);
            if (CrouchCarryOverride && crouchCarry > 0f) rotation = Quaternion.Slerp(rotation, transform.rotation * Quaternion.Euler(CrouchCarryEuler), crouchCarry);
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
            if (handIK != null) handIK.Shape(1f, 1f - Mathf.Max(carry, crouchCarry));
        }

        void TurnCrouchRun()
        {
            if (model == null || animator == null) return;
            float target = StateWeight(CrouchRunStateId);
            crouchTurn = Mathf.MoveTowards(crouchTurn, target, Time.deltaTime * 4f);
            model.localRotation = modelRestRotation * Quaternion.Euler(0f, CrouchRunYaw * Mathf.SmoothStep(0f, 1f, crouchTurn), 0f);
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
            float target = Mathf.Clamp(groundOffset - lowest, GroundLowest, GroundHighest);
            groundOffset = Mathf.Lerp(groundOffset, target, 1f - Mathf.Exp(-GroundRate * Time.deltaTime));
            model.localPosition = modelRest + model.parent.InverseTransformVector(Vector3.up * groundOffset);
        }

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
            rb.linearVelocity = -transform.forward * Random.Range(0.4f, 1.0f) + Vector3.up * 0.6f;
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

        void Die()
        {
            state = State.Dead;
            deathTime = Time.time;
            agent.isStopped = true;
            agent.enabled = false;
            if (body != null) body.enabled = false;
            if (animator == null) model.localScale = Vector3.one;
            if (animator != null)
            {
                animator.speed = 1f; // 달리다 쓰러져도 사망 동작은 원래 속도로
                animator.SetInteger(DieIndexId, forcedDeath >= 0 ? forcedDeath : Random.Range(0, 3));
                animator.SetTrigger(DieId);
                LyingBodyRest.Attach(animator); // the body comes to rest on the ground, not on the NavMesh above it
            }
            DropRifle();
            Gone?.Invoke(this);
            Destroy(gameObject, 8f);
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
