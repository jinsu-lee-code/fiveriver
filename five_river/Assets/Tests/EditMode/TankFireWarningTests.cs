using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 전차포 예고 (결정 2026-11-24): 쏘기 lead 초 전에 한 번, 등장 연출 · 어두운 화면이면 건너뜀, 조정은 쏘기 전에 0으로 돌아옴
    public sealed class TankFireWarningTests
    {
        static TankFireWarning New(float lead = 0.8f) => new TankFireWarning(lead, 0.15f, 0.45f, 1.2f, 0.6f);

        [Test]
        public void WarnsOnceLeadSecondsBeforeTheShot()
        {
            var w = New();
            Assert.IsFalse(w.Tick(0f, 9f, true));
            Assert.IsFalse(w.Tick(8.1f, 9f, true));
            Assert.IsTrue(w.Tick(8.25f, 9f, true));    // 9 - 0.8 = 8.2 이후 첫 프레임
            Assert.IsTrue(w.Active);
            Assert.IsFalse(w.Tick(8.3f, 9f, true));    // 한 번뿐
            Assert.IsFalse(w.Tick(8.9f, 9f, true));
            Assert.IsTrue(w.Active);
        }

        [Test]
        public void FirstShotAlsoWarnsAndTheNextShotWarnsAgain()
        {
            var w = New();
            Assert.IsTrue(w.Tick(8.4f, 9f, true));
            w.Fired();
            Assert.IsFalse(w.Active);
            Assert.IsFalse(w.Tick(9.0f, 18f, true));
            Assert.IsTrue(w.Tick(17.3f, 18f, true));
        }

        [Test]
        public void WarningEndsWhenTheShotTimePasses()
        {
            var w = New();
            w.Tick(8.3f, 9f, true);
            Assert.IsTrue(w.Active);
            w.Tick(9.0f, 9f, true);      // 시각이 지나면 켜진 채로 두지 않는다
            Assert.IsFalse(w.Active);
        }

        [Test]
        public void NoWarningWhenItCannotWarn()
        {
            var w = New();
            // 어두운 재시도 화면 · 등장 연출 · 전투 잠금: 알릴 수 없는 동안 이 발은 건너뛴다
            Assert.IsFalse(w.Tick(8.3f, 9f, false));
            Assert.IsFalse(w.Active);
            // 어두움이 걷혀도 남은 시간이 짧으면(0.4초) 늦은 예고는 하지 않는다
            Assert.IsFalse(w.Tick(8.6f, 9f, true));
            Assert.IsFalse(w.Active);
        }

        [Test]
        public void LateWarningNeedsEnoughTimeToReact()
        {
            // 남은 시간이 lead 의 60%(0.48초) 미만이면 늦은 예고는 하지 않는다
            var tooLate = New();
            Assert.IsFalse(tooLate.Tick(8.6f, 9f, true));
            Assert.IsFalse(tooLate.Active);
            var enough = New();
            Assert.IsTrue(enough.Tick(8.45f, 9f, true));   // 남은 0.55초
        }

        [Test]
        public void ScriptedOrDarkCancelsAnActiveWarning()
        {
            var w = New();
            Assert.IsTrue(w.Tick(8.3f, 9f, true));
            w.Tick(8.4f, 9f, false);
            Assert.IsFalse(w.Active);
            Assert.AreEqual(0f, w.NudgeYaw(8.5f));
        }

        [Test]
        public void CancelAllowsTheSameShotTimeToWarnAgain()
        {
            var w = New();
            Assert.IsTrue(w.Tick(8.3f, 9f, true));
            w.Cancel();                                 // 전차가 다시 나타남 (nextFire 가 새로 정해짐)
            Assert.IsTrue(w.Tick(8.3f, 9f, true));
        }

        [Test]
        public void NudgeIsOneSmoothBumpThatReturnsToZeroBeforeTheShot()
        {
            var w = New();
            w.Tick(8.21f, 9f, true);
            Assert.AreEqual(0f, w.NudgeYaw(8.21f), 1e-5f);              // 예고 직후엔 아직
            Assert.AreEqual(0f, w.NudgeYaw(8.21f + 0.10f), 1e-5f);      // 지연 중
            Assert.AreEqual(1.2f, w.NudgeYaw(8.21f + 0.15f + 0.225f), 1e-3f);   // 가운데가 가장 크다
            Assert.AreEqual(0.6f, w.NudgePitch(8.21f + 0.15f + 0.225f), 1e-3f);
            Assert.AreEqual(0f, w.NudgeYaw(8.21f + 0.65f), 1e-5f);      // 끝난 뒤
            Assert.AreEqual(0f, w.NudgeYaw(8.99f), 1e-5f);             // 쏘는 순간 판정에 닿지 않는다
        }

        [Test]
        public void ZeroLeadDisablesTheWarning()
        {
            var w = New(0f);
            Assert.IsFalse(w.Tick(8.9f, 9f, true));
            Assert.IsFalse(w.Active);
        }
    }
}
