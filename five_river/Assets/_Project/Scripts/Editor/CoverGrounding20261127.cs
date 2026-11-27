using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-27 (아트 검수 "엄폐물 바위가 비탈에서 떠 있음, 모래주머니 벽도 의심"):
    //  - the rocks (ClaudeMapNature's RockFootprint meshes, the slab-shaped cover rocks most of all) have a flat underside
    //    standing level on a slope: on the downhill side their edge hung 20-50 cm over the ground with the sky under it.
    //    Each rock's mesh is drawn again from its own source: the lower half of it follows the ground down, to Bury below it
    //    where it hung (a skirt of rock into the slope). Where it was already in the ground nothing moves. The top of the rock,
    //    its place, its scale and every collider stay as they are, so cover, bullets and paths are unchanged.
    //  - the low sandbag walls (CoverWalls20261107) sit on the ground except where the ground was dug away under them later
    //    (CP12, on the bank of the stream cut on 11-15: its lowest course hung up to 0.75 m in the air). Under a wall whose
    //    lowest course hangs more than WallGap, further courses of the same row are laid until it stands in the ground.
    //    The cover collider is not touched.
    //  - the poplars on the stream bank: see Poplars below.
    // Numbers: BALANCE.md 4-4. Re-running gives the same result (rocks are redrawn from RockFootprint_*, footings rebuilt).
    // Changes meshes under Art/Maps/ClaudePolish20261030/Nature (new RockGrounded_* assets) and the Dabudong scene (saves it).
    public static class CoverGrounding20261127
    {
        // ---- numbers (BALANCE.md 4-4)
        public const float Bury = 0.15f;          // how far under the ground the hanging edge of a rock ends
        public const float SkirtShare = 1.00f;    // the share of a rock's height, from the bottom, that follows the ground (its top stays)
        public const float Round = 0.45f;         // a rock's underside is taken as rounded up at its edge by this share of its height
        public const float MostDrop = 1.1f;       // a rock's underside is never drawn further down than this
        public const float WallGap = 0.12f, WallRow = 0.44f, WallPress = 0.17f; public const int WallMostFootings = 3;

        [MenuItem("FiveRivers/Setup/Cover Grounding 20261127")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var root = GameObject.Find("/[Map]/Dabudong_Imported");
            var terrain = Terrain.activeTerrain;
            if (root == null || terrain == null) throw new System.InvalidOperationException("Dabudong 씬을 연 상태에서 실행하세요.");
            var sb = new StringBuilder();
            sb.Append(Rocks(root, terrain));
            sb.Append(Walls(terrain));
            sb.Append(Poplars(terrain));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
            File.WriteAllText("Temp/cover_grounding.txt", sb.ToString());
            Debug.Log("[CoverGrounding]\n" + sb);
            return sb.ToString();
        }

        static float Ground(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;

        // Lowest point of the mesh over the ground, cell by cell of a grid over its footprint: the largest of the edge cells.
        public static float HangingEdge(Vector3[] world, Bounds b, Terrain t)
        {
            const int n = 5; var low = Enumerable.Repeat(float.MaxValue, n * n).ToArray();
            foreach (var w in world)
            {
                int ix = Mathf.Clamp((int)((w.x - b.min.x) / Mathf.Max(.001f, b.size.x) * n), 0, n - 1), iz = Mathf.Clamp((int)((w.z - b.min.z) / Mathf.Max(.001f, b.size.z) * n), 0, n - 1);
                low[iz * n + ix] = Mathf.Min(low[iz * n + ix], w.y - Ground(t, w));
            }
            float worst = float.MinValue;
            for (int iz = 0; iz < n; iz++) for (int ix = 0; ix < n; ix++)
                if ((ix == 0 || iz == 0 || ix == n - 1 || iz == n - 1) && low[iz * n + ix] < float.MaxValue) worst = Mathf.Max(worst, low[iz * n + ix]);
            return worst;
        }

        static string Rocks(GameObject root, Terrain terrain)
        {
            var sb = new StringBuilder(); int done = 0, moved = 0, turned = 0; float worstBefore = 0f, worstAfter = float.MinValue; var worstName = "";
            var hung = new List<string>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                // the source mesh: RockFootprint_NNN_P (this menu's own output is RockGrounded_NNN_P beside it)
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.StartsWith("RockFootprint_") && !file.StartsWith("RockGrounded_")) continue;
                string dir = Path.GetDirectoryName(path).Replace('\\', '/');
                string sourcePath = dir + "/" + file.Replace("RockGrounded_", "RockFootprint_") + ".asset";
                string groundedPath = dir + "/" + file.Replace("RockFootprint_", "RockGrounded_") + ".asset";
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath);
                if (source == null) { sb.AppendLine("  missing source " + sourcePath); continue; }

                var verts = source.vertices; var m = filter.transform.localToWorldMatrix; var inv = filter.transform.worldToLocalMatrix;
                var world = verts.Select(v => m.MultiplyPoint3x4(v)).ToArray();
                var b = new Bounds(world[0], Vector3.zero); foreach (var w in world) b.Encapsulate(w);
                float before = HangingEdge(world, b, terrain);
                float skirt = Mathf.Max(.05f, b.size.y * SkirtShare); bool any = false;
                // Every vertex goes down by how far the rock's lowest level stands over the ground at that place (plus Bury), times a
                // weight that is 1 at the rock's lowest level and 0 above the skirt. The drop changes only as the ground does and the
                // weight only with height, so the shape is sheared and stretched, never folded: no triangle turns inside out.
                // (Tried: the underside read cell by cell over the footprint, to follow a rounded underside more closely — it folded
                // the mesh, 1,190 to 2,008 triangles inside out over the 126 rocks, and the dawn sky showed through as white cracks.)
                for (int i = 0; i < world.Length; i++)
                {
                    var w = world[i];
                    // (Round is added because a rock is round below: its edge stands higher than its lowest level)
                    float drop = Mathf.Clamp(b.min.y - (Ground(terrain, w) - Bury) + b.size.y * Round, 0f, MostDrop);
                    if (drop <= 0.001f) continue;
                    float weight = 1f - Mathf.SmoothStep(0f, 1f, (w.y - b.min.y) / skirt);
                    if (weight <= 0f) continue;
                    w.y -= drop * weight; world[i] = w; any = true;
                }
                float after = HangingEdge(world, b, terrain);
                {   // no triangle may be turned inside out by it (a hole in the rock from outside)
                    var tr = source.triangles;
                    for (int i = 0; i < tr.Length; i += 3)
                    {
                        Vector3 a0 = m.MultiplyPoint3x4(verts[tr[i]]), a1 = m.MultiplyPoint3x4(verts[tr[i + 1]]), a2 = m.MultiplyPoint3x4(verts[tr[i + 2]]);
                        var n0 = Vector3.Cross(a1 - a0, a2 - a0); var n1 = Vector3.Cross(world[tr[i + 1]] - world[tr[i]], world[tr[i + 2]] - world[tr[i]]);
                        if (n0.sqrMagnitude > 1e-12f && n1.sqrMagnitude > 1e-12f && Vector3.Dot(n0, n1) < 0f) turned++;
                    }
                }
                if (before > worstBefore) { worstBefore = before; worstName = file + " at " + b.center.ToString("0.0"); }
                worstAfter = Mathf.Max(worstAfter, after);
                if (before > 0.1f) hung.Add($"{file} {b.center.ToString("0.0")} {b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0} m: {before:0.00} -> {after:0.00}");

                var mesh = Object.Instantiate(source); mesh.name = Path.GetFileNameWithoutExtension(groundedPath);
                mesh.vertices = world.Select(w => inv.MultiplyPoint3x4(w)).ToArray();
                mesh.normals = WeldedNormals(mesh);
                mesh.RecalculateTangents(); mesh.RecalculateBounds();
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(groundedPath);
                if (existing == null) { AssetDatabase.CreateAsset(mesh, groundedPath); existing = mesh; }
                else { existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.tangents = mesh.tangents; existing.RecalculateBounds(); existing.UploadMeshData(false); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); }
                if (filter.sharedMesh != existing)
                {
                    Undo.RecordObject(filter, "Rock grounding");
                    filter.sharedMesh = existing; EditorUtility.SetDirty(filter); PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                }
                done++; if (any) moved++;
            }
            sb.AppendLine($"rocks: {done} redrawn, {moved} with an underside taken down to the ground; worst hanging edge {worstBefore:0.00} m ({worstName}) -> {worstAfter:0.00} m; triangles turned inside out {turned}");
            sb.AppendLine($"  hung more than 10 cm ({hung.Count}):");
            foreach (var h in hung.OrderBy(x => x)) sb.AppendLine("    " + h);
            return sb.ToString();
        }

        // Smooth normals of the redrawn shape. Vertices at the same place (split along the UV seams) get the same normal, so no
        // seam shows; RecalculateNormals would shade each side of a seam on its own.
        static Vector3[] WeldedNormals(Mesh mesh)
        {
            var v = mesh.vertices; var tris = mesh.triangles; var sum = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);   // area-weighted
                foreach (var p in new[] { a, b, c }) { var k = Key(p); sum.TryGetValue(k, out var s); sum[k] = s + n; }
            }
            var old = mesh.normals;
            return v.Select((p, i) => sum.TryGetValue(Key(p), out var s) && s.sqrMagnitude > 1e-12f ? s.normalized : (old.Length == v.Length ? old[i] : Vector3.up)).ToArray();
        }

        // The poplars along the stream stand on its bank: on the bank's slope the flared foot of a trunk stood in the air on the
        // downhill side (its cut underside showed). Each is set down so the lowest ground under its foot is PoplarSink into it.
        public const float PoplarFoot = 0.6f, PoplarSink = 0.30f;
        static string Poplars(Terrain terrain)
        {
            var group = GameObject.Find("ValleyRoad_Aug1950");
            if (group == null) return "poplars: ValleyRoad_Aug1950 not in the scene" + "\n";
            int n = 0, moved = 0; float most = 0f;
            foreach (Transform t in group.transform)
            {
                if (!t.name.StartsWith("Poplar_")) continue; n++;
                float low = float.MaxValue;
                for (int k = 0; k < 8; k++) { var p = t.position + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * PoplarFoot; low = Mathf.Min(low, Ground(terrain, p)); }
                float y = Mathf.Min(low, Ground(terrain, t.position)) - PoplarSink;
                if (Mathf.Abs(t.position.y - y) < 0.005f) continue;
                most = Mathf.Max(most, t.position.y - y);
                Undo.RecordObject(t, "Poplar foot"); t.position = new Vector3(t.position.x, y, t.position.z); moved++;
            }
            return $"poplars: {n} checked, {moved} set down (the most {most:0.00} m)" + "\n";
        }

        // gap under the middle of a sandbag row (its ends curl up, they are not counted)
        static float UnderRow(Transform row, Terrain t)
        {
            float worst = float.MinValue;
            foreach (var f in row.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                var mb = f.sharedMesh.bounds; var m = f.transform.localToWorldMatrix;
                // the row runs along the longest axis of its mesh
                int axis = mb.size.x >= mb.size.y && mb.size.x >= mb.size.z ? 0 : mb.size.z >= mb.size.y ? 2 : 1;
                const int n = 6; var low = Enumerable.Repeat(float.MaxValue, n).ToArray();
                foreach (var v in f.sharedMesh.vertices)
                {
                    float u = (v[axis] - mb.min[axis]) / Mathf.Max(.001f, mb.size[axis]);
                    if (u < 0.2f || u > 0.8f) continue;
                    var w = m.MultiplyPoint3x4(v); int k = Mathf.Clamp((int)((u - 0.2f) / 0.6f * n), 0, n - 1);
                    low[k] = Mathf.Min(low[k], w.y - Ground(t, w));
                }
                foreach (float g in low) if (g < float.MaxValue) worst = Mathf.Max(worst, g);
            }
            return worst;
        }

        static string Walls(Terrain terrain)
        {
            var sb = new StringBuilder();
            var root = GameObject.Find("/[Map]/CoverWalls20261107");
            if (root == null) return "walls: CoverWalls20261107 not in the scene\n";
            int walls = 0, footed = 0;
            foreach (Transform wall in root.transform)
            {
                foreach (var old in wall.Cast<Transform>().Where(c => c.name.StartsWith("Footing")).ToList()) Undo.DestroyObjectImmediate(old.gameObject);
                if (wall.childCount == 0) continue;
                walls++;
                var lowest = wall.GetChild(0);
                float before = UnderRow(lowest, terrain), gap = before; int n = 0;
                var above = lowest;
                while (gap > WallGap && n < WallMostFootings)
                {
                    var go = Object.Instantiate(lowest.gameObject, wall);
                    Undo.RegisterCreatedObjectUndo(go, "Wall footing");
                    go.name = "Footing" + (++n);
                    go.transform.SetAsLastSibling();   // the courses keep their places in the hierarchy (CoverWallCourses reads child 0, 1)
                    go.transform.rotation = above.rotation * Quaternion.Euler(0f, n % 2 == 0 ? 2f : -2f, 0f);
                    go.transform.localScale = Vector3.Scale(above.localScale, new Vector3(1.04f, 1f, 1.06f));
                    go.transform.position = above.position - above.up * (WallRow * above.lossyScale.y - WallPress);
                    foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    above = go.transform; gap = UnderRow(above, terrain);
                }
                if (n > 0) { footed++; sb.AppendLine($"  {wall.name} at {wall.position.ToString("0.0")}: lowest course hung {before:0.00} m over the ground -> {n} more course(s) under it, now {gap:0.00} m"); }
            }
            sb.Insert(0, $"walls: {walls} checked, {footed} given courses down to the ground\n");
            return sb.ToString();
        }
    }
}
