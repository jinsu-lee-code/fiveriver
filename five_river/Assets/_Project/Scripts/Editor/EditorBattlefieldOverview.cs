using System.Reflection;
using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace FiveRivers.EditorTools
{
    [InitializeOnLoad]
    public static class EditorBattlefieldOverview
    {
        static EditorBattlefieldOverview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Apply;
            };
        }
        [MenuItem("FiveRivers/Review/Show Battlefield Overview")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var player=Object.FindFirstObjectByType<PlayerController>();if(player==null)return;
            var points=Object.FindFirstObjectByType<LevelPoints>();
            var bounds=new Bounds(player.transform.position,Vector3.one*35f);
            if(points!=null&&points.enemySpawns!=null)foreach(var entry in points.enemySpawns)if(entry!=null)bounds.Encapsulate(entry.position);
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if(type!=null)foreach(var obj in Resources.FindObjectsOfTypeAll(type))
            {
                var game=obj as EditorWindow;if(game==null)continue;game.maximized=false;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
                var minimum=type.GetProperty("minScale",flags);var snap=type.GetMethod("SnapZoom",flags);
                if(minimum!=null&&snap!=null)snap.Invoke(game,new[]{minimum.GetValue(game)});
                game.Repaint();
            }
            // Editor-only visibility excludes screen UI and first-person meshes from
            // the wide map view. Game cameras retain their own rendering masks.
            Tools.visibleLayers &= ~((1<<5)|(1<<31));
            foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if(renderer.name=="Ridges_Continuous_Backdrop")SceneVisibilityManager.instance.Hide(renderer.gameObject,false);
            var scene=EditorWindow.GetWindow<SceneView>();scene.maximized=false;scene.sceneLighting=false;scene.in2DMode=false;
            scene.sceneViewState.showFog=false;
            scene.sceneViewState.showSkybox=false;
            scene.sceneViewState.showImageEffects=false;
            scene.LookAt(bounds.center,Quaternion.Euler(38,0,0),Mathf.Max(85,bounds.extents.magnitude*.85f),false,true);
            Selection.activeObject=null;scene.Focus();scene.Repaint();
        }
    }
}
