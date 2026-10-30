#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Blender에서 만든 다부동(328고지 일대, 1950년 8월) 환경 맵을 씬(Dabudong_Env)으로 조립한다.
/// 메뉴: FiveRiver > 다부동 환경 씬 만들기 (다른 프로젝트에서는 Tools > Dabudong > 다부동 환경 씬 만들기)
///
/// 자료 폴더(dabudong_scene.json 이 있는 폴더)는 프로젝트 어디에 두어도 찾는다. 재질·프리팹·지형 데이터는 그 안 Generated/ 에 만든다.
/// 자료는 Dabudong_Map/scripts/export_unity.py 가 만든다(높이맵·스플랫·디테일 .raw, FBX, 구운 텍스처, JSON/CSV).
/// 좌표는 내보낼 때 이미 Unity 기준으로 바꿨다(Unity = (-x, z, -y)).
/// 필요: URP(Universal RP) 14 이상, Unity 2022.2 이상(지형 디테일 인스턴스 개수 모드).
/// </summary>
public static class DabudongEnvImporter
{
    const string Title = "다부동 환경";
    const string ProjectScenes = "Assets/_Project/Scenes";

    // LOD: 화면 높이 비율이 이보다 작아지면 다음 단계로(마지막 단계 아래로는 그리지 않음)
    const float Lod0ScreenHeight = .10f;
    const float Lod1CullFoliage = .02f;
    const float Lod1CullSolid = .01f;

    // 지형
    const float TerrainPixelError = 3f;
    const float BasemapDistance = 250f;
    const float DetailDistance = 150f;          // 이보다 먼 풀·벼는 지형층 색으로 보인다
    const int DetailPatchResolution = 32;

    // 밝기(Unity 6000.3 배치 렌더로 맞춘 값). 해가 동쪽 낮게(14°) 떠 비탈이 그늘에 들므로 주변광을 넉넉히 준다
    const float SunIntensity = 2.4f;
    const float FoliageGlow = .35f;             // 잎 재질 자체발광(바탕색 × 이 값): 양면 잎의 뒷면이 검게 나오는 것을 막는다

    // 콜라이더를 붙일 것(사람이 부딪히는 것). 풀·덤불·그루터기·잔돌은 뺀다.
    static readonly HashSet<string> Solid = new HashSet<string> {
        "sandbag_row", "sandbag_wall", "fighting_position", "trench_revetment", "granite_boulder_a", "granite_boulder_b", "granite_boulder_c",
        "rock_outcrop", "farm_barn", "ox_cart", "fuel_drums", "telegraph_pole", "red_pine", "dead_pine", "onggi_jars", "straw_stack" };

    // 그림자를 끄는 것: 아주 작거나 얇아 그림자 비용만 든다
    static readonly HashSet<string> NoShadow = new HashSet<string> { "rice", "pebble" };

    static string Dir, Gen, OutScene;

    #pragma warning disable 0649   // 아래 정보 클래스의 필드는 JsonUtility가 채운다
    [Serializable] class DetailInfo { public string file; public string asset; public float min; public float max; public int count; }
    [Serializable] class LayerInfo { public string name; public float tile; public float smooth; }
    [Serializable] class TerrainInfo
    {
        public float[] pos; public float[] size;
        public int heightRes; public string height;
        public int splatRes; public string[] splats;
        public int detailRes; public DetailInfo[] details;
        public LayerInfo[] layers;
    }
    [Serializable] class SunInfo { public float[] fwd; public float[] color; public float energy; }
    [Serializable] class FogInfo { public float[] color; public float density; }
    [Serializable] class CamInfo { public string name; public float[] pos; public float[] fwd; public float[] up; public float fov; }
    [Serializable] class SceneInfo { public TerrainInfo terrain; public SunInfo sun; public FogInfo fog; public CamInfo[] cameras; }
    [Serializable] class AssetInfo { public string name; public string fbx; public int slots; public string[] @base; public string[] normal; public bool foliage; public int tris; public string lod1; }
    [Serializable] class AssetList { public AssetInfo[] assets; }
    #pragma warning restore 0649

