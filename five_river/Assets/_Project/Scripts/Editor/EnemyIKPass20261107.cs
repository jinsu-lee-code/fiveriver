using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Turns on the IK pass of the KPA base layer so EnemyHandIK can put the support hand on the rifle.
    public static class EnemyIKPass20261107
    {
        [MenuItem("FiveRivers/Animation/Enable KPA Hand IK 20261107")]
        public static void Apply()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Art/Animations/AC_KPA.controller");
            var layers = c.layers;
            if (layers[0].iKPass) { Debug.Log("[KPA IK] Already on."); return; }
            layers[0].iKPass = true;
            c.layers = layers;
            EditorUtility.SetDirty(c);
            AssetDatabase.SaveAssets();
            Debug.Log("[KPA IK] IK pass enabled on " + layers[0].name);
        }
    }
}
