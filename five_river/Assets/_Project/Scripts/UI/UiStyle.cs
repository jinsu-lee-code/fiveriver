using FiveRivers.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveRivers.UI
{
    // 화면 스타일 (모던 워페어 HUD 방식 참고: 흰 글자 · 얇은 선 · 구석 배치 · 반투명 띠. 로고 · 에셋은 쓰지 않음)
    // 점수 · 킬 카운트 · 히트마커는 넣지 않는다 (CLAUDE.md 핵심 설계 규칙 6).
    public static class UiStyle
    {
        public static readonly Color Text = new Color(0.93f, 0.93f, 0.90f);
        public static readonly Color Dim = new Color(0.93f, 0.93f, 0.90f, 0.55f);
        public static readonly Color Accent = new Color(0.95f, 0.72f, 0.30f);
        public static readonly Color Warning = new Color(0.92f, 0.36f, 0.30f);
        public static readonly Color Band = new Color(0f, 0f, 0f, 0.5f);
        public const string AccentHex = "#F2B84D";

        // 안내 키 표시: 실제 바인딩을 읽어 [Z] 처럼 보여 준다 (CLAUDE.md 핵심 설계 규칙 9)
        public static string Key(InputAction action) => Key(GameControls.KeyLabel(action));
        public static string Key(string label) => $"<color={AccentHex}>[{label}]</color>";
    }
}
