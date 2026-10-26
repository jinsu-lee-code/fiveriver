using System;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 플레이어 체력. 일정 시간 맞지 않으면 회복한다. 8:15 이후 · 결말 중에는 피해를 받지 않는다 (FLOW 3-3 불변 4).
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] PlayerConfig config;
        [SerializeField] FlowDirector director;

        float lastDamageTime = -999f;
        bool dead;

        public float Current { get; private set; }
        public float Max => config.maxHealth;
        public float Fraction => Max > 0f ? Current / Max : 0f;
        public bool Invulnerable { get; set; } // 디버그 창

        public event Action<float, Vector3> Damaged;
        public event Action Died;

        void Awake()
        {
            Restore();
        }

        void Update()
        {
            if (dead || Current >= Max) return;
            if (Time.time - lastDamageTime >= config.regenDelay)
                Current = Mathf.Min(Max, Current + config.regenPerSecond * Time.deltaTime);
        }

        public void TakeDamage(float amount, Vector3 from)
        {
            var flow = director.Flow;
            if (dead || Invulnerable || !flow.IsRunning || flow.CombatLocked || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            lastDamageTime = Time.time;
            Damaged?.Invoke(amount, from);
            if (Current <= 0f)
            {
                dead = true;
                Died?.Invoke();
            }
        }

        public void Restore()
        {
            Current = config.maxHealth;
            dead = false;
            lastDamageTime = -999f;
        }
    }
}
