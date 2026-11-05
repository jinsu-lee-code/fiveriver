using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    public static class ClaudeMapNature
    {
        const string Source="C:/Users/mbc/Documents/ChatGPT/다섯개의강/Claude_Map_Polish_20261030/Nature_Models";
        const string Folder=ClaudeMapIntegration.Art+"/Nature";
        [MenuItem("FiveRivers/Map/Claude Replace Nature Visuals")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode.");
            var root=GameObject.Find("/[Map]/Dabudong_Imported");
            if(root==null)throw new InvalidOperationException("Imported map missing.");
            Directory.CreateDirectory(Folder);
            foreach(var file in Directory.GetFiles(Source))
            {
                string ext=Path.GetExtension(file).ToLowerInvariant();
                if(ext==".fbx"||ext==".png"||ext==".jpg")File.Copy(file,Folder+"/"+Path.GetFileName(file),true);
            }
            AssetDatabase.Refresh();
            var materials=new Dictionary<string,Material>();
            foreach(string name in new[]{"BG_Pine","BG_Bush","BG_DeadTree","BG_RockA","BG_RockB","BG_RockC"})
            {
                string modelPath=Folder+"/"+name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.importCameras=false;importer.importLights=false;
                importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                bool alpha=name=="BG_Pine"||name=="BG_Bush";
                string basePath=Folder+"/T_"+name+"_BaseColor"+(alpha?".png":".jpg");
                string normalPath=Folder+"/T_"+name+"_Normal.jpg";
                ConfigureTexture(basePath,false,alpha);ConfigureTexture(normalPath,true,false);
                string matPath=Folder+"/M_"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="M_"+name};AssetDatabase.CreateAsset(mat,matPath);}
                mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.8f);
                mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.12f);
                mat.SetFloat("_AlphaClip",alpha?1:0);mat.SetFloat("_Cutoff",.45f);mat.SetFloat("_Cull",alpha?0:2);
                if(alpha){mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
                else{mat.DisableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","Opaque");mat.renderQueue=-1;}
                mat.enableInstancing=true;EditorUtility.SetDirty(mat);materials.Add(name,mat);
            }
            int pine=0,bush=0,rock=0;
            var beforeColliders=root.GetComponentsInChildren<Collider>(true);
            var report=new List<object>();
            foreach(var lod in root.GetComponentsInChildren<LODGroup>(true))
            {
                var levels=lod.GetLODs();if(levels.Length==0||levels[0].renderers.Length==0)continue;
                var old=levels[0].renderers.FirstOrDefault(r=>r!=null);
                if(old==null)continue;
                string species=null;
                if(old.name.StartsWith("Korean_Red_Pine")){species="BG_Pine";pine++;}
                else if(old.name.StartsWith("Summer_Shrub")){species="BG_Bush";bush++;}
                else if(old.name.StartsWith("Granite_Boulder")){species="BG_Rock"+(char)('A'+rock%3);rock++;}
                if(species==null)continue;
                // Render children only; original root, collider children and their settings stay intact.
                var existing=lod.transform.Find("ClaudeNature_Visual");
                if(existing!=null)Object.DestroyImmediate(existing.gameObject);
                var originalBounds=old.bounds;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+species+".fbx");
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(prefab,lod.transform);
                visual.name="ClaudeNature_Visual";visual.transform.localPosition=Vector3.zero;
                visual.transform.rotation=Quaternion.Euler(0,lod.transform.eulerAngles.y,0)*prefab.transform.rotation;visual.transform.localScale=Vector3.one;
                foreach(var c in visual.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
                var renderers=visual.GetComponentsInChildren<Renderer>(true);
                foreach(var r in renderers){r.sharedMaterial=materials[species];r.gameObject.layer=old.gameObject.layer;}
                var bounds=BoundsOf(renderers);
                float scale=originalBounds.size.y/Mathf.Max(.01f,bounds.size.y);
                visual.transform.localScale*=scale;
                bounds=BoundsOf(renderers);
                // Preserve old footprint centre and ground level, without changing any placement root.
                visual.transform.position+=new Vector3(originalBounds.center.x-bounds.center.x,originalBounds.min.y-bounds.min.y,originalBounds.center.z-bounds.center.z);
                if(species.StartsWith("BG_Rock")) FitRockFootprint(visual,originalBounds,rock);
                foreach(var level in levels)foreach(var r in level.renderers)
                    if(r!=null){r.enabled=false;EditorUtility.SetDirty(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
                lod.enabled=false;EditorUtility.SetDirty(lod);PrefabUtility.RecordPrefabInstancePropertyModifications(lod);
                report.Add(new{species,root=lod.transform.parent!=null?lod.transform.parent.name:lod.name,
                    position=lod.transform.position.ToString("R"),oldHeight=originalBounds.size.y,newHeight=BoundsOf(renderers).size.y,alphaClip=species=="BG_Pine"||species=="BG_Bush",renderers=renderers.Length});
            }
            var afterColliders=root.GetComponentsInChildren<Collider>(true);
            if(beforeColliders.Length!=afterColliders.Length||!beforeColliders.SequenceEqual(afterColliders))throw new InvalidOperationException("Nature replacement changed collider inventory.");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveOpenScenes();
            File.WriteAllText(ClaudeMapIntegration.Stage+"/nature_apply.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{pine,bush,rock,collidersPreserved=true,report},Newtonsoft.Json.Formatting.Indented));
            Debug.Log("[ClaudeMap] Nature visuals replaced: pine="+pine+" shrub="+bush+" rock="+rock+"; all original collider/root components retained.");
        }
        static void FitRockFootprint(GameObject visual,Bounds desired,int index)
        {
            var current=BoundsOf(visual.GetComponentsInChildren<Renderer>(true));
            var ratio=new Vector3(desired.size.x/Mathf.Max(.001f,current.size.x),1,desired.size.z/Mathf.Max(.001f,current.size.z));
            int part=0;
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=Object.Instantiate(filter.sharedMesh);
                var vertices=mesh.vertices;var normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++)
                {
                    var world=filter.transform.TransformPoint(vertices[i]);
                    world=desired.center+Vector3.Scale(world-current.center,ratio);
                    vertices[i]=filter.transform.InverseTransformPoint(world);
                    if(normals.Length==vertices.Length)
                    {
                        var normal=filter.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]);
                        normal=new Vector3(normal.x/ratio.x,normal.y,normal.z/ratio.z);
                        normals[i]=filter.transform.localToWorldMatrix.transpose.MultiplyVector(normal).normalized;
                    }
                }
                mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                string path=Folder+"/RockFootprint_"+index.ToString("D3")+"_"+part+++".asset";
                var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old==null)AssetDatabase.CreateAsset(mesh,path);
                else{EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}
                filter.sharedMesh=mesh;EditorUtility.SetDirty(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            }
        }
        static Bounds BoundsOf(Renderer[] renderers){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static void ConfigureTexture(string path,bool normal,bool alpha)
        {
            var i=(TextureImporter)AssetImporter.GetAtPath(path);
            i.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;i.sRGBTexture=!normal;
            i.mipmapEnabled=true;i.anisoLevel=4;i.maxTextureSize=2048;i.wrapMode=TextureWrapMode.Repeat;
            i.alphaSource=alpha?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            i.alphaIsTransparency=alpha;i.SaveAndReimport();
        }

        [MenuItem("FiveRivers/Map/Enable Readable Map Collider Meshes")]
        public static void EnableReadableColliderMeshes()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode.");
            var root=GameObject.Find("/[Map]/Dabudong_Imported");
            string backup=ClaudeMapIntegration.Stage+"/Backups/ColliderImporterMeta";Directory.CreateDirectory(backup);
            var paths=root.GetComponentsInChildren<MeshCollider>(true).Where(c=>c.sharedMesh!=null)
                .Select(c=>AssetDatabase.GetAssetPath(c.sharedMesh)).Distinct().ToArray();
            var changed=new List<string>();
            foreach(string path in paths)
            {
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null||importer.isReadable)continue;
                string target=backup+"/"+AssetDatabase.AssetPathToGUID(path)+".meta";
                if(!File.Exists(target))File.Copy(path+".meta",target);
                importer.isReadable=true;importer.SaveAndReimport();changed.Add(path);
            }
            File.WriteAllText(ClaudeMapIntegration.Stage+"/collider_readability_changes.json",Newtonsoft.Json.JsonConvert.SerializeObject(changed,Newtonsoft.Json.Formatting.Indented));
            Debug.Log("[ClaudeMap] Enabled Read/Write on "+changed.Count+" existing collider FBX importers; original meta backed up; runtime code untouched.");
        }
    }
}



