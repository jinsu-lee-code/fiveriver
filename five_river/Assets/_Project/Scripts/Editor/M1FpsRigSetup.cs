using System;
using System.Collections.Generic;
using System.Linq;
using FiveRivers.Combat;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    public static class M1FpsRigSetup
    {
        const string Generated = "Assets/_Project/Art/Animations/FP/MixamoSource/";
        const string Arms = "Assets/_Project/Art/Models/Characters/ROK_Arms/";

        public static void Configure(GameObject corrected, Transform presentation, M1Rifle rifle, SerializedObject settings)
        {
            M1HandProportions.Apply(corrected);
            var camera = presentation.GetComponentInParent<Camera>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Arms + "SK_CH_ROK_Arms.fbx");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(source, presentation);
            PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rig.name = "M1_MixamoArms";
            rig.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            foreach (var animator in rig.GetComponentsInChildren<Animator>()) animator.enabled = false;
            var sourceBones = rig.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            var correctedBones = corrected.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            var pose = JObject.Parse(System.IO.File.ReadAllText(Arms + "ROK_Arms_Poses.json"));
            var grip = pose["grips"].First(p => (string)p["id"] == "M1");
            foreach (var row in grip["bones"])
            {
                if (!sourceBones.TryGetValue((string)row["name"], out var bone)) continue;
                var matrix = new Matrix4x4();
                for (int r=0;r<4;r++) for(int c=0;c<4;c++) matrix[r,c] = (float)row["matrix"][r*4+c];
                var world = camera.transform.localToWorldMatrix * matrix;
                bone.SetPositionAndRotation(world.GetColumn(3), world.rotation);
            }
            foreach (var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Keep the full skeleton, render only the continuous derived cloth.
                skin.enabled=false;
            }
            foreach(var sleeve in corrected.GetComponentsInChildren<MeshRenderer>()) sleeve.enabled=false;
            Transform Target(string name,Transform wrist)
            {
                var target=new GameObject(name).transform;target.SetParent(presentation,false);
                target.SetPositionAndRotation(wrist.position,wrist.rotation);return target;
            }
            var rightTarget=Target("Grip_Right",correctedBones["wrist.R"]);
            var leftTarget=Target("Grip_Left",correctedBones["wrist.L"]);
            var rightUpper=sourceBones["mixamorig:RightArm"];
            var rightFore=sourceBones["mixamorig:RightForeArm"];
            var rightWrist=sourceBones["mixamorig:RightHand"];
            var leftUpper=sourceBones["mixamorig:LeftArm"];
            var leftFore=sourceBones["mixamorig:LeftForeArm"];
            var leftWrist=sourceBones["mixamorig:LeftHand"];
            var motion=CreateMixamoClip(rig);
            var rightWristRest=rightWrist.localRotation;
            var leftWristRest=leftWrist.localRotation;
            M1ArmRig.Solve(rightUpper,rightFore,rightWrist,rightTarget);
            M1ArmRig.Solve(leftUpper,leftFore,leftWrist,leftTarget);
            rightWrist.localRotation=rightWristRest;
            leftWrist.localRotation=leftWristRest;
            void Graft(Transform wrist,Transform native,Transform target)
            {
                wrist.SetParent(native,true);wrist.SetPositionAndRotation(target.position,target.rotation);
            }
            Graft(correctedBones["wrist.R"],rightWrist,rightTarget);Graft(correctedBones["wrist.L"],leftWrist,leftTarget);
            var armRig=rig.AddComponent<M1ArmRig>();
            var data=new SerializedObject(armRig);
            void Set(string key,Object value)=>data.FindProperty(key).objectReferenceValue=value;
            Set("rifle",rifle);Set("view",camera);Set("fireMotion",motion);
            Set("leftUpper",leftUpper);Set("leftForearm",leftFore);Set("leftWrist",leftWrist);Set("leftTarget",leftTarget);
            Set("rightUpper",rightUpper);Set("rightForearm",rightFore);Set("rightWrist",rightWrist);Set("rightTarget",rightTarget);
            Set("leftVisualWrist",correctedBones["wrist.L"]);Set("rightVisualWrist",correctedBones["wrist.R"]);
            const string clothPath=Generated+"M1_FP_Cloth.mat";
            var cuffMaterial=AssetDatabase.LoadAssetAtPath<Material>(clothPath);
            // Defaults only for a new material; keep the reviewed cloth look on regeneration.
            if(cuffMaterial==null)
            {
                cuffMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="M1_FP_Cloth"};
                cuffMaterial.SetColor("_BaseColor",new Color(.23f,.25f,.17f));cuffMaterial.SetFloat("_Smoothness",.16f);
                AssetDatabase.CreateAsset(cuffMaterial,clothPath);
            }
            Transform Cuff(string name,Transform wrist,Transform forearm,Transform target)
            {
                var go=new GameObject(name);go.transform.SetParent(wrist,false);
                go.transform.SetPositionAndRotation(target.position,Quaternion.LookRotation(target.position-forearm.position));
                go.AddComponent<MeshFilter>().sharedMesh=CreateCuff();
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=cuffMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;go.layer=31;
                return go.transform;
            }
            Set("leftCuff",Cuff("Left_ClothCuff",leftWrist,leftFore,leftTarget));
            Set("rightCuff",Cuff("Right_ClothCuff",rightWrist,rightFore,rightTarget));
            var handSkin=corrected.GetComponentInChildren<SkinnedMeshRenderer>();
            void SkinCap(string name,string capField,string radiusField)
            {
                int index=Array.FindIndex(handSkin.bones,b=>b.name==name);
                var mesh=handSkin.sharedMesh;var weights=mesh.boneWeights;var vertices=mesh.vertices;
                var local=vertices.Select((v,i)=>new{v=mesh.bindposes[index].MultiplyPoint3x4(v),w=weights[i]}).Where(x=>x.w.boneIndex0==index).Select(x=>x.v).ToArray();
                float low=local.Min(v=>v.y);var band=local.Where(v=>v.y<low+.007f).ToArray();
                var centre=band.Aggregate(Vector3.zero,(a,v)=>a+v)/band.Length;
                data.FindProperty(capField).vector3Value=centre;
                data.FindProperty(radiusField).floatValue=band.Max(v=>new Vector2(v.x-centre.x,v.z-centre.z).magnitude);
            }
            SkinCap("wrist.L","leftSkinCap","leftSkinRadius");SkinCap("wrist.R","rightSkinCap","rightSkinRadius");
            data.ApplyModifiedPropertiesWithoutUndo();
            settings.FindProperty("rightHand").objectReferenceValue=rightTarget;
            settings.FindProperty("rightHandVisual").objectReferenceValue=correctedBones["wrist.R"];
            var hand=correctedBones["wrist.R"];
            var forward=(correctedBones["finger3-1.R"].position-hand.position).normalized;
            var across=correctedBones["finger2-1.R"].position-correctedBones["finger5-1.R"].position;
            var palm=Vector3.Cross(forward,across).normalized;
            var localPalm=hand.InverseTransformDirection(palm);
            var localForward=hand.InverseTransformDirection(forward);
            var reloadRotation=Quaternion.LookRotation(Vector3.forward,Vector3.down)*Quaternion.Inverse(Quaternion.LookRotation(localPalm,localForward));
            settings.FindProperty("reloadHandRotation").quaternionValue=reloadRotation;
            settings.FindProperty("clipGripOffset").vector3Value=Quaternion.Inverse(reloadRotation)*(new Vector3(0f,.14f,.075f)-new Vector3(.025f,.25f,.09f));
            settings.FindProperty("sleeves").objectReferenceValue=null;
            settings.FindProperty("firingClip").objectReferenceValue=CreateFirstPersonFireClip();
        }

        static AnimationClip CreateMixamoClip(GameObject rig)
        {
            const string path=Generated+"MX_FiringRifle.fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null)throw new InvalidOperationException("Mixamo Firing Rifle source missing.");
            if(importer.animationType!=ModelImporterAnimationType.Generic || !importer.importAnimation)
            {
                importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
                importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
            var sourceBindings=AnimationUtility.GetCurveBindings(source);
            var targetBones=rig.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")).ToArray();
            var clip=new AnimationClip{name="A_FP_M1_Mixamo_Fire_Arms",frameRate=60f,legacy=true};
            int mapped=0;
            foreach(var bone in targetBones)
            {
                if(!bone.name.Contains("Arm") && !bone.name.Contains("Shoulder") && !bone.name.Contains("Spine"))continue;
                var targetPath=AnimationUtility.CalculateTransformPath(bone,rig.transform);
                var matches=sourceBindings.Where(b=>b.path.Split('/').Last()==bone.name && b.propertyName.StartsWith("m_LocalRotation.")).ToArray();
                if(matches.Length!=4)continue;
                Quaternion Read(float t)
                {
                    float C(string axis)=>AnimationUtility.GetEditorCurve(source,matches.First(b=>b.propertyName.EndsWith(axis))).Evaluate(t);
                    return new Quaternion(C(".x"),C(".y"),C(".z"),C(".w")).normalized;
                }
                var origin=Read(0f);var rest=bone.localRotation;
                var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int frame=0;frame<=18;frame++)
                {
                    float t=frame/60f;
                    // Complete Mixamo recoil AND recovery, retimed to one semiautomatic shot.
                    var delta=Quaternion.Inverse(origin)*Read(Mathf.Lerp(.067f,source.length,t/.3f));
                    float envelope=Mathf.SmoothStep(0f,1f,t/.025f)*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.23f,.3f,t)));
                    var q=rest*Quaternion.Slerp(Quaternion.identity,delta,envelope);
                    curves[0].AddKey(t,q.x);curves[1].AddKey(t,q.y);curves[2].AddKey(t,q.z);curves[3].AddKey(t,q.w);
                }
                for(int axis=0;axis<4;axis++)clip.SetCurve(targetPath,typeof(Transform),"localRotation."+"xyzw"[axis],curves[axis]);
                mapped++;
            }
            if(mapped<6)throw new InvalidOperationException("Mixamo local-rotation mappings not found: "+mapped);
            clip.EnsureQuaternionContinuity();
            Debug.Log("[FPS] Real Mixamo source "+source.name+" mapped "+mapped+" arm/spine bones, 19 samples.");
            return SaveClip(clip,Generated+"A_FP_M1_Mixamo_Fire_Arms.anim");
        }

        static AnimationClip CreateFirstPersonFireClip()
        {
            var clip=new AnimationClip{name="A_FP_M1_Shot",frameRate=60f,legacy=true};
            float[] t={0f,.018f,.05f,.10f,.17f,.25f};
            void Curve(string property,float[] values)
            {
                var c=new AnimationCurve();for(int i=0;i<t.Length;i++)c.AddKey(t[i],values[i]);
                clip.SetCurve("",typeof(Transform),property,c);
            }
            Curve("localPosition.z",new[]{0f,-.026f,-.043f,-.018f,.003f,0f});
            Curve("localPosition.y",new[]{0f,.003f,.006f,.002f,-.001f,0f});
            Curve("localEulerAnglesRaw.x",new[]{0f,-2.1f,-3.8f,-1.4f,.18f,0f});
            Curve("localEulerAnglesRaw.y",new[]{0f,.15f,.3f,.08f,0f,0f});
            Curve("localEulerAnglesRaw.z",new[]{0f,-.3f,-.65f,-.2f,0f,0f});
            return SaveClip(clip,Generated+"A_FP_M1_Shot.anim");
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
        }

        static Mesh CreateCuff()
        {
            const string path=Generated+"M1_ClothWristCuff.asset";
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            float[] z=Enumerable.Range(0,35).Select(i=>-.6f+i*.032f).ToArray();
            float[] radius=Enumerable.Repeat(.03f,35).ToArray();
            const int segments=32;
            for(int ring=0;ring<z.Length;ring++)for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2f/segments;vertices.Add(new Vector3(Mathf.Cos(a)*radius[ring],Mathf.Sin(a)*radius[ring],z[ring]));uv.Add(new Vector2(i/(float)segments,ring/(float)(z.Length-1)));
            }
            for(int ring=0;ring<z.Length-1;ring++)for(int i=0;i<segments;i++)
            {
                int a=ring*(segments+1)+i,b=a+segments+1;indices.AddRange(new[]{a,a+1,b,a+1,b+1,b});
            }
            var mesh=new Mesh{name="M1_ClothWristCuff"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            return SaveMesh(mesh,path);
        }
        static AnimationClip SaveClip(AnimationClip clip,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(existing==null){AssetDatabase.CreateAsset(clip,path);return clip;}
            EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(clip);return existing;
        }

        struct SkinVertex
        {
            public Vector3 position,normal;
            public Vector2 uv;
            public BoneWeight weight;
        }

        static void ClipAtWrist(Mesh mesh,int wristIndex)
        {
            if(wristIndex<0)throw new InvalidOperationException("Missing original arm wrist binding.");
            var p=mesh.vertices;var n=mesh.normals;var uv=mesh.uv;var w=mesh.boneWeights;
            var triangles=mesh.triangles;var binds=mesh.bindposes;var plane=binds[wristIndex];
            var output=new List<SkinVertex>();var indices=new List<int>();
            SkinVertex Read(int i)=>new SkinVertex{position=p[i],normal=n[i],uv=uv[i],weight=w[i]};
            SkinVertex Lerp(SkinVertex a,SkinVertex b,float t)
            {
                var weights=new Dictionary<int,float>();
                void Add(BoneWeight bw,float scale)
                {
                    int[] ids={bw.boneIndex0,bw.boneIndex1,bw.boneIndex2,bw.boneIndex3};
                    float[] values={bw.weight0,bw.weight1,bw.weight2,bw.weight3};
                    for(int i=0;i<4;i++)if(values[i]>0f)weights[ids[i]]=(weights.TryGetValue(ids[i],out var old)?old:0f)+values[i]*scale;
                }
                Add(a.weight,1f-t);Add(b.weight,t);
                var sorted=weights.OrderByDescending(x=>x.Value).Take(4).ToArray();
                float sum=sorted.Sum(x=>x.Value);var bw=new BoneWeight();
                for(int i=0;i<sorted.Length;i++)
                {
                    int id=sorted[i].Key;float v=sorted[i].Value/sum;
                    switch(i){case 0:bw.boneIndex0=id;bw.weight0=v;break;case 1:bw.boneIndex1=id;bw.weight1=v;break;case 2:bw.boneIndex2=id;bw.weight2=v;break;case 3:bw.boneIndex3=id;bw.weight3=v;break;}
                }
                return new SkinVertex{position=Vector3.Lerp(a.position,b.position,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t),weight=bw};
            }
            const float cut=-0.005f;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var poly=new[]{Read(triangles[i]),Read(triangles[i+1]),Read(triangles[i+2])};
                var clipped=new List<SkinVertex>();
                for(int edge=0;edge<3;edge++)
                {
                    var a=poly[edge];var b=poly[(edge+1)%3];
                    float da=plane.MultiplyPoint3x4(a.position).y,db=plane.MultiplyPoint3x4(b.position).y;
                    if(da<=cut)clipped.Add(a);
                    if((da<=cut)!=(db<=cut))clipped.Add(Lerp(a,b,(cut-da)/(db-da)));
                }
                if(clipped.Count<3)continue;
                int start=output.Count;output.AddRange(clipped);
                for(int j=1;j<clipped.Count-1;j++)indices.AddRange(new[]{start,start+j,start+j+1});
            }
            if(output.Count<100)throw new InvalidOperationException("Wrist plane removed the original arm.");
            mesh.Clear();mesh.SetVertices(output.Select(x=>x.position).ToList());mesh.SetNormals(output.Select(x=>x.normal).ToList());mesh.SetUVs(0,output.Select(x=>x.uv).ToList());
            mesh.boneWeights=output.Select(x=>x.weight).ToArray();mesh.bindposes=binds;mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();
        }
    }
}
