using System;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    public enum WeaponSlot
    {
        M1 = 0,      // 키 1
        Bazooka = 1, // 키 2 (S5부터)
    }

    // 무기 전환(1 · 2) · 조준(우클릭) · 시야각. 전환 중에는 쏘지 않는다.
    public sealed class WeaponHolder : MonoBehaviour
    {
        [SerializeField] WeaponConfig config;
        [SerializeField] PlayerController player;
        [SerializeField] Camera view;
        [SerializeField] M1Rifle m1;
        [SerializeField] Bazooka bazooka;

        WeaponSlot pending;
        WeaponSlot shown;
        float switchEnd;
        float hideAt;

        public WeaponSlot Current { get; private set; } = WeaponSlot.M1;
        public bool Switching { get; private set; }
        public bool BazookaUnlocked { get; private set; }
        public M1Rifle M1 => m1;
        public Bazooka Bazooka => bazooka;
        public bool Reloading => Current == WeaponSlot.M1 ? m1.Reloading : bazooka.Reloading;

        public event Action<WeaponSlot> Switched;   // 플레이어가 1 · 2로 바꿨을 때만
        public event Action LoadoutReset;          // 체크포인트 · 새 회차 (소리 없음)
        public event Action BazookaGiven;

        void Start()
        {
            ShowOnly(Current);
        }

        void Update()
        {
            var controls = player.Controls;
            bool canAct = player.Gate.CanAct;

            if (canAct && !Switching)
            {
                if (controls.Weapon1.WasPressedThisFrame() && Current != WeaponSlot.M1) BeginSwitch(WeaponSlot.M1);
                else if (controls.Weapon2.WasPressedThisFrame() && BazookaUnlocked && Current != WeaponSlot.Bazooka) BeginSwitch(WeaponSlot.Bazooka);
            }
            if (Switching)
            {
                if (Time.time >= hideAt) ShowOnly(pending);
                if (Time.time >= switchEnd)
                {
                    Switching = false;
                    Current = pending;
                    Switched?.Invoke(Current);
                }
            }

            ApplyVisibility();
            bool aim = canAct && !Switching && controls.Aim.IsPressed() && !player.Sprinting;
            player.Aiming = aim;
            bool weaponCanAct = canAct && !Switching;
            if (Current == WeaponSlot.M1) m1.Tick(weaponCanAct, aim);
            else bazooka.Tick(weaponCanAct, aim);

            float blend = Current == WeaponSlot.M1 ? m1.AimBlend : bazooka.AimBlend;
            float aimFov = Current == WeaponSlot.M1 ? config.m1AimFov : config.bazookaAimFov;
            view.fieldOfView = Mathf.Lerp(config.hipFov, aimFov, blend);
        }

        void BeginSwitch(WeaponSlot to)
        {
            m1.CancelReload();
            bazooka.CancelReload();
            // 바주카 내리기 클립은 따로 없다 (ANIM_SPEC) → M1 집어넣기 시간을 함께 쓴다
            float unequip = config.m1UnequipTime;
            float equip = to == WeaponSlot.M1 ? config.m1EquipTime : config.bazookaEquipTime;
            pending = to;
            Switching = true;
            hideAt = Time.time + unequip;
            switchEnd = hideAt + equip;
        }

        void ShowOnly(WeaponSlot slot)
        {
            shown = slot;
            ApplyVisibility();
        }

        // 시작 화면 · 결과 화면(조작 없음)에서는 총을 숨긴다
        void ApplyVisibility()
        {
            bool visible = player.Gate.Mode != Core.ControlMode.None;
            m1.Model.gameObject.SetActive(visible && shown == WeaponSlot.M1);
            bazooka.Model.gameObject.SetActive(visible && shown == WeaponSlot.Bazooka);
        }

        // 전차전(S5) 시작 때 바주카를 받는다. 조작 안내는 HUD가 한다.
        public void GiveBazooka()
        {
            if (BazookaUnlocked) return;
            BazookaUnlocked = true;
            bazooka.ResetAmmo();
            BazookaGiven?.Invoke();
        }

        // 체크포인트 복구 · 새 회차: 탄약을 체크포인트 값으로 되돌린다.
        public void ResetLoadout(bool bazookaUnlocked, WeaponSlot slot)
        {
            BazookaUnlocked = bazookaUnlocked;
            m1.ResetAmmo();
            bazooka.ResetAmmo();
            Switching = false;
            Current = slot;
            ShowOnly(slot);
            LoadoutReset?.Invoke();
        }
    }
}
