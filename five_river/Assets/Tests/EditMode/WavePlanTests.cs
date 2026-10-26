using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    public class WavePlanTests
    {
        [Test]
        public void MainAssault_NeverExceedsMaxActive()
        {
            var plan = new WavePlan(7, 5, 1f);
            int spawned = 0;
            for (float t = 0; t < 100; t += 0.25f)
            {
                if (plan.TrySpawn(t)) spawned++;
                Assert.LessOrEqual(plan.Active, 5);
                if (t > 10 && t % 3 == 0) plan.OnEnemyGone();
            }
            Assert.AreEqual(7, spawned);
        }

        [Test]
        public void JoinedScouts_CountTowardMaxActive()
        {
            var plan = new WavePlan(7, 5, 0f, joined: 2);
            int spawned = 0;
            while (plan.TrySpawn(0f)) spawned++;
            Assert.AreEqual(3, spawned);
            Assert.AreEqual(5, plan.Active);
        }

        [Test]
        public void Done_WhenAllSpawnedAndGone()
        {
            var plan = new WavePlan(2, 5, 0f);
            plan.TrySpawn(0f);
            plan.TrySpawn(0f);
            Assert.IsFalse(plan.Done);
            plan.OnEnemyGone();
            plan.OnEnemyGone();
            Assert.IsTrue(plan.Done);
        }

        [Test]
        public void StopSpawning_EndsAfterActiveGone()
        {
            var plan = new WavePlan(7, 5, 0f);
            plan.TrySpawn(0f);
            plan.StopSpawning();
            Assert.IsFalse(plan.TrySpawn(1f));
            plan.OnEnemyGone();
            Assert.IsTrue(plan.Done);
        }

        [Test]
        public void SpawnInterval_IsRespected()
        {
            var plan = new WavePlan(3, 5, 2f);
            Assert.IsTrue(plan.TrySpawn(0f));
            Assert.IsFalse(plan.TrySpawn(1f));
            Assert.IsTrue(plan.TrySpawn(2f));
        }
    }
}
