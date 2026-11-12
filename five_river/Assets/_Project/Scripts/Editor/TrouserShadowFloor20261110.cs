using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // The ROK soldiers' baked body textures have near-black texels on trouser faces that stay hidden while they
    // stand: the top of the trousers under the jacket hem and the inside of the thighs (some of those faces are
    // mapped onto the unused black border of the atlas). A long running stride pulls them into view as a black
    // wedge between the legs. This lifts those texels, on the hip and thigh faces only, to the colour of shadowed
    // trouser cloth. The delivered PNG is not touched: a copy is written to SurfaceDetail20261110 and the body
    // material's Base Map is pointed at it (to undo, set the Base Map back to the texture named in the copy's
    // importer user data). Safe to run more than once: it always starts again from the delivered texture.
    public static class TrouserShadowFloor20261110
    {
        const string Folder = "Assets/_Project/Art/SurfaceDetail20261110";
        const string Suffix = "_TrouserShadowFloor";
        // Texels darker than this share of the lit trouser cloth are lifted towards it.
        public const float Floor = 0.55f;
        static readonly string[] Bones = { "Hips", "LeftUpLeg", "RightUpLeg" };
        static readonly string[] SleeveBones = { "LeftArm", "RightArm" };

        [MenuItem("FiveRivers/Characters/Lift Trouser Shadow Floor 20261110")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[TrouserShadowFloor] Edit mode only."); return; }
            var done = new HashSet<Material>();
            foreach (var renderer in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include))
            {
                var material = renderer.sharedMaterials.Length > 0 ? renderer.sharedMaterials[0] : null;
                if (material == null || !material.name.Contains("Baked_Body") || !AssetDatabase.Contains(material) || !done.Add(material)) continue;
                var current = material.GetTexture("_BaseMap");
                string currentPath = AssetDatabase.GetAssetPath(current);
                if (string.IsNullOrEmpty(currentPath)) continue;
                // already pointing at a copy: go back to the delivered texture it was made from
                string sourcePath = Path.GetFileNameWithoutExtension(currentPath).EndsWith(Suffix)
                    ? AssetImporter.GetAtPath(currentPath).userData : currentPath;
                var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                if (source == null) { Debug.LogError("[TrouserShadowFloor] source texture missing for " + material.name + ": " + sourcePath); continue; }

                var lifted = Build(renderer, source, out int count);
                if (lifted == null) { Debug.LogWarning("[TrouserShadowFloor] nothing to lift on " + renderer.sharedMesh.name); continue; }
                // Park wears his sleeves rolled: the inside of each sleeve opening is baked black too. The other
                // riflemen's sleeves are closed at the wrist, and their folds are left as delivered.
                if (material.name.Contains("ParkYongpal"))
                {
                    var sleeves = Build(renderer, lifted, out int more, SleeveBones);
                    if (sleeves != null)
                    {
                        Object.DestroyImmediate(lifted);
                        lifted = sleeves;
                        count += more;
                    }
                }
                Directory.CreateDirectory(Folder);
                // both delivered textures are called Body_BaseColor: name the copy after the character folder
                string character = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(sourcePath)));
                string path = Folder + "/" + character + "_" + Path.GetFileNameWithoutExtension(sourcePath) + Suffix + ".png";
                File.WriteAllBytes(path, lifted.EncodeToPNG());
                Object.DestroyImmediate(lifted);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var from = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
                var to = (TextureImporter)AssetImporter.GetAtPath(path);
                to.textureType = from.textureType;
                to.sRGBTexture = from.sRGBTexture;
                to.maxTextureSize = from.maxTextureSize;
                to.textureCompression = from.textureCompression;
                to.mipmapEnabled = from.mipmapEnabled;
                to.wrapMode = from.wrapMode;
                to.filterMode = from.filterMode;
                to.anisoLevel = from.anisoLevel;
                to.alphaSource = from.alphaSource;
                to.userData = sourcePath;
                to.SaveAndReimport();

                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                EditorUtility.SetDirty(material);
                Debug.Log("[TrouserShadowFloor] " + material.name + ": lifted " + count + " texels -> " + path);
            }
            AssetDatabase.SaveAssets();
        }

        // The corrected copy of the body texture for this renderer's sub-mesh 0, or null when it has no hip or
        // thigh faces. lifted: how many texels were changed.
        public static Texture2D Build(SkinnedMeshRenderer renderer, Texture source, out int lifted) => Build(renderer, source, out lifted, Bones);

        // bones: the faces to treat are those whose three corners all follow one of these bones.
        public static Texture2D Build(SkinnedMeshRenderer renderer, Texture source, out int lifted, string[] bones)
        {
            lifted = 0;
            var mesh = renderer.sharedMesh;
            int w = source.width, h = source.height;
            var pixels = ReadPixels(source, w, h);
            var uv = mesh.uv;
            var weights = mesh.boneWeights;
            var triangles = mesh.GetTriangles(0);
            var legBone = new bool[renderer.bones.Length];
            for (int i = 0; i < legBone.Length; i++)
                foreach (var name in bones)
                    if (renderer.bones[i] != null && renderer.bones[i].name.EndsWith(name)) legBone[i] = true;

            // what lit trouser cloth looks like on this character
            double r = 0, g = 0, b = 0; int lit = 0;
            var seen = new HashSet<int>();
            foreach (int v in triangles)
            {
                if (!legBone[weights[v].boneIndex0] || !seen.Add(v)) continue;
                var c = pixels[Index(uv[v], w, h)];
                if (Luminance(c) <= 0.25f) continue;
                r += c.r; g += c.g; b += c.b; lit++;
            }
            if (lit == 0) return null;
            var floor = new Color32((byte)(r / lit * Floor), (byte)(g / lit * Floor), (byte)(b / lit * Floor), 255);
            float floorLuminance = Luminance(floor);

            var visited = new bool[w * h];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int i0 = triangles[t], i1 = triangles[t + 1], i2 = triangles[t + 2];
                if (!legBone[weights[i0].boneIndex0] || !legBone[weights[i1].boneIndex0] || !legBone[weights[i2].boneIndex0]) continue;
                var p0 = new Vector2(Mathf.Repeat(uv[i0].x, 1f) * w, Mathf.Repeat(uv[i0].y, 1f) * h);
                var p1 = new Vector2(Mathf.Repeat(uv[i1].x, 1f) * w, Mathf.Repeat(uv[i1].y, 1f) * h);
                var p2 = new Vector2(Mathf.Repeat(uv[i2].x, 1f) * w, Mathf.Repeat(uv[i2].y, 1f) * h);
                // two texels of bleed past the edges, so filtering and mip levels do not bring the black back
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x)) - 2), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x)) + 2);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y)) - 2), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y)) + 2);
                if (x1 - x0 > w / 2 || y1 - y0 > h / 2) continue; // a face that wraps round the atlas edge
                float area = (p1.x - p0.x) * (p2.y - p0.y) - (p2.x - p0.x) * (p1.y - p0.y);
                float margin = Mathf.Abs(area) > 1e-6f ? 2f * Mathf.Max(x1 - x0, y1 - y0) / Mathf.Abs(area) : float.MaxValue;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int index = y * w + x;
                        if (visited[index]) continue;
                        if (margin < float.MaxValue)
                        {
                            float px = x + 0.5f, py = y + 0.5f;
                            float a = ((p1.x - px) * (p2.y - py) - (p2.x - px) * (p1.y - py)) / area;
                            float bb = ((p2.x - px) * (p0.y - py) - (p0.x - px) * (p2.y - py)) / area;
                            float cc = 1f - a - bb;
                            if (a < -margin || bb < -margin || cc < -margin) continue;
                        }
                        visited[index] = true;
                        var c = pixels[index];
                        float luminance = Luminance(c);
                        if (luminance >= floorLuminance) continue;
                        // black goes all the way to the floor; cloth near the floor keeps most of its own shading
                        float k = 1f - luminance / floorLuminance;
                        pixels[index] = new Color32((byte)Mathf.Lerp(c.r, floor.r, k), (byte)Mathf.Lerp(c.g, floor.g, k), (byte)Mathf.Lerp(c.b, floor.b, k), c.a);
                        lifted++;
                    }
            }
            var result = new Texture2D(w, h, TextureFormat.RGBA32, true);
            result.SetPixels32(pixels);
            result.Apply(true);
            return result;
        }

        static Color32[] ReadPixels(Texture source, int w, int h)
        {
            // the delivered textures are imported compressed and not readable: read them back through a blit
            var target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            RenderTexture.active = target;
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            var pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        static int Index(Vector2 uv, int w, int h) =>
            Mathf.Clamp((int)(Mathf.Repeat(uv.y, 1f) * h), 0, h - 1) * w + Mathf.Clamp((int)(Mathf.Repeat(uv.x, 1f) * w), 0, w - 1);

        static float Luminance(Color32 c) => (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
    }
}
