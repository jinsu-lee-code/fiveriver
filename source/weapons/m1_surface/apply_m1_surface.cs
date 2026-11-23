// Points the first-person M1's three materials at the maps of compose_maps.py (run with: unity command script eval).
// The normal maps and the parkerized detail maps stay as they were.
string dir = "Assets/_Project/Art/Weapons/M1Surface20261123/";
UnityEditor.AssetDatabase.Refresh();
string o = "";
foreach (var name in new[] { "Metal", "Hardware", "Wood" })
{
    foreach (var kind in new[] { "BaseColor", "Mask" })
    {
        var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir + "T_WP_M1_" + name + "_" + kind + ".png");
        ti.sRGBTexture = kind == "BaseColor"; ti.maxTextureSize = kind == "BaseColor" && name != "Hardware" ? 4096 : 2048;
        ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.alphaSource = kind == "Mask" ? UnityEditor.TextureImporterAlphaSource.FromInput : UnityEditor.TextureImporterAlphaSource.None;
        ti.textureCompression = UnityEditor.TextureImporterCompression.CompressedHQ;
        foreach (var pf in new[] { "Android", "iPhone" }) { var s = ti.GetPlatformTextureSettings(pf); s.overridden = true; s.maxTextureSize = 1024; ti.SetPlatformTextureSettings(s); }
        ti.SaveAndReimport();
    }
    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Weapons/M1Handling20261102/M1_View_Photo_" + name + ".mat");
    var b = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_WP_M1_" + name + "_BaseColor.png");
    var k = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_WP_M1_" + name + "_Mask.png");
    m.SetTexture("_BaseMap", b); m.SetTexture("_MainTex", b); m.SetTexture("_MetallicGlossMap", k); m.SetTexture("_OcclusionMap", k);
    m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP");
    m.SetColor("_BaseColor", UnityEngine.Color.white); m.SetColor("_Color", UnityEngine.Color.white);
    m.SetFloat("_Smoothness", 1f); m.SetFloat("_OcclusionStrength", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
    if (name != "Wood") { m.SetFloat("_DetailAlbedoMapScale", 0.2f); m.SetFloat("_DetailNormalMapScale", 0.25f); }
    UnityEditor.EditorUtility.SetDirty(m);
    o += name + " ok; ";
}
UnityEditor.AssetDatabase.SaveAssets();
return o;
