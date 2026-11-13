using System.Reflection;
using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    public sealed class AlliedTerrainFootPlacementTests
    {
        static readonly MethodInfo Lift = typeof(AlliedTerrainFootPlacement).GetMethod(
            "RequiredLift", BindingFlags.Static | BindingFlags.NonPublic);

        [TestCase(-.015f, .003f, .2f, .018f)]
        [TestCase(-.133f, .003f, .2f, .136f)]
        [TestCase(-.047624588f, .003f, .2f, .050624588f)]
        [TestCase(.003f, .003f, .2f, 0f)]
        [TestCase(.12f, .003f, .2f, 0f)]
        [TestCase(-.21f, .003f, .2f, 0f)]
        [TestCase(-.015f, .003f, .01f, 0f)]
        [TestCase(float.NaN, .003f, .2f, 0f)]
        [TestCase(float.NegativeInfinity, .003f, .2f, 0f)]
        public void GroundCorrectionUsesClearanceAndPreservesSwing(float gap, float clearance, float maximum, float expected)
        {
            Assert.That(Lift, Is.Not.Null);
            var actual = (float)Lift.Invoke(null, new object[] { gap, clearance, maximum });
            Assert.That(actual, Is.EqualTo(expected).Within(.000001f));
        }

        [TestCase(true, -.017f)]
        [TestCase(false, 0f)]
        public void OnlySettledStandingMayLowerTheFoot(bool allowLower, float expected)
        {
            var correction = typeof(AlliedTerrainFootPlacement).GetMethod(
                "RequiredCorrection", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(correction, Is.Not.Null);
            var actual = (float)correction.Invoke(null, new object[] { .020f, .003f, .2f, allowLower });
            Assert.That(actual, Is.EqualTo(expected).Within(.000001f));
        }

        [TestCase(.72f, true)]
        [TestCase(.74f, false)]
        public void LoweringRequiresAReachableTarget(float distance, bool expected)
        {
            var canReach = typeof(AlliedTerrainFootPlacement).GetMethod(
                "CanReach", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(canReach, Is.Not.Null);
            Assert.That(canReach.Invoke(null, new object[] { .4f, .33f, distance }), Is.EqualTo(expected));
        }
        [TestCase(.7f, 0f)]
        [TestCase(.72f, .012f)]
        [TestCase(.8f, .03f)]
        [TestCase(float.NaN, 0f)]
        public void PelvisOnlyDropsForMissingReachAndStaysBounded(float targetBelowHip, float expected)
        {
            var drop = typeof(AlliedTerrainFootPlacement).GetMethod(
                "RequiredPelvisDrop", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(drop, Is.Not.Null);
            var actual = (float)drop.Invoke(null, new object[] { .355f, .355f, new Vector3(0f, targetBelowHip, 0f), .03f });
            Assert.That(actual, Is.EqualTo(expected).Within(.000001f));
        }

        [Test]
        public void AHorizontalUnreachableTargetDoesNotMoveThePelvis()
        {
            var drop = typeof(AlliedTerrainFootPlacement).GetMethod(
                "RequiredPelvisDrop", BindingFlags.Static | BindingFlags.NonPublic);
            var actual = (float)drop.Invoke(null, new object[] { .355f, .355f, new Vector3(.8f, .8f, 0f), .03f });
            Assert.That(actual, Is.Zero);
        }

        [Test]
        public void WalkingSupportHasItsOwnFortyMillimetreLimit()
        {
            var drop = typeof(AlliedTerrainFootPlacement).GetMethod(
                "RequiredPelvisDrop", BindingFlags.Static | BindingFlags.NonPublic);
            var actual = (float)drop.Invoke(null, new object[] { .355f, .355f, new Vector3(0f, .8f, 0f), .04f });
            Assert.That(actual, Is.EqualTo(.04f));
        }

        [TestCase(.16f, .16f)]
        [TestCase(.135f, 0f)]
        public void KneeBendsForwardWithoutChangingTheAnkleGoal(float kneeForward, float ankleLift)
        {
            const BindingFlags nonPublic = BindingFlags.NonPublic;
            var placement = typeof(AlliedTerrainFootPlacement);
            var legType = placement.GetNestedType("Leg", nonPublic);
            var leg = System.Activator.CreateInstance(legType, true);
            var solve = placement.GetMethod("Solve", BindingFlags.Static | nonPublic);
            var hip = new GameObject("Hip").transform;
            try
            {
                var knee = new GameObject("Knee").transform;
                knee.SetParent(hip, false);
                knee.position = new Vector3(0f, -.41f, kneeForward);
                var ankle = new GameObject("Ankle").transform;
                ankle.SetParent(knee, false);
                ankle.position = new Vector3(0f, -.8f, .3f);
                var target = ankle.position + Vector3.up * ankleLift;
                var rotation = Quaternion.Euler(15f, 0f, 0f);
                legType.GetField("Upper", BindingFlags.Instance | nonPublic).SetValue(leg, hip);
                legType.GetField("Lower", BindingFlags.Instance | nonPublic).SetValue(leg, knee);
                legType.GetField("Foot", BindingFlags.Instance | nonPublic).SetValue(leg, ankle);

                solve.Invoke(null, new object[] { leg, target, rotation, Vector3.forward });

                var bend = Vector3.ProjectOnPlane(knee.position - hip.position, target - hip.position);
                Assert.That(Vector3.Dot(bend, Vector3.forward), Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(ankle.position, target), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(ankle.rotation, rotation), Is.LessThan(.01f));
            }
            finally
            {
                Object.DestroyImmediate(hip.gameObject);
            }
        }

    }
}
