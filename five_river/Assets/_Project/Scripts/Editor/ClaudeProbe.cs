using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 임시 점검 도구 (2026-10-30 Claude). 씬을 저장하지 않는다. 작업이 끝나면 지운다.
    public static class ClaudeProbe
    {
        const string GitHubMap = "Assets/_Project/Art/Maps/Dabudong/SM_ENV_Dabudong_Map.prefab";
        const string Out = "Logs/claude_probe/";

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            var log = new StringBuilder();
            try
            {
                EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
                var map = GameObject.Find("/[Map]").transform;
                log.AppendLine("[Map] children: " + string.Join(", ", map.Cast<Transform>().Select(t => t.name + (t.gameObject.activeSelf ? "" : "(off)"))));
                var points = map.GetComponentInChildren<LevelPoints>(true);
                var player = GameObject.Find("/[Player]");
                var cam = player != null ? player.GetComponentInChildren<Camera>(true) : Camera.main;
                log.AppendLine("player cam: " + (cam != null ? cam.name + " " + cam.transform.position : "none"));
                Shot(cam, "cur_hip");
                Overview("cur_overview");
                Eye(points, "cur_eye");

                var imported = map.Find("Dabudong_Imported");
                if (imported != null) imported.gameObject.SetActive(false);
                var gh = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GitHubMap), map);
                gh.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Physics.SyncTransforms();
                var terrain = gh.GetComponentInChildren<Terrain>();
                log.AppendLine("github terrain: " + terrain.transform.position + " size " + terrain.terrainData.size);
                log.AppendLine("github roots: " + string.Join(", ", gh.transform.Cast<Transform>().Select(t => t.name + (t.gameObject.activeSelf ? "" : "(off)"))));
                void Probe(string name, Transform t)
                {
                    if (t == null) return;
                    var p = t.position;
                    float th = terrain.SampleHeight(p) + terrain.transform.position.y;
                    string hits = string.Join(" | ", Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore)
                        .OrderBy(h => h.distance).Take(4).Select(h => h.collider.name + "@" + h.point.y.ToString("F2")));
                    log.AppendLine($"{name} ({p.x:F1},{p.y:F2},{p.z:F1}) terrain={th:F2} hits: {hits}");
                }
                Probe("playerSpawn", points.playerSpawn);
                Probe("tankCheckpoint", points.tankCheckpoint);
                Probe("moundTop", points.moundTop);
                Probe("tankStart", points.tankStart);
                Probe("tankHold", points.tankHold);
                Probe("target", points.target);
                Probe("parkSpot", points.parkSpot);
                for (int i = 0; i < points.allySpots.Length; i++) Probe("ally" + i, points.allySpots[i]);
                for (int i = 0; i < points.ammoCrates.Length; i++) Probe("crate" + i, points.ammoCrates[i]);
                for (int i = 0; i < points.enemySpawns.Length; i++) Probe("enemySpawn" + i, points.enemySpawns[i]);
                for (int i = 0; i < points.assaultCovers.Length; i++) Probe("assault" + i, points.assaultCovers[i]);
                Shot(cam, "gh_hip");
                Overview("gh_overview");
                Eye(points, "gh_eye");
            }
            catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
            File.WriteAllText(Out + "probe.txt", log.ToString());
            EditorApplication.Exit(0);
        }

        static void Overview(string name)
        {
            var go = new GameObject("ProbeCam");
            var c = go.AddComponent<Camera>();
            c.transform.SetPositionAndRotation(new Vector3(5f, 45f, -45f), Quaternion.Euler(35f, 0f, 0f));
            c.fieldOfView = 60f; c.farClipPlane = 1000f;
            Shot(c, name);
            Object.DestroyImmediate(go);
        }

        static void Eye(LevelPoints points, string name)
        {
            var go = new GameObject("ProbeCam");
            var c = go.AddComponent<Camera>();
            c.transform.SetPositionAndRotation(points.playerSpawn.position + Vector3.up * 1.6f, Quaternion.Euler(4f, 0f, 0f));
            c.fieldOfView = 70f; c.farClipPlane = 1000f;
            Shot(c, name);
            Object.DestroyImmediate(go);
        }

        static void Shot(Camera c, string name)
        {
            if (c == null) return;
            var rt = new RenderTexture(1600, 900, 24);
            var old = c.targetTexture;
            c.targetTexture = rt;
            c.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Out + name + ".png", tex.EncodeToPNG());
            c.targetTexture = old;
            RenderTexture.active = null;
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
