using System;
using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 크게 맞은 뒤 몸에 남는 것 (결정 2026-11-24)
    public sealed class WoundAftermathTests
    {
        static WoundAftermath New() => new WoundAftermath(WoundRules.Default);

        [Test]
        public void SmallHitLeavesNothing()
        {
            var w = New();
            var hit = w.Hit(30f, 100f);
            Assert.IsFalse(hit.Big);
            Assert.IsFalse(w.Active);
            Assert.AreEqual(0f, w.Level);
            var t = w.Tremor(1f, false);
            Assert.AreEqual(0f, t.yaw);
            Assert.AreEqual(0f, t.pitch);
        }

        [Test]
        public void BigHitStartsEffectForTenToTwentySeconds()
        {
            var w = New();
            var hit = w.Hit(50f, 100f);           // 일반판 적탄 한 발(체력 100의 절반)
            Assert.IsTrue(hit.Big);
            Assert.IsFalse(hit.Severe);
            Assert.IsTrue(w.Active);
            Assert.GreaterOrEqual(w.Remaining, 10f);
            Assert.LessOrEqual(w.Remaining, 20f);
            Assert.Greater(w.Level, 0.4f);
        }

        [Test]
        public void BiggerHitIsStrongerAndLongerAndSevereAddsEarRinging()
        {
            var a = New(); a.Hit(45f, 100f);
            var b = New(); var hb = b.Hit(90f, 100f);
            Assert.IsTrue(hb.Severe);
            Assert.Greater(b.Remaining, a.Remaining);
            Assert.Greater(b.Level, a.Level);
            Assert.LessOrEqual(b.Remaining, 20f);
        }

        [Test]
        public void StackingGrowsButIsCapped()
        {
            var w = New();
            float last = 0f;
            for (int i = 0; i < 8; i++)
            {
                w.Hit(50f, 100f);
                Assert.GreaterOrEqual(w.Level, last - 1e-6f);
                last = w.Level;
                Assert.LessOrEqual(w.Level, 1f);
                Assert.LessOrEqual(w.Remaining, 20f);
            }
            Assert.AreEqual(1f, w.Level, 1e-5f);
        }

        [Test]
        public void SecondHitAddsStrength()
        {
            var w = New();
            w.Hit(45f, 100f);
            float one = w.Level;
            w.Hit(45f, 100f);
            Assert.Greater(w.Level, one);
        }

        [Test]
        public void FadesOutAndEndsAtZero()
        {
            var w = New();
            w.Hit(50f, 100f);
            float total = w.Remaining;
            w.Tick(total - 8f);                       // 아직 풀림 구간 앞: 세기 그대로
            float full = w.Level;
            w.Tick(6f);                               // 남은 2초: 풀리는 중
            Assert.Less(w.Level, full);
            Assert.Greater(w.Level, 0f);
            w.Tick(3f);
            Assert.IsFalse(w.Active);
            Assert.AreEqual(0f, w.Level);
        }

        [Test]
        public void ResetClearsAtOnce()
        {
            var w = New();
            w.Hit(90f, 100f);
            w.Hit(60f, 100f);
            w.Reset();
            Assert.IsFalse(w.Active);
            Assert.AreEqual(0f, w.Level);
            Assert.AreEqual(0f, w.Remaining);
            var t = w.Tremor(3f, false);
            Assert.AreEqual(0f, t.yaw);
            Assert.AreEqual(0f, t.pitch);
        }

        [Test]
        public void TremorStaysWithinAmplitudeAndAimedIsWeaker()
        {
            var w = New();
            w.Hit(100f, 100f);                         // 세기 1
            float maxHip = 0f, maxAim = 0f;
            for (int i = 0; i < 4000; i++)
            {
                float t = i * 0.01f;
                var h = w.Tremor(t, false);
                var a = w.Tremor(t, true);
                maxHip = Math.Max(maxHip, Math.Max(Math.Abs(h.yaw), Math.Abs(h.pitch)));
                maxAim = Math.Max(maxAim, Math.Max(Math.Abs(a.yaw), Math.Abs(a.pitch)));
            }
            Assert.LessOrEqual(maxHip, 0.35f + 1e-4f);
            Assert.LessOrEqual(maxAim, 0.10f + 1e-4f);
            Assert.Greater(maxHip, 0.15f);             // 실제로 떨린다
            Assert.Greater(maxHip, maxAim * 2f);
            // 조준 중 흔들림은 30m 앞에서 몇 cm 수준: 표적 크기(약 0.5m)의 일부
            float metres = 30f * (float)Math.Tan(maxAim * Math.PI / 180.0);
            Assert.Less(metres, 0.1f);
        }

        [Test]
        public void ZeroOrInvalidInputIsIgnored()
        {
            var w = New();
            Assert.IsFalse(w.Hit(0f, 100f).Big);
            Assert.IsFalse(w.Hit(50f, 0f).Big);
            Assert.IsFalse(w.Active);
        }

        [Test]
        public void BreathLevelFollowsTheEffectAndIsClamped()
        {
            var w = New();
            Assert.AreEqual(0f, w.BreathLevel(0.8f));
            w.Hit(100f, 100f);
            Assert.AreEqual(0.8f, w.BreathLevel(0.8f), 1e-5f);
            Assert.AreEqual(1f, w.BreathLevel(5f), 1e-5f);
        }
    }
}
