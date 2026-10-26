using UnityEngine;

namespace FiveRivers.Data
{
    // 튜토리얼 판정 · 안내 시간. BALANCE.md 4번.
    [CreateAssetMenu(fileName = "TutorialConfig", menuName = "FiveRivers/Tutorial Config")]
    public sealed class TutorialConfig : ScriptableObject
    {
        public float highlightAfter;
        public float hintAfter;
        public float lookDegrees;  // 이만큼 둘러보면 "시점" 완료
        public float moveMeters;   // 이만큼 걸으면 "이동" 완료
    }
}
