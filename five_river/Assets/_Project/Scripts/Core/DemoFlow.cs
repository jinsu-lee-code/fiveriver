using System;
using FiveRivers.Data;

namespace FiveRivers.Core
{
    // 회차 진행 규칙 (FLOW.md 3번). 씬을 직접 건드리지 않고 Tick과 보고만 받아 이벤트를 낸다.
    // 전환 시각은 프레임 시각이 아니라 규칙이 정한 시각이며, 한 Tick 안에서 여러 전환이 차례로 일어날 수 있다.
    public sealed class DemoFlow
    {
        // 같은 시각에 걸리면 위쪽이 먼저 (FLOW.md 3-2)
        enum Boundary { None, ForcedEnding, ForcedTank, SectionEnd, TankDeadline, HardCap }

        readonly FlowConfig config;
        readonly RunClock clock = new RunClock();

        double sectionStart;
        double silenceGoalAt = -1; // S4 완료 보고 시각, 미보고면 음수
        bool forcedSilence;        // 7:00 강제 전환으로 들어온 정적

        public DemoFlow(FlowConfig config)
        {
            this.config = config ? config : throw new ArgumentNullException(nameof(config));
        }

        public FlowSection Section { get; private set; } = FlowSection.Title;
        public bool IsRunning => Section != FlowSection.Title;
        public double RunTime => clock.Elapsed;
        public double SectionTime => clock.Elapsed - sectionStart;
        public bool Paused { get; private set; }
        public bool TutorialTimedOut { get; private set; }
        public bool ForcedTankTriggered { get; private set; }
        public bool ForcedEndingTriggered { get; private set; }
        public bool EndingStarted { get; private set; }

        // 8:15 이후 · 아군 지원 · 결말 중에는 피해 · 사망 · 재시도를 받지 않는다.
        public bool CombatLocked => ForcedEndingTriggered || Section >= FlowSection.AllySupport;

        public event Action<FlowTransition> SectionChanged;
        public event Action EnemiesRetreatRequested;
        public event Action PauseReleased;

        public bool StartRun()
        {
            if (IsRunning) return false;
            clock.Reset();
            Paused = false;
            TutorialTimedOut = false;
            ForcedTankTriggered = false;
            ForcedEndingTriggered = false;
            EndingStarted = false;
            Enter(FlowSection.Tutorial, TransitionReason.RunStart);
            return true;
        }

        public void AbortToTitle()
        {
            if (!IsRunning) return;
            Paused = false;
            Enter(FlowSection.Title, TransitionReason.Abort);
        }

        // 일시정지는 표시만 바꾼다. 회차 시계는 계속 흐른다 (GDD 14번 9).
        public void SetPaused(bool paused)
        {
            if (IsRunning) Paused = paused;
        }

        public void Tick(double unscaledDelta)
        {
            if (!IsRunning || unscaledDelta <= 0) return;
            AdvanceTo(clock.Elapsed + unscaledDelta);
        }

