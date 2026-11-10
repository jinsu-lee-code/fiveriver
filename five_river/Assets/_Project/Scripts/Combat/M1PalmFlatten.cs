using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // The right hand mesh is modelled gripping the stock, so its palm is cupped. When the reload opens the hand that
    // hollow shows as a dark groove. This adds a corrective blend shape that fills only the hollows of the right palm
    // (convex skin is left alone); the reload turns it on while the hand is open, the grip never uses it.
    public static class M1PalmFlatten
    {
        public const string ShapeName = "ReloadPalmFlat";
        static readonly string[] PalmBones = { "metacarpal1.R", "metacarpal2.R", "metacarpal3.R", "metacarpal4.R" };

        // Returns the blend shape index on the renderer's (instanced) mesh, or -1 when the mesh has no right palm bones.
        public static int Add(SkinnedMeshRenderer skin, int iterations = 40)
        {
            if (skin == null || skin.sharedMesh == null) return -1;
            var source = skin.sharedMesh;
            int existing = source.GetBlendShapeIndex(ShapeName);
            if (existing >= 0) return existing;
            var palm = new HashSet<int>();
            for (int b = 0; b < skin.bones.Length; b++)
                if (skin.bones[b] != null && System.Array.IndexOf(PalmBones, skin.bones[b].name) >= 0) palm.Add(b);
            if (palm.Count == 0) return -1;

            var verts = source.vertices; var normals = source.normals; var tris = source.triangles; var weights = source.boneWeights;
            int n = verts.Length;
            if (weights.Length != n || normals.Length != n) return -1;
            // how much each vertex belongs to the palm, softened toward its edge
            var mask = new float[n];
            for (int i = 0; i < n; i++)
            {
                var w = weights[i]; float s = 0;
                if (palm.Contains(w.boneIndex0)) s += w.weight0; if (palm.Contains(w.boneIndex1)) s += w.weight1;
                if (palm.Contains(w.boneIndex2)) s += w.weight2; if (palm.Contains(w.boneIndex3)) s += w.weight3;
                mask[i] = Mathf.Clamp01((s - .35f) / .4f);
            }
            // UV seams split vertices: treat vertices at the same position as one so the smoothing never tears them
            var weld = new int[n]; var byPos = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < n; i++)
            {
                var key = Vector3Int.RoundToInt(verts[i] * 20000f);
                if (!byPos.TryGetValue(key, out weld[i])) { weld[i] = i; byPos.Add(key, i); }
            }
            var neighbours = new List<int>[n];
            for (int t = 0; t < tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = weld[tris[t + k]], b = weld[tris[t + (k + 1) % 3]];
                    (neighbours[a] ??= new List<int>()).Add(b); (neighbours[b] ??= new List<int>()).Add(a);
                }
            var pos = (Vector3[])verts.Clone();
            for (int it = 0; it < iterations; it++)
            {
                var next = (Vector3[])pos.Clone();
                for (int i = 0; i < n; i++)
                {
                    if (weld[i] != i || mask[i] <= 0 || neighbours[i] == null) continue;
                    var avg = Vector3.zero; foreach (var j in neighbours[i]) avg += pos[j]; avg /= neighbours[i].Count;
                    var step = (avg - pos[i]) * .5f * mask[i];
                    // fill only: move a vertex out along its normal, never pull skin inward
                    float outward = Vector3.Dot(step, normals[i]);
                    if (outward > 0) next[i] = pos[i] + normals[i] * outward;
                }
                pos = next;
            }
            var delta = new Vector3[n];
            for (int i = 0; i < n; i++) delta[i] = pos[weld[i]] - verts[weld[i]];

            // normal change measured the same way on both shapes, so the mesh's own seams and smoothing cancel out
            var flatNormals = Recalculated(source.indexFormat, verts, delta, tris, weld, true);
            var restNormals = Recalculated(source.indexFormat, verts, delta, tris, weld, false);
            var dn = new Vector3[n];
            for (int i = 0; i < n; i++) if (delta[i].sqrMagnitude > 0) dn[i] = flatNormals[i] - restNormals[i];

            var mesh = Object.Instantiate(source); mesh.name = source.name + "_PalmFlat";
            mesh.AddBlendShapeFrame(ShapeName, 100f, delta, dn, null);
            skin.sharedMesh = mesh;
            return mesh.GetBlendShapeIndex(ShapeName);
        }

        static Vector3[] Recalculated(UnityEngine.Rendering.IndexFormat format, Vector3[] verts, Vector3[] delta, int[] tris, int[] weld, bool flat)
        {
            // area-weighted normals summed over welded vertices, so split seam vertices share one smooth normal
            var v = new Vector3[verts.Length];
            for (int i = 0; i < v.Length; i++) v[i] = verts[weld[i]] + (flat ? delta[i] : Vector3.zero);
            var sum = new Vector3[verts.Length];
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                sum[weld[a]] += fn; sum[weld[b]] += fn; sum[weld[c]] += fn;
            }
            var result = new Vector3[verts.Length];
            for (int i = 0; i < v.Length; i++) result[i] = sum[weld[i]].normalized;
            return result;
        }
    }
}
