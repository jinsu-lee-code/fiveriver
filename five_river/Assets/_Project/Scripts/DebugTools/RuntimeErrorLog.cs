using System.IO;
using UnityEngine;

namespace FiveRivers.DebugTools
{
    // 플레이 중 오류 · 예외 · 경고를 파일로 남긴다 (에디터: Temp/fiveriver_runtime.txt, 개발 빌드: persistentDataPath).
    // 다른 패키지 로그에 섞여 놓치지 않도록 FiveRivers 코드에서 난 것만 남긴다.
    public sealed class RuntimeErrorLog : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string path;

        void OnEnable()
        {
#if UNITY_EDITOR
            path = "Temp/fiveriver_runtime.txt";
#else
            path = Path.Combine(Application.persistentDataPath, "fiveriver_runtime.txt");
#endif
            File.WriteAllText(path, "");
            Application.logMessageReceived += OnLog;
        }

        void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            bool ours = (stack != null && stack.Contains("FiveRivers")) || message.Contains("FiveRivers") || type == LogType.Exception;
            if (!ours) return;
            File.AppendAllText(path, $"[{Time.time:0.0}] {type}: {message}\n{stack}\n");
        }
#endif
    }
}
