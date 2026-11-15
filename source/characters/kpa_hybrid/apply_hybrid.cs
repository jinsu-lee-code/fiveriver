// Builds the hybrid KPA mesh from hybrid.bin and puts it on both enemy prefabs
// (run in the editor: unity command script eval --code "<this file>").
// The skeleton, bind poses, avatar and prefab hierarchy stay as they are: only the mesh and its materials change.
var dir = "Assets/_Project/Art/Models/Characters/KPA_Hybrid/";
var bin = "C:/sk_assets/varco/kpa/hybrid/hybrid.bin";
var prefabPath = "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
var smr = root.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
var oldMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Models/Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx").GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>().sharedMesh;
var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(dir + "SK_CH_KPA_Hybrid.asset");
if (mesh == null) { mesh = new UnityEngine.Mesh { name = "SK_CH_KPA_Hybrid" }; UnityEditor.AssetDatabase.CreateAsset(mesh, dir + "SK_CH_KPA_Hybrid.asset"); }
mesh.Clear();
mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
using (var r = new System.IO.BinaryReader(System.IO.File.OpenRead(bin)))
{
    int nv = r.ReadInt32(), ns = r.ReadInt32();
    var v = new UnityEngine.Vector3[nv]; var n = new UnityEngine.Vector3[nv]; var uv = new UnityEngine.Vector2[nv]; var bw = new UnityEngine.BoneWeight[nv];
    for (int i = 0; i < nv; i++)
    {
        v[i] = new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        n[i] = new UnityEngine.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()).normalized;
        uv[i] = new UnityEngine.Vector2(r.ReadSingle(), r.ReadSingle());
        int b0 = r.ReadInt32(), b1 = r.ReadInt32(), b2 = r.ReadInt32(), b3 = r.ReadInt32();
        bw[i] = new UnityEngine.BoneWeight { boneIndex0 = b0, boneIndex1 = b1, boneIndex2 = b2, boneIndex3 = b3, weight0 = r.ReadSingle(), weight1 = r.ReadSingle(), weight2 = r.ReadSingle(), weight3 = r.ReadSingle() };
    }
    mesh.vertices = v; mesh.normals = n; mesh.uv = uv; mesh.boneWeights = bw;
    mesh.subMeshCount = ns;
    for (int s = 0; s < ns; s++)
    {
        int ni = r.ReadInt32(); var idx = new int[ni];
        for (int i = 0; i < ni; i++) idx[i] = r.ReadInt32();
        mesh.SetTriangles(idx, s);
    }
}
mesh.bindposes = oldMesh.bindposes;
mesh.RecalculateTangents(); mesh.RecalculateBounds();
UnityEditor.EditorUtility.SetDirty(mesh);
// materials: the generated uniform, and the game's own hands
var body = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "M_CH_KPA_Hybrid_Body.mat");
if (body == null) { body = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")); UnityEditor.AssetDatabase.CreateAsset(body, dir + "M_CH_KPA_Hybrid_Body.mat"); }
var ni2 = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir + "T_CH_KPA_Hybrid_Normal.png");
if (ni2 != null && ni2.textureType != UnityEditor.TextureImporterType.NormalMap) { ni2.textureType = UnityEditor.TextureImporterType.NormalMap; ni2.SaveAndReimport(); }
body.SetTexture("_BaseMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Hybrid_BaseColor.png"));
body.SetTexture("_BumpMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Hybrid_Normal.png")); body.EnableKeyword("_NORMALMAP");
body.SetFloat("_Smoothness", 0.2f); body.SetFloat("_Metallic", 0f); body.SetColor("_BaseColor", new UnityEngine.Color(0.86f, 0.84f, 0.80f));
UnityEditor.EditorUtility.SetDirty(body);
// the second rifleman wears the same uniform sun-bleached and dusty
var bodyB = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "M_CH_KPA_Hybrid_Body_B.mat");
if (bodyB == null) { bodyB = new UnityEngine.Material(body); UnityEditor.AssetDatabase.CreateAsset(bodyB, dir + "M_CH_KPA_Hybrid_Body_B.mat"); }
bodyB.CopyPropertiesFromMaterial(body); bodyB.SetColor("_BaseColor", new UnityEngine.Color(0.74f, 0.70f, 0.62f));
UnityEditor.EditorUtility.SetDirty(bodyB);
// the hands keep the game's own hand textures, on materials of their own (the ones inside the FBX cannot be edited)
var handL = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Models/Characters/KPA_Rifleman/Materials_Game/LeftHand_Skin_v3.mat");
var handR = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Models/Characters/KPA_Rifleman/Materials_Game/RightHand_Skin_v3.mat");
if (handL == null || handR == null) return "hand materials missing";
smr.sharedMesh = mesh;
smr.sharedMaterials = new[] { body, handL, handR };
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
// the variant keeps its own hands
var pathB = "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman_B.prefab";
var rootB = UnityEditor.PrefabUtility.LoadPrefabContents(pathB);
var smrB = rootB.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
smrB.sharedMaterials = new[] { bodyB, handL, handR };
string b = smrB.sharedMesh != null ? smrB.sharedMesh.name : "null";
UnityEditor.PrefabUtility.SaveAsPrefabAsset(rootB, pathB);
UnityEditor.PrefabUtility.UnloadPrefabContents(rootB);
UnityEditor.AssetDatabase.SaveAssets();
return "mesh verts " + mesh.vertexCount + " sub " + mesh.subMeshCount + " bounds " + mesh.bounds.size.ToString("F4") + " | A mats " + handL.name + "," + handR.name + " | B mesh " + b;
