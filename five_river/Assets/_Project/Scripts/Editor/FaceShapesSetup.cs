using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Data;
using FiveRivers.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 대사 립싱크 블렌드 셰이프 얹기 (부족한 점 1번, 2026-11-26).
    // Blender(source/characters/faceshapes_20261125, run_all.sh)가 만든 정점 이동값 json 을 각 아군 메시의 사본에
    // 위치 기준으로 얹고, 프리팹의 SkinnedMeshRenderer 가 그 사본을 쓰게 하며 FaceRig 를 붙인다.
    // 원본 FBX · .meta 는 건드리지 않는다. 다시 실행하면 사본을 같은 원본에서 다시 만든다(같은 GUID, 같은 결과).
    // 결과 · 짝짓기 수치: Temp/face_shapes_report.txt
    public static class FaceShapesSetup
    {
        const string Chars = "Assets/_Project/Art/Models/Characters/";
        const string Prefabs = "Assets/_Project/Prefabs/";
        public const string ReportPath = "Temp/face_shapes_report.txt";

        struct Target
        {
            public string prefab, json, renderer, baseMesh, baseName, folder;
            public FaceModel model;
        }

        static readonly Target[] Targets =
        {
            // 박용팔: 프리팹은 팔 · 셔츠를 고친 사본(ParkSkinFix)을 쓴다 — 정점 위치는 FBX 와 같다
            new Target { prefab = "PF_CH_ROK_ParkYongpal", json = "faceshapes_ParkYongpal.json", renderer = "Model/Body_Low",
                baseMesh = "Assets/_Project/Art/SurfaceDetail20261110/ROK_ParkYongpal_SkinFix.asset", baseName = "ROK_ParkYongpal_SkinFix",
                folder = Chars + "ROK_ParkYongpal/FaceShapes", model = FaceModel.Park },
            new Target { prefab = "PF_CH_ROK_Soldier", json = "faceshapes_Person1.json", renderer = "Model/Body_Low",
                baseMesh = Chars + "ROK_Soldier/SK_CH_ROK_Soldier_Person1.fbx", baseName = "Body_Low",
                folder = Chars + "ROK_Soldier/FaceShapes", model = FaceModel.Regular },
            new Target { prefab = "PF_CH_ROK_StudentReinforcement", json = "faceshapes_Soldier.json", renderer = "Model/StudentSoldier_Rigged_Mesh",
                baseMesh = Chars + "ROK_Soldier/SK_CH_ROK_Soldier.fbx", baseName = "StudentSoldier_Rigged_Mesh",
                folder = Chars + "ROK_Soldier/FaceShapes", model = FaceModel.Student },
        };

        static string JsonDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "source", "characters", "faceshapes_20261125"));

        [MenuItem("FiveRivers/Setup/Face Shapes")]
        public static void Run() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[FaceShapes] 편집 모드에서만 실행"); return "play mode"; }
            var report = new StringBuilder();
            var config = EnsureConfig();
            var built = new Dictionary<string, Mesh>();
            foreach (var target in Targets)
            {
                try
                {
                    var mesh = BuildMesh(target, report);
                    if (mesh == null) continue;
                    built[target.prefab] = mesh;
                    WirePrefab(target, mesh, config, report);
                }
                catch (Exception e)
                {
                    report.AppendLine($"[{target.prefab}] 실패: {e.Message}");
                    Debug.LogException(e);
                }
            }
            AssetDatabase.SaveAssets();
            RevertSceneMeshOverrides(built, report);
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("[FaceShapes]\n" + report);
            return $"face shapes: {built.Count}/{Targets.Length} → {ReportPath}";
        }

        static FaceConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<FaceConfig>(FaceConfig.AssetPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<FaceConfig>();
            AssetDatabase.CreateAsset(config, FaceConfig.AssetPath);
            return config;
        }

        // ---- 메시 사본 ------------------------------------------------------------------------------------------------
        static Mesh BuildMesh(Target target, StringBuilder report)
        {
            var source = AssetDatabase.LoadAllAssetsAtPath(target.baseMesh).OfType<Mesh>().FirstOrDefault(m => m.name == target.baseName);
            if (source == null) { report.AppendLine($"[{target.prefab}] 원본 메시 없음 {target.baseMesh}:{target.baseName}"); return null; }
            string jsonPath = Path.Combine(JsonDir, target.json);
            if (!File.Exists(jsonPath)) { report.AppendLine($"[{target.prefab}] json 없음 {jsonPath}"); return null; }
            var data = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(jsonPath));
            var shapes = (Dictionary<string, object>)data["shapes"];

            var positions = source.vertices;
            var map = AxisMap(source.bounds, (Dictionary<string, object>)data["local_bbox"], report, target.prefab);
            report.AppendLine($"[{target.prefab}] 원본 {target.baseMesh}:{source.name} 정점 {positions.Length} (FBX 정점 {Num(data, "vertex_count_fbx")}, 위치가 다른 Unity 정점 {DistinctPositions(positions)})");

            var grid = new PositionGrid(positions, map.cell);
            var mesh = Object.Instantiate(source);
            mesh.name = source.name; // 이름으로 메시를 확인하는 코드가 있다 (ParkCombatFootPlacement 등)

            var baseNormals = RecalculatedNormals(source, positions);
            foreach (var pair in shapes)
            {
                if (mesh.GetBlendShapeIndex(pair.Key) >= 0) { report.AppendLine($"   {pair.Key}: 이미 있음 — 건너뜀"); continue; }
                var entries = (List<object>)pair.Value;
                var delta = new Vector3[positions.Length];
                var owner = new int[positions.Length];
                int matched = 0, unmatched = 0, assigned = 0, conflicts = 0, most = 0;
                float worst = 0f;
                for (int e = 0; e < entries.Count; e++)
                {
                    var row = (List<object>)entries[e];
                    var p = map.Point(D(row[0]), D(row[1]), D(row[2]));
                    var d = map.Delta(D(row[3]), D(row[4]), D(row[5]));
                    var hits = grid.Near(p, map.tolerance, out float nearest);
                    if (hits.Count == 0) { unmatched++; continue; }
                    matched++;
                    worst = Mathf.Max(worst, nearest);
                    most = Mathf.Max(most, hits.Count);
                    foreach (int v in hits)
                    {
                        if (owner[v] != 0 && (delta[v] - d).sqrMagnitude > map.tolerance * map.tolerance) conflicts++;
                        if (owner[v] == 0) assigned++;
                        owner[v] = e + 1;
                        delta[v] = d;
                    }
                }
                var normals = DeltaNormals(source, positions, delta, baseNormals);
                mesh.AddBlendShapeFrame(pair.Key, 100f, delta, normals, null);
                float maxMove = delta.Max(v => v.magnitude);
                report.AppendLine($"   {pair.Key}: json 정점 {entries.Count} → 짝 {matched} · 못 찾음 {unmatched} · Unity 정점 {assigned} (한 위치 최대 {most}개) · 다른 이동값 충돌 {conflicts} · 최대 거리 {worst:E1} · 최대 이동 {maxMove * 1000f:F3} mm(로컬)");
                if (unmatched > 0) Debug.LogWarning($"[FaceShapes] {target.prefab} {pair.Key}: {unmatched} json 정점의 짝을 찾지 못함");
            }

            Directory.CreateDirectory(target.folder);
            string path = $"{target.folder}/{source.name}_Face.asset";
            var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (previous == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, previous);
                Object.DestroyImmediate(mesh);
                mesh = previous;
            }
            EditorUtility.SetDirty(mesh);
            report.AppendLine($"   → {path} 블렌드 셰이프 {mesh.blendShapeCount}: {string.Join(", ", Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName))}");
            return mesh;
        }

        struct Map
        {
            public Vector3 sign;
            public float scale, tolerance, cell;
            public Vector3 Point(double x, double y, double z) => new Vector3((float)(sign.x * scale * x), (float)(sign.y * scale * y), (float)(sign.z * scale * z));
            public Vector3 Delta(double x, double y, double z) => Point(x, y, z);
        }

        // json local_bbox(FBX 메시 로컬)와 Unity 메시 경계를 비교해 축 부호 · 배율을 정한다
        static Map AxisMap(Bounds unity, Dictionary<string, object> bbox, StringBuilder report, string label)
        {
            var min = (List<object>)bbox["min"];
            var max = (List<object>)bbox["max"];
            var best = new Map { scale = 1f };
            float bestError = float.MaxValue;
            foreach (float scale in new[] { 1f, 0.01f })
            {
                var sign = Vector3.one;
                float error = 0f;
                for (int a = 0; a < 3; a++)
                {
                    float lo = (float)D(min[a]) * scale, hi = (float)D(max[a]) * scale;
                    float plus = Mathf.Abs(lo - unity.min[a]) + Mathf.Abs(hi - unity.max[a]);
                    float minus = Mathf.Abs(-hi - unity.min[a]) + Mathf.Abs(-lo - unity.max[a]);
                    sign[a] = plus <= minus ? 1f : -1f;
                    error += Mathf.Min(plus, minus);
                }
                if (error < bestError) { bestError = error; best = new Map { sign = sign, scale = scale }; }
            }
            float extent = unity.size.magnitude;
            if (bestError > extent * 0.01f) throw new InvalidDataException($"경계가 맞지 않음 (오차 {bestError} / 크기 {extent})");
            // 1e-5 로컬 단위 = 약 0.016 mm (json match_rule)
            best.tolerance = 1e-5f * best.scale;
            best.cell = best.tolerance * 2f;
            report.AppendLine($"[{label}] 축: unity = ({best.sign.x:+0;-0}x, {best.sign.y:+0;-0}y, {best.sign.z:+0;-0}z) × {best.scale} · 경계 오차 {bestError:E2} (크기 {extent:F5}) · 짝 허용 {best.tolerance:E1}");
            return best;
        }

        // 이동값으로 바뀐 노멀 = 이동 후 다시 계산한 노멀 - 원래 위치에서 다시 계산한 노멀 (가져온 노멀에 더해진다)
        static Vector3[] RecalculatedNormals(Mesh source, Vector3[] positions)
        {
            var temp = new Mesh { indexFormat = source.indexFormat };
            temp.vertices = positions;
            temp.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) temp.SetTriangles(source.GetTriangles(s), s, false);
            temp.RecalculateNormals();
            var normals = temp.normals;
            Object.DestroyImmediate(temp);
            return normals;
        }

        static Vector3[] DeltaNormals(Mesh source, Vector3[] positions, Vector3[] delta, Vector3[] baseNormals)
        {
            var moved = new Vector3[positions.Length];
            for (int i = 0; i < moved.Length; i++) moved[i] = positions[i] + delta[i];
            var after = RecalculatedNormals(source, moved);
            var result = new Vector3[positions.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var d = after[i] - baseNormals[i];
                if (d.sqrMagnitude > 1e-8f) result[i] = d;
            }
            return result;
        }

        static int DistinctPositions(Vector3[] positions) => new HashSet<Vector3>(positions).Count;

        sealed class PositionGrid
        {
            readonly Dictionary<(int, int, int), List<int>> cells = new Dictionary<(int, int, int), List<int>>();
            readonly Vector3[] positions;
            readonly float cell;
            readonly List<int> hits = new List<int>();

            public PositionGrid(Vector3[] positions, float cell)
            {
                this.positions = positions;
                this.cell = cell;
                for (int i = 0; i < positions.Length; i++)
                {
                    var key = Key(positions[i]);
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>(2);
                    list.Add(i);
                }
            }

            (int, int, int) Key(Vector3 p) => (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));

            public List<int> Near(Vector3 p, float tolerance, out float nearest)
            {
                hits.Clear();
                nearest = float.MaxValue;
                var k = Key(p);
                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!cells.TryGetValue((k.Item1 + x, k.Item2 + y, k.Item3 + z), out var list)) continue;
                    foreach (int i in list)
                    {
                        float d = (positions[i] - p).magnitude;
                        if (d <= tolerance) { hits.Add(i); nearest = Mathf.Min(nearest, d); }
                    }
                }
                return hits;
            }
        }

        // ---- 프리팹 · 씬 연결 ------------------------------------------------------------------------------------------
        static void WirePrefab(Target target, Mesh mesh, FaceConfig config, StringBuilder report)
        {
            string path = Prefabs + target.prefab + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var node = root.transform.Find(target.renderer);
                var renderer = node != null ? node.GetComponent<SkinnedMeshRenderer>() : null;
                if (renderer == null) { report.AppendLine($"[{target.prefab}] 렌더러 {target.renderer} 없음"); return; }
                renderer.sharedMesh = mesh;
                var rig = root.GetComponent<FaceRig>();
                if (rig == null) rig = root.AddComponent<FaceRig>();
                rig.Configure(config, target.model, renderer);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine($"[{target.prefab}] 프리팹 연결: {target.renderer} → {AssetDatabase.GetAssetPath(mesh)}, FaceRig({target.model})");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // 씬의 인물이 메시를 따로 덮어쓰고 있으면(ParkSkinFix 가 씬에 쓴 것 등) 프리팹 값을 따르게 되돌린다. 바뀐 게 있을 때만 씬 저장
        static void RevertSceneMeshOverrides(Dictionary<string, Mesh> built, StringBuilder report)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) { report.AppendLine("씬 확인 건너뜀 (Dabudong 이 열려 있지 않음)"); return; }
            int reverted = 0;
            foreach (var renderer in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
                if (instanceRoot == null) continue;
                var asset = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                if (asset == null || !built.TryGetValue(asset.name, out var mesh)) continue;
                if (renderer.sharedMesh == mesh) continue;
                var property = new SerializedObject(renderer).FindProperty("m_Mesh");
                if (property == null || !property.prefabOverride) continue;
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                reverted++;
                report.AppendLine($"씬 {instanceRoot.name}/{renderer.name}: 메시 덮어쓰기 되돌림 → {(renderer.sharedMesh != null ? renderer.sharedMesh.name : "null")}");
            }
            if (reverted > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            report.AppendLine($"씬 메시 덮어쓰기 되돌림 {reverted}개{(reverted > 0 ? " (씬 저장)" : " (씬 저장 안 함)")}");
        }

        static double D(object o) => Convert.ToDouble(o, CultureInfo.InvariantCulture);
        static string Num(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "?";

        // ---- 작은 JSON 읽기 (Blender 내보내기 형식만: 객체 · 배열 · 수 · 문자열 · true/false/null) -------------------------
        static class MiniJson
        {
            public static object Parse(string text)
            {
                int i = 0;
                return Value(text, ref i);
            }

            static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var o = new Dictionary<string, object>();
                    i++;
                    Skip(s, ref i);
                    if (s[i] == '}') { i++; return o; }
                    while (true)
                    {
                        Skip(s, ref i);
                        string key = Str(s, ref i);
                        Skip(s, ref i);
                        i++; // :
                        o[key] = Value(s, ref i);
                        Skip(s, ref i);
                        if (s[i++] == '}') return o;
                    }
                }
                if (c == '[')
                {
                    var a = new List<object>();
                    i++;
                    Skip(s, ref i);
                    if (s[i] == ']') { i++; return a; }
                    while (true)
                    {
                        a.Add(Value(s, ref i));
                        Skip(s, ref i);
                        if (s[i++] == ']') return a;
                    }
                }
                if (c == '"') return Str(s, ref i);
                if (s.Length >= i + 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (s.Length >= i + 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (s.Length >= i + 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                if (s.Length >= i + 3 && string.CompareOrdinal(s, i, "NaN", 0, 3) == 0) { i += 3; return double.NaN; }
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++; // "
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++;
                        char e = s[i];
                        if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                        else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e == 'b' ? '\b' : e == 'f' ? '\f' : e);
                    }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }
        }
    }
}
