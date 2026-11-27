using System.Collections.Generic;
using FiveRivers.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 새벽 정상 · 트럭 하차에서 멈춘 사람들이 서는 자리 (2026-11-27, 검수: 한 자리에 겹쳐 서고 전원이 같은 자세)
    public sealed class StandSpacingTests
    {
        static void AllApart(IReadOnlyList<Vector3> spots, float gap)
        {
            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                    Assert.GreaterOrEqual(StandSpacing.Flat(spots[i], spots[j]), gap - 1e-4f, $"{i} and {j}");
        }

        [Test]
        public void MenInOneHeapAreSpreadAtLeastTheGapApart_TheFewestStepsAway()
        {
            var heap = new[] { new Vector3(0f, 5f, 0f), new Vector3(0.3f, 5f, 0.1f), new Vector3(-0.2f, 5f, 0.4f), new Vector3(0.1f, 5f, -0.3f) };
            var spots = StandSpacing.Spread(heap, 1.2f, (p, i) => true);
            AllApart(spots, 1.2f);
            // the first keeps his place, nobody goes further than the search reach, heights are kept
            Assert.AreEqual(heap[0], spots[0]);
            for (int i = 0; i < heap.Length; i++)
            {
                Assert.LessOrEqual(StandSpacing.Flat(heap[i], spots[i]), 2.4f + 1e-4f);
                Assert.AreEqual(heap[i].y, spots[i].y);
            }
        }

        [Test]
        public void MenAlreadyApartDoNotMove()
        {
            var apart = new[] { new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 3f) };
            var spots = StandSpacing.Spread(apart, 1.2f, (p, i) => false); // nowhere else allowed, and no need
            for (int i = 0; i < apart.Length; i++) Assert.AreEqual(apart[i], spots[i]);
        }

        [Test]
        public void ForbiddenGroundIsNeverChosen_AndTheOthersAreKeptClearOf()
        {
            // the trench lies at x < 0: no spot there
            var two = new[] { new Vector3(0.2f, 0f, 0f), new Vector3(0.4f, 0f, 0.2f) };
            var player = new[] { new Vector3(1.2f, 0f, 0f) };
            var spots = StandSpacing.Spread(two, 1.2f, (p, i) => p.x >= 0f, player, 1.4f);
            AllApart(spots, 1.2f);
            Assert.GreaterOrEqual(spots[1].x, 0f);
            Assert.GreaterOrEqual(StandSpacing.Flat(spots[1], player[0]), 1.4f - 1e-4f);
            // the first man stood too close to the player as well: he moved, onto allowed ground
            Assert.GreaterOrEqual(StandSpacing.Flat(spots[0], player[0]), 1.4f - 1e-4f);
            Assert.GreaterOrEqual(spots[0].x, 0f);
        }

        [Test]
        public void WithNowhereClearHeStaysWhereHeIs()
        {
            var two = new[] { Vector3.zero, new Vector3(0.1f, 0f, 0f) };
            var spots = StandSpacing.Spread(two, 1.2f, (p, i) => false);
            Assert.AreEqual(two[1], spots[1]);
        }

        [Test]
        public void EachManHasHisOwnFixedOffset()
        {
            var a = StandSpacing.Unit("Soldier_1", 1);
            Assert.AreEqual(a, StandSpacing.Unit("Soldier_1", 1));
            Assert.That(a, Is.InRange(-1f, 1f));
            // not the same value for every man or every purpose
            var values = new HashSet<float>();
            foreach (var name in new[] { "Soldier_1", "Soldier_2", "Soldier_3", "ParkYongpal", "Mansu" })
                for (int salt = 1; salt <= 3; salt++)
                {
                    float v = StandSpacing.Unit(name, salt);
                    Assert.That(v, Is.InRange(-1f, 1f));
                    values.Add(v);
                }
            Assert.Greater(values.Count, 10);
        }

        [Test]
        public void PosesAreMixed_AndTheLeaderStands()
        {
            var three = StandSpacing.Poses(3, 3, 1);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, three);
            var two = StandSpacing.Poses(2, 3, 2);
            Assert.AreNotEqual(two[0], two[1]);
            // man 0 is the squad leader: he always takes pose 0, the rest still differ
            var withLeader = StandSpacing.Poses(4, 3, 0, i => i == 0);
            Assert.AreEqual(0, withLeader[0]);
            Assert.AreEqual(3, new HashSet<int>(new[] { withLeader[1], withLeader[2], withLeader[3] }).Count);
        }
    }
}
