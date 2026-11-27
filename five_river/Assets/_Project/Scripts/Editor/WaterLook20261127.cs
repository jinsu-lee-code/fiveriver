using System.Text;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-27 (아트 검수 "새벽 · 고지에서 원경의 강 · 개울이 순백 줄무늬 띠로 빛남"):
    //  - the white striped band was the distant Nakdong (RiverHint20261125, FR_DistantRiver.shader), not the stream: it was painted
    //    in a fixed pale sky colour, so by night it stood at 170 of 255 over land at 7, and its ripple term showed as stripes.
    //    The shader now only lifts what lies behind it by _Lift (see the shader); this menu sets that lift.
    //  - the stream's pools (Stream/M_StreamWater.mat, the sample WaterSurface graph): smoothness 0.8 -> 0.5 and the foam nearly
    //    off (0.35 -> 0.04), so the low-angle sky reflection and the foam do not run to white; it stays silty water.
    // Numbers: BALANCE.md 4-3. Materials only, the scene is not touched. Re-running gives the same result.
    public static class WaterLook20261127
    {
        const string Stream = "Assets/_Project/Art/Maps/Dabudong1950/Stream/M_StreamWater.mat";
        const string River = "Assets/_Project/Art/Maps/Dabudong_Env/RiverHint20261125/M_NakdongWater.mat";

        // ---- numbers (BALANCE.md 4-3)
        public const float StreamSmoothness = 0.50f, StreamFoam = 0.04f, StreamFoamAlpha = 0.05f, StreamReflection = 0.04f, StreamNormal = 0.18f, StreamTrail = 0.05f;
        public static readonly Color RiverLift = new Color(0.26f, 0.30f, 0.38f, 1f);   // (0.13, 0.15, 0.20) first: on the screen under 4 of 255, the river could not be made out at dawn
        public const float RiverStrength = 0.85f, RiverEdge = 0.35f;

        [MenuItem("FiveRivers/Setup/Water Look 20261127")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var sb = new StringBuilder();
            var stream = AssetDatabase.LoadAssetAtPath<Material>(Stream);
            if (stream == null) sb.AppendLine("  missing " + Stream);
            else
            {
                Undo.RecordObject(stream, "Water Look");
                Set(stream, "_Smoothness", StreamSmoothness, sb); Set(stream, "_Foam_Strength", StreamFoam, sb);
                Set(stream, "_Reflection_Strength", StreamReflection, sb); Set(stream, "_Normal_Strength", StreamNormal, sb);
                Set(stream, "_Water_Trail_Strength", StreamTrail, sb);
                if (stream.HasProperty("_Foam_Color")) { var c = stream.GetColor("_Foam_Color"); if (!Mathf.Approximately(c.a, StreamFoamAlpha)) { sb.AppendLine($"  {stream.name} _Foam_Color alpha {c.a:0.###} -> {StreamFoamAlpha}"); c.a = StreamFoamAlpha; stream.SetColor("_Foam_Color", c); } }
                EditorUtility.SetDirty(stream);
            }
            var river = AssetDatabase.LoadAssetAtPath<Material>(River);
            if (river == null) sb.AppendLine("  missing " + River + " (FiveRivers/Setup/River Hint 20261125 makes it)");
            else
            {
                Undo.RecordObject(river, "Water Look");
                if (river.HasProperty("_Lift") && river.GetColor("_Lift") != RiverLift) { sb.AppendLine($"  {river.name} _Lift {river.GetColor("_Lift")} -> {RiverLift}"); river.SetColor("_Lift", RiverLift); }
                Set(river, "_Strength", RiverStrength, sb); Set(river, "_Edge", RiverEdge, sb);
                EditorUtility.SetDirty(river);
            }
            AssetDatabase.SaveAssets();
            if (sb.Length == 0) sb.AppendLine("  nothing to change");
            Debug.Log("[WaterLook]\n" + sb);
            return sb.ToString();
        }

        static void Set(Material m, string name, float value, StringBuilder sb)
        {
            if (!m.HasProperty(name)) { sb.AppendLine($"  {m.name} has no {name}"); return; }
            float old = m.GetFloat(name);
            if (Mathf.Approximately(old, value)) return;
            m.SetFloat(name, value); sb.AppendLine($"  {m.name} {name} {old:0.###} -> {value:0.###}");
        }
    }
}
