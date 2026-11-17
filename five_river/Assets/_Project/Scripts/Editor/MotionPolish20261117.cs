using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 동작 자연스러움 2차 점검 (2026-11-17). 무릎 자세 ↔ 선 자세, 달리다 무릎 꿇기 전환이 모두 0.15초라
    // 연속 촬영에서 4프레임 만에 벌떡 일어나고 털썩 앉았다. 사람이 일어나고 앉는 데 걸리는 시간(0.3~0.4초)으로 늘린다.
    // 전환 조건 · 상태 · 클립은 건드리지 않는다. 여러 번 실행해도 결과가 같다. 기록: Temp/motion_polish.txt
    public static class MotionPolish20261117
    {
        const string Kpa = "Assets/_Project/Art/Animations/AC_KPA.controller";
        const string Rok = "Assets/_Project/Art/Animations/AC_ROK.controller";
        const string Park = "Assets/_Project/Art/CharacterVariants/ParkCombatFootPlacement20261106/AC_ROK_ParkCombatFootIK.controller";

        // controller, from state, to state, seconds
        static readonly (string controller, string from, string to, float seconds)[] Durations =
        {
            (Kpa, "Crouch_Aim", "Idle_Aim", 0.36f),   // 엄폐에서 일어나 쏘기 (사격까지 0.7초 남는다)
            (Kpa, "Idle_Aim", "Crouch_Aim", 0.32f),   // 쏘고 다시 앉기
            (Kpa, "CrouchRun", "Crouch_Aim", 0.26f),  // 달려와 엄폐에 무릎 꿇기
            (Kpa, "Crouch_Aim", "CrouchRun", 0.22f),  // 엄폐에서 다시 출발
            (Rok, "Idle_Trench", "Idle_Stand", 0.38f),
            (Rok, "Idle_Stand", "Idle_Trench", 0.34f),
            (Park, "Idle_Trench", "Idle_Stand", 0.38f),
            (Park, "Idle_Stand", "Idle_Trench", 0.34f),
        };

        [MenuItem("FiveRivers/Animation/Motion Polish 20261117 (Kneel-Stand Transitions)")]
        public static string Apply()
        {
            var report = new StringBuilder();
            int changed = 0;
            foreach (var (path, from, to, seconds) in Durations)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null) { report.AppendLine($"missing controller {path}"); continue; }
                bool found = false;
                foreach (var child in controller.layers[0].stateMachine.states)
                {
                    if (child.state.name != from) continue;
                    foreach (var t in child.state.transitions)
                    {
                        if (t.destinationState == null || t.destinationState.name != to) continue;
                        found = true;
                        float before = t.duration;
                        if (!t.hasFixedDuration || !Mathf.Approximately(before, seconds))
                        {
                            t.hasFixedDuration = true;
                            t.duration = seconds;
                            EditorUtility.SetDirty(t);
                            EditorUtility.SetDirty(controller);
                            changed++;
                        }
                        report.AppendLine($"{Path.GetFileNameWithoutExtension(path)} {from} -> {to}: {before:0.00} -> {seconds:0.00}");
                    }
                }
                if (!found) report.AppendLine($"{Path.GetFileNameWithoutExtension(path)} {from} -> {to}: transition not found");
            }
            if (changed > 0) AssetDatabase.SaveAssets();
            report.AppendLine($"changed {changed}");
            File.WriteAllText("Temp/motion_polish.txt", report.ToString());
            Debug.Log("[Motion polish] " + report);
            return $"changed {changed}";
        }
    }
}
