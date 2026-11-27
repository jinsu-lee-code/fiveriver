// Materials and prefabs for the poplars (run in the editor: unity command script eval --code "<this file>").
var dir = "Assets/_Project/Art/Maps/Dabudong1950/Poplar/";
UnityEditor.AssetDatabase.Refresh();
foreach (var n in new[] { "T_Poplar_Branches", "T_Poplar_Far", "T_Poplar_Bark" })
{
    var ti = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir + n + ".png");
    if (ti == null) return "missing " + n;
    bool leaves = n != "T_Poplar_Bark";
    ti.alphaIsTransparency = leaves; ti.mipMapsPreserveCoverage = leaves; ti.alphaTestReferenceValue = 0.4f;
    ti.wrapMode = leaves ? UnityEngine.TextureWrapMode.Clamp : UnityEngine.TextureWrapMode.Repeat;
    ti.maxTextureSize = 1024; ti.SaveAndReimport();
}
var lit = UnityEngine.Shader.Find("Universal Render Pipeline/Lit");
UnityEngine.Material Mat(string name, string tex, bool cutout)
{
    var path = dir + name + ".mat";
    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
    if (m == null) { m = new UnityEngine.Material(lit); UnityEditor.AssetDatabase.CreateAsset(m, path); }
    var t = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + tex + ".png");
    m.SetTexture("_BaseMap", t); m.SetTexture("_MainTex", t);
    m.SetColor("_BaseColor", UnityEngine.Color.white);
    m.SetFloat("_Smoothness", cutout ? 0.12f : 0.08f); m.SetFloat("_Metallic", 0f);
    if (cutout)
    {
        // leaves: cut out, seen from both sides
        m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.38f); m.SetFloat("_Cull", 0f);
        m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        m.doubleSidedGI = true;
    }
    UnityEditor.EditorUtility.SetDirty(m);
    return m;
}
var bark = Mat("M_Poplar_Bark", "T_Poplar_Bark", false);
var leaf = Mat("M_Poplar_Leaves", "T_Poplar_Branches", true);
var far = Mat("M_Poplar_Far", "T_Poplar_Far", true);
UnityEditor.AssetDatabase.SaveAssets();
var log = new System.Text.StringBuilder();
foreach (var v in new[] { "A", "B" })
{
    var fbx = dir + "SM_Poplar_" + v + ".fbx";
    var mi = (UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(fbx);
    if (mi == null) return "missing model " + v;
    mi.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "M_Poplar_Bark"), bark);
    mi.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "M_Poplar_Leaves"), leaf);
    mi.AddRemap(new UnityEditor.AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Material), "M_Poplar_Far"), far);
    mi.importNormals = UnityEditor.ModelImporterNormals.Import;      // the foliage normals are authored
    mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
    mi.SaveAndReimport();
    var model = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(fbx);
    var root = new UnityEngine.GameObject("PF_Poplar_" + v);
    var inst = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(model, root.transform);
    // (2026-11-27) The meshes are named *_LOD0..2, so Unity's model import puts its own LODGroup on the model (0.25 / 0.125 /
    // 0.01). With the root's LODGroup below, each renderer was in two LODGroups ("registered with more than one LODGroup"
    // warning for every tree, play and edit). The model importer has no setting to turn that off: the model's own group
    // is removed here and the root's (these distances, LOD2 without shadows, the height) is the one that counts.
    foreach (var own in inst.GetComponentsInChildren<UnityEngine.LODGroup>(true)) UnityEngine.Object.DestroyImmediate(own);
    var lods = new UnityEngine.LOD[3];
    float[] until = { 0.30f, 0.10f, 0.012f };
    float height = 0f;
    for (int i = 0; i < 3; i++)
    {
        UnityEngine.Renderer r = null;
        foreach (var x in inst.GetComponentsInChildren<UnityEngine.MeshRenderer>()) if (x.name.EndsWith("_LOD" + i)) r = x;
        if (r == null) return "no LOD" + i + " in " + v;
        r.shadowCastingMode = i == 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        lods[i] = new UnityEngine.LOD(until[i], new[] { r });
        if (i == 0) height = r.bounds.size.y;
        log.Append(v + " LOD" + i + " " + r.bounds.size.ToString("F1") + " mats=" + r.sharedMaterials.Length + "; ");
    }
    var group = root.AddComponent<UnityEngine.LODGroup>();
    group.SetLODs(lods); group.RecalculateBounds();
    var c = root.AddComponent<UnityEngine.CapsuleCollider>();
    c.direction = 1; c.radius = 0.24f; c.height = 4.5f; c.center = new UnityEngine.Vector3(0f, 2.25f, 0f);
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, dir + "PF_Poplar_" + v + ".prefab");
    UnityEngine.Object.DestroyImmediate(root);
}
return log.ToString();
