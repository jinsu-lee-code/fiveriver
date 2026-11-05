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
    // 받침손(왼손) 파지 (2026-10-30 사용자 지시: 파지법 사진대로 바르코3D로 손을 새로 만들어 Blender에서 다듬어 적용).
    // 근거: M1 입사(offhand) 사진과 FM 23-5(1951) "총이 엄지와 검지 사이 V에 놓이게, 손은 방아쇠울 바로 앞".
    // 모델: 바르코3D 이미지→3D(흐름 "다섯개의강"), Blender `Docs/Art/M1FPS_20261030/SupportHandVarco/hand_prep.py`로
    //       쥔 손 안쪽 통로 중심을 원점, 통로 축을 +Z, 손목을 -Y로 맞추고 통로 반지름을 총몸 반폭(.027 m)에 맞춤.
    // 적용: 기존 골격 왼손은 숨기고(손목 뼈 크기 0), 새 손을 총(M1_ShotMotion)에 고정, 소매 끝(Grip_Left)을 새 손목에 맞춘다.
    public static class SupportGripSetup
    {
        const string HandModel = "Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/SM_CH_ROK_SupportHand_M1.fbx";
        const string InstanceName = "SupportHand_M1";
        const string MaterialPath = "Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/M_CH_ROK_SupportHand_M1.mat";
        const string BaseColorPath = "Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/T_CH_ROK_SupportHand_M1_BaseColor.png";
        // The Varco base colour is used as is.
        static readonly Color SkinTint = Color.white;
        const float HandScale = 0.65f;
        // Rifle space (M1_ShotMotion): x right, y up, z toward the muzzle. The forend wood spans y ~0..0.049 here.
        static readonly Vector3 HandPosition = new Vector3(0f, 0.026f, 0.20f);
        static readonly Vector3 HandEuler = new Vector3(0f, 0f, -30f);
        // Sleeve end just inside the wrist cut (model cut at -0.11 by hand_cut_wrist.py; wrist toward -Y).
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
            if (armRig != null) M1WristCuffSetup.Configure(armRig);
        }

        public static void ApplyBatch()
        {
            EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            Apply();
            EditorApplication.Exit(0);
        }
    }
}
