using System.Text;
using FiveRivers.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FiveRivers.Tutorial
{
    // 조작 안내 화면의 배치 · 모양 (2026-11-26 피드백 "튜토리얼은 가시성이 좀 떨어진다").
    // 예전: 화면 아래 250px, 검정 50% 띠, 30px — 모래주머니 · 소총 위에 겹쳐 밤 장면에서 잘 안 보였다.
    // 지금: 조준점 위쪽 하늘(위에서 190px), 검정 78% 띠 + 왼쪽 강조색 선, 40px, 힌트 26px,
    //       띠 위에 진행 표시(점 다섯 개 + "2 / 5", ●○는 KS X 1001에 있어 맑은 고딕으로 나온다).
    //       단계를 해냈을 때의 "완료" 표시 · 클릭 소리는 분위기와 어긋나 뺐다 → 분대장의 짧은 대답(TrenchIntroDirector).
    // UiBuilder가 만든 요소를 실행 중에 다시 배치한다 (씬을 다시 만들지 않아도 된다).
    public sealed class TutorialPromptView
    {
        readonly RectTransform box;
        readonly TMP_Text prompt;
        readonly TMP_Text hint;
        readonly TMP_Text progress;

        public TutorialPromptView(CanvasGroup group, RectTransform box, TMP_Text prompt, TMP_Text hint)
        {
            this.box = box;
            this.prompt = prompt;
            this.hint = hint;

            var root = (RectTransform)group.transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -190f);
            root.sizeDelta = new Vector2(1000f, 200f);

            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = new Vector2(0f, -36f);
            box.sizeDelta = new Vector2(920f, 78f);
            var band = box.GetComponent<Image>();
            if (band != null) band.color = new Color(0f, 0f, 0f, 0.78f);

            var bar = new GameObject("AccentBar", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(box, false);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(6f, 0f);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = UiStyle.Accent;
            barImage.raycastTarget = false;

            var promptRt = prompt.rectTransform;
            promptRt.sizeDelta = new Vector2(880f, 74f);
            prompt.fontSize = 40f;
            prompt.color = UiStyle.Text;
            prompt.textWrappingMode = TextWrappingModes.NoWrap;

            var hintRt = hint.rectTransform;
            hintRt.anchorMin = hintRt.anchorMax = hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = new Vector2(0f, -126f);
            hintRt.sizeDelta = new Vector2(1000f, 74f);
            hint.fontSize = 26f;
            hint.color = new Color(UiStyle.Text.r, UiStyle.Text.g, UiStyle.Text.b, 0.85f);

            var progressRt = new GameObject("Progress", typeof(RectTransform)).GetComponent<RectTransform>();
            progressRt.SetParent(root, false);
            progressRt.anchorMin = progressRt.anchorMax = progressRt.pivot = new Vector2(0.5f, 1f);
            progressRt.anchoredPosition = Vector2.zero;
            progressRt.sizeDelta = new Vector2(920f, 30f);
            progress = progressRt.gameObject.AddComponent<TextMeshProUGUI>();
            progress.font = prompt.font;
            progress.fontSize = 22f;
            progress.alignment = TextAlignmentOptions.Center;
            progress.color = UiStyle.Dim;
            progress.richText = true;
            progress.raycastTarget = false;
            progress.characterSpacing = 4f;
        }

        public RectTransform Box => box;

        public void ShowStep(string keysAndPrompt, string hintText)
        {
            prompt.text = keysAndPrompt;
            hint.text = hintText;
        }

        public void SetProgress(int done, int total) => progress.text = Progress(done, total);

        // "●●○○○  2 / 5" (해낸 점은 강조색)
        public static string Progress(int done, int total)
        {
            done = Mathf.Clamp(done, 0, total);
            var sb = new StringBuilder();
            sb.Append($"<color={UiStyle.AccentHex}>");
            for (int i = 0; i < done; i++) sb.Append('●');
            sb.Append("</color>");
            for (int i = done; i < total; i++) sb.Append('○');
            sb.Append($"   {Mathf.Min(done + 1, total)} / {total}");
            return sb.ToString();
        }
    }
}
