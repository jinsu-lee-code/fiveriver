using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Shell craters as the record has it (Appleman, South to the Naktong, North to the Yalu, CMH 1961, ch. 19):
    // US 105 mm fire supporting ROK 1st Division fell mostly on the KPA approach in front of the line, and KPA
    // shells mostly landed in the rear areas. So: the ten forward impact sites (ImpactMarksForward20261107) are
    // dug as real craters, and a few smaller KPA craters are added behind the trench.
    // Craters are dug into the terrain (bowl plus a low, uneven, lopsided throw-out lip, never a ring on flat
    // ground), the forward rubble is moved up onto each rim, and the rear craters get scorch / mud / thrown-soil
    // paint. The trench, the spoil mound, the dawn truck road and anything standing nearby are left alone.
    public static class ShellCraters20261107
    {
        const int Soil = 1, Mud = 3, Burned = 4;
        static readonly Vector2[] Forward =
        {
            new Vector2(-20.3f, 29.6f), new Vector2(-10.2f, 34.1f), new Vector2(10.7f, 35.3f), new Vector2(28.3f, 30.0f),
            new Vector2(-25.1f, 47.7f), new Vector2(-10.6f, 46.9f), new Vector2(9.9f, 48.0f), new Vector2(25.1f, 57.8f),
            new Vector2(-14.8f, 60.6f), new Vector2(4.4f, 58.5f),
        };
        static readonly Vector2[] Rear = { new Vector2(-14f, -14f), new Vector2(-4f, -25f), new Vector2(8f, -12f), new Vector2(14f, -31f), new Vector2(-21f, -37f) };

        [MenuItem("FiveRivers/Map/Shell Craters As Recorded 20261107")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var terrain = Terrain.activeTerrain;
            var debris = GameObject.Find("Ground_Debris");
            if (terrain == null || debris == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            var map = GameObject.Find("/[Map]");
            if (map.transform.Find("ShellCraters20261107") != null) { Debug.Log("[Craters] Already dug."); return; }
            var td = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(td, "Shell craters");
            var marker = new GameObject("ShellCraters20261107"); // one-shot marker, empty
            Undo.RegisterCreatedObjectUndo(marker, "Shell craters");
            marker.transform.SetParent(map.transform, false);

            var rng = new System.Random(19500816);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var log = new List<string>();
            var dug = new List<(Vector2 c, float r)>();
            foreach (var c in Forward)
            {
                float r = R(1.3f, 1.8f);
                if (Blocked(c, r, debris.transform, out string why)) { log.Add("skip forward " + c + " (" + why + ")"); continue; }
                Dig(terrain, c, r, R(.35f, .55f), R(0f, 360f), rng); dug.Add((c, r));
            }
            foreach (var c in Rear)
            {
                float r = R(1.0f, 1.4f);
                if (Blocked(c, r, debris.transform, out string why)) { log.Add("skip rear " + c + " (" + why + ")"); continue; }
                Dig(terrain, c, r, R(.28f, .42f), R(0f, 360f), rng);
                Paint(terrain, c, r, rng);
                dug.Add((c, r));
            }
            terrain.Flush();

            // forward rubble: up onto the nearest rim, seated a third into the ground
            var rubble = debris.transform.Cast<Transform>().Where(t => t.name.StartsWith("Impact_rubble")).ToList();
            if (dug.Count == 0) rubble.Clear(); // nothing dug: leave the rubble where it is
            foreach (var t in rubble)
            {
                var p = new Vector2(t.position.x, t.position.z);
                var near = dug.OrderBy(d => (d.c - p).sqrMagnitude).First();
                if ((near.c - p).magnitude > near.r * 2.2f) continue;
                var dir = (p - near.c).sqrMagnitude > .01f ? (p - near.c).normalized : Vector2.right;
                var onRim = near.c + dir * near.r * R(1.05f, 1.45f);
                var w = new Vector3(onRim.x, 0f, onRim.y);
                w.y = terrain.SampleHeight(w) + terrain.transform.position.y;
                var n = td.GetInterpolatedNormal((w.x - terrain.transform.position.x) / td.size.x, (w.z - terrain.transform.position.z) / td.size.z);
                Undo.RecordObject(t, "Rubble onto crater rim");
                var b = t.GetComponentInChildren<Renderer>().bounds;
                t.position = w - n * (b.size.y * .33f);
                t.rotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, .8f)) * Quaternion.Euler(0f, R(0f, 360f), 0f);
            }
            EditorUtility.SetDirty(td);
            EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("[Craters] Dug " + dug.Count + " craters (" + Forward.Length + " forward sites, " + Rear.Length + " rear). " + string.Join("; ", log));
        }

        // nothing standing within the crater and its lip, and well clear of the trench, mound and truck road
        static bool Blocked(Vector2 c, float r, Transform debris, out string why)
        {
            why = "";
            if (c.y > -6f && c.y < 6f && c.x > -21f && c.x < 24f) { why = "trench"; return true; }
            if (c.x > 22f && c.x < 34f && c.y > -11f && c.y < -1f) { why = "spoil mound"; return true; }
            if (c.x > 30f && c.y < 6f) { why = "truck road"; return true; }
            var centre = new Vector3(c.x, 0f, c.y);
            foreach (var col in Physics.OverlapBox(centre, new Vector3(r * 1.7f, 50f, r * 1.7f)))
            {
                if (col is TerrainCollider) continue;
                if (col.transform.IsChildOf(debris)) continue;
                why = col.name; return true;
            }
            return false;
        }

        static void Dig(Terrain t, Vector2 c, float r, float depth, float throwDir, System.Random rng)
        {
            var td = t.terrainData; var o = t.transform.position; int res = td.heightmapResolution;
            float outer = r * 1.9f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - outer - o.x) / td.size.x * (res - 1)), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.y - outer - o.z) / td.size.z * (res - 1)), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + outer - o.x) / td.size.x * (res - 1)), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.y + outer - o.z) / td.size.z * (res - 1)), 0, res - 1);
            var h = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float lip = depth * .22f, off = (float)rng.NextDouble() * 50f;
            var throwV = new Vector2(Mathf.Cos(throwDir * Mathf.Deg2Rad), Mathf.Sin(throwDir * Mathf.Deg2Rad));
            for (int z = 0; z <= z1 - z0; z++)
                for (int x = 0; x <= x1 - x0; x++)
                {
                    var w = new Vector2(o.x + (x0 + x) / (float)(res - 1) * td.size.x, o.z + (z0 + z) / (float)(res - 1) * td.size.z);
                    var v = w - c; float dist = v.magnitude;
                    // irregular outline: the radius wobbles with angle
                    float ang = Mathf.Atan2(v.y, v.x);
                    float rr = r * (1f + (Mathf.PerlinNoise(Mathf.Cos(ang) * 1.3f + off, Mathf.Sin(ang) * 1.3f + off) - .5f) * .35f);
                    float d = dist / rr;
                    float dy;
                    if (d < 1f) dy = -depth * (1f - d * d) * (1f - d * d * .25f); // rounded bowl
                    else
                    {
                        // low lip, heavier on the throw side and broken by noise, gone by 1.9 r
                        float side = dist > .01f ? Vector2.Dot(v / dist, throwV) * .5f + .5f : .5f;
                        float fall = Mathf.Exp(-Mathf.Pow((d - 1.05f) / .35f, 2f));
                        float broken = Mathf.PerlinNoise(w.x * 1.7f + off, w.y * 1.7f + off);
                        dy = lip * fall * Mathf.Lerp(.35f, 1.2f, side) * Mathf.Lerp(.4f, 1.1f, broken);
                    }
                    h[z, x] += dy / td.size.y;
                }
            td.SetHeights(x0, z0, h);
        }

        // KPA craters behind the line: scorched core, wet churned mud, a broken spray of thrown red soil
        static void Paint(Terrain t, Vector2 c, float r, System.Random rng)
        {
            var td = t.terrainData; var o = t.transform.position; int L = td.alphamapLayers; float outer = r * 3f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - outer - o.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.y - outer - o.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + outer - o.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.y + outer - o.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
            var a = td.GetAlphamaps(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float off = (float)rng.NextDouble() * 50f;
            for (int z = 0; z <= z1 - z0; z++)
                for (int x = 0; x <= x1 - x0; x++)
                {
                    var w = new Vector2(o.x + (x0 + x + .5f) / td.alphamapWidth * td.size.x, o.z + (z0 + z + .5f) / td.alphamapHeight * td.size.z);
                    var v = w - c; float d = v.magnitude / r; float ang = Mathf.Atan2(v.y, v.x);
                    float rays = Mathf.PerlinNoise(Mathf.Cos(ang) * 3f + off, Mathf.Sin(ang) * 3f + off); // spokes of thrown soil
                    float burned = .45f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, 1.1f, d)));
                    float mud = .3f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, 1.4f, d)));
                    float soil = .55f * rays * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.9f, 2.9f, d + (rays - .5f))));
                    burned = Mathf.Max(a[z, x, Burned], burned); mud = Mathf.Max(a[z, x, Mud], mud); soil = Mathf.Max(a[z, x, Soil], soil);
                    float used = Mathf.Min(1f, burned + mud + soil); float s = used > 1e-5f ? Mathf.Min(1f, used) / (burned + mud + soil) : 0f;
                    burned *= s; mud *= s; soil *= s;
                    float rest = 0f; for (int l = 0; l < L; l++) if (l != Burned && l != Mud && l != Soil) rest += a[z, x, l];
                    float k = rest > 1e-5f ? Mathf.Max(0f, 1f - burned - mud - soil) / rest : 0f;
                    for (int l = 0; l < L; l++) if (l != Burned && l != Mud && l != Soil) a[z, x, l] *= k;
                    a[z, x, Burned] = burned; a[z, x, Mud] = mud; a[z, x, Soil] = soil;
                }
            td.SetAlphamaps(x0, z0, a);
        }
    }
}
