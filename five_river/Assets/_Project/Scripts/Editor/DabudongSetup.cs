using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Dabudong 씬을 만들고 연결한다. 편집 모드에서만, 몇 번 실행해도 같은 결과.
    // 순서: 수치 에셋 → 맵 → 시스템 → 플레이어 → 전투 · 연출 (뒤의 것이 앞의 것을 참조)
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

            DataAssetsSetup.Run();
            var points = MapBuilder.Build();
            var systems = SetupSystems();
            var player = PlayerBuilder.Build(points, systems.gate, systems.director, systems.effects);
            CombatBuilder.Build(systems.director.gameObject, systems.director, points, player, systems.effects);
            SetupDebug(systems.debug, player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Dabudong 씬 구성 완료");
        }

        struct Systems
        {
            public InputGate gate;
            public FlowDirector director;
            public Effects effects;
            public DebugWindow debug;
        }

        static Systems SetupSystems()
        {
            var go = SetupUtil.Root("[Systems]");
            var s = new Systems
            {
                gate = SetupUtil.Ensure<InputGate>(go),
                director = SetupUtil.Ensure<FlowDirector>(go),
                effects = SetupUtil.Ensure<Effects>(go),
                debug = SetupUtil.Ensure<DebugWindow>(go),
            };
            SetupUtil.Ensure<RuntimeErrorLog>(go);
            SetupUtil.SetRef(s.director, "config", SetupUtil.LoadAsset<FlowConfig>(DataDir + "FlowConfig.asset"));
            SetupUtil.SetRef(s.director, "tankConfig", SetupUtil.LoadAsset<TankConfig>(DataDir + "TankConfig.asset"));
            SetupUtil.SetRef(s.director, "assistConfig", SetupUtil.LoadAsset<AssistConfig>(DataDir + "AssistConfig.asset"));
            SetupUtil.SetRef(s.director, "inputGate", s.gate);
            SetupUtil.SetRef(s.debug, "director", s.director);
            PlayerBuilder.SetupEffects(s.effects);

            GrayboxAssets.EnsureComponent<AmmoCrate>(MapBuilder.CratePrefab());
            GrayboxAssets.EnsureComponent<TutorialTarget>(MapBuilder.TargetPrefab());
            return s;
        }

        static void SetupDebug(DebugWindow debug, PlayerController player)
        {
            var panel = SetupUtil.Ensure<PlayerDebugPanel>(debug.gameObject);
            SetupUtil.SetRef(panel, "player", player);
        }
    }
}
