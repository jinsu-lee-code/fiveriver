using FiveRivers.Combat;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 걷은 소매 (2026-11-27, 사용자 결정 "소매 걷기"): M1 천 튜브의 링 반지름 — 위팔은 그대로, 팔꿈치 아래 접힌 띠는 소매보다
    // 두껍고, 그 아래는 손목 맨살 굵기로 줄어드는 팔뚝(소매보다 굵어지지 않음), 손목에서는 손 잘린 테두리 반지름과 맞는다.
    // (2026-11-27) the NavMesh bake leaves out mesh colliders over the builder's 1e9 m3 limit (the ridge backdrop), not the rest
    public sealed class NavMeshBackdropTests
    {
        [Test]
        public void OnlyTheHugeBackdropIsLeftOut()
        {
            Assert.IsTrue(FiveRivers.Core.NavMeshBootstrap.TooLargeForNavMesh(new Vector3(3000f, 169f, 3000f)));   // Ridges_Continuous_Backdrop
            Assert.IsFalse(FiveRivers.Core.NavMeshBootstrap.TooLargeForNavMesh(new Vector3(500f, 60f, 500f)));    // a map-sized piece
            Assert.IsFalse(FiveRivers.Core.NavMeshBootstrap.TooLargeForNavMesh(new Vector3(2f, 1f, 1f)));
        }
    }

    public sealed class RolledSleeveShapeTests
    {
        RolledSleeveConfig config;
        [SetUp] public void Make() => config = ScriptableObject.CreateInstance<RolledSleeveConfig>();
        [TearDown] public void Drop() => Object.DestroyImmediate(config);

        [Test]
        public void UpperSleeveUnchanged_BandThicker_ForearmBare()
        {
            const float cloth = 0.047f, wrist = 0.029f;
            Assert.AreEqual(cloth, RolledSleeveShape.Radius(config, -0.5f, 0f, cloth, wrist, out var skin), 1e-6f);   // upper arm
            Assert.IsFalse(skin);
            float band = RolledSleeveShape.Radius(config, -0.095f, 0f, cloth, wrist, out skin);                      // a roll's crest
            Assert.IsFalse(skin); Assert.Greater(band, cloth * 1.08f);
            float bare = RolledSleeveShape.Radius(config, 0.5f, 1.2f, cloth, wrist, out skin);
            Assert.IsTrue(skin); Assert.Less(bare, cloth); Assert.Greater(bare, wrist);
        }

        [Test]
        public void ForearmTapersToTheWristAndNeverOutgrowsTheSleeve()
        {
            const float cloth = 0.040f, wrist = 0.029f;
            float near = RolledSleeveShape.Radius(config, 0.3f, 1.6f, cloth, wrist, out _), far = RolledSleeveShape.Radius(config, 0.9f, 1.6f, cloth, wrist, out _);
            Assert.Greater(near, far);
            for (float f = 0.2f; f < 1f; f += 0.05f)
                for (float a = 0f; a < 6.3f; a += 0.4f)
                    Assert.LessOrEqual(RolledSleeveShape.Radius(config, f, a, cloth, wrist, out _), cloth + 1e-6f);
            // at the wrist, within the oval and the faint ulna knob of the bare wrist radius
            float mean = 0f; int n = 0;
            for (float a = 0f; a < 6.28f; a += 0.1f) { mean += RolledSleeveShape.Radius(config, 0.95f, a, 0.06f, wrist, out _); n++; }
            Assert.AreEqual(wrist, mean / n, wrist * 0.06f);
        }

        [Test]
        public void TubeLengthMapsToForearmFraction()
        {
            Assert.AreEqual(0f, RolledSleeveShape.ForearmFraction(0.5f, 0.3f, 0.25f), 1e-6f);
            Assert.AreEqual(1f, RolledSleeveShape.ForearmFraction(1f, 0.3f, 0.25f), 1e-6f);
            Assert.Less(RolledSleeveShape.ForearmFraction(0.45f, 0.3f, 0.25f), 0f);
            // the skin submesh starts just below the band's lip
            float from = RolledSleeveShape.ForearmFraction(config.m1SkinFromT, 0.3f, 0.25f);
            Assert.Greater(from, config.bandBottom + config.lipLength * 0.5f);
            Assert.Less(from, config.bandBottom + 0.1f);
            Assert.AreEqual(38, M1ArmRig.SkinFromRing(config.m1SkinFromT, 65));
        }
    }
}
