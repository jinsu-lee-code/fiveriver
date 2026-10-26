using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 그레이박스 FBX (tools/make_graybox_fbx.py, ART_SPEC 파일명 그대로)를 프리팹 Model 자식으로 쓴다.
    // FBX 재질 슬롯은 같은 이름의 재질(Art/Materials/M_*.mat)에 연결한다 → 최종 FBX도 슬롯 이름만 맞으면 그대로 연결된다.
    // FBX가 아직 없으면 기본 도형으로 대신한다.
    static class GrayboxModels
    {
        public const string ModelDir = "Assets/_Project/Art/Models/";
        const string MaterialDir = "Assets/_Project/Art/Materials/";

        // 슬롯 이름 → 그레이박스 색 (어두운 화면에서 구분 우선)
        static readonly Dictionary<string, Color> SlotColors = new Dictionary<string, Color>
        {
            { "M_PR_Sandbag", new Color(0.42f, 0.36f, 0.26f) },
            { "M_PR_AmmoCrate", new Color(0.80f, 0.68f, 0.45f) },
            { "M_PR_Target", new Color(0.78f, 0.74f, 0.62f) },
            { "M_PR_Blanket", new Color(0.30f, 0.32f, 0.22f) },
            { "M_PR_Boot", new Color(0.18f, 0.12f, 0.08f) },
            { "M_CH_ROK_Body", new Color(0.46f, 0.44f, 0.34f) },
            { "M_CH_ROK_Gear", new Color(0.30f, 0.33f, 0.26f) },
            { "M_CH_KPA_Body", new Color(0.62f, 0.24f, 0.18f) },
            { "M_CH_KPA_Gear", new Color(0.42f, 0.38f, 0.22f) },
            { "M_WP_M1_Wood", new Color(0.33f, 0.20f, 0.11f) },
            { "M_WP_M1_Metal", new Color(0.16f, 0.16f, 0.17f) },
            { "M_WP_M1_Clip", new Color(0.20f, 0.20f, 0.21f) },
            { "M_WP_M1_Brass", new Color(0.75f, 0.58f, 0.25f) },
            { "M_WP_Bazooka", new Color(0.26f, 0.29f, 0.18f) },
            { "M_WP_Mosin", new Color(0.33f, 0.20f, 0.11f) },
            { "M_VH_T34_Body", new Color(0.20f, 0.24f, 0.18f) },
            { "M_VH_T34_Track", new Color(0.12f, 0.12f, 0.11f) },
            { "M_VH_T34_Burnt", new Color(0.08f, 0.08f, 0.07f) },
        };

        public static Material SlotMaterial(string slot)
        {
            string path = MaterialDir + slot + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            GrayboxAssets.EnsureFolder(MaterialDir);
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = slot };
            mat.SetColor("_BaseColor", SlotColors.TryGetValue(slot, out var c) ? c : Color.gray);
            mat.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // FBX 가져오기 설정 + 재질 슬롯 연결. 바뀐 게 있을 때만 다시 가져온다.
        public static GameObject LoadFbx(string relPath)
        {
            string path = ModelDir + relPath;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;
            bool dirty = false;
            if (importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                dirty = true;
            }
            if (importer.importCameras || importer.importLights)
            {
                importer.importCameras = false;
                importer.importLights = false;
                dirty = true;
            }
            var map = importer.GetExternalObjectMap();
            var slots = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name)
                .Concat(map.Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)).Distinct().ToList();
            foreach (var slot in slots)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot);
                var target = SlotMaterial(slot);
                if (!map.TryGetValue(id, out var current) || current != target)
                {
                    importer.AddRemap(id, target);
                    dirty = true;
                }
            }
            if (dirty) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static bool Exists(string relPath) => AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + relPath) != null;

        // 프리팹의 Model 자식: FBX가 있으면 그 인스턴스, 없으면 fallback(기본 도형)
        public static Transform Model(GameObject root, string fbxRelPath, Action<Transform> fallback)
        {
            var fbx = Exists(fbxRelPath) ? LoadFbx(fbxRelPath) : null;
            if (fbx == null)
            {
                var model = GrayboxAssets.ModelRoot(root);
                fallback(model);
                return model;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            instance.name = "Model";
            return instance.transform;
        }
    }
}
