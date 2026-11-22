using UnityEngine;

namespace FiveRivers.Data
{
    // M1 개런드 · 바주카. BALANCE.md 2-2번. 장전 · 꺼내기 시간은 ANIM_SPEC 클립 길이와 맞춘다.
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "FiveRivers/Weapon Config")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Header("M1")]
        public int m1ClipSize;
        public int m1StartReserve;
        public int m1MaxReserve;
        public float m1FireInterval;
        public float m1Damage;
        public float m1Range;
        public float m1HipSpread;   // 도
        public float m1AimSpread;   // 도
        public float m1RecoilPitch; // 도
        public float m1ReloadEmptyTime;
        public float m1ReloadPartialTime;
        public float m1EquipTime;
        public float m1UnequipTime;

        [Header("M1 넉넉한 판정 (보이는 적만. 0이면 끔)")]
        [Tooltip("조준 사격: 가늠쇠에서 이 각도(도) 안이면 명중")] public float m1AimForgive = 1.2f;
        [Tooltip("지향 사격: 십자선에서 이 각도(도) 안이면 명중")] public float m1HipForgive = 0.8f;
        [Tooltip("각도와 상관없이 몸에서 이만큼(m)은 봐준다 (근거리)")] public float m1ForgiveLeast = 0.3f;
        [Tooltip("아무리 멀어도 몸에서 이보다(m) 더는 안 봐준다")] public float m1ForgiveMost = 1f;

        [Header("조준 (시야각)")]
        public float hipFov;
        public float m1AimFov;
        public float aimBlendTime;

        [Header("바주카")]
        public int bazookaRockets;   // 장전 1발 포함 전체
        public float bazookaReloadTime;
        public float bazookaEquipTime;
        public float bazookaAimFov;
        public float bazookaRecoilPitch;
        public float rocketSpeed;
        public float rocketLifetime;
        public float rocketBlastRadius;
        public float rocketBlastDamage;

        [Header("보충 (F)")]
        public float resupplyDistance;
    }
}
