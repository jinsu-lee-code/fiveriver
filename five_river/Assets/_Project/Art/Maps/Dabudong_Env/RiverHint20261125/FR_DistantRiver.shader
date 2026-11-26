// 먼 낙동강 (2026-11-25, 사용자 지시 "… 다음장을 암시하는 장면 하나 넣어주고"): 새벽 정상에서 350 m 밖 골짜기 바닥의 강은
// 장면 안개(exp2)에 99 % 묻혀 보이지 않았다. 실제 새벽 강은 물안개 너머로 하늘을 비춘 은빛 띠로 읽힌다 — 그 모습만 그린다:
// 안개 색보다 밝은 하늘빛, 해 쪽으로 반짝임, 가장자리는 부드럽게 흐려 모래톱으로 사라진다. 빛 · 그림자를 받지 않는다.
Shader "FiveRivers/DistantRiver"
{
    Properties
    {
        _Sky ("Mirrored sky", Color) = (0.86, 0.84, 0.88, 1)
        _Glint ("Sun glint", Color) = (1.0, 0.82, 0.62, 1)
        _Strength ("Show through the haze (0..1)", Range(0,1)) = 0.55
        _Edge ("Soft edge (share of the width)", Range(0.05,0.5)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-420" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Sky, _Glint; half _Strength, _Edge;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            V Vert(A v) { V o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.uv = v.uv; return o; }
            float Hash(float2 p) { return frac(sin(dot(p, float2(41.3, 289.1))) * 9137.7); }
            half4 Frag(V i) : SV_Target
            {
                // uv.x: -1.25 .. 1.25 across (|x| > 1 are the banks), uv.y: along, 60 m a unit
                half across = abs(i.uv.x);
                half body = 1.0 - smoothstep(1.0 - _Edge, 1.0, across);
                // sandbars and a broken surface: the band is not one even strip
                half bars = smoothstep(0.35, 0.75, Hash(floor(float2(i.uv.y * 3.0, i.uv.x * 2.0 + 7.0)))) * smoothstep(0.55, 0.9, across) * 0.6;
                body *= 1.0 - bars;
                float3 view = normalize(_WorldSpaceCameraPos - i.positionWS);
                Light sun = GetMainLight();
                float3 refl = reflect(-view, float3(0, 1, 0));
                half glint = pow(saturate(dot(refl, sun.direction)), 24.0);
                half ripple = 0.85 + 0.15 * sin(i.uv.y * 40.0 + _Time.y * 0.6 + i.uv.x * 6.0);
                half3 col = lerp(unity_FogColor.rgb, _Sky.rgb, 0.65) * ripple + _Glint.rgb * sun.color * glint * 0.6;
                return half4(col, body * _Strength);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
