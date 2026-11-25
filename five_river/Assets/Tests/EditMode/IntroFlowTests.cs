using FiveRivers.Data;
using FiveRivers.Narrative;
using FiveRivers.Tutorial;
using FiveRivers.UI;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 조작 안내 가시성 (2026-11-26 피드백): 진행 표시와 해낸 행동 수.
    public class TutorialVisibilityTests
    {
        [Test]
        public void Progress_ShowsDoneDotsAndCurrentStepNumber()
        {
            string p = TutorialPromptView.Progress(2, 5);
            StringAssert.Contains("●●</color>○○○", p);
            StringAssert.EndsWith("3 / 5", p);
            StringAssert.EndsWith("1 / 5", TutorialPromptView.Progress(0, 5));
            StringAssert.EndsWith("5 / 5", TutorialPromptView.Progress(5, 5), "다 끝나도 6 / 5가 되지 않는다");
        }

        [Test]
        public void Tracker_CountsDoneSteps_IncludingOnesDoneAhead()
        {
            var t = new TutorialTracker(15f, 30f);
            Assert.AreEqual(5, t.StepCount);
            Assert.AreEqual(0, t.DoneCount);
            t.Report(TutorialStep.Stance); // 차례보다 먼저
            Assert.AreEqual(1, t.DoneCount);
            Assert.AreEqual(TutorialStep.Look, t.Current);
            t.Report(TutorialStep.Look);
            t.Report(TutorialStep.Move);
            Assert.AreEqual(3, t.DoneCount);
            Assert.AreEqual(TutorialStep.ShootTarget, t.Current, "먼저 해낸 자세 단계는 건너뛴다");
        }
    }

    // 시작 컷씬(검은 화면 날짜)과 참호 방어로 잇는 안내 (2026-11-26 피드백).
    public class IntroFlowTests
    {
        [Test]
        public void Timeline_CoversTitle_ShowsLinesInOrder_ThenRevealsTrench()
        {
            var t = new IntroTimeline();
            Assert.AreEqual(0f, t.BlackAlpha, 1e-4f);
            t.Advance(t.FadeIn);
            Assert.AreEqual(1f, t.BlackAlpha, 1e-4f, "타이틀이 다 덮인 뒤에");
            Assert.AreEqual(0f, t.LineAlpha(0), 1e-4f, "첫 줄이 나오기 시작한다");

            t.Advance(t.LineStep + t.LineFade * 0.5f);
            Assert.Greater(t.LineAlpha(0), t.LineAlpha(1), "날짜가 장소보다 먼저");
            Assert.AreEqual(0f, t.LineAlpha(2), 1e-4f);

            t.Advance(t.TextEnd - t.Time);
            for (int i = 0; i < t.Lines; i++) Assert.AreEqual(1f, t.LineAlpha(i), 1e-4f, "머무는 동안 세 줄 모두 보인다");
            Assert.IsFalse(t.Revealing, "글자가 남아 있는 동안은 회차를 시작하지 않는다");

            t.Advance(t.TextOut + 0.001f); // 경계값을 그대로 더하면 부동소수점 오차로 모자랄 수 있다
            Assert.IsTrue(t.Revealing);
            Assert.AreEqual(1f, t.BlackAlpha, 0.01f, "회차는 검은 화면 아래에서 시작한다");
            for (int i = 0; i < t.Lines; i++) Assert.AreEqual(0f, t.LineAlpha(i), 1e-4f);

            t.Advance(100f);
            Assert.IsTrue(t.Finished);
            Assert.AreEqual(0f, t.BlackAlpha, 1e-4f, "끝나면 참호가 다 보인다");
        }

        [Test]
        public void Timeline_StartKeyIsNotReadAsSkip_LaterSkipJumpsToTextOut()
        {
            var t = new IntroTimeline();
            Assert.IsFalse(t.Skip(), "시작 키를 누른 바로 그 순간은 건너뛰지 않는다");
            t.Advance(t.SkipAfter + 0.1f);
            Assert.IsTrue(t.Skip());
            Assert.AreEqual(t.TextEnd, t.Time, 1e-4f);
            Assert.IsFalse(t.Skip(), "이미 글자가 사라지는 중이면 그대로");
            Assert.IsFalse(t.Revealing);
        }

        [Test]
        public void Timeline_TotalLengthStaysShort()
        {
            var t = new IntroTimeline();
            Assert.That(t.End, Is.InRange(6f, 11f), "건너뛰지 않아도 10초 안팎");
        }

        [TestCase(0f, false, TrenchTurn.None)]
        [TestCase(50f, false, TrenchTurn.None)]
        [TestCase(70f, false, TrenchTurn.Right)]
        [TestCase(-70f, false, TrenchTurn.Left)]
        [TestCase(150f, false, TrenchTurn.Back)]
        [TestCase(-170f, false, TrenchTurn.Back)]
        [TestCase(50f, true, TrenchTurn.Right)]   // 떠 있는 동안은 40°까지 돌아와야 사라진다
        [TestCase(30f, true, TrenchTurn.None)]
        public void Guide_NamesTheTurnTowardTheValley(float angle, bool showing, TrenchTurn expected)
        {
            Assert.AreEqual(expected, TrenchGuide.Turn(angle, showing));
        }

        [Test]
        public void Text_IntroAndGuideLinesAreFilled()
        {
            var text = ScriptableObject.CreateInstance<GameText>();
            try
            {
                StringAssert.Contains("1950년 8월", text.introDate);
                Assert.IsNotEmpty(text.introPlace);
                Assert.IsNotEmpty(text.introContext);
                foreach (var line in new[] { text.introLeaderWake, text.introLeaderPractice, text.holdTrenchLine })
                {
                    Assert.IsNotEmpty(line.speaker);
                    Assert.IsNotEmpty(line.text);
                    Assert.Greater(line.duration, 1f);
                }
                Assert.IsNotEmpty(text.objectiveHoldTrench);
                Assert.IsNotEmpty(text.tutorialAcks, "조작을 해냈을 때의 분대장 대답");
                foreach (var ack in text.tutorialAcks)
                {
                    Assert.IsNotEmpty(ack.text);
                    Assert.That(ack.duration, Is.InRange(1f, 2.5f), "짧게");
                }
                Assert.IsNotEmpty(text.guideTurnLeft);
                Assert.IsNotEmpty(text.guideTurnRight);
                Assert.IsNotEmpty(text.guideTurnBack);
            }
            finally
            {
                Object.DestroyImmediate(text);
            }
        }

        [Test]
        public void TutorialConfig_BridgeTimesAreSet()
        {
            var config = ScriptableObject.CreateInstance<TutorialConfig>();
            try
            {
                Assert.That(config.promptDelay, Is.InRange(2f, 6f), "첫 대사(3.5초)를 들을 만큼");
                Assert.That(config.holdBridge, Is.InRange(3f, 8f), "\"제자리 지켜라\"(3.5초)를 들을 만큼");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
