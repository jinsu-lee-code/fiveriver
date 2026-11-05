using System;
using System.Linq;
using System.Collections.Generic;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace FiveRivers.EditorTools
{
    public static class M1SightHandlingSetup
    {
        const string Dir="Assets/_Project/Art/Weapons/M1Handling20261102";
        [MenuItem("FiveRivers/Setup/Refine M1 Sights And Handling")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
            if(!AssetDatabase.IsValidFolder(Dir))AssetDatabase.CreateFolder("Assets/_Project/Art/Weapons","M1Handling20261102");
            var rifle=Object.FindFirstObjectByType<M1Rifle>();
            var art=rifle.Model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="ArtM1");
            var filters=art.GetComponentsInChildren<MeshFilter>(true);
            foreach(var filter in filters.Where(f=>f.name=="SM_M1_Receiver_L0"||f.name=="SM_M1_Barrel_L0"))
            {
                string originalKey=filter.name+"_Source";
                string sourcePath=Dir+"/"+originalKey+".asset";
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
                if(source==null){source=Object.Instantiate(filter.sharedMesh);source.name=originalKey;AssetDatabase.CreateAsset(source,sourcePath);}
                var mesh=Object.Instantiate(source);var points=source.vertices;
                if(filter.name.Contains("Receiver"))
                {
                    int removed=0;
                    bool Plate(Vector3 p){p=art.InverseTransformPoint(filter.transform.TransformPoint(p));return Mathf.Abs(p.x)<.0056f&&p.y>.0684f&&p.z>-.0216f&&p.z<-.0195f;}
                    for(int s=0;s<source.subMeshCount;s++)
                    {
                        var tri=source.GetTriangles(s);var keep=new List<int>();
                        for(int i=0;i<tri.Length;i+=3){if(Plate(points[tri[i]])&&Plate(points[tri[i+1]])&&Plate(points[tri[i+2]])){removed++;continue;}keep.AddRange(new[]{tri[i],tri[i+1],tri[i+2]});}
                        mesh.SetTriangles(keep,s);
                    }
                    if(removed<100)throw new InvalidOperationException("Reviewed rear aperture did not match expected component");
                    mesh.name="M1_Receiver_SingleAperture";
                }
                else
                {
                    var normals=source.normals;
                    for(int i=0;i<points.Length;i++)
                    {
                        var p=art.InverseTransformPoint(filter.transform.TransformPoint(points[i]));
                        if(p.z>.676f&&p.z<.699f&&Mathf.Abs(p.x)>.0048f&&p.y>.0705f)
                        {
                            // Shorter protective ears retain their outward flare, leaving the
                            // central blade legible instead of a tall tuning-fork silhouette.
                            p.y=.071f+(p.y-.071f)*.75f;
                            p.x=Mathf.Sign(p.x)*(.0052f+(Mathf.Abs(p.x)-.0052f)*.85f);
                            points[i]=filter.transform.InverseTransformPoint(art.TransformPoint(p));
                            var normal=art.InverseTransformDirection(filter.transform.TransformDirection(normals[i]));
                            normal=new Vector3(normal.x/.85f,normal.y/.75f,normal.z).normalized;
                            normals[i]=filter.transform.InverseTransformDirection(art.TransformDirection(normal));
                        }
                    }
                    // Keep the authored barrel's welded shading across its UV seams.
                    // Only the resized guard needs its normal field transformed.
                    mesh.vertices=points;mesh.normals=normals;mesh.tangents=source.tangents;mesh.name="M1_Barrel_RefinedFrontGuard";
                }
                mesh.RecalculateBounds();filter.sharedMesh=Save(mesh,Dir+"/"+mesh.name+".asset");EditorUtility.SetDirty(filter);
            }
            var old=art.Find("M1_SingleRearAperture");if(old!=null)Object.DestroyImmediate(old.gameObject);
            var rear=new GameObject("M1_SingleRearAperture");rear.transform.SetParent(art,false);rear.layer=31;
            rear.AddComponent<MeshFilter>().sharedMesh=Save(Aperture(),Dir+"/M1_RearAperture.asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/M1_SightSteel.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="M1_SightSteel"};AssetDatabase.CreateAsset(material,Dir+"/M1_SightSteel.mat");}
            material.SetColor("_BaseColor",new Color(.24f,.26f,.27f));material.SetFloat("_Metallic",.7f);material.SetFloat("_Smoothness",.28f);EditorUtility.SetDirty(material);
            var renderer=rear.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            Vector3 back=new Vector3(0,.0797f,-.020575f),front=new Vector3(0,.0821156f,.68755f);
            Marker(art,"Sight_RearCentre",back);Marker(art,"Sight_FrontTip",front);
            var aim=Quaternion.FromToRotation((front-back).normalized,Vector3.forward);
            var data=new SerializedObject(rifle);
            data.FindProperty("aimEuler").vector3Value=aim.eulerAngles;
            data.FindProperty("aimPosition").vector3Value=Vector3.forward*.095f-aim*back;
            data.FindProperty("hipPosition").vector3Value=new Vector3(.14f,-.165f,.225f);
            data.ApplyModifiedProperties();
            var pres=rifle.Model.GetComponentInChildren<M1Presentation>(true);var settings=new SerializedObject(pres);
            settings.FindProperty("chamberRounds").objectReferenceValue=filters.First(f=>f.name=="SM_M1_Clip_L0").transform;settings.ApplyModifiedProperties();
            foreach(var r in art.GetComponentsInChildren<Renderer>(true))
            {
                var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    if(mats[i]==null||!(mats[i].name.StartsWith("Photo_")||mats[i].name.StartsWith("M1_View_")))continue;
                    string sourceName=mats[i].name.Replace("M1_View_","");string path=Dir+"/M1_View_"+sourceName+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat==null){mat=new Material(mats[i]){name="M1_View_"+sourceName};AssetDatabase.CreateAsset(mat,path);}
                    mat.SetFloat("_Smoothness",sourceName.Contains("Wood")?.6f:.48f);EditorUtility.SetDirty(mat);mats[i]=mat;
                }
                r.sharedMaterials=mats;EditorUtility.SetDirty(r);
            }
            EditorSceneManager.MarkSceneDirty(rifle.gameObject.scene);EditorSceneManager.SaveScene(rifle.gameObject.scene);AssetDatabase.SaveAssets();
            EditorBattlefieldOverview.Apply();
        }
        static void Marker(Transform parent,string name,Vector3 position)
        {
            var marker=parent.Find(name);if(marker==null){marker=new GameObject(name).transform;marker.SetParent(parent,false);}marker.localPosition=position;
        }
        static Mesh Save(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            existing.Clear(false);existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.tangents=mesh.tangents;existing.uv=mesh.uv;
            existing.subMeshCount=mesh.subMeshCount;for(int s=0;s<mesh.subMeshCount;s++)existing.SetTriangles(mesh.GetTriangles(s),s);existing.RecalculateBounds();EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
        }
        static Mesh Aperture()
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();const int count=96;
            Vector3 P(float radius,int i,float z){float a=i*Mathf.PI*2/count;return new Vector3(Mathf.Cos(a)*radius,.0797f+Mathf.Sin(a)*radius,z);}
            float Outer(int i){float a=i*Mathf.PI*2/count;return Mathf.Sin(a)>=0?.00525f:Mathf.Min(.00525f/Mathf.Max(.0001f,Mathf.Abs(Mathf.Cos(a))),.0107f/Mathf.Max(.0001f,-Mathf.Sin(a)));}
            void Q(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int k=v.Count;v.AddRange(new[]{a,b,c,d});
                // Millimetre-sized bevel faces fall below Vector3.normalized's
                // world-unit epsilon unless their edges are scaled first.
                var normal=Vector3.Cross((b-a)*1000f,(c-a)*1000f).normalized;
                n.AddRange(new[]{normal,normal,normal,normal});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});indices.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            }
            const float back=-.021275f,front=-.019875f,hole=.00145f;
            for(int i=0;i<count;i++)
            {
                int j=i+1;float a=Outer(i),b=Outer(j);
                Q(P(hole,i,back),P(hole,j,back),P(b-.00015f,j,back),P(a-.00015f,i,back));
                Q(P(a-.00015f,i,front),P(b-.00015f,j,front),P(hole,j,front),P(hole,i,front));
                Q(P(a-.00015f,i,back),P(b-.00015f,j,back),P(b,j,back+.00015f),P(a,i,back+.00015f));
                Q(P(a,i,back+.00015f),P(b,j,back+.00015f),P(b,j,front-.00015f),P(a,i,front-.00015f));
                Q(P(a,i,front-.00015f),P(b,j,front-.00015f),P(b-.00015f,j,front),P(a-.00015f,i,front));
                Q(P(hole,j,back),P(hole,i,back),P(hole-.0001f,i,back+.0001f),P(hole-.0001f,j,back+.0001f));
                Q(P(hole-.0001f,j,back+.0001f),P(hole-.0001f,i,back+.0001f),P(hole-.0001f,i,front-.0001f),P(hole-.0001f,j,front-.0001f));
                Q(P(hole-.0001f,j,front-.0001f),P(hole-.0001f,i,front-.0001f),P(hole,i,front),P(hole,j,front));
            }
            var mesh=new Mesh{name="M1_RearAperture"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
    }
}
