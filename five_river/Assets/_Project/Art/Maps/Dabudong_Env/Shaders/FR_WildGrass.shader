// 들풀 (2026-11-25, 사용자 지시 "풀은가져오지말고 느낌만 빼와서 기존에 있는거에 이식해"): URP 3D 샘플 오아시스 풀의 느낌을
// 기존 풀(ENV_grass_*)에 옮긴 셰이더. 알파로 오린 풀잎 카드, 양면, 뿌리 쪽 어둡게(포기 속 그늘), 바람에 끝만 흔들림(높이 제곱),
// 빛을 등지면 잎이 비쳐 보임(역광 투과), 포기마다 색이 조금씩 다름(월드 위치 해시). 깊이 · 깊이노멀 · 그림자 패스도 같은 바람과
// 오림을 써서 SSAO · 그림자가 어긋나지 않는다. LOD 교차 페이드 지원(거리별 그리기).
Shader "FiveRivers/WildGrass"
{
    Properties
    {
        _BaseMap ("Blades (RGBA)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.45
        _Height ("Clump Height (m)", Float) = 0.45
        _RootDarken ("Root Darkening", Range(0,1)) = 0.55
        _DryVariation ("Per-clump Dry Variation", Range(0,1)) = 0.35
        _DryTint ("Dry Tint", Color) = (0.78, 0.68, 0.46, 1)
        _Translucency ("Back-light Translucency", Range(0,2)) = 0.6
        _WindStrength ("Wind Strength (m at tip)", Range(0,0.3)) = 0.06
        _WindSpeed ("Wind Speed", Range(0,5)) = 1.3
        _WindDirection ("Wind Direction (xz)", Vector) = (0.8, 0, 0.6, 0)
        _Smoothness ("Smoothness", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "DisableBatching"="False" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _DryTint;
        half _Cutoff, _Height, _RootDarken, _DryVariation, _Translucency, _Smoothness;
        float _WindStrength, _WindSpeed;
        float4 _WindDirection;
        CBUFFER_END

        float GrassHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

        // the clump's origin in the world (per object), the blade's height share (0 root .. 1 tip)
        float3 WindOffsetWS(float3 positionOS, float3 positionWS)
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            float h = saturate(positionOS.y / max(_Height, 0.01));
            float t = _Time.y * _WindSpeed;
            float phase = dot(origin.xz, float2(0.37, 0.21)) + positionWS.y * 0.5;
            float gust = sin(t * 0.37 + origin.x * 0.05) * 0.5 + 0.5;                 // slow gusts across the field
            float sway = sin(t + phase) * 0.65 + sin(t * 2.31 + phase * 1.7) * 0.25 + sin(t * 5.1 + positionWS.x * 3.1 + positionWS.z * 2.3) * 0.10;
            float2 dir = normalize(_WindDirection.xz + 1e-4);
            float amount = _WindStrength * h * h * (0.55 + 0.45 * gust) * sway;
            return float3(dir.x * amount, -abs(amount) * 0.25, dir.y * amount);   // a bent blade also dips a little
        }

        struct GrassAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

        float3 GrassPositionWS(GrassAttributes v)
        {
            float3 ws = TransformObjectToWorld(v.positionOS.xyz);
            return ws + WindOffsetWS(v.positionOS.xyz, ws);
        }

        void GrassClip(float2 uv, float4 positionCS)
        {
            half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            clip(a - _Cutoff);
            #ifdef LOD_FADE_CROSSFADE
            LODFadeCrossFade(positionCS);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2;
                half h : TEXCOORD3; half dry : TEXCOORD4; half fog : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(GrassAttributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 ws = GrassPositionWS(v);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.h = saturate(v.positionOS.y / max(_Height, 0.01));
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                o.dry = GrassHash(floor(origin.xz * 3.0));
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
                #endif
                half3 albedo = tex.rgb * _BaseColor.rgb;
                // some clumps further into the dry season than others
                albedo = lerp(albedo, albedo * _DryTint.rgb * 1.35, saturate(i.dry * _DryVariation * 1.6 - 0.2));
                // the inside of a clump is in its own shade
                albedo *= lerp(1.0 - _RootDarken, 1.0, smoothstep(0.0, 0.55, i.h));

                half3 n = normalize(i.normalWS);
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;   // soft, clump-shaped normals (up and out), the same from both sides
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.fogCoord = i.fog;

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo; s.alpha = 1; s.metallic = 0; s.specular = half3(0.04, 0.04, 0.04);
                s.smoothness = _Smoothness; s.occlusion = lerp(0.6, 1.0, i.h); s.normalTS = half3(0, 0, 1);
                half4 color = UniversalFragmentPBR(inputData, s);

                // light through the blade when it is between the eye and the light (dawn sun behind, a flare above)
                Light main = GetMainLight(inputData.shadowCoord, i.positionWS, inputData.shadowMask);
                half back = pow(saturate(dot(inputData.viewDirectionWS, -main.direction)), 4.0);
                color.rgb += albedo * main.color * main.shadowAttenuation * back * _Translucency * (0.4 + 0.6 * i.h);

                color.rgb = MixFog(color.rgb, i.fog);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex VertShadow
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V VertShadow(GrassAttributes v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                float3 ws = GrassPositionWS(v);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - ws);
                #else
                float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, ld));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs; o.uv = TRANSFORM_TEX(v.uv, _BaseMap); return o;
            }
            half4 FragDepth(V i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); GrassClip(i.uv, i.positionCS); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V VertDepth(GrassAttributes v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); o.positionCS = TransformWorldToHClip(GrassPositionWS(v)); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); return o; }
            half FragDepth(V i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); GrassClip(i.uv, i.positionCS); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex VertDN
            #pragma fragment FragDN
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V VertDN(GrassAttributes v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); o.positionCS = TransformWorldToHClip(GrassPositionWS(v)); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 FragDN(V i) : SV_Target { UNITY_SETUP_INSTANCE_ID(i); GrassClip(i.uv, i.positionCS); return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
