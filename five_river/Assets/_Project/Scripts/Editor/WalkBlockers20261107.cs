using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Stops people walking through solid map objects that had no collider: standing pines get a trunk capsule (not
    // the canopy), boulders / fallen pines / scanned sandbag rows get a collider of their own mesh, the barbed-wire
    // runs and stakes an oriented box, and the curved ends of the hill cover walls (they stick out of the 2.6 m CP
    // box) a mesh collider too. Bushes, small torn bags and the flat truck-road ruts stay passable on purpose.
    // All added colliders live under [Map]/WalkBlockers20261107 (rebuilt on every run, so re-running is safe);
    // the runtime NavMesh is built from colliders, so enemies and allies route around them as well.
    public static class WalkBlockers20261107
    {
        const float MinHeight = 0.35f, MinFootprint = 0.3f, TrunkRadius = 0.22f;

        [MenuItem("FiveRivers/Map/Block Walk-Through Objects 20261107")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var map = GameObject.Find("/[Map]");
            if (map == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            var old = map.transform.Find("WalkBlockers20261107");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            Physics.SyncTransforms();
            var holder = new GameObject("WalkBlockers20261107").transform;
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "Walk blockers");
            holder.SetParent(map.transform, false);
            holder.gameObject.isStatic = true;

            var added = new Dictionary<string, int>();
            void Count(string k) => added[k] = added.TryGetValue(k, out int n) ? n + 1 : 1;

            // 1) the hill cover wall courses: their curled ends leave the CP box
            var walls = map.transform.Find("CoverWalls20261107");
            if (walls != null)
                foreach (var mf in walls.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null && IsCourse(mf.transform))
                    { MeshBlocker(holder, mf); Count("cover wall course"); }
            Physics.SyncTransforms();

            // 2) every other standing object without any collider
            foreach (var r in map.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || r.forceRenderingOff || r.transform.IsChildOf(holder)) continue;
                var b = r.bounds;
                if (b.size.y < MinHeight || Mathf.Max(b.size.x, b.size.z) < MinFootprint || b.size.x > 40f || b.size.z > 40f) continue;
                if (r.GetComponentInParent<Animator>() != null) continue;
                string n = (r.GetComponent<MeshFilter>()?.sharedMesh?.name ?? "") + "|" + r.name + "|" + r.transform.parent?.name;
                if (n.Contains("Bush") || n.Contains("Shrub") || n.Contains("Torn") || n.Contains("DawnTruckRoad") || n.Contains("Grass")) continue;
                if (HasCollider(b)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if ((n.Contains("Pine") || n.Contains("pine") || n.Contains("Tree")) && b.size.y > 3f && !n.Contains("Fallen") && !n.Contains("rfixH"))
                { TrunkBlocker(holder, r, b); Count("standing pine trunk"); }
                else if (n.Contains("Fallen") || n.Contains("rfixH"))
                { LogBlocker(holder, mf); Count("fallen pine"); }
                else if (n.Contains("wire") || n.Contains("Wire") || n.Contains("Stakes"))
                { BoxBlocker(holder, mf); Count("barbed wire / stakes"); }
                else { MeshBlocker(holder, mf); Count(r.name); }
                Physics.SyncTransforms();
            }
            EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("[Walk blockers] " + string.Join(", ", added.Select(kv => kv.Value + " " + kv.Key)));
        }

        static bool IsCourse(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name.StartsWith("PF_MS_SandbagRound") || p.name == "Course3") return true;
            return false;
        }

        static bool HasCollider(Bounds b)
        {
            foreach (var c in Physics.OverlapBox(b.center, b.extents * 0.7f))
                if (!(c is TerrainCollider) && !c.isTrigger) return true;
            return false;
        }

        static Transform Twin(Transform holder, Transform source, string label)
        {
            var go = new GameObject(label + "_" + source.name);
            go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(source.position, source.rotation);
            go.transform.localScale = source.lossyScale;
            go.isStatic = true;
            return go.transform;
        }

        static void MeshBlocker(Transform holder, MeshFilter mf)
        {
            var mc = Twin(holder, mf.transform, "Mesh").gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
        }

        // wire: from the ground up to 1.2 m, so nobody steps over the low strands; it never stops bullets
        // (Ignore Raycast layer), only bodies
        const float WireHeight = 1.2f;
        static void BoxBlocker(Transform holder, MeshFilter mf)
        {
            var t = Twin(holder, mf.transform, "Box");
            t.gameObject.layer = 2;
            var box = t.gameObject.AddComponent<BoxCollider>();
            var mb = mf.sharedMesh.bounds;
            var terrain = Terrain.activeTerrain;
            var centre = t.TransformPoint(mb.center);
            float ground = terrain != null ? terrain.SampleHeight(centre) + terrain.transform.position.y : t.TransformPoint(mb.min).y;
            float sy = Mathf.Max(1e-4f, t.lossyScale.y);
            var c = mb.center; var s = mb.size;
            s.y = WireHeight / sy;
            c.y = t.InverseTransformPoint(new Vector3(centre.x, ground + WireHeight * .5f, centre.z)).y;
            box.center = c; box.size = s;
        }

        // a fallen pine: its thin trunk and branches let a character controller step up and over, so the whole
        // lying tree is boxed (stops bullets too, as a log would)
        static void LogBlocker(Transform holder, MeshFilter mf)
        {
            var box = Twin(holder, mf.transform, "Log").gameObject.AddComponent<BoxCollider>();
            box.center = mf.sharedMesh.bounds.center;
            box.size = mf.sharedMesh.bounds.size;
        }

        // trunk only: a capsule from the ground up to about two thirds of the tree, at the bottom-centre of the mesh
        static void TrunkBlocker(Transform holder, Renderer r, Bounds b)
        {
            var go = new GameObject("Trunk_" + r.name);
            go.transform.SetParent(holder, false);
            go.isStatic = true;
            float h = b.size.y * 0.66f;
            // the trunk stands where the mesh meets the ground, not at the (canopy-biased) bounds centre
            var base_ = new Vector3(b.center.x, 0f, b.center.z);
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
            {
                var sum = Vector3.zero; int n = 0;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = r.transform.TransformPoint(v);
                    if (w.y < b.min.y + 0.5f) { sum += w; n++; }
                }
                if (n > 0) base_ = sum / n;
            }
            go.transform.position = new Vector3(base_.x, b.min.y + h * 0.5f, base_.z);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = TrunkRadius;
            cap.height = h;
            cap.direction = 1;
        }
    }
}
