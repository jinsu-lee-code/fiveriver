using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // A non-destructive mesh refinement of the exact art worktree delivery.
    public static class M1MeshRefinement
    {
        const string Dir = "Assets/_Project/Art/Models/Weapons/M1Garand/Refined";
        public static void Apply(GameObject visual)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Models/Weapons/M1Garand", "Refined");
            var body = visual.GetComponentsInChildren<MeshFilter>().First(x => x.name == "Body");
            var source = body.sharedMesh;
            var vertices = source.vertices;
            var refined = Object.Instantiate(source);
            refined.name = "M1_ArtBody_CleanAperture";
            int removed = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var src = source.GetTriangles(s);
                var keep = new List<int>();
                for (int i=0; i<src.Length; i+=3)
                {
                    bool Plate(Vector3 p) => Mathf.Abs(p.x) < .0062f && p.y > .0779f && p.z > .1079f && p.z < .1105f;
                    if (Plate(vertices[src[i]]) && Plate(vertices[src[i+1]]) && Plate(vertices[src[i+2]])) { removed++; continue; }
                    keep.Add(src[i]); keep.Add(src[i+1]); keep.Add(src[i+2]);
                }
                refined.SetTriangles(keep, s);
            }
            if (removed < 30) { Object.DestroyImmediate(refined); throw new System.InvalidOperationException("Art aperture topology did not match the reviewed delivery."); }
            body.sharedMesh = SaveMesh(refined, Dir + "/M1_ArtBody_Refined.asset");
            var aperture = new GameObject("ArtM1_CleanRearAperture");
            aperture.transform.SetParent(body.transform, false);
            var filter = aperture.AddComponent<MeshFilter>();
            var verts = new List<Vector3>();
            var indices = new List<int>();
            const int n = 64;
            Vector3 P(float radius, int i, float z) { float a=i*Mathf.PI*2f/n;return new Vector3(Mathf.Cos(a)*radius,.08769f+Mathf.Sin(a)*radius,z); }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int k=verts.Count;verts.AddRange(new[]{a,b,c,d});indices.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
            }
            // Independent flat faces, inner bore, narrow edge bevels and outer cylindrical surface.
            float centre=.10926f, front=centre-.0007f, back=centre+.0007f;
            for(int i=0;i<n;i++)
            {
                int j=i+1;
                Quad(P(.00145f,i,front),P(.00145f,j,front),P(.00510f,j,front),P(.00510f,i,front));
                Quad(P(.00510f,i,back),P(.00510f,j,back),P(.00145f,j,back),P(.00145f,i,back));
                Quad(P(.00510f,i,front),P(.00510f,j,front),P(.00525f,j,front+.00015f),P(.00525f,i,front+.00015f));
                Quad(P(.00525f,i,front+.00015f),P(.00525f,j,front+.00015f),P(.00525f,j,back-.00015f),P(.00525f,i,back-.00015f));
                Quad(P(.00525f,i,back-.00015f),P(.00525f,j,back-.00015f),P(.00510f,j,back),P(.00510f,i,back));
                Quad(P(.00145f,i,back),P(.00145f,j,back),P(.00130f,j,back-.00015f),P(.00130f,i,back-.00015f));
                Quad(P(.00130f,i,back-.00015f),P(.00130f,j,back-.00015f),P(.00130f,j,front+.00015f),P(.00130f,i,front+.00015f));
                Quad(P(.00130f,i,front+.00015f),P(.00130f,j,front+.00015f),P(.00145f,j,front),P(.00145f,i,front));
            }
            // Retain the art silhouette's narrow stem while replacing the radial triangulation above it.
            var a=new Vector3(-.0025f,.07795f,front);var b=new Vector3(.0025f,.07795f,front);
            var c=new Vector3(.0025f,.08310f,front);var d=new Vector3(-.0025f,.08310f,front);
            Quad(a,b,c,d);a.z=b.z=c.z=d.z=back;Quad(d,c,b,a);
            var ring = new Mesh {name="M1_ArtRearAperture_Bevelled"};
            ring.SetVertices(verts);ring.SetTriangles(indices,0);ring.RecalculateNormals();ring.RecalculateBounds();
            filter.sharedMesh=SaveMesh(ring,Dir+"/M1_RearAperture_Refined.asset");
            var renderer=aperture.AddComponent<MeshRenderer>();
            string matPath=Dir+"/M1_Aperture_Parkerized.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null) {material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="M1_Aperture_Parkerized"};AssetDatabase.CreateAsset(material,matPath);}
            material.SetColor("_BaseColor",new Color(.065f,.074f,.078f));material.SetFloat("_Metallic",.85f);material.SetFloat("_Smoothness",.28f);
            renderer.sharedMaterial=material;
            EditorUtility.SetDirty(material);
            Debug.Log("[M1 mesh refinement] Original art body triangles removed from damaged aperture: "+removed+"; clean bevelled aperture triangles: "+indices.Count/3);
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }
    }
}
