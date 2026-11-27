using UnityEngine;

namespace FiveRivers.Data
{
    // 쓰러짐 물리(부분 래그돌) · 맞는 순간 반응 (부족한 점 3 · 6번, 2026-11-26). BALANCE.md 2-1d.
    // 래그돌 뼈대는 메뉴 FiveRivers/Setup/Enemy Ragdoll 이 적 프리팹에 만든다.
    [CreateAssetMenu(fileName = "BodyPhysicsConfig", menuName = "FiveRivers/Body Physics Config")]
    public sealed class BodyPhysicsConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Data/BodyPhysicsConfig.asset";

        [Header("사망 동작 → 물리 넘기기")]
        [Tooltip("가슴이 땅에서 이 높이(m) 아래로 내려오면 물리로 넘긴다 — 쓰러지는 동작(엄폐물 피하기 포함)은 그대로 보이고, 눕는 자리만 물리. " +
                 "무릎 꿇은 채 넘기면 힘 없는 몸이 꿇어앉은 채 버티다 천천히 넘어져 어색했다 (2026-11-26 촬영)")]
        public float handoverChest = 0.4f;
        [Tooltip("넘기기 전 최소 시간(초)")] public float handoverEarliest = 0.3f;
        [Tooltip("그래도 이 시간(초)이 지나면 넘긴다")] public float handoverLatest = 3.4f;
        [Tooltip("넘길 때 몸 둘레 이 거리(m) 안에 충돌체 없는 아트 바위 · 모래주머니(CoverVisuals)가 있으면 물리로 넘기지 않는다 (몸이 그 안으로 빠짐)")]
        public float coverClear = 0.35f;
        [Tooltip("넘길 때 뼈 움직임에서 이어받는 속도 상한 (m/s, 끊긴 프레임에 날아가지 않게)")] public float inheritMaxSpeed = 2.5f;
        [Tooltip("넘길 때 땅에 묻혀 있으면 이만큼(m)까지 들어 올린다")] public float liftMost = 0.35f;
        [Tooltip("넘길 때 이보다 깊이(m) 묻혀 있으면 물리로 넘기지 않는다 (가파른 비탈에서 접힌 다리가 땅속에 남음)")] public float sunkSkip = 0.12f;
        [Tooltip("몸 조각 회전 감쇠 (비탈에서 굴러 내려가지 않게, 메뉴가 Rigidbody 에 넣음)")] public float spinDamping = 1.5f;
        [Tooltip("이어받는 회전 속도 상한 (rad/s)")] public float inheritMaxSpin = 8f;

        [Header("동시에 물리로 넘기는 시신 수 상한 (조명탄 · 최대 적 수 구간 성능). 넘으면 사망 동작 그대로 굳힘")]
        [Min(0)] public int maxSimulating = 6;
        [Tooltip("플레이어 카메라에서 이 거리(m) 밖이면 물리로 넘기지 않는다 (먼 적은 동작 갱신이 드물어 넘긴 속도가 낡아 튐)")]
        public float physicsRange = 45f;
        [Tooltip("엉덩이 아래 지형 경사가 이 각도(도)를 넘으면 물리로 넘기지 않는다 (가파른 비탈에서 꿇은 다리가 땅속에 남음)")]
        public float maxSlope = 18f;
        [Tooltip("물리 중 어느 조각이든 이 깊이(m)보다 땅에 묻히면 몸 전체를 그만큼 들어 올린다")]
        public float sinkHold = 0.06f;
        [Tooltip("물리 중 무릎 · 팔꿈치가 이 각도(도)보다 반대로 꺾이면 곧게 돌려놓는다 (사람이 할 수 없는 동작 금지)")]
        public float hingeSlack = 3f;

        [Header("총알 · 폭발")]
        [Tooltip("물리로 넘길 때 맞은 부위에 총알 방향으로 더하는 속도 (m/s)")] public float bulletPush = 0.7f;
        [Tooltip("바주카 폭발로 죽으면 처음부터 물리 + 이 속도 변화(폭발 가운데, m/s, 거리에 따라 줄어듦). 넘어지고 밀리는 정도 — " +
                 "사람이 몇 m씩 날아가는 영화식 과장은 하지 않는다 (감독 조건 2026-11-26)")] public float blastSpeed = 2.5f;
        public float blastRadius = 5f;
        [Tooltip("폭발 힘을 위로 드는 정도 (AddExplosionForce upwardsModifier). 폭발 가운데를 이만큼 낮춰 봄 — 발이 땅으로 밀려 들어가지 않게(0.2 에서 1 m 폭발에 8 cm 관통), 세기가 작아 치솟지 않음")] public float blastLift = 0.6f;

        [Header("멈춤")]
        [Tooltip("모든 몸 조각이 이 속도(m/s) 아래로 이만큼(초) 있으면 굳힌다 (그 뒤는 WaveConfig.bodySettleAfter 로 동작 계산도 멈춤)")]
        public float restSpeed = 0.12f;
        public float restHold = 0.5f;
        [Tooltip("물리 시간 상한(초). 넘으면 그 자리에서 굳힌다")] public float physicsMost = 4f;
        [Header("몸 재질 (흙 · 모래주머니 위에서 미끄러지지 않게)")]
        [Range(0f, 1f)] public float friction = 0.9f;

        [Header("맞는 순간 꺾임 (0.05초에 최대, 0.25초에 사라짐)")]
        public float flinchRise = 0.05f;
        public float flinchFade = 0.25f;
        [Tooltip("머리 · 가슴 · 위팔 · 넓적다리 · 엉덩이가 총알 반대쪽으로 꺾이는 최대 각도(도)")]
        public float flinchHead = 18f, flinchChest = 10f, flinchArm = 24f, flinchLeg = 14f, flinchHips = 8f;

        [Header("맞은 방향으로 쓰러지는 쪽 고르기")]
        [Tooltip("이 확률로 맞은 방향에 맞는 동작을 먼저 고른다 (앞에서 맞으면 뒤로, 뒤에서 맞으면 앞으로). 나머지는 지금처럼 무작위")]
        [Range(0f, 1f)] public float directionBias = 0.65f;
    }
}
