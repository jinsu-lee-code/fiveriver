using System;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // M1 개런드 (반자동). 한 번 누르면 한 발: 총구 화염 · 발사음 · 탄약 감소 각 1회 (GDD 7번).
    // 마지막 발에 클립이 튀어나가고(핑), 예비탄이 있으면 바로 장전을 시작한다. R은 잔탄 장전.
    public sealed class M1Rifle : MonoBehaviour
    {
        [SerializeField] WeaponConfig config;
        [SerializeField] PlayerController player;
        [SerializeField] Camera view;
        [SerializeField] Effects effects;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform clipEject; // Socket_ClipEject (ART_SPEC 4)
        [SerializeField] Transform model;
        [SerializeField] Vector3 hipPosition;
        [SerializeField] Vector3 aimPosition;
        [SerializeField] Vector3 hipEuler;
        [SerializeField] Vector3 aimEuler;
        [SerializeField] LayerMask hitMask = ~0;

        float nextFire;
        float reloadEnd;
        float aimBlend;
        float reloadStart;
        float reloadDuration;
        bool insertionCueSent;

        public M1Magazine Magazine { get; private set; }
        public bool Reloading { get; private set; }
        public ReloadKind ReloadKind { get; private set; }
        public float AimBlend => aimBlend;
        public Transform Model => model;
        public float ReloadProgress => Reloading ? Mathf.Clamp01((Time.time - reloadStart) / Mathf.Max(0.01f, reloadDuration)) : 0f;

        public event Action Fired;
        public event Action ClipEjected;
        public event Action DryFired;
        public event Action<ReloadKind> ReloadStarted;
        public event Action Reloaded;
        public event Action ClipInserted;
        public event Action<Collider> HitSomething;

        void Awake()
        {
            Magazine = new M1Magazine(config.m1ClipSize, config.m1StartReserve, config.m1MaxReserve);
        }

        public void Tick(bool canAct, bool aim)
        {
            float blendSpeed = config.aimBlendTime > 0f ? Time.deltaTime / config.aimBlendTime : 1f;
            aimBlend = Mathf.MoveTowards(aimBlend, aim && !Reloading ? 1f : 0f, blendSpeed);
            model.localPosition = Vector3.Lerp(hipPosition, aimPosition, aimBlend);
            model.localRotation = Quaternion.Slerp(Quaternion.Euler(hipEuler), Quaternion.Euler(aimEuler), aimBlend);

            if (Reloading)
            {
                AdvanceReload(Time.time);
                return;
            }
            if (!canAct) return;

            var controls = player.Controls;
            if (controls.Reload.WasPressedThisFrame()) TryReload();
            else if (controls.Fire.WasPressedThisFrame() && Time.time >= nextFire)
            {
                if (Magazine.CanFire) Shoot(aimBlend > 0.5f);
                else
                {
                    DryFired?.Invoke();
                    TryReload();
                }
            }
        }

        void Shoot(bool aimed)
        {
            Magazine.Fire(out bool ejected);
            nextFire = Time.time + config.m1FireInterval;

            float spread = aimed ? config.m1AimSpread : config.m1HipSpread;
            var dir = Quaternion.Euler(UnityEngine.Random.Range(-spread, spread) * 0.5f, UnityEngine.Random.Range(-spread, spread) * 0.5f, 0f) * Vector3.forward;
            var ray = new Ray(view.transform.position, view.transform.rotation * dir);
            if (Physics.Raycast(ray, out var hit, config.m1Range, hitMask, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<IDamageable>();
                target?.TakeDamage(config.m1Damage, view.transform.position);
                if (hit.collider.GetComponentInParent<TutorialTarget>() != null)
                    M1SurfaceImpact.Wood(hit);
                else
                    effects.Spawn(EffectKind.ImpactDirt, hit.point, Quaternion.LookRotation(hit.normal));
                HitSomething?.Invoke(hit.collider);
            }

            effects.Spawn(EffectKind.MuzzleFlashM1, muzzle.position, muzzle.rotation);
            player.AddRecoil(config.m1RecoilPitch);
            Fired?.Invoke();
            if (ejected)
            {
                var clip = effects.Spawn(EffectKind.ClipEject, clipEject.position, clipEject.rotation);
                var body = clip != null ? clip.GetComponent<Rigidbody>() : null;
                if (body != null)
                {
                    // 핑: 위로 튀어 오르며 오른쪽 뒤로
                    body.linearVelocity = view.transform.rotation * new Vector3(0.6f, 3.2f, -0.6f);
                    body.angularVelocity = UnityEngine.Random.insideUnitSphere * 12f;
                    var col = clip.GetComponent<Collider>();
                    var self = player.GetComponent<CharacterController>();
                    if (col != null && self != null) Physics.IgnoreCollision(col, self);
                }
                ClipEjected?.Invoke();
                TryReload();
            }
        }

        public bool TryReload()
        {
            var kind = Magazine.PlanReload();
            if (kind == ReloadKind.None || Reloading) return false;
            Reloading = true;
            ReloadKind = kind;
            reloadStart = Time.time;
            insertionCueSent = false;
            reloadDuration = kind == ReloadKind.Empty ? config.m1ReloadEmptyTime : config.m1ReloadPartialTime;
            reloadEnd = reloadStart + reloadDuration;
            ReloadStarted?.Invoke(kind);
            return true;
        }

        void AdvanceReload(float now)
        {
            if (!Reloading) return;
            if (!insertionCueSent && now >= reloadStart + reloadDuration * 0.66f)
            {
                insertionCueSent = true;
                ClipInserted?.Invoke();
            }
            if (now >= reloadEnd) FinishReload();
        }

        void FinishReload()
        {
            Magazine.CompleteReload();
            Reloading = false;
            Reloaded?.Invoke();
        }

        public void CancelReload()
        {
            Reloading = false;
            insertionCueSent = false;
        }

        public void ResetAmmo()
        {
            Magazine.Reset(config.m1StartReserve);
            Reloading = false;
            insertionCueSent = false;
            aimBlend = 0f;
        }
    }
}
