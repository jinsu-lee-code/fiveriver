using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace FiveRivers.EditorTools
{
    // FiveRivers 어셈블리의 컴파일 오류 · 경고를 Temp/fiveriver_compile.txt에 남긴다 (MCP · 자동 확인용).
    // 콘솔에는 다른 패키지 로그가 섞여 컴파일 결과를 놓치기 쉽다.
    [InitializeOnLoad]
    static class CompileReporter
    {
        const string ResultPath = "Temp/fiveriver_compile.txt";

        static CompileReporter()
        {
            CompilationPipeline.compilationStarted += _ => File.WriteAllText(ResultPath, "compiling\n");
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.compilationFinished += _ => File.AppendAllText(ResultPath, "done\n");
        }

        static void OnAssemblyFinished(string assembly, CompilerMessage[] messages)
        {
            if (!Path.GetFileName(assembly).StartsWith("FiveRivers")) return;
            var lines = messages
                .Where(m => m.type == CompilerMessageType.Error || m.type == CompilerMessageType.Warning)
                .Select(m => $"{m.type}: {m.message}");
            File.AppendAllLines(ResultPath, lines);
        }
    }
}
