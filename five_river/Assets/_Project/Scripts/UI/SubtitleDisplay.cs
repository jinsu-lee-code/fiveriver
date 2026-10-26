using System;
using System.Collections.Generic;
using FiveRivers.Data;
using TMPro;
using UnityEngine;

namespace FiveRivers.UI
{
    // 서사 자막 (화면 아래 가운데, 2줄 이내, 목표와 다른 위치 — GDD 7번). 말하는 사람은 강조색.
    // 줄이 시작될 때 LineStarted로 음성 재생 · 효과음 덕킹을 알린다.
    public sealed class SubtitleDisplay : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text text;
        [SerializeField] float fade = 0.2f;

        readonly Queue<GameText.Line> queue = new Queue<GameText.Line>();
        float endTime;
        bool showing;

        public bool Speaking => showing;
        public event Action<GameText.Line> LineStarted;
        public event Action LineEnded;

        void Awake()
        {
            group.alpha = 0f;
        }

        public void Say(GameText.Line line)
        {
            if (string.IsNullOrEmpty(line.text)) return;
            queue.Enqueue(line);
            if (!showing) Next();
        }

        // 음성이 자막 시간보다 길면 음성이 끝날 때까지 남긴다
        public void ExtendCurrent(float seconds)
        {
            if (showing) endTime = Mathf.Max(endTime, Time.unscaledTime + seconds);
        }

        public void Clear()
        {
            queue.Clear();
            if (showing) LineEnded?.Invoke();
            showing = false;
            group.alpha = 0f;
        }

        void Next()
        {
            if (queue.Count == 0)
            {
                if (showing) LineEnded?.Invoke();
                showing = false;
                return;
            }
            var line = queue.Dequeue();
            text.text = string.IsNullOrEmpty(line.speaker) ? line.text : $"<color={UiStyle.AccentHex}>{line.speaker}</color>   {line.text}";
            endTime = Time.unscaledTime + line.duration;
            showing = true;
            LineStarted?.Invoke(line);
        }

        void Update()
        {
            if (showing && Time.unscaledTime >= endTime) Next();
            group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / fade);
        }
    }
}
