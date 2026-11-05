using System;
using FiveRivers.Audio;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    public static class AllyCombatSetup
    {
        [MenuItem("FiveRivers/Setup/Connect Allied Defensive Fire")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before connecting allies.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Art/Weapons/M1Refined/Prefabs/M1_Reviewed.prefab");
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_Project/Data/WaveConfig.asset");
            if (prefab == null || config == null) throw new InvalidOperationException("Missing M1 or wave config.");
            foreach (string name in new[] { "Ally_1", "Ally_2", "Ally_3", "ParkYongpal" })
            {
                var go = GameObject.Find(name);
                if (go == null) throw new InvalidOperationException("Missing scene ally: " + name);
                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman) throw new InvalidOperationException("Missing humanoid: " + name);
                var combat = SetupUtil.Ensure<AllyCombatant>(go);
                AllySkinNormals.Apply(go);
                var weapon = go.transform.Find("AlliedM1");
                if (weapon == null)
                {
                    weapon = new GameObject("AlliedM1").transform;
                    weapon.SetParent(go.transform, false);
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, weapon);
                    visual.name = "M1_Visual";
                    foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                    foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                }
                var muzzle = weapon.Find("Socket_Muzzle");
                if (muzzle == null)
                {
                    muzzle = new GameObject("Socket_Muzzle").transform;
                    muzzle.SetParent(weapon, false);
                }
                // World M1_Reviewed is 1.105m long; its actual barrel ends at z=.724.
                muzzle.localPosition = new Vector3(0f, 0.026f, 0.724f);
                muzzle.localRotation = Quaternion.identity;
                SetupUtil.SetRef(combat, "director", Object.FindFirstObjectByType<FlowDirector>());
                SetupUtil.SetRef(combat, "enemies", Object.FindFirstObjectByType<EnemyDirector>());
                SetupUtil.SetRef(combat, "defencePoints", Object.FindFirstObjectByType<LevelPoints>());
                SetupUtil.SetRef(combat, "config", config);
                SetupUtil.SetRef(combat, "playerHealth", Object.FindFirstObjectByType<PlayerHealth>());
                SetupUtil.SetRef(combat, "effects", Object.FindFirstObjectByType<Effects>());
                SetupUtil.SetRef(combat, "audioDirector", Object.FindFirstObjectByType<AudioDirector>());
                SetupUtil.SetRef(combat, "animator", animator);
                SetupUtil.SetRef(combat, "weapon", weapon);
                SetupUtil.SetRef(combat, "muzzle", muzzle);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                EditorUtility.SetDirty(combat);
                PrefabUtility.RecordPrefabInstancePropertyModifications(combat);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Allies] Four ROK actors connected: crouch/fire clip, tracked M1 muzzle, spatial M1 SFX; no damage or progression writes.");
        }
    }
}
