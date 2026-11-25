using System;
using FiveRivers.Core;
using FiveRivers.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FiveRivers.UI
{
    // 시작 컷씬 (2026-11-26 피드백 "처음에 컷씬 하나, 검은 배경에 다부동 전투 날짜").
    // 타이틀에서 시작하면 검은 화면에 날짜 · 장소 · 설명을 한 줄씩 보이고, 글자가 사라질 때 회차를 시작해
    // 참호로 밝아진다. 회차(DemoFlow)는 이때 시작하므로 학습 상한 · 회차 시계는 컷씬 시간을 세지 않는다.
    // 화면은 실행 중에 만든다 (씬을 다시 만들지 않아도 된다). Enter · Space · 좌클릭으로 글자 부분을 건너뛴다.
    public sealed class IntroCard : MonoBehaviour
    {
        readonly IntroTimeline timeline = new IntroTimeline();
        CanvasGroup group;
        Image black;
        TMP_Text[] lines;
        TMP_Text skipHint;
        Action beginRun;
        InputGate gate;
        bool begun;
        bool blocking;

        public bool Playing { get; private set; }

        public static IntroCard Create(Transform canvasRoot, UiFonts fonts, GameText text)
        {
            var root = Stretch(new GameObject("IntroCard", typeof(RectTransform)).GetComponent<RectTransform>(), canvasRoot);
            root.SetAsLastSibling(); // 자막 · 목표 · 타이틀 위
            var card = root.gameObject.AddComponent<IntroCard>();
            card.group = root.gameObject.AddComponent<CanvasGroup>();
            card.group.blocksRaycasts = false;
            card.group.interactable = false;

            card.black = Stretch(new GameObject("Black", typeof(RectTransform)).GetComponent<RectTransform>(), root)
                .gameObject.AddComponent<Image>();
            card.black.color = Color.black;
            card.black.raycastTarget = false;

            TMP_FontAsset serif = fonts != null ? fonts.Letter : null;
            TMP_FontAsset sans = fonts != null ? fonts.Ui : null;
            card.lines = new[]
            {
                Label(root, "Date", text.introDate, serif, 64f, UiStyle.Text, new Vector2(0f, 70f), new Vector2(1400f, 90f), 10f),
                Label(root, "Place", text.introPlace, serif, 32f, UiStyle.Dim, new Vector2(0f, 0f), new Vector2(1400f, 50f), 6f),
                Label(root, "Context", text.introContext, sans, 26f, UiStyle.Dim, new Vector2(0f, -100f), new Vector2(1400f, 110f), 0f),
            };
            card.timeline.Lines = card.lines.Length;
            card.skipHint = Label(root, "Skip", text.introSkip, sans, 20f, UiStyle.Dim, Vector2.zero, new Vector2(400f, 40f), 0f);
            var skipRt = card.skipHint.rectTransform;
            skipRt.anchorMin = skipRt.anchorMax = skipRt.pivot = new Vector2(1f, 0f);
            skipRt.anchoredPosition = new Vector2(-60f, 50f);
            card.skipHint.alignment = TextAlignmentOptions.BottomRight;

            card.Apply();
            root.gameObject.SetActive(false);
            return card;
        }

        // beginRun 은 글자가 사라진 뒤 검은 화면이 걷히기 시작할 때 한 번 불린다.
        public void Play(Action beginRun, InputGate gate)
        {
            if (Playing) return;
            this.beginRun = beginRun;
            this.gate = gate;
            begun = false;
            timeline.Restart();
            Playing = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Apply();
        }

        void Update()
        {
            if (!Playing) return;
            if (SkipPressed()) timeline.Skip();
            timeline.Advance(Time.unscaledDeltaTime);

            if (!begun && timeline.Revealing)
            {
                begun = true;
                // 밝아지는 동안 앞부분은 손을 묶어 둔다: 검은 화면 아래에서 시점이 돌아가 있지 않게
                if (gate != null) { gate.Block(this); blocking = true; }
                beginRun?.Invoke();
            }
            if (blocking && timeline.Time >= timeline.RevealAt + timeline.Reveal * 0.5f) Unblock();

            Apply();
            if (timeline.Finished) Stop();
        }

        void Stop()
        {
            Unblock();
            Playing = false;
            gameObject.SetActive(false);
        }

        void OnDisable() => Unblock();

        void Unblock()
        {
            if (!blocking) return;
            blocking = false;
            if (gate != null) gate.Unblock(this);
        }

        void Apply()
        {
            black.color = new Color(0f, 0f, 0f, timeline.BlackAlpha);
            for (int i = 0; i < lines.Length; i++) lines[i].alpha = timeline.LineAlpha(i);
            skipHint.alpha = timeline.SkipHintAlpha * 0.6f;
        }

        static bool SkipPressed()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                   || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        static RectTransform Stretch(RectTransform rt, Transform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static TMP_Text Label(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color,
            Vector2 pos, Vector2 box, float spacing)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.characterSpacing = spacing;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.text = value ?? "";
            return t;
        }
    }
}
