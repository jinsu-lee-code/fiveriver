using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FiveRivers.EditorTools
{
    // 2026-11-25 (사용자 지시 "나무가 맞지않으면 지금보다 실사에 가깝게 … 고쳐놔"): every Korean red pine on the map (the
    // Korean_Red_Pine scan and the older BG_Pine) is drawn as the re-made pine (RedPineSetup20261125) — a new pine stands at the
    // same foot, turned the same way and as tall as the old one, and the old one's renderers are switched off. Its colliders
    // stay, so cover, bullets and the NavMesh are as before. Re-running removes the pines this menu made and makes them again.
    public static class MapPineSwap20261125
    {
        const string Suffix = "_RedPine20261125";

        static bool IsOldPine(GameObject go)
        {
            if (go.name.EndsWith(Suffix)) return false;
            for (var t = go.transform; t != null; t = t.parent) if (t.name.StartsWith("PositionDressing")) return false;
            var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            var path = src != null ? AssetDatabase.GetAssetPath(src) : "";
            if (PrefabUtility.IsAnyPrefabInstanceRoot(go) && (path.EndsWith("Korean_Red_Pine.prefab") || path.EndsWith("Korean_Red_Pine.fbx"))) return true;
            // the older pine model, placed on its own
            var mf = go.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "BG_Pine" && go.GetComponentInParent<LODGroup>() == null;
        }

        [MenuItem("FiveRivers/Setup/Map Pines 20261125")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var scene = SceneManager.GetActiveScene();
            int removed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t != null && t.name.EndsWith(Suffix)).ToArray())
            { Undo.DestroyObjectImmediate(t.gameObject); removed++; }
            var pines = RedPineSetup20261125.Trees.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{RedPineSetup20261125.Folder}/{n}.fbx")).ToArray();
            float[] pineHeight = pines.Select(p => p.GetComponentsInChildren<MeshFilter>().Where(m => m.name.EndsWith("LOD0")).Select(m => m.sharedMesh.bounds.max.y).DefaultIfEmpty(9f).Max()).ToArray();
            var olds = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Select(t => t.gameObject).Where(IsOldPine).ToArray();
            int made = 0, hidden = 0;
            var rng = new System.Random(1950);
            foreach (var old in olds)
            {
                // all its renderers, also the ones a previous run switched off; size from the meshes (a disabled renderer has no bounds)
                var rs = old.GetComponentsInChildren<Renderer>(false).Where(r => r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null).ToArray();
                if (rs.Length == 0) continue;
                Bounds World(Renderer r)
                {
                    var mb = r.GetComponent<MeshFilter>().sharedMesh.bounds; var m = r.transform.localToWorldMatrix;
                    var wb = new Bounds(m.MultiplyPoint3x4(mb.center), Vector3.zero);
                    for (int c = 0; c < 8; c++) wb.Encapsulate(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
                    return wb;
                }
                var b = World(rs[0]); foreach (var r in rs) b.Encapsulate(World(r));
                float foot = b.min.y, h = b.size.y;
                int v = rng.Next(pines.Length);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pines[v], old.transform.parent);
                Undo.RegisterCreatedObjectUndo(go, "Map Pines");
                go.name = old.name + Suffix;
                go.transform.SetPositionAndRotation(new Vector3(old.transform.position.x, foot, old.transform.position.z), Quaternion.Euler(0f, old.transform.eulerAngles.y + (float)rng.NextDouble() * 40f, 0f));
                float s = Mathf.Clamp(h / pineHeight[v], .25f, 2.2f);
                go.transform.localScale = Vector3.one;
                go.transform.localScale = new Vector3(s, s, s);
                if (go.transform.parent != null) { var ps = go.transform.parent.lossyScale; go.transform.localScale = new Vector3(s / ps.x, s / ps.y, s / ps.z); }
                RedPineSetup20261125.TuneLods(go);
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(old));
                foreach (var r in rs) { Undo.RecordObject(r, "Map Pines"); r.enabled = false; hidden++; }
                made++;
            }
            var msg = $"map pines: old pines {olds.Length}, new pines {made} (removed {removed} from a previous run), old renderers switched off {hidden}, colliders kept";
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/map_pines.txt", msg);
            Debug.Log("[MapPineSwap20261125] " + msg);
            return msg;
        }
    }
}
