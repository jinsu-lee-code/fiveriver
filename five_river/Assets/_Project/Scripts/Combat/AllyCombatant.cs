using FiveRivers.Audio;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Defensive supporting fire is presentation only: it cannot damage, kill, or advance a goal.
    // Cadence and range share WaveConfig; no extra difficulty or progression system is introduced.
    // 2026-11-18 (general run only): each aimed round is reported to EnemyDirector.AllyShotAt, which may let it bring
    // down a man beyond the player's share of the assault (never one who is firing at the player). The exhibit run
    // is unchanged: there the squad's fire still cannot kill.
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
        static readonly int AimStandId = Animator.StringToHash("Aim_Stand"), FireStandId = Animator.StringToHash("Fire_Stand");
        // (2026-11-23, asked for) The allies fire standing at the parapet, not from one knee: with a man to shoot at,
        // or firing at the slope, they stand in the aim (Aim_Stand / Fire_Stand). They still go down on one knee to
        // watch the ridge on the hill and under a barrage. False: the kneeling fire as before.
        public static bool StandToFire = true;
        public static float StandYawOffset = -25f;   // the body bladed to the rifle, standing
        public bool StandsToFire { get; private set; }
        void Posture(bool stand)
        {
            StandsToFire = stand;
            animator.SetBool(CrouchId, !stand);
            // The controller's parameters are fixed by ANIM_SPEC 1-5, so the two standing states have no transitions
            // into them: he is cross-faded there from here, and out again by StopFiring or by kneeling.
            if (stand) { if (!InState(AimStandId) && !InState(FireStandId)) animator.CrossFadeInFixedTime(AimStandId, 0.3f); }
            else if (InState(AimStandId) || InState(FireStandId)) animator.CrossFadeInFixedTime(IdleTrenchId, 0.3f);
        }
        static readonly int IdleTrenchId = Animator.StringToHash("Idle_Trench"), IdleStandId = Animator.StringToHash("Idle_Stand");
        bool InState(int id) => animator.GetCurrentAnimatorStateInfo(0).shortNameHash == id
            || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == id);
        static readonly int RunRifleId = Animator.StringToHash("Run_Rifle");
        static readonly int CrouchRunRifleId = Animator.StringToHash("CrouchRun_Rifle");
        Transform rightHand, leftHand;
        Quaternion restingRotation;
        Vector3 defensiveForward;
        EnemyAgent target;
        float nextShot, cueAt, nextPick = float.MaxValue;
        bool pending, engaged, suppressing;
        [SerializeField, Tooltip("교전 중 표적을 놓친 뒤 무릎 사격 자세로 그 자리를 겨누고 기다리는 시간(초)")] float holdAimAfterTarget = 4f;
        float lastTargetAt = -100f;
        Vector3 lastAim;
        Vector3 suppressAim;
        AllyRiflePose riflePose;
        AlliedRifleFit rifleFit;
        // 2026-11-24: switching between port arms, the aim and the carry used to move the rifle up to 48 cm and 137 degrees
        // in one frame (the hill cover). The arms and the rifle go from the pose last shown over this many seconds (0 = off).
        public static float HoldFadeSeconds = 0.22f;
        AllyPoseFade poseFade;
        float lastAdvance, lastRun; int portCut;
        float recoilAt = -100f;

        // 박용팔의 사격 원본은 가슴이 이미 옆으로 돌아가 있어 몸을 덜 비틀어 견착한다.
        float AimYawOffset => StandsToFire ? StandYawOffset : AlliedRifleFit.IsParkAvatar(animator) ? -26f : rifleYawOffset;

        // Set by HillAllies while he walks up the ridge: no turning or firing, rifle in the carry pose.
        public bool Advancing
        {
            get => advancing;
            set { advancing = value; if (value) hasAdvanced = true; }
        }
        bool advancing;
        // Has he moved off once since the section began? Before that he waits in the trench for the off.
        bool hasAdvanced;
        // Kneeling to fire: HillAllies leaves his facing alone while this is true.
        public bool Engaged => engaged;
        // AllyIdleLife lets the carried rifle ride the chest while an idle clip plays (null until Start).
        public AllyRiflePose RiflePose => riflePose;
        // Rigify clips (2026-11-18): set by AllyClipPlayer when it attaches to his animator (null without the clip data).
        internal AllyClipPlayer Clips { get; set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int ShotsFired { get; private set; }
        public int MuzzleEffectsSpawned { get; private set; }
        public int AudioCuesRequested { get; private set; }
        public string TargetName => target != null ? target.name : "";
        public string CarryMode { get; private set; } = "";
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
            poseFade = new AllyPoseFade(animator, transform, weapon, HoldFadeSeconds);
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
            // a director takes him over (Choi Man-su's fall): the clip graph lets go of his animator at once
            if (Clips != null) Clips.Release();
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
            poseFade?.Clear(); // a new section or retry starts from where he is put, not from the last pose
            watching = false;
            lastTargetAt = -100f;
            if (resetCounts && takingCover) { takingCover = false; LookUp(); }
            hasAdvanced = advancing; // already walking (HillAllies may hear the section change first): keep it
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

        // Halted on the hill assault (expanded run) while the fight is on: kneel and watch the ridge.
        // Only once he has moved off: before the first move he stands ready in the trench, as before.
        bool HillWatch => hasAdvanced && director.Flow.IsExpanded && director.Flow.Section == FlowSection.HillAssault && CombatActive;
        bool watching;

        // Under the barrage (2026-11-18): down on one knee behind the parapet, head bowed, no firing. ShellingDirector
        // sets it for the barrage and the breather after it; LookUp lifts the head again as the dust thins.
        bool takingCover;
        FiveRivers.Narrative.HeadBow coverBow;
        public bool TakingCover => takingCover;

        public void TakeCover(bool on)
        {
            if (takingCover == on) return;
            takingCover = on;
            if (on)
            {
                CancelCue();
                target = null;
                watching = false; // picks the low cover aim on the next frame
                if (isActiveAndEnabled && animator != null) coverBow = FiveRivers.Narrative.HeadBow.On(transform, 30f, 0.45f);
            }
            else
            {
                LookUp();
                watching = false; // aims up the field again
            }
        }

        public void LookUp()
        {
            if (coverBow != null) coverBow.Release(1.1f);
            coverBow = null;
        }

        // Behind the parapet, a few metres out and below its top: where the rifle points while he keeps his head down.
        Vector3 CoverPoint()
        {
            var forward = defensiveForward; forward.y = 0f;
            return transform.position + forward.normalized * 5f - Vector3.up * 0.6f;
        }

        // A spot on the ground some way up the slope, straight toward the enemy's side of the ridge.
        Vector3 WatchPoint()
        {
            var forward = defensiveForward; forward.y = 0f;
            var point = transform.position + forward.normalized * 30f;
            return Physics.Raycast(point + Vector3.up * 80f, Vector3.down, out var hit, 160f, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.8f
                : point + Vector3.up * 2f;
        }

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
                watching = false;
                return;
            }
            if (!CombatActive || takingCover) target = null;
            else if (target == null || !target.Alive || target.Retreating) target = FindTarget();
            if (target == null && (HillWatch || takingCover))
            {
                // Stopped on the open slope of the hill assault with nobody in sight: down on one knee with the
                // rifle up the slope, the way a man halts in the open under fire, instead of standing upright with
                // the rifle at the waist (2026-11-16). No rounds: there is nothing to shoot at.
                CancelCue();
                suppressing = false;
                // under the barrage the muzzle comes down below the parapet: hunched low behind the sandbags, head bowed
                if (!watching) { watching = true; suppressAim = takingCover ? CoverPoint() : WatchPoint(); }
                engaged = true;
                Posture(false);
                var look = suppressAim - transform.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(look) * Quaternion.Euler(0f, -AimYawOffset, 0f), turnSpeed * 0.5f * Time.deltaTime);
                return;
            }
            watching = false;
            if (target != null) { lastTargetAt = Time.time; lastAim = AimPoint(); }
            // Lost his man for a moment (behind cover, retargeting) while the fight is on: stays on one knee with the
            // rifle on the last spot instead of dropping out of the aim. Without this, in the western assault he went
            // up to fire, down to the low wait and up again every 1-1.5 s, or stood up in the trench (2026-11-18).
            // While men are still out on the slope (in cover, out of his sight) he stays up too; down only once it is clear.
            if (target == null && !SuppressActive && engaged && CombatActive && (Time.time - lastTargetAt < holdAimAfterTarget || EnemyOutThere()))
            {
                CancelCue();
                suppressing = false;
                suppressAim = lastAim; // LateUpdate aims through AimPoint(): the rifle stays on that spot
                Posture(StandToFire);
                var hold = lastAim - transform.position; hold.y = 0f;
                if (hold.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(hold) * Quaternion.Euler(0f, -AimYawOffset, 0f), turnSpeed * 0.5f * Time.deltaTime);
                return;
            }
            if (target == null && !SuppressActive)
            {
                StopFiring();
                if (TutorialCarryPose) animator.SetBool(CrouchId, false);
                return;
            }
            if (target == null && (!suppressing || (!pending && Time.time >= nextPick))) PickSuppressAim();
            suppressing = target == null;
            engaged = true;
            Posture(StandToFire);
            var direction = AimPoint() - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            var aimRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -AimYawOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, aimRotation, turnSpeed * Time.deltaTime);
            if (pending || Time.time < nextShot || Quaternion.Angle(transform.rotation, aimRotation) > 8f) return;
            // still coming up from the low wait to one knee (AllyClipPlayer): the rifle is not at his shoulder yet
            if (Clips != null && Clips.HoldsFire) return;
            if (!suppressing && !CanSee(target)) { target = FindTarget(); nextShot = Time.time + 0.25f; return; }
            if (StandsToFire) animator.CrossFadeInFixedTime(FireStandId, 0.05f, 0, 0f);
            else animator.SetTrigger(FireId);
            pending = true;
            cueAt = Time.time + fireCueDelay;
            nextShot = Time.time + Random.Range(config.fireIntervalMin, config.fireIntervalMax) * (suppressing ? suppressIntervalScale : 1f);
            // Hold the aim through the shot, then sweep to the next spot well before the next round.
            nextPick = Mathf.Min(cueAt + 0.5f, nextShot - 1f);
        }

        // 2026-11-24: a new target or the spot he watches used to swing the rifle up to 55 degrees in one frame; the
        // shouldered rifle now turns to it at most this fast (degrees a second). The first aimed frame starts on the point.
        public static float AimTurnSpeed = 320f;
        Vector3 steadyAimDir; bool steadyAimSet;
        Vector3 SteadyAim()
        {
            var want = AimPoint();
            var from = transform.position + transform.up * 1.4f;
            var dir = want - from; float far = dir.magnitude;
            if (far < 0.01f) return want;
            dir /= far;
            steadyAimDir = steadyAimSet && AimTurnSpeed > 0f ? Vector3.RotateTowards(steadyAimDir, dir, AimTurnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f) : dir;
            steadyAimSet = true;
            return from + steadyAimDir * far;
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
            suppressAim = Physics.Raycast(point + Vector3.up * 80f, Vector3.down, out var hit, 160f, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore)
                ? hit.point + Vector3.up * 0.6f
                : point + Vector3.up * 2f;
        }

        bool EnemyOutThere()
        {
            float range = config.sightRange * config.sightRange;
            foreach (var e in enemies.ActiveEnemies)
                if (e != null && e.Alive && !e.Retreating && (e.transform.position - transform.position).sqrMagnitude < range) return true;
            return false;
        }

        EnemyAgent FindTarget()
        {
            EnemyAgent nearest = null;
            float distance = config.sightRange * config.sightRange;
            foreach (var candidate in enemies.ActiveEnemies)
            {
                if (candidate == null || !candidate.Alive || candidate.Retreating) continue;
                float d = (candidate.transform.position - transform.position).sqrMagnitude;
                // the men firing at the player are the player's: the squad takes the others first (2026-11-18)
                if (enemies.AimsAtPlayer(candidate)) d *= 6f;
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
            return !Physics.Linecast(origin, aim, out var hit, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore)
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

        // The standing aim and shot are the KPA clips, which stand these bodies 11-14 cm above the floor (toes at
        // 17-20 cm, 6 in their own standing idle): while those states show, the hips come down until the lower toe
        // is at its idle height over the ground.
        public static float ToeRest = 0.06f, StandDropMost = 0.25f;
        Transform standHips, leftToe, rightToe;
        float standDrop;
        void StandOnFloor()
        {
            float w = Mathf.Clamp01(StateWeight(AimStandId) + StateWeight(FireStandId));
            if (w <= 0f) { standDrop = 0f; return; }
            if (standHips == null)
            {
                standHips = animator.GetBoneTransform(HumanBodyBones.Hips);
                leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            if (standHips == null || leftToe == null || rightToe == null) return;
            float low = float.MaxValue;
            foreach (var toe in new[] { leftToe, rightToe })
                if (Physics.Raycast(toe.position + Vector3.up * 0.6f, Vector3.down, out var hit, 3f, FiveRivers.Core.WorldQuery.Solid, QueryTriggerInteraction.Ignore))
                    low = Mathf.Min(low, toe.position.y - hit.point.y);
            if (low == float.MaxValue) return;
            standDrop = Mathf.MoveTowards(standDrop, Mathf.Clamp(low - ToeRest, 0f, StandDropMost), Time.deltaTime * 0.6f);
            standHips.position += Vector3.down * (standDrop * w);
        }

        void LateUpdate()
        {
            if (director.Flow.Paused) return;
            StandOnFloor();
            // Rigify clips (2026-11-18, AllyClipPlayer): while a trench clip has the whole pose, the rifle follows the
            // clip's own track and the hands hold it as the clip has them. The aim, the carry and the tutorial look rest.
            bool clipRifle = Clips != null && Clips.OwnsRifle;
            int holdMode = PoseFadeClock.Clip;
            if (!engaged || clipRifle) steadyAimSet = false;
            if (clipRifle) NoteCarry("clip");
            // Both palms come from the actual crouch/fire clip, including its recoil. While he kneels to fire the rifle
            // stays shouldered even on the frame a target is lost (it used to drop into the standing carry).
            else if (engaged)
            {
                riflePose.Apply(SteadyAim(), 0.022f * Mathf.Exp(-(Time.time - recoilAt) * 24f));
                NoteCarry(watching ? "watch" : "aim");
                holdMode = watching ? PoseFadeClock.Watch : PoseFadeClock.Aim;
            }
            else if (rightHand != null && leftHand != null && weapon != null)
            {
                var section = director.Flow.Section;
                float advance = Advancing ? StateWeight(CrouchRunRifleId) : 0f, run = Advancing ? StateWeight(RunRifleId) : 0f;
                // the new run clips carry the rifle in both hands themselves: port arms only for what is left of standing
                bool clipRun = Clips != null && Clips.RunClipsInstalled, clipAdvance = Clips != null && Clips.AdvanceClipsInstalled;
                bool portArms = director.Flow.IsExpanded && (section == FlowSection.HillAssault || section == FlowSection.Dawn)
                    && riflePose.ApplyPortArms(clipAdvance ? 0f : advance, clipRun ? 0f : run, section == FlowSection.Dawn ? 1f : 0f);
                if (portArms && (clipRun || clipAdvance)) Clips.HoldMovingRifle(weapon, clipRun ? run : 0f, clipAdvance ? advance : 0f);
                NoteCarry(portArms ? $"port a{StateWeight(CrouchRunRifleId):0.00} r{StateWeight(RunRifleId):0.00}" : "carry");
                // stopping at cover (Advancing off) or the controller cutting run ↔ crouched advance in one frame moved the
                // rifle from the clip's track to the standing port arms at once: count those as a change of hold too
                if (Mathf.Abs(advance - lastAdvance) + Mathf.Abs(run - lastRun) > 0.35f) portCut++;
                lastAdvance = advance; lastRun = run;
                holdMode = portArms ? (Advancing ? PoseFadeClock.PortMove : PoseFadeClock.PortStand) + 2 * (portCut & 0xFFFF) : PoseFadeClock.Carry;
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
            // going into or out of a clip: what was done above is kept in proportion (nothing when no clip plays)
            if (Clips != null) Clips.HoldRifle(weapon);
            if (!engaged && (Clips == null || Clips.RifleWeight <= 0f)
                && (director.Flow.Section == FlowSection.Title || director.Flow.Section == FlowSection.Tutorial))
                riflePose.LookAt(transform.position + defensiveForward * 60f + Vector3.up * 1.4f);
            // the way he holds the rifle changed this frame (port arms ↔ aim ↔ carry): from the pose last on screen, not in one frame
            if (poseFade != null && HoldFadeSeconds > 0f) { poseFade.Clock.Duration = HoldFadeSeconds; poseFade.Apply(holdMode, Time.deltaTime); }
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
            if (Physics.Raycast(ground + Vector3.up * 2f, Vector3.down, out var hit, 4f, FiveRivers.Core.WorldQuery.All, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<EnemyAgent>() == null
                && hit.collider.GetComponentInParent<PlayerHealth>() == null)
                effects.Spawn(EffectKind.ImpactDirt, hit.point, Quaternion.LookRotation(hit.normal));
            if (!suppressing) enemies.AllyShotAt(target);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ShotsFired++;
            if (flash != null) MuzzleEffectsSpawned++;
            AudioCuesRequested++;
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void NoteCarry(string mode)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CarryMode = mode;
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
            bool stood = StandsToFire;
            StandsToFire = false;
            animator.SetBool(CrouchId, false);
            if (stood && (InState(AimStandId) || InState(FireStandId))) animator.CrossFadeInFixedTime(IdleStandId, 0.3f);
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Fire_Crouch")
                || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Fire_Crouch")))
                animator.CrossFade("Idle_Stand", 0.1f);
        }
    }
}


