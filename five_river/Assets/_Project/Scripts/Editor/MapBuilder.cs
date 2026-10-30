using System.Collections.Generic;
using FiveRivers.Core;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 다부동 그레이박스 맵. +Z가 적 쪽, 플레이어 참호는 z=0 근처.
    // 참호에서 보면 전차가 정면(도탄), 왼쪽 둔덕에서 보면 측면이 되도록 배치한다 (BALANCE 2-3 sideAngle 50°).
    // 생성 컨테이너("Generated")는 매번 지우고 다시 만든다. 같은 입력이면 같은 결과 (난수 시드 고정).
    static class MapBuilder
    {
        const string GroundMeshPath = "Assets/_Project/Art/Graybox/Ground_Dabudong.asset";
        const int Seed = 1950;

        // ---- 지형 ----
        public static float Height(float x, float z)
        {
            float h = 0f;
            if (z > 3f) h += 6f * Smooth01((z - 3f) / 97f);           // 적 쪽 비탈 (z=100 능선 6m)
            if (z > 100f) h -= 3f * Smooth01((z - 100f) / 30f);       // 능선 뒤 (적 출현 지점이 가려짐)
            if (z < -4f) h -= 1f * Smooth01((-z - 4f) / 30f);         // 참호 뒤
            float dx = x + 30f, dz = z - 42f;
            h += 4f * Mathf.Exp(-(dx * dx + dz * dz) / (2f * 7f * 7f)); // 왼쪽 측면 둔덕
            h += 0.3f * Mathf.Sin(x * 0.13f) * Mathf.Cos(z * 0.11f) * Smooth01((z - 4f) / 11f);
            return h;
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static Vector3 OnGround(float x, float z, float lift = 0f) => new Vector3(x, Height(x, z) + lift, z);

        // ---- 배치 표 ----
        static readonly Vector2[] EnemySpawns = { new(-24, 118), new(-10, 120), new(2, 118), new(22, 121), new(30, 119) };
        static readonly Vector2[] RetreatPoints = { new(-15, 126), new(5, 127), new(24, 125) };
        static readonly Vector2[] ScoutCovers = { new(-14, 58), new(3, 60) };
        static readonly Vector2[] AssaultCovers =
        {
            new(-16, 38), new(-8, 34), new(0, 40), new(8, 36), new(26, 36),
            new(-12, 24), new(-2, 22), new(6, 26), new(24, 20), new(-21, 28),
        };
        static readonly Vector2[] MoundPath = { new(-20, -0.5f), new(-21, 8), new(-24, 20), new(-28, 32), new(-30, 40) };
        const float RoadX = 14f;
        const float TankHoldZ = 48f;
        const float TankStartZ = 110f; // 능선 바로 뒤에서 넘어온다 (정지 위치까지 62 m)

        public static LevelPoints Build()
        {
            var map = SetupUtil.Root("[Map]");
            var gen = Regenerate(map.transform, "Generated");

            BuildGround(map.transform);
            BuildTrench(gen);
            BuildField(gen);
            BuildMoundPath(gen);
            BuildRoad(gen);
            BuildCharacters(gen);
            BuildLighting();
            return BuildPoints(map.transform);
        }

        static Transform Regenerate(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void BuildGround(Transform map)
        {
            const float minX = -120, maxX = 120, minZ = -80, maxZ = 160, step = 2;
            int nx = (int)((maxX - minX) / step) + 1, nz = (int)((maxZ - minZ) / step) + 1;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = minX + i * step, z = minZ + j * step;
                verts[j * nx + i] = new Vector3(x, Height(x, z), z);
                uvs[j * nx + i] = new Vector2(x / 4f, z / 4f);
            }
            var tris = new List<int>((nx - 1) * (nz - 1) * 6);
            for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);
            if (mesh == null)
            {
                GrayboxAssets.EnsureFolder("Assets/_Project/Art/Graybox");
                mesh = new Mesh { name = "Ground_Dabudong" };
                AssetDatabase.CreateAsset(mesh, GroundMeshPath);
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var ground = SetupUtil.Child(map, "Ground");
            SetupUtil.Ensure<MeshFilter>(ground).sharedMesh = mesh;
            SetupUtil.Ensure<MeshRenderer>(ground).sharedMaterial = GrayboxAssets.Ground;
            SetupUtil.Ensure<MeshCollider>(ground).sharedMesh = mesh;

            // NavMesh는 맵 전체에서 실행 시작 때 굽는다
            var surface = SetupUtil.Ensure<NavMeshSurface>(map.gameObject);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            SetupUtil.Ensure<NavMeshBootstrap>(map.gameObject);
        }

        static void BuildTrench(Transform gen)
        {
            var trench = new GameObject("Trench").transform;
            trench.SetParent(gen, false);
            var sandbag = SandbagPrefab();
            for (float x = -19; x <= 19; x += 2)
                Place(sandbag, trench, new Vector3(x, 0, 1.3f), 0);
            // 뒤쪽 낮은 벽 · 양 끝 벽
            for (float x = -19; x <= 19; x += 2)
                Place(sandbag, trench, new Vector3(x, -0.45f, -2.6f), 0);
            Place(sandbag, trench, new Vector3(21f, 0, -0.6f), 90);

            var crate = CratePrefab();
            Place(crate, trench, new Vector3(5f, 0, -1.6f), 10);
            Place(crate, trench, new Vector3(-17.5f, 0, -1.6f), -5);

            // 철조망 말뚝 (프로토타입 화면)
            var stakes = new GameObject("WireStakes").transform;
            stakes.SetParent(gen, false);
            for (float x = -21; x <= 21; x += 3)
                GrayboxAssets.Part(stakes, PrimitiveType.Cube, "Stake", OnGround(x, 6f, 0.5f), new Vector3(0.1f, 1f, 0.1f), GrayboxAssets.Wood);
        }

        static void BuildField(Transform gen)
        {
            var field = new GameObject("Field").transform;
            field.SetParent(gen, false);
            var sandbag = SandbagPrefab();
            var covers = new List<Vector2>(ScoutCovers);
            covers.AddRange(AssaultCovers);
            for (int i = 0; i < covers.Count; i++)
            {
                // 엄폐물은 엄폐 지점보다 플레이어 쪽(-Z)에 둔다
                var c = covers[i];
                if (i % 2 == 0) RockAt(field, c.x, c.y - 1.3f, 1.8f, 1.15f, 1.3f);
                else Place(sandbag, field, new Vector3(c.x, Height(c.x, c.y - 1.2f) - 0.1f, c.y - 1.2f), 0);
            }

            var rng = new System.Random(Seed);
            int placed = 0;
            for (int tries = 0; placed < 45 && tries < 500; tries++)
            {
                float x = (float)(rng.NextDouble() * 90 - 45);
                float z = (float)(rng.NextDouble() * 85 + 10);
                if (Mathf.Abs(x - RoadX) < 4f) continue;
                if (NearMoundPath(x, z)) continue;
                float s = 0.4f + (float)rng.NextDouble() * 0.8f;
                RockAt(field, x, z, s * 1.2f, s * 0.7f, s);
                placed++;
            }
        }

        static bool NearMoundPath(float x, float z)
        {
            for (int i = 0; i < MoundPath.Length - 1; i++)
            {
                var a = MoundPath[i];
                var b = MoundPath[i + 1];
                var p = new Vector2(x, z);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                if (Vector2.Distance(p, a + ab * t) < 3f) return true;
            }
            return false;
        }

        static void RockAt(Transform parent, float x, float z, float sx, float sy, float sz)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.transform.SetParent(parent, false);
            rock.transform.position = OnGround(x, z, sy * 0.25f);
            rock.transform.localScale = new Vector3(sx, sy, sz);
            rock.GetComponent<MeshRenderer>().sharedMaterial = GrayboxAssets.Rock;
            GameObjectUtility.SetStaticEditorFlags(rock, StaticEditorFlags.BatchingStatic);
        }

        // 참호 왼쪽 끝에서 측면 둔덕까지: 어둠에서도 보이는 흰 천 말뚝 + 밝은 흙길 (GDD 7번)
        static void BuildMoundPath(Transform gen)
        {
            var path = new GameObject("MoundPath").transform;
            path.SetParent(gen, false);
            for (int i = 0; i < MoundPath.Length - 1; i++)
            {
                var a = MoundPath[i];
                var b = MoundPath[i + 1];
                float len = Vector2.Distance(a, b);
                int segments = Mathf.CeilToInt(len / 4f);
                var dir = (b - a).normalized;
                var side = new Vector2(dir.y, -dir.x);
                for (int s = 0; s < segments; s++)
                {
                    var p = Vector2.Lerp(a, b, (s + 0.5f) / segments);
                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    GrayboxAssets.Part(path, PrimitiveType.Cube, "PathStrip", OnGround(p.x, p.y, 0.03f), new Vector3(1.4f, 0.04f, len / segments + 0.2f), GrayboxAssets.Dirt, new Vector3(0, yaw, 0));
                    var m = p + side * 1.3f * (s % 2 == 0 ? 1 : -1);
                    GrayboxAssets.Part(path, PrimitiveType.Cube, "PathStake", OnGround(m.x, m.y, 0.45f), new Vector3(0.08f, 0.9f, 0.08f), GrayboxAssets.Wood);
                    GrayboxAssets.Part(path, PrimitiveType.Cube, "PathCloth", OnGround(m.x, m.y, 0.85f), new Vector3(0.18f, 0.12f, 0.04f), GrayboxAssets.Marker);
                }
            }
        }

        static void BuildRoad(Transform gen)
        {
            var road = new GameObject("TankRoad").transform;
            road.SetParent(gen, false);
            for (float z = TankHoldZ - 4; z < 160; z += 4)
                GrayboxAssets.Part(road, PrimitiveType.Cube, "RoadStrip", OnGround(RoadX, z + 2, 0.02f), new Vector3(4.5f, 0.04f, 4.3f), GrayboxAssets.Dirt);
        }

        static void BuildCharacters(Transform gen)
        {
            var people = new GameObject("People").transform;
            people.SetParent(gen, false);
            var rok = RokPrefab();
            Crouched(Place(rok, people, new Vector3(-14f, 0, -0.4f), 0)).name = "Ally_1";
            Crouched(Place(rok, people, new Vector3(11f, 0, -0.4f), 0)).name = "Ally_2";
            Crouched(Place(rok, people, new Vector3(16f, 0, -0.4f), -10)).name = "Ally_3";
            Place(ParkPrefab(), people, new Vector3(-6.5f, 0, -1.2f), 90).name = "ParkYongpal";
            var blanket = Place(BlanketPrefab(), people, new Vector3(-9.5f, 0, -1.5f), 90);
            blanket.name = "BlanketBody";
            blanket.SetActive(false); // S4에서 켠다

            var props = new GameObject("Props").transform;
            props.SetParent(gen, false);
            Place(TargetPrefab(), props, OnGround(4f, 18f), 180).name = "TutorialTarget";
        }

        static LevelPoints BuildPoints(Transform map)
        {
            var pointsGo = SetupUtil.Child(map, "Points");
            var points = SetupUtil.Ensure<LevelPoints>(pointsGo);
            var root = pointsGo.transform;

            points.playerSpawn = Point(root, "PlayerSpawn", new Vector3(0f, 0f, -0.5f), 0);
            points.tankCheckpoint = Point(root, "TankCheckpoint", new Vector3(-18f, 0f, -0.5f), -15);
            points.moundTop = Point(root, "MoundTop", OnGround(-30f, 42f), 60);
            points.enemySpawns = Points(root, "EnemySpawn", EnemySpawns, 180);
            points.scoutCovers = Points(root, "ScoutCover", ScoutCovers, 180);
            points.assaultCovers = Points(root, "AssaultCover", AssaultCovers, 180);
            points.retreatPoints = Points(root, "Retreat", RetreatPoints, 0);
            points.tankStart = Point(root, "TankStart", OnGround(RoadX, TankStartZ), 180);
            points.tankHold = Point(root, "TankHold", OnGround(RoadX, TankHoldZ), 180);
            points.ammoCrates = new[] { Point(root, "AmmoCrate_Trench", new Vector3(5f, 0, -1.6f), 0), Point(root, "AmmoCrate_Left", new Vector3(-17.5f, 0, -1.6f), 0) };
            points.target = Point(root, "Target", OnGround(4f, 18f), 180);
            points.parkSpot = Point(root, "ParkSpot", new Vector3(-6.5f, 0, -1.2f), 90);
            points.blanketSpot = Point(root, "BlanketSpot", new Vector3(-9.5f, 0, -1.5f), 90);
            points.allySpots = new[] { Point(root, "AllySpot_1", new Vector3(-14f, 0, -0.4f), 0), Point(root, "AllySpot_2", new Vector3(11f, 0, -0.4f), 0), Point(root, "AllySpot_3", new Vector3(16f, 0, -0.4f), 0) };
            points.reinforcementsStart = Point(root, "ReinforcementsStart", OnGround(-6f, -40f), 0);
            points.reinforcementsEnd = Point(root, "ReinforcementsEnd", OnGround(-4f, -6f), 0);
            EditorUtility.SetDirty(points);
            return points;
        }

        static Transform Point(Transform root, string name, Vector3 pos, float yaw)
        {
            var t = SetupUtil.Child(root, name).transform;
            t.position = pos;
            t.rotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        static Transform[] Points(Transform root, string prefix, Vector2[] xz, float yaw)
        {
            var result = new Transform[xz.Length];
            for (int i = 0; i < xz.Length; i++)
                result[i] = Point(root, $"{prefix}_{i}", OnGround(xz[i].x, xz[i].y), yaw);
            return result;
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        static void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.13f, 0.15f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.10f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.03f, 0.03f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.08f);
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.skybox = null;

            var moonGo = GameObject.Find("/Moon") ?? GameObject.Find("/Directional Light");
            if (moonGo == null) moonGo = new GameObject("Moon", typeof(Light));
            moonGo.name = "Moon";
            var moon = moonGo.GetComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.70f, 0.92f);
            moon.intensity = 0.5f;
            moon.shadows = LightShadows.Soft;
            moonGo.transform.rotation = Quaternion.Euler(28f, 200f, 0f); // 적 쪽 하늘에서 비춰 적이 실루엣으로 보인다
        }

        // ---- 그레이박스 프리팹 ----
        public static GameObject SandbagPrefab() => GrayboxAssets.Prefab("PF_PR_SandbagWall", () =>
        {
            var root = new GameObject();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(2f, 1f, 0.6f);
            col.center = new Vector3(0, 0.5f, 0);
            GrayboxModels.Model(root, "Props/Sandbag/SM_PR_SandbagWall.fbx", model =>
                GrayboxAssets.Part(model, PrimitiveType.Cube, "Bags", new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 0.6f), GrayboxAssets.Sandbag));
            return root;
        });

        public static GameObject CratePrefab() => GrayboxAssets.Prefab("PF_PR_AmmoCrate", () =>
        {
            var root = new GameObject();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.7f, 0.3f, 0.4f);
            col.center = new Vector3(0, 0.15f, 0);
            GrayboxModels.Model(root, "Props/AmmoCrate/SM_PR_AmmoCrate.fbx", model =>
                GrayboxAssets.Part(model, PrimitiveType.Cube, "Box", new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.3f, 0.4f), GrayboxAssets.CratePale));
            // 어두운 구간에서 찾기 쉽게 하는 흰 띠 (ART_SPEC 3-4 "밝은 나무색 또는 흰 띠") — 최종 모델이 띠를 갖추면 지운다
            GrayboxAssets.Part(root.transform, PrimitiveType.Cube, "Band", new Vector3(0, 0.305f, 0), new Vector3(0.72f, 0.02f, 0.12f), GrayboxAssets.Marker);
            return root;
        });

        public static GameObject TargetPrefab() => GrayboxAssets.Prefab("PF_PR_Target", () =>
        {
            var root = new GameObject();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.5f, 1.2f, 0.1f);
            col.center = new Vector3(0, 1.1f, 0);
            GrayboxModels.Model(root, "Props/Target/SM_PR_Target.fbx", model =>
            {
                GrayboxAssets.Part(model, PrimitiveType.Cube, "Stake", new Vector3(0, 0.6f, 0.04f), new Vector3(0.08f, 1.2f, 0.06f), GrayboxAssets.Wood);
                GrayboxAssets.Part(model, PrimitiveType.Cube, "Board", new Vector3(0, 1.1f, 0), new Vector3(0.5f, 1.2f, 0.05f), GrayboxAssets.Target);
            });
            // 밤에도 찾을 수 있는 흰 표식 (GDD 7번 "어둠에도 구분") — 앞면(+Z)
            GrayboxAssets.Part(root.transform, PrimitiveType.Cube, "Mark", new Vector3(0, 1.35f, 0.03f), new Vector3(0.18f, 0.18f, 0.01f), GrayboxAssets.Marker);
            return root;
        });

        public static GameObject BlanketPrefab() => GrayboxAssets.Prefab("PF_PR_BlanketBody", () =>
        {
            var root = new GameObject();
            GrayboxModels.Model(root, "Props/BlanketBody/SM_PR_BlanketBody.fbx", model =>
            {
                GrayboxAssets.Part(model, PrimitiveType.Capsule, "Blanket", new Vector3(0, 0.17f, 0), new Vector3(0.6f, 0.9f, 0.34f), GrayboxAssets.Blanket, new Vector3(90, 0, 0));
                GrayboxAssets.Part(model, PrimitiveType.Cube, "Boot", new Vector3(0.08f, 0.1f, -0.92f), new Vector3(0.12f, 0.16f, 0.26f), GrayboxAssets.Boot);
            });
            return root;
        });

        public static GameObject RokPrefab() => GrayboxAssets.Prefab("PF_CH_ROK_Soldier", () =>
            Ally(Person(GrayboxAssets.Rok, GrayboxAssets.RokHelmet, 1.70f, false, "Characters/ROK_Soldier/SK_CH_ROK_Soldier.fbx")));

        public static GameObject ParkPrefab() => GrayboxAssets.Prefab("PF_CH_ROK_ParkYongpal", () =>
        {
            var root = Ally(Person(GrayboxAssets.Rok, GrayboxAssets.RokHelmet, 1.72f, false, "Characters/ROK_ParkYongpal/SK_CH_ROK_ParkYongpal.fbx"));
            // 그레이박스 구분: 목의 수건 (ART_SPEC 구분 체크 "국군 병사 ↔ 박용팔") — 최종 모델(스킨 메시)이면 붙이지 않는다
            if (root.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                GrayboxAssets.Part(root.transform, PrimitiveType.Cube, "Towel", new Vector3(0, 1.42f, 0.02f), new Vector3(0.36f, 0.08f, 0.3f), GrayboxAssets.Towel);
            return root;
        });

        // 아군 Animator(AC_ROK) + 시작 자세 컴포넌트. 그레이박스 모델이면 Animator 없이 둔다.
        static GameObject Ally(GameObject root)
        {
            var pose = root.AddComponent<FiveRivers.Narrative.AllyPose>();
            var animator = GrayboxModels.Animate(root.transform.Find("Model"), "AC_ROK");
            if (animator != null) SetupUtil.SetRef(pose, "animator", animator);
            return root;
        }

        // 참호에 놓인 아군은 몸을 낮춰 대기 (A_ROK_Idle_Trench, ANIM_SPEC S1~S4)
        static GameObject Crouched(GameObject ally)
        {
            var so = new SerializedObject(ally.GetComponent<FiveRivers.Narrative.AllyPose>());
            so.FindProperty("crouch").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ally;
        }

        // 키에 맞춘 캡슐 + 머리 위 모양: 국군 = 둥근 철모, 북한군 = 납작한 군모 (ART_SPEC 구분 체크)
        public static GameObject Person(Material body, Material head, float height, bool softCap, string fbx)
        {
            var root = new GameObject();
            var col = root.AddComponent<CapsuleCollider>();
            col.height = height;
            col.radius = 0.3f;
            col.center = new Vector3(0, height / 2f, 0);
            GrayboxModels.Model(root, fbx, model =>
            {
                float bodyHeight = height - 0.2f;
                GrayboxAssets.Part(model, PrimitiveType.Capsule, "Body", new Vector3(0, bodyHeight / 2f, 0), new Vector3(0.55f, bodyHeight / 2f, 0.4f), body);
                if (softCap)
                    GrayboxAssets.Part(model, PrimitiveType.Cube, "Cap", new Vector3(0, height - 0.12f, 0.02f), new Vector3(0.24f, 0.1f, 0.3f), head);
                else
                    GrayboxAssets.Part(model, PrimitiveType.Sphere, "Helmet", new Vector3(0, height - 0.16f, 0), new Vector3(0.34f, 0.22f, 0.36f), head);
            });
            return root;
        }
    }
}
