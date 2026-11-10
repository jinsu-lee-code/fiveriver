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

        // Peep-sight aiming: see PeepSightOverlay. Near clip drops while aiming so the rear sight 3-4 cm from
        // the eye is not cut away.
        public static bool DebugForceAim; // capture/review tools only
        public static float HipNearClip = 0.03f, AimNearClip = 0.01f;
        // Off by request (2026-11-07): aim through the real rear-sight model at its original eye position, no
        // painted ghost ring or focus blur. Set true to bring the ghost-ring presentation back.
        public static bool PeepRing = false;
        PeepSightOverlay peep;
        Transform rearSight;
        Renderer[] rearApertures = Array.Empty<Renderer>();

        void Start()
        {
            ShowOnly(Current);
            peep = view.gameObject.AddComponent<PeepSightOverlay>();
            foreach (var t in m1.GetComponentsInChildren<Transform>(true))
                if (t.name == "Sight_RearCentre") rearSight = t;
            rearApertures = Array.FindAll(m1.GetComponentsInChildren<Renderer>(true), r => r.name == "M1_SingleRearAperture");
            if (rearSight == null) Debug.LogWarning("[WeaponHolder] Sight_RearCentre not found; peep ring stays screen-centred.");
        }

        void LateUpdate()
        {
            float blend = PeepRing && Current == WeaponSlot.M1 && !Switching ? m1.AimBlend : 0f;
            // the ring only appears once the eye is actually at the aperture
            float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.95f, blend));
            var centre = new Vector2(.5f, .5f);
            if (rearSight != null && ring > 0f)
            {
                var sp = view.WorldToViewportPoint(rearSight.position);
                if (sp.z > 0f) centre = sp;
            }
            peep.Show(ring, centre);
            foreach (var r in rearApertures) r.forceRenderingOff = ring > 0.5f;
        }

        void Update()
        {
            var controls = player.Controls;
            bool canAct = player.Gate.CanAct;

            // Title/result end pending actions; temporary Full-mode input blockers do not.
            if (player.Gate.Mode == Core.ControlMode.None)
            {
                m1.CancelReload();
                bazooka.CancelReload();
                Switching = false;
                shown = Current;
            }

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
            bool aim = canAct && !Switching && (controls.Aim.IsPressed() || DebugForceAim) && !player.Sprinting;
            player.Aiming = aim;
            bool weaponCanAct = canAct && !Switching;
            if (Current == WeaponSlot.M1) m1.Tick(weaponCanAct, aim);
            else bazooka.Tick(weaponCanAct, aim);

            float blend = Current == WeaponSlot.M1 ? m1.AimBlend : bazooka.AimBlend;
            float aimFov = Current == WeaponSlot.M1 ? config.m1AimFov : config.bazookaAimFov;
            view.fieldOfView = Mathf.Lerp(config.hipFov, aimFov, blend);
            view.nearClipPlane = Mathf.Lerp(HipNearClip, AimNearClip, PeepRing && Current == WeaponSlot.M1 ? blend : 0f);
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
