using FiveRivers.Core;
using FiveRivers.Data;
using TMPro;
using UnityEngine;

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
            if (!showing) return;

            float t = (float)flow.SectionTime;
            float nameAt = config.dateDuration;
            float letterAt = nameAt + config.nameDuration;
            float titleAt = letterAt + config.letterDuration;
            // 날짜 · 이름은 편지가 나올 때까지 남고, 마지막 3초에는 제목만 남는다
            dateGroup.alpha = Window(t, 0f, letterAt);
            nameGroup.alpha = Window(t, nameAt, letterAt);
            letterGroup.alpha = Window(t, letterAt, titleAt);
            titleGroup.alpha = Window(t, titleAt, titleAt + config.titleDuration + fadeTime);
        }

        float Window(float t, float from, float to)
        {
            if (t < from || t > to) return 0f;
            return Mathf.Min(1f, (t - from) / fadeTime, (to - t) / fadeTime);
        }
    }
}
