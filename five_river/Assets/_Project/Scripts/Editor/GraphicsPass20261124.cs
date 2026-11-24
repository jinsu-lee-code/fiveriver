using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    // Engine-features graphics pass (2026-11-24): volumetric fog, post finish. Every menu can be run again and gives
    // the same result (absolute values, objects found by name, sub-assets reused).
    // What each menu changed is written to Docs/Review/gfx_pass_20261124_log.txt (old -> new).
    public static class GraphicsPass20261124
    {
        public const string AtmosphereObject = "[Atmosphere20261124]";
        const string AtmosphereProfilePath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_Atmosphere_20261124.asset";
        const string DawnProfilePath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_Dawn_Grade.asset";
        const string FogShaderPath = "Assets/_Project/Art/VFX/Atmosphere20261124/FrontlineVolumetricFog.shader";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string LogPath = "../Docs/Review/gfx_pass_20261124_log.txt";
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        // lights that shine in the fog: the flares, shell bursts, the tank's fire and explosions, enemy muzzle flashes.
        // Not the player's or the allies' M1 flash, not the bazooka back-blast, not the first-person fill lights.
        static readonly string[] FogLightPrefabs =
        {
            "Assets/_Project/Art/VFX/VFX_Flare.prefab",
            "Assets/_Project/Art/VFX/Shelling20261118/VFX_Shell_Blast.prefab",
            "Assets/_Project/Art/VFX/VFX_MuzzleFlash_Enemy.prefab",
            "Assets/_Project/Art/VFX/VFX_Tank_Fire.prefab",
            "Assets/_Project/Art/VFX/VFX_Tank_HitExplosion.prefab",
            "Assets/_Project/Art/VFX/VFX_Tank_DestroyExplosion.prefab",
            "Assets/_Project/Art/VFX/VFX_Tank_Ricochet.prefab",
        };
        static readonly string[] FogLightSceneNames = { "VFX_Tank_Fire" };

        // night: a thin haze that lies in the low ground and that the lights shine through
        // values chosen by eye on the player's view (2026-11-24, same-frame on/off comparison; see TASKS.md)
        public static readonly Dictionary<string, float> NightFog = new Dictionary<string, float>
        {
            // ambient .5: where no light shines the haze is darker than the distance-fog colour, so the trench and the
            // shadows sink instead of turning milky; the lights do the brightening
            ["density"] = .008f, ["airDensity"] = .0004f, ["baseHeight"] = -1f, ["heightFalloff"] = 4f, ["distance"] = 120f, ["ambient"] = .5f,
            ["mainLightScattering"] = .6f, ["mainLightAnisotropy"] = .55f, ["lightScattering"] = 1.5f, ["lightAnisotropy"] = .25f,
            ["lightNearRadius"] = 4f, ["lightCeiling"] = .15f, ["noise"] = .35f, ["noiseScale"] = .04f, ["steps"] = 18f, // 24 -> 18: 0.5 -> 0.3 ms (frame test 11-24)
        };
        // daybreak (Frontline_Dawn_Grade, weight follows the dawn): mist in the valley, the low sun glows in it
        public static readonly Dictionary<string, float> DawnFog = new Dictionary<string, float>
        {
            ["density"] = .006f, ["airDensity"] = 0f, ["baseHeight"] = -1f, ["heightFalloff"] = 2.5f, ["distance"] = 200f, ["ambient"] = 1f,
            ["mainLightScattering"] = .8f, ["mainLightAnisotropy"] = .7f, ["lightScattering"] = 1f, ["lightAnisotropy"] = .35f,
            ["lightNearRadius"] = 2f, ["noise"] = .45f, ["noiseScale"] = .03f, ["steps"] = 18f,
        };

        static StringBuilder log;
        static void Log(string line) { log.AppendLine(line); }
        static void WriteLog(string title)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, $"==== {title} {System.DateTime.Now:yyyy-MM-dd HH:mm}\n{log}\n");
        }

        static UnityEngine.SceneManagement.Scene OpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드에서 실행");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) scene = EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            return scene;
        }

        static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            profile.components.RemoveAll(c => c == null);
            var effect = profile.TryGet<T>(out var existing) ? existing : profile.Add<T>(true);
            if (!EditorUtility.IsPersistent(effect)) AssetDatabase.AddObjectToAsset(effect, profile);
            effect.active = true;
            EditorUtility.SetDirty(effect);
            return effect;
        }

        public static void SetFog(FrontlineVolumetricFog f, Dictionary<string, float> v, Color tint)
        {
            f.density.Override(v["density"]); f.airDensity.Override(v.TryGetValue("airDensity", out var air) ? air : 0f); f.baseHeight.Override(v["baseHeight"]); f.heightFalloff.Override(v["heightFalloff"]);
            f.distance.Override(v["distance"]); f.ambient.Override(v["ambient"]); f.tint.Override(tint);
            f.mainLightScattering.Override(v["mainLightScattering"]); f.mainLightAnisotropy.Override(v["mainLightAnisotropy"]);
            f.lightScattering.Override(v["lightScattering"]); f.lightAnisotropy.Override(v["lightAnisotropy"]); f.lightNearRadius.Override(v["lightNearRadius"]);
            f.lightCeiling.Override(v.TryGetValue("lightCeiling", out var ceil) ? ceil : 0f);
            f.noise.Override(v["noise"]); f.noiseScale.Override(v["noiseScale"]); f.wind.Override(new Vector3(.6f, 0f, .2f));
            f.steps.Override(Mathf.RoundToInt(v["steps"]));
            EditorUtility.SetDirty(f);
        }

        [MenuItem("FiveRivers/Setup/Volumetric Fog 20261124")]
        public static string ApplyFog()
        {
            var scene = OpenScene();
            log = new StringBuilder();

            // ---- renderer feature on the PC renderer (the mobile renderer is left alone) ----
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(FogShaderPath);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var feature = data.rendererFeatures.OfType<FrontlineVolumetricFogFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FrontlineVolumetricFogFeature>();
                feature.name = "FrontlineVolumetricFog";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var dso = new SerializedObject(data);
                var list = dso.FindProperty("m_RendererFeatures");
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                var map = dso.FindProperty("m_RendererFeatureMap");
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                dso.ApplyModifiedPropertiesWithoutUndo();
                Log("PC_Renderer: added renderer feature FrontlineVolumetricFog");
            }
            var fso = new SerializedObject(feature);
            fso.FindProperty("shader").objectReferenceValue = shader;
            fso.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            EditorUtility.SetDirty(feature); EditorUtility.SetDirty(data);
            Log("feature shader " + (shader != null ? shader.name : "MISSING"));

            // ---- night: a separate global volume above the night grade, so it can be switched off on its own ----
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosphereProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, AtmosphereProfilePath); Log("created " + AtmosphereProfilePath); }
            SetFog(Effect<FrontlineVolumetricFog>(profile), NightFog, Color.white);
            Log("night fog " + string.Join(", ", NightFog.Select(kv => kv.Key + " " + kv.Value.ToString(C))));
            var go = GameObject.Find(AtmosphereObject) ?? new GameObject(AtmosphereObject);
            var volume = go.GetComponent<Volume>(); if (volume == null) volume = go.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = .5f; volume.weight = 1f; volume.sharedProfile = profile;
            go.layer = 0;
            EditorUtility.SetDirty(volume);

            // ---- dawn: the dawn grade volume (its weight follows the daybreak) carries the valley mist ----
            var dawn = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DawnProfilePath);
            SetFog(Effect<FrontlineVolumetricFog>(dawn), DawnFog, new Color(1f, .97f, .94f));
            Log("dawn fog " + string.Join(", ", DawnFog.Select(kv => kv.Key + " " + kv.Value.ToString(C))));

            // ---- which lights scatter ----
            foreach (var path in FogLightPrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = 0;
                    foreach (var l in root.GetComponentsInChildren<Light>(true))
                    {
                        var d = l.GetUniversalAdditionalLightData();
                        uint before = d.renderingLayers;
                        FrontlineVolumetricFog.MarkLight(l);
                        if (d.renderingLayers != before) n++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Log($"fog light layer: {Path.GetFileName(path)} ({n} changed)");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (FogLightSceneNames.Contains(l.name))
                {
                    var d = l.GetUniversalAdditionalLightData();
                    uint before = d.renderingLayers;
                    FrontlineVolumetricFog.MarkLight(l);
                    if (d.renderingLayers != before) { EditorUtility.SetDirty(d); Log("fog light layer: scene " + l.name); }
                }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            WriteLog("Volumetric Fog 20261124");
            return "fog applied";
        }

        // quick tuning without recompiling: Temp/gfx_fog.txt lines "night density 0.006" / "dawn steps 20" / "unityfog 0.01"
        public static string TuneFog()
        {
            var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosphereProfilePath);
            var dawn = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DawnProfilePath);
            int n = 0;
            foreach (var raw in File.ReadAllLines("Temp/gfx_fog.txt"))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length < 3) continue;
                var target = p[0] == "night" ? night : p[0] == "dawn" ? dawn : null;
                if (target == null || !target.TryGet<FrontlineVolumetricFog>(out var f)) continue;
                float v = float.Parse(p[2], C);
                var field = typeof(FrontlineVolumetricFog).GetField(p[1]);
                var param = field?.GetValue(f);
                if (param is ClampedFloatParameter cf) cf.Override(v);
                else if (param is FloatParameter ff) ff.Override(v);
                else if (param is ClampedIntParameter ci) ci.Override(Mathf.RoundToInt(v));
                else continue;
                EditorUtility.SetDirty(f); n++;
            }
            AssetDatabase.SaveAssets();
            return "tuned " + n;
        }

        // ---- reflection probes: the trench and the stream (rendered at runtime only when the light changes) ----
        public const string ProbesObject = "[ReflectionProbes20261124]";
        const int FirstPersonLayer = 31, UiLayer = 5, BlockerLayer = 8;

        [MenuItem("FiveRivers/Setup/Reflection Probes 20261124")]
        public static string ApplyReflections()
        {
            var scene = OpenScene();
            log = new StringBuilder();
            var root = GameObject.Find(ProbesObject) ?? new GameObject(ProbesObject);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // the trench: everything under the sandbag wall groups
            var walls = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r =>
            {
                for (var t = r.transform; t != null; t = t.parent) if (t.name.StartsWith("Trench_Sandbag_Walls")) return true;
                return false;
            }).ToArray();
            if (walls.Length > 0)
            {
                var b = walls[0].bounds; foreach (var w in walls) b.Encapsulate(w.bounds);
                var size = new Vector3(b.size.x + 2f, Mathf.Max(4f, b.size.y + 2.5f), b.size.z + 2f);
                Probe(root, "Probe_Trench", new Vector3(b.center.x, b.min.y + size.y * .5f - .8f, b.center.z), size, 1.5f);
            }
            else Log("trench walls not found: no trench probe");

            // the stream in the valley: a still water surface that now reflects its sky and banks
            var water = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.name == "StreamWater");
            if (water != null)
            {
                var b = water.bounds;
                Probe(root, "Probe_Stream", b.center + Vector3.up * 2f, new Vector3(b.size.x + 14f, 10f, b.size.z + 6f), 3f);
                var m = water.sharedMaterial;
                if (m != null && m.HasProperty("_Smoothness"))
                {
                    float old = m.GetFloat("_Smoothness");
                    if (old < .86f) { m.SetFloat("_Smoothness", .86f); EditorUtility.SetDirty(m); Log($"{m.name} _Smoothness {old:0.###} -> 0.86 (still water)"); }
                }
            }
            else Log("StreamWater not found: no stream probe");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            WriteLog("Reflection Probes 20261124");
            return "probes applied";
        }

        static void Probe(GameObject root, string name, Vector3 center, Vector3 size, float blend)
        {
            var t = root.transform.Find(name);
            var go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            var p = go.GetComponent<ReflectionProbe>(); if (p == null) p = go.AddComponent<ReflectionProbe>();
            p.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            p.size = size; p.center = Vector3.zero; p.boxProjection = true; p.blendDistance = blend;
            p.resolution = 128; p.hdr = true; p.intensity = 1f; p.importance = 1;
            p.shadowDistance = 60f; p.farClipPlane = 300f; p.nearClipPlane = .3f;
            p.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            p.cullingMask = ~((1 << FirstPersonLayer) | (1 << UiLayer) | (1 << BlockerLayer));
            if (go.GetComponent<ReflectionProbeRefresh>() == null) go.AddComponent<ReflectionProbeRefresh>();
            EditorUtility.SetDirty(p);
            Log($"{name}: centre {center} size {size} blend {blend} realtime, rendered when the light changes, 128 px HDR, box projection");
        }

        // ---- trench shade: a height map of the trench rim, read by the fog composite (TrenchShade) ----
        public const string TrenchShadeObject = "[TrenchShade20261124]";
        const string RimMapPath = "Assets/_Project/Art/VFX/Atmosphere20261124/T_TrenchRim20261124.asset";

        [MenuItem("FiveRivers/Setup/Trench Shade 20261124")]
        public static string ApplyTrenchShade()
        {
            var scene = OpenScene();
            log = new StringBuilder();
            var walls = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r =>
            {
                for (var t = r.transform; t != null; t = t.parent) if (t.name.StartsWith("Trench_Sandbag_Walls")) return true;
                return false;
            }).ToArray();
            var b = walls[0].bounds; foreach (var wall in walls) b.Encapsulate(wall.bounds);
            const float Margin = 4f, Cell = .25f, Radius = 1.5f;
            float minX = b.min.x - Margin, minZ = b.min.z - Margin, sizeX = b.size.x + 2f * Margin, sizeZ = b.size.z + 2f * Margin;
            int w = Mathf.CeilToInt(sizeX / Cell), h = Mathf.CeilToInt(sizeZ / Cell);
            var ground = new float[w * h];
            Physics.SyncTransforms();
            const int Mask = ~((1 << FirstPersonLayer) | (1 << UiLayer) | (1 << BlockerLayer) | (1 << 2));
            float top = b.max.y + 10f;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    var from = new Vector3(minX + (i + .5f) * Cell, top, minZ + (j + .5f) * Cell);
                    float y = b.min.y - 2f;
                    // the highest solid thing that is not a man (allies and the player stand in the trench)
                    foreach (var hit in Physics.RaycastAll(from, Vector3.down, 30f, Mask, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
                    {
                        var c = hit.collider;
                        if (c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<Animator>() != null || c.attachedRigidbody != null) continue;
                        y = hit.point.y; break;
                    }
                    ground[j * w + i] = y;
                }
            // the rim: the highest ground within the radius
            int rr = Mathf.CeilToInt(Radius / Cell);
            var rim = new float[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float m = ground[j * w + i];
                    for (int dj = -rr; dj <= rr; dj++)
                        for (int di = -rr; di <= rr; di++)
                        {
                            if (di * di + dj * dj > rr * rr) continue;
                            int x = i + di, z = j + dj;
                            if (x < 0 || z < 0 || x >= w || z >= h) continue;
                            m = Mathf.Max(m, ground[z * w + x]);
                        }
                    rim[j * w + i] = m;
                }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RimMapPath);
            if (tex == null || tex.width != w || tex.height != h)
            {
                if (tex != null) AssetDatabase.DeleteAsset(RimMapPath);
                tex = new Texture2D(w, h, TextureFormat.RHalf, false, true) { name = "T_TrenchRim20261124", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                AssetDatabase.CreateAsset(tex, RimMapPath);
            }
            var px = new Color[w * h];
            for (int k = 0; k < px.Length; k++) px[k] = new Color(rim[k], 0f, 0f, 1f);
            tex.SetPixels(px); tex.Apply(false, false);
            EditorUtility.SetDirty(tex);
            var go = GameObject.Find(TrenchShadeObject) ?? new GameObject(TrenchShadeObject);
            var shade = go.GetComponent<TrenchShade>(); if (shade == null) shade = go.AddComponent<TrenchShade>();
            shade.Set(tex, new Vector4(minX, minZ, sizeX, sizeZ));
            EditorUtility.SetDirty(shade);
            float deepest = 0f; for (int k = 0; k < rim.Length; k++) deepest = Mathf.Max(deepest, rim[k] - ground[k]);
            Log($"trench rim map {w}x{h} ({Cell} m cells, rim radius {Radius} m) over x {minX:0.#}..{minX + sizeX:0.#} z {minZ:0.#}..{minZ + sizeZ:0.#}, deepest {deepest:0.00} m below the rim; shade .35 from .45 m to 1.7 m below the rim, nothing nearer than 1.5 m to the camera");
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            WriteLog("Trench Shade 20261124");
            return $"rim {w}x{h} deepest {deepest:0.00}";
        }

        // ---- night brightness (2026-11-24, user: "밤인거 치고 너무 밝아서 그부분 고쳐봐") ----
        // Without a flare the field read like dusk: earth and grass in their day colours. The flare-lit exposure (the night
        // grade's +1.8) stays, so a flare still lights the field as before; what lit the field without one comes down:
        // the moon, the bounce fill, the ambient sky, the night sky dome and the haze colour, and the night loses more
        // colour. DawnDirector reads these night values at Awake and turns them to dawn as before.
        public static readonly Dictionary<string, float> Night = new Dictionary<string, float>
        {
            ["moon"] = .50f, ["bounce"] = .10f, ["ambient"] = .50f, ["sky"] = .085f, ["fog"] = .60f, ["saturation"] = -32f,
        };
        static readonly Color MoonColor = new Color(.70f, .80f, 1f);
        static readonly Color OldAmbientSky = new Color(.22f, .26f, .34f), OldAmbientEquator = new Color(.21f, .235f, .28f), OldAmbientGround = new Color(.06f, .055f, .047f);
        static readonly Color OldFog = new Color(.12f, .16f, .23f);
        const string SkyMaterialPath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_NightDawnSky.mat";

        [MenuItem("FiveRivers/Setup/Night Exposure 20261124")]
        public static string ApplyNightExposure()
        {
            var scene = OpenScene();
            log = new StringBuilder();
            var v = new Dictionary<string, float>(Night);
            if (File.Exists("Temp/gfx_night.txt"))
                foreach (var raw in File.ReadAllLines("Temp/gfx_night.txt")) { var q = raw.Trim().Split(' '); if (q.Length == 2 && v.ContainsKey(q[0])) v[q[0]] = float.Parse(q[1], C); }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) continue;
                if (l.name == "Moon") { Log($"Moon intensity {l.intensity:0.###} -> {v["moon"]:0.###}, colour {l.color} -> {MoonColor}"); l.intensity = v["moon"]; l.color = MoonColor; EditorUtility.SetDirty(l); }
                else if (l.name == "Moon_Bounce") { Log($"Moon_Bounce intensity {l.intensity:0.###} -> {v["bounce"]:0.###}"); l.intensity = v["bounce"]; EditorUtility.SetDirty(l); }
            }
            // ambient and fog from the 11-16 values times a factor (absolute, so the menu can run again)
            Color Scale(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
            Log($"ambient sky {RenderSettings.ambientSkyColor} -> {Scale(OldAmbientSky, v["ambient"])} (x{v["ambient"]} of the 11-16 night values)");
            RenderSettings.ambientSkyColor = Scale(OldAmbientSky, v["ambient"]);
            RenderSettings.ambientEquatorColor = Scale(OldAmbientEquator, v["ambient"]);
            RenderSettings.ambientGroundColor = Scale(OldAmbientGround, v["ambient"]);
            Log($"fog colour {RenderSettings.fogColor} -> {Scale(OldFog, v["fog"])}");
            RenderSettings.fogColor = Scale(OldFog, v["fog"]);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky != null && sky.HasProperty("_NightExposure")) { Log($"sky _NightExposure {sky.GetFloat("_NightExposure"):0.###} -> {v["sky"]:0.###}"); sky.SetFloat("_NightExposure", v["sky"]); EditorUtility.SetDirty(sky); }
            var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosphereProfilePath);
            var ca = Effect<ColorAdjustments>(night);
            foreach (var prm in ca.parameters) prm.overrideState = false; // only the saturation: the night grade keeps its +1.8 exposure
            ca.saturation.Override(v["saturation"]);
            Log($"night saturation (atmosphere volume over the night grade's -18) -> {v["saturation"]:0}");
            DynamicGI.UpdateEnvironment();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            WriteLog("Night Exposure 20261124");
            return "night exposure applied";
        }

        // ---- post finish: grain and vignette lighter, depth of field only in the staged shots ----
        const string CutsceneDofObject = "[CutsceneDof20261124]";

        [MenuItem("FiveRivers/Setup/Post Finish 20261124")]
        public static string ApplyPostFinish()
        {
            var scene = OpenScene();
            log = new StringBuilder();
            var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosphereProfilePath);
            var vg = Effect<Vignette>(night); vg.intensity.Override(.22f); vg.smoothness.Override(.45f); vg.color.Override(Color.black);
            var gr = Effect<FilmGrain>(night); gr.type.Override(FilmGrainLookup.Medium2); gr.intensity.Override(.15f); gr.response.Override(.8f);
            Log("night (over the night grade): vignette .30 -> .22, grain .20 -> .15");
            var dawn = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DawnProfilePath);
            if (dawn.TryGet<Vignette>(out var dv)) { Log($"dawn vignette {dv.intensity.value:0.##} -> .18"); dv.intensity.Override(.18f); EditorUtility.SetDirty(dv); }
            if (dawn.TryGet<FilmGrain>(out var dg)) { Log($"dawn grain {dg.intensity.value:0.##} -> .12"); dg.intensity.Override(.12f); EditorUtility.SetDirty(dg); }

            var go = GameObject.Find(CutsceneDofObject) ?? new GameObject(CutsceneDofObject);
            var vol = go.GetComponent<Volume>(); if (vol == null) vol = go.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 2f; vol.weight = 0f; vol.sharedProfile = null;
            if (go.GetComponent<CutsceneDepthOfField>() == null) go.AddComponent<CutsceneDepthOfField>();
            EditorUtility.SetDirty(vol);
            Log("cutscene depth of field: Gaussian, far side only, start >= 8 m, weight follows CutsceneRig.Running");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            WriteLog("Post Finish 20261124");
            return "post applied";
        }
    }
}
