using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-16 (사용자 지시 "트럭하고 전차하고 같은 화면에 나오지 않게 하고 트럭에서 학도병 내리는 모션으로 바꾸어줘").
    // The dawn truck stops behind the west end of the trench, coming in from the east behind the line, so that from the
    // summit and from the trench it is never in the same view as the knocked-out tank (the summit camera also turns
    // away from the wreck, SummitDawnDirector.AwayFromTank). The jump down itself is DawnTruckSequence + DismountPose.
    // Also removes the first try at a hand-built dismount clip, if it is still there. Re-running gives the same result.
    public static class DawnTruckDismountSetup
    {
        const string OldClipPath = "Assets/_Project/Art/Animations/A_ROK_Dismount_Truck.anim";
        const string ControllerPath = "Assets/_Project/Art/Animations/AC_ROK.controller";

        [MenuItem("FiveRivers/Setup/Dawn Truck Route (Away From Tank)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드에서 실행");
            var report = new StringBuilder();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            var old = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Dismount");
            if (old != null) { sm.RemoveState(old); EditorUtility.SetDirty(controller); report.AppendLine("removed AC_ROK state Dismount"); }
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(OldClipPath) != null) { AssetDatabase.DeleteAsset(OldClipPath); report.AppendLine("deleted " + OldClipPath); }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) scene = EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var start = GameObject.Find("[Narrative]/DawnTruckRoute/ArrivalStart").transform;
            var stop = GameObject.Find("[Narrative]/DawnTruckRoute/ArrivalStop").transform;
            var terrain = Terrain.activeTerrain;
            Vector3 G(float x, float z) { var p = new Vector3(x, 0f, z); p.y = terrain.SampleHeight(p) + terrain.transform.position.y; return p; }
            // in from the east behind the trench line; stops 14 m past the west end of the trench, tailgate toward the line.
            // From the summit the tank wreck (39, 23) is then 37 degrees off the truck, from the trench 165 (measured 11-16).
            var a = G(6f, -12f); var b = G(-20f, -10f);
            var dir = b - a; dir.y = 0f;
            start.SetPositionAndRotation(a, Quaternion.LookRotation(dir));
            stop.SetPositionAndRotation(b, Quaternion.Euler(0f, 270f, 0f));
            EditorUtility.SetDirty(start); EditorUtility.SetDirty(stop);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            report.AppendLine($"truck route {a} -> {b}");
            System.IO.File.WriteAllText("Temp/dismount_setup.txt", report.ToString());
            return "ok";
        }
    }
}
