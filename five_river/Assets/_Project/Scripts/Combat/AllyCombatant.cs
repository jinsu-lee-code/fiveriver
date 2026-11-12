using FiveRivers.Audio;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Defensive supporting fire is presentation only: it cannot damage, kill, or advance a goal.
    // Cadence and range share WaveConfig; no extra difficulty or progression system is introduced.
    public sealed class AllyCombatant : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] EnemyDirector enemies;
        [SerializeField] LevelPoints defencePoints;
        [SerializeField] WaveConfig config;
        [SerializeField] PlayerHealth playerHealth;
        [SerializeField] Effects effects;
        [SerializeField] AudioDirector audioDirector;
        [SerializeField] Animator animator;
        [SerializeField] Transform weapon;
        [SerializeField] Transform muzzle;
        [SerializeField] Vector3 gripLocal = new Vector3(0f, -0.035f, -0.16f);
        [SerializeField] float turnSpeed = 160f;
        [SerializeField] float fireCueDelay = 0.1f;
        // Fire_Crouch palms point 33.6 degrees left of the body's forward axis.
        [SerializeField] float rifleYawOffset = -33.6f;
        // From the tutorial on, with no enemy in sight, allies fire suppressing rounds at the far slope
        // (2026-10-30 user exception to the 10/30 freeze). Presentation only, like targeted fire.
        [SerializeField] bool fireFromStart = true;
        [SerializeField] Vector2 suppressDistance = new Vector2(45f, 85f);
        [SerializeField] float suppressSpread = 25f;
        [SerializeField] float suppressIntervalScale = 1.4f;

        static readonly int FireId = Animator.StringToHash("Fire");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int RunRifleId = Animator.StringToHash("Run_Rifle");
        static readonly int CrouchRunRifleId = Animator.StringToHash("CrouchRun_Rifle");
        Transform rightHand, leftHand;
        Quaternion restingRotation;
        Vector3 defensiveForward;
        EnemyAgent target;
        float nextShot, cueAt, nextPick = float.MaxValue;
        bool pending, engaged, suppressing;
        Vector3 suppressAim;
        AllyRiflePose riflePose;
        AlliedRifleFit rifleFit;
        float recoilAt = -100f;

        // Set by HillAllies while he walks up the ridge: no turning or firing, rifle in the carry pose.
        public bool Advancing { get; set; }
        // Kneeling to fire: HillAllies leaves his facing alone while this is true.
        public bool Engaged => engaged;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int ShotsFired { get; private set; }
        public int MuzzleEffectsSpawned { get; private set; }
        public int AudioCuesRequested { get; private set; }
        public string TargetName => target != null ? target.name : "";
