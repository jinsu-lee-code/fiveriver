using System;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // M20 바주카. 한 발 쏘면 예비 로켓이 있을 때 바로 재장전한다 (전차를 멈추려면 2발이 필요, ANIM_SPEC 27).
    // 2026-11-24 로켓포반: 탄약수(LoaderTeamDirector)가 LoaderControlled 를 켜 두면 장전 시간을 이 클래스가 재지 않고
    // 탄약수가 CompleteReload() 로 끝낸다. 꺼 두면(탄약수 없음 · 전시용 · 장면이 깨짐) 예전 그대로 혼자 장전한다.
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
        float reloadStart = float.NegativeInfinity;
        float aimBlend;
        float firedAt = float.NegativeInfinity;

        public int Loaded { get; private set; }
        public int Reserve { get; private set; }
        public bool Reloading { get; private set; }
        public float ReloadDuration => config.bazookaReloadTime;
        public float AimBlend => aimBlend;
        public Transform Model => model;

        // 탄약수가 장전을 맡을 수 있게 켜 둔다 (LoaderTeamDirector). 켜진 동안 새 장전은 ExternalReload 가 된다.
        public bool LoaderControlled { get; set; }
        // 지금 진행 중인 장전을 탄약수가 하고 있다: 시간을 재지 않고, 1인칭 장전 동작(ReloadStarted)도 쓰지 않는다.
        public bool ExternalReload { get; private set; }

        public event Action Fired;
        public event Action ReloadStarted;
        public event Action Reloaded;
        // 탄약수가 맡은 장전이 끝나기 전에 취소됐다 (무기 바꿈 · 메뉴 · 탄약 초기화)
        public event Action ExternalReloadCancelled;

        void Awake()
        {
            ResetAmmo();
        }

        public void Tick(bool canAct, bool aim)
        {
            float blendSpeed = config.aimBlendTime > 0f ? Time.deltaTime / config.aimBlendTime : 1f;
            bool holdingShot = Reloading && Time.time - firedAt < shotAimHold;
            // 탄약수가 장전하는 동안은 발사관을 든 채 조준을 유지한다 (시야각이 55 → 70 으로 튀지 않게)
            aimBlend = Mathf.MoveTowards(aimBlend, aim && (!Reloading || holdingShot || ExternalReload) ? 1f : 0f, blendSpeed);
            model.localPosition = Vector3.Lerp(hipPosition, aimPosition, aimBlend);

            if (Reloading)
            {
                if (!ExternalReload && Time.time >= reloadEnd)
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
            // The tube empty with rockets in reserve (the last one was fired with none left, then he resupplied):
            // the trigger did nothing and only R loaded it. Pulling it now loads, like the rifle's dry fire.
            else if (controls.Fire.WasPressedThisFrame()) TryReload();
        }

        void Launch()
        {
            Loaded = 0;
            firedAt = Time.time;
            var rotation = view.transform.rotation;
            var rocket = effects.SpawnTracked(rocketPrefab, muzzle.position, rotation);
            rocket.Launch(config, effects, view.transform.position);
            var blast = effects.Spawn(EffectKind.BazookaBackblast, backblast.position, backblast.rotation);
            // 탄약수가 사수 뒤에 있으면 후폭풍 연기가 그를 덮지 않게 좁은 원뿔로만 (탄약수 없는 흐름은 예전 그대로)
            if (LoaderControlled) BackblastShaping.ForLoader(blast);
            player.AddRecoil(config.bazookaRecoilPitch);
            Fired?.Invoke();
            TryReload();
        }

        // R · 방아쇠(빈 발사관) · 발사 직후 자동. 탄약수가 있으면 "장전 요청"이다.
        public bool TryReload()
        {
            if (Reloading || Loaded > 0 || Reserve <= 0) return false;
            Reloading = true;
            reloadStart = Time.time;
            if (LoaderControlled)
            {
                ExternalReload = true;
                reloadEnd = float.PositiveInfinity;
                return true;
            }
            ExternalReload = false;
            reloadEnd = Time.time + config.bazookaReloadTime;
            ReloadStarted?.Invoke();
            return true;
        }

        // 탄약수가 로켓을 다 밀어 넣었다
        public bool CompleteReload()
        {
            if (!Reloading || !ExternalReload) return false;
            Reloading = false;
            ExternalReload = false;
            Loaded = 1;
            Reserve--;
            Reloaded?.Invoke();
            return true;
        }

        // 탄약수가 맡지 못한다 → 지금부터 혼자 장전 (처음부터 한 번 더 걸린다: 1인칭 장전 동작도 같은 시간으로 돈다)
        public bool TakeOverReload()
        {
            if (!Reloading || !ExternalReload) return false;
            ExternalReload = false;
            reloadStart = Time.time;
            reloadEnd = Time.time + config.bazookaReloadTime;
            ReloadStarted?.Invoke();
            return true;
        }

        public void CancelReload()
        {
            bool external = Reloading && ExternalReload;
            Reloading = false;
            ExternalReload = false;
            if (external) ExternalReloadCancelled?.Invoke();
        }

        public void Refill()
        {
            Reserve = config.bazookaRockets - Loaded;
        }

        public void ResetAmmo()
        {
            bool external = Reloading && ExternalReload;
            Loaded = 1;
            Reserve = config.bazookaRockets - 1;
            Reloading = false;
            ExternalReload = false;
            aimBlend = 0f;
            firedAt = float.NegativeInfinity;
            reloadStart = float.NegativeInfinity;
            if (external) ExternalReloadCancelled?.Invoke();
        }
    }
}
