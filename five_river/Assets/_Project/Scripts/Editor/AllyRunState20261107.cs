using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Gives the ROK riflemen a rifle run for the hill assault (they used to "run" by playing the tired walk at
    // double speed). The KPA rifle run is a humanoid clip, so it retargets onto the ROK avatars. The state has no
    // transitions: HillAllies cross-fades into and out of it directly. Safe to run more than once.
    public static class AllyRunState20261107
    {
        public const string State = "Run_Rifle";
        const string Clip = "Assets/_Project/Art/Animations/KPA/A_KPA_Run_Rifle.fbx";
        static readonly string[] Controllers =
        {
            "Assets/_Project/Art/Animations/AC_ROK.controller",
            "Assets/_Project/Art/CharacterVariants/ParkCombatFootPlacement20261106/AC_ROK_ParkCombatFootIK.controller",
        };

        [MenuItem("FiveRivers/Animation/Add Ally Rifle Run 20261107")]
        public static void Apply()
        {
            AnimationClip run = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Clip))
                if (o is AnimationClip c && !c.name.StartsWith("__")) run = c;
            if (run == null) throw new System.InvalidOperationException("Rifle run clip missing: " + Clip);
            foreach (var path in Controllers)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) { Debug.LogWarning("[Ally run] Missing " + path); continue; }
                var machine = controller.layers[0].stateMachine;
                bool exists = false;
                foreach (var s in machine.states) if (s.state.name == State) exists = true;
                if (exists) continue;
                var state = machine.AddState(State, new Vector3(500f, 260f, 0f));
                state.motion = run;
                state.writeDefaultValues = true;
                EditorUtility.SetDirty(controller);
                Debug.Log("[Ally run] Added " + State + " to " + path);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
