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
        [Min(1)] public int westAssault = 4;
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
        [Min(1f)] public float expandedDamage = 50f; // 2026-11-18 사용자 지시 "총알 두 대 맞으면 죽는 걸로": 체력 100에서 두 발
        public float sightRange;

        // 2026-11-18 (사용자 지시 "동시활동은 5명 규칙으로 하지말고 유연하게 만들어 그당시에 상황이 느껴질수있는 선에서"):
        // 일반판은 구간마다 총수 · 동시 활동 상한을 따로 둔다. 전시용 짧은 흐름은 위의 mainAssault · westAssault ·
        // maxActive(5명)를 그대로 쓴다. BALANCE.md 2-1b.
        [Header("일반판 밀도 : BALANCE 2-1b")]
        [Min(1)] public int expandedMainAssault = 14;
        [Min(1)] public int expandedMainMaxActive = 9;
        [Min(1)] public int expandedWestAssault = 9;
        [Min(1)] public int expandedWestMaxActive = 6;
        [Min(0.2f)] public float expandedSpawnInterval = 1.5f;
        [Tooltip("고지 탈환: 엄폐 지점마다 곁에 더 서는 수비병 수")] [Min(0)] public int hillExtraDefenders = 1;
        [Tooltip("동시에 플레이어를 겨누는 적의 최대 수. 나머지는 참호의 아군과 교전한다(플레이어 피해 없음)")]
        [Min(1)] public int aimAtPlayerMax = 2;
        [Tooltip("살아 있는 적이 이 수보다 많을 때만 아군 사격이 적을 쓰러뜨린다 (이 수는 플레이어의 몫)")]
        [Min(0)] public int allyFloorMain = 4;
        [Min(0)] public int allyFloorWest = 3;
        [Min(0)] public int allyFloorHill = 1;
        [Range(0f, 1f)] [Tooltip("아군의 한 발이 겨눈 적을 쓰러뜨릴 확률")] public float allyKillChance = 0.2f;
        [Tooltip("출현 지점에서 옆으로 흩어지는 거리 m · 이동 속도 차이 (줄지어 같은 걸음으로 오지 않게)")]
        public float spawnSpread = 5f;
        [Range(0f, 0.3f)] public float speedSpread = 0.1f;

        [Header("쓰러진 적 : 그 회차 동안 남는다")]
        [Tooltip("이 수를 넘으면 화면 밖 · 먼 것부터 치운다")] [Min(1)] public int maxBodies = 24;
        [Tooltip("화면 안의 몸은 이 거리보다 멀 때만 치운다 (m)")] public float bodyCullDistance = 55f;
        [Tooltip("쓰러진 뒤 이 시간이 지나면 자세를 굳혀 계산을 멈춘다 (초)")] public float bodySettleAfter = 5f;
    }
}
