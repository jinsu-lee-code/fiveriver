using UnityEngine;

namespace FiveRivers.Core
{
    // 맵의 위치 표식 모음. Setup 메뉴(FiveRivers/Setup/Dabudong Scene)가 채운다.
    public sealed class LevelPoints : MonoBehaviour
    {
        [Header("플레이어")]
        public Transform playerSpawn;      // S1 · CP1 · CP2
        public Transform tankCheckpoint;   // CP3 전차 직전
        public Transform moundTop;         // 측면 둔덕 (전차 측면이 보이는 곳)

        [Header("적")]
        public Transform[] enemySpawns;
        public Transform[] scoutCovers;
        public Transform[] assaultCovers;
        public Transform[] retreatPoints;

        [Header("전차")]
        public Transform tankStart;
        public Transform tankHold;

        [Header("소품 · 인물")]
        public Transform[] ammoCrates;
        public Transform target;
        public Transform parkSpot;
        public Transform blanketSpot;
        public Transform[] allySpots;
        public Transform reinforcementsStart;
        public Transform reinforcementsEnd;
    }
}
