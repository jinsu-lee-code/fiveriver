using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace FiveRivers.EditorTools
{
    // LateUpdate poses these renderers only in Play mode. Keep the authoring pose
    // out of the editor Game view, including newly opened/reloaded scene objects.
    [InitializeOnLoad]
    public sealed class FirstPersonEditorPreview : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        static bool hideQueued;
        public int callbackOrder => -1000;

        static FirstPersonEditorPreview()
        {
            QueueHide();
            EditorSceneManager.sceneOpened += (scene, mode) => QueueHide();
            EditorApplication.hierarchyChanged += QueueHide;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                    SetHidden(false);
                else if (state == PlayModeStateChange.EnteredEditMode)
                    QueueHide();
            };
        }

        static void QueueHide()
        {
            if (hideQueued || EditorApplication.isPlayingOrWillChangePlaymode) return;
            hideQueued = true;
            EditorApplication.update += HideWhenReady;
        }

        static void HideWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            EditorApplication.update -= HideWhenReady;
            hideQueued = false;
            Hide();
        }

        [MenuItem("FiveRivers/Review/Clear Arms From Editor Game Preview")]
        static void Hide()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            SetHidden(true);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        static void SetHidden(bool hidden)
        {
            foreach (var rifle in Object.FindObjectsByType<M1Rifle>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rifle.Model != null && !EditorSceneManager.IsPreviewScene(rifle.gameObject.scene))
                    foreach (var renderer in rifle.Model.GetComponentsInChildren<Renderer>(true))
                        renderer.forceRenderingOff = hidden;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            SetHidden(false);
            // Postprocess is not guaranteed after an aborted/failed build.
            EditorApplication.update -= RestoreAfterBuild;
            EditorApplication.update += RestoreAfterBuild;
        }

        static void RestoreAfterBuild()
        {
            if (BuildPipeline.isBuildingPlayer) return;
            EditorApplication.update -= RestoreAfterBuild;
            QueueHide();
        }

        public void OnPostprocessBuild(BuildReport report) => QueueHide();
    }
}
