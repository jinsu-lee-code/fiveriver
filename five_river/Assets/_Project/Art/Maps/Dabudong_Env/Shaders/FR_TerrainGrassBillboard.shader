// 지형 풀 빌보드 (2026-11-27). URP 17.3.0 Shaders/Terrain/WavingGrassBillboard.shader (Copyright (c) 2016 Unity Technologies,
// MIT license) 의 사본. 바꾼 것: 앞쪽 · GBuffer 패스가 프로브 볼륨(APV) 변형을 받고(ProbeVolumeVariants + EVALUATE_SH_*),
// 조각 셰이더가 FRLitPassFragmentGrass(FR_TerrainGrassLighting.hlsl: 간접광 · 뿌리 그늘 · 끝 직접광)다. 정점 · 바람결 ·
// 거리 페이드 · 깊이 패스는 URP 원본 hlsl 을 그대로 include 한다 (URP 버전에 묶임 — 업그레이드 때 원본과 다시 맞출 것).
// 지형이 이 셰이더를 쓰게 하는 연결: 메뉴 FiveRivers/Setup/Terrain Grass Billboard Shader 20261127.
Shader "Hidden/FiveRivers/TerrainGrassBillboard"
{
    Properties
    {
        _WavingTint ("Fade Color", Color) = (.7,.6,.5, 0)
        _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
        _WaveAndDistance ("Wave and distance", Vector) = (12, 3.6, 1, 1)
        _Cutoff ("Cutoff", float) = 0.5
    }
    SubShader
    {
        Tags {"Queue" = "Geometry+200" "RenderType" = "GrassBillBoard" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "SimpleLit" "DisableBatching" = "True" }
        Cull Off
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex WavingGrassBillboardVert
            #pragma fragment FRLitPassFragmentGrass
            #define _ALPHATEST_ON

            #if USE_DYNAMIC_BRANCH_FOG_KEYWORD && SHADER_API_VULKAN && SHADER_API_MOBILE
            #define SKIP_SHADOWS_LIGHT_INDEX_CHECK 1
            #endif

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassPasses.hlsl"
            #include "FR_TerrainGrassLighting.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            HLSLPROGRAM
            #pragma target 4.5

            // Deferred Rendering Path does not support the OpenGL-based graphics API:
            // Desktop OpenGL, OpenGL ES 3.0, WebGL 2.0.
            #pragma exclude_renderers gles3 glcore

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #pragma vertex WavingGrassBillboardVert
            #pragma fragment FRLitPassFragmentGrass
            #define _ALPHATEST_ON
            #define TERRAIN_GBUFFER

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassPasses.hlsl"
            #include "FR_TerrainGrassLighting.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutputFormat.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthOnlyBillboardVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #define _ALPHATEST_ON
            #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthNormalOnlyBillboardVertex
            #pragma fragment DepthNormalOnlyFragment

            // -------------------------------------
            // Material Keywords
            #define _ALPHATEST_ON
            #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/WavingGrassDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
