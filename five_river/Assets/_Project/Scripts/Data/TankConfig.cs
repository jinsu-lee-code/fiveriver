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

        [Header("포탄 구덩이 (2026-11-13): 떨어진 자리 지형을 판다, 회차 · 재시도 때 원래대로")]
        [Tooltip("구덩이 반지름 m (0이면 만들지 않음)")] public float craterRadius = 1.6f;
        [Tooltip("가운데 깊이 m")] public float craterDepth = 0.55f;
        [Tooltip("가장자리로 솟는 흙 높이 m")] public float craterRim = 0.15f;
        [Tooltip("이 거리(m) 안의 참호 모래주머니 벽 토막은 무너진다 (0이면 무너지지 않음)")] public float craterBreach = 1.2f;

        [Header("조준해 들어옴 · 직격 즉사 (2026-11-24). 위 cannonDamage · cannonRadius · nearMissOffset 은 옛 규칙의 값이라 더 쓰지 않는다")]
        [Tooltip("직격으로 치는 착탄점-플레이어 거리 m. 이 안에 떨어지면 무조건 사망")] public float directRadius = 1f;
        [Tooltip("노출된 채로 몇 번째 발이 직격인가 (3 = 첫 발 멀리 · 둘째 발 가까이 · 셋째 발 직격)")] public int shotsToDirect = 3;
        [Tooltip("첫 단계 빗나감 거리 범위 m (플레이어 앞이나 뒤)")] public float farMin = 9f;
        public float farMax = 13f;
        [Tooltip("중간 단계 빗나감 거리 범위 m")] public float midMin = 3f;
        public float midMax = 5f;
        [Tooltip("직격 때 착탄점이 플레이어에서 벗어나는 최대 m")] public float directScatter = 0.4f;
        [Tooltip("빗나감 착탄점의 좌우 흩어짐 최대 m (전차-플레이어 축 기준)")] public float aimLateral = 1.5f;
        [Tooltip("지난 발 뒤로 이만큼 옮겨 서면 조준이 한 단계 뒤로 간다 m")] public float aimMoveReset = 4f;
        [Tooltip("빗나간 포탄이 이 거리 안(수평)에 떨어지면 센 흔들림 · 귀 먹먹함 · 흙 쏟아짐, 밖이면 거리에 따라 약하게 m")] public float feelCloseRange = 6.5f;

        [Header("쏘기 전 예고 (2026-11-24): 포탑 회전 소리 + 포탑이 마지막으로 살짝 조정됨. 노출 판정은 쏘는 순간 그대로")]
        [Tooltip("쏘기 이 시간(초) 전에 포탑 회전 소리를 낸다. 0이면 예고 없음. 듣고 Z로 숙일 시간이 있어야 한다")] public float warnLead = 0.8f;
        [Tooltip("예고가 시작되고 이만큼 뒤에 포탑이 한 번 조정된다 (s) / 걸리는 시간 (s)")] public float warnNudgeDelay = 0.15f;
        public float warnNudgeSeconds = 0.45f;
        [Tooltip("포탑 좌우 · 주포 위아래로 조정되는 각도 (도). 쏘기 전에 0으로 돌아온다")] public float warnNudgeYaw = 1.2f;
        public float warnNudgePitch = 0.6f;

        [Header("등장 연출 (2026-11-10 시험 제작, 일반판에서만)")]
        [Tooltip("0 = 없음(기존 그대로), 1 = Fable판(코드로 만든 연출), 2 = Blender판(Blender에서 만들어 구운 연출), 3 = 합본(두 판의 좋은 컷 + 카메라 흔들림, 2026-11-12)")]
        public int entranceTake;
    }
}
