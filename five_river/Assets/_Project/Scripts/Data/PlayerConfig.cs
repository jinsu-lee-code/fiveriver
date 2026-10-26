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
    }
}
