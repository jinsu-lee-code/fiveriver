using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 아군 소총 들기 방식이 바뀔 때(앞에총 ↔ 겨눔 ↔ 들기) 한 프레임에 튀지 않고 넘어가는 시계 (2026-11-24).
    public class AllyPoseFadeTests
    {
        const float Dt = 1f / 60f;

        [Test]
        public void SameModeNeverFades()
        {
            var c = new PoseFadeClock(0.22f);
            for (int i = 0; i < 30; i++) { c.Note(PoseFadeClock.PortStand, Dt); Assert.AreEqual(0f, c.HoldWeight); }
        }

        [Test]
        public void ModeChangeHoldsTheLastPoseThenLetsGo()
        {
            var c = new PoseFadeClock(0.22f);
            c.Note(PoseFadeClock.PortStand, Dt); c.Note(PoseFadeClock.PortStand, Dt);
            c.Note(PoseFadeClock.Aim, Dt);
            Assert.AreEqual(1f, c.HoldWeight, 1e-4f, "the frame it changes shows the last pose");
            float previous = 1f;
            for (int i = 0; i < 12; i++)
            {
                c.Note(PoseFadeClock.Aim, Dt);
                Assert.Less(c.HoldWeight, previous + 1e-6f, "goes down steadily");
                previous = c.HoldWeight;
            }
            for (int i = 0; i < 6; i++) c.Note(PoseFadeClock.Aim, Dt);
            Assert.AreEqual(0f, c.HoldWeight, "over after the duration");
        }

        [Test]
        public void ClipChangesAreLeftToTheClipPlayer()
        {
            var c = new PoseFadeClock(0.22f);
            c.Note(PoseFadeClock.Aim, Dt); c.Note(PoseFadeClock.Clip, Dt);
            Assert.AreEqual(0f, c.HoldWeight, "into a clip");
            c.Note(PoseFadeClock.Carry, Dt);
            Assert.AreEqual(0f, c.HoldWeight, "out of a clip");
        }

        [Test]
        public void ClearForgetsTheLastMode()
        {
            var c = new PoseFadeClock(0.22f);
            c.Note(PoseFadeClock.PortStand, Dt); c.Clear(); c.Note(PoseFadeClock.Aim, Dt);
            Assert.AreEqual(0f, c.HoldWeight, "a new section or retry does not fade from the old pose");
        }

        // 2026-11-25 (고지 출발 손): 141 ms 끊긴 프레임 하나가 섞기를 한 번에 넘기지 않는다 (30 fps 한 프레임만큼만)
        [Test]
        public void AHitchedFrameAdvancesTheFadeOneStepOnly()
        {
            var c = new PoseFadeClock(0.4f);
            c.Note(PoseFadeClock.PortMove, Dt); c.Note(PoseFadeClock.Aim, Dt);
            c.Note(PoseFadeClock.Aim, 0.141f);
            var even = new PoseFadeClock(0.4f);
            even.Note(PoseFadeClock.PortMove, Dt); even.Note(PoseFadeClock.Aim, Dt);
            even.Note(PoseFadeClock.Aim, PoseFadeClock.MaxStep);
            Assert.AreEqual(even.HoldWeight, c.HoldWeight, 1e-5f);
            Assert.Greater(c.HoldWeight, 0.9f, "most of the last pose is still shown after the hitch");
            Assert.AreEqual(0f, PoseFadeClock.Step(-1f));
            Assert.AreEqual(Dt, PoseFadeClock.Step(Dt), 1e-6f);
        }

        [Test]
        public void ChangingAgainMidwayStartsOver()
        {
            var c = new PoseFadeClock(0.22f);
            c.Note(PoseFadeClock.PortStand, Dt); c.Note(PoseFadeClock.Aim, Dt);
            for (int i = 0; i < 6; i++) c.Note(PoseFadeClock.Aim, Dt);
            Assert.Less(c.HoldWeight, 1f);
            c.Note(PoseFadeClock.Watch, Dt);
            Assert.AreEqual(1f, c.HoldWeight, 1e-4f);
        }
    }
}
