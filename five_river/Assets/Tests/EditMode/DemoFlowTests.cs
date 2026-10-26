using System;
using System.Collections.Generic;
using System.Linq;
using FiveRivers.Core;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEditor;

namespace FiveRivers.Tests
{
    // BALANCE.md 1-2 검산 케이스(T1~T7)와 FLOW.md 3-3 불변 조건. 값은 실제 FlowConfig.asset을 쓴다.
    public class DemoFlowTests
    {
        const string FlowConfigPath = "Assets/_Project/Data/FlowConfig.asset";
        const string ResultConfigPath = "Assets/_Project/Data/ResultConfig.asset";
        const string TankConfigPath = "Assets/_Project/Data/TankConfig.asset";
        const string AssistConfigPath = "Assets/_Project/Data/AssistConfig.asset";
        const double Step = 0.1;
        const double Eps = 1e-6;

        FlowConfig config;
        TankConfig tank;
        DemoFlow flow;
        List<FlowTransition> log;
        List<FlowCheckpoint> retries;
        int retreats;
        int pauseReleases;
        int assistActivations;

        [SetUp]
        public void SetUp()
        {
            config = AssetDatabase.LoadAssetAtPath<FlowConfig>(FlowConfigPath);
            tank = AssetDatabase.LoadAssetAtPath<TankConfig>(TankConfigPath);
            var assist = AssetDatabase.LoadAssetAtPath<AssistConfig>(AssistConfigPath);
            Assert.IsNotNull(config, FlowConfigPath);
            Assert.IsNotNull(tank, TankConfigPath);
            Assert.IsNotNull(assist, AssistConfigPath);
            flow = new DemoFlow(config, tank, assist);
            log = new List<FlowTransition>();
            retries = new List<FlowCheckpoint>();
            retreats = 0;
            pauseReleases = 0;
            assistActivations = 0;
            flow.SectionChanged += t => log.Add(t);
            flow.EnemiesRetreatRequested += () => retreats++;
            flow.PauseReleased += () => pauseReleases++;
            flow.RetryRequested += cp => retries.Add(cp);
            flow.Checkpoints.AssistActivated += () => assistActivations++;
        }

        void AdvanceTo(double time)
        {
            if (time > flow.RunTime) flow.Tick(time - flow.RunTime);
        }

        void RunUntil(double until)
        {
            while (flow.IsRunning && flow.RunTime < until - Eps)
                flow.Tick(Math.Min(Step, until - flow.RunTime));
        }

        void RunToEnd() => RunUntil(config.hardCap + 60);

        // S1 75 · S2 45 · S3 120초에 완료 보고 → 240초에 S4 진입
        void ReachSilenceAt240()
        {
            flow.StartRun();
            AdvanceTo(75);
            flow.ReportGoalMet(FlowSection.Tutorial);
            AdvanceTo(120);
            flow.ReportGoalMet(FlowSection.Scout);
            AdvanceTo(240);
            flow.ReportGoalMet(FlowSection.Assault);
        }

        // S4 완료를 245초에 보고 → 최소 20초를 채운 260초에 S5 진입
        void ReachTankAt260()
        {
            ReachSilenceAt240();
            AdvanceTo(245);
            flow.ReportGoalMet(FlowSection.Silence);
            AdvanceTo(260);
        }

        FlowTransition Entry(FlowSection to) => log.Single(t => t.To == to);
        int Count(FlowSection to) => log.Count(t => t.To == to);

        static void AssertTransition(FlowTransition t, TransitionReason reason, double time)
        {
            Assert.AreEqual(reason, t.Reason, t.ToString());
            Assert.AreEqual(time, t.Time, Eps, t.ToString());
        }

