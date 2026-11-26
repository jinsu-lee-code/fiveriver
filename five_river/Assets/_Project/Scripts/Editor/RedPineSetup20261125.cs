using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-25 (사용자 지시 "나무가 맞지않으면 지금보다 실사에 가깝게 질감이나 느낌이 나게 만들고 지금은 대충 만든느낌이 있어서 그런거 고쳐놔"):
    // the Korean red pines re-made in Blender (source/environment/red_pine_20261125/make_red_pine.py) — bark baked as tiling
    // maps (grey-brown plated lower trunk, orange flaking upper trunk), needle twigs baked to an alpha atlas, cards in flat
    // layered pads. This menu gives the three FBX their materials (the needles use FiveRivers/WildGrass: wind in the crown,
    // light through the needles against the sun) and their LOD switch distances. Re-running gives the same result.
    public static class RedPineSetup20261125
    {
        public const string Folder = "Assets/_Project/Art/Maps/Dabudong_Env/RedPine20261125";
        public static readonly string[] Trees = { "SM_RedPine_A", "SM_RedPine_B", "SM_RedPine_C" };


        // LOD switch heights for a placed pine (the FBX's own LODGroup): near detail, middle, far, then gone
        public static void TuneLods(GameObject tree)
        {
            var g = tree.GetComponentInChildren<LODGroup>();
            if (g == null) return;
            var lods = g.GetLODs();
            float[] h = { .30f, .10f, .012f };
            for (int i = 0; i < lods.Length && i < h.Length; i++) { lods[i].screenRelativeTransitionHeight = h[i]; lods[i].fadeTransitionWidth = .2f; }
            g.SetLODs(lods);
            g.fadeMode = LODFadeMode.CrossFade; g.animateCrossFading = true;
        }
        [MenuItem("FiveRivers/Setup/Red Pines 20261125")]
        public static string Apply()
        {
            void Normal(string tex)
            {
                if (AssetImporter.GetAtPath(tex) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            }
            Material Bark(string name, string stem)
            {
                string path = $"{Folder}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                var b = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/T_RedPine_{stem}_BaseColor.png");
                string np = $"{Folder}/T_RedPine_{stem}_Normal.png"; Normal(np);
                m.SetTexture("_BaseMap", b); m.SetTexture("_MainTex", b);
                m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .12f); m.SetFloat("_Metallic", 0f);
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np)); m.SetFloat("_BumpScale", 1.2f); m.EnableKeyword("_NORMALMAP");
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                return m;
            }
            var low = Bark("M_RedPine_BarkLow", "BarkLow");
            var high = Bark("M_RedPine_BarkHigh", "BarkHigh");
            string needlesPath = $"{Folder}/M_RedPine_Needles.mat";
            var needles = AssetDatabase.LoadAssetAtPath<Material>(needlesPath);
            var shader = Shader.Find("FiveRivers/WildGrass");
            if (needles == null) { needles = new Material(shader); AssetDatabase.CreateAsset(needles, needlesPath); }
            needles.shader = shader;
            string atlas = $"{Folder}/T_RedPine_Needles_BaseColor.png";
            if (AssetImporter.GetAtPath(atlas) is TextureImporter ai && (!ai.alphaIsTransparency || !ai.mipMapsPreserveCoverage))
            { ai.alphaIsTransparency = true; ai.mipMapsPreserveCoverage = true; ai.alphaTestReferenceValue = .4f; ai.wrapMode = TextureWrapMode.Clamp; ai.SaveAndReimport(); }
            needles.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(atlas));
            needles.SetColor("_BaseColor", new Color(1.05f, 1.05f, 1f));
            needles.SetFloat("_Cutoff", .4f); needles.SetFloat("_Height", 9f); needles.SetFloat("_RootDarken", .25f);
            needles.SetFloat("_DryVariation", 0f); needles.SetFloat("_Translucency", .5f);
            needles.SetFloat("_WindStrength", .14f); needles.SetFloat("_WindSpeed", .9f);
            needles.enableInstancing = true;
            EditorUtility.SetDirty(needles);
            AssetDatabase.SaveAssets();

            int done = 0;
            foreach (var t in Trees)
            {
                string fbx = $"{Folder}/{t}.fbx";
                if (!(AssetImporter.GetAtPath(fbx) is ModelImporter mi)) continue;
                var map = new (string slot, Material mat)[] { ("M_RedPine_BarkLow", low), ("M_RedPine_BarkHigh", high), ("M_RedPine_Needles", needles) };
                foreach (var (slot, mat) in map) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), mat);
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.importNormals = ModelImporterNormals.Import;
                mi.SaveAndReimport();
                done++;
            }
            var msg = $"red pines: {done} FBX set up (bark low/high, needles on FiveRivers/WildGrass)";
            Debug.Log("[RedPineSetup20261125] " + msg);
            System.IO.File.WriteAllText("Temp/red_pine_setup.txt", msg);
            return msg;
        }
    }
}
