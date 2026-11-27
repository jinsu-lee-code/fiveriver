using FiveRivers.Core;
using FiveRivers.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveRivers.UI
{
    // 시작 화면. 회차가 없을 때(Title) 보이고, Enter · Space · 좌클릭으로 새 회차를 시작한다.
    // 2026-11-26: 시작하면 먼저 검은 화면 컷씬(IntroCard: 날짜 · 장소)을 보이고, 그 끝에서 회차를 시작한다.
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

        TMP_Text soundSetting;
        IntroCard intro;
        float shownAt;
        bool wasTitle;

        // The "operator restart Ctrl + Shift + R" line under the title: only on the exhibition run (legacyExhibitMode),
        // never on the general release (DemoFlow.IsExpanded)
        public static bool ShowsOperatorHint(bool expandedRun) => !expandedRun;

        void Start()
        {
            title.text = text.gameTitle;
            subtitle.text = text.gameSubtitle;
            startPrompt.text = text.startPrompt;
            operatorHint.text = text.operatorHint;
            // 2026-11-27 (검수 itch_20261126/06_title_a): the operator's restart key belongs to the exhibition stand; the
            // general release does not show it (the words stay in GameText.operatorHint, the key itself still works)
            operatorHint.gameObject.SetActive(ShowsOperatorHint(director != null && director.Flow != null && director.Flow.IsExpanded));
            soundSetting = GameSoundSetting.AddRowBelow(startPrompt);
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) intro = IntroCard.Create(canvas.rootCanvas.transform, canvas.rootCanvas.GetComponent<UiFonts>(), text);
        }

        void Update()
        {
            bool isTitle = !director.Flow.IsRunning;
            if (isTitle && !wasTitle) shownAt = Time.unscaledTime;
            wasTitle = isTitle;

            group.alpha = Mathf.MoveTowards(group.alpha, isTitle ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            group.blocksRaycasts = isTitle;
            if (!isTitle) return;
            if (intro != null && intro.Playing) return; // 컷씬이 시작 키를 받는다
            if (GameSoundSetting.HandleInput(soundSetting)) return;

            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 2.4f);
            startPrompt.alpha = pulse;

            if (Time.unscaledTime - shownAt < inputDelay) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool start = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                         || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            if (!start) return;
            if (intro != null) intro.Play(() => director.Flow.StartRun(), director.InputGate);
            else director.Flow.StartRun();
        }
    }
}
