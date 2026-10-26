using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // EditMode 테스트를 돌리고 결과를 Temp/fiveriver_tests.txt에 남긴다 (MCP · 자동 확인용).
    static class DevTestRunner
    {
        public const string ResultPath = "Temp/fiveriver_tests.txt";

        [MenuItem("FiveRivers/Dev/Run EditMode Tests")]
        public static void Run()
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callback());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { "FiveRivers.Tests.EditMode" },
            }));
        }

        class Callback : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"pass={result.PassCount} fail={result.FailCount} skip={result.SkipCount} status={result.TestStatus}");
                Collect(result, sb);
                File.WriteAllText(ResultPath, sb.ToString());
                Debug.Log($"[Tests] 통과 {result.PassCount} · 실패 {result.FailCount}");
            }

            static void Collect(ITestResultAdaptor r, StringBuilder sb)
            {
                if (!r.HasChildren)
                {
                    if (r.TestStatus != TestStatus.Passed) sb.AppendLine($"{r.TestStatus} {r.FullName} {r.Message}");
                    return;
                }
                foreach (var c in r.Children) Collect(c, sb);
            }
        }
    }
}
