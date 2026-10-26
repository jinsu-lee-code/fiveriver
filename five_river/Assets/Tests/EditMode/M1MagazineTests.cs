using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    public class M1MagazineTests
    {
        [Test]
        public void EightShots_EjectClipOnLastShot()
        {
            var mag = new M1Magazine(8, 16, 64);
            for (int i = 0; i < 7; i++)
            {
                Assert.IsTrue(mag.Fire(out bool ejected));
                Assert.IsFalse(ejected);
            }
            Assert.IsTrue(mag.Fire(out bool last));
            Assert.IsTrue(last);
            Assert.IsFalse(mag.CanFire);
            Assert.IsFalse(mag.Fire(out _));
            Assert.AreEqual(ReloadKind.Empty, mag.PlanReload());
        }

        [Test]
        public void EmptyReload_TakesFullClipFromReserve()
        {
            var mag = new M1Magazine(8, 16, 64);
            for (int i = 0; i < 8; i++) mag.Fire(out _);
            mag.CompleteReload();
            Assert.AreEqual(8, mag.InClip);
            Assert.AreEqual(8, mag.Reserve);
        }

        [Test]
        public void PartialReload_ReturnsRemainingRoundsToReserve()
        {
            var mag = new M1Magazine(8, 16, 64);
            for (int i = 0; i < 3; i++) mag.Fire(out _);
            Assert.AreEqual(ReloadKind.Partial, mag.PlanReload());
            mag.CompleteReload();
            Assert.AreEqual(8, mag.InClip);
            Assert.AreEqual(13, mag.Reserve);
            Assert.AreEqual(21, mag.Total);
        }

        [Test]
        public void NoReserve_NoReload_AndLastPartialClip()
        {
            var mag = new M1Magazine(8, 3, 64);
            for (int i = 0; i < 8; i++) mag.Fire(out _);
            mag.CompleteReload();
            Assert.AreEqual(3, mag.InClip);
            Assert.AreEqual(0, mag.Reserve);
            for (int i = 0; i < 3; i++) mag.Fire(out _);
            Assert.AreEqual(ReloadKind.None, mag.PlanReload());
        }

        [Test]
        public void FullClip_NoReload_RefillToMax()
        {
            var mag = new M1Magazine(8, 0, 40);
            Assert.AreEqual(ReloadKind.None, mag.PlanReload());
            mag.Refill();
            Assert.AreEqual(40, mag.Reserve);
        }
    }
}
