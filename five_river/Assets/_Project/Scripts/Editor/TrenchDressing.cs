using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.EditorTools
{
    // 2026-11-16 (user: "다 고쳐놔. 에셋도 저번에 다운받은 거 다 어디다 팔아먹은 거야"): dresses the FM 5-15 trench with
    // assets already in the project, nothing new downloaded.
    //  - front parapet: the inside face revetted with the delivered sandbag row (ENV_sandbag_row + T_sandbag_row_0),
    //    top 3 cm over the parapet crest, so the player's line of sight and the squad's kneeling fire stay clear;
    //    the old 2 m sandbag walls, which showed one buried course that read as rocks, are hidden (their colliders
    //    stay, so cover, bullets and movement are as before)
    //  - parados: one course of the same row behind the rear berm
    //  - wooden revetment only where it holds something up: the back walls of the ammunition niches (planks from the
    //    brown_planks_04 scan, posts of the same). 2026-11-17 (user: the boards in front mean nothing, remove them):
    //    the plank panels on the front wall, the two extra rear runs and the niche panel at the trench's west end
    //    (it stood out of the ramp on its own) are no longer made
    //  - the rest of the walls: an earth facing (dirt_floor scan, the floor's own texture and scale) laid 1 cm off the
    //    terrain with UVs in metres, so the steep wall no longer shows the terrain texture stretched down it
    //  - no torn sacks: scaled up on the parapet the SandbagTorn scan read as a heap of rubble from the player's eye
    //    (user, 2026-11-17), so they are no longer placed
    // No colliders on anything added (the NavMesh is baked from colliders). Re-running rebuilds the same result.
    public static class TrenchDressing
    {
        const string RootName = "TrenchDressing_20261116";
        const string Folder = "Assets/_Project/Art/MapPolish20261105/TrenchDressing";
        const string Art = "Assets/_Project/Art/";

        // firing places along the front wall (squad, Park, the player) and other short runs of planks; x ranges
        static readonly (float a, float b)[] FrontPlanks = { };
        static readonly (float a, float b)[] RearPlanksExtra = { };
        const float NicheEndMargin = 1.5f;   // niche panels stop this far short of the trench ends (there they stood out of the ramp)

        [MenuItem("FiveRivers/Setup/Trench Dressing (Existing Assets)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            if (map == null || map.transform.Find("TrenchRebuild_FM515") == null) throw new System.InvalidOperationException("먼저 Trench Rebuild (FM 5-15) 를 적용하세요.");
            var terrain = Terrain.activeTerrain;
            float y0 = terrain.transform.position.y;
            float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + y0;
            float Pre(float x, float z) => H(x, z) - TrenchDig.RebuildRel(x, z);  // ground before the dig
            var report = new StringBuilder();
            Directory.CreateDirectory(Folder + "/Meshes");

            // materials from the delivered textures
            var sandMat = Mat("M_TD_SandbagRow", "Maps/Dabudong_Env/Textures/T_sandbag_row_0_BaseColor.jpg", "Maps/Dabudong_Env/Textures/T_sandbag_row_0_Normal.png", Color.white, .12f, 1f);
            var plankMat = Mat("M_TD_Planks", "Maps/ClaudePolish20261030/Scan/brown_planks_04_diff_2k.jpg", "Maps/ClaudePolish20261030/Scan/brown_planks_04_nor_gl_2k.jpg", new Color(.78f, .72f, .66f), .18f, 1f);
            var earthMat = Mat("M_TD_EarthWall", "MapPolish20261105/dirt_floor_diff_2k.jpg", "MapPolish20261105/dirt_floor_nor_gl_2k.jpg", new Color(.70f, .67f, .63f), .08f, 1f);
            // posts: the boards' own scan, a shade darker (the RL_Wood texture and a .46-.66 tint read as black bars on the
            // shaded side of the trench, 11-16 checks)
            var postMat = Mat("M_TD_Posts", "Maps/ClaudePolish20261030/Scan/brown_planks_04_diff_2k.jpg",
                "Maps/ClaudePolish20261030/Scan/brown_planks_04_nor_gl_2k.jpg", new Color(.74f, .67f, .59f), .14f, .8f);

            // rebuild the root
            var old = map.transform.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Trench Dressing");
            root.transform.SetParent(map.transform, false);
            Transform Group(string n) { var g = new GameObject(n); g.transform.SetParent(root.transform, false); return g.transform; }
            var gSand = Group("Sandbags"); var gPlank = Group("Revetment"); var gEarth = Group("EarthFacing");
            var rng = new System.Random(1950);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // 1. the old 2 m walls: hide only what they show
            int hidden = 0;
            foreach (var w in map.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Trench_Sandbag_Walls")))
                foreach (var r in w.GetComponentsInChildren<Renderer>(true)) { if (r.enabled) { Undo.RecordObject(r, "Trench Dressing"); r.enabled = false; hidden++; } }
            report.AppendLine($"old sandbag wall renderers hidden {hidden} (colliders kept)");

            // 2. sandbag rows
            var row = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Maps/Dabudong_Env/Models/ENV_sandbag_row.fbx");
            const float RowLen = 3.0f, RowDepth = .67f, RowHigh = .47f;
            // on the elbow rest, its face at the top of the wall, so the whole face shows above the rest (the back of the
            // row goes into the parapet's rise); the top stays 3 cm over the crest, as before for sight and fire
            float frontZ = TrenchDig.FrontFloorZ + TrenchDig.WallRunZ + RowDepth * .5f + .02f;
            int rows = 0;
            void PlaceRow(Transform parent, float x, float z, float top, float lenScale, float yaw)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(row, parent);
                go.transform.position = new Vector3(x, top - RowHigh, z);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localScale = new Vector3(lenScale, 1f, 1f);
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = sandMat; r.shadowCastingMode = ShadowCastingMode.On; }
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                rows++;
            }
            for (float x = TrenchDig.TrenchWest + 1.4f; x <= TrenchDig.TrenchEast - 1.2f; x += RowLen * .96f)
            {
                float s = R(.95f, 1.03f);
                PlaceRow(gSand, x, frontZ + R(-.03f, .03f), Pre(x, frontZ) + TrenchDig.ParapetHeight * TrenchDig.EndFade(x) + .03f, s, 180f + R(-2.5f, 2.5f));
            }
            for (float x = TrenchDig.TrenchWest + .7f; x <= TrenchDig.TrenchEast - .6f; x += RowLen * .5f * .95f)
            {
                float zr = Mathf.Min(TrenchDig.RearFloorZ(x - .7f), TrenchDig.RearFloorZ(x), TrenchDig.RearFloorZ(x + .7f));
                float z = zr - TrenchDig.WallRunZ - RowDepth * .5f - .02f;
                PlaceRow(gSand, x, z + R(-.03f, .03f), Pre(x, z) + TrenchDig.ParadosHeight * TrenchDig.EndFade(x) + .02f, .5f * R(.95f, 1.03f), R(-3f, 3f));
            }
            report.AppendLine($"sandbag rows {rows}, torn sacks 0");

            // 3. wall faces. surfaceZ: where the wall face is at height h above the floor, found on the terrain itself
            float FloorY(float x) => H(x, (TrenchDig.FrontFloorZ + TrenchDig.RearFloorZ(x)) * .5f);
            float SurfaceZ(float x, float y, bool front)
            {
                // front wall: height rises with z; rear wall: height rises as z falls
                float lo, hi;
                if (front) { lo = TrenchDig.FrontFloorZ - .3f; hi = TrenchDig.FrontFloorZ + TrenchDig.WallRunZ + TrenchDig.BermWidth + .2f; }
                else { float zr = TrenchDig.RearFloorZ(x); lo = zr - TrenchDig.WallRunZ - TrenchDig.BermWidth - .2f; hi = zr + .3f; }
                for (int i = 0; i < 24; i++)
                {
                    float m = (lo + hi) * .5f;
                    bool above = H(x, m) > y;
                    if (front ? above : !above) hi = m; else lo = m;
                }
                return (lo + hi) * .5f;
            }
            int meshes = 0;
            var written = new HashSet<string>();
            Mesh SaveMesh(Mesh m, string name)
            {
                string path = $"{Folder}/Meshes/{name}.asset";
                written.Add(path);
                m.name = name;   // CopySerialized also copies the name: without this every post and board run was "TD_Post" / "TD_Boards"
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); m = existing; }
                else AssetDatabase.CreateAsset(m, path);
                meshes++;
                return m;
            }
            void Put(Transform parent, string name, Mesh m, Material mat, bool shadows)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
            // a sheet over the wall face from the floor to the top of the wall; flat = planks (each row one straight
            // line, kept in front of every bump of the terrain), otherwise it follows the terrain (earth facing)
            Mesh Sheet(float a, float b, bool front, bool flat, float off, float tile, float topOver)
            {
                int nx = Mathf.Max(2, Mathf.CeilToInt((b - a) / .25f) + 1), ny = 7;
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                var rowZ = new float[ny];
                var xs = Enumerable.Range(0, nx).Select(i => Mathf.Lerp(a, b, i / (float)(nx - 1))).ToArray();
                float Bottom(float x) => FloorY(x) - .02f;
                float Top(float x) => Pre(x, front ? TrenchDig.FrontFloorZ + TrenchDig.WallRunZ + TrenchDig.BermWidth * .5f : TrenchDig.RearFloorZ(x) - TrenchDig.WallRunZ - TrenchDig.BermWidth * .5f) + topOver;
                float yRef = 0f, batter = front ? .25f : -.25f;   // z per metre of height: the 1 on 4 wall leans back
                if (flat)
                {
                    yRef = xs.Min(x => Bottom(x));
                    float best = front ? float.MaxValue : float.MinValue;
                    foreach (var x in xs)
                        for (int j = 0; j < ny; j++)
                        {
                            float y = Mathf.Lerp(Bottom(x), Top(x), j / (float)(ny - 1));
                            float z0 = SurfaceZ(x, y, front) - batter * (y - yRef);
                            best = front ? Mathf.Min(best, z0) : Mathf.Max(best, z0);
                        }
                    rowZ[0] = best + (front ? -off : off);  // the plane's z at yRef
                }
                for (int i = 0; i < nx; i++)
                {
                    float x = xs[i], vAcc = 0f; Vector3 prev = Vector3.zero;
                    for (int j = 0; j < ny; j++)
                    {
                        float y = Mathf.Lerp(Bottom(x), Top(x), j / (float)(ny - 1));
                        float z = flat ? rowZ[0] + batter * (y - yRef) : SurfaceZ(x, y, front) + (front ? -off : off);
                        var p = new Vector3(x, y, z);
                        if (j > 0) vAcc += Vector3.Distance(p, prev);
                        prev = p;
                        v.Add(p);
                        uv.Add(new Vector2(x / tile, (flat ? y - Bottom(x) : vAcc) / tile));
                    }
                }
                for (int i = 0; i < nx - 1; i++)
                    for (int j = 0; j < ny - 1; j++)
                    {
                        int k = i * ny + j;
                        // faces look into the trench: front wall faces -z, rear wall faces +z
                        if (front) { tri.AddRange(new[] { k, k + 1, k + ny, k + 1, k + ny + 1, k + ny }); }
                        else { tri.AddRange(new[] { k, k + ny, k + 1, k + 1, k + ny, k + ny + 1 }); }
                    }
                var m = new Mesh { name = "TD_Sheet" };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
                m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
                return m;
            }
            // 2026-11-16 (user: "작은 것도 고치고 나서 이야기해"): the revetment was one sheet with a plank texture and the
            // posts upright boxes. Now: separate boards with real thickness, gaps and small irregularities, and posts on
            // the same 1 on 4 batter as the boards (an upright post stood up to 22 cm off the leaning boards at the top)
            // with chamfered corners, so their faces catch the sky and the bounce light like the boards instead of
            // reading as black bars on the shaded wall.
            float WallTop(float x, bool front) => Pre(x, front ? TrenchDig.FrontFloorZ + TrenchDig.WallRunZ + TrenchDig.BermWidth * .5f : TrenchDig.RearFloorZ(x) - TrenchDig.WallRunZ - TrenchDig.BermWidth * .5f);
            // the plane a run of revetment stands on: the batter, one straight line per height, in front of every bump of
            // the terrain over the run by 'off'
            (float yRef, float z0, float batter) PlankPlane(float a, float b, bool front, float off)
            {
                int nx = Mathf.Max(2, Mathf.CeilToInt((b - a) / .25f) + 1), ny = 7;
                var xs = Enumerable.Range(0, nx).Select(i => Mathf.Lerp(a, b, i / (float)(nx - 1))).ToArray();
                float batter = front ? .25f : -.25f;
                float yRef = xs.Min(x => FloorY(x) - .02f);
                float best = front ? float.MaxValue : float.MinValue;
                foreach (var x in xs)
                    for (int j = 0; j < ny; j++)
                    {
                        float y = Mathf.Lerp(FloorY(x) - .02f, WallTop(x, front) + .02f, j / (float)(ny - 1));
                        float z = SurfaceZ(x, y, front) - batter * (y - yRef);
                        best = front ? Mathf.Min(best, z) : Mathf.Max(best, z);
                    }
                return (yRef, best + (front ? -off : off), batter);
            }
            void Quad(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
                Vector2 t0, Vector2 t1, Vector2 t2, Vector2 t3, Vector3 outward)
            {
                int s = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3); uv.Add(t0); uv.Add(t1); uv.Add(t2); uv.Add(t3);
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) >= 0f) tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                else tri.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            Mesh Finish(string name, List<Vector3> v, List<Vector2> uv, List<int> tri)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
                m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
                return m;
            }
            // boards 22 cm wide and 2.8 cm thick with 6 mm between them, stacked from just under the top of the wall
            // down into the floor; each a few mm off in height and depth and a little off level, joints on alternate
            // inner posts. brown_planks_04 has six boards across the texture: each board shows one of them, avoiding the
            // texture's own end joints (u 0.41-0.51).
            Mesh Boards(float a, float b, bool front, (float yRef, float z0, float batter) P, float[] posts)
            {
                const float BoardW = .22f, Gap = .006f, Thick = .028f, UPerM = .314f;
                float face = front ? -1f : 1f;                      // toward the trench
                float PZ(float y) => P.z0 + P.batter * (y - P.yRef);
                var xs = Enumerable.Range(0, 9).Select(i => Mathf.Lerp(a, b, i / 8f)).ToArray();
                float floorLow = xs.Min(x => FloorY(x)) - .06f;
                float top = xs.Min(x => WallTop(x, front)) - .01f;
                var inner = posts.Length > 2 ? posts.Skip(1).Take(posts.Length - 2).ToArray() : new float[0];
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                int k = 0;
                for (float yt = top; yt > floorLow; yt -= BoardW + Gap, k++)
                {
                    var cuts = new List<float> { a };
                    if (b - a > 2.2f && inner.Length > 0) cuts.Add(inner[k % inner.Length]);
                    cuts.Add(b);
                    for (int s = 0; s < cuts.Count - 1; s++)
                    {
                        float sa = cuts[s] + (s > 0 ? .003f : -R(0f, .03f)), sb = cuts[s + 1] - (s < cuts.Count - 2 ? .003f : -R(0f, .03f));
                        float dy = R(-.004f, .004f), dz = R(-.004f, .004f), slope = R(-.005f, .005f), mid = (sa + sb) * .5f;
                        int band = (k * 7 + s * 3 + (front ? 0 : 2)) % 5;
                        float v0 = .130f + .166f * band + .012f, v1 = v0 + .166f - .024f;
                        float uLen = (sb - sa) * UPerM, u0 = .52f + R(0f, Mathf.Max(0f, .88f - uLen));
                        Vector3 Pt(float x, float y, bool back)
                        {
                            y += dy + slope * (x - mid);
                            return new Vector3(x, y, PZ(y) + dz - (back ? face * Thick : 0f));
                        }
                        float yb = yt - BoardW;
                        Vector3 fbl = Pt(sa, yb, false), fbr = Pt(sb, yb, false), ftr = Pt(sb, yt, false), ftl = Pt(sa, yt, false);
                        Vector3 bbl = Pt(sa, yb, true), bbr = Pt(sb, yb, true), btr = Pt(sb, yt, true), btl = Pt(sa, yt, true);
                        Quad(v, uv, tri, fbl, fbr, ftr, ftl, new Vector2(u0, v0), new Vector2(u0 + uLen, v0), new Vector2(u0 + uLen, v1), new Vector2(u0, v1), new Vector3(0f, 0f, face));
                        Quad(v, uv, tri, ftl, ftr, btr, btl, new Vector2(u0, v1), new Vector2(u0 + uLen, v1), new Vector2(u0 + uLen, v1 - .02f), new Vector2(u0, v1 - .02f), Vector3.up);
                        Quad(v, uv, tri, fbl, bbl, bbr, fbr, new Vector2(u0, v0), new Vector2(u0, v0 + .02f), new Vector2(u0 + uLen, v0 + .02f), new Vector2(u0 + uLen, v0), Vector3.down);
                        Quad(v, uv, tri, fbl, ftl, btl, bbl, new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u0 + .01f, v1), new Vector2(u0 + .01f, v0), Vector3.left);
                        Quad(v, uv, tri, fbr, bbr, btr, ftr, new Vector2(u0 + uLen, v0), new Vector2(u0 + uLen - .01f, v0), new Vector2(u0 + uLen - .01f, v1), new Vector2(u0 + uLen, v1), Vector3.right);
                    }
                }
                return Finish("TD_Boards", v, uv, tri);
            }
            // a 10 cm post standing on the boards' face, on the same batter, corners chamfered 1.5 cm; the face against the
            // boards is left out; the grain runs along the post
            Mesh Post(float x, bool front, (float yRef, float z0, float batter) P, float bottom, float top)
            {
                const float w = .10f, ch = .015f, h = w * .5f;
                float face = front ? -1f : 1f;
                var sec = new[] { new Vector2(-h + ch, 0f), new Vector2(h - ch, 0f), new Vector2(h, ch), new Vector2(h, w - ch), new Vector2(h - ch, w), new Vector2(-h + ch, w), new Vector2(-h, w - ch), new Vector2(-h, ch) };
                Vector3 At(Vector2 s, float y) => new Vector3(x + s.x, y, P.z0 + P.batter * (y - P.yRef) + face * (.006f + s.y));
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                float len = top - bottom, uTop = len * .35f;
                for (int i = 1; i < sec.Length; i++)
                {
                    Vector2 s0 = sec[i], s1 = sec[(i + 1) % sec.Length], mid = (s0 + s1) * .5f - new Vector2(0f, w * .5f);
                    float vv0 = .30f + i * .018f, vv1 = vv0 + Vector2.Distance(s0, s1) * .6f;
                    Quad(v, uv, tri, At(s0, bottom), At(s1, bottom), At(s1, top), At(s0, top),
                        new Vector2(0f, vv0), new Vector2(0f, vv1), new Vector2(uTop, vv1), new Vector2(uTop, vv0), new Vector3(mid.x, 0f, face * mid.y));
                }
                int c0 = v.Count;
                var cap = sec.Select(s => At(s, top)).ToArray();
                v.AddRange(cap); uv.AddRange(sec.Select(s => new Vector2(.6f + s.x, .32f + s.y)));
                for (int i = 1; i < cap.Length - 1; i++)
                {
                    if (Vector3.Dot(Vector3.Cross(cap[i] - cap[0], cap[i + 1] - cap[0]), Vector3.up) >= 0f) tri.AddRange(new[] { c0, c0 + i, c0 + i + 1 });
                    else tri.AddRange(new[] { c0, c0 + i + 1, c0 + i });
                }
                return Finish("TD_Post", v, uv, tri);
            }

            var frontRuns = FrontPlanks.ToList();
            // niche planks only on the niche's own back wall, inside the 0.3 m where it curves out to the trench wall
            var rearRuns = TrenchDig.AmmoNiches
                .Select(n => (Mathf.Max(n.x - n.half + .1f, TrenchDig.TrenchWest + NicheEndMargin), Mathf.Min(n.x + n.half - .1f, TrenchDig.TrenchEast - NicheEndMargin)))
                .Where(r => r.Item2 - r.Item1 > 1.2f).Concat(RearPlanksExtra).OrderBy(r => r.Item1).ToList();
            int pi = 0, boards = 0;
            void Planks(List<(float, float)> runs, bool front)
            {
                foreach (var (a, b) in runs)
                {
                    if (b < TrenchDig.TrenchWest || a > TrenchDig.TrenchEast) continue;
                    float aa = Mathf.Max(a, TrenchDig.TrenchWest), bb = Mathf.Min(b, TrenchDig.TrenchEast);
                    var plane = PlankPlane(aa, bb, front, .035f);
                    // posts every ~1.2 m, in front of the boards
                    int n = Mathf.Max(2, Mathf.RoundToInt((bb - aa) / 1.2f) + 1);
                    var postXs = Enumerable.Range(0, n).Select(k => Mathf.Lerp(aa + .08f, bb - .08f, k / (float)(n - 1))).ToArray();
                    var mesh = SaveMesh(Boards(aa, bb, front, plane, postXs), $"TD_Planks_{(front ? "F" : "R")}{pi}");
                    boards += mesh.vertexCount / 20;
                    Put(gPlank, mesh.name, mesh, plankMat, true);
                    for (int k = 0; k < n; k++)
                    {
                        float x = postXs[k];
                        float bottom = FloorY(x) - .05f;
                        float top = Pre(x, front ? TrenchDig.FrontFloorZ + TrenchDig.WallRunZ + .1f : TrenchDig.RearFloorZ(x) - TrenchDig.WallRunZ - .1f) + R(.06f, .16f);
                        var post = SaveMesh(Post(x, front, plane, bottom, top), $"TD_Post_{(front ? "F" : "R")}{pi}_{k}");
                        Put(gPlank, post.name, post, postMat, true);
                    }
                    pi++;
                }
            }
            Planks(frontRuns, true);
            Planks(rearRuns, false);

            // earth facing over the stretches without planks
            int ei = 0;
            void Earth(List<(float, float)> runs, bool front)
            {
                float x = TrenchDig.TrenchWest;
                foreach (var (a, b) in runs.Concat(new[] { (TrenchDig.TrenchEast, TrenchDig.TrenchEast + 1f) }))
                {
                    float end = Mathf.Min(a, TrenchDig.TrenchEast);
                    if (end - x > .3f)
                    {
                        var m = SaveMesh(Sheet(x, end, front, false, .012f, 2.1f, .005f), $"TD_Earth_{(front ? "F" : "R")}{ei++}");
                        Put(gEarth, m.name, m, earthMat, false);
                    }
                    x = Mathf.Max(x, b);
                }
            }
            Earth(frontRuns, true);
            Earth(rearRuns, false);
            // meshes left from an earlier run with other runs or post counts: nothing uses them any more
            int stale = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { Folder + "/Meshes" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!written.Contains(path) && AssetDatabase.DeleteAsset(path)) stale++;
            }
            report.AppendLine($"meshes saved {meshes}, plank runs {pi}, boards {boards}, earth runs {ei}, stale meshes removed {stale}");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(map.scene);
            EditorSceneManager.SaveScene(map.scene);
            File.WriteAllText("Temp/trench_dressing.txt", report.ToString());
            return "dressed";
        }

        static Material Mat(string name, string baseMap, string normal, Color tint, float smooth, float bump)
        {
            string path = $"{Folder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            var b = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + baseMap);
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + normal);
            if (n != null && AssetImporter.GetAtPath(Art + normal) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            m.SetTexture("_BaseMap", b); m.SetTexture("_MainTex", b);
            m.SetColor("_BaseColor", tint); m.SetColor("_Color", tint);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
