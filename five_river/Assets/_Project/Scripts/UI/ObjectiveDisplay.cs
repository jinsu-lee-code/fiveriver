using System;
using TMPro;
using UnityEngine;

namespace FiveRivers.UI
{
    // 왼쪽 위 목표 (GDD 7번 "행동 목표 1개를 완료까지 유지"). 바뀔 때 옆에서 미끄러지며 나타난다.
    public sealed class ObjectiveDisplay : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform body;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text objective;
        [SerializeField] float showTime = 0.45f;
        [SerializeField] float slide = 24f;

        Vector2 basePosition;
        float shownAt = -99f;
        bool visible;

        public string Current { get; private set; } = "";
        public event Action Shown;

        void Awake()
        {
            basePosition = body.anchoredPosition;
            group.alpha = 0f;
        }

        public void SetLabel(string text) => label.text = text;

        public void Show(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }
            if (visible && text == Current) return;
            Current = text;
            objective.text = text;
            visible = true;
            shownAt = Time.unscaledTime;
            Shown?.Invoke();
        }

        public void Hide()
        {
            visible = false;
            Current = "";
        }

        void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / showTime);
            float target = visible ? 1f : 0f;
            group.alpha = visible ? t : Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / showTime);
            float ease = 1f - (1f - t) * (1f - t);
            body.anchoredPosition = basePosition + new Vector2(visible ? -slide * (1f - ease) : 0f, 0f);
        }
    }
}
