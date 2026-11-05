using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    public class TankApproachTests
    {
        static readonly Vector3 Start = new Vector3(44.766666f, 0.009178437f, 105f);
        static readonly Vector3 Hold = new Vector3(40.94f, 0.009178437f, 23f);
        static readonly Quaternion StartRotation = Quaternion.Euler(0f, 182.7f, 0f);
        static readonly Quaternion HoldRotation = Quaternion.Euler(0f, 260f, 0f);

        [Test]
        public void MovingHullStaysAlignedWithTravelAndTurnsOnlyAfterStopping()
        {
            var previousPosition = Start;
            var previousRotation = StartRotation;
            bool sawTranslation = false, sawStationaryTurn = false;
            for (int step = 1; step <= 400; step++)
            {
                TankController.SampleApproach(step / 400f, Start, StartRotation, Hold, HoldRotation,
                    out var position, out var rotation);
                var travel = position - previousPosition;
                if (travel.sqrMagnitude > 0.000001f)
                {
                    sawTranslation = true;
                    Assert.Less(Vector3.Angle(travel, rotation * Vector3.forward), 0.1f,
                        "The hull must not slide sideways while facing the final defence direction.");
                    Assert.Less(Quaternion.Angle(previousRotation, rotation), 0.01f);
                }
                else if (Quaternion.Angle(previousRotation, rotation) > 0.01f)
                {
                    sawStationaryTurn = true;
                    Assert.Less(Vector3.Distance(position, Hold), 0.0001f);
                }
                Assert.AreEqual(Start.y, position.y, 0.00001f, "Authored road height is preserved.");
                previousPosition = position;
                previousRotation = rotation;
            }
            Assert.IsTrue(sawTranslation);
            Assert.IsTrue(sawStationaryTurn);
        }

        [Test]
        public void FinalHeadingCompletesAtOriginalApproachDuration()
        {
            float duration = Vector3.Distance(Start, Hold) / 3.5f;
            TankController.SampleApproach((duration - 0.01f) / duration, Start, StartRotation, Hold, HoldRotation,
                out var beforePosition, out var beforeRotation);
            Assert.Less(Vector3.Distance(beforePosition, Hold), 0.0001f);
            // Quaternion.Angle treats this small remaining turn as equal; compare headings directly.
            Assert.Greater(Vector3.Angle(beforeRotation * Vector3.forward, HoldRotation * Vector3.forward), 0.05f);

            TankController.SampleApproach(duration / duration, Start, StartRotation, Hold, HoldRotation,
                out var position, out var rotation);
            Assert.Less(Vector3.Distance(position, Hold), 0.0001f);
            Assert.Less(Quaternion.Angle(rotation, HoldRotation), 0.01f);
        }

        [TestCase(-0.1f, false)]
        [TestCase(1.2f, true)]
        public void FrameOvershootClampsToAuthoredEndpoints(float progress, bool end)
        {
            TankController.SampleApproach(progress, Start, StartRotation, Hold, HoldRotation,
                out var position, out var rotation);
            Assert.Less(Vector3.Distance(position, end ? Hold : Start), 0.0001f);
            Assert.Less(Quaternion.Angle(rotation, end ? HoldRotation : StartRotation), 0.01f);
        }
    }
}
