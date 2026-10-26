using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Tests
{
    // CLAUDE.md "기술 스택 (변경 금지)"와 빌드 씬 목록이 바뀌지 않았는지 지킨다.
    public class ProjectSetupTests
    {
        const string MainScenePath = "Assets/_Project/Scenes/Dabudong.unity";

        [Test]
        public void RenderPipeline_IsUrp()
        {
            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline);
        }

        [Test]
        public void BuildSettings_FirstSceneIsDabudong()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsNotEmpty(scenes);
            Assert.AreEqual(MainScenePath, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);
        }
    }
}
