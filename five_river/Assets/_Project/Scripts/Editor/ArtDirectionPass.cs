using System.Linq;
using System.Text;
using FiveRivers.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    // 2026-11-16 아트 패스 (사용자 지시 "상업용 수준 퀄리티까지 끌어올려봐").
    // 밤: 달빛은 차갑게, 불빛(조명탄 · 포화 · 등불)만 따뜻하게 남기는 색 분리 + 필름 톤(ACES) + 하늘의 별 · 달무리 · 지평선 띠.
    // 새벽: 낮게 깔린 금빛 해와 푸른 그늘, 하늘이 하얗게 날아가지 않게.
    // 여러 번 실행해도 결과가 같다. 편집 모드에서만.
    public static class ArtDirectionPass
    {
        // 밤하늘 그림의 밝은 구역(동쪽) 상한. 어두운 쪽 하늘 밝기의 약 3배 (2026-11-16)
        internal static float NightCeiling = 0.008f;
        const string NightProfilePath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_Night_Profile.asset";
        const string DawnProfilePath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_Dawn_Grade.asset";
        const string SkyPath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_NightDawnSky.mat";
        const string TrenchFloorLayer = "Assets/_Project/Art/MapPolish20261105/TL_DirtFloor_TrenchOnly.terrainlayer";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string DawnGradeObject = "[DawnGrade]";

        [MenuItem("FiveRivers/Setup/Art Direction Pass (Night + Dawn Grade)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드에서 실행");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) scene = EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var report = new StringBuilder();

            // ---- night grade ------------------------------------------------------------------------------
            var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NightProfilePath);
            Grade(night, new GradeValues
            {
                // ACES has a darker toe than the old neutral curve: +1.4 stops keeps the play value, the grade does the mood.
                // 1.5 -> 1.8 (2026-11-16): measured on the player view, the far slope where they come averaged 26/255 and an
                // enemy at 76 m stood 2.5 levels off it, too little for a bright exhibition hall.
                exposure = 1.8f, contrast = 8f, saturation = -18f, temperature = -5f, tint = -3f,
                shadows = new Color(.43f, .50f, .55f), highlights = new Color(.60f, .55f, .47f), balance = -10f,
                bloomThreshold = 1f, bloomIntensity = .55f, bloomScatter = .72f, bloomTint = new Color(1f, .94f, .86f),
                vignette = .3f, grain = .2f, aberration = .04f, lensFlare = .3f,
            });

            // ---- dawn grade: a second global volume above the night one, its weight follows the daybreak ----
            var dawn = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DawnProfilePath);
            if (dawn == null) { dawn = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(dawn, DawnProfilePath); }
            Grade(dawn, new GradeValues
            {
                exposure = 1.0f, contrast = 18f, saturation = -4f, temperature = 4f, tint = 0f, // 11-16 AAA pass: lit sandbags no longer go orange
                shadows = new Color(.42f, .47f, .57f), highlights = new Color(.63f, .56f, .48f), balance = 14f,
                bloomThreshold = .95f, bloomIntensity = .7f, bloomScatter = .75f, bloomTint = new Color(1f, .9f, .76f),
                vignette = .24f, grain = .14f, aberration = .03f, lensFlare = .35f,
            });
            var dawnGo = GameObject.Find(DawnGradeObject) ?? new GameObject(DawnGradeObject);
            var dawnVolume = dawnGo.GetComponent<Volume>();
            if (dawnVolume == null) dawnVolume = dawnGo.AddComponent<Volume>();
            dawnVolume.isGlobal = true; dawnVolume.priority = 1f; dawnVolume.weight = 0f; dawnVolume.sharedProfile = dawn;
            dawnGo.layer = 0;
            EditorUtility.SetDirty(dawnVolume);

            // ---- the daybreak itself -----------------------------------------------------------------------
            var dawnDirector = Object.FindFirstObjectByType<DawnDirector>(FindObjectsInactive.Include);
            var dso = new SerializedObject(dawnDirector);
            dso.FindProperty("dawnGrade").objectReferenceValue = dawnVolume;
            dso.FindProperty("dawnLight").colorValue = new Color(1f, .78f, .60f);     // low gold sun (less saturated: burlap read orange)
            dso.FindProperty("dawnIntensity").floatValue = 1.5f;
            dso.FindProperty("dawnSky").colorValue = new Color(.44f, .50f, .62f);     // the shade side stays blue
            dso.FindProperty("dawnEquator").colorValue = new Color(.52f, .44f, .40f);
            // 2026-11-16: was (.70, .58, .50), a warm haze brighter than the cool sky above it: the far ridge stood out as a flat cream cut-out. The haze now takes the horizon sky's own grey-blue, a little darker, so the ridge is a soft silhouette.
            dso.FindProperty("dawnFog").colorValue = new Color(.50f, .50f, .56f);
            dso.ApplyModifiedPropertiesWithoutUndo();
            var sunEuler = dso.FindProperty("dawnLightEuler").vector3Value;

            // ---- moon bounce: the low moon is in front of the trench, so every face the player looks at (the
            //      parapet, the enemy coming up the slope) was back-lit and fell to black. A weak shadowless sky
            //      fill from the player's side keeps them readable, the moon still draws the rim and the shadows.
            var moon = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            var bounceGo = GameObject.Find("Moon_Bounce") ?? new GameObject("Moon_Bounce");
            bounceGo.transform.SetParent(moon.transform.parent, false);
            var mf = moon.transform.forward;
            bounceGo.transform.rotation = Quaternion.LookRotation(new Vector3(-mf.x, -0.35f, -mf.z).normalized);
            var bounce = bounceGo.GetComponent<Light>();
            if (bounce == null) bounce = bounceGo.AddComponent<Light>();
            bounce.type = LightType.Directional; bounce.shadows = LightShadows.None;
            bounce.color = new Color(.56f, .66f, .86f); bounce.intensity = .16f;
            bounce.renderMode = LightRenderMode.ForcePixel;
            EditorUtility.SetDirty(bounce);

            // ---- sky ---------------------------------------------------------------------------------------
            moon =RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            sky.SetColor("_HorizonGlow", new Color(.014f, .020f, .034f, 7f));
            sky.SetFloat("_StarBrightness", .4f);
            sky.SetFloat("_StarDensity", .016f);
            sky.SetFloat("_NightCeiling", NightCeiling);
            sky.SetVector("_MoonDir", -moon.transform.forward);
            sky.SetColor("_MoonGlow", new Color(.05f, .065f, .09f, 48f));
            sky.SetFloat("_DawnExposure", .42f);
            sky.SetColor("_DawnTint", new Color(.86f, .92f, 1.04f)); // zenith stays cool, the warmth is only on the sun side
            sky.SetVector("_SunDir", -(Quaternion.Euler(sunEuler) * Vector3.forward));
            sky.SetColor("_SunGlow", new Color(1.1f, .5f, .18f, 5f));
            EditorUtility.SetDirty(sky);

            // ---- the trench floor was the brightest thing on screen at night: bring it to the sandbags' value ----
            var floor = AssetDatabase.LoadAssetAtPath<TerrainLayer>(TrenchFloorLayer);
            floor.diffuseRemapMax = new Vector4(.70f, .67f, .63f, 1f);
            EditorUtility.SetDirty(floor);

            // ---- smoke: these particle materials are unlit, so at night they glowed pale pink against a dark slope.
            //      A burning tank gives black oil smoke and a bullet strike a dim puff of dust.
            void Tint(string path, Color c) { var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m == null) { report.AppendLine("missing " + path); return; } m.SetColor("_TintColor", c); EditorUtility.SetDirty(m); }
            Tint("Assets/_Project/Art/JMO Assets/WarFX/Desktop/Materials/Smoke/WFX_M_SmokeGrenade AB.mat", new Color(.36f, .36f, .38f, .5f));
            Tint("Assets/_Project/Art/JMO Assets/WarFX/Desktop/Materials/Impacts/WFX_M_Impact Smoke.mat", new Color(.30f, .29f, .28f, .5f));

            // ---- grounding and banding ---------------------------------------------------------------------
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var ssao = renderer.rendererFeatures.FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            if (ssao != null)
            {
                var so = new SerializedObject(ssao);
                so.FindProperty("m_Settings.Intensity").floatValue = .9f;
                so.FindProperty("m_Settings.Radius").floatValue = .45f;
                so.FindProperty("m_Settings.DirectLightingStrength").floatValue = .35f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var pipeline = UniversalRenderPipeline.asset;
            var pso = new SerializedObject(pipeline);
            pso.FindProperty("m_HDRColorBufferPrecision").intValue = 1; // 64-bit: the dark sky gradient no longer steps
            pso.ApplyModifiedPropertiesWithoutUndo();
            foreach (var data in Object.FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var cam = data.GetComponent<Camera>();
                if (cam == null || !data.renderPostProcessing) continue;
                data.dithering = true;
                EditorUtility.SetDirty(data);
                report.AppendLine("dithering on " + cam.name);
            }

            // ---- report what the terrain actually paints with, for the next pass ------------------------------
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
                foreach (var l in terrain.terrainData.terrainLayers)
                    report.AppendLine($"terrain layer {l.name} remap {l.diffuseRemapMax}");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("art direction pass applied");
            Debug.Log(report.ToString());
            System.IO.File.WriteAllText("Temp/art_pass.txt", report.ToString());
            return "ok";
        }

        struct GradeValues
        {
            public float exposure, contrast, saturation, temperature, tint, balance;
            public Color shadows, highlights, bloomTint;
            public float bloomThreshold, bloomIntensity, bloomScatter, vignette, grain, aberration, lensFlare;
        }

        static void Grade(VolumeProfile profile, GradeValues v)
        {
            T Effect<T>() where T : VolumeComponent
            {
                profile.components.RemoveAll(c => c == null);
                var effect = profile.TryGet<T>(out var existing) ? existing : profile.Add<T>(true);
                if (!EditorUtility.IsPersistent(effect)) AssetDatabase.AddObjectToAsset(effect, profile);
                effect.active = true;
                EditorUtility.SetDirty(effect);
                return effect;
            }
            Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var c = Effect<ColorAdjustments>();
            c.postExposure.Override(v.exposure); c.contrast.Override(v.contrast); c.saturation.Override(v.saturation);
            c.colorFilter.Override(Color.white); c.hueShift.Override(0f);
            var wb = Effect<WhiteBalance>();
            wb.temperature.Override(v.temperature); wb.tint.Override(v.tint);
            var st = Effect<SplitToning>();
            st.shadows.Override(v.shadows); st.highlights.Override(v.highlights); st.balance.Override(v.balance);
            var b = Effect<Bloom>();
            b.threshold.Override(v.bloomThreshold); b.intensity.Override(v.bloomIntensity); b.scatter.Override(v.bloomScatter);
            b.tint.Override(v.bloomTint); b.highQualityFiltering.Override(true);
            var vg = Effect<Vignette>();
            vg.intensity.Override(v.vignette); vg.smoothness.Override(.42f); vg.color.Override(Color.black);
            var g = Effect<FilmGrain>();
            g.type.Override(FilmGrainLookup.Medium2); g.intensity.Override(v.grain); g.response.Override(.8f);
            Effect<ChromaticAberration>().intensity.Override(v.aberration);
            // only the brightest things (flare, muzzle flash, the dawn sun) are above the bloom threshold, so only they streak
            var lf = Effect<ScreenSpaceLensFlare>();
            lf.intensity.Override(v.lensFlare); lf.tintColor.Override(new Color(1f, .9f, .78f));
            lf.firstFlareIntensity.Override(.5f); lf.secondaryFlareIntensity.Override(.35f); lf.warpedFlareIntensity.Override(.25f);
            lf.streaksIntensity.Override(.6f); lf.streaksLength.Override(.45f); lf.streaksThreshold.Override(.3f);
            lf.chromaticAbberationIntensity.Override(.4f);
            EditorUtility.SetDirty(profile);
        }
    }
}
