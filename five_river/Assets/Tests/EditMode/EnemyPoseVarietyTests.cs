using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 적 보병의 박자 · 엄폐 대기 자세 (2026-11-27, 검수: 같은 박자 · 같은 자세, 엄폐 뒤에서 상반신을 꼿꼿이 세움)
    public sealed class EnemyPoseVarietyTests
    {
        [Test]
        public void PaceStaysWithinTheSpread()
        {
            Assert.AreEqual(0.92f, EnemyPoseVariety.Pace(0f, 0.08f), 1e-5f);
            Assert.AreEqual(1f, EnemyPoseVariety.Pace(0.5f, 0.08f), 1e-5f);
            Assert.AreEqual(1.08f, EnemyPoseVariety.Pace(1f, 0.08f), 1e-5f);
            Assert.AreEqual(1.08f, EnemyPoseVariety.Pace(3f, 0.08f), 1e-5f); // clamped
            Assert.AreEqual(1f, EnemyPoseVariety.Pace(0.1f, 0f), 1e-5f);    // no spread: everyone at 1
        }

        [Test]
        public void HeKeepsLowOnlyWhileWaitingAtHisCover()
        {
            Assert.IsTrue(EnemyPoseVariety.KeepsLow(true, true, false, false));
            // standing up to fire, or about to: upright as before (the firing rhythm is unchanged)
            Assert.IsFalse(EnemyPoseVariety.KeepsLow(true, true, true, false));
            Assert.IsFalse(EnemyPoseVariety.KeepsLow(true, true, false, true));
            // running, retreating, or switched off
            Assert.IsFalse(EnemyPoseVariety.KeepsLow(true, false, false, false));
            Assert.IsFalse(EnemyPoseVariety.KeepsLow(false, true, false, false));
        }

        [Test]
        public void TheHunchComesAndGoesGradually()
        {
            float w = 0f;
            for (int i = 0; i < 6; i++) w = EnemyPoseVariety.Ease(w, true, 2.5f, 1f / 60f);
            Assert.That(w, Is.InRange(0.2f, 0.3f));
            for (int i = 0; i < 60; i++) w = EnemyPoseVariety.Ease(w, true, 2.5f, 1f / 60f);
            Assert.AreEqual(1f, w, 1e-5f);
            // up to fire within the 0.7 s he takes to stand before the shot
            for (int i = 0; i < 25; i++) w = EnemyPoseVariety.Ease(w, false, 2.5f, 1f / 60f);
            Assert.AreEqual(0f, w, 1e-5f);
        }
    }
}
