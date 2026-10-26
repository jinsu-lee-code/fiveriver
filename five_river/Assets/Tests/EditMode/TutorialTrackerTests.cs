using System.Linq;
using FiveRivers.Tutorial;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // BALANCE 4번: 정체 15초 강조 · 30초 힌트
    public class TutorialTrackerTests
    {
        [Test]
        public void StepsInOrder_Complete()
        {
            var t = new TutorialTracker(15, 30);
            int completed = 0;
            t.Completed += () => completed++;
            foreach (var s in new[] { TutorialStep.Look, TutorialStep.Move, TutorialStep.Stance, TutorialStep.ShootTarget, TutorialStep.Reload })
            {
                Assert.AreEqual(s, t.Current);
                t.Report(s);
            }
            Assert.IsTrue(t.Complete);
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void EarlyAction_SkipsWhenItsTurnComes()
        {
            var t = new TutorialTracker(15, 30);
            t.Report(TutorialStep.Stance); // 순서보다 먼저
            Assert.AreEqual(TutorialStep.Look, t.Current);
            t.Report(TutorialStep.Look);
            t.Report(TutorialStep.Move);
            Assert.AreEqual(TutorialStep.ShootTarget, t.Current);
        }

        [Test]
        public void IdleTime_HighlightThenHint_ResetOnStep()
        {
            var t = new TutorialTracker(15, 30);
            t.Tick(14.9f);
            Assert.IsFalse(t.Highlight);
            t.Tick(0.2f);
            Assert.IsTrue(t.Highlight);
            Assert.IsFalse(t.ShowHint);
            t.Tick(15f);
            Assert.IsTrue(t.ShowHint);
            t.Report(TutorialStep.Look);
            Assert.IsFalse(t.Highlight);
            Assert.AreEqual(0f, t.StepTime, 1e-6f);
        }

        [Test]
        public void Remaining_ListsUndoneSteps()
        {
            var t = new TutorialTracker(15, 30);
            t.Report(TutorialStep.Look);
            t.Report(TutorialStep.Stance);
            CollectionAssert.AreEqual(new[] { TutorialStep.Move, TutorialStep.ShootTarget, TutorialStep.Reload }, t.Remaining.ToArray());
        }

        [Test]
        public void DuplicateReports_AreIgnored()
        {
            var t = new TutorialTracker(15, 30);
            int changes = 0;
            t.StepChanged += _ => changes++;
            t.Report(TutorialStep.Look);
            t.Report(TutorialStep.Look);
            Assert.AreEqual(1, changes);
        }
    }
}
