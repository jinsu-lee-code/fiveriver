using FiveRivers.Core;
using FiveRivers.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveRivers.UI
{
    // 결과 화면 S7 (GDD 8번): 날짜 4초 · 이름 5초 · 편지 8초 · 제목 3초. 점수 · 킬 카운트 없음.
    // 시간은 ResultConfig(BALANCE 1-4), 문구는 GameText. 끝나면 회차가 시작 화면으로 돌아간다(DemoFlow).
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] ResultConfig config;
        [SerializeField] GameText text;
        [SerializeField] CanvasGroup group;
        [SerializeField] CanvasGroup dateGroup;
        [SerializeField] CanvasGroup nameGroup;
        [SerializeField] CanvasGroup letterGroup;
        [SerializeField] CanvasGroup titleGroup;
        [SerializeField] TMP_Text date;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text letter;
        [SerializeField] TMP_Text title;
        [SerializeField] float fadeTime = 0.8f;

        int readingPage;
        bool beganReading;

        void Start()
        {
            date.text = text.resultDate;
            nameText.text = text.resultName;
            letter.text = text.resultLetter;
            title.text = text.resultTitle;
            group.alpha = 0f;
        }

        void Update()
        {
            var flow = director.Flow;
            bool showing = flow.Section == FlowSection.Result;
            group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / fadeTime);
            if (!showing)
            {
                if (beganReading) { date.text = text.resultDate; letter.text = text.resultLetter; }
                readingPage = 0;
                beganReading = false;
                return;
            }

            float t = (float)flow.SectionTime;
            float nameAt = config.dateDuration;
            float letterAt = nameAt + config.nameDuration;
            float titleAt = letterAt + config.letterDuration;
            if (flow.IsExpanded && t >= letterAt)
            {
                ShowReadingPage(flow);
                return;
            }
            // 각 내용을 순서대로 4 · 5 · 8 · 3초 표시한다.
            dateGroup.alpha = Window(t, 0f, nameAt);
            nameGroup.alpha = Window(t, nameAt, letterAt);
            letterGroup.alpha = Window(t, letterAt, titleAt);
            titleGroup.alpha = Window(t, titleAt, titleAt + config.titleDuration);
        }

        void ShowReadingPage(DemoFlow flow)
        {
            // Holding a movement key or firing as the hill falls must not skip a page.
            var keyboard = Keyboard.current;
            bool advance = keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
            if (beganReading && advance)
            {
                if (readingPage == 2)
                {
                    flow.ReportGoalMet(FlowSection.Result);
                    return;
                }
                readingPage++;
            }
            beganReading = true;
            dateGroup.alpha = 1f;
            nameGroup.alpha = 0f;
            letterGroup.alpha = readingPage < 2 ? 1f : 0f;
            titleGroup.alpha = readingPage == 2 ? 1f : 0f;
            switch (readingPage)
            {
                case 0:
                    date.text = text.mansuPage + "     " + text.letterNext;
                    letter.text = text.resultLetter;
                    break;
                case 1:
                    date.text = text.seokguPage + "     " + text.letterClose;
                    letter.text = text.seokguLetter;
                    break;
                default:
                    date.text = text.missionReturn;
                    break;
            }
        }

        float Window(float t, float from, float to)
        {
            if (t < from || t > to) return 0f;
            return Mathf.Min(1f, (t - from) / fadeTime, (to - t) / fadeTime);
        }
    }
}
