// Builds the hybrid KPA mesh (body and, since 2026-11-18, the hands in one mesh) from hybrid.bin and puts it on both enemy prefabs
// (run in the editor: unity command script eval --code "<this file>").
// The skeleton, bind poses, avatar and prefab hierarchy stay as they are: only the mesh and its materials change.
var dir = "Assets/_Project/Art/Models/Characters/KPA_Hybrid/";
var bin = "C:/sk_assets/varco/kpa/own/hybrid.bin";   // the build folder (own: the whole-model rig, 2026-11-18)
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
// (2026-11-18, the Rigify videos' way) the bones go onto the model's own joints (build_hybrid.py own-joints writes
// them, in this mesh's space): their bind poses keep their turn and take the new place; the bones in both prefabs
// are moved to match and a Humanoid avatar is built on the moved skeleton below
var fitPath = bin + "_bones.json";   // every bone (own-joints build) or none
var fit = new System.Collections.Generic.Dictionary<string, UnityEngine.Vector3>();
if (System.IO.File.Exists(fitPath))
    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(System.IO.File.ReadAllText(fitPath), @"""([^""]+)"": \[([^\]]+)\]"))
    {
        var c = m.Groups[2].Value.Split(',');
        fit[m.Groups[1].Value] = new UnityEngine.Vector3(float.Parse(c[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(c[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(c[2], System.Globalization.CultureInfo.InvariantCulture));
    }
// the forearm twist bones (elbow-twist video): in the order the build numbered them, after the rig's own bones
var twistNames = new System.Collections.Generic.List<string>();
foreach (var k in fit.Keys) if (k.Contains("ForeArmTwist")) twistNames.Add(k);
// each prefab: the rig's own bones, then the twist bones (made as children of the forearm, unturned, where missing)
void SetBones(UnityEngine.GameObject r)
{
    var s = r.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
    var all = new System.Collections.Generic.Dictionary<string, UnityEngine.Transform>();
    foreach (var t in r.GetComponentsInChildren<UnityEngine.Transform>(true)) all[t.name] = t;
    var list = new System.Collections.Generic.List<UnityEngine.Transform>();
    foreach (var t in s.bones) if (t == null || !t.name.Contains("ForeArmTwist")) list.Add(t);
    foreach (var n in twistNames)
    {
        if (!all.TryGetValue(n, out var t))
        {
            t = new UnityEngine.GameObject(n).transform;
            t.SetParent(all[n.Substring(0, n.IndexOf("Twist"))], false);
        }
        list.Add(t);
    }
    s.bones = list.ToArray();
    var an = r.GetComponentInChildren<UnityEngine.Animator>();
    if (twistNames.Count > 0 && an.GetComponent<FiveRivers.Combat.ForearmTwist>() == null) an.gameObject.AddComponent<FiveRivers.Combat.ForearmTwist>();
}
SetBones(root);
var binds = new System.Collections.Generic.List<UnityEngine.Matrix4x4>(oldMesh.bindposes);
for (int i = 0; i < binds.Count; i++)
{
    if (smr.bones[i] == null || !fit.TryGetValue(smr.bones[i].name, out var at)) continue;
    var inv = binds[i].inverse; inv.SetColumn(3, new UnityEngine.Vector4(at.x, at.y, at.z, 1f)); binds[i] = inv.inverse;
}
// a twist bone's bind: its forearm's turn, at its own place on the forearm
foreach (var n in twistNames)
{
    int fa = System.Array.FindIndex(smr.bones, t => t != null && t.name == n.Substring(0, n.IndexOf("Twist")));
    var inv = binds[fa].inverse; var at = fit[n]; inv.SetColumn(3, new UnityEngine.Vector4(at.x, at.y, at.z, 1f)); binds.Add(inv.inverse);
}
mesh.bindposes = binds.ToArray();
// a prefab's bones onto the same places (parents before children, so each lands where it is meant to)
string FitBones(UnityEngine.GameObject r)
{
    var s = r.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
    var all = new System.Collections.Generic.Dictionary<string, UnityEngine.Transform>();
    foreach (var t in r.GetComponentsInChildren<UnityEngine.Transform>(true)) all[t.name] = t;
    int Depth(UnityEngine.Transform t) { int d = 0; for (; t != null; t = t.parent) d++; return d; }
    var names = new System.Collections.Generic.List<string>(); foreach (var k in fit.Keys) if (all.ContainsKey(k)) names.Add(k);
    names.Sort((a, b) => Depth(all[a]).CompareTo(Depth(all[b])));   // parents before children
    float before = 0f; int moved = 0;
    foreach (var n in names)
    {
        if (!all.TryGetValue(n, out var t)) continue;
        var to = s.transform.TransformPoint(fit[n]);
        before = UnityEngine.Mathf.Max(before, UnityEngine.Vector3.Distance(t.position, to)); t.position = to; moved++;
    }
    return moved + " bones, furthest moved " + before.ToString("F3");
}
// the avatar: the model's own Humanoid description (bone mapping, muscle limits) with the moved skeleton. The bones
// keep their turn, so the clips mean what they meant; only the lengths and places are the model's.
UnityEngine.Avatar fitAvatar = null;
string FitAvatar(UnityEngine.GameObject r)
{
    var an = r.GetComponentInChildren<UnityEngine.Animator>();
    if (fitAvatar == null)
    {
        UnityEngine.Avatar srcAv = null;
        foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Models/Characters/KPA_Rifleman/SK_CH_KPA_Rifleman.fbx"))
            if (o is UnityEngine.Avatar a) srcAv = a;
        var hd = srcAv.humanDescription; var sk = hd.skeleton;
        var all = new System.Collections.Generic.Dictionary<string, UnityEngine.Transform>();
        foreach (var t in an.GetComponentsInChildren<UnityEngine.Transform>(true)) all[t.name] = t;
        for (int i = 0; i < sk.Length; i++)
            if (all.TryGetValue(sk[i].name, out var t) && t != an.transform) { sk[i].position = t.localPosition; sk[i].rotation = t.localRotation; sk[i].scale = t.localScale; }
        // bones the model's description does not know (the twist bones) are added with their rest place
        var known = new System.Collections.Generic.HashSet<string>(System.Array.ConvertAll(sk, x => x.name));
        var skl = new System.Collections.Generic.List<UnityEngine.SkeletonBone>(sk);
        foreach (var t in an.GetComponentsInChildren<UnityEngine.Transform>(true))
            if (!known.Contains(t.name)) skl.Add(new UnityEngine.SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
        hd.skeleton = skl.ToArray();
        var built = UnityEngine.AvatarBuilder.BuildHumanAvatar(an.gameObject, hd);
        built.name = "SK_CH_KPA_OwnAvatar";
        // kept as the same asset when rebuilt: deleted and made again, the prefabs' link to it was lost (Avatar is null)
        fitAvatar = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Avatar>(dir + "SK_CH_KPA_OwnAvatar.asset");
        if (fitAvatar == null) { fitAvatar = built; UnityEditor.AssetDatabase.CreateAsset(fitAvatar, dir + "SK_CH_KPA_OwnAvatar.asset"); }
        else { UnityEditor.EditorUtility.CopySerialized(built, fitAvatar); fitAvatar.name = "SK_CH_KPA_OwnAvatar"; UnityEditor.EditorUtility.SetDirty(fitAvatar); UnityEngine.Object.DestroyImmediate(built); }
        UnityEditor.AssetDatabase.SaveAssets();
    }
    an.avatar = fitAvatar;
    return "avatar valid " + fitAvatar.isValid + " human " + fitAvatar.isHuman;
}
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
// the hands: with build_hands.py's hands in the mesh (2 submeshes) one material of their own with that hand's
// textures, tinted toward the heads' skin; with the old hands (3 submeshes) the game's own hand materials
UnityEngine.Material[] hands;
if (mesh.subMeshCount == 2)
{
    var src = System.IO.Path.GetDirectoryName(bin) + "/hands.bin";
    foreach (var t in new[] { "BaseColor", "Normal" })
        System.IO.File.Copy(src + "_" + t + ".png", System.IO.Path.Combine(UnityEngine.Application.dataPath, "_Project/Art/Models/Characters/KPA_Hybrid/T_CH_KPA_Hands_" + t + ".png"), true);
    UnityEditor.AssetDatabase.Refresh();
    var nimp = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(dir + "T_CH_KPA_Hands_Normal.png");
    if (nimp.textureType != UnityEditor.TextureImporterType.NormalMap) { nimp.textureType = UnityEditor.TextureImporterType.NormalMap; nimp.SaveAndReimport(); }
    var hm = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "M_CH_KPA_Hands.mat");
    if (hm == null) { hm = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")); UnityEditor.AssetDatabase.CreateAsset(hm, dir + "M_CH_KPA_Hands.mat"); }
    hm.SetTexture("_BaseMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Hands_BaseColor.png"));
    hm.SetTexture("_BumpMap", UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_CH_KPA_Hands_Normal.png")); hm.EnableKeyword("_NORMALMAP");
    hm.SetFloat("_BumpScale", 0.45f); hm.SetFloat("_Smoothness", 0.3f); hm.SetFloat("_Metallic", 0f); hm.SetColor("_BaseColor", new UnityEngine.Color(0.86f, 0.84f, 0.80f));   // the heads' own tint: the hands' map is painted in the heads' skin colour (the darker, redder tint was for the generated hand's map and left these hands a different man's)
    UnityEditor.EditorUtility.SetDirty(hm);
    hands = new[] { hm };
}
else
{
    var handL = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Models/Characters/KPA_Rifleman/Materials_Game/LeftHand_Skin_v3.mat");
    var handR = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Project/Art/Models/Characters/KPA_Rifleman/Materials_Game/RightHand_Skin_v3.mat");
    if (handL == null || handR == null) return "hand materials missing";
    hands = new[] { handL, handR };
}
UnityEngine.Material[] With(UnityEngine.Material first) { var m = new UnityEngine.Material[1 + hands.Length]; m[0] = first; hands.CopyTo(m, 1); return m; }
smr.sharedMesh = mesh;
var fitLog = fit.Count > 0 ? "A " + FitBones(root) + ", " + FitAvatar(root) : "";
smr.sharedMaterials = With(body);
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
// the variant keeps its own hands
var pathB = "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman_B.prefab";
var rootB = UnityEditor.PrefabUtility.LoadPrefabContents(pathB);
var smrB = rootB.GetComponentInChildren<UnityEngine.SkinnedMeshRenderer>();
SetBones(rootB);
smrB.sharedMaterials = With(bodyB);
if (fit.Count > 0) fitLog += " | B " + FitBones(rootB) + ", " + FitAvatar(rootB);
string b = smrB.sharedMesh != null ? smrB.sharedMesh.name : "null";
UnityEditor.PrefabUtility.SaveAsPrefabAsset(rootB, pathB);
UnityEditor.PrefabUtility.UnloadPrefabContents(rootB);
UnityEditor.AssetDatabase.SaveAssets();
return "mesh verts " + mesh.vertexCount + " sub " + mesh.subMeshCount + " bounds " + mesh.bounds.size.ToString("F4") + " | hand mats " + string.Join(",", System.Array.ConvertAll(hands, h => h.name)) + " | B mesh " + b + " | bones " + fitLog;
