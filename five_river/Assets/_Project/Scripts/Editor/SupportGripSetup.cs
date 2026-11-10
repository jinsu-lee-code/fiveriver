using System;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Original VARCO support hand; source mesh, material and texture are unchanged.
    // Rotation-only adjustment: keep the original scale, position and local +Z wrist measurement.
    public static class SupportGripSetup
    {
        const string HandModel = "Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/SM_CH_ROK_SupportHand_M1.fbx";
        const string InstanceName = "SupportHand_M1";
        const string MaterialPath = "Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/M_CH_ROK_SupportHand_M1.mat";
        const string BaseColorPath = "Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/T_CH_ROK_SupportHand_M1_BaseColor.png";
        // Preserve the original support-hand material values.
        static readonly Color SkinTint = Color.white;
        const float HandScale = 0.65f;
        public static readonly Vector3 CuffAxis = Vector3.forward;
        // Rifle space (M1_ShotMotion): x right, y up, z toward the muzzle. The forend wood spans y ~0..0.049 here.
        static readonly Vector3 HandPosition = new Vector3(0f, 0.026f, 0.20f);
        static readonly Vector3 HandEuler = new Vector3(-14f, -5f, -30f);
        // Original sleeve endpoint seed; the original local +Z wrist measurement follows.
        static readonly Vector3 WristInHand = new Vector3(0f, -0.09f, 0f);

        [MenuItem("FiveRivers/Setup/Natural Support Grip")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new InvalidOperationException("Open the Dabudong gameplay scene.");
            var grip = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Grip_Left");
            if (grip == null) throw new InvalidOperationException("Grip_Left is missing; run Apply Refined M1 first.");
            Set(grip);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            Debug.Log("[SupportGrip] Varco support hand placed on the M1 forend.");
        }

        // Absolute values: running it again gives the same result.
        public static void Set(Transform grip)
        {
            var rifleSpace = grip.parent;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandModel);
            if (prefab == null) throw new InvalidOperationException("Support hand model is missing: " + HandModel);
            var hand = rifleSpace.Find(InstanceName);
            if (hand == null)
            {
                hand = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, rifleSpace)).transform;
                hand.name = InstanceName;
            }
            // Remove any previous candidate mesh override from the existing scene instance.
            // Restore the original FBX mesh before applying the selected rotation.
            var sourceMesh = prefab.GetComponentInChildren<MeshFilter>(true);
            var visualMesh = hand.GetComponentInChildren<MeshFilter>(true);
            if (sourceMesh == null || visualMesh == null) throw new InvalidOperationException("Support hand mesh is missing.");
            visualMesh.sharedMesh = sourceMesh.sharedMesh;
            EditorUtility.SetDirty(visualMesh);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visualMesh);
            hand.SetLocalPositionAndRotation(HandPosition, Quaternion.Euler(HandEuler));
            hand.localScale = Vector3.one * HandScale;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_CH_ROK_SupportHand_M1" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath));
            material.SetColor("_BaseColor", SkinTint);
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            foreach (var t in hand.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            foreach (var r in hand.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
            // The skeletal left hand stays for the arm rig but is collapsed out of sight.
            var wrist = rifleSpace.GetComponentsInChildren<Transform>(true).First(t => t.name == "wrist.L");
            wrist.localScale = Vector3.one * 0.0001f;
            // The model is cut at the wrist; the left cloth sleeve covers the joint like the right arm.
            var leftSleeve = rifleSpace.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Left_ClothCuff");
            if (leftSleeve != null && leftSleeve.TryGetComponent<Renderer>(out var sleeveRenderer)) sleeveRenderer.enabled = true;
            grip.SetPositionAndRotation(hand.TransformPoint(WristInHand), hand.rotation);
            var armRig = rifleSpace.GetComponentInChildren<M1ArmRig>(true);
            if (armRig != null)
            {
                // A newly generated rig still needs both original cuff fits.
                // Updating an existing support hand leaves its normal right-hand mesh intact.
                var rigData = new SerializedObject(armRig);
                if (rigData.FindProperty("rightCuffRim").arraySize == 32) M1WristCuffSetup.ConfigureSupport(armRig);
                else M1WristCuffSetup.Configure(armRig);
            }
        }

        public static void ApplyBatch()
        {
            EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            Apply();
            EditorApplication.Exit(0);
        }
    }
}
