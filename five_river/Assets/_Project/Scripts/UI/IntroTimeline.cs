using UnityEngine;

namespace FiveRivers.UI
{
    // 시작 컷씬의 시간표 (순수 계산, 테스트 대상). 시각은 컷씬을 시작한 뒤의 초.
    // 검게 덮기 → 날짜 · 장소 · 설명이 한 줄씩 → 머무름 → 글자가 사라짐 → 회차 시작 → 참호가 밝아짐.
    public sealed class IntroTimeline
    {
        public float FadeIn = 0.6f;     // 타이틀 위로 검게 덮이는 시간
        public float LineStep = 0.7f;   // 다음 줄이 나오기까지
        public float LineFade = 0.8f;   // 한 줄이 나타나는 시간
        public float Hold = 3.8f;       // 마지막 줄까지 다 보인 뒤 머무는 시간
        public float TextOut = 0.6f;    // 글자가 사라지는 시간
        public float Reveal = 1.6f;     // 회차 시작 뒤 참호가 밝아지는 시간
        public float SkipAfter = 0.8f;  // 시작 키가 그대로 건너뛰기로 읽히지 않게
        public int Lines = 3;

        public float Time { get; private set; }
        public float TextEnd => FadeIn + (Lines - 1) * LineStep + LineFade + Hold;
        public float RevealAt => TextEnd + TextOut;
        public float End => RevealAt + Reveal;
        public bool Finished => Time >= End;
        public bool Revealing => Time >= RevealAt;

        public void Restart() => Time = 0f;

        public void Advance(float dt)
        {
            if (dt > 0f) Time = Mathf.Min(End, Time + dt);
        }

        // 글자가 다 나오기 전이면 글자가 사라지는 곳으로 건너뛴다.
        public bool Skip()
        {
            if (Time < SkipAfter || Time >= TextEnd) return false;
            Time = TextEnd;
            return true;
        }

        public float BlackAlpha => Time < RevealAt
            ? Mathf.Clamp01(Time / FadeIn)
            : 1f - Mathf.Clamp01((Time - RevealAt) / Reveal);

        public float LineAlpha(int index)
        {
            float appear = Mathf.Clamp01((Time - FadeIn - index * LineStep) / LineFade);
            float vanish = 1f - Mathf.Clamp01((Time - TextEnd) / TextOut);
            return Mathf.Min(appear, vanish);
        }

        public float SkipHintAlpha => Time >= SkipAfter && Time < TextEnd ? Mathf.Clamp01((Time - SkipAfter) / 0.5f) : 0f;
    }
}
