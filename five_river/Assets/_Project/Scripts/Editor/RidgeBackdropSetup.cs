using System.Text;
using FiveRivers.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.EditorTools
{
    // 먼 산 배경 (2026-11-18, 사용자 지시 "너가 직접 해결해봐").
    // 맵을 두른 구릉 메시(Ridges_Continuous_Backdrop)는 150 m 밖이 장면 안개에 잠겨 단색 면으로만 보였다.
    // 그 뒤에 능선 띠 세 겹을 세운다: 가까운 겹일수록 진하고 뒤로 갈수록 하늘에 가까워진다.
    // 근거 사진: US Army "Hill 518" (1950-09, 왜관 북쪽, 퍼블릭 도메인) — 완만하게 겹친 능선, 뭉툭한 봉우리,
    // 윗비탈의 얼룩덜룩한 수목과 아랫비탈의 맨흙. 사진 파일은 쓰지 않고 눈으로 보고 따라 만들었다.
    // 기존 구릉 메시는 그대로 둔다(가까운 · 중간 거리의 땅이 그 메시다). 여러 번 실행해도 결과가 같다.
    public static class RidgeBackdropSetup
    {
        const string Folder = "Assets/_Project/Art/Maps/RidgeBackdrop20261118/";
        const string TexturePath = Folder + "T_ENV_RidgeBand.png";
        const string ShaderPath = Folder + "RidgeBackdrop.shader";
        const string PatchTexturePath = Folder + "T_ENV_SlopePatch.png";
        const string DetailShaderPath = Folder + "DistantSlopeDetail.shader";
        const string DetailName = "SlopeDetail";
        // 안개에 잠긴 구릉의 수목 얼룩 · 등진 사면 음영 (화면을 보고 맞춘 값)
        const float DetailPatch = .18f, DetailShade = .14f, DetailStart = .80f, DetailTile = 80f, DetailHeightBias = .002f;
        const string RootName = "[RidgeBackdrop]";
        const int Segments = 360;
        const float CrownMetres = 12f;      // 텍스처의 능선 위 띠(위 1/4) 높이, 텍스처 한 장이 TileMetres 일 때
        const float TileMetres = 614f;      // 4096 px x 0.15 m. 띠마다 한 장의 실제 너비에 맞춰 나무 높이를 같이 줄인다
        const float Foot = -18f;            // 띠 바닥 (지면 아래)
        static readonly Vector3 Centre = new Vector3(7.5f, 0f, 30f); // 지형 한가운데

        struct Band
        {
            public string name; public float radius, low, high; public int tiles; public float offset; public int seed, queue; public float peakAt;
            public Color night, nightHaze, dawn; public float nightContrast, dawnContrast, dawnSaturation, dawnHaze, shade, sunGain, fadeLow, fadeHigh;
        }

        // 높이는 지형 한가운데에서 본 올려본각으로 정했다: 가까운 겹 4~8도, 가운데 6~10.5도, 먼 겹 8~12.5도.
        // (유학산 839 m 는 다부동 골짜기 바닥에서 2~3 km, 올려본각 13~18도)
        static readonly Band[] Bands =
        {
            new Band { name = "Ridge_Far", radius = 640f, low = 100f, high = 165f, tiles = 7, offset = .37f, seed = 71, queue = 2600, peakAt = -28f,
                night = new Color(.075f, .110f, .200f), nightContrast = .30f, nightHaze = new Color(.016f, .024f, .044f, 0f),
                dawn = new Color(.36f, .37f, .42f), dawnContrast = .34f, dawnSaturation = .25f, dawnHaze = .66f,
                shade = .22f, sunGain = .10f, fadeLow = .10f, fadeHigh = .80f },
            new Band { name = "Ridge_Mid", radius = 480f, low = 55f, high = 100f, tiles = 6, offset = .61f, seed = 43, queue = 2601, peakAt = 38f,
                night = new Color(.065f, .095f, .170f), nightContrast = .42f, nightHaze = new Color(.016f, .024f, .044f, 0f),
                dawn = new Color(.27f, .275f, .30f), dawnContrast = .42f, dawnSaturation = .40f, dawnHaze = .46f,
                shade = .28f, sunGain = .16f, fadeLow = .12f, fadeHigh = .85f },
            new Band { name = "Ridge_Near", radius = 345f, low = 24f, high = 50f, tiles = 4, offset = .13f, seed = 19, queue = 2602, peakAt = 150f,
                night = new Color(.055f, .080f, .145f), nightContrast = .52f, nightHaze = new Color(.016f, .024f, .044f, 0f),
                dawn = new Color(.20f, .20f, .205f), dawnContrast = .50f, dawnSaturation = .55f, dawnHaze = .26f,
                shade = .32f, sunGain = .22f, fadeLow = .15f, fadeHigh = .95f },
        };

        [MenuItem("FiveRivers/Setup/Ridge Backdrop")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드에서 실행");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) scene = EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var report = new StringBuilder();

            ImportTexture();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (texture == null || shader == null) throw new System.InvalidOperationException("먼 산 텍스처 또는 셰이더가 없다: " + Folder);

            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            root.transform.SetParent(null);
            root.transform.SetPositionAndRotation(Centre, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            if (root.GetComponent<RidgeBackdrop>() == null) root.AddComponent<RidgeBackdrop>();

            int triangles = 0;
            foreach (var band in Bands)
            {
                var mesh = BuildMesh(band);
                string meshPath = Folder + "SM_ENV_" + band.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved == null) { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); }
                triangles += saved.triangles.Length / 3;

                string matPath = Folder + "M_ENV_" + band.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, matPath); }
                material.shader = shader;
                material.SetTexture("_MainTex", texture);
                material.SetColor("_NightColor", band.night);
                material.SetFloat("_NightContrast", band.nightContrast);
                material.SetColor("_NightHaze", band.nightHaze);
                material.SetColor("_DawnColor", band.dawn);
                material.SetFloat("_DawnContrast", band.dawnContrast);
                material.SetFloat("_DawnSaturation", band.dawnSaturation);
                material.SetFloat("_DawnHaze", band.dawnHaze);
                material.SetFloat("_Shade", band.shade);
                material.SetFloat("_SunGain", band.sunGain);
                material.SetFloat("_FadeLow", band.fadeLow);
                material.SetFloat("_FadeHigh", band.fadeHigh);
                material.renderQueue = band.queue; // far to near
                EditorUtility.SetDirty(material);

                var child = root.transform.Find(band.name);
                var go = child != null ? child.gameObject : new GameObject(band.name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
                var filter = go.GetComponent<MeshFilter>(); if (filter == null) filter = go.AddComponent<MeshFilter>();
                var renderer = go.GetComponent<MeshRenderer>(); if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
                filter.sharedMesh = saved;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                report.Append($"{band.name} r{band.radius:0} {band.low:0}-{band.high:0}m; ");
            }
            // ---- the fogged slopes of the near hills -------------------------------------------------------
            ImportPatch();
            var patch = AssetDatabase.LoadAssetAtPath<Texture2D>(PatchTexturePath);
            var detailShader = AssetDatabase.LoadAssetAtPath<Shader>(DetailShaderPath);
            if (patch == null || detailShader == null) throw new System.InvalidOperationException("구릉 얼룩 텍스처 또는 셰이더가 없다: " + Folder);
            string detailPath = Folder + "M_ENV_DistantSlopeDetail.mat";
            var detail = AssetDatabase.LoadAssetAtPath<Material>(detailPath);
            if (detail == null) { detail = new Material(detailShader); AssetDatabase.CreateAsset(detail, detailPath); }
            detail.shader = detailShader;
            detail.SetTexture("_MainTex", patch);
            detail.SetFloat("_Tile", DetailTile);
            detail.SetFloat("_Patch", DetailPatch);
            detail.SetFloat("_Shade", DetailShade);
            detail.SetFloat("_Start", DetailStart);
            detail.SetFloat("_HeightBias", DetailHeightBias);
            detail.renderQueue = 2590; // before the ridge bands
            EditorUtility.SetDirty(detail);
            var boxChild = root.transform.Find(DetailName);
            var box = boxChild != null ? boxChild.gameObject : new GameObject(DetailName);
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = Vector3.up * 100f; box.transform.localRotation = Quaternion.identity;
            box.transform.localScale = new Vector3(1500f, 700f, 1500f); // the camera is always inside
            var boxFilter = box.GetComponent<MeshFilter>(); if (boxFilter == null) boxFilter = box.AddComponent<MeshFilter>();
            var boxRenderer = box.GetComponent<MeshRenderer>(); if (boxRenderer == null) boxRenderer = box.AddComponent<MeshRenderer>();
            boxFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            boxRenderer.sharedMaterial = detail;
            boxRenderer.shadowCastingMode = ShadowCastingMode.Off; boxRenderer.receiveShadows = false;
            boxRenderer.lightProbeUsage = LightProbeUsage.Off; boxRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            boxRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            boxRenderer.allowOcclusionWhenDynamic = false;
            GameObjectUtility.SetStaticEditorFlags(box, 0);
            triangles += 12;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Append($"triangles {triangles}");
            Debug.Log("[RidgeBackdrop] " + report);
            return report.ToString();
        }

        static void ImportTexture()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null) { AssetDatabase.ImportAsset(TexturePath); importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter; }
            if (importer == null) return;
            bool dirty = false;
            void Set<T>(T current, T wanted, System.Action<T> apply) { if (!Equals(current, wanted)) { apply(wanted); dirty = true; } }
            Set(importer.maxTextureSize, 4096, v => importer.maxTextureSize = v);
            Set(importer.sRGBTexture, true, v => importer.sRGBTexture = v);
            Set(importer.alphaIsTransparency, true, v => importer.alphaIsTransparency = v);
            Set(importer.mipmapEnabled, true, v => importer.mipmapEnabled = v);
            Set(importer.mipMapsPreserveCoverage, true, v => importer.mipMapsPreserveCoverage = v);
            Set(importer.alphaTestReferenceValue, .5f, v => importer.alphaTestReferenceValue = v);
            Set(importer.wrapModeU, TextureWrapMode.Repeat, v => importer.wrapModeU = v);
            Set(importer.wrapModeV, TextureWrapMode.Clamp, v => importer.wrapModeV = v);
            Set(importer.anisoLevel, 8, v => importer.anisoLevel = v);
            Set(importer.filterMode, FilterMode.Trilinear, v => importer.filterMode = v);
            Set(importer.textureCompression, TextureImporterCompression.CompressedHQ, v => importer.textureCompression = v);
            Set(importer.npotScale, TextureImporterNPOTScale.None, v => importer.npotScale = v);
            if (dirty) importer.SaveAndReimport();
        }

        static void ImportPatch()
        {
            var importer = AssetImporter.GetAtPath(PatchTexturePath) as TextureImporter;
            if (importer == null) { AssetDatabase.ImportAsset(PatchTexturePath); importer = AssetImporter.GetAtPath(PatchTexturePath) as TextureImporter; }
            if (importer == null) return;
            if (importer.maxTextureSize == 1024 && !importer.sRGBTexture && importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Repeat
                && importer.anisoLevel == 4 && importer.filterMode == FilterMode.Trilinear) return;
            importer.maxTextureSize = 1024; importer.sRGBTexture = false; importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat; importer.anisoLevel = 4; importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
        }

        // 능선 높이: 낮은 주파수의 물결을 겹친 완만한 윤곽(뭉툭한 봉우리와 긴 어깨). 뾰족한 봉우리가 나오지 않게
        // 잔 물결은 작게 둔다. 가장 가파른 곳이 25도 안팎 (Hill 518 사진의 능선 경사 15~25도).
        // 가장 높은 봉우리를 정한 방위(북 = +Z 에서 시계 방향, 도)에 놓는다: 먼 겹의 큰 산은 정면 왼쪽(유학산 쪽).
        static float Crest(Band band, float angle)
        {
            float best = 0f, bestAngle = 0f;
            for (int i = 0; i < 720; i++)
            {
                float a = i / 720f * Mathf.PI * 2f, h = CrestRaw(band, a);
                if (h > best) { best = h; bestAngle = a; }
            }
            return CrestRaw(band, angle - band.peakAt * Mathf.Deg2Rad + bestAngle);
        }

        static float CrestRaw(Band band, float angle)
        {
            var random = new System.Random(band.seed);
            int[] wave = { 2, 3, 5, 7, 11, 17, 29 };
            float[] weight = { .50f, .46f, .36f, .28f, .15f, .07f, .03f };
            float sum = 0f, total = 0f;
            for (int i = 0; i < wave.Length; i++)
            {
                float phase = (float)random.NextDouble() * Mathf.PI * 2f;
                sum += weight[i] * Mathf.Sin(wave[i] * angle + phase);
                total += weight[i];
            }
            float s = .5f + .5f * (float)System.Math.Tanh(sum / total * 2.6f); // eases into the top: a rounded crown, not a flat table
            s = Mathf.Lerp(s, s * s, .45f); // long shoulders, fewer and rounder tops
            return Mathf.Lerp(band.low, band.high, s);
        }

        static Mesh BuildMesh(Band band)
        {
            int columns = Segments + 1;
            var vertices = new Vector3[columns * 3];
            var normals = new Vector3[columns * 3];
            var uv = new Vector2[columns * 3];
            for (int i = 0; i < columns; i++)
            {
                float f = i / (float)Segments;
                float angle = f * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                float crest = Crest(band, angle);
                float u = f * band.tiles + band.offset;
                // the slope leans back from foot to crest so the facing changes a little with height
                var normal = (-outward * .82f + Vector3.up * .57f).normalized;
                float step = Mathf.PI * 2f / Segments;
                float along = (Crest(band, angle + step) - Crest(band, angle - step)) / (2f * step * band.radius);
                var tangent = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
                normal = (normal - tangent * along * .8f).normalized;
                vertices[i * 3] = outward * band.radius + Vector3.up * Foot;
                vertices[i * 3 + 1] = outward * band.radius + Vector3.up * crest;
                vertices[i * 3 + 2] = outward * band.radius + Vector3.up * (crest + CrownMetres * (Mathf.PI * 2f * band.radius / band.tiles) / TileMetres);
                uv[i * 3] = new Vector2(u, 0f);
                uv[i * 3 + 1] = new Vector2(u, .75f);
                uv[i * 3 + 2] = new Vector2(u, 1f);
                normals[i * 3] = normals[i * 3 + 1] = normals[i * 3 + 2] = normal;
            }
            var triangles = new int[Segments * 12];
            int t = 0;
            for (int i = 0; i < Segments; i++)
                for (int row = 0; row < 2; row++)
                {
                    int a = i * 3 + row, b = a + 1, c = a + 3, d = a + 4;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = c; triangles[t++] = b; triangles[t++] = d;
                }
            var mesh = new Mesh { name = "SM_ENV_" + band.name };
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateBounds();
            // seen from anywhere on the map: never frustum-culled by a far plane closer than the band
            mesh.bounds = new Bounds(Vector3.up * 60f, new Vector3(band.radius * 2f + 40f, 220f, band.radius * 2f + 40f));
            return mesh;
        }
    }
}
