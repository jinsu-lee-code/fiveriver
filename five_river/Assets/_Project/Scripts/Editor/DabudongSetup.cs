using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Dabudong 씬의 시스템 오브젝트를 만들고 연결한다. 편집 모드에서만, 몇 번 실행해도 같은 결과.
    static class DabudongSetup
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dabudong.unity";
        public const string DataDir = "Assets/_Project/Data/";

        [MenuItem("FiveRivers/Setup/Dabudong Scene")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Setup] 플레이 중에는 실행하지 않는다");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[Setup] Dabudong 씬을 연 상태에서 실행한다: " + ScenePath);
                return;
            }

            MapBuilder.Build();
            SetupSystems();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Dabudong 씬 구성 완료");
        }

        static void SetupSystems()
        {
            var systems = SetupUtil.Root("[Systems]");
            var gate = SetupUtil.Ensure<InputGate>(systems);
            var director = SetupUtil.Ensure<FlowDirector>(systems);
            SetupUtil.SetRef(director, "config", SetupUtil.LoadAsset<FlowConfig>(DataDir + "FlowConfig.asset"));
            SetupUtil.SetRef(director, "tankConfig", SetupUtil.LoadAsset<TankConfig>(DataDir + "TankConfig.asset"));
            SetupUtil.SetRef(director, "assistConfig", SetupUtil.LoadAsset<AssistConfig>(DataDir + "AssistConfig.asset"));
            SetupUtil.SetRef(director, "inputGate", gate);

            var debug = SetupUtil.Ensure<DebugWindow>(systems);
            SetupUtil.SetRef(debug, "director", director);
        }
    }
}
