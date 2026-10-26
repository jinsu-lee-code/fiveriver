using System;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 튜토리얼 표적 (S1 "표적 1회 명중"). 쓰러지지 않고 맞은 것만 알린다.
    public sealed class TutorialTarget : MonoBehaviour, IDamageable
    {
        public event Action Hit;

        public void TakeDamage(float amount, Vector3 from)
        {
            Hit?.Invoke();
        }
    }
}
