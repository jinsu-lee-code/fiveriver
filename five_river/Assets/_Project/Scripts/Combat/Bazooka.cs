using System;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // M20 바주카. 한 발 쏘면 예비 로켓이 있을 때 바로 재장전한다 (전차를 멈추려면 2발이 필요, ANIM_SPEC 27).
    public sealed class Bazooka : MonoBehaviour
    {
        [SerializeField] WeaponConfig config;
        [SerializeField] PlayerController player;
        [SerializeField] Camera view;
        [SerializeField] Effects effects;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform backblast;
        [SerializeField] Transform model;
        [SerializeField] Vector3 hipPosition;
        [SerializeField] Vector3 aimPosition;
        [SerializeField] Rocket rocketPrefab;
        // Presentation only: the sight stays up this long after a shot (while aim is held) so the rocket can be
        // followed and the recoil reads, instead of the launcher snapping to the hip on the trigger pull.
        // Reload timing is unchanged (it still starts at the shot).
        [SerializeField, Min(0f)] float shotAimHold = .35f;

        float reloadEnd;
        float aimBlend;
        float firedAt = float.NegativeInfinity;

        public int Loaded { get; private set; }
        public int Reserve { get; private set; }
        public bool Reloading { get; private set; }
        public float ReloadDuration => config.bazookaReloadTime;
        public float AimBlend => aimBlend;
        public Transform Model => model;

        public event Action Fired;
        public event Action ReloadStarted;
        public event Action Reloaded;

        void Awake()
        {
            ResetAmmo();
        }

        public void Tick(bool canAct, bool aim)
        {
            float blendSpeed = config.aimBlendTime > 0f ? Time.deltaTime / config.aimBlendTime : 1f;
            bool holdingShot = Reloading && Time.time - firedAt < shotAimHold;
            aimBlend = Mathf.MoveTowards(aimBlend, aim && (!Reloading || holdingShot) ? 1f : 0f, blendSpeed);
            model.localPosition = Vector3.Lerp(hipPosition, aimPosition, aimBlend);

            if (Reloading)
            {
                if (Time.time >= reloadEnd)
                {
                    Reloading = false;
                    Loaded = 1;
                    Reserve--;
                    Reloaded?.Invoke();
                }
                return;
            }
            if (!canAct) return;

            var controls = player.Controls;
            if (controls.Reload.WasPressedThisFrame()) TryReload();
            else if (controls.Fire.WasPressedThisFrame() && Loaded > 0) Launch();
        }

        void Launch()
        {
            Loaded = 0;
            firedAt = Time.time;
            var rotation = view.transform.rotation;
            var rocket = effects.SpawnTracked(rocketPrefab, muzzle.position, rotation);
            rocket.Launch(config, effects, view.transform.position);
            effects.Spawn(EffectKind.BazookaBackblast, backblast.position, backblast.rotation);
            player.AddRecoil(config.bazookaRecoilPitch);
            Fired?.Invoke();
            TryReload();
        }

        public bool TryReload()
        {
            if (Reloading || Loaded > 0 || Reserve <= 0) return false;
            Reloading = true;
            reloadEnd = Time.time + config.bazookaReloadTime;
            ReloadStarted?.Invoke();
            return true;
        }

        public void CancelReload() => Reloading = false;

        public void Refill()
        {
            Reserve = config.bazookaRockets - Loaded;
        }

        public void ResetAmmo()
        {
            Loaded = 1;
            Reserve = config.bazookaRockets - 1;
            Reloading = false;
            aimBlend = 0f;
            firedAt = float.NegativeInfinity;
        }
    }
}
