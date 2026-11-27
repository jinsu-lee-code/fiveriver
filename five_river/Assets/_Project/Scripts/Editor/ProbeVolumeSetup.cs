using System.IO;
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
    // 반사되는 간접광 (부족한 점 2번, 2026-11-26): 적응형 프로브 볼륨(APV).
    // Configure: PC URP 자산의 Light Probe System → Adaptive Probe Volumes(시나리오 · 섞기 켬, 모바일 자산은 그대로),
    //   씬 조명 설정 자산, 전장(지형 영역) Probe Volume 하나, 굽기 묶음(Night · Dawn), 달빛만 Mixed(간접광을 굽고 직접광 · 그림자는 그대로 실시간).
    // Bake: 밤 상태로 Night 를 굽고, DawnDirector 의 새벽 값(달빛 색 · 세기 · 각도, 주변광)으로 바꿔 Dawn 을 구운 뒤 밤 상태로 되돌린다.
    // 실행 중에는 ProbeScenarioBlend 가 DawnDirector 의 새벽 비율로 두 시나리오를 섞는다. 여러 번 실행해도 결과가 같다.
    // 되돌리기: Revert 메뉴 (URP 자산 Light Probe System → Light Probe Groups, 달빛 Realtime, Probe Volume · 조명 설정 연결 제거).
    public static class ProbeVolumeSetup
    {
        public const string Folder = "Assets/_Project/Art/Lighting/APV";
        const string SettingsPath = Folder + "/Dabudong_Lighting.lighting";
        const string BakingSetPath = Folder + "/Dabudong_BakingSet.asset";
        const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        const string VolumeName = "ProbeVolume_Battlefield";
        const string MoonName = "Moon";
        const string ReportPath = "Temp/apv_report.txt";

        // 지형(135 x 180 m, 원점 (-60, -1.65, -60), 높이 64 m) 바깥으로 5 m 더
        static readonly Vector3 VolumeCenter = new Vector3(7.5f, 26f, 30f);
        static readonly Vector3 VolumeSize = new Vector3(145f, 60f, 190f);

        [MenuItem("FiveRivers/Setup/Probe Volumes (APV) Configure")]
        public static string Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "play mode";
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) return "open Dabudong first";
            var sb = new StringBuilder();

            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            var so = new SerializedObject(rp);
            so.FindProperty("m_LightProbeSystem").intValue = (int)LightProbeSystem.ProbeVolumes;
            so.FindProperty("m_ProbeVolumeSHBands").intValue = (int)ProbeVolumeSHBands.SphericalHarmonicsL1;
            so.FindProperty("m_SupportProbeVolumeScenarios").boolValue = true;
            so.FindProperty("m_SupportProbeVolumeScenarioBlending").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine($"URP {RpAssetPath}: lightProbeSystem {rp.lightProbeSystem}, SH {rp.probeVolumeSHBands}, scenarios {rp.supportProbeVolumeScenarios}, blending {rp.supportProbeVolumeScenarioBlending}");

            Directory.CreateDirectory(Folder);
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
            if (settings == null) { settings = new LightingSettings { name = "Dabudong_Lighting" }; AssetDatabase.CreateAsset(settings, SettingsPath); }
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.autoGenerate = false;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;

            var set = AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(BakingSetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<ProbeVolumeBakingSet>(); set.name = "Dabudong_BakingSet"; AssetDatabase.CreateAsset(set, BakingSetPath); }
            if (!set.lightingScenarios.Contains(ProbeScenarioBlend.Night))
            {
                if (set.lightingScenarios.Count > 0 && !set.lightingScenarios.Contains(ProbeScenarioBlend.Dawn)) set.RenameScenario(set.lightingScenarios[0], ProbeScenarioBlend.Night);
                else set.TryAddScenario(ProbeScenarioBlend.Night);
            }
            if (!set.lightingScenarios.Contains(ProbeScenarioBlend.Dawn)) set.TryAddScenario(ProbeScenarioBlend.Dawn);
            set.minDistanceBetweenProbes = 1f;   // 참호 폭(약 1.2 m) 안에도 프로브가 들어가게
            set.simplificationLevels = 3;        // 빈 곳은 27 m 간격
            set.skyOcclusion = false;
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            if (!set.sceneGUIDs.Contains(guid)) set.TryAddScene(guid);
            set.SetSceneBaking(guid, true);
            EditorUtility.SetDirty(set);
            sb.AppendLine($"baking set {BakingSetPath}: scenarios {string.Join(",", set.lightingScenarios)}, scenes {set.sceneGUIDs.Count}, min probe {set.minDistanceBetweenProbes} m, levels {set.simplificationLevels}");

            var volumeGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == VolumeName);
            if (volumeGo == null) { volumeGo = new GameObject(VolumeName); SceneManager_Move(volumeGo, scene); }
            volumeGo.transform.SetPositionAndRotation(VolumeCenter, Quaternion.identity);
            volumeGo.transform.localScale = Vector3.one;
            var volume = volumeGo.GetComponent<ProbeVolume>() ?? volumeGo.AddComponent<ProbeVolume>();
            volume.mode = ProbeVolume.Mode.Local;
            volume.size = VolumeSize;
            volume.fillEmptySpaces = false;
            EditorUtility.SetDirty(volume);
            sb.AppendLine($"volume {VolumeName} center {VolumeCenter} size {VolumeSize}");

            var moon = scene.GetRootGameObjects().Select(g => g.GetComponent<Light>()).FirstOrDefault(l => l != null && l.name == MoonName);
            if (moon != null && moon.lightmapBakeType != LightmapBakeType.Mixed) { moon.lightmapBakeType = LightmapBakeType.Mixed; EditorUtility.SetDirty(moon); }
            sb.AppendLine($"moon {(moon != null ? moon.lightmapBakeType.ToString() : "missing")}");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            File.WriteAllText(ReportPath, sb.ToString());
            return sb.ToString();
        }

        static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        // ---- 굽기 (밤 → 새벽, 끝나면 밤 상태로) -------------------------------------------------------------------------
        static int step;
        static double started, stepStarted;
        static Color moonColor, sky, equator;
        static float moonIntensity;
        static Quaternion moonRotation;
        static readonly StringBuilder bakeLog = new StringBuilder();

        [MenuItem("FiveRivers/Setup/Probe Volumes (APV) Bake Night + Dawn")]
        public static string Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "play mode";
            if (Lightmapping.isRunning || AdaptiveProbeVolumes.isRunning) return "a bake is running";
            Configure();
            bakeLog.Clear();
            step = 0;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            File.WriteAllText(ReportPath, "baking\n");
            return "bake started";
        }

        static Light Moon() => EditorSceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.GetComponent<Light>()).FirstOrDefault(l => l != null && l.name == MoonName);

        static void Tick()
        {
            if (Lightmapping.isRunning || AdaptiveProbeVolumes.isRunning) return;
            double now = EditorApplication.timeSinceStartup;
            var volume = ProbeReferenceVolume.instance;
            var moon = Moon();
            switch (step)
            {
                case 0: // 밤
                    moonColor = moon.color; moonIntensity = moon.intensity; moonRotation = moon.transform.rotation;
                    sky = RenderSettings.ambientSkyColor; equator = RenderSettings.ambientEquatorColor;
                    volume.lightingScenario = ProbeScenarioBlend.Night;
                    stepStarted = now;
                    if (!AdaptiveProbeVolumes.BakeAsync()) { Fail("night bake did not start"); return; }
                    step = 1;
                    break;
                case 1: // 새벽 값으로
                    bakeLog.AppendLine($"Night baked in {now - stepStarted:0.0}s");
                    var dawn = Object.FindFirstObjectByType<DawnDirector>();
                    if (dawn == null) { Fail("no DawnDirector"); return; }
                    var d = new SerializedObject(dawn);
                    moon.color = d.FindProperty("dawnLight").colorValue;
                    moon.intensity = d.FindProperty("dawnIntensity").floatValue;
                    moon.transform.rotation = Quaternion.Euler(d.FindProperty("dawnLightEuler").vector3Value);
                    RenderSettings.ambientSkyColor = d.FindProperty("dawnSky").colorValue;
                    RenderSettings.ambientEquatorColor = d.FindProperty("dawnEquator").colorValue;
                    volume.lightingScenario = ProbeScenarioBlend.Dawn;
                    stepStarted = now;
                    if (!AdaptiveProbeVolumes.BakeAsync()) { Restore(moon); Fail("dawn bake did not start"); return; }
                    step = 2;
                    break;
                case 2: // 밤으로 되돌리고 저장
                    bakeLog.AppendLine($"Dawn baked in {now - stepStarted:0.0}s");
                    Restore(moon);
                    volume.lightingScenario = ProbeScenarioBlend.Night;
                    var scene = EditorSceneManager.GetActiveScene();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    AssetDatabase.SaveAssets();
                    EditorApplication.update -= Tick;
                    bakeLog.Insert(0, $"RESULT apv bake done in {now - started:0.0}s\n");
                    bakeLog.AppendLine(DataReport());
                    File.WriteAllText(ReportPath, bakeLog.ToString());
                    break;
            }
        }

        static void Restore(Light moon)
        {
            moon.color = moonColor; moon.intensity = moonIntensity; moon.transform.rotation = moonRotation;
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator;
        }

        static void Fail(string why)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText(ReportPath, "RESULT failed: " + why + "\n" + bakeLog);
        }

        // 구운 결과 파일 크기 (저장소 용량)
        public static string DataReport()
        {
            var sb = new StringBuilder();
            long total = 0;
            var scene = EditorSceneManager.GetActiveScene();
            var dirs = new[] { Folder, Path.Combine(Path.GetDirectoryName(scene.path), Path.GetFileNameWithoutExtension(scene.path)) };
            foreach (var dir in dirs.Distinct())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))
                {
                    long n = new FileInfo(f).Length; total += n;
                    sb.AppendLine($"  {f.Replace('\\', '/')} {n / 1024.0:0.0} KB");
                }
            }
            sb.AppendLine($"  total {total / (1024.0 * 1024.0):0.00} MB");
            var v = ProbeReferenceVolume.instance;
            sb.AppendLine($"  runtime: initialized {v.isInitialized}, scenario {v.lightingScenario}, video memory {v.GetVideoMemoryCost() / (1024.0 * 1024.0):0.0} MB");
            return sb.ToString();
        }

        // ---- 되돌리기 ---------------------------------------------------------------------------------------------------
        [MenuItem("FiveRivers/Setup/Probe Volumes (APV) Revert")]
        public static string Revert()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "play mode";
            var scene = EditorSceneManager.GetActiveScene();
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);
            var so = new SerializedObject(rp);
            so.FindProperty("m_LightProbeSystem").intValue = (int)LightProbeSystem.LegacyLightProbes;
            so.FindProperty("m_SupportProbeVolumeScenarios").boolValue = false;
            so.FindProperty("m_SupportProbeVolumeScenarioBlending").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == VolumeName);
            if (go != null) Object.DestroyImmediate(go);
            var moon = Moon();
            if (moon != null) moon.lightmapBakeType = LightmapBakeType.Realtime;
            Lightmapping.lightingSettings = null;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "APV reverted (baked files left in " + Folder + " and next to the scene; delete them by hand if not needed)";
        }
    }
}
