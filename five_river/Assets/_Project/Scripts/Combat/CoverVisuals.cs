using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FiveRivers.Combat
{
    // The art meshes of the cover (rocks, sandbag walls) that have no collider of their own: the map keeps a smaller
    // greybox box for play, and the boulder's own mesh collider is switched off (AssaultCover_1: a 3.2 x 3.1 m rock over
    // a 1.6 x 1.0 m box). Only for keeping a falling or lying body out of them (2026-11-16); play, bullets and paths
    // still use the colliders. Built once per scene, from the renderers whose names mark them as cover.
    public static class CoverVisuals
    {
        static readonly List<Bounds> boxes = new List<Bounds>();
        static int sceneHandle = -1;
        static readonly string[] Marks = { "cover", "boulder", "sandbag", "rock" };
        // the bounds are axis-aligned boxes round shapes that are not: keep the inner part of them
        const float Shrink = 0.8f;

        static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.handle == sceneHandle) return;
            sceneHandle = scene.handle;
            boxes.Clear();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var b = r.bounds;
                var s = b.size;
                float wide = Mathf.Max(s.x, s.z);
                if (s.y < 0.25f || s.y > 2.2f || wide < 0.6f || wide > 8f) continue;
                if (r.GetComponentInParent<EnemyAgent>() != null || r.GetComponentInParent<AllyCombatant>() != null
                    || r.GetComponentInParent<CharacterController>() != null || r.GetComponentInParent<Rigidbody>() != null) continue;
                bool marked = false;
                for (var t = r.transform; t != null && !marked; t = t.parent)
                {
                    var n = t.name.ToLowerInvariant();
                    foreach (var m in Marks) if (n.Contains(m)) { marked = true; break; }
                }
                if (!marked) continue;
                b.extents = new Vector3(b.extents.x * Shrink, b.extents.y, b.extents.z * Shrink);
                boxes.Add(b);
            }
        }

        // Is p within radius of a cover mesh (ignoring one that already holds 'except', the spot he knelt behind it)?
        public static bool Blocks(Vector3 p, float radius, Vector3 except)
        {
            Build();
            float r2 = radius * radius;
            foreach (var b in boxes)
                if (b.SqrDistance(p) < r2 && b.SqrDistance(except) > 0.01f) return true;
            return false;
        }

        // The cover mesh p lies in, if any.
        public static bool Inside(Vector3 p, out Vector3 centre)
        {
            Build();
            foreach (var b in boxes)
                if (b.Contains(p)) { centre = b.center; return true; }
            centre = default;
            return false;
        }
    }
}
