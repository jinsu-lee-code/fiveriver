using System.Collections.Generic;
using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 걸음 위상 (부족한 점 8번, 2026-11-26): 간 거리로 돌고, 한 걸음(보폭)마다 발이 닿으며 왼발 · 오른발이 번갈아 온다.
    public sealed class StepCycleTests
    {
        const float Dt = 1f / 60f;

        static List<(float distance, int foot)> Run(StepCycle steps, float speed, float seconds, bool running)
        {
            var landed = new List<(float, int)>();
            float gone = 0f;
            steps.Landed += foot => landed.Add((gone, foot));
            for (float t = 0f; t < seconds; t += Dt) { gone += speed * Dt; steps.Advance(speed * Dt, running, Dt); }
            return landed;
        }

        [Test]
        public void AFootLandsEachStride_LeftAndRightInTurn()
        {
            var steps = new StepCycle { WalkStride = 1.6f, RunStride = 2.2f };
            var landed = Run(steps, 5.6f, 4f, true); // 22.4 m
            Assert.AreEqual(10, landed.Count, "22.4 m / 2.2 m");
            for (int i = 0; i < landed.Count; i++)
            {
                Assert.AreEqual(2.2f * (i + 1), landed[i].distance, 0.1f, "one stride apart");
                Assert.AreEqual(i % 2 == 0 ? 1 : 0, landed[i].foot, "right, then left, in turn");
            }
            Assert.AreEqual(5.6f / 4.4f, steps.CyclesPerSecond, 0.02f, "two steps a cycle");
            Assert.That(steps.Moving, Is.GreaterThan(0.95f));
        }

        [Test]
        public void StandingStill_DoesNotTurn_AndSettles()
        {
            var steps = new StepCycle();
            Run(steps, 1.4f, 1f, false);
            float phase = steps.Phase;
            var landed = Run(steps, 0f, 2f, false);
            Assert.AreEqual(0, landed.Count);
            Assert.AreEqual(phase, steps.Phase, 1e-6f, "the phase stops where the feet stopped");
            Assert.That(steps.Moving, Is.LessThan(0.01f));
            Assert.That(steps.CyclesPerSecond, Is.LessThan(0.01f));
        }

        [Test]
        public void ATeleportIsNotAStep_AndResetStartsOnTheLeftFoot()
        {
            var steps = new StepCycle();
            int landed = 0; steps.Landed += _ => landed++;
            steps.Advance(15f, false, Dt); // restored to a checkpoint
            Assert.AreEqual(0, landed);
            Assert.AreEqual(0f, steps.Phase);
            steps.Advance(1.0f, false, Dt);
            steps.Reset();
            Assert.AreEqual(0f, steps.Phase);
            Assert.AreEqual(0f, steps.Moving);
        }
    }
}
