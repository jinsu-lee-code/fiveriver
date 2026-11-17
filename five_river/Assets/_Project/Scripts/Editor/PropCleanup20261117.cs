using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 소품 전수 점검 정리 (2026-11-17). 여러 번 실행해도 결과가 같다. 지우지 않고 끄기만 한다.
    //  1. 들판 엄폐벽 옆의 찢어진 마대 덩어리 (충돌체 없음, 돌무더기처럼 보임)
    //  2. 트럭 위치를 옮긴 뒤 쓰이지 않는 네모난 후진 길 조각
    //  3. 엄폐벽 모래주머니를 줄기가 뚫고 선 나무
    public static class PropCleanup20261117
    {
        [MenuItem("FiveRivers/Setup/Prop Cleanup 20261117")]
        public static void Run()
        {
            var log = new StringBuilder();
            var map = GameObject.Find("[Map]");
            if (map == null) { Debug.LogError("[PropCleanup] [Map] not found"); return; }
            var all = map.GetComponentsInChildren<Transform>(true);

            foreach (var t in all.Where(t => t.name == "PF_MS_SandbagTorn"))
                Off(t.gameObject, "torn sandbag", log);

            var road = all.FirstOrDefault(t => t.name == "DawnTruckRoad20261107");
            var reverse = road != null ? road.Find("Reverse") : null;
            if (reverse != null) Off(reverse.gameObject, "unused road patch", log);

            var walls = all.Where(t => t.name.StartsWith("CP") && t.name.EndsWith("_Wall"))
                .SelectMany(w => w.GetComponentsInChildren<Renderer>(false))
                // the sandbag renderers are named after their mesh (yd0jcch_...); the prefab name is on the parent (2026-11-17: this filter used to match nothing)
                .Where(r => r is MeshRenderer && !r.name.Contains("Torn") && (r.transform.parent == null || !r.transform.parent.name.Contains("Torn"))).Select(r => r.bounds).ToArray();
            string[] treeWords = { "pine", "Pine", "Poplar", "Tree", "tree" };
            foreach (var t in all)
            {
                if (!t.gameObject.activeInHierarchy || !treeWords.Any(t.name.Contains) || t.name.Contains("Fallen")) continue;
                if (t.GetComponentInParent<Terrain>() != null || t.GetComponent<Renderer>() == null && t.GetComponent<LODGroup>() == null) continue;
                // the trunk stands at the object's origin
                Vector3 p = t.position;
                bool through = false;
                foreach (var b in walls)
                {
                    if (p.x < b.min.x - .15f || p.x > b.max.x + .15f || p.z < b.min.z - .15f || p.z > b.max.z + .15f) continue;
                    if (p.y > b.max.y + .5f) continue;
                    through = true;
                    break;
                }
                // 기울어 선 나무는 기준점이 벽 밖에 있어도 줄기가 벽을 지난다: 메시 꼭짓점이 벽 안쪽에 드는지 본다
                var filter = t.GetComponent<MeshFilter>();
                var rend = t.GetComponent<Renderer>();
                if (!through && filter != null && filter.sharedMesh != null && rend != null && walls.Any(b => b.Intersects(rend.bounds)))
                {
                    var m = t.localToWorldMatrix;
                    var world = filter.sharedMesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
                    foreach (var b in walls)
                    {
                        if (!b.Intersects(rend.bounds)) continue;
                        var inner = new Bounds(b.center, b.size - new Vector3(.3f, .3f, .3f));
                        if (world.Count(inner.Contains) >= 3) { through = true; break; }
                    }
                }
                if (through) Off(t.gameObject, "tree through cover wall", log);
            }

            EditorSceneManager.MarkSceneDirty(map.scene);
            EditorSceneManager.SaveScene(map.scene);
            Directory.CreateDirectory("Logs/claude_probe/props");
            File.WriteAllText("Logs/claude_probe/props/cleanup.txt", log.ToString());
            Debug.Log("[PropCleanup] done\n" + log);
        }

        static void Off(GameObject go, string why, StringBuilder log)
        {
            log.AppendLine($"{why}\t{Path(go.transform)}\t{go.transform.position}\twas active {go.activeSelf}");
            if (!go.activeSelf) return;
            go.SetActive(false);
            EditorUtility.SetDirty(go);
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
