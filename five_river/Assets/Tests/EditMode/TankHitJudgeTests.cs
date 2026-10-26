using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 맵 배치(MapBuilder)에서 참호는 정면, 왼쪽 둔덕은 측면이 되는지 (BALANCE 2-3 sideAngle 50°)
    public class TankHitJudgeTests
    {
        const float SideAngle = 50f;
        static readonly Vector3 TankHold = new Vector3(14f, 2.6f, 48f);
        static readonly Vector3 TankForward = Vector3.back; // 참호(-Z) 쪽을 본다

        [TestCase(0f, 0f)]
        [TestCase(-18f, 0f)]
        [TestCase(18f, 0f)]
        public void FromTrench_IsRicochet(float x, float z)
        {
            Assert.AreEqual(TankHitResult.Ricochet, TankHitJudge.Judge(TankHold, TankForward, new Vector3(x, 1.6f, z), SideAngle));
        }

        [Test]
        public void FromMoundTop_IsSide()
        {
            var mound = new Vector3(-30f, 7.6f, 42f);
            Assert.AreEqual(TankHitResult.Side, TankHitJudge.Judge(TankHold, TankForward, mound, SideAngle));
            Assert.Greater(TankHitJudge.AngleFromFront(TankHold, TankForward, mound), 75f);
        }

        [Test]
        public void FromBehind_CountsAsSide()
        {
            Assert.AreEqual(TankHitResult.Side, TankHitJudge.Judge(TankHold, TankForward, new Vector3(14f, 3f, 80f), SideAngle));
        }

        [Test]
        public void HeightDifference_IsIgnored()
        {
            float low = TankHitJudge.AngleFromFront(TankHold, TankForward, new Vector3(-30f, 0f, 42f));
            float high = TankHitJudge.AngleFromFront(TankHold, TankForward, new Vector3(-30f, 30f, 42f));
            Assert.AreEqual(low, high, 1e-3f);
        }
    }
}
