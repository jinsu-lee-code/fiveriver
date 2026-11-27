using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 고지 돌격 중 멈춰 쏘는 아군은 무릎쏴 (2026-11-27, 검수 R_03_hill_assault_a: 발을 모으고 꼿꼿이 서서 쏨)
    public sealed class AllyHillFireTests
    {
        [Test]
        public void OutOnTheHillHeKneelsToFire_AtTheParapetHeStands()
        {
            Assert.IsFalse(AllyCombatant.FiresStandingFor(true, true, true));   // out of the trench on the hill: one knee
            Assert.IsTrue(AllyCombatant.FiresStandingFor(true, true, false));   // the trench parapet: standing as before
            Assert.IsTrue(AllyCombatant.FiresStandingFor(true, false, true));   // switched off: standing everywhere as before
            Assert.IsFalse(AllyCombatant.FiresStandingFor(false, true, false)); // standing fire off: kneeling everywhere as before
        }

        // 2026-11-27 (07 재측정): halted on the hill still in the advance gait he fired upright; he fires once the gait is off the screen
        [Test]
        public void OnTheHillHeFiresOnlyOnceTheAdvanceGaitHasFaded()
        {
            float most = AllyCombatant.KneelGaitMax;
            Assert.IsFalse(AllyCombatant.KneelReadyToFire(false, true, 1f));          // still in the crouched advance: no shot
            Assert.IsFalse(AllyCombatant.KneelReadyToFire(false, true, 0.5f));        // halfway down to the knee: no shot
            Assert.IsTrue(AllyCombatant.KneelReadyToFire(false, true, most));         // down on the knee
            Assert.IsTrue(AllyCombatant.KneelReadyToFire(false, true, 0f));
            Assert.IsTrue(AllyCombatant.KneelReadyToFire(false, false, 1f));          // the trench: unchanged
            Assert.IsTrue(AllyCombatant.KneelReadyToFire(true, true, 1f));            // the standing shot has its own wait (StandingAimWeight)
        }
    }
}
