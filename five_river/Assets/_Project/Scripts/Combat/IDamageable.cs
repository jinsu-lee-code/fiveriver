using UnityEngine;

namespace FiveRivers.Combat
{
    public interface IDamageable
    {
        // from: 공격이 온 위치 (피격 방향 표시용)
        void TakeDamage(float amount, Vector3 from);
    }
}
