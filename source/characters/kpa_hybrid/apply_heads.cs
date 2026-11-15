// Builds the three KPA heads from hybrid.bin.head<N>.bin and hangs them on both enemy prefabs
// (run in the editor after apply_hybrid.cs: unity command script eval --code "<this file>").
// The head is a renderer of its own on the body's bones; EnemyHeadVariant gives each man one of the three.
var dir = "Assets/_Project/Art/Models/Characters/KPA_Hybrid/";
var src = "C:/sk_assets/varco/kpa/hybrid/hybrid.bin";
var oldMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Models/Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx").GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMesh;
var meshes = new UnityEngine.Mesh[3]; var mats = new UnityEngine.Material[3];
for (int h = 0; h < 3; h++)
{
    foreach (var t in new[] { "BaseColor", "Normal" })
        System.IO.File.Copy(src + ".head" + h + "_" + t + ".png", System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/Art/Models/Characters/KPA_Hybrid/T_CH_KPA_Head" + h + "_" + t + ".png"), true);
}
UnityEditor.AssetDatabase.Refresh();
for (int h = 0; h < 3; h++)
{
    var path = dir + "SK_CH_KPA_Head" + h + ".asset";
    var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
    if (mesh == null) { mesh = new UnityEngine.Mesh { name = "SK_CH_KPA_Head" + h }; UnityEditor.AssetDatabase.CreateAsset(mesh, path); }
    mesh.Clear();
    using (var r = new System.IO.BinaryReader(System.IO.File.OpenRead(src + ".head" + h + ".bin")))
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
    mesh.bindposes = oldMesh.bindposes; mesh.RecalculateTangents(); mesh.RecalculateBounds();
    UnityEditor.EditorUtility.SetDirty(mesh); meshes[h] = mesh;
    var ni2 = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir + "T_CH_KPA_Head" + h + "_Normal.png");
    if (ni2.textureType != UnityEditor.TextureImporterType.NormalMap) { ni2.textureType = UnityEditor.TextureImporterType.NormalMap; ni2.SaveAndReimport(); }
    var mp = dir + "M_CH_KPA_Head" + h + ".mat";
    var m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(mp);
    if (m == null) { m = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")); UnityEditor.AssetDatabase.CreateAsset(m, mp); }
    m.SetTexture("_BaseMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Head" + h + "_BaseColor.png"));
    m.SetTexture("_BumpMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Head" + h + "_Normal.png")); m.EnableKeyword("_NORMALMAP");
    m.SetFloat("_Smoothness", 0.3f); m.SetFloat("_Metallic", 0f); m.SetColor("_BaseColor", new UnityEngine.Color(0.86f, 0.84f, 0.80f));
    UnityEditor.EditorUtility.SetDirty(m); mats[h] = m;
}
UnityEditor.AssetDatabase.SaveAssets();
string log = "";
foreach (var p in new[] { "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman.prefab", "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman_B.prefab" })
{
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(p);
    UnityEngine.SkinnedMeshRenderer body = null, head = null;
    foreach (var s in root.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true)) { if (s.name == "Head_KPA") head = s; else body = s; }
    if (head == null)
    {
        var go = new UnityEngine.GameObject("Head_KPA");
        go.transform.SetParent(body.transform.parent, false);
        go.transform.localPosition = body.transform.localPosition; go.transform.localRotation = body.transform.localRotation; go.transform.localScale = body.transform.localScale;
        go.layer = body.gameObject.layer;
        head = go.AddComponent<UnityEngine.SkinnedMeshRenderer>();
    }
    head.bones = body.bones; head.rootBone = body.rootBone; head.sharedMesh = meshes[0]; head.sharedMaterial = mats[0];
    head.shadowCastingMode = body.shadowCastingMode; head.receiveShadows = body.receiveShadows; head.updateWhenOffscreen = body.updateWhenOffscreen;
    head.localBounds = body.localBounds;
    var model = body.transform.parent;
    var variant = model.GetComponent<FiveRivers.Combat.EnemyHeadVariant>() ?? model.gameObject.AddComponent<FiveRivers.Combat.EnemyHeadVariant>();
    variant.Configure(head, meshes, mats);
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, p);
    UnityEditor.PrefabUtility.UnloadPrefabContents(root);
    log += System.IO.Path.GetFileNameWithoutExtension(p) + " ok; ";
}
return log + "heads " + meshes[0].vertexCount + "/" + meshes[1].vertexCount + "/" + meshes[2].vertexCount;
