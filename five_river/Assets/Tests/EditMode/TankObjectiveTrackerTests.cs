using FiveRivers.Narrative;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    public class TankObjectiveTrackerTests
    {
        [Test]
        public void ReachingMoundThenLeaving_KeepsEquipObjectiveUntilEquipped()
        {
            var tracker = new TankObjectiveTracker();
            tracker.Observe(false, false);
            Assert.AreEqual(TankObjectiveStep.MoveToMound, tracker.Step);
            tracker.Observe(true, false);
            Assert.AreEqual(TankObjectiveStep.EquipBazooka, tracker.Step);
            tracker.Observe(false, false);
            Assert.AreEqual(TankObjectiveStep.EquipBazooka, tracker.Step);
            tracker.Observe(false, true);
            Assert.AreEqual(TankObjectiveStep.FireAtSide, tracker.Step);
            tracker.Observe(false, false);
            Assert.AreEqual(TankObjectiveStep.FireAtSide, tracker.Step);
        }

        [Test]
        public void EquippingEarly_DoesNotSkipMoveObjective()
        {
            var tracker = new TankObjectiveTracker();
            tracker.Observe(false, true);
            Assert.AreEqual(TankObjectiveStep.MoveToMound, tracker.Step);
            tracker.Observe(true, true);
            Assert.AreEqual(TankObjectiveStep.FireAtSide, tracker.Step);
        }

        [Test]
        public void Reset_ClearsCompletedActions()
        {
            var tracker = new TankObjectiveTracker();
            tracker.Observe(true, true);
            tracker.Reset();
            tracker.Observe(false, false);
            Assert.AreEqual(TankObjectiveStep.MoveToMound, tracker.Step);
        }
    }
}
