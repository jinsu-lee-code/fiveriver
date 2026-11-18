using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 처음부터 있는 전장의 흔적 (2026-11-18, 사용자 지시 "흔적도 남겨서 전쟁의 비참함을 끌어 올려").
    // 며칠째 포격을 맞은 들판: 오래된 포탄 구덩이와 그을린 땅(실행 중 지형 사본에 판다: ShellCraters),
    // 타다 남은 그루터기, 능선 너머에서 피어오르는 연기 기둥. 여러 번 실행해도 결과가 같다(자리는 고정 씨앗).
    // 플레이어 · 적의 길, 엄폐물, 탄약 상자, 전차 · 트럭 길은 피한다.
    public static class BattlefieldTracesSetup
    {
        const string Dir = "Assets/_Project/Art/VFX/Shelling20261118/";
        const string Root = "[BattlefieldTraces]";
        // 포격에 부러진 소나무(이미 맵에 있는 것과 같은 메시)를 그을린 재질로: A = 가슴 높이에서 부러진 줄기, C = 밑동
        static readonly string[] StumpFbx = { "Assets/_Project/Art/Maps/Dabudong1950/ShelledPine/SM_ShelledPine_A.fbx", "Assets/_Project/Art/Maps/Dabudong1950/ShelledPine/SM_ShelledPine_C.fbx" };
        const string SmokeTexture = "Assets/_Project/Art/VFX/Textures/T_VFX_Smoke.png";

        [MenuItem("FiveRivers/Setup/Battlefield Traces")]
        public static void Run() => Debug.Log("[Setup] " + Apply());

        struct Avoid { public Vector2 a, b; public float radius; }

        public static string Apply()
        {
            if (EditorApplication.isPlaying) return "not in play mode";
            if (EditorSceneManager.GetActiveScene().path != DabudongSetup.ScenePath) EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(Root);
            var traces = root.AddComponent<BattlefieldTraces>();
            var terrain = Terrain.activeTerrain;
            var points = Object.FindFirstObjectByType<LevelPoints>();
            var config = AssetDatabase.LoadAssetAtPath<ShellingConfig>(DabudongSetup.DataDir + "ShellingConfig.asset");
            if (terrain == null || points == null || config == null) return "terrain, level points or ShellingConfig missing";
            Physics.SyncTransforms();

            // ---- 피할 곳 ----
            var avoid = new List<Avoid>();
            void Spot(Vector3 p, float r) => avoid.Add(new Avoid { a = new Vector2(p.x, p.z), b = new Vector2(p.x, p.z), radius = r });
            void Lane(Vector3 a, Vector3 b, float r) => avoid.Add(new Avoid { a = new Vector2(a.x, a.z), b = new Vector2(b.x, b.z), radius = r });
            foreach (var c in points.assaultCovers) Spot(c.position, 6f);
            foreach (var c in points.scoutCovers) Spot(c.position, 6f);
            foreach (var c in points.enemySpawns) Spot(c.position, 8f);
            foreach (var c in points.retreatPoints) Spot(c.position, 8f);
            foreach (var c in points.ammoCrates) Spot(c.position, 8f);
            foreach (var c in points.allySpots) Spot(c.position, 9f);
            Spot(points.target.position, 6f);
            Spot(points.playerSpawn.position, 12f);
            Lane(points.tankStart.position, points.tankHold.position, 9f);
            Lane(points.tankHold.position, points.tankHold.position + (points.tankHold.position - points.tankStart.position).normalized * 14f, 9f);
            // 고지 탈환의 길: 오른쪽 참호 끝 → 엄폐 8 → 2 → 9 → 정상 쪽
            var hill = new[] { points.moundTop.position, points.assaultCovers[8].position, points.assaultCovers[2].position, points.assaultCovers[9].position, points.enemySpawns[2].position };
            for (int i = 0; i + 1 < hill.Length; i++) Lane(hill[i], hill[i + 1], 5f);
            // 새벽 트럭 길
            var route = GameObject.Find("DawnTruckRoute");
            if (route != null)
            {
                var stops = route.GetComponentsInChildren<Transform>().Where(t => t != route.transform).Select(t => t.position).ToArray();
                for (int i = 0; i + 1 < stops.Length; i++) Lane(stops[i], stops[i + 1], 9f);
                foreach (var s in stops) Spot(s, 9f);
            }
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (r.name.StartsWith("ShelledPine") || r.transform.parent != null && r.transform.parent.name.StartsWith("ShelledPine")) Spot(r.bounds.center, 3f);
            // 참호
            var trench = new Rect(-19.3f, -3.7f, 40.9f, 4.9f);
            bool any = false; var tb = new Bounds();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var t = r.transform;
                for (int k = 0; k < 4 && t != null; k++, t = t.parent)
                    if (t.name.StartsWith("Trench_Sandbag_Walls")) { if (any) tb.Encapsulate(r.bounds); else { tb = r.bounds; any = true; } break; }
            }
            if (any) trench = new Rect(tb.min.x, tb.min.z, tb.size.x, tb.size.z);

            bool Free(Vector2 p, float extra)
            {
                const float TrenchMargin = 9f;
                if (p.x > trench.xMin - TrenchMargin && p.x < trench.xMax + TrenchMargin && p.y > trench.yMin - TrenchMargin && p.y < trench.yMax + TrenchMargin) return false;
                foreach (var a in avoid)
                {
                    var ab = a.b - a.a;
                    float u = ab.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector2.Dot(p - a.a, ab) / ab.sqrMagnitude) : 0f;
                    if ((a.a + ab * u - p).magnitude < a.radius + extra) return false;
                }
                return true;
            }
            var origin = terrain.transform.position; var size = terrain.terrainData.size;
            bool OnOpenGround(Vector2 p, out Vector3 world, float maxSlope)
            {
                world = default;
                var from = new Vector3(p.x, origin.y + size.y + 5f, p.y);
                if (!Physics.Raycast(from, Vector3.down, out var hit, size.y + 20f, WorldQuery.Solid, QueryTriggerInteraction.Ignore) || !(hit.collider is TerrainCollider)) return false;
                if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope) return false;
                world = hit.point;
                return true;
            }

            // ---- 오래된 구덩이 · 그을린 땅 ----
            var rng = new System.Random(19500818);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var craters = new List<BattlefieldTraces.OldCrater>();
            var placed = new List<Vector3>();
            void Scatter(Rect area, int count, bool scorchOnly)
            {
                int made = 0;
                for (int attempt = 0; attempt < 600 && made < count; attempt++)
                {
                    var p = new Vector2(R(area.xMin, area.xMax), R(area.yMin, area.yMax));
                    float radius = scorchOnly ? R(1.6f, 3f) : R(1.5f, 2.5f);
                    if (!Free(p, radius * 0.5f) || !OnOpenGround(p, out var world, 16f)) continue;
                    if (placed.Any(q => (q - world).magnitude < 8f)) continue;
                    placed.Add(world);
                    craters.Add(new BattlefieldTraces.OldCrater
                    {
                        position = p, radius = radius, seed = R(0f, 100f),
                        depth = scorchOnly ? 0f : R(0.22f, 0.36f), rim = scorchOnly ? 0f : R(0.04f, 0.08f),
                        scorch = scorchOnly ? R(0.45f, 0.7f) : R(0.5f, 0.8f), // weathered: never as black as a fresh one
                    });
                    made++;
                }
            }
            Scatter(config.fieldArea, 11, false);
            Scatter(config.rearArea, 5, false);
            Scatter(config.fieldArea, 4, true);
            Scatter(config.rearArea, 2, true);
            traces.oldCraters = craters.ToArray();

            // ---- 타다 남은 그루터기: 구덩이 곁, 사람이 다니지 않는 가장자리에 ----
            var stumps = new GameObject("Stumps").transform;
            stumps.SetParent(root.transform, false);
            var fbxs = StumpFbx.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(f => f != null).ToArray();
            int stumpCount = 0;
            if (fbxs.Length > 0)
            {
                foreach (var crater in placed.OrderByDescending(c => Mathf.Abs(c.x - 5f) + (c.z < 0f ? 30f : 0f)).Take(8))
                {
                    if (stumpCount >= 5) break;
                    for (int attempt = 0; attempt < 12; attempt++)
                    {
                        float angle = R(0f, Mathf.PI * 2f), distance = R(3f, 4.6f);
                        var p = new Vector2(crater.x + Mathf.Cos(angle) * distance, crater.z + Mathf.Sin(angle) * distance);
                        if (!Free(p, 1.5f) || !OnOpenGround(p, out var world, 20f)) continue;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbxs[stumpCount % fbxs.Length], stumps);
                        go.name = "CharredStump_" + stumpCount;
                        go.transform.SetPositionAndRotation(world - Vector3.up * 0.04f, Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)));
                        go.transform.localScale = Vector3.one * R(0.85f, 1.15f);
                        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(Charred).ToArray();
                        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                        Spot(world, 3f);
                        stumpCount++;
                        break;
                    }
                }
            }

            // ---- 능선 너머의 연기 기둥 ----
            var columns = new GameObject("SmokeColumns").transform;
            columns.SetParent(root.transform, false);
            var smokeMat = SmokeMaterial();
            var centre = origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            // 방위(북에서 시계 방향) · 거리 · 굵기
            var where = new[] { new Vector3(-40f, 300f, 1f), new Vector3(26f, 330f, 0.8f), new Vector3(112f, 285f, 0.9f) };
            for (int i = 0; i < where.Length; i++)
            {
                float bearing = where[i].x * Mathf.Deg2Rad;
                var at = centre + new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing)) * where[i].y + Vector3.up * 22f;
                SmokeColumn(columns, "SmokeColumn_" + i, at, where[i].z, smokeMat, 400 + i * 17);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            return $"traces: old craters {craters.Count(c => c.depth > 0f)}, scorched patches {craters.Count(c => c.depth <= 0f)}, stumps {stumpCount}, smoke columns {where.Length}";
        }

        // 원래 재질의 그을린 판 (같은 폴더에 한 번 만들어 두고 다시 쓴다)
        static Material Charred(Material source)
        {
            if (source == null) return null;
            string path = Dir + "M_ENV_Charred_" + source.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(source); AssetDatabase.CreateAsset(mat, path); }
            mat.CopyPropertiesFromMaterial(source);
            // dark enough to read as burnt, light enough that the bark and the splintered break still show
            var tint = new Color(0.26f, 0.24f, 0.225f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material SmokeMaterial()
        {
            Directory.CreateDirectory(Dir);
            string path = Dir + "M_VFX_DistantSmoke.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Dir + "DistantSmoke.shader");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexture));
            mat.SetColor("_NightColor", new Color(0.035f, 0.058f, 0.115f, 1f));
            mat.SetFloat("_NightStrength", 0.4f);
            mat.SetColor("_DawnColor", new Color(0.3f, 0.3f, 0.33f, 1f));
            mat.SetFloat("_Strength", 0.6f);
            mat.SetFloat("_FootHeight", 25f);
            mat.SetFloat("_FootBand", 45f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // 멀리서 천천히 솟아 바람에 기우는 굵은 연기. 능선 뒤에서 시작하므로 아랫부분은 구릉에 가린다.
        static void SmokeColumn(Transform parent, string name, Vector3 at, float girth, Material material, int seed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 0f;
            var m = ps.main;
            m.loop = true; m.prewarm = true; m.duration = 20f; m.playOnAwake = true;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            // low and leaning: it stands a little above the ridge line, not up among the stars (first tried 200 m tall
            // and sparse: its puffs hung in the night sky like spots on a lens)
            m.startLifetime = new ParticleSystem.MinMaxCurve(48f, 60f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.1f);
            m.startSize = new ParticleSystem.MinMaxCurve(13f * girth, 19f * girth);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 1f, 1f, 1f), new Color(0.7f, 1f, 1f, 1f));
            m.maxParticles = 100;
            m.gravityModifier = 0f;
            ps.useAutoRandomSeed = false;
            ps.randomSeed = (uint)seed;
            var emission = ps.emission; emission.rateOverTime = 1.5f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 4f; shape.radius = 4f * girth;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.4f, 0.08f), new GradientAlphaKey(0.28f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sizeOver = ps.sizeOverLifetime; sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.7f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            // the wind leans it, more the higher it gets
            var force = ps.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World;
            force.x = 0.055f; force.z = -0.018f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.9f; noise.frequency = 0.02f; noise.scrollSpeed = 0.05f;
        }
    }
}
