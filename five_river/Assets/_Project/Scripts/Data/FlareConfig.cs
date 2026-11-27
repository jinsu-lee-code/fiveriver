using UnityEngine;

namespace FiveRivers.Data
{
    // 조명탄 리듬. BALANCE.md 2-5번. "어두울 때 이동 · 보충, 밝아지면 엄폐 사격" (GDD 2번 핵심 경험 1)
    [CreateAssetMenu(fileName = "FlareConfig", menuName = "FiveRivers/Flare Config")]
    public sealed class FlareConfig : ScriptableObject
    {
        public float scoutFlareDelay;   // S2 시작 뒤 첫 조명탄
        public float assaultFirstDelay; // S3 시작 뒤 첫 조명탄
        public float assaultInterval;   // S3 조명탄 간격 (발사 ~ 발사)
        public float burnTime;          // 밝게 타는 시간
        public float fadeTime;          // 꺼지는 시간
        public float launchHeight;
        public float fallSpeed;
        public float swayAmplitude;
        public float lightIntensity;
        public float lightRange;

        // 조명탄 아래 땅에서 튕겨 올라오는 빛 (부족한 점 2번, 2026-11-26). 땅에 대칭으로 비춘 자리(땅 아래)의 그림자 없는 점광:
        // 세기 = 조명탄 세기 × 흙 반사율 × 세기 배율, 색 = 조명탄 색 × 흙색. 배율 0 이면 끔. BALANCE.md 2-5번.
        [Range(0f, 1f)] public float bounceAlbedo = 0.22f;        // 마른 흙 반사율
        [Range(0f, 2f)] public float bounceStrength = 1f;         // 밤 분위기에 맞춘 배율
        public Color bounceSoil = new Color(0.62f, 0.5f, 0.38f);  // 흙색 (색만 씀, 밝기는 반사율)
        public float bounceRangePad = 30f;                        // 대칭점에서 땅 위까지 거리 + 이만큼

        // 주 공세 조명탄 첫 연출 컷의 가까운 장면 (2026-11-27): 조명탄 바로 아래라 땅이 하얗게 날아가지 않게, 그 순간 밤 노출에서
        // 이만큼(스톱) 낮춘다. 노출만 바꾸고 밤 채도 · 대비는 그대로 둔다(전에는 모두 기본값으로 덮여 풀이 진한 초록이 됐다). BALANCE.md 2-5번.
        [Range(0f, 3f)] public float revealCloseStopsDown = 0.6f;
    }
}
