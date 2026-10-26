using FiveRivers.Core;
using FiveRivers.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveRivers.UI
{
    // 시작 화면. 회차가 없을 때(Title) 보이고, Enter · Space · 좌클릭으로 새 회차를 시작한다.
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] GameText text;
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text subtitle;
        [SerializeField] TMP_Text startPrompt;
        [SerializeField] TMP_Text operatorHint;
        [SerializeField] float inputDelay = 1.5f; // 결과 화면에서 막 돌아왔을 때 실수로 시작하지 않게

        float shownAt;
        bool wasTitle;

        void Start()
        {
            title.text = text.gameTitle;
            subtitle.text = text.gameSubtitle;
            startPrompt.text = text.startPrompt;
            operatorHint.text = text.operatorHint;
        }

        void Update()
        {
            bool isTitle = !director.Flow.IsRunning;
            if (isTitle && !wasTitle) shownAt = Time.unscaledTime;
            wasTitle = isTitle;

            group.alpha = Mathf.MoveTowards(group.alpha, isTitle ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            group.blocksRaycasts = isTitle;
            if (!isTitle) return;

            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f);
            startPrompt.alpha = pulse;

            if (Time.unscaledTime - shownAt < inputDelay) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool start = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                         || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (start) director.Flow.StartRun();
        }
    }
}
