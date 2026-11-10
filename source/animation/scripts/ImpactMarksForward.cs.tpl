using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Moves the shell-impact marks (rubble + scorched/mud terrain paint) from the four clusters near the trench
    // out onto the KPA approach slope, where friendly artillery mostly fell (Appleman, CMH 1961, ch. 19).
    // Only terrain paint is changed, never terrain height (the dawn-truck decision kept heights fixed).
    public static class ImpactMarksForward20261107
    {
        const int Burned = 4, Mud = 3;            // TL_Claude_Burned, TL_Claude_Mud
        const float PaintRadius = 2.6f, ClearRadius = 3.8f, BurnedPeak = .38f, MudPeak = .14f;
        static readonly Vector2[] Sites =
        {
            __SITES__
        };

        [MenuItem("FiveRivers/Map/Move Impact Marks Forward 20261107")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var debris = GameObject.Find("Ground_Debris");
            var terrain = Terrain.activeTerrain;
            if (debris == null || terrain == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            var rubble = debris.transform.Cast<Transform>().Where(t => t.name.StartsWith("Impact_rubble")).OrderBy(t => t.name.Length).ThenBy(t => t.name).ToList();
            if (rubble.Count == 0) throw new System.InvalidOperationException("No impact rubble found.");
            if (Vector2.Distance(Flat(rubble[0].position), Sites[0]) < 2.5f) { Debug.Log("[Impact marks] Already moved."); return; }

            var td = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Move impact marks");
            // 1) clear the old scorched rings around the current rubble clusters
            foreach (var c in Clusters(rubble.Select(t => Flat(t.position)).ToList())) Clear(terrain, c);
            // 2) paint new rings and move the rubble into small groups around each site
            foreach (var s in Sites) Paint(terrain, s);
            var rng = new System.Random(8250);
            for (int i = 0; i < rubble.Count; i++)
            {
                var site = Sites[i % Sites.Length];
                float a = (float)(rng.NextDouble() * Mathf.PI * 2), r = .4f + (float)rng.NextDouble() * 1.4f;
                var p = new Vector3(site.x + Mathf.Cos(a) * r, 0f, site.y + Mathf.Sin(a) * r);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                var n = td.GetInterpolatedNormal((p.x - terrain.transform.position.x) / td.size.x, (p.z - terrain.transform.position.z) / td.size.z);
                Undo.RecordObject(rubble[i], "Move impact rubble");
                var b = rubble[i].GetComponentInChildren<Renderer>().bounds;
                // Seat each lump about a third into the ground so it reads as thrown-up earth, not a stone on top.
                rubble[i].position = p - n * (b.size.y * .33f);
                rubble[i].rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, .8f)) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            }
            EditorUtility.SetDirty(td);
            EditorSceneManager.MarkSceneDirty(debris.scene);
            Debug.Log("[Impact marks] Moved " + rubble.Count + " rubble pieces to " + Sites.Length + " sites on the forward slope.");
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        static List<Vector2> Clusters(List<Vector2> pts)
        {
            var centers = new List<Vector2>(); var counts = new List<int>();
            foreach (var p in pts)
            {
                int k = centers.FindIndex(c => Vector2.Distance(c, p) < 5f);
                if (k < 0) { centers.Add(p); counts.Add(1); }
                else { centers[k] = (centers[k] * counts[k] + p) / (counts[k] + 1); counts[k]++; }
            }
            return centers;
        }

        static void ForArea(Terrain t, Vector2 c, float radius, System.Action<float[,,], int, int, float> body)
        {
            var td = t.terrainData; var o = t.transform.position;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - radius - o.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.y - radius - o.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + radius - o.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.y + radius - o.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var a = td.GetAlphamaps(x0, z0, w, h);
            for (int z = 0; z < h; z++) for (int x = 0; x < w; x++)
            {
                float wx = o.x + (x0 + x + .5f) / td.alphamapWidth * td.size.x, wz = o.z + (z0 + z + .5f) / td.alphamapHeight * td.size.z;
                float d = Vector2.Distance(new Vector2(wx, wz), c) / radius;
                if (d < 1f) body(a, z, x, d);
            }
            td.SetAlphamaps(x0, z0, a);
        }

        // Replace burned/mud with the surrounding ground mix, fading to the edge so no ring outline is left.
        static void Clear(Terrain t, Vector2 c)
        {
            var td = t.terrainData; int L = td.alphamapLayers; var ring = new float[L]; int n = 0; var o = t.transform.position;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f; var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (ClearRadius + 1.5f);
                int ax = Mathf.Clamp((int)((p.x - o.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
                int az = Mathf.Clamp((int)((p.y - o.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
                var s = td.GetAlphamaps(ax, az, 1, 1); for (int l = 0; l < L; l++) ring[l] += s[0, 0, l]; n++;
            }
            for (int l = 0; l < L; l++) ring[l] /= n;
            ring[Burned] = 0f; ring[Mud] = 0f; Normalize(ring);
            ForArea(t, c, ClearRadius, (a, z, x, d) =>
            {
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.7f, 1f, d));
                for (int l = 0; l < L; l++) a[z, x, l] = Mathf.Lerp(a[z, x, l], ring[l], k);
            });
        }

        static void Paint(Terrain t, Vector2 c)
        {
            int L = t.terrainData.alphamapLayers;
            ForArea(t, c, PaintRadius, (a, z, x, d) =>
            {
                float k = 1f - Mathf.SmoothStep(0f, 1f, d);
                float burned = Mathf.Max(a[z, x, Burned], BurnedPeak * k), mud = Mathf.Max(a[z, x, Mud], MudPeak * k);
                float rest = 0f; for (int l = 0; l < L; l++) if (l != Burned && l != Mud) rest += a[z, x, l];
                float scale = rest > 1e-5f ? (1f - burned - mud) / rest : 0f;
                for (int l = 0; l < L; l++) if (l != Burned && l != Mud) a[z, x, l] *= scale;
                a[z, x, Burned] = burned; a[z, x, Mud] = mud;
            });
        }

        static void Normalize(float[] w) { float s = w.Sum(); if (s > 1e-5f) for (int i = 0; i < w.Length; i++) w[i] /= s; }
    }
}
