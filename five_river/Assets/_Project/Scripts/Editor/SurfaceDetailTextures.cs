using System.IO;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Procedural, periodic microdetail only. Existing albedo and UVs are never changed.
    public static class SurfaceDetailTextures
    {
        public const string DirectoryPath = "Assets/_Project/Art/SurfaceDetail20261030";
        const int Size = 512;

        [MenuItem("FiveRivers/Setup/Generate Surface Microdetail Textures")]
        public static void Generate()
        {
            Directory.CreateDirectory(DirectoryPath);
            GenerateSurface("Skin", 1.2f, 0.62f, 0.035f);
            GenerateSurface("Cotton", 1.8f, 0.87f, 0.04f);
            GenerateSurface("Parkerized", 0.65f, 0.63f, 0.055f);
            AssetDatabase.SaveAssets();
            Debug.Log("[Surfaces] Generated deterministic 512px repeating normal + roughness for Skin, Cotton, Parkerized. Albedo untouched.");
        }

        static void GenerateSurface(string name, float depth, float roughness, float variation)
        {
            var height = new float[Size * Size];
            var normal = new Color[height.Length];
            var rough = new Color[height.Length];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                float noise = Noise(u, v, 64, 37) * 0.6f + Noise(u, v, 128, 83) * 0.4f;
                float h;
                if (name == "Cotton")
                {
                    float warp = Mathf.Cos(u * Mathf.PI * 64f);
                    float weft = Mathf.Cos(v * Mathf.PI * 64f);
                    float weave = Mathf.Sin(u * Mathf.PI * 32f) * Mathf.Sin(v * Mathf.PI * 32f);
                    h = Mathf.Lerp(warp, weft, (weave + 1f) * 0.5f) * 0.3f + noise * 0.12f;
                }
                else if (name == "Skin")
                    h = noise * 0.13f - Pores(u, v) * 0.18f;
                else
                    h = noise * 0.5f + Noise(u, v, 32, 109) * 0.12f;
                height[y * Size + x] = h;
                float r = Mathf.Clamp01(roughness + (noise - 0.5f) * variation * 2f);
                // RGB is linear roughness. Alpha is inverse roughness for optional URP mask packing.
                rough[y * Size + x] = new Color(r, r, r, 1f - r);
            }
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = height[y * Size + (x + 1) % Size] - height[y * Size + (x + Size - 1) % Size];
                float dy = height[((y + 1) % Size) * Size + x] - height[((y + Size - 1) % Size) * Size + x];
                var n = new Vector3(-dx * depth, -dy * depth, 1f).normalized;
                normal[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            Write(name + "_DetailNormal", normal, true);
            Write(name + "_Roughness", rough, false);
        }

        static void Write(string name, Color[] colors, bool normal)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            texture.SetPixels(colors);
            texture.Apply(false);
            string path = DirectoryPath + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        static float Noise(float u, float v, int cells, uint seed)
        {
            float x = u * cells, y = v * cells;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float tx = Mathf.SmoothStep(0f, 1f, x - ix), ty = Mathf.SmoothStep(0f, 1f, y - iy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, cells, seed), Hash(ix + 1, iy, cells, seed), tx),
                Mathf.Lerp(Hash(ix, iy + 1, cells, seed), Hash(ix + 1, iy + 1, cells, seed), tx), ty);
        }

        static float Pores(float u, float v)
        {
            const int cells = 32;
            float x = u * cells, y = v * cells, result = 0f;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                float px = ix + i + 0.2f + Hash(ix + i, iy + j, cells, 13) * 0.6f;
                float py = iy + j + 0.2f + Hash(ix + i, iy + j, cells, 71) * 0.6f;
                float d = ((x - px) * (x - px) + (y - py) * (y - py)) / 0.012f;
                result += Mathf.Exp(-d);
            }
            return result;
        }

        static float Hash(int x, int y, int cells, uint seed)
        {
            x = (x % cells + cells) % cells;
            y = (y % cells + cells) % cells;
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + seed * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }
    }
}
