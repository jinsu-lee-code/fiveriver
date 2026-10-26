namespace FiveRivers.Core
{
    // 값은 끝에만 추가한다 (에셋 · 기록이 정수로 저장함).
    public enum FlowSection
    {
        Title = 0,
        Tutorial = 1,    // S1 조작 학습
        Scout = 2,       // S2 첫 교전
        Assault = 3,     // S3 주 공세
        Silence = 4,     // S4 재정비와 정적
        Tank = 5,        // S5 전차전
        AllySupport = 6, // S5 아군 지원 (5초)
        Dawn = 7,        // S6 새벽
        Result = 8,      // S7 결과 화면
    }

    public enum TransitionReason
    {
        RunStart = 0,
        GoalMet = 1,      // 구간 완료 조건 충족 (씬이 보고)
        SectionCap = 2,   // 구간 상한 도달
        Timer = 3,        // 정해진 길이가 끝남 (강제 정적 · 아군 지원 · 새벽 · 결과)
        ForcedTank = 4,   // 7:00 강제 전환
        TankDeadline = 5, // 7:25 전차 진입 보장
        ForcedEnding = 6, // 8:15 강제 전환
        HardCap = 7,      // 9:00 절대 상한 (안전장치)
        Abort = 8,        // 운영자 재시작
    }

    public readonly struct FlowTransition
    {
        public readonly FlowSection From;
        public readonly FlowSection To;
        public readonly TransitionReason Reason;
        public readonly double Time;

        public FlowTransition(FlowSection from, FlowSection to, TransitionReason reason, double time)
        {
            From = from;
            To = to;
            Reason = reason;
            Time = time;
        }

        public override string ToString() => $"{From} -> {To} ({Reason}) @ {Time:0.###}";
    }
}