        [Test]
        public void T1_AllCapsReached_TankAttemptsEndBeforeEightFifteen()
        {
            flow.StartRun();
            RunToEnd();

            AssertTransition(Entry(FlowSection.Scout), TransitionReason.SectionCap, 105);
            AssertTransition(Entry(FlowSection.Assault), TransitionReason.SectionCap, 165);
            AssertTransition(Entry(FlowSection.Silence), TransitionReason.SectionCap, 315);
            AssertTransition(Entry(FlowSection.Tank), TransitionReason.SectionCap, 355);
            // 전차 1차 75초 + 2차 45초 = 120초 < S5 상한 140초 → 2회 실패로 아군 지원
            AssertTransition(Entry(FlowSection.AllySupport), TransitionReason.TankFailed, 475);
            AssertTransition(Entry(FlowSection.Dawn), TransitionReason.Timer, 480);
            AssertTransition(Entry(FlowSection.Result), TransitionReason.Timer, 500);
            AssertTransition(log.Last(), TransitionReason.Timer, 520);
            Assert.AreEqual(FlowSection.Title, flow.Section);
            Assert.IsTrue(flow.TutorialTimedOut);
            Assert.IsFalse(flow.ForcedTankTriggered);
            Assert.IsFalse(flow.ForcedEndingTriggered);
            Assert.AreEqual(1, retreats);
            CollectionAssert.AreEqual(new[] { FlowCheckpoint.Tank }, retries);
        }

        [Test]
        public void T1b_SectionCapAndEightFifteenTie_EightFifteenFirst()
        {
            // 전차 시도 제한이 없다고 보면 S5 140초 상한과 8:15가 495초에 겹친다 → 8:15가 먼저, 아군 지원 한 번
            var noAttemptLimit = UnityEngine.Object.Instantiate(tank);
            noAttemptLimit.attempt1 = noAttemptLimit.attempt2 = 1000;
            flow = new DemoFlow(config, noAttemptLimit, AssetDatabase.LoadAssetAtPath<AssistConfig>(AssistConfigPath));
            log.Clear();
            flow.SectionChanged += t => log.Add(t);
            flow.StartRun();
            RunToEnd();
            AssertTransition(Entry(FlowSection.AllySupport), TransitionReason.ForcedEnding, 495);
            AssertTransition(log.Last(), TransitionReason.Timer, 540);
            UnityEngine.Object.DestroyImmediate(noAttemptLimit);
        }

        [Test]
        public void DeathInScout_RetriesCheckpointAndKeepsRunTime()
        {
            flow.StartRun();
            AdvanceTo(75);
            flow.ReportGoalMet(FlowSection.Tutorial);
            AdvanceTo(100);
            flow.ReportPlayerDied();
            Assert.AreEqual(FlowSection.Scout, flow.Section);
            Assert.AreEqual(100, flow.RunTime, Eps);
            Assert.AreEqual(0, flow.SectionTime, Eps);
            CollectionAssert.AreEqual(new[] { FlowCheckpoint.FirstContact }, retries);
            Assert.AreEqual(1, flow.Checkpoints.Failures(FlowCheckpoint.FirstContact));
            Assert.IsFalse(flow.Checkpoints.AssistApplied);
        }

        [Test]
        public void TwoDeathsAtSameCheckpoint_ActivatesAssistOnce()
        {
            ReachSilenceAt240();
            flow.DebugJumpTo(FlowSection.Assault);
            flow.ReportPlayerDied();
            flow.ReportPlayerDied();
            flow.ReportPlayerDied();
            Assert.IsTrue(flow.Checkpoints.AssistApplied);
            Assert.AreEqual(1, assistActivations);
            Assert.AreEqual(3, flow.Checkpoints.Failures(FlowCheckpoint.MainAssault));
        }

        [Test]
        public void DeathsAtDifferentCheckpoints_NoAssist()
        {
            flow.DebugJumpTo(FlowSection.Scout);
            flow.ReportPlayerDied();
            flow.DebugJumpTo(FlowSection.Assault);
            flow.ReportPlayerDied();
            Assert.IsFalse(flow.Checkpoints.AssistApplied);
        }

