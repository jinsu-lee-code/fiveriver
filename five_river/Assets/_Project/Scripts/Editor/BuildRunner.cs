using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Windows 실행 빌드. 결과 요약(성공 · 크기 · 시간 · 오류 · 경고)을 Temp/fiveriver_build.txt에 남긴다.
    // 출력: five_river/Builds/ (git 무시). 개발 빌드는 디버그 창(F1) · 점검 모드(-fiverivers-smoke)가 켜진다.
    static class BuildRunner
    {
        const string SummaryPath = "Temp/fiveriver_build.txt";

        [MenuItem("FiveRivers/Build/Windows Development")]
        public static void BuildDevelopment() => Build(true);

        [MenuItem("FiveRivers/Build/Windows Release")]
        public static void BuildRelease() => Build(false);

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "FiveRivers";
            PlayerSettings.productName = "FiveRivers";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
        }

        static void Build(bool development)
        {
            if (File.Exists(SummaryPath)) File.Delete(SummaryPath);
            ApplyPlayerSettings();
            string dir = development ? "Builds/WindowsDev" : "Builds/Windows";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { DabudongSetup.ScenePath },
                locationPathName = dir + "/FiveRivers.exe",
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"result={s.result} development={development}");
            sb.AppendLine($"output={Path.GetFullPath(dir)}");
            sb.AppendLine($"size={s.totalSize / (1024f * 1024f):0.0}MB time={s.totalTime.TotalSeconds:0}s errors={s.totalErrors} warnings={s.totalWarnings}");
            foreach (var m in report.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                sb.AppendLine("error: " + m.content);
            File.WriteAllText(SummaryPath, sb.ToString());
            Debug.Log("[Build] " + sb);
        }
    }
}
