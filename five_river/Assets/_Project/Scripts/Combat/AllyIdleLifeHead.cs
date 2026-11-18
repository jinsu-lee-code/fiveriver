using UnityEngine;

namespace FiveRivers.Combat
{
    // AllyIdleLife 의 머리 쪽 절반 (2026-11-18). AllyCombatant 가 LateUpdate 에서 소총을 맞추고 머리를
    // 들판 쪽으로 돌린 뒤에 실행되어야 고개 동작이 덮이지 않는다. HeadBow(200)보다는 먼저.
    [DefaultExecutionOrder(150), DisallowMultipleComponent]
    public sealed class AllyIdleLifeHead : MonoBehaviour
    {
        AllyIdleLife life;
        internal void Bind(AllyIdleLife owner) => life = owner;
        void LateUpdate() { if (life != null && life.isActiveAndEnabled) life.ApplyHead(); }
    }
}
