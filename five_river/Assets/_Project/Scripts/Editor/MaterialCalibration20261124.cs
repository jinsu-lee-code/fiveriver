using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // PBR calibration (2026-11-24, engine-features graphics pass). Material values only; no texture is touched.
    // Chosen from the survey (GraphicsSurvey20261124 -> Temp/gfx_materials.tsv): what reads as plastic (cloth and
    // parkerized steel with a glossy finish), painted iron set up as bare metal, a too-bright canvas.
    // A smoothness or metallic entry is an upper limit (only lowered, never raised: several assets share a name), and
    // an albedo entry a target the tint converges to, so running the menu again gives the same result. Old -> new is appended to Docs/Review/material_calibration_20261124.tsv.
    // Not touched on purpose: the player's M1 (M1_View_Photo_*, reworked 11-23) and the first-person arms and hands
    // (work in progress elsewhere), the terrain layers and the earth / sandbag materials (all inside the ranges).
    public static class MaterialCalibration20261124
    {
        const string LogPath = "../Docs/Review/material_calibration_20261124.tsv";
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        enum Kind { Float, AlbedoLuma }
        struct Entry
        {
            public string path, property, why; public float value; public Kind kind;
            public Entry(string path, string property, float value, string why, Kind kind = Kind.Float) { this.path = path; this.property = property; this.value = value; this.why = why; this.kind = kind; }
        }

        static readonly Entry[] Entries =
        {
            // allies' and the student reinforcements' M1 (photo-scanned set): parkerized steel and a canvas sling
            // were as glossy as polished metal and plastic
            new Entry("Photo_Metal", "_Smoothness", .62f, "파커라이징 강철: 맵 A .68 x 1 -> x .62 = .42 (무광 인산염 피막)"),
            new Entry("Photo_Hardware", "_Smoothness", .70f, "철물: .55 -> .39"),
            new Entry("Photo_Canvas", "_Smoothness", .40f, "면 멜빵: .60 -> .24 (천이 번들거림)"),
            new Entry("Photo_Wood", "_Smoothness", .75f, "기름 먹인 호두나무: .54 -> .40"),
            // ally and student clothing and skin with the importer's default 0.5
            // (the student reinforcement's cap and face, smoothness .5, are materials embedded in SK_CH_ROK_Soldier.fbx:
            //  not editable without extracting them, left as they are)
            new Entry("SK_CH_ROK_ParkYongpal_URPLab_Baked_Helmat", "_Smoothness", .32f, "닳은 도장 철모: .50 -> .32"),
            new Entry("M_CH_ROK_Body", "_Smoothness", .55f, "군복 천: 맵 A .45 x 1 -> x .55 = .25"),
            // canvas web gear a little brighter than khaki canvas reads under a flare
            new Entry("URPLab_Baked_Pauch", "_BaseColor", 132f, "탄입대(카키 면포) 알베도 147 -> 132", Kind.AlbedoLuma),
            new Entry("URPLab_Baked_Pauch.001", "_BaseColor", 132f, "탄입대(카키 면포) 알베도 147 -> 132", Kind.AlbedoLuma),
            // M1917 ammunition crate fittings: painted iron, not bare metal
            new Entry("M_AC_Iron", "_Metallic", .20f, "도장된 철 장식: 금속성 .80 -> .20 (도장면, 모서리만 닳음)"),
            new Entry("Granite_Boulder", "_Smoothness", .80f, "마른 화강암: 맵 A .37 x 1 -> x .8 = .30"),
        };

        [MenuItem("FiveRivers/Setup/Material Calibration 20261124")]
        public static string Apply()
        {
            var byName = new Dictionary<string, List<Material>>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project", "Assets/URPLab" }.Where(AssetDatabase.IsValidFolder).ToArray()))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                if (!byName.TryGetValue(m.name, out var list)) byName[m.name] = list = new List<Material>();
                list.Add(m);
            }
            // only materials the scene and its spawned prefabs actually use (several assets can share a name)
            var used = new HashSet<Material>(GraphicsSurvey20261124.Materials().Select(i => i.material));
            var sb = new StringBuilder();
            if (!File.Exists(LogPath)) sb.AppendLine("time\tmaterial\tpath\tproperty\told\tnew\twhy");
            int changed = 0;
            foreach (var e in Entries)
            {
                if (!byName.TryGetValue(e.path, out var mats)) { sb.AppendLine($"{System.DateTime.Now:HH:mm}\t{e.path}\t-\t{e.property}\tMISSING\t-\t{e.why}"); continue; }
                foreach (var m in mats.Where(used.Contains))
                {
                    if (!m.HasProperty(e.property)) continue;
                    string oldText, newText;
                    if (e.kind == Kind.Float)
                    {
                        float old = m.GetFloat(e.property);
                        oldText = old.ToString("0.###", C); newText = e.value.ToString("0.###", C);
                        if (old <= e.value + 1e-4f) continue; // an upper limit: never raise
                        m.SetFloat(e.property, e.value);
                    }
                    else
                    {
                        var info = GraphicsSurvey20261124.Describe(m);
                        var tint = m.GetColor(e.property);
                        // luma scales linearly with the tint in linear space
                        float nowLin = Mathf.GammaToLinearSpace(info.albedoSrgb / 255f), wantLin = Mathf.GammaToLinearSpace(e.value / 255f);
                        float k = wantLin / Mathf.Max(1e-4f, nowLin);
                        if (Mathf.Abs(info.albedoSrgb - e.value) < 1.5f) continue;
                        var lin = tint.linear;
                        var next = new Color(lin.r * k, lin.g * k, lin.b * k, 1f).gamma; next.a = tint.a;
                        oldText = $"{info.albedoSrgb:0} #{ColorUtility.ToHtmlStringRGB(tint)}"; newText = $"{e.value:0} #{ColorUtility.ToHtmlStringRGB(next)}";
                        m.SetColor(e.property, next);
                    }
                    EditorUtility.SetDirty(m);
                    changed++;
                    sb.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}\t{m.name}\t{AssetDatabase.GetAssetPath(m)}\t{e.property}\t{oldText}\t{newText}\t{e.why}");
                }
            }
            changed += StudentMaterials(sb) + ClothWithoutMetal(sb);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, sb.ToString());
            return "calibrated " + changed;
        }

        // 2026-11-24 (2nd pass): the student reinforcement's cap and face were materials embedded in SK_CH_ROK_Soldier.fbx
        // with the importer's smoothness .5 (a shiny cloth cap, a waxy face). They are extracted to material assets and
        // remapped in the model's importer (as its hands and vest already were), then set in range.
        const string StudentFbx = "Assets/_Project/Art/Models/Characters/ROK_Soldier/SK_CH_ROK_Soldier.fbx";
        const string StudentMatDir = "Assets/_Project/Art/Models/Characters/ROK_Soldier/Materials20261124/";

        static int StudentMaterials(StringBuilder sb)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(StudentFbx);
            if (importer == null) return 0;
            int n = 0;
            var map = importer.GetExternalObjectMap();
            foreach (var (name, smooth, why) in new[] { ("URPLab_Baked_Cap", .18f, "학도병 모자(천): .50 -> .18"), ("URPLab_Baked_Head", .38f, "학도병 얼굴 피부: .50 -> .38") })
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                Material mat;
                if (map.TryGetValue(id, out var existing) && existing is Material m0) mat = m0;
                else
                {
                    var embedded = AssetDatabase.LoadAllAssetsAtPath(StudentFbx).OfType<Material>().FirstOrDefault(x => x.name == name);
                    if (embedded == null) { sb.AppendLine($"{System.DateTime.Now:HH:mm}\t{name}\t{StudentFbx}\t-\tMISSING\t-\t{why}"); continue; }
                    Directory.CreateDirectory(StudentMatDir);
                    var path = StudentMatDir + "M_Student_" + name.Substring("URPLab_Baked_".Length) + "_20261124.mat";
                    mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null) { mat = new Material(embedded) { name = Path.GetFileNameWithoutExtension(path) }; AssetDatabase.CreateAsset(mat, path); }
                    importer.AddRemap(id, mat);
                    importer.SaveAndReimport();
                    sb.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}\t{name}\t{StudentFbx}\tremap\tembedded\t{path}\t모델에 묻힌 재질을 꺼내 리맵");
                }
                float old = mat.GetFloat("_Smoothness");
                if (old > smooth + 1e-4f)
                {
                    mat.SetFloat("_Smoothness", smooth); EditorUtility.SetDirty(mat); n++;
                    sb.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}\t{mat.name}\t{AssetDatabase.GetAssetPath(mat)}\t_Smoothness\t{old.ToString("0.###", C)}\t{smooth.ToString("0.###", C)}\t{why}");
                }
            }
            return n;
        }

        // The blanket and the boots share a metallic-smoothness map whose red (metal) channel averages .15 - cloth and
        // leather with a metallic sheen. A copy with the red channel at 0 (smoothness alpha kept) replaces it on those two
        // materials; the original texture is not touched.
        const string ClothMapSource = "Assets/_Project/Art/Models/Props/BlanketBody/Textures/T_PR_BlanketBody_MetallicSmoothness.png";
        const string ClothMapNoMetal = "Assets/_Project/Art/Models/Props/BlanketBody/Textures/T_PR_BlanketBody_MetallicSmoothness_NoMetal20261124.png";
        static readonly string[] ClothMaterials = { "Assets/_Project/Art/Materials/M_PR_Blanket.mat", "Assets/_Project/Art/Materials/M_PR_Boot.mat" };

        static int ClothWithoutMetal(StringBuilder sb)
        {
            if (!File.Exists(ClothMapSource)) return 0;
            if (!File.Exists(ClothMapNoMetal))
            {
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                src.LoadImage(File.ReadAllBytes(ClothMapSource));
                var px = src.GetPixels32();
                for (int i = 0; i < px.Length; i++) px[i].r = 0;
                src.SetPixels32(px); src.Apply();
                File.WriteAllBytes(ClothMapNoMetal, src.EncodeToPNG());
                Object.DestroyImmediate(src);
                AssetDatabase.ImportAsset(ClothMapNoMetal);
                var srcImp = (TextureImporter)AssetImporter.GetAtPath(ClothMapSource);
                var imp = (TextureImporter)AssetImporter.GetAtPath(ClothMapNoMetal);
                imp.sRGBTexture = false; imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.mipmapEnabled = true;
                imp.maxTextureSize = srcImp != null ? srcImp.maxTextureSize : 2048;
                imp.textureCompression = srcImp != null ? srcImp.textureCompression : TextureImporterCompression.Compressed;
                imp.SaveAndReimport();
                sb.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}\tT_PR_BlanketBody_MetallicSmoothness\t{ClothMapNoMetal}\tnew texture\t-\tR = 0, alpha kept\t천 · 가죽에 금속성 .15 → 0 (원본 텍스처는 그대로)");
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ClothMapNoMetal);
            int n = 0;
            foreach (var path in ClothMaterials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.GetTexture("_MetallicGlossMap") == tex) continue;
                var old = m.GetTexture("_MetallicGlossMap");
                m.SetTexture("_MetallicGlossMap", tex); EditorUtility.SetDirty(m); n++;
                sb.AppendLine($"{System.DateTime.Now:yyyy-MM-dd HH:mm}\t{m.name}\t{path}\t_MetallicGlossMap\t{(old != null ? old.name : "-")}\t{tex.name}\t천 · 가죽: 금속성 .15 → 0");
            }
            return n;
        }

        // A/B for the review tool: the logged old values back (false) or the calibrated ones (true), not saved
        public static void Preview(bool calibrated)
        {
            if (!File.Exists(LogPath)) return;
            foreach (var line in File.ReadAllLines(LogPath).Skip(1))
            {
                var p = line.Split('\t');
                if (p.Length < 6 || p[4] == "MISSING") continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p[2]);
                if (m == null) continue;
                string v = calibrated ? p[5] : p[4];
                if (p[3] == "_BaseColor") { if (ColorUtility.TryParseHtmlString(v.Substring(v.IndexOf('#')), out var c)) m.SetColor("_BaseColor", c); }
                else if (p[3].StartsWith("_") && float.TryParse(v, NumberStyles.Float, C, out var f) && m.HasProperty(p[3])) m.SetFloat(p[3], f);
            }
        }
    }
}
