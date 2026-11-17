using System.Collections.Generic;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-17: 플레이어가 보이는 바위 · 엄폐물 속으로 들어가던 문제.
    // 엄폐 바위는 자체 메시 충돌체가 꺼져 있고 작은 상자 충돌체만 있어서, 상자 밖으로 나온 바위 부분을 그냥 통과했다.
    // 보이는 메시가 기존 충돌체보다 0.25 m 넘게 튀어나온 물체에, 보이는 메시 그대로의 MeshCollider 를
    // 레이어 8(PlayerBlocker)로 붙인다. 이 레이어는 플레이어 레이어하고만 충돌하고, 총알 · 시야 · 시체 처리 ·
    // 이동 경로 굽기(WorldQuery, NavMeshBootstrap)는 보지 않는다. 여러 번 실행해도 결과가 같다.
    public static class PlayerBlockers
    {
        public const string ChildName = "__PlayerBlocker";
        public const string WallRootName = "__PlayerBlockerWalls";
        const float WallMinRise = 0.28f;  // 이보다 높이 솟은 바위는 벽으로 막는다. 더 낮은 돌만 밟고 넘어간다
        const float WallFootRise = 0.18f; // 땅에서 이만큼 넘게 솟은 부분의 윤곽을 벽으로 세운다
        const float WallAbove = 2.2f;     // 바위 꼭대기 위로 이만큼 더 올려서 위에 올라설 수 없게 한다
        const float Need = 0.25f;
        static readonly float[] Heights = { 0.35f, 0.8f, 1.3f, 1.7f };
        static readonly float[] Lateral = { -0.7f, -0.35f, 0f, 0.35f, 0.7f };
        static readonly string[] Skip = { "Bush", "Shrub", "Grass", "Torn", "DawnTruckRoad", "Leaf", "leaf", "Fern", "Net", "Smoke", "Decal", "Pine", "pine", "Tree", "wire", "Wire", "TD_Planks", "TD_Earth" };

        [MenuItem("FiveRivers/Setup/Player Blockers (Visible Shapes)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            int playerLayer = player.gameObject.layer;
            SetupLayer(playerLayer);

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>().Where(t => t.name == ChildName && t.gameObject.scene.IsValid()).ToArray())
                Object.DestroyImmediate(t.gameObject);
            var oldWalls = GameObject.Find(WallRootName);
            if (oldWalls != null) Object.DestroyImmediate(oldWalls);
            var wallRoot = new GameObject(WallRootName) { layer = WorldQuery.PlayerBlockerLayer };
            int walls = 0;
            Physics.SyncTransforms();

            var terrain = Terrain.activeTerrain;
            var sb = new StringBuilder();
            int surveyed = 0, added = 0; float worst = 0f, worstAfter = 0f;
            var rows = new List<(float pen, string line)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var b = r.bounds;
                if (b.size.y < 0.4f || Mathf.Max(b.size.x, b.size.z) < 0.5f || b.size.x > 14f || b.size.z > 14f) continue;
                if (r.GetComponentInParent<Animator>() != null || r.GetComponentInParent<Rigidbody>() != null
                    || r.GetComponentInParent<PlayerController>() != null || r.GetComponentInParent<Camera>() != null
                    || r.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null || r.GetComponentInParent<ParticleSystem>() != null) continue;
                if (r.sharedMaterials.All(m => m == null || m.renderQueue >= 3000)) continue;
                string n = mf.sharedMesh.name + "|" + r.name + "|" + (r.transform.parent != null ? r.transform.parent.name : "");
                if (Skip.Any(n.Contains)) continue;
                var lod = r.GetComponentInParent<LODGroup>();
                if (lod != null && lod.GetLODs().Skip(1).Any(l => l.renderers.Contains(r))) continue;
                float ground = terrain != null ? terrain.SampleHeight(b.center) + terrain.transform.position.y : b.min.y;
                bool rockName = n.Contains("Rock") || n.Contains("rock") || n.Contains("Boulder") || n.Contains("boulder");
                if (b.min.y > ground + 1.9f || b.max.y < ground + (rockName ? 0.1f : 0.3f)) continue;

                surveyed++;
                var go = new GameObject(ChildName) { layer = WorldQuery.PlayerBlockerLayer };
                go.transform.SetParent(r.transform, false);
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                Physics.SyncTransforms();
                float pen = Measure(mc, b, ground, WorldQuery.All, out int open, out int total);
                // 바위는 조건 없이 전부 막는다(2026-11-17 2차: 낮게 솟은 바위 32개가 빠져 있었다)
                bool need = rockName || (pen >= Need && open >= 2 && open >= total * 0.3f);
                if (!need) { Object.DestroyImmediate(go); rows.Add((pen, $"-  {pen:0.00} m  {open}/{total}  {Path(r.transform)}")); continue; }
                added++;
                worst = Mathf.Max(worst, pen);
                float after = Measure(mc, b, ground, ~0, out _, out _);
                worstAfter = Mathf.Max(worstAfter, after);
                bool wall = rockName && terrain != null && AddWall(wallRoot.transform, mf, terrain, walls);
                if (wall) walls++;
                rows.Add((pen, $"막음{(wall ? "+벽" : "")} {pen:0.00} m -> {after:0.00} m  {open}/{total}  {Path(r.transform)}  ({mf.sharedMesh.triangles.Length / 3} tri)"));
            }
            sb.AppendLine($"올라타기 방지 벽 {walls}개 (땅에서 {WallMinRise:0.00} m 넘게 솟은 바위)");
            sb.AppendLine($"조사 {surveyed}개, 차단체 추가 {added}개, 가장 깊이 들어가던 곳 {worst:0.00} m, 추가 뒤 최대 {worstAfter:0.00} m");
            sb.AppendLine("(수치: 보이는 표면에서 기존 충돌체까지 수평 거리의 최대값, 열린 광선 수/보이는 메시에 닿은 광선 수)");
            foreach (var row in rows.OrderByDescending(x => x.pen)) sb.AppendLine(row.line);
            System.IO.File.WriteAllText("Temp/player_blockers.txt", sb.ToString());
            var scene = player.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Player blockers: surveyed {surveyed}, added {added}");
        }

        // 보이는 메시(blocker)가 mask 의 충돌체보다 얼마나 튀어나와 있는지: 바깥에서 안쪽으로 수평 광선을 쏜다
        static float Measure(MeshCollider blocker, Bounds b, float ground, int mask, out int open, out int total)
        {
            open = 0; total = 0; float worst = 0f;
            float radius = new Vector2(b.extents.x, b.extents.z).magnitude;
            float len = radius * 2f + 1f;
            for (int d = 0; d < 8; d++)
            {
                float a = d * Mathf.PI / 4f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0f, dir.x);
                foreach (float l in Lateral)
                    foreach (float h in Heights)
                    {
                        float y = ground + h;
                        if (y < b.min.y || y > b.max.y) continue;
                        var origin = new Vector3(b.center.x, y, b.center.z) + dir * (radius + 1f) + side * (l * radius);
                        var ray = new Ray(origin, -dir);
                        if (!blocker.Raycast(ray, out var vis, len)) continue;
                        total++;
                        float other = len;
                        foreach (var hit in Physics.RaycastAll(ray, len, mask, QueryTriggerInteraction.Ignore))
                            other = Mathf.Min(other, hit.distance);
                        float pen = Mathf.Min(other - vis.distance, 2f);
                        if (pen >= Need) open++;
                        worst = Mathf.Max(worst, pen);
                    }
            }
            return worst;
        }

        // 바위 위에 올라서거나 튀어나온 부분 밑으로 머리가 들어가지 못하게, 땅 위로 솟은 부분의 윤곽을 수직 벽으로 세운다
        static bool AddWall(Transform root, MeshFilter mf, Terrain terrain, int index)
        {
            var pts = new List<Vector2>();
            float top = float.MinValue, bottom = float.MaxValue, rise = 0f;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = m.MultiplyPoint3x4(v);
                float g = terrain.SampleHeight(w) + terrain.transform.position.y;
                rise = Mathf.Max(rise, w.y - g);
                if (w.y < g + WallFootRise) continue;
                pts.Add(new Vector2(w.x, w.z));
                top = Mathf.Max(top, w.y); bottom = Mathf.Min(bottom, g);
            }
            if (rise < WallMinRise || pts.Count < 3) return false;
            var hull = Hull(pts);
            if (hull.Count < 3) return false;
            while (hull.Count > 100) hull = hull.Where((_, i) => i % 2 == 0).ToList();
            float y0 = bottom - 0.5f, y1 = top + WallAbove;
            int c = hull.Count;
            var verts = new Vector3[c * 2];
            for (int i = 0; i < c; i++) { verts[i] = new Vector3(hull[i].x, y0, hull[i].y); verts[c + i] = new Vector3(hull[i].x, y1, hull[i].y); }
            var tris = new List<int>();
            for (int i = 0; i < c; i++)
            {
                int j = (i + 1) % c;
                tris.AddRange(new[] { i, c + i, j, j, c + i, c + j });
                if (i > 0 && i < c - 1) { tris.AddRange(new[] { 0, i, j }); tris.AddRange(new[] { c, c + j, c + i }); }
            }
            var mesh = new Mesh { name = "PlayerBlockerWall_" + index, vertices = verts, triangles = tris.ToArray() };
            mesh.RecalculateBounds();
            var go = new GameObject("__PlayerBlockerWall") { layer = WorldQuery.PlayerBlockerLayer };
            go.transform.SetParent(root, false);
            var mc = go.AddComponent<MeshCollider>();
            mc.convex = true;
            mc.sharedMesh = mesh;
            return true;
        }

        static List<Vector2> Hull(List<Vector2> p)
        {
            p = p.OrderBy(v => v.x).ThenBy(v => v.y).ToList();
            var h = new List<Vector2>();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            for (int i = 0; i < p.Count; i++) { while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], p[i]) <= 0) h.RemoveAt(h.Count - 1); h.Add(p[i]); }
            int lower = h.Count + 1;
            for (int i = p.Count - 2; i >= 0; i--) { while (h.Count >= lower && Cross(h[h.Count - 2], h[h.Count - 1], p[i]) <= 0) h.RemoveAt(h.Count - 1); h.Add(p[i]); }
            h.RemoveAt(h.Count - 1);
            return h;
        }

        static void SetupLayer(int playerLayer)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layer = tags.FindProperty("layers").GetArrayElementAtIndex(WorldQuery.PlayerBlockerLayer);
            if (layer.stringValue != "PlayerBlocker")
            {
                if (!string.IsNullOrEmpty(layer.stringValue)) throw new System.InvalidOperationException("Layer 8 is in use: " + layer.stringValue);
                layer.stringValue = "PlayerBlocker";
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(WorldQuery.PlayerBlockerLayer, i, i != playerLayer);
        }

        static string Path(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
