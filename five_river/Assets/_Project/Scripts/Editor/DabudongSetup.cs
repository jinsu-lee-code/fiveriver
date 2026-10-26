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
            AnimationGraybox.Run();
            var points = MapBuilder.Build();
            var systems = SetupSystems();
            var player = PlayerBuilder.Build(points, systems.gate, systems.director, systems.effects);
            CombatBuilder.Build(systems.director.gameObject, systems.director, points, player, systems.effects);
            TankBuilder.Build(systems.director, points, player, systems.effects);
            SetupRunCoordinator(systems, points, player);
            if (TmpEssentials.Present) SetupUiAndNarrative(systems, points, player);
            else Debug.LogError("[Setup] TMP 필수 리소스가 없다 → FiveRivers/Setup/Import TMP Essentials 먼저");
            SetupDebug(systems.debug, player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Dabudong 씬 구성 완료");
        }

        // 그레이박스 프리팹을 다시 만든다 (FBX가 새로 생겼을 때). 같은 경로에 덮어써서 GUID는 유지된다.
        // 손으로 고친 프리팹 내용은 사라지므로, 최종 에셋이 들어온 뒤에는 쓰지 않는다 (FBX 교체만으로 충분).
        [MenuItem("FiveRivers/Setup/Rebuild Graybox Prefabs (FBX)")]
        public static void RebuildPrefabs()
        {
            GrayboxAssets.BeginRebuild();
            try
            {
                MapBuilder.SandbagPrefab();
                MapBuilder.CratePrefab();
                MapBuilder.TargetPrefab();
                MapBuilder.BlanketPrefab();
                MapBuilder.RokPrefab();
                MapBuilder.ParkPrefab();
                CombatBuilder.EnemyPrefab();
                TankBuilder.TankPrefab();
                PlayerBuilder.RocketPrefab();
            }
            finally
            {
                GrayboxAssets.EndRebuild();
            }
            GrayboxAssets.EnsureComponent<AmmoCrate>(MapBuilder.CratePrefab());
            GrayboxAssets.EnsureComponent<TutorialTarget>(MapBuilder.TargetPrefab());
            Run();
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
            SetupUtil.SetRef(SetupUtil.Ensure<SmokeRunner>(go), "director", s.director);
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

        static void SetupRunCoordinator(Systems systems, LevelPoints points, PlayerController player)
        {
            var run = SetupUtil.Ensure<RunCoordinator>(systems.director.gameObject);
            SetupUtil.SetRef(run, "director", systems.director);
            SetupUtil.SetRef(run, "points", points);
            SetupUtil.SetRef(run, "player", player);
            SetupUtil.SetRef(run, "health", player.GetComponent<PlayerHealth>());
            SetupUtil.SetRef(run, "weapons", player.GetComponent<WeaponHolder>());
            SetupUtil.SetRef(run, "effects", systems.effects);
        }

        static void SetupUiAndNarrative(Systems systems, LevelPoints points, PlayerController player)
        {
            var text = SetupUtil.LoadAsset<GameText>(DataDir + "GameText.asset");
            var target = Object.FindFirstObjectByType<TutorialTarget>();
            var ui = UiBuilder.Build(systems.director, player, text,
                SetupUtil.LoadAsset<ResultConfig>(DataDir + "ResultConfig.asset"),
                SetupUtil.LoadAsset<TutorialConfig>(DataDir + "TutorialConfig.asset"), target);

            var narrativeGo = GameObject.Find("/[Narrative]") ?? new GameObject("[Narrative]");
            var narrative = SetupUtil.Ensure<FiveRivers.Narrative.NarrativeDirector>(narrativeGo);
            SetupUtil.SetRef(narrative, "director", systems.director);
            SetupUtil.SetRef(narrative, "text", text);
            SetupUtil.SetRef(narrative, "objective", ui.objective);
            SetupUtil.SetRef(narrative, "subtitles", ui.subtitles);
            SetupUtil.SetRef(narrative, "points", points);
            SetupUtil.SetRef(narrative, "player", player);
            SetupUtil.SetRef(narrative, "resupplier", player.GetComponent<Resupplier>());
            SetupUtil.SetRef(narrative, "flares", Object.FindFirstObjectByType<FlareDirector>());
            SetupUtil.SetRef(narrative, "tank", Object.FindFirstObjectByType<TankController>());
            SetupUtil.SetRef(narrative, "blanketBody", GameObject.Find("/[Map]/Generated/People/BlanketBody"));

            var dawn = SetupUtil.Ensure<FiveRivers.Narrative.DawnDirector>(narrativeGo);
            SetupUtil.SetRef(dawn, "director", systems.director);
            SetupUtil.SetRef(dawn, "points", points);
            SetupUtil.SetRef(dawn, "moon", GameObject.Find("/Moon").GetComponent<Light>());
            SetupUtil.SetRef(dawn, "view", player.GetComponentInChildren<Camera>());
            SetupUtil.SetRef(dawn, "reinforcementPrefab", MapBuilder.RokPrefab());

            var recorder = SetupUtil.Ensure<FiveRivers.Recording.RunRecorder>(systems.director.gameObject);
            SetupUtil.SetRef(recorder, "director", systems.director);
            SetupUtil.SetRef(recorder, "player", player);
            SetupUtil.SetRef(recorder, "health", player.GetComponent<PlayerHealth>());
            SetupUtil.SetRef(recorder, "tank", Object.FindFirstObjectByType<TankController>());

            AudioGraybox.Run();
            var audioGo = GameObject.Find("/[Audio]") ?? new GameObject("[Audio]");
            var audio = SetupUtil.Ensure<FiveRivers.Audio.AudioDirector>(audioGo);
            SetupUtil.SetRef(audio, "library", SetupUtil.LoadAsset<SoundLibrary>(DataDir + "SoundLibrary.asset"));
            SetupUtil.SetRef(audio, "config", SetupUtil.LoadAsset<AudioConfig>(DataDir + "AudioConfig.asset"));
            SetupUtil.SetRef(audio, "director", systems.director);
            SetupUtil.SetRef(audio, "player", player);
            SetupUtil.SetRef(audio, "health", player.GetComponent<PlayerHealth>());
            SetupUtil.SetRef(audio, "weapons", player.GetComponent<WeaponHolder>());
            SetupUtil.SetRef(audio, "resupplier", player.GetComponent<Resupplier>());
            SetupUtil.SetRef(audio, "effects", systems.effects);
            SetupUtil.SetRef(audio, "enemies", Object.FindFirstObjectByType<EnemyDirector>());
            SetupUtil.SetRef(audio, "tank", Object.FindFirstObjectByType<TankController>());
            SetupUtil.SetRef(audio, "flares", Object.FindFirstObjectByType<FlareDirector>());
            SetupUtil.SetRef(audio, "subtitles", ui.subtitles);
            SetupUtil.SetRef(audio, "objective", ui.objective);
        }

        static void SetupDebug(DebugWindow debug, PlayerController player)
        {
            var panel = SetupUtil.Ensure<PlayerDebugPanel>(debug.gameObject);
            SetupUtil.SetRef(panel, "player", player);
        }
    }
}
