using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 표시용 Animator는 무기 동작을 따라간다. 탄약과 입력 허용 시점은 WeaponHolder와 각 무기가 결정한다.
    [DefaultExecutionOrder(40)]
    public sealed class FirstPersonAnimatorBridge : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] Resupplier resupplier;
        [SerializeField] Animator m1Animator;
        [SerializeField] Animator bazookaAnimator;

        static readonly int Idle = Animator.StringToHash("Idle");
        static readonly int Fire = Animator.StringToHash("Fire");
        static readonly int ReloadEmpty = Animator.StringToHash("ReloadEmpty");
        static readonly int ReloadPartial = Animator.StringToHash("ReloadPartial");
        static readonly int Reload = Animator.StringToHash("Reload");
        static readonly int Equip = Animator.StringToHash("Equip");
        static readonly int Unequip = Animator.StringToHash("Unequip");
        static readonly int Resupply = Animator.StringToHash("Resupply");
        static readonly int Sprint = Animator.StringToHash("Sprint");

        readonly Dictionary<string, float> clipLengths = new();
        float fireEnd;
        float queuedReloadEnd;
        bool reloadQueued;

        void Awake()
        {
            CacheClipLengths(m1Animator);
            CacheClipLengths(bazookaAnimator);
        }

        void OnEnable()
        {
            if (weapons == null || resupplier == null) return;
            weapons.M1.Fired += OnM1Fired;
            weapons.M1.ReloadStarted += OnM1Reload;
            weapons.M1.Reloaded += OnM1Reloaded;
            weapons.Bazooka.Fired += OnBazookaFired;
            weapons.Bazooka.ReloadStarted += OnBazookaReload;
            weapons.Bazooka.Reloaded += OnBazookaReloaded;
            weapons.Lowering += OnLowering;
            weapons.Raising += OnRaising;
            weapons.Switched += OnSwitched;
            weapons.LoadoutReset += OnLoadoutReset;
            resupplier.Resupplied += OnResupply;
        }

        void OnDisable()
        {
            if (weapons == null || resupplier == null) return;
            weapons.M1.Fired -= OnM1Fired;
            weapons.M1.ReloadStarted -= OnM1Reload;
            weapons.M1.Reloaded -= OnM1Reloaded;
            weapons.Bazooka.Fired -= OnBazookaFired;
            weapons.Bazooka.ReloadStarted -= OnBazookaReload;
            weapons.Bazooka.Reloaded -= OnBazookaReloaded;
            weapons.Lowering -= OnLowering;
            weapons.Raising -= OnRaising;
            weapons.Switched -= OnSwitched;
            weapons.LoadoutReset -= OnLoadoutReset;
            resupplier.Resupplied -= OnResupply;
            OnLoadoutReset();
        }

        void Update()
        {
            if (weapons == null) return;
            if (IsActive(m1Animator))
                m1Animator.SetBool(Sprint, player.Sprinting && !weapons.Switching && !weapons.M1.Reloading);
            if (!reloadQueued) return;
            if (!weapons.Bazooka.Reloading)
            {
                reloadQueued = false;
                return;
            }
            if (Time.time < fireEnd) return;
            reloadQueued = false;
            Play(bazookaAnimator, Reload, "A_FP_Bazooka_Reload", queuedReloadEnd - Time.time, .045f);
        }

        void OnM1Fired()
        {
            if (IsActive(m1Animator) && m1Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Resupply)
                m1Animator.CrossFadeInFixedTime(Idle, .06f);
        }

        void OnM1Reload(ReloadKind kind)
        {
            bool empty = kind == ReloadKind.Empty;
            Play(m1Animator, empty ? ReloadEmpty : ReloadPartial,
                empty ? "A_FP_M1_ReloadEmpty" : "A_FP_M1_ReloadPartial", weapons.M1.ReloadDuration, .045f);
        }

        void OnBazookaFired()
        {
            fireEnd = Time.time + Length("A_FP_Bazooka_Fire");
            Play(bazookaAnimator, Fire, "A_FP_Bazooka_Fire");
        }

        void OnBazookaReload()
        {
            // 자동 장전은 즉시 시작하고, 화면에서는 발사 반동이 돌아온 뒤 같은 종료 시점에 맞춰 장전한다.
            reloadQueued = Time.time < fireEnd;
            queuedReloadEnd = Time.time + weapons.Bazooka.ReloadDuration;
            if (!reloadQueued)
                Play(bazookaAnimator, Reload, "A_FP_Bazooka_Reload", weapons.Bazooka.ReloadDuration, .045f);
        }

        void OnM1Reloaded() => ResetSpeed(m1Animator);
        void OnBazookaReloaded() => ResetSpeed(bazookaAnimator);

        // Which part of each clip has the weapon on screen (share of the clip, measured frame by frame 2026-11-17):
        // the lowering clips have it out of view after two thirds, the raising clips only bring it into view after
        // 0.40 (M1) and 0.48 (bazooka). Played whole, the hands were empty for 0.47 s going to the bazooka and
        // 0.33 s coming back. The visible part is now spread over the same switch time (WeaponConfig, unchanged),
        // so one weapon goes down as the other comes up.
        const float UnequipVisible = .70f, M1EquipFrom = .36f, BazookaEquipFrom = .44f;

        void OnLowering(WeaponSlot slot, float duration)
        {
            reloadQueued = false;
            Play(AnimatorFor(slot), Unequip,
                slot == WeaponSlot.M1 ? "A_FP_M1_Unequip" : "A_FP_Bazooka_Unequip", duration, .035f, 0f, UnequipVisible);
        }

        void OnRaising(WeaponSlot slot, float duration)
        {
            Play(AnimatorFor(slot), Equip,
                slot == WeaponSlot.M1 ? "A_FP_M1_Equip" : "A_FP_Bazooka_Equip", duration, 0f,
                slot == WeaponSlot.M1 ? M1EquipFrom : BazookaEquipFrom);
        }

        void OnSwitched(WeaponSlot slot) => ResetSpeed(AnimatorFor(slot));

        void OnResupply()
        {
            if (weapons.Current == WeaponSlot.M1 && !weapons.M1.Reloading)
                Play(m1Animator, Resupply, "A_FP_Resupply", blend: .045f);
        }

        void OnLoadoutReset()
        {
            fireEnd = 0f;
            reloadQueued = false;
            ResetAnimator(m1Animator);
            ResetAnimator(bazookaAnimator);
        }

        void CacheClipLengths(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                clipLengths[clip.name] = clip.length;
        }

        float Length(string clip) => clipLengths.TryGetValue(clip, out float length) ? length : 0f;

        // from / to: the share of the clip that is played over 'seconds' (the whole clip by default)
        void Play(Animator animator, int state, string clip, float seconds = 0f, float blend = 0f, float from = 0f, float to = 1f)
        {
            if (!IsActive(animator)) return;
            float length = Length(clip) * Mathf.Max(.05f, to - from);
            animator.speed = seconds > 0f && length > 0f ? length / Mathf.Max(.01f, seconds) : 1f;
            if (blend > 0f) animator.CrossFadeInFixedTime(state, blend, 0, from * Length(clip));
            else animator.Play(state, 0, from);
        }

        Animator AnimatorFor(WeaponSlot slot) => slot == WeaponSlot.M1 ? m1Animator : bazookaAnimator;

        static bool IsActive(Animator animator) => animator != null && animator.isActiveAndEnabled;

        static void ResetSpeed(Animator animator)
        {
            if (animator != null) animator.speed = 1f;
        }

        static void ResetAnimator(Animator animator)
        {
            ResetSpeed(animator);
            if (IsActive(animator)) animator.Play(Idle, 0, 0f);
        }
    }
}
