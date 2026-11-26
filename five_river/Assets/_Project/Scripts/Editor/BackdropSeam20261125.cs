using System.IO;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-25 (사용자 지시 "… 나머지 디테일에 조금더 신경써봐"): seen from the summit at dawn, the land round the map
    // (Ridges_Continuous_Backdrop) stood up to 8 m above the terrain's edge, so the terrain read as a sunken box with a wall
    // round it — a flat card with the trench's poplars in it, right behind Tae-seok while he talks. The backdrop's ground
    // within SeamWidth of the terrain is let down to the terrain's own edge height, easing back to its own shape further out.
    // The untouched mesh is kept once (…_Original.asset) and every run starts from it, so re-running gives the same result.
    public static class BackdropSeam20261125
    {
        const string MeshPath = "Assets/_Project/Art/Maps/DabudongRefined/Source/Art/TerrainRidges20261023/Ridges_Backdrop.asset";
        const string OriginalPath = "Assets/_Project/Art/Maps/DabudongRefined/Source/Art/TerrainRidges20261023/Ridges_Backdrop_Original.asset";
        const float SeamWidth = 45f, Sink = .15f;

        [MenuItem("FiveRivers/Setup/Backdrop Seam 20261125")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var go = GameObject.Find("Ridges_Continuous_Backdrop");
            var terrain = Terrain.activeTerrain;
            if (mesh == null || go == null || terrain == null) throw new System.InvalidOperationException("구릉 메시 · 지형을 찾지 못함");
            var original = AssetDatabase.LoadAssetAtPath<Mesh>(OriginalPath);
            if (original == null) { original = Object.Instantiate(mesh); original.name = mesh.name + "_Original"; AssetDatabase.CreateAsset(original, OriginalPath); }
            var tr = go.transform;
            var tp = terrain.transform.position; var ts = terrain.terrainData.size;
            // URP works the distance fog out at the corners of a triangle and blends it across: the backdrop's triangles
            // ran hundreds of metres, so the ground just past the terrain took the thick fog of a corner far away and
            // stood out as a pale sheet round a dark card (seen from the summit at dawn). Near the map the long
            // triangles are split until no side is longer than the fog can bend across (finer the nearer).
            var work = Object.Instantiate(original);
            int splits = Subdivide(work, p =>
            {
                var w = tr.TransformPoint(p);
                float cx = Mathf.Clamp(w.x, tp.x, tp.x + ts.x), cz = Mathf.Clamp(w.z, tp.z, tp.z + ts.z);
                return Mathf.Sqrt((w.x - cx) * (w.x - cx) + (w.z - cz) * (w.z - cz));
            });
            var src = work.vertices;
            var dst = new Vector3[src.Length];
            int moved = 0; float most = 0f;
            for (int i = 0; i < src.Length; i++)
            {
                var w = tr.TransformPoint(src[i]);
                float cx = Mathf.Clamp(w.x, tp.x, tp.x + ts.x), cz = Mathf.Clamp(w.z, tp.z, tp.z + ts.z);
                float d = Mathf.Sqrt((w.x - cx) * (w.x - cx) + (w.z - cz) * (w.z - cz));
                dst[i] = src[i];
                if (d >= SeamWidth) continue;
                // the terrain's own height at its edge, a hair below so the two never fight over the same pixels
                float edge = terrain.SampleHeight(new Vector3(cx, 0f, cz)) + tp.y - Sink;
                float t = d / SeamWidth; t = t * t * (3f - 2f * t);
                float y = Mathf.Lerp(edge, w.y, t);
                if (y >= w.y) continue;   // only ever let it down
                most = Mathf.Max(most, w.y - y);
                w.y = y; dst[i] = tr.InverseTransformPoint(w); moved++;
            }
            Undo.RecordObject(mesh, "Backdrop Seam");
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = dst;
            if (work.uv.Length == dst.Length) mesh.uv = work.uv;
            if (work.uv2.Length == dst.Length) mesh.uv2 = work.uv2;
            if (work.colors.Length == dst.Length) mesh.colors = work.colors;
            mesh.subMeshCount = work.subMeshCount;
            for (int sm = 0; sm < work.subMeshCount; sm++) mesh.SetTriangles(work.GetTriangles(sm), sm);
            Object.DestroyImmediate(work);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            // the colour: the backdrop's tint is kept at its own (0.76, 0.72, 0.65): its texture already comes out at the terrain's
            // mean colour (the fog still pales it with distance)
            var bm = go.GetComponent<Renderer>().sharedMaterial; var c0 = new Color(.76f, .72f, .65f); bm.SetColor("_BaseColor", c0); bm.SetColor("_Color", c0); EditorUtility.SetDirty(bm);
            var msg = $"backdrop seam: {splits} long sides split near the map ({original.vertexCount} -> {dst.Length} vertices), {moved} vertices let down within {SeamWidth} m of the terrain (most {most:0.0} m), from {OriginalPath}; colour unchanged (the texture already comes out at the terrain mean)";
            File.WriteAllText("Temp/backdrop_seam.txt", msg);
            Debug.Log("[BackdropSeam20261125] " + msg);
            return msg;
        }

        // longest-side splitting with shared midpoints (no cracks); a side may be at most MaxSide(distance from the map)
        static float MaxSide(float d) => d > 900f ? float.MaxValue : 5f + d * .08f;

        static int Subdivide(Mesh m, System.Func<Vector3, float> distance)
        {
            var v = new System.Collections.Generic.List<Vector3>(m.vertices);
            var uv = new System.Collections.Generic.List<Vector2>(m.uv);
            var uv2 = new System.Collections.Generic.List<Vector2>(m.uv2);
            var col = new System.Collections.Generic.List<Color>(m.colors);
            var mids = new System.Collections.Generic.Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mids.TryGetValue(key, out int i)) return i;
                v.Add((v[a] + v[b]) * .5f);
                if (uv.Count > 0) uv.Add((uv[a] + uv[b]) * .5f);
                if (uv2.Count > 0) uv2.Add((uv2[a] + uv2[b]) * .5f);
                if (col.Count > 0) col.Add((col[a] + col[b]) * .5f);
                mids[key] = v.Count - 1; return v.Count - 1;
            }
            int total = 0;
            var subs = new System.Collections.Generic.List<int>[m.subMeshCount];
            for (int sm = 0; sm < m.subMeshCount; sm++) subs[sm] = new System.Collections.Generic.List<int>(m.GetTriangles(sm));
            for (int pass = 0; pass < 60; pass++)
            {
                int split = 0;
                for (int sm = 0; sm < subs.Length; sm++)
                {
                    var t = subs[sm]; var next = new System.Collections.Generic.List<int>(t.Count);
                    for (int i = 0; i < t.Count; i += 3)
                    {
                        int a = t[i], b = t[i + 1], c = t[i + 2];
                        float ab = (v[a] - v[b]).sqrMagnitude, bc = (v[b] - v[c]).sqrMagnitude, ca = (v[c] - v[a]).sqrMagnitude;
                        float d = Mathf.Min(distance(v[a]), Mathf.Min(distance(v[b]), distance(v[c])));
                        float lim = MaxSide(d); lim *= lim;
                        if (Mathf.Max(ab, Mathf.Max(bc, ca)) <= lim) { next.Add(a); next.Add(b); next.Add(c); continue; }
                        split++;
                        if (ab >= bc && ab >= ca) { int x = Mid(a, b); next.AddRange(new[] { a, x, c, x, b, c }); }
                        else if (bc >= ca) { int x = Mid(b, c); next.AddRange(new[] { a, b, x, a, x, c }); }
                        else { int x = Mid(c, a); next.AddRange(new[] { a, b, x, x, b, c }); }
                    }
                    subs[sm] = next;
                }
                total += split;
                if (split == 0) break;
            }
            m.Clear();
            m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v);
            if (uv.Count == v.Count) m.SetUVs(0, uv);
            if (uv2.Count == v.Count) m.SetUVs(1, uv2);
            if (col.Count == v.Count) m.SetColors(col);
            m.subMeshCount = subs.Length;
            for (int sm = 0; sm < subs.Length; sm++) m.SetTriangles(subs[sm], sm);
            return total;
        }
    }
}
