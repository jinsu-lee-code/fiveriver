using System;

namespace FiveRivers.Core
{
    // 회차 시간 (초). 시작 버튼부터 사망 · 재시도 · 일시정지를 모두 포함한다.
    // timeScale과 무관하게 unscaled 시간을 받아 앞으로만 간다.
    public sealed class RunClock
    {
        public double Elapsed { get; private set; }

        public void Reset() => Elapsed = 0;

        public void AdvanceTo(double time)
        {
            if (time > Elapsed) Elapsed = time;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 디버그 창 · 테스트 전용. 뒤로도 옮길 수 있다.
        public void DebugSet(double time) => Elapsed = Math.Max(0, time);
#endif
    }
}
