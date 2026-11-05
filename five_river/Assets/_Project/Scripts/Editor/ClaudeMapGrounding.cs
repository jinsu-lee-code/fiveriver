using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace FiveRivers.EditorTools {
public static class ClaudeMapGrounding {
const string Folder=ClaudeMapIntegration.Art+"/GroundedSandbags";
[MenuItem("FiveRivers/Map/Claude Ground Sandbag Visuals")]
public static void Apply(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play.");
 var root=GameObject.Find("/[Map]/Dabudong_Imported");var terrain=root.GetComponentInChildren<Terrain>();
 Directory.CreateDirectory(Folder);AssetDatabase.Refresh();var reports=new List<object>();int bag=0;
 foreach(var lod in root.GetComponentsInChildren<LODGroup>(true)){
 var levels=lod.GetLODs();if(levels.Length==0)continue;
 var render=levels[0].renderers.FirstOrDefault(r=>r!=null&&r.name.StartsWith("Sandbag_Wall"));if(render==null)continue;
 if(lod.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Unexpected visual collider.");
 var owner=lod.transform;while(owner!=null&&!owner.name.Contains("_Sandbags_"))owner=owner.parent;
 if(owner==null)throw new InvalidOperationException("Missing cover identity.");
 string cp=owner.name.Split('_')[0];var cover=root.transform.Find("M01_Greybox/Cover/"+cp).GetComponent<BoxCollider>();
 string prefix=Folder+"/Bag_"+bag.ToString("D2");
 var primary=render.GetComponent<MeshFilter>();
 var baseMesh=Baseline(primary.sharedMesh,prefix+"_LOD0_Source.asset");
 // Source FBX is Z-up. Its importer rotates X=-90; the old scene had cancelled that rotation.
 var orientation=Quaternion.Euler(-90,0,0);
 var oriented=baseMesh.vertices.Select(v=>orientation*v).ToArray();
 var sourceBounds=new Bounds(oriented[0],Vector3.zero);foreach(var v in oriented)sourceBounds.Encapsulate(v);
 var size=cover.size;var centre=cover.center;
 var scale=new Vector3(size.x/sourceBounds.size.x,size.y/sourceBounds.size.y,size.z/sourceBounds.size.z);
 int levelIndex=0;
 foreach(var level in levels){
 foreach(var renderer in level.renderers){
 if(renderer==null)continue;var filter=renderer.GetComponent<MeshFilter>();if(filter==null)continue;
 string id=prefix+"_LOD"+levelIndex;
 var source=Baseline(filter.sharedMesh,id+"_Source.asset");
 var mesh=Object.Instantiate(source);mesh.name="Sandbag_ClaudeUpright_"+bag+"_LOD"+levelIndex;
 var vertices=mesh.vertices;var normals=mesh.normals;
 for(int i=0;i<vertices.Length;i++){
 var p=orientation*vertices[i];
 var local=centre+Vector3.Scale(p-sourceBounds.center,scale);
 var world=cover.transform.TransformPoint(local);
 float t=(p.y-sourceBounds.min.y)/sourceBounds.size.y;
 // Conform only the lower course; preserve the original cover's top and firing envelope.
 float blend=1-Mathf.SmoothStep(0,1,t/.28f);
 var bottom=cover.transform.TransformPoint(new Vector3(local.x,centre.y-size.y*.5f,local.z));
 world.y+=(Ground(terrain,world)-.015f-bottom.y)*blend;
 vertices[i]=filter.transform.InverseTransformPoint(world);
 if(normals.Length==vertices.Length){
 var n=orientation*normals[i];n=new Vector3(n.x/scale.x,n.y/scale.y,n.z/scale.z);
 n=cover.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(n);
 normals[i]=filter.transform.localToWorldMatrix.transpose.MultiplyVector(n).normalized;
 }
 }
 mesh.vertices=vertices;if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();
 mesh.RecalculateTangents();mesh.RecalculateBounds();
 string destination=id+"_Grounded.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(destination);
 if(existing==null)AssetDatabase.CreateAsset(mesh,destination);
 else{EditorUtility.CopySerialized(mesh,existing);existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.tangents=mesh.tangents;existing.RecalculateBounds();existing.UploadMeshData(false);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);mesh=existing;}
 filter.sharedMesh=mesh;EditorUtility.SetDirty(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
 }
 levelIndex++;
 }
 lod.RecalculateBounds();EditorUtility.SetDirty(lod);
 var worldVertices=primary.sharedMesh.vertices.Select(primary.transform.TransformPoint).ToArray();
 var contacts=Enumerable.Range(0,oriented.Length).Where(i=>oriented[i].y<=sourceBounds.min.y+sourceBounds.size.y*.02f)
 .Select(i=>worldVertices[i].y-Ground(terrain,worldVertices[i])).OrderBy(v=>v).ToArray();
 reports.Add(new{cp,index=bag,orientationRestored=true,sourceSizeMetres=(sourceBounds.size*100).ToString("R"),
 visibleTop=worldVertices.Max(v=>v.y),colliderTop=cover.bounds.max.y,topDifference=worldVertices.Max(v=>v.y)-cover.bounds.max.y,
 contactMin=contacts.First(),contactMedian=contacts[contacts.Length/2],contactMax=contacts.Last(),
 originalCoverCentre=cover.center.ToString("R"),originalCoverSize=cover.size.ToString("R"),colliderComponentsChanged=0,placementRootUnchanged=true});
 bag++;
 }
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveOpenScenes();
 File.WriteAllText(ClaudeMapIntegration.Stage+"/sandbag_grounding.json",Newtonsoft.Json.JsonConvert.SerializeObject(reports,Newtonsoft.Json.Formatting.Indented));
 Debug.Log("[ClaudeMap] Upright source sandbags fit to "+bag+" original cover envelopes; lower course grounded, collider/root unchanged.");
}
static float Ground(Terrain t,Vector3 p)=>t.SampleHeight(p)+t.transform.position.y;
static Mesh Baseline(Mesh source,string path){
 var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
 mesh=Object.Instantiate(source);mesh.name=Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(mesh,path);return mesh;
}
}}