        [Test]
        public void DeathWhileCombatLocked_IsIgnored()
        {
            ReachTankAt260();
            flow.DebugSetRunTime(494);
            RunUntil(496);
            Assert.IsTrue(flow.CombatLocked);
            flow.ReportPlayerDied();
            Assert.IsEmpty(retries);
            Assert.AreEqual(0, flow.Checkpoints.Failures(FlowCheckpoint.Tank));
        }

        [Test]
        public void TankAttemptsTimeOutTwice_AllySupport()
        {
            ReachTankAt260();
            RunUntil(400);
            CollectionAssert.AreEqual(new[] { FlowCheckpoint.Tank }, retries);
            AssertTransition(Entry(FlowSection.AllySupport), TransitionReason.TankFailed, 380); // 260 + 75 + 45
            Assert.AreEqual(2, flow.TankFailures);
            Assert.IsTrue(flow.Checkpoints.AssistApplied);
        }

        [Test]
        public void TankDeathThenTimeout_SecondAttemptIsShort()
        {
            ReachTankAt260();
            AdvanceTo(300);
            flow.ReportPlayerDied();
            Assert.AreEqual(FlowSection.Tank, flow.Section);
            Assert.AreEqual(tank.attempt2, flow.TankAttemptLimit, Eps);
            RunUntil(360);
            AssertTransition(Entry(FlowSection.AllySupport), TransitionReason.TankFailed, 345);
        }

        [Test]
        public void T2_BaselineFlow_ReturnsAtSevenMinutesWithoutForcedTransition()
        {
            ReachSilenceAt240();
            AdvanceTo(280);
            AdvanceTo(380);
            flow.ReportGoalMet(FlowSection.Tank);
            RunToEnd();

            // S4 기준 길이 40초 = 상한 40초라서 기준 흐름의 S4는 상한으로 끝난다
            AssertTransition(Entry(FlowSection.Tank), TransitionReason.SectionCap, 280);
            AssertTransition(Entry(FlowSection.Dawn), TransitionReason.GoalMet, 380);
            AssertTransition(log.Last(), TransitionReason.Timer, 420);
            Assert.AreEqual(0, Count(FlowSection.AllySupport));
            Assert.IsFalse(flow.ForcedTankTriggered);
            Assert.IsFalse(flow.ForcedEndingTriggered);
            Assert.IsFalse(flow.TutorialTimedOut);
        }

        [Test]
        public void T3_ForcedTankDuringAssault_EntersTankAt720()
        {
            flow.StartRun();
            AdvanceTo(75);
            flow.ReportGoalMet(FlowSection.Tutorial);
            AdvanceTo(120);
            flow.ReportGoalMet(FlowSection.Scout);
            flow.DebugSetRunTime(410);
            RunUntil(450);

            AssertTransition(Entry(FlowSection.Silence), TransitionReason.ForcedTank, 420);
            AssertTransition(Entry(FlowSection.Tank), TransitionReason.Timer, 440);
            Assert.LessOrEqual(Entry(FlowSection.Tank).Time, config.tankEntryDeadline);
            Assert.AreEqual(1, retreats);
        }

        [Test]
        public void T3b_ForcedTankDuringSilence_KeepsSilenceAndEntersTankBy725()
        {
            ReachSilenceAt240();
            flow.DebugSetRunTime(410); // 정적 시작이 410초로 옮겨짐 → 40초 상한이면 450초
            RunUntil(460);

            Assert.IsTrue(flow.ForcedTankTriggered);
            Assert.AreEqual(1, Count(FlowSection.Silence));
            AssertTransition(Entry(FlowSection.Tank), TransitionReason.TankDeadline, 445);
            Assert.AreEqual(0, retreats);
        }

