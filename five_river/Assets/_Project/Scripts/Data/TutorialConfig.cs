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

        // 2026-11-26 참호 방어로 이어지는 안내 (BALANCE.md 4번). 에셋에 값이 없으면 이 초기값이 쓰인다.
        [Tooltip("회차 시작 뒤 첫 조작 안내가 뜨기까지 (분대장 첫 대사를 듣는 시간)")]
        public float promptDelay = 4f;
        [Tooltip("조작을 다 익힌 뒤 정찰병이 오기까지 (\"제자리 지켜라\" 대사와 목표를 보는 시간)")]
        public float holdBridge = 4.5f;
    }
}
