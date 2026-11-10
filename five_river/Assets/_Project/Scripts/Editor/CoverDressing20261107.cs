using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Makes the hill cover-point sandbag stacks read as dug-in positions instead of boxes standing on the grass.
    // Visual only: the CP BoxColliders, their positions and the cover routes are untouched.
    // Per CP: hide the buried graybox duplicate, seat and turn the scanned stack a little, add a slumped low
    // course and two torn bags at its base, and paint thrown-up earth around it (terrain paint only, no height).
    public static class CoverDressing20261107
    {
        const string Root = "Assets/_Project/Art/Megascans20261107/";
        const int Soil = 1, Mud = 3;
        const float EarthRadius = 2.2f, SoilPeak = .85f, MudPeak = .25f;

        [MenuItem("FiveRivers/Map/Dress Hill Cover Sandbags 20261107")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var cover = GameObject.Find("/[Map]/Dabudong_Imported/M01_Greybox/Cover");
            var map = GameObject.Find("/[Map]");
            var terrain = Terrain.activeTerrain;
            if (cover == null || map == null || terrain == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            var round = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "SandbagRound/PF_MS_SandbagRound.prefab");
            var torn = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "SandbagTorn/PF_MS_SandbagTorn.prefab");
            if (round == null || torn == null) throw new System.InvalidOperationException("Run Megascans 20261107 / Create Materials and Prefabs first.");

            // One-shot: the stacks are re-seated relative to their current pose, so never apply twice (use Undo instead).
            if (map.transform.Find("CoverDressing20261107") != null) { Debug.Log("[Cover dressing] Already applied."); return; }
            Tint(Root + "SandbagRound/M_MS_SandbagRound.mat", new Color(.85f, .85f, .85f, 1f));
            Tint(Root + "SandbagTorn/M_MS_SandbagTorn.mat", new Color(1.02f, 1.04f, 1.1f, 1f));
            var parent = new GameObject("CoverDressing20261107");
            Undo.RegisterCreatedObjectUndo(parent, "Dress cover sandbags");
            parent.transform.SetParent(map.transform, false);
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Cover earth paint");

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            int dressed = 0;
            foreach (Transform cp in cover.transform)
            {
                // the scanned stack (LODGroup parent of the Sandbag_Wall_Geometry renderers) belonging to this CP
                var wall = renderers.Where(r => r.transform.parent != null && r.transform.parent.name == "Sandbag_Wall_Geometry"
                        && Vector3.Distance(r.bounds.center, cp.position) < 1.5f)
                    .Select(r => r.GetComponentInParent<LODGroup>()).FirstOrDefault(g => g != null);
                if (wall == null) continue;
                var stack = wall.transform;
                var seed = new System.Random(cp.name.GetHashCode());
                float Rand(float a, float b) => a + (float)seed.NextDouble() * (b - a);

                // 1) the graybox duplicate is buried inside the scan; hide it (kept in the scene)
                foreach (var r in renderers.Where(r => r.name == cp.name + "_Sandbags"))
                {
                    Undo.RecordObject(r.gameObject, "Hide graybox cover duplicate");
                    r.gameObject.SetActive(false);
                }

                // 2) seat the scan: sink 12 cm, small yaw, lean 2-3 degrees so the faces are not plumb
                var n = Normal(terrain, stack.position);
                Undo.RecordObject(stack, "Seat cover stack");
                stack.SetPositionAndRotation(stack.position - Vector3.up * .12f,
                    Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, .3f)) * Quaternion.Euler(Rand(-3f, 3f), Rand(-6f, 6f), Rand(-3f, 3f)) * stack.rotation);

                // 3) a slumped low course hugging the base on one face (which face, how long and how high vary per
                //    position), and torn bags at the ends
                var fwd = Vector3.ProjectOnPlane(cp.forward, Vector3.up).normalized; if (fwd.sqrMagnitude < .01f) fwd = Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd);
                float face = seed.NextDouble() < .5 ? 1f : -1f;
                float len = Rand(.5f, .66f);
                Place(round, parent.transform, terrain, cp.position + fwd * (.18f * face) + right * Rand(-.2f, .2f),
                    Quaternion.LookRotation(-fwd * face) * Quaternion.Euler(0f, Rand(-10f, 10f), 0f), new Vector3(len, Rand(.38f, .48f), Rand(.45f, .55f)), .04f);
                if (seed.NextDouble() < .6)
                    Place(torn, parent.transform, terrain, cp.position + right * Rand(.9f, 1.1f) + fwd * Rand(-.25f, .25f),
                        Quaternion.Euler(0f, Rand(0f, 360f), 0f), Vector3.one * Rand(1.1f, 1.3f), .03f);
                Place(torn, parent.transform, terrain, cp.position - right * Rand(.9f, 1.1f) + fwd * Rand(-.25f, .25f),
                    Quaternion.Euler(0f, Rand(0f, 360f), 0f), Vector3.one * Rand(1.0f, 1.25f), .03f);

                // 4) thrown-up earth around the position
                // spoil thrown out toward the enemy side, as when the position was dug
                var spoil = cp.position + fwd * 1.1f;
                PaintEarth(terrain, new Vector2(spoil.x, spoil.z), new Vector2(fwd.x, fwd.z), seed);
                dressed++;
            }
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("[Cover dressing] Dressed " + dressed + " sandbag cover points (colliders unchanged).");
        }

        // Bring the separate scans to the stack's grey-brown: darker low course, less saturated torn bags.
        static void Tint(string path, Color c)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m == null) return;
            m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m);
        }

        static Vector3 Normal(Terrain t, Vector3 p)
        {
            var td = t.terrainData; var o = t.transform.position;
            return td.GetInterpolatedNormal((p.x - o.x) / td.size.x, (p.z - o.z) / td.size.z);
        }

        static void Place(GameObject prefab, Transform parent, Terrain t, Vector3 at, Quaternion yaw, Vector3 scale, float sink)
        {
            at.y = t.SampleHeight(at) + t.transform.position.y;
            var n = Normal(t, at);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(at - n * sink, Quaternion.FromToRotation(Vector3.up, n) * yaw);
            go.transform.localScale = scale;
        }

        // Elliptical spoil patch (long axis across the position), edge broken up with noise so it is not a disc.
        static void PaintEarth(Terrain t, Vector2 c, Vector2 fwd, System.Random seed)
        {
            var td = t.terrainData; var o = t.transform.position; int L = td.alphamapLayers;
            var across = new Vector2(fwd.y, -fwd.x); float off = (float)seed.NextDouble() * 100f;
            float R = EarthRadius;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((c.x - R - o.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((c.y - R - o.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((c.x + R - o.x) / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((c.y + R - o.z) / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
            var a = td.GetAlphamaps(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int z = 0; z <= z1 - z0; z++) for (int x = 0; x <= x1 - x0; x++)
            {
                float wx = o.x + (x0 + x + .5f) / td.alphamapWidth * td.size.x, wz = o.z + (z0 + z + .5f) / td.alphamapHeight * td.size.z;
                var v = new Vector2(wx, wz) - c;
                float d = Mathf.Sqrt(Mathf.Pow(Vector2.Dot(v, across) / R, 2f) + Mathf.Pow(Vector2.Dot(v, fwd) / (R * .55f), 2f));
                d += (Mathf.PerlinNoise(wx * 1.3f + off, wz * 1.3f + off) - .5f) * .55f;
                if (d >= 1f) continue;
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, d));
                float soil = Mathf.Max(a[z, x, Soil], SoilPeak * k), mud = Mathf.Max(a[z, x, Mud], MudPeak * k);
                float rest = 0f; for (int l = 0; l < L; l++) if (l != Soil && l != Mud) rest += a[z, x, l];
                float scale = rest > 1e-5f ? Mathf.Max(0f, 1f - soil - mud) / rest : 0f;
                for (int l = 0; l < L; l++) if (l != Soil && l != Mud) a[z, x, l] *= scale;
                a[z, x, Soil] = soil; a[z, x, Mud] = mud;
            }
            td.SetAlphamaps(x0, z0, a);
        }
    }
}
