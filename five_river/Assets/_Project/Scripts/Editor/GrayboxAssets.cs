using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 그레이박스 재질 · 프리팹. 이미 있으면 그대로 쓴다 (손으로 고친 것 · 교체한 모델 보존).
    // 프리팹(PF_)이 코드 · 콜라이더 · 소켓을 갖고, 눈에 보이는 모양은 "Model" 자식에 둔다.
    // 최종 모델이 오면 Model 자식만 FBX로 바꾼다 (ART_SPEC 1-1).
    static class GrayboxAssets
    {
        public const string MaterialDir = "Assets/_Project/Art/Materials/Graybox/";
        public const string PrefabDir = "Assets/_Project/Prefabs/";

        static Shader litShader;

        public static Material Mat(string name, Color color, float emission = 0f)
        {
            string path = MaterialDir + "M_Graybox_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            EnsureFolder(MaterialDir);
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(litShader) { name = "M_Graybox_" + name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.15f);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // true면 이미 있는 프리팹도 다시 만든다 (같은 경로에 덮어써서 GUID 유지). FiveRivers/Setup/Rebuild Graybox Prefabs
        public static bool Rebuild;
        static readonly System.Collections.Generic.HashSet<string> rebuiltThisRun = new System.Collections.Generic.HashSet<string>();

        public static GameObject Prefab(string name, Func<GameObject> build)
        {
            string path = PrefabDir + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && (!Rebuild || rebuiltThisRun.Contains(path))) return existing;
            rebuiltThisRun.Add(path);
            EnsureFolder(PrefabDir);
            var go = build();
            go.name = name;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[Setup] 그레이박스 프리팹 " + (existing != null ? "다시 만듦: " : "생성: ") + path);
            return prefab;
        }

        public static void BeginRebuild()
        {
            Rebuild = true;
            rebuiltThisRun.Clear();
        }

        public static void EndRebuild() => Rebuild = false;

        // 콜라이더 없는 보이는 조각
        public static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static Transform ModelRoot(GameObject root)
        {
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            return model.transform;
        }

        public static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        // ---- 공용 색 (어두운 화면에서 구분되는 것 우선) ----
        public static Material Ground => Mat("Ground", new Color(0.16f, 0.19f, 0.13f));
        public static Material Dirt => Mat("Dirt", new Color(0.30f, 0.26f, 0.20f));
        public static Material Sandbag => Mat("Sandbag", new Color(0.42f, 0.36f, 0.26f));
        public static Material Rock => Mat("Rock", new Color(0.30f, 0.30f, 0.29f));
        public static Material Wood => Mat("Wood", new Color(0.36f, 0.27f, 0.18f));
        public static Material CratePale => Mat("CratePale", new Color(0.80f, 0.68f, 0.45f));
        public static Material Marker => Mat("Marker", new Color(0.85f, 0.85f, 0.80f), 0.35f);
        public static Material Rok => Mat("ROK", new Color(0.46f, 0.44f, 0.34f));
        public static Material RokHelmet => Mat("ROKHelmet", new Color(0.30f, 0.33f, 0.26f));
        public static Material Kpa => Mat("KPA", new Color(0.62f, 0.24f, 0.18f));
        public static Material Blanket => Mat("Blanket", new Color(0.30f, 0.32f, 0.22f));
        public static Material Boot => Mat("Boot", new Color(0.18f, 0.12f, 0.08f));
        public static Material Towel => Mat("Towel", new Color(0.82f, 0.80f, 0.72f));
        public static Material Tank => Mat("Tank", new Color(0.20f, 0.24f, 0.18f));
        public static Material Track => Mat("Track", new Color(0.12f, 0.12f, 0.11f));
        public static Material Target => Mat("Target", new Color(0.78f, 0.74f, 0.62f));
        public static Material Metal => Mat("Metal", new Color(0.16f, 0.16f, 0.17f));
        public static Material RifleWood => Mat("RifleWood", new Color(0.33f, 0.20f, 0.11f));
        public static Material Olive => Mat("Olive", new Color(0.26f, 0.29f, 0.18f));
        public static Material FxYellow => Mat("FxYellow", new Color(1f, 0.8f, 0.4f), 4f);
        public static Material FxOrange => Mat("FxOrange", new Color(1f, 0.45f, 0.12f), 3f);
        public static Material FxWhite => Mat("FxWhite", new Color(0.85f, 0.85f, 0.82f), 1.5f);
        public static Material FxBrown => Mat("FxBrown", new Color(0.35f, 0.27f, 0.18f));
        public static Material FxSmoke => Mat("FxSmoke", new Color(0.18f, 0.18f, 0.18f));

        // 프리팹에 컴포넌트가 없으면 붙인다 (이미 있는 프리팹에 새 기능을 연결할 때)
        public static void EnsureComponent<T>(GameObject prefabAsset) where T : Component
        {
            if (prefabAsset.GetComponent<T>() != null) return;
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            var root = PrefabUtility.LoadPrefabContents(path);
            root.AddComponent<T>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
