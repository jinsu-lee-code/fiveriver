// Puts the new hands (hands.bin from build_hands.py) on both enemy prefabs as a renderer of their own on the
// body's bones, and empties the old hands' submeshes of the body mesh.
// Run in the editor after apply_hybrid.cs and apply_heads.cs (apply_hybrid.cs puts the old hands back).
var dir = "Assets/_Project/Art/Models/Characters/KPA_Hybrid/";
var HandTint = new UnityEngine.Color(0.68f, 0.60f, 0.55f);   // toward the heads' skin (the generated hand is pinker)
var src = "C:/sk_assets/varco/kpa/hybrid/hands.bin";
foreach (var t in new[] { "BaseColor", "Normal" })
    System.IO.File.Copy(src + "_" + t + ".png", System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/Art/Models/Characters/KPA_Hybrid/T_CH_KPA_Hands_" + t + ".png"), true);
UnityEditor.AssetDatabase.Refresh();
var oldMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Models/Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx").GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMesh;
var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(dir + "SK_CH_KPA_Hands.asset");
if (mesh == null) { mesh = new UnityEngine.Mesh { name = "SK_CH_KPA_Hands" }; UnityEditor.AssetDatabase.CreateAsset(mesh, dir + "SK_CH_KPA_Hands.asset"); }
mesh.Clear();
using (var r = new System.IO.BinaryReader(System.IO.File.OpenRead(src)))
{
    int nv = r.ReadInt32(); r.ReadInt32();
    var v = new UnityEngine.Vector3[nv]; var n = new UnityEngine.Vector3[nv]; var uv = new UnityEngine.Vector2[nv]; var bw = new UnityEngine.BoneWeight[nv];
    for (int i = 0; i < nv; i++)
    {
        v[i] = new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        n[i] = new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()).normalized;
        uv[i] = new UnityEngine.Vector2(r.ReadSingle(), r.ReadSingle());
        int b0 = r.ReadInt32(), b1 = r.ReadInt32(), b2 = r.ReadInt32(), b3 = r.ReadInt32();
        bw[i] = new UnityEngine.BoneWeight { boneIndex0 = b0, boneIndex1 = b1, boneIndex2 = b2, boneIndex3 = b3, weight0 = r.ReadSingle(), weight1 = r.ReadSingle(), weight2 = r.ReadSingle(), weight3 = r.ReadSingle() };
    }
    int ni = r.ReadInt32(); var idx = new int[ni];
    for (int i = 0; i < ni; i++) idx[i] = r.ReadInt32();
    mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.boneWeights = bw; mesh.triangles = idx;
}
mesh.bindposes = oldMesh.bindposes; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
UnityEditor.EditorUtility.SetDirty(mesh);
var nimp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir + "T_CH_KPA_Hands_Normal.png");
if (nimp.textureType != UnityEditor.TextureImporterType.NormalMap) { nimp.textureType = UnityEditor.TextureImporterType.NormalMap; nimp.SaveAndReimport(); }
var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "M_CH_KPA_Hands.mat");
if (m == null) { m = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")); UnityEditor.AssetDatabase.CreateAsset(m, dir + "M_CH_KPA_Hands.mat"); }
m.SetTexture("_BaseMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Hands_BaseColor.png"));
m.SetTexture("_BumpMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Hands_Normal.png")); m.EnableKeyword("_NORMALMAP");
m.SetFloat("_Smoothness", 0.3f); m.SetFloat("_Metallic", 0f); m.SetColor("_BaseColor", HandTint);
UnityEditor.EditorUtility.SetDirty(m);
var body = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(dir + "SK_CH_KPA_Hybrid.asset");
for (int s = 1; s < body.subMeshCount; s++) body.SetTriangles(new int[0], s);
UnityEditor.EditorUtility.SetDirty(body);
UnityEditor.AssetDatabase.SaveAssets();
string log = "";
foreach (var p in new[] { "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman.prefab", "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman_B.prefab" })
{
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(p);
    UnityEngine.SkinnedMeshRenderer bodyR = null, hands = null;
    foreach (var s in root.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) { if (s.name == "Hands_KPA") hands = s; else if (s.name != "Head_KPA") bodyR = s; }
    if (hands == null)
    {
        var go = new UnityEngine.GameObject("Hands_KPA");
        go.transform.SetParent(bodyR.transform.parent, false);
        go.transform.localPosition = bodyR.transform.localPosition; go.transform.localRotation = bodyR.transform.localRotation; go.transform.localScale = bodyR.transform.localScale;
        go.layer = bodyR.gameObject.layer; hands = go.AddComponent<UnityEngine.SkinnedMeshRenderer>();
    }
    hands.bones = bodyR.bones; hands.rootBone = bodyR.rootBone; hands.sharedMesh = mesh; hands.sharedMaterial = m;
    hands.shadowCastingMode = bodyR.shadowCastingMode; hands.updateWhenOffscreen = bodyR.updateWhenOffscreen; hands.localBounds = bodyR.localBounds;
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, p); UnityEditor.PrefabUtility.UnloadPrefabContents(root);
    log += System.IO.Path.GetFileNameWithoutExtension(p) + " ok; ";
}
return log + "hands verts " + mesh.vertexCount;
