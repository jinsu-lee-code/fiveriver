using System.IO;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-05 VARCO T-34-85 (Docs/Art/T34_Varco432_20261105). 여러 번 실행해도 결과가 같다 (편집 모드에서만).
    // 1) 포구 소켓을 새 포신 끝에 맞춘다  2) 파괴 모델을 전차 프리팹 안에 꺼진 채로 넣는다
    // 3) 맵 배치용 파괴 전차 프리팹을 만든다  4) 열린 씬의 TankController에 교체 대상 두 개를 연결한다 (씬 저장은 사람이)
    public static class TankVarcoSetup
    {
        const string TankPrefab = "Assets/_Project/Prefabs/PF_VH_T34_85.prefab";
        const string DestroyedFbx = "Assets/_Project/Art/Models/Vehicles/T34_85_Destroyed/SM_VH_T34_85_Destroyed.fbx";
        const string DestroyedPrefab = "Assets/_Project/Prefabs/PF_VH_T34_85_Destroyed.prefab";

        [MenuItem("FiveRivers/Setup/T34 Varco432 (Muzzle + Destroyed)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[T34] 플레이 모드를 끄고 실행"); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DestroyedFbx) != null) RemapDestroyedMaterial();
            var destroyed = AssetDatabase.LoadAssetAtPath<GameObject>(DestroyedFbx);
            var root = PrefabUtility.LoadPrefabContents(TankPrefab);
            try
            {
                FitMuzzle(root.transform);
                var old = root.transform.Find("DestroyedModel");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                if (destroyed != null)
                {
                    var d = (GameObject)PrefabUtility.InstantiatePrefab(destroyed, root.transform);
                    d.name = "DestroyedModel";
                    d.transform.localPosition = Vector3.zero; // Preserve the imported FBX axis conversion.
                    d.SetActive(false);
                }
                PrefabUtility.SaveAsPrefabAsset(root, TankPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            if (destroyed != null) BuildDestroyedPrefab(destroyed);
            WireScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[T34] 포구 소켓 · 파괴 모델 " + (destroyed != null ? "연결 완료" : "없음 (FBX 대기)") + ". 씬을 저장하세요.");
        }

        // 포신 메시에서 포탑 정면(+Z) 쪽으로 가장 먼 정점들의 중심 = 포구.
        static void FitMuzzle(Transform root)
        {
            var gun = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Gun");
            var socket = gun != null ? gun.Find("Socket_Muzzle") : null;
            var mf = gun != null ? gun.GetComponent<MeshFilter>() : null;
            if (socket == null || mf == null || mf.sharedMesh == null) { Debug.LogWarning("[T34] Gun/Socket_Muzzle 없음"); return; }
            var fwd = gun.InverseTransformDirection(root.forward).normalized;
            // Imported FBX transforms can contain unit scaling; select the last 3 cm in world space.
            var v = mf.sharedMesh.vertices.Select(p => gun.TransformPoint(p)).ToArray();
            float max = v.Max(p => Vector3.Dot(p, root.forward));
            var end = v.Where(p => Vector3.Dot(p, root.forward) > max - 0.03f).ToArray();
            var tip = end.Aggregate(Vector3.zero, (a, p) => a + p) / end.Length;
            socket.position = tip;
            socket.localRotation = Quaternion.LookRotation(fwd, gun.InverseTransformDirection(root.up));
        }

        // FBX의 M_VH_T34_Destroyed 슬롯을 URP Lit 재질(녹 · 그을음 텍스처)로 연결한다.
        static void RemapDestroyedMaterial()
        {
            const string dir = "Assets/_Project/Art/Models/Vehicles/T34_85_Destroyed/";
            const string matPath = dir + "M_VH_T34_Destroyed.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var normalPath = dir + "Textures/T_VH_T34_Destroyed_Normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ni && ni.textureType != TextureImporterType.NormalMap)
            { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Textures/T_VH_T34_Destroyed_BaseColor.png"));
            mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "Textures/T_VH_T34_Destroyed_MetallicSmoothness.png"));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(mat);
            var importer = (ModelImporter)AssetImporter.GetAtPath(DestroyedFbx);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "M_VH_T34_Destroyed"), mat);
            importer.SaveAndReimport();
        }

        static void BuildDestroyedPrefab(GameObject fbx)
        {
            var root = new GameObject("PF_VH_T34_85_Destroyed");
            try
            {
                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(3.0f, 2.4f, 6.2f); col.center = new Vector3(0, 1.2f, 0);
                var m = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                m.name = "Model";
                Directory.CreateDirectory(Path.GetDirectoryName(DestroyedPrefab));
                PrefabUtility.SaveAsPrefabAsset(root, DestroyedPrefab);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void WireScene()
        {
            var tank = Object.FindFirstObjectByType<TankController>(FindObjectsInactive.Include);
            if (tank == null) return;
            var so = new SerializedObject(tank);
            var body = so.FindProperty("body").objectReferenceValue as Transform;
            if (body == null) return;
            var model = body.Find("Model");
            var destroyed = body.Find("DestroyedModel");
            so.FindProperty("intactModel").objectReferenceValue = model != null ? model.gameObject : null;
            so.FindProperty("destroyedModel").objectReferenceValue = destroyed != null ? destroyed.gameObject : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(tank.gameObject.scene);
        }
    }
}