        // 현재 구간의 완료 조건이 충족됐다 (S1 행동 완료 · S2 · S3 처치 · S4 보충과 연출 끝 · S5 전차 정지).
        // 다른 구간 이름으로 온 늦은 보고는 무시한다.
        public void ReportGoalMet(FlowSection section)
        {
            if (!IsRunning || section != Section) return;
            switch (Section)
            {
                case FlowSection.Tutorial: Enter(FlowSection.Scout, TransitionReason.GoalMet); break;
                case FlowSection.Scout: Enter(FlowSection.Assault, TransitionReason.GoalMet); break;
                case FlowSection.Assault: Enter(FlowSection.Silence, TransitionReason.GoalMet); break;
                case FlowSection.Silence:
                    if (silenceGoalAt < 0) silenceGoalAt = clock.Elapsed;
                    AdvanceTo(clock.Elapsed);
                    break;
                case FlowSection.Tank: Enter(FlowSection.Dawn, TransitionReason.GoalMet); break;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 디버그 창(TASKS 1-1b) · 테스트 전용. 구간 경과 시간은 그대로 두고 회차 시계만 옮긴다.
        public void DebugSetRunTime(double runTime)
        {
            if (!IsRunning) return;
            double delta = Math.Max(0, runTime) - clock.Elapsed;
            clock.DebugSet(runTime);
            sectionStart += delta;
            if (silenceGoalAt >= 0) silenceGoalAt += delta;
        }
#endif

        void AdvanceTo(double target)
        {
            while (IsRunning)
            {
                Boundary next = NextBoundary(out double at);
                if (next == Boundary.None || at > target) break;
                clock.AdvanceTo(at);
                Apply(next);
            }
            if (IsRunning) clock.AdvanceTo(target);
        }

        Boundary NextBoundary(out double at)
        {
            Boundary best = Boundary.None;
            at = double.PositiveInfinity;

            if (!ForcedEndingTriggered && !EndingStarted && config.forceEndingAt < at)
            {
                best = Boundary.ForcedEnding;
                at = config.forceEndingAt;
            }
            if (!ForcedTankTriggered && Section <= FlowSection.Silence && config.forceTankAt < at)
            {
                best = Boundary.ForcedTank;
                at = config.forceTankAt;
            }
            double end = SectionEndTime();
            if (end < at)
            {
                best = Boundary.SectionEnd;
                at = end;
            }
            if (Section == FlowSection.Silence && ForcedTankTriggered && config.tankEntryDeadline < at)
            {
                best = Boundary.TankDeadline;
                at = config.tankEntryDeadline;
            }
            if (config.hardCap < at)
            {
                best = Boundary.HardCap;
                at = config.hardCap;
            }
            return best;
        }

        double SectionEndTime()
        {
            switch (Section)
            {
                case FlowSection.Tutorial: return sectionStart + config.s1Cap;
                case FlowSection.Scout: return sectionStart + config.s2Cap;
                case FlowSection.Assault: return sectionStart + config.s3Cap;
                case FlowSection.Silence:
                    if (forcedSilence) return sectionStart + config.forcedSilence;
                    double end = sectionStart + config.s4Max;
                    if (silenceGoalAt >= 0) end = Math.Min(end, Math.Max(silenceGoalAt, sectionStart + config.s4Min));
                    return end;
                case FlowSection.Tank: return sectionStart + config.s5Cap;
                case FlowSection.AllySupport: return sectionStart + config.allySupportDuration;
                case FlowSection.Dawn: return sectionStart + config.dawnDuration;
                case FlowSection.Result: return sectionStart + config.resultDuration;
                default: return double.PositiveInfinity;
            }
        }

        void Apply(Boundary boundary)
        {
            switch (boundary)
            {
                case Boundary.ForcedEnding:
                    ForcedEndingTriggered = true;
                    ReleasePause();
                    if (Section < FlowSection.AllySupport)
                    {
                        RequestRetreatIfCombat();
                        Enter(FlowSection.AllySupport, TransitionReason.ForcedEnding);
                    }
                    break;
                case Boundary.ForcedTank:
                    ForcedTankTriggered = true;
                    ReleasePause();
                    if (Section != FlowSection.Silence)
                    {
                        RequestRetreatIfCombat();
                        Enter(FlowSection.Silence, TransitionReason.ForcedTank);
                    }
                    break;
                case Boundary.SectionEnd:
                    EndSection();
                    break;
                case Boundary.TankDeadline:
                    Enter(FlowSection.Tank, TransitionReason.TankDeadline);
                    break;
                case Boundary.HardCap:
                    Paused = false;
                    Enter(FlowSection.Title, TransitionReason.HardCap);
                    break;
            }
        }

        void EndSection()
        {
            switch (Section)
            {
                case FlowSection.Tutorial:
                    TutorialTimedOut = true;
                    Enter(FlowSection.Scout, TransitionReason.SectionCap);
                    break;
                case FlowSection.Scout:
                    // 생존 정찰병은 주 공세에 합류한다 (씬 처리)
                    Enter(FlowSection.Assault, TransitionReason.SectionCap);
                    break;
                case FlowSection.Assault:
                    EnemiesRetreatRequested?.Invoke();
                    Enter(FlowSection.Silence, TransitionReason.SectionCap);
                    break;
                case FlowSection.Silence:
                    var reason = forcedSilence ? TransitionReason.Timer
                        : silenceGoalAt >= 0 ? TransitionReason.GoalMet
                        : TransitionReason.SectionCap;
                    Enter(FlowSection.Tank, reason);
                    break;
                case FlowSection.Tank:
                    Enter(FlowSection.AllySupport, TransitionReason.SectionCap);
                    break;
                case FlowSection.AllySupport:
                    Enter(FlowSection.Dawn, TransitionReason.Timer);
                    break;
                case FlowSection.Dawn:
                    Enter(FlowSection.Result, TransitionReason.Timer);
                    break;
                case FlowSection.Result:
                    Enter(FlowSection.Title, TransitionReason.Timer);
                    break;
            }
        }

        void Enter(FlowSection to, TransitionReason reason)
        {
            var from = Section;
            Section = to;
            sectionStart = clock.Elapsed;
            if (to == FlowSection.Silence)
            {
                silenceGoalAt = -1;
                forcedSilence = reason == TransitionReason.ForcedTank;
            }
            if (to == FlowSection.Dawn) EndingStarted = true;
            SectionChanged?.Invoke(new FlowTransition(from, to, reason, clock.Elapsed));
        }

        void RequestRetreatIfCombat()
        {
            if (Section == FlowSection.Scout || Section == FlowSection.Assault)
                EnemiesRetreatRequested?.Invoke();
        }

        void ReleasePause()
        {
            if (!Paused) return;
            Paused = false;
            PauseReleased?.Invoke();
        }
    }
}
