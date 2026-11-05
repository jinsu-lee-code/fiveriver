using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
public static class ClaudeMapMaterials
{
    const string Src = ClaudeMapIntegration.Stage + "/ArtSource/Textures/PolyHaven/";
    const string ScanDir = ClaudeMapIntegration.Art + "/Scan";
    const string MatDir = ClaudeMapIntegration.Art + "/Materials/";
    const string MeshDir = ClaudeMapIntegration.Art + "/Meshes/";
    const string Original = "Assets/_Project/Art/Maps/Dabudong/Source/Art/Realistic/";
    static readonly float[] PlankBands = { .129f, .299f, .462f, .624f, .792f, .959f };
    const float PlankTile = 1.2f;
    const float PlankSpan = .15f;

    [MenuItem("FiveRivers/Map/Claude Apply Derived Trench Materials")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode.");
        var root=GameObject.Find("/[Map]/Dabudong_Imported");
        if(root==null)throw new InvalidOperationException("Current imported map missing.");
        EnsureFolder(MatDir.TrimEnd('/')); EnsureFolder(MeshDir.TrimEnd('/'));
        var materials=new Dictionary<string,Material>();
        foreach(string name in new[]{"RL_Wood","RL_Earth","RL_Sandbag","RL_Apron"})
        {
            string path=MatDir+name+".mat";
            var target=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(target==null)
            {
                var original=AssetDatabase.LoadAssetAtPath<Material>(Original+"Materials/"+name+".mat");
                if(original==null)throw new InvalidOperationException("Missing source material "+name);
                target=new Material(original){name=name}; AssetDatabase.CreateAsset(target,path);
            }
            materials.Add(name,target);
        }
        var meshes=new Dictionary<string,Mesh>();
        foreach(string name in new[]{"RL_Revetment","RL_Crates"})
        {
            string path=MeshDir+name+".asset";
            var target=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(target==null)
            {
                var original=AssetDatabase.LoadAssetAtPath<Mesh>(Original+"Meshes/"+name+".asset");
                if(original==null)continue;
                target=Object.Instantiate(original);target.name=name;AssetDatabase.CreateAsset(target,path);
            }
            meshes.Add(name,target);
        }
        Upgrade();
        var ground=AssetDatabase.LoadAssetAtPath<TerrainLayer>(ClaudeMapIntegration.Art+"/TL_Claude_Grass.terrainlayer");
        if(ground!=null)
        {
            var apron=materials["RL_Apron"];apron.SetTexture("_BaseMap",ground.diffuseTexture);
            apron.SetTexture("_BumpMap",ground.normalMapTexture);apron.EnableKeyword("_NORMALMAP");
            apron.SetColor("_BaseColor",new Color(.92f,.86f,.75f));apron.SetFloat("_Smoothness",.04f);EditorUtility.SetDirty(apron);
        }
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var assigned=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<assigned.Length;i++)if(assigned[i]!=null&&materials.TryGetValue(assigned[i].name=="RL_Wood_August"?"RL_Wood":assigned[i].name,out var replacement))
            {assigned[i]=replacement;changed=true;}
            if(changed){renderer.sharedMaterials=assigned;EditorUtility.SetDirty(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
        }
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            if(filter.sharedMesh!=null&&meshes.TryGetValue(filter.sharedMesh.name,out var replacement))
            {filter.sharedMesh=replacement;EditorUtility.SetDirty(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(filter);}
        // UV-only clones have identical geometry; existing collider meshes stay intact.
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ClaudeMap] Scan materials/UV clones applied. Source materials and meshes preserved.");
    }
    static void Upgrade()
    {
        EnsureFolder(ScanDir);
        EnsureFolder(ScanDir + "/Backup");

        // 판자: 메시 UV 를 미터 단위로 다시 펴므로 타일링은 1. 거칠기 0.5 쯤이라 매끄러움은 0.4 배로 낮춰 마른 나무로.
        var planks = Import("brown_planks_04", false);
        SetMat("RL_Wood", planks, new Vector2(1f, 1f), new Color(1.02f, .93f, .82f), .4f, 1f); // 푸르게 뜨지 않게 따뜻한 회갈색
        RemapPlanks("RL_Revetment");
        RemapPlanks("RL_Crates");

        // 모래주머니: AddBag UV 는 둘레 U 0..1(약 1.7m), 위아래 V 0..1(약 0.8m). 올 굵기 2.5mm 기준 한 장 ≈ 0.28m
        //   → U 6(정수라야 둘레 이음매가 맞는다), V 3. 마대는 거의 무광이라 매끄러움 0.35 배.
        var hessian = Import("hessian_230", true);
        SetMat("RL_Sandbag", hessian, new Vector2(6f, 3f), new Color(1.2f, 1.26f, 1.32f), .35f, 1f);

        // 흙둑: MakeBerm UV 는 1 = 2m, 스캔 한 장도 약 2m(발자국 크기로 잼) → 1. 원본이 어두워 코드 흙 색에 맞게 밝힌다.
        var mud = Import("brown_mud_02", false);
        SetMat("RL_Earth", mud, new Vector2(1f, 1f), new Color(1.3f, 1.25f, 1.2f), 1f, 1f);

        AssetDatabase.SaveAssets();
        Debug.Log("[FiveRiver] 참호 재질을 실측 스캔으로 바꿨습니다: RL_Wood, RL_Sandbag, RL_Earth");
    }

