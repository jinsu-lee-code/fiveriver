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
    }
}
