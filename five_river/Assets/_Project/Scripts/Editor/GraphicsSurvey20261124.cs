using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    // Survey for the engine-features graphics pass (2026-11-24): what lights, volumes, cameras, probes and
    // materials the Dabudong scene uses, and how bright each material's albedo is (sRGB 0-255 average of
    // base map x base colour), how metallic and smooth, how strong its normal map. Read only.
    // Output: Temp/gfx_survey.txt and Temp/gfx_materials.tsv. Invoke: FiveRivers.EditorTools.GraphicsSurvey20261124.Run
    public static class GraphicsSurvey20261124
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        public sealed class MatInfo
        {
            public Material material;
            public string shader, path, baseTex, users = "";
            public int renderers;
            public float area;                 // summed bounds surface of the renderers that use it (screen weight)
            public float albedoSrgb = -1f;     // average luma, sRGB 0-255
            public Color albedoColor;          // average colour, sRGB 0-1
            public float metallic = -1f, smoothness = -1f, normal = -1f;
            public string metalSource = "", smoothSource = "";
        }

        public static string Run()
        {
            if (EditorSceneManager.GetActiveScene().path != DabudongSetup.ScenePath) EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var sb = new StringBuilder();
            sb.AppendLine($"fog {RenderSettings.fog} {RenderSettings.fogMode} density {RenderSettings.fogDensity:0.0000} start {RenderSettings.fogStartDistance} end {RenderSettings.fogEndDistance} colour {RenderSettings.fogColor}");
            sb.AppendLine($"ambient {RenderSettings.ambientMode} sky {RenderSettings.ambientSkyColor} equator {RenderSettings.ambientEquatorColor} ground {RenderSettings.ambientGroundColor} intensity {RenderSettings.ambientIntensity} reflection {RenderSettings.defaultReflectionMode} {RenderSettings.reflectionIntensity} skybox {(RenderSettings.skybox ? RenderSettings.skybox.name : "-")} sun {(RenderSettings.sun ? RenderSettings.sun.name : "-")}");
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"volume {Path(v.transform)} global {v.isGlobal} priority {v.priority} weight {v.weight} profile {(v.sharedProfile ? AssetDatabase.GetAssetPath(v.sharedProfile) : "-")} layer {v.gameObject.layer} active {v.isActiveAndEnabled}");
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var d = cam.GetUniversalAdditionalCameraData();
                sb.AppendLine($"camera {Path(cam.transform)} enabled {cam.enabled} post {d.renderPostProcessing} aa {d.antialiasing} {d.antialiasingQuality} volumeMask {d.volumeLayerMask.value} type {d.renderType} near {cam.nearClipPlane} far {cam.farClipPlane} fov {cam.fieldOfView} cull {cam.cullingMask} depth {d.requiresDepthTexture} dither {d.dithering}");
            }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var d = l.GetUniversalAdditionalLightData();
                sb.AppendLine($"light {Path(l.transform)} {l.type} active {l.isActiveAndEnabled} i {l.intensity:0.###} range {l.range:0.#} colour {l.color} shadows {l.shadows} cull {l.cullingMask} layers {(uint)d.renderingLayers} mode {l.lightmapBakeType}");
            }
            foreach (var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"probe {Path(p.transform)} {p.mode} {p.refreshMode} size {p.size} intensity {p.intensity} box {p.boxProjection}");
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"terrain {t.name} material {(t.materialTemplate ? t.materialTemplate.shader.name : "-")} pos {t.transform.position} size {t.terrainData.size}");
                foreach (var tl in t.terrainData.terrainLayers)
                {
                    if (tl == null) continue;
                    var avg = AverageSrgb(tl.diffuseTexture, out var col);
                    var tint = tl.diffuseRemapMax;
                    sb.AppendLine($"  layer {tl.name} tex {(tl.diffuseTexture ? tl.diffuseTexture.name : "-")} avg {avg:0} colour {Hex(col)} remapMax {tint} metallic {tl.metallic} smooth {tl.smoothness} normal {(tl.normalMapTexture ? tl.normalScale.ToString(C) : "-")} mask {(tl.maskMapTexture ? tl.maskMapTexture.name : "-")}");
                }
            }
            foreach (var s in new[] { "Assets/Settings/PC_Renderer.asset" })
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(s);
                foreach (var f in data.rendererFeatures) sb.AppendLine($"feature {s} {(f ? f.GetType().Name + " " + f.isActive : "null")}");
            }
            File.WriteAllText("Temp/gfx_survey.txt", sb.ToString());

            var mats = Materials();
            var tsv = new StringBuilder("material\tshader\tarea\trenderers\talbedo_srgb\tcolour\tmetallic\tmetal_src\tsmooth\tsmooth_src\tnormal\tbase_tex\tpath\tusers\n");
            foreach (var m in mats.OrderByDescending(x => x.area))
                tsv.AppendLine(string.Join("\t", m.material.name, m.shader, m.area.ToString("0.0", C), m.renderers, m.albedoSrgb.ToString("0", C), Hex(m.albedoColor),
                    m.metallic.ToString("0.00", C), m.metalSource, m.smoothness.ToString("0.00", C), m.smoothSource, m.normal.ToString("0.00", C), m.baseTex, m.path, m.users));
            File.WriteAllText("Temp/gfx_materials.tsv", tsv.ToString());
            return $"survey: {mats.Count} materials";
        }


        // who uses these materials (name list in Temp/gfx_who_in.txt, one per line) -> Temp/gfx_who.txt
        public static string Who()
        {
            var names = new HashSet<string>(File.ReadAllLines("Temp/gfx_who_in.txt").Select(x => x.Trim()).Where(x => x.Length > 0));
            var sb = new StringBuilder();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                    if (m != null && names.Contains(m.name))
                        sb.AppendLine($"{m.name}	{Path(r.transform)}	enabled {r.enabled} active {r.gameObject.activeInHierarchy} shadowsOnly {r.shadowCastingMode == ShadowCastingMode.ShadowsOnly} size {r.bounds.size}");
            File.WriteAllText("Temp/gfx_who.txt", sb.ToString());
            return "who " + sb.Length;
        }

        static string Path(Transform t) { var s = t.name; for (var p = t.parent; p != null && s.Length < 160; p = p.parent) s = p.name + "/" + s; return s; }
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // every material on the scene's renderers, active or not, and on prefabs the scene's components spawn
        public static List<MatInfo> Materials()
        {
            var byMat = new Dictionary<Material, MatInfo>();
            void Add(Renderer r, string owner)
            {
                if (r == null) return;
                float area = 0f;
                var b = r.bounds.size;
                if (r.gameObject.scene.IsValid()) area = 2f * (b.x * b.y + b.y * b.z + b.x * b.z);
                else area = 2f * (b.x * b.y + b.y * b.z + b.x * b.z) * 3f; // a spawned prefab is seen several times
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (!byMat.TryGetValue(m, out var info)) byMat[m] = info = Describe(m);
                    info.renderers++;
                    info.area += area / Mathf.Max(1, r.sharedMaterials.Length);
                    if (info.users.Length < 120 && !info.users.Contains(owner)) info.users += (info.users.Length > 0 ? "," : "") + owner;
                }
            }
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Add(r, Root(r.transform));
            }
            var prefabs = new HashSet<GameObject>();
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                while (it.NextVisible(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null)
                    {
                        var o = it.objectReferenceValue;
                        var go = o as GameObject ?? (o as Component)?.gameObject;
                        if (go != null && EditorUtility.IsPersistent(go) && PrefabUtility.IsPartOfPrefabAsset(go)) prefabs.Add(go.transform.root.gameObject);
                    }
            }
            foreach (var p in prefabs)
                foreach (var r in p.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    Add(r, "prefab:" + p.name);
                }
            return byMat.Values.ToList();
        }

        static string Root(Transform t) { while (t.parent != null) t = t.parent; return t.name; }

        public static MatInfo Describe(Material m)
        {
            var info = new MatInfo { material = m, shader = m.shader != null ? m.shader.name : "-", path = AssetDatabase.GetAssetPath(m) };
            Texture tex = null;
            foreach (var p in new[] { "_BaseMap", "_MainTex", "_BaseColorMap", "_Albedo" }) if (m.HasProperty(p) && m.GetTexture(p) != null) { tex = m.GetTexture(p); break; }
            Color tint = Color.white;
            foreach (var p in new[] { "_BaseColor", "_Color" }) if (m.HasProperty(p)) { tint = m.GetColor(p); break; }
            info.baseTex = tex != null ? tex.name : "-";
            Color avgLin;
            if (tex != null) { AverageSrgb(tex, out var c); avgLin = c.linear; } else avgLin = Color.white;
            var lin = new Color(avgLin.r * tint.linear.r, avgLin.g * tint.linear.g, avgLin.b * tint.linear.b);
            info.albedoColor = lin.gamma;
            info.albedoSrgb = 255f * Mathf.LinearToGammaSpace(0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b);
            bool metalMap = m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null;
            if (m.HasProperty("_Metallic")) { info.metallic = m.GetFloat("_Metallic"); info.metalSource = metalMap ? "map" : "value"; }
            if (m.HasProperty("_Smoothness")) { info.smoothness = m.GetFloat("_Smoothness"); info.smoothSource = metalMap ? "map*" : (m.HasProperty("_SmoothnessTextureChannel") && m.GetFloat("_SmoothnessTextureChannel") > 0.5f ? "albedoA*" : "value"); }
            else if (m.HasProperty("_Glossiness")) { info.smoothness = m.GetFloat("_Glossiness"); info.smoothSource = "gloss"; }
            if (metalMap)
            {
                // the map's own average (R metal, A smooth), scaled by the slider as URP Lit does
                var mt = m.GetTexture("_MetallicGlossMap");
                var avg = AverageLinearRgba(mt);
                info.metallic = avg.r; info.metalSource = "map " + avg.r.ToString("0.00", C);
                info.smoothness = avg.a * (m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 1f);
                info.smoothSource = "mapA " + avg.a.ToString("0.00", C) + " x" + (m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness").ToString("0.00", C) : "1");
            }
            if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null) info.normal = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            return info;
        }

        static RenderTexture rt;
        static Texture2D read;

        // average colour of a texture as the shader sees it, returned as sRGB (0-1), luma 0-255 sRGB as result
        public static float AverageSrgb(Texture tex, out Color srgb)
        {
            srgb = Color.white;
            if (tex == null) return -1f;
            var lin = AverageLinearRgba(tex, true);
            srgb = new Color(Mathf.LinearToGammaSpace(lin.r), Mathf.LinearToGammaSpace(lin.g), Mathf.LinearToGammaSpace(lin.b), lin.a);
            float y = 0.2126f * lin.r + 0.7152f * lin.g + 0.0722f * lin.b;
            return 255f * Mathf.LinearToGammaSpace(y);
        }

        // mean of the texture's values in linear space (colour textures decoded from sRGB, data textures raw)
        public static Color AverageLinearRgba(Texture tex, bool colour = false)
        {
            const int N = 128;
            if (rt == null) { rt = new RenderTexture(N, N, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear); rt.Create(); read = new Texture2D(N, N, TextureFormat.RGBAFloat, false, true); }
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, N, N), 0, 0, false);
            RenderTexture.active = prev;
            var px = read.GetPixels();
            double r = 0, g = 0, b = 0, a = 0;
            foreach (var p in px) { r += p.r; g += p.g; b += p.b; a += p.a; }
            int n = px.Length;
            return new Color((float)(r / n), (float)(g / n), (float)(b / n), (float)(a / n));
        }
    }
}
