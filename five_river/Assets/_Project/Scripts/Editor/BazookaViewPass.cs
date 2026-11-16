using System;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Commercial pass on the first-person M20 (2026-11-16). Idempotent; edit mode only.
    //  - matte olive drab instead of a semi-gloss tube that read as a plastic toy at 2.8x the scene brightness
    //  - rear sight level with the front post, so the aimed tube points where the rocket goes
    //  - hip carry lowered off the crosshair
    //  - backblast effect: no front flame / sparks through the shooter's own view from the rear socket
    public static class BazookaViewPass
    {
        const string LauncherMaterial = "Assets/_Project/Art/Materials/M_WP_Bazooka.mat";
        const string Backblast = "Assets/_Project/Art/VFX/VFX_Bazooka_Backblast.prefab";

        // Smoothness multiplies the delivered map's alpha (0.46-0.58), so 0.48 gives about 0.25: dull paint.
        const float Smoothness = .48f;
        static readonly Color PaintTint = new Color(.68f, .68f, .64f, 1f);

        // Low ready: carried at the hip, nose down 6 degrees, 2 degrees inward, canted 5. Was a level tube at shoulder
        // height (.20, -.09, .36) whose muzzle sat on the crosshair and whose near end filled the right edge (19.8% of
        // the screen); now about 8% (the M1 at the hip is 7.9%). Aiming lifts it to the shoulder.
        static readonly Vector3 HipPosition = new Vector3(.22f, -.24f, .34f);
        static readonly Vector3 HipEuler = new Vector3(6f, -2f, 5f);
        const float ShotLight = .3f; // was .65
        internal static float ArmScale = 1.38f;

        [MenuItem("FiveRivers/Setup/Bazooka View Pass")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) throw new InvalidOperationException("다부동 플레이어를 찾을 수 없습니다.");
            var bazooka = player.GetComponent<Bazooka>();
            var launcher = bazooka.Model.GetComponent<BazookaPresentation>();
            var shot = bazooka.Model.GetComponentsInChildren<Transform>(true).First(t => t.name == "ShotPresentation");

            var data = new SerializedObject(launcher);
            FirstPersonAnimationSetup.FitLauncherSight(shot, data);
            data.FindProperty("hipViewPosition").vector3Value = HipPosition;
            data.FindProperty("hipViewEuler").vector3Value = HipEuler;
            // The shot light (first-person layer only) turned the steel sight ring white for three frames.
            data.FindProperty("flashIntensity").floatValue = ShotLight;
            // A recoilless launcher barely moves; shouldered, keep a third of the fire clip's hip shove.
            data.FindProperty("aimedKickScale").floatValue = .3f;
            data.ApplyModifiedPropertiesWithoutUndo();

            // 2026-11-16 사용자 지시 "손이 너무 가늘어": 바주카를 든 팔 · 손을 키운다. 손목은 손잡이에 붙잡혀 있으므로
            // (BazookaPresentation.FitArm) 전체를 키우면 손이 손잡이를 더 크게 감싸고 팔뚝이 굵어진다.
            var report = new System.Text.StringBuilder();
            foreach (var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                report.AppendLine($"{skin.name} root {skin.transform.root.name} path {AnimationUtility.CalculateTransformPath(skin.transform, player.transform)} lossy {skin.transform.lossyScale} bounds {skin.localBounds.size}");
            var arms = bazooka.Model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Arms");
            if (arms != null)
            {
                report.AppendLine($"bazooka Arms local scale before {arms.localScale}");
                arms.localScale = Vector3.one * ArmScale;
                EditorUtility.SetDirty(arms);
            }
            System.IO.File.WriteAllText("Temp/bz_arms.txt", report.ToString());

            var paint = AssetDatabase.LoadAssetAtPath<Material>(LauncherMaterial);
            paint.SetFloat("_Smoothness", Smoothness);
            paint.SetColor("_BaseColor", PaintTint);
            EditorUtility.SetDirty(paint);

            QuietBackblast();
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        // The effect is spawned at the rear socket facing backwards. Its "FrontFlame" half fired forwards from there,
        // straight through the camera (a white-out and streaks in front of the sight); the presentation already makes
        // the muzzle flash and exhaust at the real muzzle. Sparks that sprayed in every direction now open backwards.
        static void QuietBackblast()
        {
            var root = PrefabUtility.LoadPrefabContents(Backblast);
            try
            {
                // Its flame lights would also burn the near sight ring white from behind; the first-person layer
                // gets the presentation's own shot light instead.
                foreach (var light in root.GetComponentsInChildren<Light>(true)) light.cullingMask &= ~(1 << 31);
                var front = root.transform.Find("FrontFlame");
                if (front != null) front.gameObject.SetActive(false);
                var smoke = root.transform.Find("BackSmoke");
                if (smoke != null)
                    foreach (var ps in smoke.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (!ps.name.Contains("parkles") || ps.name.Contains("Vertical")) continue;
                        var shape = ps.shape;
                        if (shape.shapeType == ParticleSystemShapeType.Sphere) shape.shapeType = ParticleSystemShapeType.Hemisphere;
                        ps.transform.localRotation = Quaternion.identity;
                    }
                PrefabUtility.SaveAsPrefabAsset(root, Backblast);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