    class ScanSet { public Texture2D albedo, normal, mask; public bool hasAO; }

    /// <summary>스캔 원본을 Art/Scan 으로 복사하고 마스크를 만든다. arm=true 면 _arm(R AO, G 거칠기), 아니면 _rough(흑백)</summary>
    static ScanSet Import(string name, bool arm)
    {
        string dir = Src + name + "/textures/";
        string diff = name + "_diff_2k.jpg", nor = name + "_nor_gl_2k.jpg";
        var s = new ScanSet { hasAO = arm };
        s.albedo = CopyTex(dir + diff, ScanDir + "/" + diff, TexKind.Color);
        s.normal = CopyTex(dir + nor, ScanDir + "/" + nor, TexKind.Normal);

        // 마스크: R 0, G AO, B 0, A 1 - 거칠기
        string srcMask = FullPath(dir + name + (arm ? "_arm_2k.jpg" : "_rough_2k.jpg"));
        if (!File.Exists(srcMask)) throw new FileNotFoundException("스캔 텍스처 없음: " + srcMask);
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!t.LoadImage(File.ReadAllBytes(srcMask))) throw new IOException("이미지를 읽지 못함: " + srcMask);
        var px = t.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            byte ao = arm ? c.r : (byte)255;
            byte rough = arm ? c.g : c.r;
            px[i] = new Color32(0, ao, 0, (byte)(255 - rough));
        }
        var o = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
        o.SetPixels32(px); o.Apply();
        string maskPath = ScanDir + "/" + name + "_mask.png";
        File.WriteAllBytes(FullPath(maskPath), o.EncodeToPNG());
        Object.DestroyImmediate(t); Object.DestroyImmediate(o);
        AssetDatabase.ImportAsset(maskPath, ImportAssetOptions.ForceUpdate);
        s.mask = Configure(maskPath, TexKind.Mask);
        return s;
    }

    enum TexKind { Color, Normal, Mask }

    static Texture2D CopyTex(string src, string dst, TexKind kind)
    {
        if (!File.Exists(FullPath(src))) throw new FileNotFoundException("스캔 텍스처 없음: " + src);
        File.Copy(FullPath(src), FullPath(dst), true);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
        return Configure(dst, kind);
    }

    static Texture2D Configure(string path, TexKind kind)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = kind == TexKind.Color;          // 노멀·마스크는 선형
        ti.alphaSource = kind == TexKind.Mask ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        ti.alphaIsTransparency = false;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 8;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.maxTextureSize = 2048;
        ti.npotScale = TextureImporterNPOTScale.ToNearest; // hessian 은 2048x2036
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) throw new IOException("텍스처를 불러오지 못함: " + path);
        return tex;
    }

    /// <summary>RL_* 머티리얼을 그 자리에서 고친다. smoothMul 은 마스크 A 에 곱해진다(URP/Lit: 매끄러움 = A × _Smoothness)</summary>
    static void SetMat(string name, ScanSet s, Vector2 tiling, Color tint, float smoothMul, float bump)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");
        if (m == null) throw new FileNotFoundException("머티리얼 없음: " + MatDir + name + ".mat (M01RealisticBuilder 를 먼저 실행)");

        m.SetTexture("_BaseMap", s.albedo);
        m.SetTexture("_MainTex", s.albedo);
        m.SetTextureScale("_BaseMap", tiling); m.SetTextureOffset("_BaseMap", Vector2.zero);
        m.SetTextureScale("_MainTex", tiling); m.SetTextureOffset("_MainTex", Vector2.zero);
        m.SetColor("_BaseColor", tint);
        m.SetColor("_Color", tint);

        m.SetTexture("_BumpMap", s.normal);
        m.SetTextureScale("_BumpMap", tiling);
        m.SetFloat("_BumpScale", bump);
        m.EnableKeyword("_NORMALMAP");

        m.SetFloat("_WorkflowMode", 1f); // 금속성 방식
        m.SetTexture("_MetallicGlossMap", s.mask);
        m.SetFloat("_SmoothnessTextureChannel", 0f); // 금속성 맵의 A
        m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", smoothMul);

        if (s.hasAO)
        {
            m.SetTexture("_OcclusionMap", s.mask); // G 채널을 읽는다
            m.SetFloat("_OcclusionStrength", 1f);
            m.EnableKeyword("_OCCLUSIONMAP");
        }
        else
        {
            m.SetTexture("_OcclusionMap", null);
            m.DisableKeyword("_OCCLUSIONMAP");
        }
        EditorUtility.SetDirty(m);
    }

    /// <summary>
    /// AddBox 상자(면 6 × 꼭짓점 4)의 UV 를 다시 편다. 원래 UV 는 면마다 축이 고정이라 기둥에 가로 널빤지가 층층이 보였다.
    /// 나뭇결(U)은 면의 긴 변을 따라 1.2m 에 1, 짧은 변(V)은 스캔 널빤지 한 장(이음매 사이) 안에 들어가게 한다.
    /// 상자마다 널빤지와 U 위치를 달리 골라 같은 무늬가 되풀이되지 않게 한다. 위치만 보고 계산하므로 다시 실행해도 같다.
    /// </summary>
    static void RemapPlanks(string meshName)
    {
        string path = MeshDir + meshName + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { Debug.LogWarning("[FiveRiver] 메시 없음: " + path); return; }
        if (!mesh.isReadable) { Debug.LogWarning("[FiveRiver] 읽을 수 없는 메시라 UV 를 바꾸지 않음: " + path); return; }
        var v = mesh.vertices;
        if (v.Length == 0 || v.Length % 24 != 0) { Debug.LogWarning("[FiveRiver] 상자 메시가 아니라 UV 를 바꾸지 않음: " + path); return; }

        string backup = ScanDir + "/Backup/" + meshName + "_orig.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(backup) == null && !AssetDatabase.CopyAsset(path, backup))
            throw new IOException("메시 백업 실패: " + backup);

        var uv = new Vector2[v.Length];
        for (int box = 0; box < v.Length / 24; box++)
        {
            var rng = new System.Random(1950 + box * 31);
            float uOff = (float)rng.NextDouble();
            float vOff = PlankBands[rng.Next(PlankBands.Length)] + .008f;
            for (int f = 0; f < 6; f++)
            {
                // AddBox 꼭짓점 순서: a(-u,-v) b(-u,+v) c(+u,+v) d(+u,-v)
                int a = box * 24 + f * 4, b = a + 1, c = a + 2, d = a + 3;
                float lu = (v[d] - v[a]).magnitude, lv = (v[b] - v[a]).magnitude;
                bool alongU = lu >= lv;
                float len = (alongU ? lu : lv) / PlankTile;
                float wid = (alongU ? lv : lu) / PlankTile;
                if (wid <= .2f) wid = Mathf.Min(wid, PlankSpan); // 판자·기둥: 널빤지 한 장 안
                if (alongU)
                {
                    uv[a] = new Vector2(uOff, vOff); uv[b] = new Vector2(uOff, vOff + wid);
                    uv[c] = new Vector2(uOff + len, vOff + wid); uv[d] = new Vector2(uOff + len, vOff);
                }
                else
                {
                    uv[a] = new Vector2(uOff, vOff); uv[b] = new Vector2(uOff + len, vOff);
                    uv[c] = new Vector2(uOff + len, vOff + wid); uv[d] = new Vector2(uOff, vOff + wid);
                }
            }
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        EditorUtility.SetDirty(mesh);
    }

    // =====================================================================
    //  되돌리기 (M01RealisticBuilder.MakeMaterials 와 같은 값)
    // =====================================================================
    static string FullPath(string projectPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, projectPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}


}

