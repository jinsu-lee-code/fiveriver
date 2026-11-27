using FiveRivers.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 연출 컷에서 카메라와 피사체 사이를 가리는 장식 찾기 (2026-11-27, 검수: 전차 정중앙의 나무 줄기 · 하차 병사 앞 나무)
    public sealed class SightlineMathTests
    {
        static readonly Vector3 Eye = new Vector3(0f, 1.5f, 0f);
        static readonly Vector3 Subject = new Vector3(0f, 1.5f, 20f);

        [Test]
        public void ATrunkOnTheLineBlocks_OneBesideItOrBehindTheSubjectDoesNot()
        {
            var trunk = new Bounds(new Vector3(0f, 4f, 10f), new Vector3(0.6f, 8f, 0.6f));
            Assert.IsTrue(SightlineMath.Blocks(Eye, Subject, trunk, 0.6f, 0.15f));
            var beside = new Bounds(new Vector3(3f, 4f, 10f), new Vector3(0.6f, 8f, 0.6f));
            Assert.IsFalse(SightlineMath.Blocks(Eye, Subject, beside, 0.6f, 0.15f));
            var behind = new Bounds(new Vector3(0f, 4f, 26f), new Vector3(0.6f, 8f, 0.6f));
            Assert.IsFalse(SightlineMath.Blocks(Eye, Subject, behind, 0.6f, 0.15f));
            var behindCamera = new Bounds(new Vector3(0f, 4f, -5f), new Vector3(0.6f, 8f, 0.6f));
            Assert.IsFalse(SightlineMath.Blocks(Eye, Subject, behindCamera, 0.6f, 0.15f));
        }

        [Test]
        public void ABushTouchingTheSubjectIsNotInFrontOfIt()
        {
            // its near side is only 0.3 m short of the subject: it stands beside him, not between
            var atHisFeet = new Bounds(new Vector3(0f, 1.5f, 20f), new Vector3(1f, 1f, 0.6f));
            Assert.IsFalse(SightlineMath.Blocks(Eye, Subject, atHisFeet, 0.6f, 0.15f));
            // a bough over the lens: the camera is inside its box and the subject is not
            var overLens = new Bounds(new Vector3(0f, 1.5f, 0.2f), new Vector3(2f, 2f, 2f));
            Assert.IsTrue(SightlineMath.Blocks(Eye, Subject, overLens, 0.6f, 0.15f));
        }

        [Test]
        public void TheEntryDistanceIsWhereTheLineMeetsTheBox()
        {
            var box = new Bounds(new Vector3(0f, 1.5f, 10f), new Vector3(2f, 2f, 2f));
            Assert.IsTrue(SightlineMath.Enters(Eye, Subject, box, out float enter));
            Assert.AreEqual(9f, enter, 1e-4f);
            Assert.IsFalse(SightlineMath.Enters(Eye, Eye, box, out _));
        }

        [Test]
        public void TheSubjectIsSeenAtItsMiddleTopAndBothSides()
        {
            var points = new Vector3[4];
            Assert.AreEqual(4, SightlineMath.SubjectPoints(Eye, Subject, 2f, 2.8f, points));
            Assert.AreEqual(Subject, points[0]);
            Assert.AreEqual(Subject.y + 1.4f, points[1].y, 1e-5f);
            // the sides are across the view (here along x), as far as the half-width
            Assert.AreEqual(2f, Mathf.Abs(points[2].x - Subject.x), 1e-5f);
            Assert.AreEqual(-(points[2].x - Subject.x), points[3].x - Subject.x, 1e-5f);
            Assert.AreEqual(Subject.z, points[2].z, 1e-5f);
            Assert.AreEqual(0, SightlineMath.SubjectPoints(Eye, Subject, 2f, 2.8f, new Vector3[2]));
        }
    }
}
