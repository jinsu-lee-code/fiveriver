using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FiveRivers.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Editor-only roundtrip: no scene switching, Editor.Exit, runtime or global settings writes.
    public static partial class ClaudeMapIntegration
    {
        public const string Stage = "C:/Users/mbc/Documents/ChatGPT/다섯개의강/Map_Import_20261030/ClaudeMapIntegration_20261030";
        public const string Art = "Assets/_Project/Art/Maps/ClaudePolish20261030";
        const string Map = Stage + "/ArtSource/Map";
        const string Derived = Art + "/M01_ClaudePolish_Terrain.asset";
        [Serializable] public class Placed { public string name, group; public Vector3 center, size; }
        [Serializable] public class Marker { public string id; public Vector3 pos; }
        [Serializable] public class Info
        {
            public string scene, terrainAssetPath, terrainSha256;
            public Vector3 terrainPos, terrainSize;
            public int heightRes, alphaRes, detailRes, roadChannel;
            public string[] layerNames;
            public float[] roadAverage, offRoadAverage;
            public List<Placed> objects = new List<Placed>();
            public List<Marker> markers = new List<Marker>();
        }
        static Terrain ActiveTerrain()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (EditorSceneManager.GetActiveScene().path != "Assets/_Project/Scenes/Dabudong.unity")
                throw new InvalidOperationException("Open the current five_river Dabudong scene.");
            var root = GameObject.Find("/[Map]/Dabudong_Imported");
            var terrains = root != null ? root.GetComponentsInChildren<Terrain>(true) : Array.Empty<Terrain>();
            if (terrains.Length != 1 || terrains[0].terrainData == null) throw new InvalidOperationException("Expected one current map terrain.");
            return terrains[0];
        }

        [MenuItem("FiveRivers/Map/Claude Export Current Terrain")]
        public static void Export()
        {
            var terrain = ActiveTerrain(); var data = terrain.terrainData;
            Directory.CreateDirectory(Map);
            string path = AssetDatabase.GetAssetPath(data);
            if (path == Derived) throw new InvalidOperationException("Do not overwrite the baseline export with derived terrain.");
            var info = new Info { scene = terrain.gameObject.scene.path, terrainAssetPath = path,
                terrainSha256 = Hash(path), terrainPos = terrain.transform.position, terrainSize = data.size,
                heightRes = data.heightmapResolution, alphaRes = data.alphamapResolution,
                detailRes = data.detailResolution, roadChannel = 3,
                layerNames = data.terrainLayers.Select(l => l != null ? l.name : "null").ToArray() };
            var heights = data.GetHeights(0, 0, info.heightRes, info.heightRes);
            using (var r16 = new BinaryWriter(File.Create(Map + "/heights.r16")))
            using (var f32 = new BinaryWriter(File.Create(Map + "/heights.f32")))
                for (int z = 0; z < info.heightRes; z++) for (int x = 0; x < info.heightRes; x++)
                { r16.Write((ushort)Mathf.RoundToInt(heights[z, x] * 65535f)); f32.Write(heights[z, x]); }
            int a = info.alphaRes; var alpha = data.GetAlphamaps(0, 0, a, a);
            if (alpha.GetLength(2) != 4) throw new InvalidOperationException("Inspect current layer order before adapting a non-four-layer terrain.");
            var colors = new Color[a * a];
            for (int z = 0; z < a; z++) for (int x = 0; x < a; x++)
                colors[z * a + x] = new Color(alpha[z, x, 0], alpha[z, x, 1], alpha[z, x, 2], alpha[z, x, 3]);
            SavePng(Map + "/splat_old.png", a, colors);
            info.roadAverage = new float[4]; info.offRoadAverage = new float[4];
            for (int i = 0; i < 8; i++)
            {
                float z = 25f + i * 10f, x = 38f + 7f * (z + 40f) / 150f;
                int zi = Mathf.Clamp(Mathf.RoundToInt((z - info.terrainPos.z) / data.size.z * (a - 1)), 0, a - 1);
                int xi = Mathf.Clamp(Mathf.RoundToInt((x - info.terrainPos.x) / data.size.x * (a - 1)), 0, a - 1);
                int off = Mathf.Clamp(Mathf.RoundToInt((x - 10f - info.terrainPos.x) / data.size.x * (a - 1)), 0, a - 1);
                for (int layer = 0; layer < 4; layer++)
                { info.roadAverage[layer] += alpha[zi, xi, layer] / 8f; info.offRoadAverage[layer] += alpha[zi, off, layer] / 8f; }
            }
            var root = GameObject.Find("/[Map]/Dabudong_Imported").transform;
            var obj = new StringBuilder(); int indexBase = 1;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var lod = renderer.GetComponentInParent<LODGroup>();
                if (lod != null && lod.GetLODs().Length > 0 && !lod.GetLODs()[0].renderers.Contains(renderer)) continue;
                info.objects.Add(new Placed { name = renderer.name, group = PathOf(renderer.transform), center = renderer.bounds.center, size = renderer.bounds.size });
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                obj.Append("o ").Append(renderer.name.Replace(' ', '_')).Append('\n');
                foreach (var v in mesh.vertices)
                {
                    var p = filter.transform.TransformPoint(v);
                    obj.AppendFormat(CultureInfo.InvariantCulture, "v {0} {1} {2}\n", -p.x, p.y, p.z);
                }
                var tri = mesh.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                    obj.AppendFormat(CultureInfo.InvariantCulture, "f {0} {1} {2}\n", indexBase + tri[i], indexBase + tri[i + 2], indexBase + tri[i + 1]);
                indexBase += mesh.vertexCount;
            }
            var points = Object.FindFirstObjectByType<LevelPoints>();
            if (points == null) throw new InvalidOperationException("Current gameplay LevelPoints missing.");
            foreach (var field in typeof(LevelPoints).GetFields())
            {
                var value = field.GetValue(points);
                if (value is Transform marker) info.markers.Add(new Marker { id = field.Name, pos = marker.position });
                if (value is Transform[] markers) foreach (var m in markers)
                    if (m != null) info.markers.Add(new Marker { id = field.Name + "/" + m.name, pos = m.position });
            }
            File.WriteAllText(Map + "/map.json", JsonUtility.ToJson(info, true));
            File.WriteAllText(Map + "/scene.obj", obj.ToString());
            File.Copy(info.scene, Map + "/baseline_scene.unity", true);
            File.WriteAllText(Map + "/colliders_before.json", ColliderSnapshot(root));
            Debug.Log("[ClaudeMap] Exported " + path + "; road average=" + string.Join(",", info.roadAverage) + "; markers=" + info.markers.Count);
        }
        static string ColliderSnapshot(Transform root) => Newtonsoft.Json.JsonConvert.SerializeObject(root.GetComponentsInChildren<Collider>(true)
            .Select(c => new { path = PathOf(c.transform), type = c.GetType().Name, position = c.transform.position.ToString("R"), rotation = c.transform.rotation.ToString("R"),
                enabled = c.enabled, trigger = c.isTrigger, material = c.sharedMaterial != null ? AssetDatabase.GetAssetPath(c.sharedMaterial) : "" }), Newtonsoft.Json.Formatting.Indented);
        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""); }
        static Texture2D Load(string path) { var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); t.LoadImage(File.ReadAllBytes(path)); return t; }
        static void SavePng(string path, int size, Color[] pixels) { var t = new Texture2D(size, size, TextureFormat.RGBA32, false, true); t.SetPixels(pixels); t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t); }
    }
}
