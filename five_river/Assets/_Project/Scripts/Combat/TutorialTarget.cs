using System;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 튜토리얼 표적 (S1 "표적 1회 명중"). 쓰러지지 않고 맞은 것만 알린다.
    public sealed class TutorialTarget : MonoBehaviour, IDamageable
    {
        public event Action Hit;
        Quaternion restRotation;
        float hitAt = -10f;

        void Awake() { restRotation = transform.localRotation; }

        void LateUpdate()
        {
            float age = Time.time - hitAt;
            float bend = age >= 0f && age < 0.55f ? Mathf.Sin(age * 32f) * Mathf.Exp(-age * 9f) * 3f : 0f;
            transform.localRotation = restRotation * Quaternion.Euler(bend, 0f, bend * 0.25f);
        }

        void OnDisable() { transform.localRotation = restRotation; hitAt = -10f; }

        public void TakeDamage(float amount, Vector3 from)
        {
            hitAt = Time.time;
            Hit?.Invoke();
        }
    }
}
