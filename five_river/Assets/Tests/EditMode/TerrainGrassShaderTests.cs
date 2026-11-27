using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Tests
{
    // 지형 풀 빌보드 셰이더 사본 (2026-11-27): 컴파일되고, URP 원본과 같은 패스를 갖는다.
    // 지형이 이 셰이더를 쓰는지는 메뉴 FiveRivers/Setup/Terrain Grass Billboard Shader 20261127 을 실행한 뒤에만 참이라 따로 본다.
    public sealed class TerrainGrassShaderTests
    {
        const string ShaderPath = "Assets/_Project/Art/Maps/Dabudong_Env/Shaders/FR_TerrainGrassBillboard.shader";

        [Test]
        public void TheGrassBillboardCopyCompilesWithTheSamePassesAsUrps()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, ShaderPath);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "셰이더 컴파일 오류: " + ShaderPath);
            var urp = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassBillboard.shader");
            Assert.IsNotNull(urp);
            Assert.AreEqual(urp.passCount, shader.passCount);
            var tag = new ShaderTagId("LightMode");
            for (int i = 0; i < urp.passCount; i++)
                Assert.AreEqual(urp.FindPassTagValue(i, tag), shader.FindPassTagValue(i, tag), $"pass {i}");
        }

        [Test]
        public void WhenTheTerrainUsesItThatIsTheProjectCopy()
        {
            if (!GraphicsSettings.TryGetRenderPipelineSettings<UniversalRenderPipelineRuntimeTerrainShaders>(out var terrain))
                Assert.Ignore("URP 지형 셰이더 설정 없음");
            var used = terrain.terrainDetailGrassBillboardShader;
            Assert.IsNotNull(used, "지형 풀 빌보드 셰이더가 비어 있음");
            string path = AssetDatabase.GetAssetPath(used);
            if (path != ShaderPath) Assert.Ignore("아직 URP 원본을 씀: 메뉴 Terrain Grass Billboard Shader 20261127 을 실행하면 바뀐다 (" + path + ")");
            Assert.IsFalse(ShaderUtil.ShaderHasError(used));
        }
    }
}
