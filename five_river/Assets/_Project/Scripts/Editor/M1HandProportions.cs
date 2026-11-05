using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Derive from the reviewed source on every setup: never compound edits or alter the source grip.
    public static class M1HandProportions
    {
        const string Source = "Assets/_Project/Art/Weapons/M1Refined/CorrectedGrip/Refined_Grip_Hands.asset";
        const string Output = "Assets/_Project/Art/Animations/FP/MixamoSource/M1_AdultHandProportions.asset";

        public static void Apply(GameObject hands)
        {
            var renderer=hands.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.sharedMesh!=null);
            var original=AssetDatabase.LoadAssetAtPath<Mesh>(Source);
            if(original==null)throw new InvalidOperationException("Reviewed hand source is missing.");
            var mesh=UnityEngine.Object.Instantiate(original);
            mesh.name="M1_AdultHandProportions";
            var vertices=original.vertices;var weights=original.boneWeights;var binds=original.bindposes;
            var colors=new Color[vertices.Length];
            var centres=new Vector3[binds.Length];var counts=new int[binds.Length];
            var ends=Enumerable.Repeat(float.MinValue,binds.Length).ToArray();
            for(int i=0;i<vertices.Length;i++)
            {
                int b=weights[i].boneIndex0;
                var local=binds[b].MultiplyPoint3x4(vertices[i]);centres[b]+=local;counts[b]++;ends[b]=Mathf.Max(ends[b],local.y);
            }
            for(int b=0;b<centres.Length;b++)if(counts[b]>0)centres[b]/=counts[b];
            Vector3 Expand(Vector3 point,int b)
            {
                var local=binds[b].MultiplyPoint3x4(point);
                string name=renderer.bones[b].name;
                var centre=centres[b];
                // Preserve longitudinal length and articulation. Enlarge only soft tissue cross-sections.
                if(name.StartsWith("finger"))
                {
                    float amount=name.StartsWith("finger1-")?1.18f:1.22f;
                    // Broad, rounded knuckle tissue rather than uniformly cylindrical fingers.
                    amount*=1f+.075f*Mathf.Exp(-Mathf.Pow(local.y/.007f,2f));
                    if(name.Contains("-3."))amount*=Mathf.Lerp(1f,.9f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(ends[b]*.55f,ends[b],local.y)));
                    if(name.StartsWith("finger1-1"))amount*=1.08f;
                    local.x=centre.x+(local.x-centre.x)*amount;
                    local.z=centre.z+(local.z-centre.z)*amount;
                }
                else
                {
                    local.x=centre.x+(local.x-centre.x)*1.10f;
                    local.z=centre.z+(local.z-centre.z)*1.23f;
                }
                return binds[b].inverse.MultiplyPoint3x4(local);
            }
            Color TissueTint(Vector3 point,int b)
            {
                var local=binds[b].MultiplyPoint3x4(point);
                string name=renderer.bones[b].name;
                float warm=name.StartsWith("finger")?Mathf.Exp(-Mathf.Pow(local.y/.008f,2f))*.7f:.18f;
                if(name.StartsWith("finger1-1")||name.StartsWith("metacarpal1"))warm=.4f;
                return Color.Lerp(Color.white,new Color(1.055f,.91f,.875f),warm);
            }
            for(int i=0;i<vertices.Length;i++)
            {
                var p=vertices[i];var w=weights[i];
                vertices[i]=Expand(p,w.boneIndex0)*w.weight0+Expand(p,w.boneIndex1)*w.weight1+
                    Expand(p,w.boneIndex2)*w.weight2+Expand(p,w.boneIndex3)*w.weight3;
                colors[i]=TissueTint(p,w.boneIndex0)*w.weight0+TissueTint(p,w.boneIndex1)*w.weight1+
                    TissueTint(p,w.boneIndex2)*w.weight2+TissueTint(p,w.boneIndex3)*w.weight3;
            }
            mesh.vertices=vertices;mesh.colors=colors;
            // Preserve the authored smooth hand normals. Recalculation exposes the source's
            // internal face partitioning as large palm facets despite welded UV seams.
            // Tissue expansion is modest; the source normal field retains the intended anatomy.
            mesh.normals=original.normals;mesh.tangents=original.tangents;mesh.RecalculateBounds();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Output);
            if(existing==null){AssetDatabase.CreateAsset(mesh,Output);existing=mesh;}
            else{EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);}
            renderer.sharedMesh=existing;
            const string materialPath="Assets/_Project/Art/SurfaceDetail20261030/M1_AdultHand_Skin.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(renderer.sharedMaterial){name="M1_AdultHand_Skin"};AssetDatabase.CreateAsset(material,materialPath);}
            var shader=Shader.Find("FiveRivers/Anatomical Skin");
            if(shader!=null)material.shader=shader;
            renderer.sharedMaterial=material;EditorUtility.SetDirty(material);
        }
    }
}
