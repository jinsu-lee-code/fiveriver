using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Replaces the boxy 6-course sandbag pillars at the hill cover points with low, wide walls like a dug-in 1950
    // rifle position: two courses of the scanned round sandbag row (about 0.9 m high, 2.7 m wide), the upper
    // course set back toward the defender, torn bags at the ends. The CP BoxCollider is resized to the new wall
    // (2.6 x 1.0 x 0.9 m, same ground line), so a crouched rifleman is still hidden and is seen when he stands.
    // The old pillar and the earlier perimeter dressing are hidden/removed; nothing else is touched.
    public static class CoverWalls20261107
    {
        const string Root = "Assets/_Project/Art/Megascans20261107/";
        static readonly Vector3 WallBox = new Vector3(2.6f, 1.0f, 0.9f);

        [MenuItem("FiveRivers/Map/Low Sandbag Cover Walls 20261107")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var map = GameObject.Find("/[Map]");
            var cover = GameObject.Find("/[Map]/Dabudong_Imported/M01_Greybox/Cover");
            var terrain = Terrain.activeTerrain;
            if (map == null || cover == null || terrain == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            if (map.transform.Find("CoverWalls20261107") != null) { Debug.Log("[Cover walls] Already applied."); return; }
            var round = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "SandbagRound/PF_MS_SandbagRound.prefab");
            var torn = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "SandbagTorn/PF_MS_SandbagTorn.prefab");
            if (round == null || torn == null) throw new System.InvalidOperationException("Megascans 20261107 prefabs are missing.");

            var parent = new GameObject("CoverWalls20261107");
            Undo.RegisterCreatedObjectUndo(parent, "Cover walls");
            parent.transform.SetParent(map.transform, false);
            var dressing = map.transform.Find("CoverDressing20261107");
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int walls = 0;
            foreach (Transform cp in cover.transform)
            {
                var pillar = renderers.Where(r => r != null && r.transform.parent != null && r.transform.parent.name == "Sandbag_Wall_Geometry"
                        && Vector3.Distance(r.bounds.center, cp.position) < 1.6f)
                    .Select(r => r.GetComponentInParent<LODGroup>()).FirstOrDefault(g => g != null);
                if (pillar == null) continue; // rock / log cover points keep their own shapes
                var box = cp.GetComponent<BoxCollider>();
                if (box == null) continue;
                var rng = new System.Random(cp.name.GetHashCode());
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

                // 1) hide the pillar, drop the earlier perimeter dressing around this point
                Undo.RecordObject(pillar.gameObject, "Hide sandbag pillar");
                pillar.gameObject.SetActive(false);
                if (dressing != null)
                    foreach (var child in dressing.Cast<Transform>().Where(c => Vector3.Distance(Flat(c.position), Flat(cp.position)) < 2.6f).ToList())
                        Undo.DestroyObjectImmediate(child.gameObject);

                // 2) resize the cover collider to the wall, keeping its ground line
                Undo.RecordObject(box, "Cover collider to wall");
                var s = cp.lossyScale;
                float bottom = cp.TransformPoint(box.center).y - box.size.y * s.y * .5f;
                box.size = new Vector3(WallBox.x / s.x, WallBox.y / s.y, WallBox.z / s.z);
                var centreWorld = new Vector3(cp.position.x, bottom + WallBox.y * .5f, cp.position.z);
                box.center = cp.InverseTransformPoint(centreWorld);

                // 3) the wall: two courses, upper one set back toward the defender (+z of the CP), ends in torn bags
                var wall = new GameObject(cp.name + "_Wall").transform;
                Undo.RegisterCreatedObjectUndo(wall.gameObject, "Cover wall");
                wall.SetParent(parent.transform, false);
                wall.SetPositionAndRotation(new Vector3(cp.position.x, bottom, cp.position.z), Quaternion.Euler(0f, cp.eulerAngles.y + R(-6f, 6f), 0f));
                var fwd = wall.forward;
                var right = wall.right;
                float lower = Course(round, wall, terrain, wall.position, wall.rotation * Quaternion.Euler(0f, R(-3f, 3f), 0f), new Vector3(R(.95f, 1f), R(1.0f, 1.08f), R(.68f, .74f)), .05f);
                Course(round, wall, terrain, wall.position + fwd * R(.04f, .08f) + Vector3.up * (lower - .08f),
                    wall.rotation * Quaternion.Euler(0f, R(-3f, 3f), 0f), new Vector3(R(.9f, .95f), R(1.05f, 1.12f), R(.66f, .72f)), 0f, false);
                foreach (float side in new[] { -1f, 1f })
                {
                    if (rng.NextDouble() < .25) continue;
                    var at = wall.position + right * side * R(1.38f, 1.52f) + fwd * R(-.2f, .2f);
                    Place(torn, wall, terrain, at, Quaternion.Euler(0f, R(0f, 360f), 0f), Vector3.one * R(1.15f, 1.35f), .03f);
                }
                walls++;
            }
            EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("[Cover walls] Built " + walls + " low sandbag walls; their cover colliders now " + WallBox + ".");
        }

        // One course of the scanned round-bag row. Returns its height above its base (for the next course).
        static float Course(GameObject prefab, Transform parent, Terrain t, Vector3 at, Quaternion rot, Vector3 scale, float sink, bool onGround = true)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (onGround) at.y = Height(t, at);
            var n = Normal(t, at);
            go.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, n, .5f)) * rot);
            go.transform.localScale = scale;
            var b = Bounds(go);
            go.transform.position += Vector3.up * (at.y - b.min.y - sink);
            return Bounds(go).max.y - at.y;
        }

        static void Place(GameObject prefab, Transform parent, Terrain t, Vector3 at, Quaternion yaw, Vector3 scale, float sink)
        {
            at.y = Height(t, at);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(at, Quaternion.FromToRotation(Vector3.up, Normal(t, at)) * yaw);
            go.transform.localScale = scale;
            var b = Bounds(go);
            go.transform.position += Vector3.up * (at.y - b.min.y - sink);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static float Height(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;

        static Vector3 Normal(Terrain t, Vector3 p)
        {
            var td = t.terrainData; var o = t.transform.position;
            return td.GetInterpolatedNormal((p.x - o.x) / td.size.x, (p.z - o.z) / td.size.z);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
