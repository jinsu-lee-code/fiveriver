using UnityEngine;

namespace FiveRivers.Data
{
    // 전차 판정 · 재시도 · 행동 값. BALANCE.md 2-3 · 3번.
    [CreateAssetMenu(fileName = "TankConfig", menuName = "FiveRivers/Tank Config")]
    public sealed class TankConfig : ScriptableObject
    {
        [Header("판정 : BALANCE 2-3")]
        [Tooltip("전차 정면에서 쏜 사람 쪽까지의 수평 각도가 이 값 이상이면 측면(유효)")]
        public float sideAngle;
        public int hitsToStop;

        [Header("재시도 : BALANCE 3")]
        public float attempt1;
        public float attempt2;
        public int supportAfterFails;

        [Header("행동 : BALANCE 2-3")]
        public float approachSpeed;
        public float turretTurnSpeed;
        public float fireInterval;
        public float cannonDamage;
        public float cannonRadius;
        public float nearMissOffset;
    }
}
