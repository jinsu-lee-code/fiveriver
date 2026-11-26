using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FiveRivers.EditorTools
{
    // 2026-11-25 (사용자 지시 "거리별로 식생이나 돌같은게 랜더링되는거 달라지게해놔"): grass, stones, litter, shrubs and trees that
    // were drawn at every distance (no LODGroup: the position dressing, the 11-02 frontline art, stumps, logs) now stop being
    // drawn past a distance set by what they are and how big — given in metres from the player's camera, turned into the
    // LODGroup's screen-height ratio with the current quality level's lodBias and the main camera's field of view — and fade
    // out (LOD cross-fade, on in PC_RPAsset) rather than pop. Small things also stop casting shadows (a pebble's shadow is
    // not seen; it costs a shadow-map draw). Objects that already had LOD levels keep them. Re-running gives the same result:
    // groups this menu made (one LOD holding the renderer on its own object) are re-tuned, not added twice.
    public static class DistanceDetail20261125
    {
        // what, how far it is drawn (m), whether it casts shadows
        internal enum Kind { None, Grass, Litter, Pebble, Stone, Rock, Shrub, Tree, Log, Cloth }

        // cover the enemy or the squad uses (rocks and sandbag walls in the field): drawn at every distance — an enemy at
        // 100 m kneels behind it, and a rock that faded out would leave him crouching behind nothing
        internal static bool IsCover(Renderer r) => Path(r.transform).ToLowerInvariant().Contains("cover");

        internal static Kind Classify(Renderer r) => IsCover(r) ? Kind.None : ClassifyAny(r);

        static Kind ClassifyAny(Renderer r)
        {
            var path = Path(r.transform).ToLowerInvariant();
            if (path.Contains("/people/") || path.Contains("player") || path.Contains("weapon") || path.Contains("mixamorig")) return Kind.None;
            var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
            var s = path + " " + (mesh ? mesh.name.ToLowerInvariant() : "") + " " + string.Join(",", r.sharedMaterials.Where(m => m).Select(m => m.name.ToLowerInvariant()));
            float size = r.bounds.size.magnitude;
            if (size > 12f) return Kind.None;                         // merged scatter meshes (wood fragments, cartridge cases) span the field
            if (s.Contains("grass") || s.Contains("tuft")) return Kind.Grass;
            if (s.Contains("litter") || s.Contains("twig")) return Kind.Litter;
            if (s.Contains("poncho")) return Kind.Cloth;
            if (s.Contains("fallenpine") || s.Contains("stump") || s.Contains("rfixh")) return Kind.Log;
            if (s.Contains("pebble") || s.Contains("rock") || s.Contains("stone") || s.Contains("rubble") || s.Contains("boulder") || s.Contains("granite"))
                return size < .35f ? Kind.Pebble : size < 1.5f ? Kind.Stone : Kind.Rock;
            if (s.Contains("shrub") || s.Contains("bush") || s.Contains("scrub")) return Kind.Shrub;
            if ((s.Contains("pine") && !s.Contains("spine")) || s.Contains("tree") || s.Contains("poplar") || s.Contains("cypress")) return Kind.Tree;
            return Kind.None;
        }

        static (float metres, bool shadows) Rule(Kind k)
        {
            switch (k)
            {
                case Kind.Grass: return (35f, false);
                case Kind.Litter: return (28f, false);
                case Kind.Pebble: return (25f, false);
                case Kind.Stone: return (60f, true);
                case Kind.Rock: return (160f, true);
                case Kind.Shrub: return (110f, true);
                case Kind.Log: return (100f, true);
                case Kind.Cloth: return (60f, true);
                case Kind.Tree: return (320f, true);
                default: return (0f, true);
            }
        }

        static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return "/" + s; }

        [MenuItem("FiveRivers/Setup/Distance Detail 20261125")]
        public static string Apply() => Apply(true);

        public static string Apply(bool save)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var cam = Camera.main != null ? Camera.main : Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).OrderByDescending(c => c.depth).FirstOrDefault();
            float fov = cam != null ? cam.fieldOfView : 60f;
            float bias = QualitySettings.lodBias;
            float k = 2f * Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            var counts = new System.Collections.Generic.Dictionary<Kind, (int made, int retuned, int keptLod, int shadowsOff)>();
            int removed = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is SkinnedMeshRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (r.gameObject.scene != SceneManager.GetActiveScene()) continue;
                var kind = Classify(r);
                if (kind == Kind.None || !r.gameObject.activeInHierarchy)
                {
                    // a group this menu put on something it should not have (cover, an object switched off): taken off again
                    var own = r.GetComponent<LODGroup>();
                    if (own != null && own.lodCount == 1 && ClassifyAny(r) != Kind.None && (IsCover(r) || !r.gameObject.activeInHierarchy)) { Undo.DestroyObjectImmediate(own); removed++; }
                    continue;
                }
                var (metres, shadows) = Rule(kind);
                counts.TryGetValue(kind, out var c);
                var group = r.GetComponentInParent<LODGroup>(true);
                bool ours = group != null && group.gameObject == r.gameObject && group.lodCount == 1;
                if (group != null && !ours) { c.keptLod++; counts[kind] = c; continue; }   // real LOD levels: leave them
                if (!shadows && r.shadowCastingMode != ShadowCastingMode.Off) { Undo.RecordObject(r, "Distance Detail"); r.shadowCastingMode = ShadowCastingMode.Off; c.shadowsOff++; }
                if (group == null) { group = Undo.AddComponent<LODGroup>(r.gameObject); c.made++; } else { Undo.RecordObject(group, "Distance Detail"); c.retuned++; }
                group.SetLODs(new[] { new LOD(1f, new[] { r }) });
                group.RecalculateBounds();
                // relative screen height at which it is culled = size * lodBias / (2 d tan(fov/2))
                float ratio = Mathf.Clamp(group.size * bias / (metres * k), .0005f, .9f);
                group.SetLODs(new[] { new LOD(ratio, new[] { r }) { fadeTransitionWidth = .25f } });
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                counts[kind] = c;
            }
            var sb = new StringBuilder($"distance detail: camera fov {fov:0.#}, lodBias {bias} (current quality level {QualitySettings.names[QualitySettings.GetQualityLevel()]})\n");
            foreach (var kv in counts.OrderBy(x => x.Key))
                sb.AppendLine($"  {kv.Key}: drawn to {Rule(kv.Key).metres} m, shadows {(Rule(kv.Key).shadows ? "on" : "off")} | new groups {kv.Value.made}, re-tuned {kv.Value.retuned}, own LOD kept {kv.Value.keptLod}, shadows turned off {kv.Value.shadowsOff}");
            sb.AppendLine($"  groups taken off cover / switched-off objects: {removed}; cover is drawn at every distance");
            if (save)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            File.WriteAllText("Temp/distance_detail.txt", sb.ToString());
            Debug.Log("[DistanceDetail20261125]\n" + sb);
            return sb.ToString();
        }
    }
}
