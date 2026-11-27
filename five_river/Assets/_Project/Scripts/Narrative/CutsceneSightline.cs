using System.Collections.Generic;
using FiveRivers.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FiveRivers.Narrative
{
    // Keeps the trees, pine boughs and shrubs out of the line between a staged camera and what it shows (2026-11-27,
    // 검수 캡처 itch_20261126/raw/0070_Tank_048.9: a trunk across the middle of the tank; 0076_Tank_053.2: a bough over a
    // third of the frame; dawn/after_1cut_05 · 07: a tree in front of the student soldiers jumping off the truck).
    // While a cut runs, a dressing renderer whose box the camera's line to the subject passes through draws its shadow
    // only (ShadowCastingMode.ShadowsOnly: the light on the ground stays as it was), and it is put back exactly as it
    // was when the cut ends — run out, skipped, left by a retry or a section jump (the owners call Restore from every
    // way out, and from OnDestroy). Only renderers marked as dressing by their name or material (Marks) and not part
    // of cover (CoverMarks), a person, a vehicle or the terrain; their colliders are untouched (the player cannot act
    // during a cut anyway). A whole LOD group goes together. Presentation only.
    public sealed class CutsceneSightline
    {
        public static bool Enabled = true;
        public static string[] Marks = { "pine", "needle", "bough", "bush", "shrub", "poplar", "leaves", "leaf", "foliage", "tree", "bark", "branch" };
        public static string[] CoverMarks = { "cover", "sandbag", "boulder", "rock", "crate" };
        // how far short of the subject a box must begin to count as in front of it (m), and nothing nearer than this
        // to the lens is judged (the camera's own near plane)
        public static float ClearOfSubject = 0.6f, Near = 0.15f;

        // ---- every dressing renderer in the scene, found once per scene ----
        sealed class Group { public Renderer[] renderers; public Bounds bounds; }
        static readonly List<Group> groups = new List<Group>();
        static int sceneHandle = -1;
        // a renderer may be hidden by more than one cut at once: the mode it had before the first, and how many hold it
        static readonly Dictionary<Renderer, ShadowCastingMode> before = new Dictionary<Renderer, ShadowCastingMode>();
        static readonly Dictionary<Renderer, int> holders = new Dictionary<Renderer, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { groups.Clear(); sceneHandle = -1; before.Clear(); holders.Clear(); }

        readonly HashSet<Group> hidden = new HashSet<Group>();
        readonly Vector3[] points = new Vector3[4];

        public int HiddenCount => hidden.Count;

        static bool Has(string text, string[] marks)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var lower = text.ToLowerInvariant();
            foreach (var m in marks) if (lower.Contains(m)) return true;
            return false;
        }

        static bool IsDressing(Renderer r)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SkinnedMeshRenderer || r.forceRenderingOff) return false;
            if (r.GetComponentInParent<Animator>() != null || r.GetComponentInParent<TankController>() != null
                || r.GetComponentInParent<PlayerController>() != null || r.GetComponentInParent<Rigidbody>() != null) return false;
            bool marked = false;
            for (var t = r.transform; t != null; t = t.parent)
            {
                if (Has(t.name, CoverMarks)) return false;
                if (!marked && Has(t.name, Marks)) marked = true;
            }
            if (!marked)
                foreach (var m in r.sharedMaterials)
                    if (m != null && Has(m.name, Marks)) { marked = true; break; }
            return marked;
        }

        static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.handle == sceneHandle) return;
            sceneHandle = scene.handle;
            groups.Clear();
            var seen = new HashSet<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null || seen.Contains(r) || !r.enabled || !IsDressing(r)) continue;
                var lod = r.GetComponentInParent<LODGroup>();
                Renderer[] members;
                if (lod != null)
                {
                    var list = new List<Renderer>();
                    foreach (var level in lod.GetLODs())
                        foreach (var m in level.renderers)
                            if (m != null && m.enabled && !list.Contains(m)) list.Add(m);
                    if (!list.Contains(r)) list.Add(r);
                    members = list.ToArray();
                }
                else members = new[] { r };
                var bounds = members[0].bounds;
                foreach (var m in members) { seen.Add(m); bounds.Encapsulate(m.bounds); }
                groups.Add(new Group { renderers = members, bounds = bounds });
            }
        }

        // Hides what stands between this eye and the subject (centre, half-width, height) this frame. What is hidden
        // stays hidden until Restore, so nothing pops back in and out while the camera moves.
        public void Clear(Vector3 eye, Vector3 centre, float radius, float height)
        {
            if (!Enabled) return;
            Build();
            int n = SightlineMath.SubjectPoints(eye, centre, radius, height, points);
            var reach = new Bounds(eye, Vector3.zero);
            for (int i = 0; i < n; i++) reach.Encapsulate(points[i]);
            foreach (var g in groups)
            {
                if (hidden.Contains(g) || !g.bounds.Intersects(reach)) continue;
                for (int i = 0; i < n; i++)
                {
                    if (!SightlineMath.Blocks(eye, points[i], g.bounds, ClearOfSubject, Near)) continue;
                    Hide(g);
                    break;
                }
            }
        }

        void Hide(Group g)
        {
            hidden.Add(g);
            foreach (var r in g.renderers)
            {
                if (r == null) continue;
                if (!holders.TryGetValue(r, out int count) || count <= 0)
                {
                    before[r] = r.shadowCastingMode;
                    count = 0;
                }
                holders[r] = count + 1;
                // a renderer that cast no shadow has nothing to leave behind: it is simply not drawn
                if (r.shadowCastingMode == ShadowCastingMode.Off) r.forceRenderingOff = true;
                else r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        // Puts back everything this cut hid. Safe to call more than once.
        public void Restore()
        {
            foreach (var g in hidden)
                foreach (var r in g.renderers)
                {
                    if (r == null) continue;
                    if (!holders.TryGetValue(r, out int count)) continue;
                    if (--count > 0) { holders[r] = count; continue; }
                    holders.Remove(r);
                    if (before.TryGetValue(r, out var mode))
                    {
                        if (mode == ShadowCastingMode.Off) r.forceRenderingOff = false;
                        else r.shadowCastingMode = mode;
                        before.Remove(r);
                    }
                }
            hidden.Clear();
        }
    }
}
