using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

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
        // 2026-11-24: the general run's main assault comes in waves (user: "방어공세는 조금 짧아도 되지만 그래도 이건 너무짧다")
        [Test]
        public void Waves_WaitForTheirTime_EvenWhenTheFieldIsClear()
        {
            var plan = new WavePlan(new[] { 2, 2 }, new[] { 0f, 30f }, 9, 0f, 0, startTime: 100f);
            Assert.AreEqual(4, plan.Remaining);
            Assert.IsTrue(plan.TrySpawn(100f)); Assert.AreEqual(0, plan.SpawnWave);
            Assert.IsTrue(plan.TrySpawn(100f)); Assert.AreEqual(0, plan.SpawnWave);
            plan.OnEnemyGone(); plan.OnEnemyGone();               // the first wave is down at once
            Assert.IsFalse(plan.Done, "the second wave is still to come");
            Assert.AreEqual(30f, plan.NextWaveAt);
            for (float t = 100f; t < 130f; t += 0.5f) Assert.IsFalse(plan.TrySpawn(t), "a lull until the second wave's time");
            Assert.IsTrue(plan.TrySpawn(130f)); Assert.AreEqual(1, plan.SpawnWave);
            Assert.IsTrue(plan.TrySpawn(130f));
            Assert.IsFalse(plan.TrySpawn(131f));
            plan.OnEnemyGone(); plan.OnEnemyGone();
            Assert.IsTrue(plan.Done);
            Assert.AreEqual(-1f, plan.NextWaveAt);
        }

        [Test]
        public void Waves_Overlap_WhenTheLastOneIsStillOut_ButNeverPastTheCap()
        {
            var plan = new WavePlan(new[] { 5, 5, 5 }, new[] { 0f, 26f, 50f }, 9, 1.5f);
            int peak = 0; var perWave = new int[3];
            for (float t = 0f; t < 120f; t += 0.25f)
            {
                if (plan.TrySpawn(t)) perWave[plan.SpawnWave]++;
                peak = Mathf.Max(peak, plan.Active);
                Assert.LessOrEqual(plan.Active, 9);
            }
            Assert.AreEqual(9, peak, "nobody falls: the second wave joins the first up to the cap");
            Assert.AreEqual(5, perWave[0]);
            Assert.AreEqual(4, perWave[1], "the cap holds the rest back");
            Assert.AreEqual(0, perWave[2]);
        }

        [Test]
        public void Waves_StopSpawning_EndsThePlan()
        {
            var plan = new WavePlan(new[] { 3, 3 }, new[] { 0f, 20f }, 9, 0f);
            plan.TrySpawn(0f);
            plan.StopSpawning();
            Assert.IsFalse(plan.TrySpawn(30f));
            plan.OnEnemyGone();
            Assert.IsTrue(plan.Done);
        }

        [Test]
        public void Waves_ScoutsWhoJoined_CountTowardTheCap()
        {
            var plan = new WavePlan(new[] { 5, 5 }, new[] { 0f, 26f }, 6, 0f, joined: 2);
            int n = 0; while (plan.TrySpawn(0f)) n++;
            Assert.AreEqual(4, n);
            Assert.AreEqual(6, plan.Active);
        }
    }
}
