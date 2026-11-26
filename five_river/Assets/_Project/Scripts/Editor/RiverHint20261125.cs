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
    // 2026-11-25 (사용자 지시 "마지막에 능선에서 만수자리에 엎드리라고 이야기하는건 … 지우고 다음장을 암시하는 장면 하나 넣어주고",
    // 사용자 선택: "강을 내려다봄"): from the summit at daybreak the Nakdong shows far off to the south-west, down the valley
    // the squad has held all night — the first of the five rivers. This menu lays the river on the valley floor of the land
    // round the map (Ridges_Continuous_Backdrop): it finds the lowest line across the valley, lays a wide ribbon of water on
    // it (reflecting the dawn sky, so it reads as a pale band through the mist) and leaves a point for the dawn to look at
    // (RiverLookPoint). Nothing to walk on, no collider. Re-running rebuilds the same result.
    public static class RiverHint20261125
    {
        public const string RootName = "RiverHint_20261125", LookPointName = "RiverLookPoint";
        static readonly Vector3 Summit = new Vector3(-5.7f, 25.6f, 115.4f);

        [MenuItem("FiveRivers/Setup/River Hint 20261125")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            var backdrop = GameObject.Find("Ridges_Continuous_Backdrop");
            if (map == null || backdrop == null) throw new System.InvalidOperationException("[Map] 또는 구릉 메시가 없음");
            var old = map.transform.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var report = new StringBuilder();

            // heights of the land round the map, through a throw-away collider
            var probe = new GameObject("__river_probe") { hideFlags = HideFlags.HideAndDontSave };
            var mc = probe.AddComponent<MeshCollider>(); mc.sharedMesh = backdrop.GetComponent<MeshFilter>().sharedMesh;
            probe.transform.SetPositionAndRotation(backdrop.transform.position, backdrop.transform.rotation); probe.transform.localScale = backdrop.transform.lossyScale;
            Physics.SyncTransforms();
            float Height(float x, float z) => mc.Raycast(new Ray(new Vector3(x, 600f, z), Vector3.down), out var hit, 2000f) ? hit.point.y : float.NaN;

            // the valley floor: along lines running away from the summit to the south-west, the lowest point between
            // 230 and 520 m out; joined and smoothed into a winding line across the view
            var pts = new List<Vector3>();
            for (float bearing = 165f; bearing <= 265f; bearing += 2.5f)
            {
                var dir = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
                float bestH = float.MaxValue; Vector3 best = Vector3.zero;
                for (float r = 230f; r <= 520f; r += 6f)
                {
                    var p = Summit + dir * r; float h = Height(p.x, p.z);
                    if (float.IsNaN(h)) continue;
                    if (h < bestH) { bestH = h; best = new Vector3(p.x, h, p.z); }
                }
                if (bestH < float.MaxValue) pts.Add(best);
            }
            Object.DestroyImmediate(probe);
            if (pts.Count < 8) throw new System.InvalidOperationException("골짜기 바닥을 찾지 못함");
            // smooth the line (the lowest points wander from one gully to the next)
            for (int pass = 0; pass < 6; pass++)
            {
                var s = new List<Vector3>(pts);
                for (int i = 1; i < pts.Count - 1; i++) s[i] = (pts[i - 1] + pts[i] * 2f + pts[i + 1]) * .25f;
                pts = s;
            }
            float waterY = pts.Min(p => p.y) + .4f;
            report.AppendLine($"river line {pts.Count} points, from {pts[0]} to {pts[pts.Count - 1]}, water level {waterY:0.0} - {pts.Max(p => p.y) + .4f:0.0} m, distance from summit {pts.Average(p => Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(Summit.x, 0, Summit.z))):0} m");

            // a wide ribbon, flat at the water level, with shallow banks sinking into the land on both sides
            var mesh = new Mesh { name = "SM_NakdongRibbon" };
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float along = 0f;
            float[] across = { -1.25f, -1f, -.6f, 0f, .6f, 1f, 1.25f };
            for (int i = 0; i < pts.Count; i++)
            {
                var fwd = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]); fwd.y = 0f; fwd.Normalize();
                var side = new Vector3(-fwd.z, 0f, fwd.x);
                float half = 48f + 16f * Mathf.Sin(i * .55f) + 8f * Mathf.Sin(i * 1.7f);   // the Nakdong is broad, with sandbars
                if (i > 0) along += Vector3.Distance(pts[i], pts[i - 1]);
                foreach (var a in across)
                {
                    var p = pts[i] + side * half * a; p.y = pts[i].y + .45f - (Mathf.Abs(a) > 1f ? 2.5f : 0f);   // follows the valley floor down; the outer edge dips under the banks
                    verts.Add(p); uvs.Add(new Vector2(a, along / 60f));
                }
            }
            int cols = across.Length;
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < cols - 1; k++)
                {
                    int a = i * cols + k, b = a + 1, c = a + cols, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            const string folder = "Assets/_Project/Art/Maps/Dabudong_Env/RiverHint20261125";
            Directory.CreateDirectory(folder);
            AssetDatabase.CreateAsset(mesh, folder + "/SM_NakdongRibbon.asset");
            string matPath = folder + "/M_NakdongWater.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var riverShader = Shader.Find("FiveRivers/DistantRiver");
            if (mat == null) { mat = new Material(riverShader); AssetDatabase.CreateAsset(mat, matPath); }
            // 350 m off the scene fog takes 99 % of anything lit: the river is drawn as what one sees of it at dawn, a pale
            // band of mirrored sky through the haze with a glint toward the sun (FR_DistantRiver.shader)
            mat.shader = riverShader;
            mat.SetFloat("_Strength", .85f); mat.SetFloat("_Edge", .35f);
            EditorUtility.SetDirty(mat);

            var root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "River Hint");
            root.transform.SetParent(map.transform, false);
            var river = new GameObject("Nakdong"); river.transform.SetParent(root.transform, false);
            river.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = river.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(river, StaticEditorFlags.BatchingStatic);
            // the point the dawn looks at: the stretch nearest the middle of the view
            var look = new GameObject(LookPointName); look.transform.SetParent(root.transform, false);
            var mid = pts[pts.Count / 2]; look.transform.position = new Vector3(mid.x, waterY, mid.z);
            report.AppendLine($"look point {look.transform.position}, bearing {Mathf.Atan2(mid.x - Summit.x, mid.z - Summit.z) * Mathf.Rad2Deg:0} deg from the summit");

            EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
            File.WriteAllText("Temp/river_hint.txt", report.ToString());
            Debug.Log("[RiverHint20261125]\n" + report);
            return report.ToString();
        }
    }
}
