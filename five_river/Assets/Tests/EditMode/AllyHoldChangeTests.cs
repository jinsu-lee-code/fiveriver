using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Narrative;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 새벽 시작 · 견착 -> 앞에총에서 총과 몸이 한 프레임에 튀던 것 (2026-11-27, TASKS 「고지 출발 손」 남은 수치)
    public sealed class AllyHoldChangeTests
    {
        [Test]
        public void OnlyTheHillToDawnChangeKeepsThePoseFadeAndTheFacing()
        {
            Assert.IsTrue(AllyCombatant.StaysPutAcross(FlowSection.HillAssault, FlowSection.Dawn));
            Assert.IsFalse(AllyCombatant.StaysPutAcross(FlowSection.Tank, FlowSection.HillAssault)); // they leave the trench afterwards, from their posts
            Assert.IsFalse(AllyCombatant.StaysPutAcross(FlowSection.Dawn, FlowSection.Result));
            Assert.IsFalse(AllyCombatant.StaysPutAcross(FlowSection.Result, FlowSection.Title));
        }

        [Test]
        public void TheHipDropEasesOutEvenWhenTheAimVanishesInOneFrame()
        {
            float w = 1f;
            w = AllyCombatant.EaseStandWeight(w, 0f, 1f / 60f, 0.25f);
            Assert.That(w, Is.InRange(0.9f, 0.95f)); // one frame: about a fifteenth of the way
            for (int i = 0; i < 20; i++) w = AllyCombatant.EaseStandWeight(w, 0f, 1f / 60f, 0.25f);
            Assert.That(w, Is.InRange(0f, 0.01f));
            // a hitched frame does not jump it either way further than its share of the time
            Assert.AreEqual(0.6f, AllyCombatant.EaseStandWeight(1f, 0f, 0.1f, 0.25f), 1e-5f);
            Assert.AreEqual(1f, AllyCombatant.EaseStandWeight(0.5f, 3f, 1f, 0.25f), 1e-5f); // clamped to 1
            Assert.AreEqual(0.3f, AllyCombatant.EaseStandWeight(0.8f, 0.3f, 0.1f, 0f), 1e-5f); // no easing: follows at once
        }

        [Test]
        public void AGaitChangeWaitsForACrossFadeUnderWay()
        {
            Assert.IsTrue(HillAllies.WaitsForFade(true, true, false));   // moving: another fade is on, to somewhere else
            Assert.IsFalse(HillAllies.WaitsForFade(true, false, false)); // nothing under way: cross-fade now
            Assert.IsFalse(HillAllies.WaitsForFade(true, true, true));   // already on its way there
            Assert.IsFalse(HillAllies.WaitsForFade(false, true, false)); // a one-off (Stop): cuts in as before
        }
    }
}
