using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 로켓이 맞힌 것이 전차면 판정을 맡긴다.
    public interface IRocketTarget
    {
        // shooterPosition: 쏜 위치 (측면 판정 기준, BALANCE 2-3)
        void OnRocketHit(Vector3 point, Vector3 shooterPosition);
    }

    // 바주카 로켓. 곧게 날아가며, 맞힌 곳에서 터진다. 전차에는 판정만 넘긴다.
    public sealed class Rocket : MonoBehaviour
    {
        [SerializeField] Transform trail; // VFX_RocketTrail: 터진 뒤에도 연기가 잠깐 남는다

        WeaponConfig config;
        Effects effects;
        Vector3 shooter;
        float dieAt;
        bool live;

        public void Launch(WeaponConfig weaponConfig, Effects owner, Vector3 shooterPosition)
        {
            config = weaponConfig;
            effects = owner;
            shooter = shooterPosition;
            dieAt = Time.time + config.rocketLifetime;
            live = true;
        }

        void Update()
        {
            if (!live) return;
            float step = config.rocketSpeed * Time.deltaTime;
            if (Physics.Raycast(transform.position, transform.forward, out var hit, step + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                Impact(hit);
                return;
            }
            transform.position += transform.forward * step;
            if (Time.time >= dieAt) Explode(transform.position, Vector3.up);
        }

        void Impact(RaycastHit hit)
        {
            live = false;
            var target = hit.collider.GetComponentInParent<IRocketTarget>();
            if (target != null) target.OnRocketHit(hit.point, shooter);
            else Explode(hit.point, hit.normal);
            LeaveTrail();
            Destroy(gameObject);
        }

        // 연기 꼬리를 효과 컨테이너로 옮겨 남은 연기가 사라질 때까지 둔다 (복구 · 새 회차 때 ClearAll로 함께 지워진다)
        void LeaveTrail()
        {
            if (trail == null) return;
            trail.SetParent(effects.Container, true);
            foreach (var ps in trail.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(trail.gameObject, 2.5f);
            trail = null;
        }

        void Explode(Vector3 point, Vector3 normal)
        {
            live = false;
            effects.Spawn(EffectKind.RocketExplosion, point, Quaternion.LookRotation(normal));
            foreach (var col in Physics.OverlapSphere(point, config.rocketBlastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var d = col.GetComponentInParent<IDamageable>();
                if (d != null && !(d is PlayerHealth)) d.TakeDamage(config.rocketBlastDamage, point);
            }
            LeaveTrail();
            Destroy(gameObject);
        }
    }
}
