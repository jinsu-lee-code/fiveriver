using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Flare = FiveRivers.Combat.Flare;

namespace FiveRivers.EditorTools
{
    // 적 · 조명탄 시스템과 그레이박스 프리팹 (PF_CH_KPA_Rifleman, VFX_Flare).
    static class CombatBuilder
    {
        public static void Build(GameObject systems, FlowDirector director, LevelPoints points, PlayerController player, Effects effects)
        {
            var flares = SetupUtil.Ensure<FlareDirector>(systems);
            SetupUtil.SetRef(flares, "director", director);
            SetupUtil.SetRef(flares, "config", SetupUtil.LoadAsset<FlareConfig>(DabudongSetup.DataDir + "FlareConfig.asset"));
            SetupUtil.SetRef(flares, "flarePrefab", FlarePrefab().GetComponent<Flare>());
            SetupUtil.SetRef(flares, "effects", effects);

            var enemiesGo = GameObject.Find("/[Enemies]") ?? new GameObject("[Enemies]");
            var enemies = SetupUtil.Ensure<EnemyDirector>(enemiesGo);
            SetupUtil.SetRef(enemies, "director", director);
            SetupUtil.SetRef(enemies, "config", SetupUtil.LoadAsset<WaveConfig>(DabudongSetup.DataDir + "WaveConfig.asset"));
            SetupUtil.SetRef(enemies, "points", points);
            SetupUtil.SetRef(enemies, "enemyPrefab", EnemyPrefab().GetComponent<EnemyAgent>());
            SetupUtil.SetRef(enemies, "player", player);
            SetupUtil.SetRef(enemies, "playerHealth", player.GetComponent<PlayerHealth>());
            SetupUtil.SetRef(enemies, "flares", flares);
            SetupUtil.SetRef(enemies, "effects", effects);
        }

        public static GameObject EnemyPrefab() => GrayboxAssets.Prefab("PF_CH_KPA_Rifleman", () =>
        {
            var kpaCap = GrayboxAssets.Mat("KPACap", new Color(0.42f, 0.38f, 0.22f));
            var root = MapBuilder.Person(GrayboxAssets.Kpa, kpaCap, 1.68f, true, "Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx");
            var model = root.transform.Find("Model");
            // 적 소총 SM_WP_Mosin (손 소켓이 생기면 그쪽으로 옮긴다)
            var rifleHolder = new GameObject("Socket_Rifle").transform;
            rifleHolder.SetParent(model, false);
            rifleHolder.localPosition = new Vector3(0.2f, 1.25f, 0.05f);
            if (GrayboxModels.Exists("Weapons/Mosin/SM_WP_Mosin.fbx"))
            {
                var rifle = (GameObject)PrefabUtility.InstantiatePrefab(GrayboxModels.LoadFbx("Weapons/Mosin/SM_WP_Mosin.fbx"), rifleHolder);
                rifle.name = "Rifle";
            }
            else GrayboxAssets.Part(rifleHolder, PrimitiveType.Cube, "Rifle", new Vector3(0, 0, 0.3f), new Vector3(0.05f, 0.06f, 1.1f), GrayboxAssets.RifleWood);
            var muzzle = new GameObject("Socket_Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0.2f, 1.3f, 0.95f);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.68f;
            agent.angularSpeed = 360f;
            agent.acceleration = 10f;
            agent.stoppingDistance = 0.3f;

            var enemy = root.AddComponent<EnemyAgent>();
            SetupUtil.SetRef(enemy, "model", model);
            SetupUtil.SetRef(enemy, "muzzle", muzzle);
            var animator = GrayboxModels.Animate(model, "AC_KPA");
            if (animator != null) SetupUtil.SetRef(enemy, "animator", animator);
            return root;
        });

        public static GameObject FlarePrefab() => GrayboxAssets.Prefab("VFX_Flare", () =>
        {
            var root = new GameObject();
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.shadows = LightShadows.Soft;
            light.intensity = 0f;
            var visual = GrayboxAssets.Part(root.transform, PrimitiveType.Sphere, "Visual", Vector3.zero, Vector3.one * 0.3f, GrayboxAssets.FxWhite);
            visual.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var flare = root.AddComponent<Flare>();
            SetupUtil.SetRef(flare, "flareLight", light);
            SetupUtil.SetRef(flare, "visual", visual.transform);
            return root;
        });
    }
}
