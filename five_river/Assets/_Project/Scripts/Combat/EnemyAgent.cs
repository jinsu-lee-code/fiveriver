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
                animator.SetFloat(SpeedId, agent.velocity.magnitude);
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

        void Die()
        {
            state = State.Dead;
            deathTime = Time.time;
            agent.isStopped = true;
            agent.enabled = false;
            if (body != null) body.enabled = false;
            model.localScale = Vector3.one;
            if (animator != null)
            {
                animator.SetInteger(DieIndexId, Random.Range(0, 3));
                animator.SetTrigger(DieId);
            }
            Gone?.Invoke(this);
            Destroy(gameObject, 8f);
        }

        void Leave()
        {
            state = State.Dead;
            Gone?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
