using System.Collections.Generic;
using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 등에 멘 소총 · 멜빵과 몸 겉면 (2026-11-27, 부족한 점 7번: 총이 등에서 떠 있고 끈이 가슴 아래 몸속에 묻힘)
    public sealed class SlingGeometryTests
    {
        // a trunk 0.4 m wide: chest 0.12 m deep and rounded, back 0.13 m deep and flatter, like a back (body frame, metres)
        static float FrontDepth(float x) => 0.12f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / 0.2f) * (x / 0.2f)));
        static float BackDepth(float x) => 0.13f - 0.6f * x * x;

        static List<Vector3> Trunk(float back = 0f, float step = 0.01f)
        {
            var pts = new List<Vector3>();
            for (float y = -0.5f; y <= 0.15f; y += 0.02f)
                for (float x = -0.19f; x <= 0.19f; x += step)
                {
                    pts.Add(new Vector3(x, y, FrontDepth(x)));
                    pts.Add(new Vector3(x, y, -(BackDepth(x) + back)));
                }
            return pts;
        }

        static BodySurface Surface(float back = 0f)
        {
            var s = new BodySurface(-0.16f, 0.16f, -0.55f, 0.16f, 0.04f);
            s.AddAll(Trunk(back));
            s.Fill();
            return s;
        }

        [Test]
        public void TheSurfaceIsTheOutermostOfWhatWasSeen()
        {
            var s = Surface();
            Assert.IsTrue(s.Back(0f, -0.2f, out float b));
            Assert.That(b, Is.InRange(0.13f - 1e-4f, 0.135f));   // never under the real surface, at most a cell's curve over it
            Assert.IsTrue(s.Front(0.1f, 0f, out float f));
            Assert.GreaterOrEqual(f, FrontDepth(0.1f) - 1e-4f);
            Assert.IsFalse(s.Back(0.5f, 0f, out _));           // outside the trunk: unknown, not zero
            // a second look with him breathing out 2 cm further: the outermost stays
            var later = Surface(0.02f);
            s.Merge(later);
            Assert.IsTrue(s.Back(0f, -0.2f, out float merged));
            Assert.GreaterOrEqual(merged, 0.15f - 1e-4f);
        }

        [Test]
        public void SparseVerticesAreFilledBetween()
        {
            var s = new BodySurface(-0.16f, 0.16f, -0.55f, 0.16f, 0.04f);
            for (float y = -0.5f; y <= 0.15f; y += 0.12f)
                for (float x = -0.15f; x <= 0.15f; x += 0.12f) s.Add(new Vector3(x, y, -0.13f));
            Assert.IsFalse(s.Back(-0.09f, -0.33f, out _));
            s.Fill();
            Assert.IsTrue(s.Back(-0.09f, -0.33f, out float b));
            Assert.AreEqual(0.13f, b, 1e-4f);
        }

        [Test]
        public void ARifleFloatingOffTheBackIsLaidAgainstIt_NotThroughIt()
        {
            var s = Surface();
            // diagonal across the back, right shoulder to left hip, 30 cm off the back at the top and 18 cm at the bottom
            var rear = new Vector3(-0.15f, -0.45f, -0.31f); var front = new Vector3(0.14f, 0.12f, -0.43f);
            var along = new List<float>(); var gaps = new List<float>();
            for (int i = 0; i < 12; i++)
            {
                float t = Mathf.Lerp(-0.3f, 1.05f, i / 11f);
                var p = Vector3.Lerp(rear, front, t);
                along.Add(t * (front - rear).magnitude);
                gaps.Add(s.BackGapBehind(p));
            }
            Assert.IsTrue(SlingFit.Solve(along, gaps, 0.012f, 0.45f, out float shift, out float slope));
            Assert.Less(shift, -0.1f);   // in toward the back
            float least = float.MaxValue;
            for (int i = 0; i < gaps.Count; i++) if (!float.IsNaN(gaps[i])) least = Mathf.Min(least, gaps[i] + shift + slope * along[i]);
            Assert.AreEqual(0.012f, least, 1e-4f);                              // touching, never inside
            Assert.LessOrEqual(SlingFit.WorstGap(along, gaps, shift, slope), 0.05f); // and within 5 cm of it all along the back
        }

        [Test]
        public void ARifleStillInFrontHasNoBackGap()
        {
            var s = Surface();
            Assert.IsTrue(float.IsNaN(s.BackGapBehind(new Vector3(0f, -0.1f, 0.3f))));  // port arms, in front of the chest
            Assert.IsFalse(float.IsNaN(s.BackGapBehind(new Vector3(0f, -0.1f, -0.3f))));
            Assert.IsFalse(SlingFit.Solve(new[] { 0f, 1f }, new[] { float.NaN, float.NaN }, 0.012f, 0.45f, out _, out _));
        }

        [Test]
        public void AStrapCurveCuttingTheChestIsBroughtOutOverIt()
        {
            var s = Surface();
            // a chord from the right shoulder front down to the left hip, sagging into the chest
            var pts = new List<Vector3>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                pts.Add(new Vector3(Mathf.Lerp(0.12f, -0.14f, t), Mathf.Lerp(0.1f, -0.45f, t), 0.04f));
            }
            Assert.Greater(s.CountInside(pts, 0.002f), 10);
            var pushed = new List<Vector3>();
            foreach (var p in pts) pushed.Add(s.PushOut(p, 0.0075f));
            Assert.AreEqual(0, s.CountInside(pushed, 0.002f));
            foreach (var p in pushed) Assert.GreaterOrEqual(s.FrontGap(p), 0.0075f - 1e-4f);
            // a point already outside is left where it is
            var outside = new Vector3(0f, -0.2f, 0.3f);
            Assert.AreEqual(outside, s.PushOut(outside, 0.0075f));
            // behind him it goes out over the back
            var behind = s.PushOut(new Vector3(0f, -0.2f, -0.05f), 0.0075f);
            Assert.GreaterOrEqual(s.BackGap(behind), 0.0075f - 1e-4f);
        }
    }
}
