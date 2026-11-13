using System;
using System.Collections.Generic;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    public static class ParkCombatFootPlacementSetup
    {
        const string Folder = "Assets/_Project/Art/CharacterVariants/ParkCombatFootPlacement20261106";
        const string BasePath = Folder + "/AC_ROK_ParkCombatFootIK.controller";
        const string OverridePath = Folder + "/ParkYongpal_FootIK.overrideController";

        // Call in Edit mode; the caller reviews and saves the scene.
        public static string Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Park foot setup requires Edit mode.");
            var actor = GameObject.Find("[Map]/Generated/People/ParkYongpal");
            if (actor == null) throw new InvalidOperationException("Exact ParkYongpal scene actor not found.");
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) throw new InvalidOperationException("Park humanoid Animator missing.");
            var current = animator.runtimeAnimatorController;
            var basis = current;
            while (basis is AnimatorOverrideController wrapper) basis = wrapper.runtimeAnimatorController;
            if (!(basis is AnimatorController source)) throw new InvalidOperationException("Park AnimatorController base missing.");
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            if (current is AnimatorOverrideController original) original.GetOverrides(pairs);
            else pairs.AddRange(source.animationClips.Select(c => new KeyValuePair<AnimationClip, AnimationClip>(c, null)));
            var preserved = pairs.ToDictionary(p => p.Key, p => p.Value);
            EnsureFolder(Folder);
            var privateBase = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            if (privateBase == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), BasePath))
                    throw new InvalidOperationException("Park private controller copy failed.");
                privateBase = AssetDatabase.LoadAssetAtPath<AnimatorController>(BasePath);
            }
            else if (source != privateBase)
                throw new InvalidOperationException("Existing Park private controller differs from current base; inspect before replacing.");
            var layers = privateBase.layers;
            if (layers.Length == 0) throw new InvalidOperationException("Park base layer missing.");
            layers[0].iKPass = true;
            privateBase.layers = layers;
            foreach (var child in layers[0].stateMachine.states)
                if (child.state.name == "Idle_Stand" || child.state.name == "Walk_Tired")
                {
                    child.state.iKOnFeet = true;
                    EditorUtility.SetDirty(child.state);
                }
            EditorUtility.SetDirty(privateBase);
            AssetDatabase.SaveAssetIfDirty(privateBase);
            var working = new AnimatorOverrideController(privateBase);
            working.ApplyOverrides(pairs);
            var saved = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
            if (saved == null) { AssetDatabase.CreateAsset(working, OverridePath); saved = working; }
            else { EditorUtility.CopySerialized(working, saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(working); }
            var after = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            saved.GetOverrides(after);
            if (after.Any(p => !preserved.TryGetValue(p.Key, out var clip) || p.Value != clip))
                throw new InvalidOperationException("A Park clip override changed unexpectedly.");
            AssetDatabase.SaveAssetIfDirty(saved);
            Undo.RecordObject(animator, "Park forward boot terrain contact");
            animator.runtimeAnimatorController = saved;
            if (animator.GetComponent<ParkCombatFootPlacement>() == null)
                Undo.AddComponent<ParkCombatFootPlacement>(animator.gameObject);
            EditorUtility.SetDirty(animator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            EditorSceneManager.MarkSceneDirty(actor.scene);
            return "Park only: base IK pass and standing/walking Foot IK enabled; all " + after.Count + " clip references preserved; root/terrain/arms unchanged; scene needs review and save.";
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string parent = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = next;
            }
        }
    }
}
