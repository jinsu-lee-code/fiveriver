using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    // 전투 흔적 데칼 (부족한 점 5번, 2026-11-26): 흔적 텍스처 3종(같은 씨앗이라 늘 같은 그림) · 데칼 재질 · PC 렌더러의 URP Decal 기능
    // (화면 공간 방식 — 가볍고 MSAA 없이 동작) · [Systems] 의 BattleDecals 연결 · 수치 에셋. 여러 번 실행해도 결과가 같다.
    // 되돌리기: Revert 메뉴(렌더러에서 Decal 기능 제거, BattleDecals 끔).
    public static class BattleDecalsSetup
    {
        const string Folder = "Assets/_Project/Art/Decals";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        [MenuItem("FiveRivers/Setup/Battle Decals")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "edit mode only";
            var sb = new StringBuilder();
            Directory.CreateDirectory(Folder);
            var bullet = MaterialFor("BulletHole", BulletHole());
            var scorch = MaterialFor("Scorch", Scorch());
            var blood = MaterialFor("BloodSoak", BloodSoak());
            sb.AppendLine("materials " + string.Join(", ", new[] { bullet, scorch, blood }.Select(m => AssetDatabase.GetAssetPath(m))));
            sb.AppendLine(EnsureFeature(true));

            var config = AssetDatabase.LoadAssetAtPath<BattleTraceConfig>(BattleTraceConfig.AssetPath);
            if (config == null) { config = ScriptableObject.CreateInstance<BattleTraceConfig>(); AssetDatabase.CreateAsset(config, BattleTraceConfig.AssetPath); }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) scene = EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var effects = Object.FindFirstObjectByType<Effects>(FindObjectsInactive.Include);
            if (effects == null) return sb.AppendLine("no Effects in the scene").ToString();
            var decals = effects.GetComponent<BattleDecals>() ?? effects.gameObject.AddComponent<BattleDecals>();
            decals.Configure(config, effects, bullet, scorch, blood);
            decals.enabled = true;
            EditorUtility.SetDirty(decals);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            sb.AppendLine($"BattleDecals on {effects.name}: bullets {config.bulletMost}, scorches {config.scorchMost}, blood {config.bloodMost}");
            return sb.ToString();
        }

        [MenuItem("FiveRivers/Setup/Battle Decals Revert")]
        public static string Revert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "edit mode only";
            var log = EnsureFeature(false);
            var effects = Object.FindFirstObjectByType<Effects>(FindObjectsInactive.Include);
            var decals = effects != null ? effects.GetComponent<BattleDecals>() : null;
            if (decals != null)
            {
                Object.DestroyImmediate(decals);
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            return log + " | BattleDecals removed (textures and materials left in " + Folder + ")";
        }

        // ---- the renderer feature ----
        static string EnsureFeature(bool on)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var existing = data.rendererFeatures.FirstOrDefault(f => f is DecalRendererFeature);
            if (!on)
            {
                if (existing == null) return "no Decal feature";
                data.rendererFeatures.Remove(existing);
                var so0 = new SerializedObject(data); var map0 = so0.FindProperty("m_RendererFeatureMap");
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(existing, out string _, out long id0))
                    for (int i = map0.arraySize - 1; i >= 0; i--) if (map0.GetArrayElementAtIndex(i).longValue == id0) map0.DeleteArrayElementAtIndex(i);
                so0.ApplyModifiedPropertiesWithoutUndo();
                Object.DestroyImmediate(existing, true);
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
                return "Decal feature removed";
            }
            var feature = existing as DecalRendererFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                feature.name = "Decal";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                AssetDatabase.SaveAssets();
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long id))
                {
                    var so = new SerializedObject(data); var map = so.FindProperty("m_RendererFeatureMap");
                    map.InsertArrayElementAtIndex(map.arraySize); map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            var fs = new SerializedObject(feature);
            var technique = fs.FindProperty("m_Settings.technique");
            if (technique != null) technique.enumValueIndex = 2; // ScreenSpace: no extra depth-normals target, light enough for the assault
            var maxDraw = fs.FindProperty("m_Settings.maxDrawDistance"); if (maxDraw != null) maxDraw.floatValue = 80f;
            var normalBlend = fs.FindProperty("m_Settings.screenSpaceSettings.normalBlend"); if (normalBlend != null) normalBlend.enumValueIndex = 0;
            fs.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
            // the pipeline keeps its built renderer: tell it to build it again with the new feature (without this the
            // decals stayed invisible for the rest of the editor session, 2026-11-26)
            typeof(ScriptableRendererData).GetMethod("SetDirty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, System.Type.EmptyTypes, null)?.Invoke(data, null);
            return $"Decal feature on {RendererPath} (technique {(technique != null ? technique.enumDisplayNames[technique.enumValueIndex] : "?")})";
        }

        // ---- textures (128 px, a fixed noise) and materials ----
        const int N = 128;

        static float Noise(float x, float y, int seed) => Mathf.PerlinNoise(x * 0.09f + seed * 17.3f, y * 0.09f + seed * 5.1f);

        static Color32[] BulletHole()
        {
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x - N / 2f) / (N / 2f), dy = (y - N / 2f) / (N / 2f), d = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Noise(x * 2.2f, y * 2.2f, 1);
                float edge = d * (0.85f + 0.3f * n);
                float a; Color c;
                // 2026-11-26 2nd: a lighter ring of thrown sand did not read at night (moonlit earth is the same tone);
                // freshly torn-up earth is darker than the dry crust, and a dark mark is what the eye picks out at night
                if (edge < 0.3f) { a = 0.97f; c = new Color(0.02f, 0.017f, 0.015f); }                          // the hole
                else if (edge < 0.7f) { a = 0.8f * (1f - (edge - 0.3f) / 0.4f) * (0.55f + 0.45f * n); c = new Color(0.07f, 0.055f, 0.042f); } // torn-up earth round it
                else { a = Noise(x * 5f, y * 5f, 3) > 0.72f && edge < 0.98f ? 0.5f : 0f; c = new Color(0.09f, 0.075f, 0.055f); } // specks thrown out
                px[y * N + x] = new Color(c.r, c.g, c.b, a);
            }
            return px;
        }

        static Color32[] Scorch()
        {
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x - N / 2f) / (N / 2f), dy = (y - N / 2f) / (N / 2f), d = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Noise(x, y, 7), m = Noise(x * 3f, y * 3f, 9);
                float edge = d * (0.8f + 0.45f * n);
                float a = Mathf.Clamp01(1f - Mathf.Pow(edge, 1.7f)) * (0.55f + 0.45f * m);
                if (edge > 1f) a = 0f;
                float dark = Mathf.Lerp(0.02f, 0.05f, edge);
                px[y * N + x] = new Color(dark, dark * 0.88f, dark * 0.75f, a);
            }
            return px;
        }

        static Color32[] BloodSoak()
        {
            var px = new Color32[N * N];
            var blobs = new[] { new Vector3(0f, 0f, 0.55f), new Vector3(0.25f, 0.15f, 0.35f), new Vector3(-0.2f, -0.25f, 0.3f), new Vector3(0.1f, -0.35f, 0.22f) };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (x - N / 2f) / (N / 2f), v = (y - N / 2f) / (N / 2f);
                float n = Noise(x * 1.5f, y * 1.5f, 13);
                float inside = 0f;
                foreach (var b in blobs) { float d = Mathf.Sqrt((u - b.x) * (u - b.x) + (v - b.y) * (v - b.y)) / b.z; inside = Mathf.Max(inside, 1f - d * (0.85f + 0.3f * n)); }
                float a = Mathf.Clamp01(inside * 2.2f) * (0.6f + 0.4f * Noise(x * 4f, y * 4f, 17));
                // soaked into dark earth: a dull brown-red, darkest where it pooled
                var c = Color.Lerp(new Color(0.11f, 0.035f, 0.025f), new Color(0.05f, 0.02f, 0.015f), Mathf.Clamp01(inside * 1.5f));
                px[y * N + x] = new Color(c.r, c.g, c.b, a);
            }
            return px;
        }

        static Material MaterialFor(string name, Color32[] pixels)
        {
            string texPath = $"{Folder}/T_Decal_{name}.png", matPath = $"{Folder}/M_Decal_{name}.mat";
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels); tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.sRGBTexture = true;
            imp.SaveAndReimport();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Shader Graphs/Decal")) { name = "M_Decal_" + name }; AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
