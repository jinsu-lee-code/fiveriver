using UnityEngine;

namespace FiveRivers.Data
{
    // 적 구성 · 행동. BALANCE.md 2-1번.
    [CreateAssetMenu(fileName = "WaveConfig", menuName = "FiveRivers/Wave Config")]
    public sealed class WaveConfig : ScriptableObject
    {
        [Header("구성 : BALANCE 2-1")]
        public int scouts;
        public int mainAssault;
        public int maxActive;
        public float spawnInterval;

        [Header("적 개체")]
        public float health;
        public float moveSpeed;
        public float retreatSpeed;
        public float coverHoldMin;
        public float coverHoldMax;

        [Header("사격")]
        public float fireIntervalMin;
        public float fireIntervalMax;
        public float accuracyDark;      // 어두울 때 명중률
        public float accuracyLit;       // 조명탄 아래 명중률
        public float crouchedFactor;    // 플레이어가 Z 자세면 곱함
        public float damage;
        public float sightRange;
    }
}
