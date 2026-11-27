using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    // 2026-11-27: the terrain's detail grass billboards are drawn with the shader URP's global settings name for them
    // (UniversalRenderPipelineRuntimeTerrainShaders.terrainDetailGrassBillboardShader, kept in
    // Assets/Settings/UniversalRenderPipelineGlobalSettings.asset). This menu points it at the project's copy
    // FR_TerrainGrassBillboard (indirect light from the probe volume, root shade, tip direct light; the rest as URP's
    // WavingGrassBillboard), and the second menu points it back at URP's own. Both are idempotent; only that one
    // reference in the global settings asset changes. Not touched: TerrainGrassLook20261127 (the grass colours, the
    // rooted textures, the waving tint in the terrain data).
    // Tied to URP 17.3.0: the copy includes URP's WavingGrassInput / WavingGrassPasses as they are in this version.
    public static class TerrainGrassShader20261127
    {
        public const string ShaderPath = "Assets/_Project/Art/Maps/Dabudong_Env/Shaders/FR_TerrainGrassBillboard.shader";
        public const string UrpShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassBillboard.shader";

        [MenuItem("FiveRivers/Setup/Terrain Grass Billboard Shader 20261127")]
        public static string Apply() => Point(ShaderPath);

        [MenuItem("FiveRivers/Setup/Terrain Grass Billboard Shader 20261127 (URP original)")]
        public static string Revert() => Point(UrpShaderPath);

        static string Point(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) throw new System.InvalidOperationException("셰이더가 없음: " + path);
            if (!shader.isSupported) Debug.LogWarning("[TerrainGrassShader] 이 셰이더가 이 기기에서 지원되지 않음(컴파일 오류?): " + path);
            if (!GraphicsSettings.TryGetRenderPipelineSettings<UniversalRenderPipelineRuntimeTerrainShaders>(out var terrain))
                throw new System.InvalidOperationException("URP 전역 설정에 지형 셰이더 항목이 없음 (UniversalRenderPipelineRuntimeTerrainShaders)");
            string before = terrain.terrainDetailGrassBillboardShader != null ? AssetDatabase.GetAssetPath(terrain.terrainDetailGrassBillboardShader) : "(none)";
            string report;
            if (terrain.terrainDetailGrassBillboardShader == shader) report = $"unchanged: {path}";
            else
            {
                terrain.terrainDetailGrassBillboardShader = shader;
                var settings = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
                if (settings != null) EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                report = $"{before} -> {path}";
            }
            // the terrains build their detail materials from it: have them pick it up now
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)) t.Flush();
            Debug.Log("[TerrainGrassShader] " + report);
            return report;
        }
    }
}
