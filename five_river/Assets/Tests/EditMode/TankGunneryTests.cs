using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 전차포 조준해 들어옴 규칙 (결정 2026-11-24)
    public sealed class TankGunneryTests
    {
        static float Mid() => 0.5f;
        static TankGunnery New() => new TankGunnery(GunneryRules.Default);

        [Test]
        public void ExposedThirdShotIsDirect()
        {
            var g = New();
            var a = g.Next(true, 0f, Mid);
            var b = g.Next(true, 0f, Mid);
            var c = g.Next(true, 0f, Mid);
            Assert.AreEqual(GunneryKind.Far, a.Kind);
            Assert.GreaterOrEqual(a.Distance, 9f); Assert.LessOrEqual(a.Distance, 13f);
            Assert.AreEqual(GunneryKind.Mid, b.Kind);
            Assert.GreaterOrEqual(b.Distance, 3f); Assert.LessOrEqual(b.Distance, 5f);
            Assert.IsTrue(c.Direct);
            Assert.LessOrEqual(c.Distance, 1f);
        }

        [Test]
        public void StayingExposedStaysDirect()
        {
            var g = New();
            for (int i = 0; i < 3; i++) g.Next(true, 0f, Mid);
            Assert.IsTrue(g.Next(true, 0f, Mid).Direct);
            Assert.IsTrue(g.Next(true, 0f, Mid).Direct);
        }

        [Test]
        public void HidingGoesBackToTheStartAndLandsFar()
        {
            var g = New();
            g.Next(true, 0f, Mid);
            g.Next(true, 0f, Mid);
            var hidden = g.Next(false, 0f, Mid);
            Assert.AreEqual(GunneryKind.Far, hidden.Kind);
            Assert.AreEqual(0, g.Stage);
            Assert.AreEqual(GunneryKind.Far, g.Next(true, 0f, Mid).Kind); // 다시 노출돼도 처음부터
        }

        [Test]
        public void PeekShootHideNeverDies()
        {
            var g = New();
            for (int i = 0; i < 10; i++)
            {
                var exposedShot = g.Next(true, 0f, Mid);
                Assert.IsFalse(exposedShot.Direct);
                Assert.IsFalse(g.Next(false, 0f, Mid).Direct);
            }
        }

        [Test]
        public void MovingFarStepsOneBack()
        {
            var g = New();
            g.Next(true, 0f, Mid);
            g.Next(true, 0f, Mid);              // 다음은 직격 단계
            var moved = g.Next(true, 4f, Mid);  // 4 m 옮김: 한 단계 뒤 = 가까이
            Assert.AreEqual(GunneryKind.Mid, moved.Kind);
            Assert.IsTrue(g.Next(true, 0f, Mid).Direct);
        }

        [Test]
        public void MovingLittleDoesNotStepBack()
        {
            var g = New();
            g.Next(true, 0f, Mid);
            g.Next(true, 0f, Mid);
            Assert.IsTrue(g.Next(true, 3.9f, Mid).Direct);
        }

        [Test]
        public void MovingBeforeTheFirstShotChangesNothing()
        {
            var g = New();
            Assert.AreEqual(GunneryKind.Far, g.Next(true, 20f, Mid).Kind);
            Assert.AreEqual(1, g.Stage);
        }

        [Test]
        public void MissesAreNeverDirectAndStayOutsideDirectRadius()
        {
            var rng = new System.Random(7);
            for (int run = 0; run < 200; run++)
            {
                var g = New();
                for (int i = 0; i < 2; i++)
                {
                    var s = g.Next(true, 0f, () => (float)rng.NextDouble());
                    Assert.IsFalse(s.Direct);
                    Assert.Greater(s.Distance, 1f);   // 직격 반경 1 m 밖: 체력 피해 없음
                }
            }
        }

        [Test]
        public void ResetStartsOver()
        {
            var g = New();
            g.Next(true, 0f, Mid);
            g.Next(true, 0f, Mid);
            g.Reset();
            Assert.AreEqual(GunneryKind.Far, g.Next(true, 0f, Mid).Kind);
        }

        [Test]
        public void ShotsToDirectIsConfigurable()
        {
            var r = GunneryRules.Default; r.ShotsToDirect = 2;
            var g = new TankGunnery(r);
            Assert.AreEqual(GunneryKind.Far, g.Next(true, 0f, Mid).Kind);
            Assert.IsTrue(g.Next(true, 0f, Mid).Direct);
        }
    }
}
