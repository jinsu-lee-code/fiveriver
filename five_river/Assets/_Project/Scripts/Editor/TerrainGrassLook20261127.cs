using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-27 (조명탄 첫 연출 컷 아트 검수: "조명탄 아래 풀이 땅보다 밝고 음영이 없어 떠 보인다"):
    // the terrain's detail grass stood far brighter than the soil it grows from under a flare, flat from root to tip.
    // How it is drawn (URP WavingGrassBillboard): colour = texture x prototype colour (healthy..dry by noise) x 2 x
    // lerp(0.5, wavingGrassTint in linear, wave); lit by the terrain's own normal, no specular. That billboard shader has no
    // probe-volume variant, so the grass gets NO indirect light (measured: with the directional lights off the soil stays at
    // 5 of 255 and the grass goes to 0). Its albedo had been set high enough to look right by moonlight, where the moon is
    // all that lights it; right under a flare, all direct light, that albedo showed as it is: about 1.2 x the soil, tips 1.4-1.5 x.
    // So the numbers below are a middle: under the flare the grass sits at the soil's brightness with only its tips above
    // it, and by night and at dawn it reads darker than the soil (it was 0.9 x by night already).
    //  - healthy / dry colours down and closer together (the dry end made the brightest tufts); the healthy green a little
    //    less green (it stood out as lime at dawn, when the night's desaturation is off)
    //  - derived textures (<name>_Rooted.png, the originals stay): darker toward the root, so a tuft has a foot in the ground
    //  - wavingGrassTint a touch under neutral (0.735 in sRGB is the neutral 0.5): a passing wave does not brighten the grass
    // Tried and dropped: DetailRenderMode.Grass (that shader has the probe-volume variant, but it still came out without
    // indirect light, and its unturned quads read as tilted boards from the side).
    // Only the terrain data asset changes; the scene is not touched. Numbers: BALANCE.md 4-2. Re-running gives the same
    // result (the PNGs are rewritten only when their bytes change).
    public static class TerrainGrassLook20261127
    {
        const string Dir = "Assets/_Project/Art/Maps/Dabudong1950/";
        const string TerrainDataPath = "Assets/_Project/Art/MapPolish20261105/Frontline_SandbagTrench_20261105.asset";

        // ---- numbers (BALANCE.md 4-2)
        public static readonly Color WavingTint = new Color(0.69f, 0.73f, 0.55f, 1f);
        // texture: value x RootShade at the ground, rising to x 1 at RootHeight of the tuft's height
        public const float RootShade = 0.78f, RootHeight = 0.50f;
        // source texture, healthy colour, dry colour (before: 0.34 0.44 0.21 / 0.54 0.53 0.29, and 0.46 0.52 0.28 / 0.66 0.62 0.38)
        static readonly (string source, Color healthy, Color dry)[] Grass =
        {
            ("T_Grass_DryTuft", new Color(0.33f, 0.41f, 0.20f), new Color(0.48f, 0.47f, 0.26f)),
            ("T_Grass_SeedingTall", new Color(0.44f, 0.49f, 0.27f), new Color(0.59f, 0.56f, 0.34f)),
        };
        const string Suffix = "_Rooted";

        [MenuItem("FiveRivers/Setup/Terrain Grass Look 20261127")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null) throw new System.InvalidOperationException("지형 자료가 없음: " + TerrainDataPath);
            var sb = new StringBuilder();
            var prototypes = data.detailPrototypes;
            int changed = 0;
            foreach (var (source, healthy, dry) in Grass)
            {
                var rooted = Rooted(source, sb);
                if (rooted == null) continue;
                for (int i = 0; i < prototypes.Length; i++)
                {
                    var p = prototypes[i];
                    string name = p.prototypeTexture != null ? p.prototypeTexture.name : "";
                    if (name != source && name != source + Suffix) continue;
                    if (p.prototypeTexture != rooted || p.healthyColor != healthy || p.dryColor != dry) changed++;
                    p.prototypeTexture = rooted; p.healthyColor = healthy; p.dryColor = dry;
                    sb.AppendLine($"  prototype {i}: {rooted.name}, healthy {healthy}, dry {dry}");
                }
            }
            if (data.wavingGrassTint != WavingTint) changed++;
            if (changed > 0)
            {
                Undo.RegisterCompleteObjectUndo(data, "Terrain Grass Look");
                data.detailPrototypes = prototypes;   // same count and order: the planted detail layers stay as they are
                data.wavingGrassTint = WavingTint;
                data.RefreshPrototypes();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            sb.AppendLine($"  waving tint {data.wavingGrassTint}; {(changed > 0 ? "terrain data saved" : "nothing to change")}");
            Debug.Log("[TerrainGrassLook]\n" + sb);
            return sb.ToString();
        }

        // The tuft drawn again from the original PNG: darker toward the root. Alpha as it was.
        static Texture2D Rooted(string source, StringBuilder sb)
        {
            string from = Dir + source + ".png", to = Dir + source + Suffix + ".png";
            if (!File.Exists(from)) { sb.AppendLine("  missing " + from); return null; }
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            byte[] png;
            try
            {
                t.LoadImage(File.ReadAllBytes(from));
                var px = t.GetPixels32();   // row 0 is the bottom of the image: the root
                int w = t.width, h = t.height;
                for (int y = 0; y < h; y++)
                {
                    float root = Mathf.Lerp(RootShade, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, RootHeight, y / (h - 1f))));
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[y * w + x];
                        c.r = Shade(c.r, root); c.g = Shade(c.g, root); c.b = Shade(c.b, root);
                        px[y * w + x] = c;
                    }
                }
                t.SetPixels32(px);
                png = t.EncodeToPNG();
            }
            finally { Object.DestroyImmediate(t); }
            bool wrote = !File.Exists(to) || !File.ReadAllBytes(to).SequenceEqual(png);
            if (wrote) { File.WriteAllBytes(to, png); AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceSynchronousImport); }
            // imported as the original is (mips that keep the cut-out's coverage, clamped, alpha as transparency)
            var a = (TextureImporter)AssetImporter.GetAtPath(from); var b = (TextureImporter)AssetImporter.GetAtPath(to);
            var want = new TextureImporterSettings(); a.ReadTextureSettings(want);
            var have = new TextureImporterSettings(); b.ReadTextureSettings(have);
            var platform = a.GetDefaultPlatformTextureSettings();
            if (!TextureImporterSettings.Equal(want, have) || b.GetDefaultPlatformTextureSettings().maxTextureSize != platform.maxTextureSize
                || b.GetDefaultPlatformTextureSettings().textureCompression != platform.textureCompression)
            {
                b.SetTextureSettings(want); b.SetPlatformTextureSettings(platform); b.SaveAndReimport();
                wrote = true;
            }
            sb.AppendLine($"  {to}: {(wrote ? "written" : "unchanged")}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(to);
        }

        static byte Shade(byte value, float root) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * root), 0, 255);
    }
}
