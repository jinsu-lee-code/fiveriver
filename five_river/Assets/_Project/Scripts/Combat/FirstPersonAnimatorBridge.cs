using UnityEngine;

namespace FiveRivers.Combat
{
    // 1인칭 무기 사건 → Animator 매개변수 (ANIM_SPEC 1-5 AC_FP_M1 · AC_FP_Bazooka).
    // 1인칭 팔 · 무기 Animator가 아직 없으면(그레이박스) 아무것도 하지 않는다. 조준 · 반동 · 흔들림은 코드(1-7).
    public sealed class FirstPersonAnimatorBridge : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] Resupplier resupplier;
        [SerializeField] Animator m1Animator;      // AC_FP_M1
        [SerializeField] Animator bazookaAnimator; // AC_FP_Bazooka

        static readonly int Fire = Animator.StringToHash("Fire");
        static readonly int ReloadEmpty = Animator.StringToHash("ReloadEmpty");
        static readonly int ReloadPartial = Animator.StringToHash("ReloadPartial");
        static readonly int Reload = Animator.StringToHash("Reload");
        static readonly int Equip = Animator.StringToHash("Equip");
        static readonly int Unequip = Animator.StringToHash("Unequip");
        static readonly int Resupply = Animator.StringToHash("Resupply");
        static readonly int Sprint = Animator.StringToHash("Sprint");

        WeaponSlot lastSlot;

        void Start()
        {
            weapons.M1.Fired += () => Trigger(m1Animator, Fire);
            weapons.M1.ReloadStarted += kind => Trigger(m1Animator, kind == ReloadKind.Empty ? ReloadEmpty : ReloadPartial);
            weapons.Bazooka.Fired += () => Trigger(bazookaAnimator, Fire);
            weapons.Bazooka.ReloadStarted += () => Trigger(bazookaAnimator, Reload);
            weapons.Switched += slot =>
            {
                if (slot == lastSlot) return;
                Trigger(slot == WeaponSlot.M1 ? m1Animator : bazookaAnimator, Equip);
                Trigger(lastSlot == WeaponSlot.M1 ? m1Animator : bazookaAnimator, Unequip);
                lastSlot = slot;
            };
            weapons.LoadoutReset += () => lastSlot = weapons.Current;
            resupplier.Resupplied += () => Trigger(m1Animator, Resupply);
        }

        void Update()
        {
            if (m1Animator != null && m1Animator.isActiveAndEnabled) m1Animator.SetBool(Sprint, player.Sprinting);
        }

        static void Trigger(Animator animator, int id)
        {
            if (animator != null && animator.isActiveAndEnabled) animator.SetTrigger(id);
        }
    }
}
