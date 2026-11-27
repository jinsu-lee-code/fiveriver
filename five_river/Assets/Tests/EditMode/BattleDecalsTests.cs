using System.Linq;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Tests
{
    // 전투 흔적 데칼 (부족한 점 5번, 2026-11-26, BALANCE.md 2-8): 수치 에셋 · 렌더러 기능 · 재질이 연결되어 있고,
    // 상한이 있으며 핏자국이 과장되지 않는다.
    public sealed class BattleDecalsTests
    {
        [Test]
        public void ConfigHasCapsAndQuietBlood()
        {
            var c = AssetDatabase.LoadAssetAtPath<BattleTraceConfig>(BattleTraceConfig.AssetPath);
            Assert.IsNotNull(c, "BattleTraceConfig asset (menu FiveRivers/Setup/Battle Decals)");
            Assert.That(c.bulletMost, Is.InRange(1, 300));
            Assert.That(c.scorchMost, Is.InRange(1, 40));
            Assert.That(c.bloodMost, Is.InRange(1, 80));
            Assert.That(c.bloodOpacity, Is.LessThanOrEqualTo(0.6f), "a stain soaked into dark earth, not a bright splash");
            Assert.That(c.bloodSize.y, Is.LessThanOrEqualTo(1.5f));
            Assert.That(c.depth, Is.InRange(0.05f, 0.5f), "shallow, so a passing man is not printed");
        }

        [Test]
        public void RendererHasDecalFeatureAndMaterialsExist()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            Assert.IsNotNull(data);
            Assert.IsTrue(data.rendererFeatures.Any(f => f is DecalRendererFeature && f.isActive), "Decal renderer feature on PC_Renderer");
            foreach (var n in new[] { "BulletHole", "Scorch", "BloodSoak" })
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.Material>($"Assets/_Project/Art/Decals/M_Decal_{n}.mat"), n);
        }
    }
}
