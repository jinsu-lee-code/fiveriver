using UnityEngine;

namespace FiveRivers.Data
{
    // 플레이어 이동 · 시점 · 체력. BALANCE.md 2-4번.
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "FiveRivers/Player Config")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("이동 (m/s)")]
        public float walkSpeed;
        public float sprintSpeed;
        public float crouchSpeed;
        public float aimSpeedScale;
        public float gravity;

        [Header("자세 (m)")]
        public float standEyeHeight;
        public float crouchEyeHeight;
        public float stanceChangeSpeed;

        [Header("시점")]
        public float lookSensitivity; // 도 / 마우스 픽셀
        public float aimLookScale;
        public float pitchLimit;
        public float recoilRecovery; // 도 / 초

        [Header("체력")]
        public float maxHealth;
        public float regenDelay;
        public float regenPerSecond;

        [Header("크게 맞은 뒤 몸에 남는 것 (2026-11-24) : BALANCE 2-4b")]
        [Tooltip("한 번에 최대 체력의 이 비율 이상 맞으면 크게 맞은 것")] [Range(0.05f, 1f)] public float woundBigHitFraction = 0.4f;
        [Tooltip("이 비율 이상이면 귀 울림 · 먹먹함도 난다")] [Range(0.05f, 1f)] public float woundSevereFraction = 0.75f;
        [Tooltip("효과가 남는 시간 범위 s (맞은 크기에 따라, 겹쳐도 최대를 넘지 않음)")] public float woundMinSeconds = 10f;
        public float woundMaxSeconds = 20f;
        [Tooltip("끝나기 이 시간 전부터 서서히 풀린다 s")] public float woundFadeSeconds = 4f;
        [Tooltip("문턱 크기로 맞았을 때 쌓이는 세기 (가득 맞으면 1)")] [Range(0.1f, 1f)] public float woundBaseIntensity = 0.55f;
        [Tooltip("겹쳐 쌓여도 세기는 이 값에서 멈춘다")] [Range(0.1f, 1f)] public float woundIntensityCap = 1f;
        [Tooltip("손 떨림 최대 각도 (도). 허리춤 · 조준 중. 조준점이 표적 크기의 일부만 흔들리게")] public float woundTremorHipDegrees = 0.35f;
        public float woundTremorAimDegrees = 0.10f;
        [Tooltip("손 떨림 기본 주파수 Hz (2~4 불규칙 + 0.3 숨쉬기가 따로 섞임)")] public float woundTremorHz = 3f;
        [Tooltip("가쁜 숨소리 최대 세기 0~1")] [Range(0f, 1f)] public float woundBreathMax = 0.8f;
        [Tooltip("귀 울림 · 먹먹함 시간 s / 세기 0~1 (큰 피해일 때만)")] public float woundDeafSeconds = 2.5f;
        [Range(0f, 1f)] public float woundDeafAmount = 0.55f;
    }
}
