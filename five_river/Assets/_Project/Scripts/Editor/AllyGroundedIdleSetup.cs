using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Visual animation correction only. Actor roots, markers, colliders, state timing and fire remain unchanged.
    public static class AllyGroundedIdleSetup
    {
        const string Folder="Assets/_Project/Art/SurfaceDetail20261030";
        public static void Apply()
        {
            foreach(var actor in Object.FindObjectsByType<FiveRivers.Combat.AllyCombatant>(FindObjectsSortMode.None))
            {
                var animator=actor.GetComponentInChildren<Animator>();
                var basis=animator.runtimeAnimatorController;
                while(basis is AnimatorOverrideController previous)basis=previous.runtimeAnimatorController;
                var overrides=new AnimatorOverrideController(basis);
                var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();
                var clone=Object.Instantiate(actor.gameObject);
                try
                {
                    var sample=clone.GetComponentInChildren<Animator>();sample.runtimeAnimatorController=basis;
                    foreach(var source in basis.animationClips.Where(c=>c.name=="A_ROK_Idle_Stand"||c.name=="A_ROK_Call"))
                    {
                        var derived=Object.Instantiate(source);derived.name=source.name+"_Grounded_"+actor.name;
                        var binding=AnimationUtility.GetCurveBindings(derived).First(b=>b.propertyName=="RootT.y");
                        var curve=AnimationUtility.GetEditorCurve(derived,binding);
                        source.SampleAnimation(sample.gameObject,.1f);
                        float before=sample.GetBoneTransform(HumanBodyBones.LeftFoot).position.y;
                        Shift(curve,-.1f);AnimationUtility.SetEditorCurve(derived,binding,curve);
                        derived.SampleAnimation(sample.gameObject,.1f);
                        float response=(before-sample.GetBoneTransform(HumanBodyBones.LeftFoot).position.y)/.1f;
                        if(response<.1f)throw new InvalidOperationException("Humanoid height response unavailable: "+actor.name);
                        float minimum=float.MaxValue;
                        foreach(float time in new[]{.1f,source.length*.5f,Mathf.Max(.1f,source.length-.1f)})
                        {source.SampleAnimation(sample.gameObject,time);minimum=Mathf.Min(minimum,SoleGap(clone));}
                        curve=AnimationUtility.GetEditorCurve(source,binding);
                        Shift(curve,-Mathf.Max(0,minimum-.008f)/response);
                        AnimationUtility.SetEditorCurve(derived,binding,curve);
                        string path=Folder+"/"+derived.name+".anim";
                        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if(existing==null){AssetDatabase.CreateAsset(derived,path);existing=derived;}
                        else{EditorUtility.CopySerialized(derived,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(derived);}
                        pairs.Add(new KeyValuePair<AnimationClip,AnimationClip>(source,existing));
                    }
                }
                finally{Object.DestroyImmediate(clone);}
                overrides.ApplyOverrides(pairs);
                string controllerPath=Folder+"/"+actor.name+"_GroundedIdle.overrideController";
                var saved=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
                if(saved==null){AssetDatabase.CreateAsset(overrides,controllerPath);saved=overrides;}
                else{EditorUtility.CopySerialized(overrides,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(overrides);}
                animator.runtimeAnimatorController=saved;EditorUtility.SetDirty(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();
        }
        static void Shift(AnimationCurve curve,float amount){var keys=curve.keys;for(int i=0;i<keys.Length;i++)keys[i].value+=amount;curve.keys=keys;}
        static float SoleGap(GameObject actor)
        {
            var renderer=actor.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=renderer.sharedMesh;
            var vertices=mesh.vertices;var weights=mesh.boneWeights;var binds=mesh.bindposes;
            var matrices=renderer.bones.Select((bone,i)=>bone.localToWorldMatrix*binds[i]).ToArray();
            var terrain=Terrain.activeTerrain;float minimum=float.MaxValue;
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var v=vertices[i];
                var p=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+
                    matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                minimum=Mathf.Min(minimum,p.y-terrain.SampleHeight(p)-terrain.transform.position.y);
            }
            return minimum;
        }
    }
}
