using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace FiveRivers.EditorTools {
public static partial class ClaudeMapIntegration {
public static void VerifyPersistedTerrain() {
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play.");
 AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
 string scene=ActiveTerrain().gameObject.scene.path;
 AssetDatabase.ImportAsset(Derived,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
 EditorSceneManager.OpenScene(scene);
 var t=ActiveTerrain();var d=t.terrainData;
 var info=JsonUtility.FromJson<Info>(File.ReadAllText(Map+"/map.json"));
 var source=AssetDatabase.LoadAssetAtPath<TerrainData>(info.terrainAssetPath);
 var original=source.GetHeights(0,0,info.heightRes,info.heightRes);
 var actual=d.GetHeights(0,0,info.heightRes,info.heightRes);
 var mask=File.ReadAllBytes(Map+"/out/protection_mask.u8");
 int protectedCount=0,heightMismatch=0,holesMismatch=0,detailMismatch=0;
 for(int z=0;z<info.heightRes;z++)for(int x=0;x<info.heightRes;x++)
 if(mask[z*info.heightRes+x]!=0){protectedCount++;if(original[z,x]!=actual[z,x])heightMismatch++;}
 var a=d.GetAlphamaps(0,0,d.alphamapWidth,d.alphamapHeight);
 var p=Load(Map+"/out/splat.png");var q=Load(Map+"/out/splat2.png");
 var p0=p.GetPixels();var p1=q.GetPixels();double[] sums=new double[5];float maxError=0;
 for(int z=0;z<d.alphamapHeight;z++)for(int x=0;x<d.alphamapWidth;x++){
 int n=z*d.alphamapWidth+x;float[] v={p0[n].r,p0[n].g,p0[n].b,p0[n].a,p1[n].r};float total=v.Sum();
 for(int k=0;k<5;k++){sums[k]+=a[z,x,k];maxError=Mathf.Max(maxError,Mathf.Abs(a[z,x,k]-v[k]/total));}}
 UnityEngine.Object.DestroyImmediate(p);UnityEngine.Object.DestroyImmediate(q);
 var h0=source.GetHoles(0,0,source.holesResolution,source.holesResolution);
 var h1=d.GetHoles(0,0,d.holesResolution,d.holesResolution);
 for(int z=0;z<source.holesResolution;z++)for(int x=0;x<source.holesResolution;x++)if(h0[z,x]!=h1[z,x])holesMismatch++;
 for(int k=0;k<source.detailPrototypes.Length;k++){
 var b=source.GetDetailLayer(0,0,source.detailWidth,source.detailHeight,k);
 var c=d.GetDetailLayer(0,0,d.detailWidth,d.detailHeight,k);
 for(int z=0;z<source.detailHeight;z++)for(int x=0;x<source.detailWidth;x++)if(b[z,x]!=c[z,x])detailMismatch++;}
 bool trees=source.treeInstances.SequenceEqual(d.treeInstances);
 bool treePrototypes=source.treePrototypes.Length==d.treePrototypes.Length;
 for(int i=0;i<source.treePrototypes.Length && treePrototypes;i++)
 treePrototypes=source.treePrototypes[i].prefab==d.treePrototypes[i].prefab&&source.treePrototypes[i].bendFactor==d.treePrototypes[i].bendFactor;
 bool detailPrototypes=source.detailPrototypes.Length==d.detailPrototypes.Length;
 for(int i=0;i<source.detailPrototypes.Length&&detailPrototypes;i++)
 detailPrototypes=JsonUtility.ToJson(source.detailPrototypes[i])==JsonUtility.ToJson(d.detailPrototypes[i]);
 bool hash=Hash(info.terrainAssetPath)==info.terrainSha256;
 bool collider=t.GetComponent<TerrainCollider>().terrainData==d;
 var result=new{savedAndForceImportedAndSceneReopened=true,protectedCount,heightMismatch,alphaLayers=a.GetLength(2),alphaSums=sums,alphaMaxError=maxError,holesMismatch,detailMismatch,trees,treePrototypes,detailPrototypes,sourceHashPreserved=hash,renderColliderSameData=collider};
 File.WriteAllText(Stage+"/terrain_persisted_verification.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
 if(heightMismatch!=0||maxError>.012f||holesMismatch!=0||detailMismatch!=0||!trees||!treePrototypes||!detailPrototypes||!hash||!collider)throw new InvalidOperationException("Persisted terrain validation failed; inspect report.");
 Debug.Log("[ClaudeMap] Saved/reimported/reopened terrain verified.");
}
}}