        [Test]
        public void T4_ForcedEndingDuringTank_AllySupportThenReturnAtNineMinutes()
        {
            ReachTankAt260();
            flow.DebugSetRunTime(490);
            RunUntil(496);
            Assert.IsTrue(flow.CombatLocked);
            RunToEnd();

            AssertTransition(Entry(FlowSection.AllySupport), TransitionReason.ForcedEnding, 495);
            AssertTransition(Entry(FlowSection.Dawn), TransitionReason.Timer, 500);
            AssertTransition(Entry(FlowSection.Result), TransitionReason.Timer, 520);
            AssertTransition(log.Last(), TransitionReason.Timer, 540);
        }

        [Test]
        public void T5_ForcedEndingWhileDawn_ChangesNothing()
        {
            ReachTankAt260();
            AdvanceTo(270);
            flow.ReportGoalMet(FlowSection.Tank);
            flow.DebugSetRunTime(485); // 새벽 485 ~ 505
            RunToEnd();

            Assert.IsFalse(flow.ForcedEndingTriggered);
            Assert.AreEqual(0, Count(FlowSection.AllySupport));
            Assert.AreEqual(1, Count(FlowSection.Dawn));
            Assert.AreEqual(1, Count(FlowSection.Result));
            AssertTransition(log.Last(), TransitionReason.Timer, 525);
        }

        [Test]
        public void T6_PausedAt650_ClockKeepsRunningAndForcedTransitionsReleasePause()
        {
            flow.StartRun();
            AdvanceTo(75);
            flow.ReportGoalMet(FlowSection.Tutorial);
            AdvanceTo(120);
            flow.ReportGoalMet(FlowSection.Scout);
            flow.DebugSetRunTime(410);
            flow.SetPaused(true);
            RunUntil(430);
            Assert.IsFalse(flow.Paused);
            Assert.AreEqual(1, pauseReleases);

            flow.SetPaused(true);
            RunToEnd();
            Assert.AreEqual(2, pauseReleases);
            AssertTransition(Entry(FlowSection.AllySupport), TransitionReason.ForcedEnding, 495);
            AssertTransition(log.Last(), TransitionReason.Timer, 540);
        }

        [Test]
        public void T7_ResultSplitMatchesResultDuration()
        {
            var result = AssetDatabase.LoadAssetAtPath<ResultConfig>(ResultConfigPath);
            Assert.IsNotNull(result, ResultConfigPath);
            Assert.AreEqual(config.resultDuration, result.Total, 1e-4);
        }

        [Test]
        public void SilenceGoalBeforeMinimum_WaitsUntilMinimum()
        {
            ReachSilenceAt240();
            AdvanceTo(245);
            flow.ReportGoalMet(FlowSection.Silence);
            AdvanceTo(259.9);
            Assert.AreEqual(FlowSection.Silence, flow.Section);
            AdvanceTo(260.1);
            AssertTransition(Entry(FlowSection.Tank), TransitionReason.GoalMet, 260);
        }

        [Test]
        public void StaleGoalReport_IsIgnored()
        {
            flow.StartRun();
            flow.ReportGoalMet(FlowSection.Scout);
            Assert.AreEqual(FlowSection.Tutorial, flow.Section);
        }

        [Test]
        public void TankStoppedTwice_EndingStartsOnce()
        {
            ReachTankAt260();
            flow.ReportGoalMet(FlowSection.Tank);
            flow.ReportGoalMet(FlowSection.Tank);
            RunToEnd();
            Assert.AreEqual(1, Count(FlowSection.Dawn));
            Assert.AreEqual(1, Count(FlowSection.Result));
        }

        [Test]
        public void NewRun_StartsFromCleanState()
        {
            flow.StartRun();
            flow.SetPaused(true);
            RunToEnd();
            Assert.IsTrue(flow.Checkpoints.AssistApplied); // 전차 2회 실패

            Assert.IsTrue(flow.StartRun());
            Assert.IsFalse(flow.Checkpoints.AssistApplied);
            Assert.AreEqual(0, flow.TankFailures);
            Assert.AreEqual(FlowSection.Tutorial, flow.Section);
            Assert.AreEqual(0, flow.RunTime, Eps);
            Assert.IsFalse(flow.Paused);
            Assert.IsFalse(flow.TutorialTimedOut);
            Assert.IsFalse(flow.ForcedTankTriggered);
            Assert.IsFalse(flow.ForcedEndingTriggered);
            Assert.IsFalse(flow.EndingStarted);
            Assert.IsFalse(flow.CombatLocked);
        }

