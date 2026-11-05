using System;
using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 원본 다부동 맵의 좌표를 유지하고 기존 게임 진행 표식만 그 지형에 맞춘다.
    public static class ImportedMapSetup
    {
        public const string PrefabPath = "Assets/_Project/Art/Maps/DabudongRefined/SM_ENV_Dabudong_Refined.prefab";
        const string InstanceName = "Dabudong_Imported";
        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;

        [MenuItem("FiveRivers/Setup/Replace Graybox With Imported Map")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before replacing the map.");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath)
                throw new InvalidOperationException("Open Dabudong.unity before replacing the map.");
            if (!Available) throw new InvalidOperationException("Imported map prefab is missing.");
            var map = GameObject.Find("/[Map]");
            if (map == null) throw new InvalidOperationException("Dabudong gameplay map is missing.");
            var points = map.GetComponentInChildren<LevelPoints>(true);
            if (points == null || points.playerSpawn == null || points.tankCheckpoint == null || points.moundTop == null ||
                points.tankStart == null || points.tankHold == null || points.enemySpawns.Length != 5 ||
                points.retreatPoints.Length != 3 || points.scoutCovers.Length != 2 || points.assaultCovers.Length != 10 ||
                points.ammoCrates.Length != 2 || points.allySpots.Length != 3)
                throw new InvalidOperationException("Dabudong gameplay points are incomplete; map was not changed.");
            PlaceEnvironment(map.transform);
            AlignGameplay(map.transform, points);
            var text = AssetDatabase.LoadAssetAtPath<FiveRivers.Data.GameText>(DabudongSetup.DataDir + "GameText.asset");
            if (text != null)
            {
                text.objectiveTankMove = "측면 둔덕으로 이동하라";
                EditorUtility.SetDirty(text);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ImportedMap] Graybox replaced; gameplay references retained.");
        }

        public static void ValidateEnvironment()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var terrain = prefab != null ? prefab.GetComponentInChildren<Terrain>(true) : null;
            if (terrain == null || terrain.terrainData == null)
                throw new InvalidOperationException("Imported TerrainData must load in this Editor before replacing the map.");
        }

        public static void PlaceEnvironment(Transform map)
        {
            ValidateEnvironment();
            foreach (var path in new[] { "Ground", "Generated/Trench", "Generated/Field", "Generated/MoundPath", "Generated/TankRoad", "Generated/WireStakes" })
            {
                var old = map.Find(path);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var previous = map.Find(InstanceName);
            if (previous != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(previous.gameObject) != PrefabPath)
                Object.DestroyImmediate(previous.gameObject);
            if (map.Find(InstanceName) == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, map);
                instance.name = InstanceName;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
            }
            var surface = SetupUtil.Ensure<NavMeshSurface>(map.gameObject);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            SetupUtil.Ensure<NavMeshBootstrap>(map.gameObject);
        }

        public static void AlignGameplay(Transform map, LevelPoints points)
        {
            var terrain = map.Find(InstanceName).GetComponentInChildren<Terrain>();
            if (terrain == null) throw new InvalidOperationException("Imported terrain is missing.");
            float Height(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            void Place(Transform t, float x, float z, float yaw = 0, bool trench = false)
            {
                // 다듬은 맵은 참호까지 연속 지형을 바닥으로 사용한다.
                t.SetPositionAndRotation(new Vector3(x, Height(x, z) + 0.04f, z), Quaternion.Euler(0, yaw, 0));
            }
            void Group(Transform[] ts, Vector2[] xz, float yaw)
            {
                if (ts.Length != xz.Length) throw new InvalidOperationException("Gameplay point count changed.");
                for (int i = 0; i < ts.Length; i++) Place(ts[i], xz[i].x, xz[i].y, yaw);
            }

            Place(points.playerSpawn, 0, -0.5f, 0, true);
            Place(points.tankCheckpoint, 17, -1, 70, true);
            Place(points.moundTop, 28, -6, 22);
            Group(points.enemySpawns, new[] { new Vector2(-20, 99), new Vector2(-10, 101), new Vector2(0, 99), new Vector2(17, 101), new Vector2(25, 99) }, 180);
            Group(points.retreatPoints, new[] { new Vector2(-20, 107), new Vector2(0, 108), new Vector2(25, 107) }, 0);
            // 원본 엄폐물의 북쪽(플레이어 반대편)에 둔다.
            Group(points.scoutCovers, new[] { new Vector2(-22, 64), new Vector2(-4, 68) }, 180);
            Group(points.assaultCovers, new[] {
                new Vector2(-18, 50), new Vector2(3, 54), new Vector2(20, 52),
                new Vector2(-20, 38), new Vector2(-2, 40), new Vector2(26, 40),
                new Vector2(-16, 29), new Vector2(0, 28), new Vector2(21, 30), new Vector2(24, 66)
            }, 180);
            // 실제 흙길의 중심선을 따른다. 정지 지점에서는 진지를 향해 차체를 돌린다.
            Place(points.tankStart, RoadX(105), 105, 182.7f);
            Place(points.tankHold, RoadX(23), 23, 260);
            Place(points.ammoCrates[0], 5, -1.6f, 0, true);
            Place(points.ammoCrates[1], 17.5f, -1.6f, 0, true);
            Place(points.target, 4, 18, 180);
            Place(points.parkSpot, -6.5f, -1.2f, 90, true);
            Place(points.blanketSpot, -9.5f, -1.5f, 90, true);
            Group(points.allySpots, new[] { new Vector2(-14, -0.4f), new Vector2(11, -0.4f), new Vector2(16, -0.4f) }, 0);
            Place(points.reinforcementsStart, 27, -35);
            Place(points.reinforcementsEnd, 23, -1);

            void Move(Transform t, Transform point) { if (t != null) t.SetPositionAndRotation(point.position, point.rotation); }
            Move(map.Find("Generated/People/ParkYongpal"), points.parkSpot);
            Move(map.Find("Generated/People/BlanketBody"), points.blanketSpot);
            for (int i = 0; i < points.allySpots.Length; i++) Move(map.Find("Generated/People/Ally_" + (i + 1)), points.allySpots[i]);
            Move(map.Find("Generated/Props/TutorialTarget"), points.target);
            var props = SetupUtil.Child(SetupUtil.Child(map, "Generated").transform, "Props").transform;
            GrayboxAssets.EnsureComponent<AmmoCrate>(MapBuilder.CratePrefab());
            for (int i = 0; i < points.ammoCrates.Length; i++)
            {
                string name = "GameplayAmmoCrate_" + i;
                var crate = props.Find(name);
                if (crate == null) { var go = (GameObject)PrefabUtility.InstantiatePrefab(MapBuilder.CratePrefab(), props); go.name = name; crate = go.transform; }
                Move(crate, points.ammoCrates[i]);
            }

            Physics.SyncTransforms();
            var surface = map.GetComponent<NavMeshSurface>();
            var oldData = surface.navMeshData;
            NavMeshData temporaryData = null;
            try
            {
                surface.BuildNavMesh();
                temporaryData = surface.navMeshData;
                foreach (var t in points.enemySpawns.Concat(points.retreatPoints).Concat(points.scoutCovers).Concat(points.assaultCovers))
                {
                    if (!NavMesh.SamplePosition(t.position, out var hit, 4f, NavMesh.AllAreas))
                        throw new InvalidOperationException("Point is outside the NavMesh: " + t.name);
                    t.position = hit.position;
                }
            }
            finally
            {
                surface.RemoveData();
                surface.navMeshData = oldData;
                if (oldData != null && surface.isActiveAndEnabled) surface.AddData();
                if (temporaryData != null && temporaryData != oldData && !AssetDatabase.Contains(temporaryData))
                    Object.DestroyImmediate(temporaryData);
            }
            Move(GameObject.Find("/[Player]")?.transform, points.playerSpawn);
            var tank = GameObject.Find("/[Tank]");
            if (tank != null)
            {
                var controller = tank.GetComponent<TankController>();
                var body = new SerializedObject(controller).FindProperty("body").objectReferenceValue as Transform;
                Move(body, points.tankHold);
            }
            EditorUtility.SetDirty(points);
        }

        static float RoadX(float z) => 38f + 7f * (z + 40f) / 150f;
    }
}
