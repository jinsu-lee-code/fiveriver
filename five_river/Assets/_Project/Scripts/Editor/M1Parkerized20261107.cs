using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Look matched to well-regarded WWII shooters' Garands (e.g. Hell Let Loose): dark steel with a real metallic
    // sheen and bright worn edges (the atlas already carries lighter edges - more metal and gloss make them read as
    // rubbed steel), knurled sight drums catching the light; grain and mottle kept very faint.
    // Takes the plastic look off the first-person M1's steel. A service Garand is Parkerized (manganese phosphate):
    // dark grey-green, matte, with a fine crystalline grain and soft blotchy tone - not the even mid-grey with broad
    // soft highlights it had. Darker base tint, lower metal and gloss, plus a fine tiling grain normal (barely visible, as on a real
    // rifle up close) and a faint mottle (x2 detail albedo at half strength). The rear-sight aperture plate (flat colour before) gets the same finish.
    public static class M1Parkerized20261107
    {
        const string Folder = "Assets/_Project/Art/Weapons/M1Handling20261102/";
        const string Detail = Folder + "Parkerized20261107/";

        [MenuItem("FiveRivers/Art/M1 Parkerized Steel 20261107")]
        public static void Apply()
        {
            var albedo = Texture(Detail + "T_Parkerized_DetailAlbedo.png", false);
            var normal = Texture(Detail + "T_Parkerized_DetailNormal.png", true);
            // receiver, barrel, trigger group: atlas-textured
            Finish(Folder + "M1_View_Photo_Metal.mat", new Color(.8f, .82f, .82f), .85f, .82f, albedo, normal, 20f, .1f);
            // small hardware (pins, screws, swivels)
            Finish(Folder + "M1_View_Photo_Hardware.mat", new Color(.82f, .84f, .84f), .85f, .85f, albedo, normal, 20f, .1f);
            // rear aperture plate: flat colour, so the tint is the whole colour
            Finish(Folder + "M1_SightSteel.mat", new Color(.22f, .23f, .23f), .8f, .5f, albedo, normal, 6f, .15f);
            AssetDatabase.SaveAssets();
            Debug.Log("[M1 steel] Parkerized finish applied.");
        }

        static void Finish(string path, Color tint, float metallic, float smoothness, Texture2D albedo, Texture2D normal, float tiling, float grain)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { Debug.LogWarning("[M1 steel] Missing " + path); return; }
            Undo.RecordObject(m, "Parkerized steel");
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness); // multiplies the map's gloss where there is one
            m.SetTexture("_DetailAlbedoMap", albedo);
            m.SetFloat("_DetailAlbedoMapScale", .35f); // keep the mottle subtle
            m.SetTexture("_DetailNormalMap", normal);
            m.SetFloat("_DetailNormalMapScale", grain);
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one * tiling);
            m.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(m);
        }

        static Texture2D Texture(string path, bool isNormal)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null)
            {
                bool dirty = false;
                if (isNormal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; dirty = true; }
                if (!isNormal && imp.sRGBTexture) { imp.sRGBTexture = false; dirty = true; } // detail albedo is a linear x2 multiplier
                if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
