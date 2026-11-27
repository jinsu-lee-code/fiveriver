using System.IO;
using System.Text;
using FiveRivers.Data;
using FiveRivers.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    // 밤 밝기 2차 (2026-11-26, 사용자 지시 "밤인데 너무 밝은데 조명탄이 터져도 저정도로 밝기는 아니지 않을까?").
    // 11-24 에는 조명탄이 없을 때 들판을 밝히던 것(달 · 반사광 · 주변광 · 밤하늘 · 안개색)을 낮췄고, 조명탄 아래 밝기를 정하는
    // 밤 노출 +1.8 은 그대로 두어, 조명탄이 뜨면 들판이 풀색 · 흙색 그대로 낮처럼, 먼 언덕 · 하늘까지 해 질 녘처럼 보였다.
    // 실제: 조명탄 아래 땅 1~5 lux, 보름달 0.1~0.3 lux — 사람 눈엔 색이 거의 빠지고(박명시) 가까운 것의 윤곽 · 명암만,
    // 조명탄에서 멀어질수록 빠르게 어두워지고 먼 능선은 거의 검고, 하늘은 어두운 남색.
    // 바꾸는 것(절대값이라 여러 번 실행해도 같음): 밤 노출(밤 색보정), 밤 채도(대기 볼륨), 조명탄 빛 범위(FlareConfig),
    // 안개의 조명 산란(대기 볼륨), 밤하늘 돔 밝기. 새벽은 새벽 색보정(우선순위 1, 노출 1 · 채도 -4)이 덮어써 그대로.
    // 조명탄 첫 연출 근접 컷의 자체 노출(0.6, FlareRevealDirector)은 그대로. 되돌리기: Revert 메뉴(11-24 값).
    public static class NightFlare20261126
    {
        const string NightProfilePath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_Night_Profile.asset";
        const string AtmosphereProfilePath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_Atmosphere_20261124.asset";
        const string SkyMaterialPath = "Assets/_Project/Art/FrontlinePolish20261102/Frontline_NightDawnSky.mat";
        const string FlarePath = "Assets/_Project/Data/FlareConfig.asset";

        // shadowLift (2026-11-27): lifts only the darkest tones of the night grade (ShadowsMidtonesHighlights, shadows below
        // shadowEnd), so the trench wall and sandbags right in front read without raising the flare-lit field; 0 = off
        public struct Values { public float exposure, saturation, flareRange, fogLight, sky, shadowLift, shadowEnd; }

        // 2026-11-27 (사용자 "지금 밝기가 너무 어두운 거 같지 않니?", 후보 A 1.25 · B 1.4 · C 1.2+그늘 들기를 같은 자리에서 비교,
        // 감독 결정 B): 밤 노출만 1.0 -> 1.4. 조명탄 없을 때 참호 앞 모래주머니 · 발밑 길이 읽히고, 하늘 · 먼 능선은 어둡게 남는다.
        public static readonly Values Now = new Values { exposure = 1.4f, saturation = -60f, flareRange = 90f, fogLight = 0.7f, sky = 0.06f, shadowLift = 0f, shadowEnd = 0.2f };
        public static readonly Values Before = new Values { exposure = 1.8f, saturation = -32f, flareRange = 110f, fogLight = 1.5f, sky = 0.085f, shadowLift = 0f, shadowEnd = 0.2f };

        [MenuItem("FiveRivers/Setup/Night Flare 20261126")]
        public static string Apply() => Set(Now);

        [MenuItem("FiveRivers/Setup/Night Flare 20261126 Revert")]
        public static string Revert() => Set(Before);

        // a quick try of other values: Temp/night_flare.txt with lines "exposure 1.1" etc.
        public static string ApplyTry()
        {
            var v = Now;
            if (File.Exists("Temp/night_flare.txt"))
                foreach (var raw in File.ReadAllLines("Temp/night_flare.txt"))
                {
                    var q = raw.Trim().Split(' '); if (q.Length != 2) continue;
                    float x = float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture);
                    if (q[0] == "exposure") v.exposure = x; else if (q[0] == "saturation") v.saturation = x; else if (q[0] == "flareRange") v.flareRange = x;
                    else if (q[0] == "fogLight") v.fogLight = x; else if (q[0] == "sky") v.sky = x;
                    else if (q[0] == "shadowLift") v.shadowLift = x; else if (q[0] == "shadowEnd") v.shadowEnd = x;
                }
            return Set(v);
        }

        static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            profile.components.RemoveAll(c => c == null);
            var effect = profile.TryGet<T>(out var existing) ? existing : profile.Add<T>(false);
            if (!EditorUtility.IsPersistent(effect)) AssetDatabase.AddObjectToAsset(effect, profile);
            effect.active = true;
            EditorUtility.SetDirty(effect);
            return effect;
        }

        static string Set(Values v)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "edit mode only";
            var log = new StringBuilder();
            var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(NightProfilePath);
            var grade = Effect<ColorAdjustments>(night);
            log.AppendLine($"night exposure {grade.postExposure.value:0.##} -> {v.exposure:0.##}");
            grade.postExposure.Override(v.exposure);
            var lift = Effect<ShadowsMidtonesHighlights>(night);
            log.AppendLine($"night shadow lift {lift.shadows.value.w:0.###} -> {v.shadowLift:0.###} (below {v.shadowEnd:0.##})");
            lift.shadows.Override(new Vector4(1f, 1f, 1f, v.shadowLift));
            lift.shadowsStart.Override(0f); lift.shadowsEnd.Override(v.shadowEnd);
            lift.active = v.shadowLift > 0f;
            // the dawn grade (priority 1, weight rising with the dawn) puts the shadows back to neutral, so the dawn is unchanged
            var dawnProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Art/FrontlinePolish20261102/Frontline_Dawn_Grade.asset");
            if (dawnProfile != null && v.shadowLift > 0f)
            {
                var dawnLift = Effect<ShadowsMidtonesHighlights>(dawnProfile);
                dawnLift.shadows.Override(new Vector4(1f, 1f, 1f, 0f)); dawnLift.shadowsStart.Override(0f); dawnLift.shadowsEnd.Override(v.shadowEnd);
                EditorUtility.SetDirty(dawnProfile);
            }
            var atmosphere = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AtmosphereProfilePath);
            var sat = Effect<ColorAdjustments>(atmosphere);
            log.AppendLine($"night saturation {sat.saturation.value:0} -> {v.saturation:0}");
            sat.saturation.Override(v.saturation);
            var fog = Effect<FrontlineVolumetricFog>(atmosphere);
            log.AppendLine($"fog light scattering {fog.lightScattering.value:0.##} -> {v.fogLight:0.##}");
            fog.lightScattering.Override(v.fogLight);
            var flare = AssetDatabase.LoadAssetAtPath<FlareConfig>(FlarePath);
            log.AppendLine($"flare range {flare.lightRange:0} -> {v.flareRange:0} m (intensity {flare.lightIntensity:0} kept)");
            flare.lightRange = v.flareRange; EditorUtility.SetDirty(flare);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky != null && sky.HasProperty("_NightExposure")) { log.AppendLine($"night sky {sky.GetFloat("_NightExposure"):0.###} -> {v.sky:0.###}"); sky.SetFloat("_NightExposure", v.sky); EditorUtility.SetDirty(sky); }
            EditorUtility.SetDirty(night); EditorUtility.SetDirty(atmosphere);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/night_flare_log.txt", log.ToString());
            return log.ToString();
        }
    }
}
