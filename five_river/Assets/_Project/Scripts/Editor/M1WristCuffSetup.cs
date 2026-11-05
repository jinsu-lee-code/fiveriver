using System;
using System.Collections.Generic;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Fit cloth to the actual delivered wrist boundaries. Hand and weapon source meshes stay intact.
    public static class M1WristCuffSetup
    {
        [MenuItem("FiveRivers/Setup/Fit First Person Wrist Cuffs")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var rig = Object.FindFirstObjectByType<M1ArmRig>(FindObjectsInactive.Include);
            if (rig == null) throw new InvalidOperationException("M1 arm rig missing.");
            Configure(rig);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            EditorSceneManager.SaveScene(rig.gameObject.scene);
        }

        public static void Configure(M1ArmRig rig)
        {
            var data = new SerializedObject(rig);
            var support = rig.transform.parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SupportHand_M1");
            if (support != null)
            {
                var vertices = support.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.sharedMesh != null)
                    .SelectMany(f => f.sharedMesh.vertices.Select(v => support.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
                // Blender's wrist cut imports on local -Z, unlike the skeletal hand's -Y.
                Fit(data, "left", support, vertices, Vector3.forward);
                var target = data.FindProperty("leftTarget").objectReferenceValue as Transform;
                if(target != null)
                    target.position = support.TransformPoint(data.FindProperty("leftSkinCap").vector3Value);
            }
            var right = data.FindProperty("rightVisualWrist").objectReferenceValue as Transform;
            var skin = rig.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.bones.Contains(right));
            if (right == null || skin == null) throw new InvalidOperationException("Corrected right hand skin missing.");
            int index = Array.IndexOf(skin.bones, right);
            // Always derive from the untrimmed reviewed hand, never from a prior trim.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Animations/FP/MixamoSource/M1_AdultHandProportions.asset");
            var weights = mesh.boneWeights;
            var bind = mesh.bindposes[index];
            // The hand is closed with a slanted, rounded end. Its lowest vertex is
            // not a wrist circumference. Intersect the surface above that end cap.
            var local = mesh.vertices.Select(v => bind.MultiplyPoint3x4(v)).ToArray();
            float cut = local.Where((v,i)=>weights[i].boneIndex0==index && weights[i].weight0>.5f).Min(v=>v.y)+.024f;
            var section = new List<Vector3>(); var triangles = mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                if(!Enumerable.Range(0,3).Any(j=>weights[triangles[i+j]].boneIndex0==index && weights[triangles[i+j]].weight0>.5f))continue;
                for(int j=0;j<3;j++)
                {
                    var a=local[triangles[i+j]];var b=local[triangles[i+(j+1)%3]];
                    if((a.y<cut)!=(b.y<cut))section.Add(Vector3.Lerp(a,b,(cut-a.y)/(b.y-a.y)));
                }
            }
            var rightVertices = section.ToArray();
            Fit(data, "right", right, rightVertices, Vector3.up);
            skin.sharedMesh=null;
            skin.sharedMesh=TrimEnd(mesh,index,cut);
            EditorUtility.SetDirty(skin);
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(rig);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
        }

        static void Fit(SerializedObject data, string side, Transform anchor, Vector3[] vertices, Vector3 axis)
        {
            if (vertices.Length < 8) throw new InvalidOperationException("No measured wrist vertices: " + side);
            float low = vertices.Min(v => Vector3.Dot(v,axis));
            var band = vertices.Where(v => Vector3.Dot(v,axis) <= low + .0035f).ToArray();
            var centre = band.Aggregate(Vector3.zero, (sum, v) => sum + v) / band.Length;
            centre += axis*(low-Vector3.Dot(centre,axis));
            var uAxis=Vector3.right;var vAxis=Vector3.Cross(axis,uAxis);
            var outline = Hull(band.Select(v => new Vector2(Vector3.Dot(v-centre,uAxis),Vector3.Dot(v-centre,vAxis))).ToArray());
            if (outline.Count < 3) throw new InvalidOperationException("Invalid wrist boundary: " + side);
            var rim = data.FindProperty(side + "CuffRim");
            rim.arraySize = 32;
            float radius = 0f;
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = float.PositiveInfinity;
                for (int j = 0; j < outline.Count; j++)
                {
                    var a = outline[j]; var edge = outline[(j + 1) % outline.Count] - a;
                    float determinant = Cross(direction, edge);
                    if (Mathf.Abs(determinant) < 1e-7f) continue;
                    float t = Cross(a, edge) / determinant;
                    float u = Cross(a, direction) / determinant;
                    if (t > 0f && u >= -.0001f && u <= 1.0001f) distance = Mathf.Min(distance, t);
                }
                if (float.IsInfinity(distance)) throw new InvalidOperationException("Wrist ray missed: " + side);
                var p = centre + (uAxis*direction.x+vAxis*direction.y) * distance;
                rim.GetArrayElementAtIndex(i).vector3Value = p;
                radius = Mathf.Max(radius, anchor.TransformVector(p - centre).magnitude);
            }
            data.FindProperty(side + "CuffAnchor").objectReferenceValue = anchor;
            data.FindProperty(side + "CuffAxis").vector3Value = axis;
            data.FindProperty(side + "CuffOverlap").floatValue = side=="left"?.008f:0f;
            data.FindProperty(side + "SkinCap").vector3Value = centre;
            data.FindProperty(side + "SkinRadius").floatValue = radius;
        }

        struct Vertex
        {
            public Vector3 p,n; public Vector4 t; public Vector2 uv; public Color color; public BoneWeight weight;
        }

        static Mesh TrimEnd(Mesh source,int wrist,float cut)
        {
            var p=source.vertices;var n=source.normals;var t=source.tangents;var uv=source.uv;var c=source.colors;var w=source.boneWeights;
            var output=new List<Vertex>();var indices=new List<int>();var plane=source.bindposes[wrist];
            Vertex Read(int i)=>new Vertex{p=p[i],n=n[i],t=t[i],uv=uv[i],color=c[i],weight=w[i]};
            Vertex Lerp(Vertex a,Vertex b,float f)
            {
                // Only rigid wrist vertices cross this cut, below finger articulation.
                return new Vertex{p=Vector3.Lerp(a.p,b.p,f),n=Vector3.Lerp(a.n,b.n,f).normalized,t=Vector4.Lerp(a.t,b.t,f),uv=Vector2.Lerp(a.uv,b.uv,f),color=Color.Lerp(a.color,b.color,f),weight=new BoneWeight{boneIndex0=wrist,weight0=1}};
            }
            var triangles=source.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var poly=new[]{Read(triangles[i]),Read(triangles[i+1]),Read(triangles[i+2])};
                var clipped=new List<Vertex>();
                bool trim=poly.Any(v=>v.weight.boneIndex0==wrist && v.weight.weight0>.5f);
                if(!trim)clipped.AddRange(poly);
                else for(int j=0;j<3;j++)
                {
                    var a=poly[j];var b=poly[(j+1)%3];float da=plane.MultiplyPoint3x4(a.p).y,db=plane.MultiplyPoint3x4(b.p).y;
                    if(da>=cut)clipped.Add(a);
                    if((da>=cut)!=(db>=cut))clipped.Add(Lerp(a,b,(cut-da)/(db-da)));
                }
                int start=output.Count;output.AddRange(clipped);
                for(int j=1;j<clipped.Count-1;j++)indices.AddRange(new[]{start,start+j,start+j+1});
            }
            var mesh=new Mesh{name="M1_HandCuffTrim",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(output.Select(v=>v.p).ToList());mesh.SetNormals(output.Select(v=>v.n).ToList());mesh.SetTangents(output.Select(v=>v.t).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());mesh.SetColors(output.Select(v=>v.color).ToList());
            mesh.boneWeights=output.Select(v=>v.weight).ToArray();mesh.bindposes=source.bindposes;mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            const string path="Assets/_Project/Art/Animations/FP/MixamoSource/M1_HandCuffTrim.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            // Notify skinning of the changed vertex layout. CopySerialized alone
            // leaves the old GPU skinning buffer cached when the cut size changes.
            existing.Clear(false);existing.indexFormat=mesh.indexFormat;
            existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.tangents=mesh.tangents;existing.uv=mesh.uv;existing.colors=mesh.colors;
            existing.boneWeights=mesh.boneWeights;existing.bindposes=mesh.bindposes;existing.triangles=mesh.triangles;existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static List<Vector2> Hull(Vector2[] points)
        {
            var sorted = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
            var hull = new List<Vector2>();
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count;
            for (int i = sorted.Length - 2; i >= 0; i--)
            {
                var p = sorted[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }
    }
}
