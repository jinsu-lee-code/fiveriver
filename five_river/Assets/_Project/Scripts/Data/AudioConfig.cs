using UnityEngine;

namespace FiveRivers.Data
{
    // 버스 볼륨 · 대사 덕킹 · 발걸음 · 먼 포격 간격. BALANCE.md 4번 (덕킹 값은 실제 시연 헤드폰으로 조정).
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "FiveRivers/Audio Config")]
    public sealed class AudioConfig : ScriptableObject
    {
        [Header("버스 볼륨 (0~1)")]
        public float master;
        public float sfx;
        public float amb;
        public float bgm;
        public float vo;
        public float ui;

        [Header("대사 중 효과음 · 환경음 덕킹")]
        public float duckDb;
        public float duckFade;

        [Header("발걸음 (m마다 한 번)")]
        public float walkStride;
        public float runStride;

        [Header("먼 포격 (S1~S4, 초)")]
        public float artilleryMin;
        public float artilleryMax;
    }
}