        [Test]
        public void AbortToTitle_EndsRunImmediately()
        {
            flow.StartRun();
            AdvanceTo(30);
            flow.AbortToTitle();
            Assert.AreEqual(FlowSection.Title, flow.Section);
            AssertTransition(log.Last(), TransitionReason.Abort, 30);
        }

        [Test]
        public void DebugJumpToTank_MovesClockToBaselineStart()
        {
            flow.DebugJumpTo(FlowSection.Tank);
            Assert.AreEqual(FlowSection.Tank, flow.Section);
            Assert.AreEqual(280, flow.RunTime, Eps);
            AssertTransition(log.Last(), TransitionReason.DebugJump, 280);
            flow.ReportGoalMet(FlowSection.Tank); // 280초에 바로 정지 → 새벽 20 + 결과 20
            RunToEnd();
            AssertTransition(log.Last(), TransitionReason.Timer, 320);
        }

        [Test]
        public void DebugJumpBackwards_ClearsEndingState()
        {
            flow.StartRun();
            flow.DebugJumpTo(FlowSection.Dawn);
            Assert.IsTrue(flow.EndingStarted);
            Assert.AreEqual(380, flow.RunTime, Eps);
            flow.DebugJumpTo(FlowSection.Scout);
            Assert.IsFalse(flow.EndingStarted);
            Assert.IsFalse(flow.CombatLocked);
            Assert.AreEqual(75, flow.RunTime, Eps);
            RunToEnd();
            Assert.AreEqual(2, Count(FlowSection.Dawn)); // 바로가기 1 + 실제 진행 1
        }

        // 무작위 플레이(보고 시각 · 미보고 · 일시정지 · 프레임 길이) 300회에서 불변 조건이 지켜지는지
        [Test]
        public void RandomRuns_KeepInvariants()
        {
            var rng = new Random(20261027);
            var goalAt = new Dictionary<FlowSection, double>();
            bool pausedAfterForced = false;
            flow.SectionChanged += t =>
            {
                if (t.To != FlowSection.Title)
                    goalAt[t.To] = rng.NextDouble() < 0.2 ? double.PositiveInfinity : rng.NextDouble() * 170;
                if (t.Reason == TransitionReason.ForcedTank || t.Reason == TransitionReason.ForcedEnding)
                    pausedAfterForced |= flow.Paused;
            };

            for (int run = 0; run < 300; run++)
            {
                log.Clear();
                flow.StartRun();
                int guard = 0;
                while (flow.IsRunning && guard++ < 100000)
                {
                    flow.Tick(0.016 + rng.NextDouble() * 0.5);
                    if (rng.NextDouble() < 0.01) flow.SetPaused(!flow.Paused);
                    if (rng.NextDouble() < 0.003) flow.ReportPlayerDied();
                    if (flow.IsRunning && flow.SectionTime >= goalAt[flow.Section]) flow.ReportGoalMet(flow.Section);
                }

                string where = "run " + run + ": " + string.Join(", ", log);
                Assert.AreEqual(FlowSection.Title, flow.Section, where);
                Assert.LessOrEqual(log.Last().Time, config.hardCap + Eps, where);
                Assert.AreNotEqual(TransitionReason.HardCap, log.Last().Reason, where);
                Assert.AreEqual(1, Count(FlowSection.Dawn), where);
                Assert.AreEqual(1, Count(FlowSection.Result), where);
                if (flow.ForcedTankTriggered && log.Any(t => t.To == FlowSection.Tank))
                    Assert.LessOrEqual(Entry(FlowSection.Tank).Time, config.tankEntryDeadline + Eps, where);
                Assert.IsFalse(pausedAfterForced, where);
            }
        }
    }
}
