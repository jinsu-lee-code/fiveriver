using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Gives the ROK riflemen a crouched rifle run for the hill assault (they used to advance on the slumped
    // Walk_Tired clip, made for the worn-out reinforcements, at more than twice its foot speed). The KPA crouch
    // run is a humanoid clip, so it retargets onto the ROK avatars. The state has no transitions: HillAllies
    // cross-fades into and out of it directly, like Run_Rifle (AllyRunState20261107). Safe to run more than once.
    public static class AllyCrouchRunState20261110
    {
        public const string State = "CrouchRun_Rifle";
        const string Clip = "Assets/_Project/Art/Animations/KPA/A_KPA_CrouchRun.fbx";
        static readonly string[] Controllers =
        {
            "Assets/_Project/Art/Animations/AC_ROK.controller",
            "Assets/_Project/Art/CharacterVariants/ParkCombatFootPlacement20261106/AC_ROK_ParkCombatFootIK.controller",
        };

        [MenuItem("FiveRivers/Animation/Add Ally Crouch Run 20261110")]
        public static void Apply()
        {
            AnimationClip run = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Clip))
                if (o is AnimationClip c && !c.name.StartsWith("__")) run = c;
            if (run == null) throw new System.InvalidOperationException("Crouch run clip missing: " + Clip);
            foreach (var path in Controllers)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) { Debug.LogWarning("[Ally crouch run] Missing " + path); continue; }
                var machine = controller.layers[0].stateMachine;
                bool exists = false;
                foreach (var s in machine.states) if (s.state.name == State) exists = true;
                if (exists) continue;
                var state = machine.AddState(State, new Vector3(500f, 330f, 0f));
                state.motion = run;
                state.writeDefaultValues = true;
                EditorUtility.SetDirty(controller);
                Debug.Log("[Ally crouch run] Added " + State + " to " + path);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