#endif

        void Start()
        {
            if (defencePoints == null) defencePoints = FindFirstObjectByType<LevelPoints>();
            defensiveForward = ApproachDirection(transform.position, defencePoints, transform.forward);
            restingRotation = Quaternion.LookRotation(defensiveForward, Vector3.up);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rifleFit = animator.GetComponent<AlliedRifleFit>();
            riflePose = new AllyRiflePose(animator, transform, weapon);
            director.Flow.SectionChanged += OnSectionChanged;
            director.Flow.RetryRequested += OnRetry;
            ResetCombat(true);
            started = true;
        }

        void OnDestroy()
        {
            if (director == null || director.Flow == null) return;
            director.Flow.SectionChanged -= OnSectionChanged;
            director.Flow.RetryRequested -= OnRetry;
        }

        bool started;
        void OnEnable()
        {
            if (started) ResetCombat(false); // re-enabled after a section change it missed while off
        }

        void OnDisable()
        {
            if (animator != null) StopFiring();
        }

        void OnSectionChanged(FlowTransition transition) => ResetCombat(
            transition.To == FlowSection.Title || transition.To == FlowSection.Tutorial);
        void OnRetry(FlowCheckpoint checkpoint) => ResetCombat(true);

        public void ResetForNewRun() => ResetCombat(true);

        void ResetCombat(bool resetCounts)
        {
            if (!isActiveAndEnabled) return;
            StopFiring();
            recoilAt = -100f;
            transform.rotation = restingRotation;
            nextShot = Time.time + Random.Range(config.fireIntervalMin, config.fireIntervalMax) * 0.5f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (resetCounts) ShotsFired = MuzzleEffectsSpawned = AudioCuesRequested = 0;
#endif
        }

        bool CombatActive => director.Flow.IsRunning && !director.Flow.Paused && !director.Flow.CombatLocked
            && playerHealth.Current > 0f && Time.timeScale > 0f
            && (director.Flow.Section == FlowSection.Scout || director.Flow.Section == FlowSection.Assault
                || director.Flow.Section == FlowSection.WestAssault
                || director.Flow.Section == FlowSection.HillAssault && !Advancing);

        // Park keeps his authored standing carry while the player learns the controls.
        bool TutorialCarryPose => director.Flow.Section == FlowSection.Tutorial
            && rifleFit != null && rifleFit.isActiveAndEnabled && rifleFit.correctCarryPose;

        bool SuppressActive => !TutorialCarryPose && fireFromStart && director.Flow.IsRunning && !director.Flow.Paused && !director.Flow.CombatLocked
            && playerHealth.Current > 0f && Time.timeScale > 0f
            && (director.Flow.Section == FlowSection.Tutorial || director.Flow.Section == FlowSection.Scout
                || director.Flow.Section == FlowSection.Assault);

        void Update()
        {
            if (director.Flow.Paused) return;
            if (Advancing)
            {
                StopFiring();
                return;
            }
            if (!CombatActive) target = null;
            else if (target == null || !target.Alive || target.Retreating) target = FindTarget();
            if (target == null && !SuppressActive)
            {
                StopFiring();
                if (TutorialCarryPose) animator.SetBool(CrouchId, false);
                return;
            }
            if (target == null && (!suppressing || (!pending && Time.time >= nextPick))) PickSuppressAim();
            suppressing = target == null;
            engaged = true;
            animator.SetBool(CrouchId, true);
            var direction = AimPoint() - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            var aimRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -rifleYawOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, aimRotation, turnSpeed * Time.deltaTime);
            if (pending || Time.time < nextShot || Quaternion.Angle(transform.rotation, aimRotation) > 8f) return;
            if (!suppressing && !CanSee(target)) { target = FindTarget(); nextShot = Time.time + 0.25f; return; }
            animator.SetTrigger(FireId);
            pending = true;
            cueAt = Time.time + fireCueDelay;
            nextShot = Time.time + Random.Range(config.fireIntervalMin, config.fireIntervalMax) * (suppressing ? suppressIntervalScale : 1f);
            // Hold the aim through the shot, then sweep to the next spot well before the next round.
            nextPick = Mathf.Min(cueAt + 0.5f, nextShot - 1f);
        }

        Vector3 AimPoint() => target != null
            ? target.transform.position + Vector3.up * (target.Standing ? 1.25f : 0.75f)
            : suppressAim;

        // The source idle pose can face the player (Park's authored yaw is 90 degrees).
        // The enemy entry sector, rather than that pose, defines the defensive front.
        public static Vector3 ApproachDirection(Vector3 origin, LevelPoints points, Vector3 fallback)
        {
            var centre = Vector3.zero;
            int count = 0;
            if (points != null && points.enemySpawns != null)
                foreach (var spawn in points.enemySpawns)
                    if (spawn != null) { centre += spawn.position; count++; }
            var direction = count > 0 ? centre / count - origin : fallback;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        // Suppression stays in front of the defence even when an authored actor faces sideways.
        void PickSuppressAim()
        {
            nextPick = float.MaxValue;
            var forward = defensiveForward;
            forward.y = 0f;
            forward = Quaternion.Euler(0f, Random.Range(-suppressSpread, suppressSpread), 0f) * forward.normalized;
            var point = transform.position + forward * Random.Range(suppressDistance.x, suppressDistance.y);
            suppressAim = Physics.Raycast(point + Vector3.up * 80f, Vector3.down, out var hit, 160f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.6f
                : point + Vector3.up * 2f;
        }

        EnemyAgent FindTarget()
        {
            EnemyAgent nearest = null;
            float distance = config.sightRange * config.sightRange;
            foreach (var candidate in enemies.ActiveEnemies)
            {
                if (candidate == null || !candidate.Alive || candidate.Retreating) continue;
                float d = (candidate.transform.position - transform.position).sqrMagnitude;
                if (d >= distance || !CanSee(candidate)) continue;
                nearest = candidate;
                distance = d;
            }
            return nearest;
        }

        bool CanSee(EnemyAgent candidate)
        {
            var origin = muzzle.position;
            var aim = candidate.transform.position + Vector3.up * (candidate.Standing ? 1.25f : 0.75f);
            if ((aim - origin).sqrMagnitude > config.sightRange * config.sightRange) return false;
            return !Physics.Linecast(origin, aim, out var hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<EnemyAgent>() == candidate;
        }

        // How much of one of HillAllies' moving states is on screen: follows the cross-fade in and out of it,
        // so the moving rifle carry eases in with the legs instead of snapping.
        float StateWeight(int state)
        {
            bool now = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == state;
            if (!animator.IsInTransition(0)) return now ? 1f : 0f;
            bool next = animator.GetNextAnimatorStateInfo(0).shortNameHash == state;
            float t = Mathf.SmoothStep(0f, 1f, animator.GetAnimatorTransitionInfo(0).normalizedTime);
            return now ? (next ? 1f : 1f - t) : next ? t : 0f;
        }

        void LateUpdate()
        {
            if (director.Flow.Paused) return;
            // Both palms come from the actual crouch/fire clip, including its recoil. While he kneels to fire the rifle
            // stays shouldered even on the frame a target is lost (it used to drop into the standing carry).
            if (engaged)
                riflePose.Apply(AimPoint(), 0.022f * Mathf.Exp(-(Time.time - recoilAt) * 24f));
            else if (rightHand != null && leftHand != null && weapon != null)
            {
                var section = director.Flow.Section;
                bool portArms = director.Flow.IsExpanded && (section == FlowSection.HillAssault || section == FlowSection.Dawn)
                    && riflePose.ApplyPortArms(Advancing ? StateWeight(CrouchRunRifleId) : 0f, Advancing ? StateWeight(RunRifleId) : 0f);
                if (!portArms && !riflePose.TryApplyCarry())
                {
                    var forward = leftHand.position - rightHand.position;
                    if (forward.sqrMagnitude > 0.001f)
                    {
                        var rotation = Quaternion.LookRotation(forward, transform.up);
                        weapon.SetPositionAndRotation(rightHand.position - rotation * gripLocal, rotation);
                    }
                }
            }
            if (!engaged && (director.Flow.Section == FlowSection.Title || director.Flow.Section == FlowSection.Tutorial))
                riflePose.LookAt(transform.position + defensiveForward * 60f + Vector3.up * 1.4f);
            if (!pending || Time.time < cueAt) return;
            pending = false;
            if (suppressing ? !SuppressActive
                : !CombatActive || target == null || !target.Alive || target.Retreating || !CanSee(target)) return;
            var flash = effects.Spawn(EffectKind.MuzzleFlashM1, muzzle.position, muzzle.rotation);
            audioDirector.PlaySpatial("SFX_M1_Fire", muzzle.position);
            recoilAt = Time.time;
            // Suppressing rounds hit nearby ground, never an actor or a gameplay hit target.
            var ground = suppressing ? suppressAim + Random.insideUnitSphere * 1.5f
                : target.transform.position + target.transform.right * Random.Range(0.7f, 1.4f);
            if (Physics.Raycast(ground + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<EnemyAgent>() == null
                && hit.collider.GetComponentInParent<PlayerHealth>() == null)
                effects.Spawn(EffectKind.ImpactDirt, hit.point, Quaternion.LookRotation(hit.normal));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ShotsFired++;
            if (flash != null) MuzzleEffectsSpawned++;
            AudioCuesRequested++;
#endif
        }

        void CancelCue()
        {
            pending = false;
            animator.ResetTrigger(FireId);
        }

        void StopFiring()
        {
            CancelCue();
            target = null;
            suppressing = false;
            if (!engaged) return;
            engaged = false;
            animator.SetBool(CrouchId, false);
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Fire_Crouch")
                || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Fire_Crouch")))
                animator.CrossFade("Idle_Stand", 0.1f);
        }
    }
}


