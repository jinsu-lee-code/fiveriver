using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    public static class BlanketSurfaceDetail
    {
        public static void Apply()
        {
            const string sourcePath="Assets/_Project/Art/Maps/Dabudong/Source/Art/Realistic/Meshes/RL_Blanket.asset";
            const string outputPath="Assets/_Project/Art/SurfaceDetail20261030/RL_Blanket_Draped.asset";
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
            if(source==null)return;
            var mesh=Object.Instantiate(source);mesh.name="RL_Blanket_Draped";
            var vertices=mesh.vertices.ToList();var uv=mesh.uv.ToList();var triangles=mesh.triangles.ToList();
            for(int pass=0;pass<2;pass++)
            {
                var edges=new Dictionary<(int,int),int>();var refined=new List<int>();
                int Mid(int a,int b)
                {
                    var key=(Mathf.Min(a,b),Mathf.Max(a,b));
                    if(edges.TryGetValue(key,out int index))return index;
                    index=vertices.Count;vertices.Add((vertices[a]+vertices[b])*.5f);uv.Add((uv[a]+uv[b])*.5f);edges.Add(key,index);return index;
                }
                for(int i=0;i<triangles.Count;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2],ab=Mid(a,b),bc=Mid(b,c),ca=Mid(c,a);
                    refined.AddRange(new[]{a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca});
                }
                triangles=refined;
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            WeldNormals(mesh);
            var normals=mesh.normals;var bounds=source.bounds;
            for(int i=0;i<vertices.Count;i++)
            {
                var p=vertices[i];float x=(p.x-bounds.center.x)/bounds.extents.x,z=(p.z-bounds.center.z)/bounds.extents.z;
                float height=Mathf.InverseLerp(bounds.min.y,bounds.max.y,p.y);
                float fade=Mathf.SmoothStep(0f,1f,height)*Mathf.SmoothStep(0f,1f,(1f-Mathf.Abs(z))/.18f);
                float fold=(Mathf.Sin(z*15f+x*5f)+.3f*Mathf.Sin(z*29f-x*8f))*.025f*fade;
                vertices[i]=p+normals[i]*fold;
            }
            mesh.SetVertices(vertices);mesh.RecalculateNormals();WeldNormals(mesh);mesh.RecalculateTangents();mesh.RecalculateBounds();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);
            if(existing==null){AssetDatabase.CreateAsset(mesh,outputPath);existing=mesh;}
            else{EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);}
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(filter.sharedMesh==source||filter.sharedMesh==existing){filter.sharedMesh=existing;EditorUtility.SetDirty(filter);}
        }
        static void WeldNormals(Mesh mesh)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;
            var sums=new Dictionary<Vector3Int,Vector3>();
            for(int i=0;i<vertices.Length;i++){var key=Vector3Int.RoundToInt(vertices[i]*100000f);sums[key]=(sums.TryGetValue(key,out var sum)?sum:Vector3.zero)+normals[i];}
            for(int i=0;i<vertices.Length;i++)normals[i]=sums[Vector3Int.RoundToInt(vertices[i]*100000f)].normalized;
            mesh.normals=normals;
        }
    }
}
