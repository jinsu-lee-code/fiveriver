using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 떨어진 소총이 언제 · 어떻게 굳는가 (2026-11-27, 검수: 쓰러진 적의 소총이 땅에 비스듬히 선 채 굳음)
    public sealed class DroppedRifleRestTests
    {
        static readonly DroppedRifleRest.Rule Rule = DroppedRifleRest.Rule.Fallback;

        [Test]
        public void ItIsNotFrozenWhileStillMoving_OnlyOnceAtRestAsleepOrTooLong()
        {
            // still moving 1 s after the drop: not yet
            Assert.IsFalse(DroppedRifleRest.ReadyToFreeze(1f, false, 0f, Rule));
            // at rest long enough, or asleep, or past the limit
            Assert.IsTrue(DroppedRifleRest.ReadyToFreeze(1f, false, Rule.restHold, Rule));
            Assert.IsTrue(DroppedRifleRest.ReadyToFreeze(1f, true, 0f, Rule));
            Assert.IsTrue(DroppedRifleRest.ReadyToFreeze(Rule.most, false, 0f, Rule));
        }

        [Test]
        public void StillnessCountsUpOnlyBelowBothThresholds()
        {
            float still = DroppedRifleRest.StillFor(0f, 0.01f, 0.05f, 0.02f, Rule);
            Assert.AreEqual(0.02f, still, 1e-6f);
            still = DroppedRifleRest.StillFor(still, 0.01f, 0.05f, 0.02f, Rule);
            Assert.AreEqual(0.04f, still, 1e-6f);
            // a small spin (rocking on its side) resets it, so does a slide
            Assert.AreEqual(0f, DroppedRifleRest.StillFor(still, 0.01f, Rule.restSpin * 2f, 0.02f, Rule));
            Assert.AreEqual(0f, DroppedRifleRest.StillFor(still, Rule.restSpeed * 2f, 0.05f, 0.02f, Rule));
        }

        [Test]
        public void TiltIsMeasuredFromTheGroundPlane()
        {
            Assert.AreEqual(0f, DroppedRifleRest.Tilt(Vector3.forward, Vector3.up), 1e-3f);
            Assert.AreEqual(90f, DroppedRifleRest.Tilt(Vector3.up, Vector3.up), 1e-3f);
            Assert.AreEqual(90f, DroppedRifleRest.Tilt(Vector3.down, Vector3.up), 1e-3f);
            Assert.AreEqual(45f, DroppedRifleRest.Tilt(new Vector3(0f, 1f, 1f), Vector3.up), 1e-3f);
            // on a 20 degree slope a rifle lying down the slope is flat on it
            var slope = Quaternion.Euler(20f, 0f, 0f) * Vector3.up;
            var downSlope = Quaternion.Euler(20f, 0f, 0f) * Vector3.forward;
            Assert.AreEqual(0f, DroppedRifleRest.Tilt(downSlope, slope), 1e-3f);
            Assert.IsFalse(DroppedRifleRest.NeedsLay(20f, Rule));
            Assert.IsTrue(DroppedRifleRest.NeedsLay(40f, Rule));
        }

        [Test]
        public void LayingPutsTheLongAxisOnTheGround_TheShortestWay()
        {
            var normal = Quaternion.Euler(0f, 0f, 12f) * Vector3.up;
            var leaning = Quaternion.Euler(-55f, 30f, 10f); // muzzle up at 55 degrees, a little rolled
            var laid = DroppedRifleRest.Lay(leaning, Vector3.forward, normal);
            Assert.AreEqual(0f, DroppedRifleRest.Tilt(laid * Vector3.forward, normal), 0.05f);
            // the muzzle still points the way it leaned (seen from above), not swung round
            var before = Vector3.ProjectOnPlane(leaning * Vector3.forward, normal).normalized;
            var after = Vector3.ProjectOnPlane(laid * Vector3.forward, normal).normalized;
            Assert.Greater(Vector3.Dot(before, after), 0.999f);
            // turned about the normal when the first place is blocked
            var turned = DroppedRifleRest.Lay(leaning, Vector3.forward, normal, 90f);
            Assert.AreEqual(0f, DroppedRifleRest.Tilt(turned * Vector3.forward, normal), 0.05f);
            Assert.AreEqual(90f, Vector3.Angle(after, Vector3.ProjectOnPlane(turned * Vector3.forward, normal)), 0.1f);
            // standing straight up it falls the way its top (local up) faces
            var upright = Quaternion.LookRotation(Vector3.up, Vector3.back);
            var fallen = DroppedRifleRest.Lay(upright, Vector3.forward, Vector3.up);
            Assert.AreEqual(0f, DroppedRifleRest.Tilt(fallen * Vector3.forward, Vector3.up), 0.05f);
        }

        [Test]
        public void LayingEasesOverItsTime()
        {
            Assert.AreEqual(0f, DroppedRifleRest.LayProgress(0f, Rule), 1e-6f);
            Assert.AreEqual(1f, DroppedRifleRest.LayProgress(Rule.laySeconds, Rule), 1e-6f);
            Assert.AreEqual(1f, DroppedRifleRest.LayProgress(Rule.laySeconds * 3f, Rule), 1e-6f);
            float mid = DroppedRifleRest.LayProgress(Rule.laySeconds * 0.5f, Rule);
            Assert.That(mid, Is.InRange(0.4f, 0.6f));
        }

        [Test]
        public void TheAssetValuesAreTheFallbackValues()
        {
            var config = ScriptableObject.CreateInstance<FiveRivers.Data.BodyPhysicsConfig>();
            var fromAsset = DroppedRifleRest.Rule.From(config);
            Assert.AreEqual(Rule.restSpeed, fromAsset.restSpeed);
            Assert.AreEqual(Rule.restSpin, fromAsset.restSpin);
            Assert.AreEqual(Rule.restHold, fromAsset.restHold);
            Assert.AreEqual(Rule.most, fromAsset.most);
            Assert.AreEqual(Rule.maxTilt, fromAsset.maxTilt);
            Assert.AreEqual(Rule.laySeconds, fromAsset.laySeconds);
            Object.DestroyImmediate(config);
        }
    }
}
