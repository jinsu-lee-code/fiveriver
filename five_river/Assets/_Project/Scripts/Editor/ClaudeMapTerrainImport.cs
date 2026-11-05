using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    public static partial class ClaudeMapIntegration
    {
        [Serializable] class Layer { public string name, albedo, normal; public float tile, smoothness; }
        [Serializable] class Layers { public List<Layer> layers; }
        [Serializable] class ImportResult
        {
            public int changedCells, protectedCells, protectedFloatMismatches;
            public bool sourceHashPreserved, renderColliderSameData, colliderSettingsPreserved;
            public string output;
        }

        [MenuItem("FiveRivers/Map/Claude Import Derived Terrain")]
        public static void Import()
        {
            var terrain = ActiveTerrain();
            var info = JsonUtility.FromJson<Info>(File.ReadAllText(Map + "/map.json"));
            if (Hash(info.terrainAssetPath) != info.terrainSha256) throw new InvalidOperationException("Baseline terrain changed since export.");
            var source = AssetDatabase.LoadAssetAtPath<TerrainData>(info.terrainAssetPath);
            if (source == null || source.heightmapResolution != info.heightRes || terrain.transform.position != info.terrainPos)
                throw new InvalidOperationException("Terrain identity or placement changed since export.");
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null) throw new InvalidOperationException("Existing TerrainCollider missing.");
            var input = File.ReadAllBytes(Map + "/heights.r16");
            var output = File.ReadAllBytes(Map + "/out/heights.r16");
            var mask = File.ReadAllBytes(Map + "/out/protection_mask.u8");
            int r = info.heightRes;
            if (input.Length != r * r * 2 || output.Length != input.Length || mask.Length != r * r)
                throw new InvalidDataException("Roundtrip array size mismatch.");
            var heights = source.GetHeights(0, 0, r, r);
            int changed = 0, protectedCount = 0;
            for (int z = 0; z < r; z++) for (int x = 0; x < r; x++)
            {
                int n = z * r + x, offset = n * 2;
                int before = input[offset] | input[offset + 1] << 8;
                int after = output[offset] | output[offset + 1] << 8;
                if (mask[n] != 0)
                {
                    protectedCount++;
                    if (before != after) throw new InvalidDataException("Protected r16 changed at " + n);
                }
                // Keep the exact original float wherever no height delta is requested.
                if (before != after) { heights[z, x] += (after - before) / 65535f; changed++; }
            }
            Directory.CreateDirectory(Art);
            AssetDatabase.Refresh();
            var derived = AssetDatabase.LoadAssetAtPath<TerrainData>(Derived);
            if (derived == source) throw new InvalidOperationException("The derived path must not be the source asset.");
            if (derived == null) { derived = Object.Instantiate(source); AssetDatabase.CreateAsset(derived, Derived); }
            else EditorUtility.CopySerialized(source, derived);
            derived.name = "M01_ClaudePolish_Terrain";
            derived.SetHeights(0, 0, heights);
            var actual = derived.GetHeights(0, 0, r, r);
            var original = source.GetHeights(0, 0, r, r);
            int mismatch = 0;
            for (int z = 0; z < r; z++) for (int x = 0; x < r; x++)
                if (mask[z * r + x] != 0 && actual[z, x] != original[z, x]) mismatch++;
            if (mismatch != 0) throw new InvalidOperationException("Unity protected float mismatch: " + mismatch);

            var layers = JsonUtility.FromJson<Layers>(File.ReadAllText(Map + "/out/layers.json"));
            if (layers.layers.Count != 5) throw new InvalidDataException("Expected five reviewed terrain layers.");
            var targets = new List<TerrainLayer>();
            foreach (var layer in layers.layers)
            {
                string albedo = CopyTerrainTexture(Map + "/out/" + layer.albedo, false);
                string normal = CopyTerrainTexture(Map + "/out/" + layer.normal, true);
                string path = Art + "/TL_Claude_" + layer.name + ".terrainlayer";
                var target = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (target == null) { target = new TerrainLayer(); AssetDatabase.CreateAsset(target, path); }
                target.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
                target.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
                target.tileSize = Vector2.one * layer.tile;
                target.smoothness = Mathf.Min(layer.smoothness, .12f);
                target.normalScale = .8f; target.metallic = 0f;
                EditorUtility.SetDirty(target); targets.Add(target);
            }
            derived.terrainLayers = targets.ToArray();
            var splat = Load(Map + "/out/splat.png");
            var splat2 = Load(Map + "/out/splat2.png");
            try
            {
                int a = info.alphaRes;
                if (splat.width != a || splat.height != a || splat2.width != a || splat2.height != a)
                    throw new InvalidDataException("Splat size mismatch.");
                var p0 = splat.GetPixels(); var p1 = splat2.GetPixels();
                var alpha = new float[a, a, 5];
                for (int z = 0; z < a; z++) for (int x = 0; x < a; x++)
                {
                    int n = z * a + x;
                    float sum = p0[n].r + p0[n].g + p0[n].b + p0[n].a + p1[n].r;
                    if (sum < .001f) throw new InvalidDataException("Empty splat pixel.");
                    alpha[z, x, 0] = p0[n].r / sum; alpha[z, x, 1] = p0[n].g / sum;
                    alpha[z, x, 2] = p0[n].b / sum; alpha[z, x, 3] = p0[n].a / sum;
                    alpha[z, x, 4] = p1[n].r / sum;
                }
                derived.SetAlphamaps(0, 0, alpha);
            }
            finally { Object.DestroyImmediate(splat); Object.DestroyImmediate(splat2); }
            // Keep August tree/detail prototypes and densities; only surface data changes.
            terrain.terrainData = derived;
            collider.terrainData = derived;
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            EditorUtility.SetDirty(derived);
            var root = GameObject.Find("/[Map]/Dabudong_Imported").transform;
            bool colliderSame = ColliderSnapshot(root) == File.ReadAllText(Map + "/colliders_before.json");
            if (!colliderSame) throw new InvalidOperationException("Collider settings/placement drifted before map import.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            var result = new ImportResult { changedCells = changed, protectedCells = protectedCount,
                protectedFloatMismatches = mismatch, sourceHashPreserved = Hash(info.terrainAssetPath) == info.terrainSha256,
                renderColliderSameData = terrain.terrainData == collider.terrainData,
                colliderSettingsPreserved = colliderSame, output = Derived };
            File.WriteAllText(Map + "/import_result.json", JsonUtility.ToJson(result, true));
            Debug.Log("[ClaudeMap] Derived terrain applied; source hash intact, protected float mismatch=0, collider settings preserved.");
        }

        static string CopyTerrainTexture(string source, bool normal)
        {
            string path = Art + "/" + Path.GetFileName(source);
            File.Copy(source, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true; importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat; importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return path;
        }
    }
}