    // =====================================================================
    //  진입점
    // =====================================================================
    [MenuItem("FiveRiver/다부동 환경 씬 만들기")]
    [MenuItem("Tools/Dabudong/다부동 환경 씬 만들기")]
    public static void Build()
    {
        if (!LocateData())
        {
            EditorUtility.DisplayDialog(Title, "dabudong_scene.json 이 있는 자료 폴더를 찾지 못했습니다.", "확인");
            return;
        }
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
        {
            EditorUtility.DisplayDialog(Title, "이 프로젝트는 URP(Universal Render Pipeline)를 쓰지 않습니다. 재질이 분홍색으로 보이므로 URP로 바꾼 뒤 다시 실행하세요.", "확인");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var d in new[] { Gen, Gen + "/Materials", Gen + "/Prefabs", Gen + "/Terrain" }) EnsureFolder(d);

        var info = JsonUtility.FromJson<SceneInfo>(File.ReadAllText(Full(Dir + "/dabudong_scene.json")));
        var assets = JsonUtility.FromJson<AssetList>(File.ReadAllText(Full(Dir + "/dabudong_assets.json"))).assets;

        try
        {
            EditorUtility.DisplayProgressBar(Title, "텍스처 설정", 0.05f);
            SetupTextureImporters();

            EditorUtility.DisplayProgressBar(Title, "프리팹", 0.2f);
            var prefabs = assets.ToDictionary(a => a.name, MakePrefab);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Dabudong_Environment").transform;

            EditorUtility.DisplayProgressBar(Title, "지형", 0.45f);
            BuildTerrain(info.terrain, prefabs, root);

            EditorUtility.DisplayProgressBar(Title, "원경·참호 메시", 0.65f);
            PlaceTerrainMesh("Terrain_Far", root, collider: false, castShadows: false);   // 원경은 그림자 거리 밖
            PlaceTerrainMesh("Terrain_Trench", root, collider: true, castShadows: true);
            PlaceTerrainMesh("Terrain_CommTrench", root, collider: true, castShadows: true);

            EditorUtility.DisplayProgressBar(Title, "배치", 0.8f);
            PlaceInstances(prefabs, root);

            EditorUtility.DisplayProgressBar(Title, "빛·안개·카메라", 0.95f);
            SetupLighting(info);
            SetupCameras(info.cameras);

            EditorSceneManager.SaveScene(scene, OutScene);
            AssetDatabase.SaveAssets();
            Debug.Log("다부동 환경 씬을 만들었습니다: " + OutScene + " (자료: " + Dir + ")");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// 자료 폴더를 찾는다. 여러 개면 이미 Generated/ 가 있는 폴더(전에 씬을 만든 쪽)를 쓰고 경고를 남긴다.
    /// 씬은 전에 만든 자리(Assets/_Project/Scenes 에 있으면 거기)를 그대로 쓰고, 처음이면 자료 폴더 안에 만든다.
    /// (프로젝트의 Scenes 폴더에 새 씬을 끼워 넣지 않는다)
    /// </summary>
    static bool LocateData()
    {
        var dirs = AssetDatabase.FindAssets("dabudong_scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith("/dabudong_scene.json"))
            .Select(p => p.Substring(0, p.LastIndexOf('/')))
            .OrderByDescending(d => AssetDatabase.IsValidFolder(d + "/Generated"))
            .ThenBy(d => d, StringComparer.Ordinal)
            .ToList();
        if (dirs.Count == 0) return false;
        if (dirs.Count > 1)
            Debug.LogWarning("다부동 자료 폴더가 여러 개입니다. '" + dirs[0] + "' 를 씁니다. 나머지는 지워도 됩니다: " + string.Join(", ", dirs.Skip(1)));

        Dir = dirs[0];
        Gen = Dir + "/Generated";
        string existing = ProjectScenes + "/Dabudong_Env.unity";
        OutScene = File.Exists(Full(existing)) ? existing : Dir + "/Dabudong_Env.unity";
        return true;
    }

    // =====================================================================
    //  텍스처 · 재질 · 프리팹
    // =====================================================================
    /// <summary>노멀맵 종류와 최대 크기를 맞춘다(원경·참호 조각 텍스처는 8K).</summary>
    static void SetupTextureImporters()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir + "/Textures" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            bool normal = p.EndsWith("_Normal.png") || p.EndsWith("_N.jpg");
            bool big = p.Contains("Terrain_Far") || p.Contains("Terrain_Trench") || p.Contains("Terrain_CommTrench");
            var wantType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            int wantSize = big ? 8192 : 2048;
            if (ti.textureType == wantType && ti.maxTextureSize == wantSize) continue;
            ti.textureType = wantType;
            ti.maxTextureSize = wantSize;
            ti.mipmapEnabled = true;
            ti.anisoLevel = big ? 8 : 4;
            ti.SaveAndReimport();
        }
    }

    static Texture2D Tex(string file) =>
        string.IsNullOrEmpty(file) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Textures/" + file);

    static Material MakeMaterial(string name, Texture2D baseMap, Texture2D normalMap, bool twoSided, float smoothness)
    {
        string p = Gen + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetTexture("_BaseMap", baseMap);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", normalMap);
        if (normalMap != null) m.EnableKeyword("_NORMALMAP");
        else m.DisableKeyword("_NORMALMAP");
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Cull", twoSided ? 0f : 2f);                    // 0 = 양면(얇은 잎), 2 = 뒷면 제거
        m.doubleSidedGI = twoSided;
        if (twoSided)
        {
            // URP Lit 은 뒷면 법선을 뒤집지 않아 얇은 잎 뒷면이 그늘처럼 검게 된다 → 바탕색을 약하게 발광시켜 잎 사이로 새는 빛을 흉내 낸다
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", baseMap);
            m.SetColor("_EmissionColor", new Color(FoliageGlow, FoliageGlow, FoliageGlow));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>FBX 의 첫 메시. 재질은 이 스크립트가 만들므로 FBX 재질 가져오기는 끈다.</summary>
    static Mesh FirstMesh(string fbxPath)
    {
        var mi = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (mi != null && (mi.materialImportMode != ModelImporterMaterialImportMode.None || mi.isReadable))
        {
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.isReadable = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.SaveAndReimport();
        }
        var mesh = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Mesh>().FirstOrDefault();
        return mesh != null ? mesh : throw new Exception("FBX에 메시가 없습니다: " + fbxPath);
    }

    static GameObject MeshObject(string name, Mesh mesh, Material[] materials)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        return go;
    }

    static GameObject MakePrefab(AssetInfo a)
    {
        bool glossy = a.name.Contains("rock") || a.name.Contains("boulder");
        var materials = new Material[a.slots];
        for (int i = 0; i < a.slots; i++)
        {
            var baseMap = i < a.@base.Length ? Tex(a.@base[i]) : null;
            var normalMap = a.normal != null && i < a.normal.Length ? Tex(a.normal[i]) : null;
            materials[i] = MakeMaterial($"M_Dabudong_{a.name}_{i}", baseMap, normalMap, twoSided: a.foliage, smoothness: glossy ? .18f : .08f);
        }

        var mesh = FirstMesh(Dir + "/Models/" + a.fbx);
        GameObject go;
        GameObject colliderHost;
        if (!string.IsNullOrEmpty(a.lod1))
        {
            go = new GameObject("ENV_" + a.name);
            var lod0 = MeshObject("LOD0", mesh, materials);
            var lod1 = MeshObject("LOD1", FirstMesh(Dir + "/Models/" + a.lod1), materials);
            lod0.transform.SetParent(go.transform, false);
            lod1.transform.SetParent(go.transform, false);
            var group = go.AddComponent<LODGroup>();
            group.SetLODs(new[] {
                new LOD(Lod0ScreenHeight, new Renderer[] { lod0.GetComponent<Renderer>() }),
                new LOD(a.foliage ? Lod1CullFoliage : Lod1CullSolid, new Renderer[] { lod1.GetComponent<Renderer>() }) });
            group.RecalculateBounds();
            colliderHost = lod0;
        }
        else
        {
            go = MeshObject("ENV_" + a.name, mesh, materials);
            colliderHost = go;
        }
        if (Solid.Contains(a.name)) colliderHost.AddComponent<MeshCollider>().sharedMesh = mesh;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            r.shadowCastingMode = NoShadow.Contains(a.name) ? ShadowCastingMode.Off : ShadowCastingMode.On;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Gen + "/Prefabs/ENV_" + a.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    /// <summary>지형 디테일은 루트에 메시가 있어야 한다. LOD 프리팹이면 가벼운 LOD1 메시만 담은 디테일용 프리팹을 따로 만든다.</summary>
    static GameObject DetailPrefab(GameObject prefab)
    {
        if (prefab.GetComponent<MeshFilter>() != null) return prefab;
        var lod1 = prefab.transform.Find("LOD1");
        var go = MeshObject(prefab.name + "_Detail", lod1.GetComponent<MeshFilter>().sharedMesh, lod1.GetComponent<MeshRenderer>().sharedMaterials);
        var p = PrefabUtility.SaveAsPrefabAsset(go, Gen + "/Prefabs/" + prefab.name + "_Detail.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return p;
    }

    // =====================================================================
    //  지형
    // =====================================================================
    static void BuildTerrain(TerrainInfo t, Dictionary<string, GameObject> prefabs, Transform root)
    {
        var td = new TerrainData { heightmapResolution = t.heightRes };
        td.size = new Vector3(t.size[0], t.size[1], t.size[2]);
        td.SetHeights(0, 0, ReadHeights(t));
        td.terrainLayers = MakeLayers(t.layers);

        td.alphamapResolution = t.splatRes;
        var alphas = ReadSplats(t, td.terrainLayers.Length);

        td.SetDetailResolution(t.detailRes, DetailPatchResolution);
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);   // 디테일 값 = 칸당 개수
        td.detailPrototypes = t.details.Select(d => new DetailPrototype {
            prototype = DetailPrefab(prefabs[d.asset]), usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
            minWidth = d.min, maxWidth = d.max, minHeight = d.min, maxHeight = d.max, noiseSpread = .1f,
            healthyColor = Color.white, dryColor = Color.white, alignToGround = 0f, density = 1f }).ToArray();

        // 스플랫·디테일은 에셋이 만들어진 뒤에 써야 저장된다
        string tdPath = Gen + "/Terrain/Dabudong_TerrainData.asset";
        AssetDatabase.DeleteAsset(tdPath);
        AssetDatabase.CreateAsset(td, tdPath);
        td.SetAlphamaps(0, 0, alphas);
        for (int i = 0; i < t.details.Length; i++)
            td.SetDetailLayer(0, 0, i, ReadDetail(t.details[i].file, t.detailRes));
        EditorUtility.SetDirty(td);

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain_Core";
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(t.pos[0], t.pos[1], t.pos[2]);
        go.isStatic = true;

        var terrain = go.GetComponent<Terrain>();
        string matPath = Gen + "/Materials/M_Dabudong_Terrain.mat";
        AssetDatabase.DeleteAsset(matPath);
        terrain.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        AssetDatabase.CreateAsset(terrain.materialTemplate, matPath);
        terrain.heightmapPixelError = TerrainPixelError;
        terrain.basemapDistance = BasemapDistance;
        terrain.detailObjectDistance = DetailDistance;
        terrain.detailObjectDensity = 1f;
        terrain.drawInstanced = true;
    }

    /// <summary>높이맵: uint16 리틀 엔디언, [z, x] 순서, 0..65535 → 0..1.</summary>
    static float[,] ReadHeights(TerrainInfo t)
    {
        int n = t.heightRes;
        var raw = File.ReadAllBytes(Full(Dir + "/Terrain/" + t.height));
        var h = new float[n, n];
        for (int z = 0, k = 0; z < n; z++)
            for (int x = 0; x < n; x++, k += 2)
                h[z, x] = (raw[k] | (raw[k + 1] << 8)) / 65535f;
        return h;
    }

    static TerrainLayer[] MakeLayers(LayerInfo[] infos)
    {
        var layers = new TerrainLayer[infos.Length];
        for (int i = 0; i < infos.Length; i++)
        {
            var L = infos[i];
            string p = $"{Gen}/Terrain/TL_{L.name}.terrainlayer";
            AssetDatabase.DeleteAsset(p);
            layers[i] = new TerrainLayer {
                diffuseTexture = Tex($"TL_{L.name}_D.jpg"), normalMapTexture = Tex($"TL_{L.name}_N.jpg"),
                tileSize = new Vector2(L.tile, L.tile), smoothness = L.smooth, normalScale = 1f };
            AssetDatabase.CreateAsset(layers[i], p);
        }
        return layers;
    }

    /// <summary>스플랫: 파일마다 RGBA 8비트 = 층 4개, [z, x, 채널] 순서.</summary>
    static float[,,] ReadSplats(TerrainInfo t, int layerCount)
    {
        int n = t.splatRes;
        var a = new float[n, n, layerCount];
        for (int s = 0; s < t.splats.Length; s++)
        {
            var b = File.ReadAllBytes(Full(Dir + "/Terrain/" + t.splats[s]));
            for (int z = 0, k = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                    for (int c = 0; c < 4; c++, k++)
                        if (s * 4 + c < layerCount) a[z, x, s * 4 + c] = b[k] / 255f;
        }
        return a;
    }

    /// <summary>디테일: 8비트, [z, x] 순서, 값 = 칸당 개수.</summary>
    static int[,] ReadDetail(string file, int n)
    {
        var b = File.ReadAllBytes(Full(Dir + "/Terrain/" + file));
        var d = new int[n, n];
        for (int z = 0, k = 0; z < n; z++)
            for (int x = 0; x < n; x++, k++)
                d[z, x] = b[k];
        return d;
    }

    /// <summary>원경 산·참호 고해상도 조각: 구운 텍스처를 붙인 정적 메시(월드 좌표 그대로).</summary>
    static void PlaceTerrainMesh(string name, Transform root, bool collider, bool castShadows)
    {
        var mesh = FirstMesh($"{Dir}/Models/{name}.fbx");
        var mat = MakeMaterial("M_Dabudong_" + name, Tex($"T_{name}_BaseColor.jpg"), Tex($"T_{name}_Normal.png"), twoSided: false, smoothness: .05f);
        var go = MeshObject(name, mesh, new[] { mat });
        go.transform.SetParent(root, false);
        go.isStatic = true;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
    }

    /// <summary>dabudong_instances.csv: asset,x,y,z,qx,qy,qz,qw,scale (Unity 좌표). 에셋별 Props_* 아래에 둔다.</summary>
    static void PlaceInstances(Dictionary<string, GameObject> prefabs, Transform root)
    {
        var groups = new Dictionary<string, Transform>();
        var ci = CultureInfo.InvariantCulture;
        foreach (var line in File.ReadLines(Full(Dir + "/dabudong_instances.csv")).Skip(1))
        {
            var c = line.Split(',');
            if (c.Length < 9 || !prefabs.TryGetValue(c[0], out var prefab)) continue;
            if (!groups.TryGetValue(c[0], out var group))
            {
                group = new GameObject("Props_" + c[0]).transform;
                group.SetParent(root, false);
                groups[c[0]] = group;
            }
            float F(int k) => float.Parse(c[k], ci);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            go.transform.SetPositionAndRotation(new Vector3(F(1), F(2), F(3)), new Quaternion(F(4), F(5), F(6), F(7)));
            go.transform.localScale = Vector3.one * F(8);
            go.isStatic = true;
        }
    }

    // =====================================================================
    //  빛 · 안개 · 카메라
    // =====================================================================
    static Color Col(float[] a) => new Color(a[0], a[1], a[2]);
    static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

    static void SetupLighting(SceneInfo info)
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = Col(info.sun.color);
        sun.intensity = SunIntensity;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.LookRotation(V(info.sun.fwd));
        RenderSettings.sun = sun;

        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", .035f);
        sky.SetFloat("_AtmosphereThickness", 1.15f);
        sky.SetColor("_SkyTint", new Color(.55f, .6f, .68f));
        sky.SetColor("_GroundColor", new Color(.42f, .38f, .32f));
        sky.SetFloat("_Exposure", 1.1f);
        string skyPath = Gen + "/Materials/M_Dabudong_Sky.mat";
        AssetDatabase.DeleteAsset(skyPath);
        AssetDatabase.CreateAsset(sky, skyPath);
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.66f, .71f, .80f);
        RenderSettings.ambientEquatorColor = new Color(.62f, .57f, .50f);
        RenderSettings.ambientGroundColor = new Color(.40f, .34f, .27f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Col(info.fog.color);       // 8월 먼지 낀 대기(누런 회색)
        RenderSettings.fogDensity = info.fog.density;

        // 1950년 기록 사진처럼 채도를 덜고 대비를 조금
        string profilePath = Gen + "/Dabudong_PostProfile.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);     // ACES 는 이 장면에서 너무 어둡다
        var color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(-15f);
        color.contrast.Override(5f);
        color.postExposure.Override(.35f);
        EditorUtility.SetDirty(profile);

        var volume = new GameObject("PostProcess").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    /// <summary>Blender QC 카메라와 같은 자리. CAM_Valley 만 켜서 메인 카메라로 쓴다.</summary>
    static void SetupCameras(CamInfo[] cams)
    {
        var parent = new GameObject("Cameras").transform;
        foreach (var c in cams)
        {
            var go = new GameObject(c.name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(V(c.pos), Quaternion.LookRotation(V(c.fwd), V(c.up)));
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = c.fov;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 9000f;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            bool main = c.name == "CAM_Valley";
            cam.enabled = main;
            if (!main) continue;
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
        }
    }

    // =====================================================================
    static string Full(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
