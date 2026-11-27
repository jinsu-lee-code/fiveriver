// 먼 낙동강 (2026-11-25, 사용자 지시 "… 다음장을 암시하는 장면 하나 넣어주고"): 새벽 정상에서 350 m 밖 골짜기 바닥의 강은
// 장면 안개(exp2)에 99 % 묻혀 보이지 않았다. 실제 새벽 강은 물안개 너머로 하늘을 비춘 엷은 띠로 읽힌다 — 그 모습만 그린다.
// 2026-11-27 (아트 검수 "원경의 강이 순백 줄무늬 띠로 빛남"): 처음에는 정해 둔 밝은 하늘색(0.86)을 그대로 칠해서 밤에도
// 8비트 170 안팎의 흰 띠였고(둘레 땅은 7), 강 방향 사인 물결이 줄무늬로 보였다. 이제 뒤에 있는 것(안개 낀 골짜기)을
// _Lift 만큼만 밝힌다(Blend DstColor One: 결과 = 뒤 × (1 + _Lift × 몸통)). 그래서 밤에는 어둡고 새벽에는 안개보다 조금 밝은
// 푸른빛 띠가 된다. 줄무늬 · 해 반짝임은 뺐다. 값은 메뉴 FiveRivers/Setup/Water Look 20261127 (BALANCE 4-3).
Shader "FiveRivers/DistantRiver"
{
    Properties
    {
        _Lift ("Lift over what lies behind (linear, per channel)", Color) = (0.26, 0.30, 0.38, 1)
        _Strength ("Strength (0..1)", Range(0,1)) = 0.85
        _Edge ("Soft edge (share of the width)", Range(0.05,0.5)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-420" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend DstColor One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Lift; half _Strength, _Edge;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V Vert(A v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            float Hash(float2 p) { return frac(sin(dot(p, float2(41.3, 289.1))) * 9137.7); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }
            half4 Frag(V i) : SV_Target
            {
                // uv.x: -1.25 .. 1.25 across (|x| > 1 are the banks), uv.y: along, 60 m a unit
                half across = abs(i.uv.x);
                half body = 1.0 - smoothstep(1.0 - _Edge, 1.0, across);
                // sandbars: the band is not one even strip
                half bars = smoothstep(0.35, 0.75, Hash(floor(float2(i.uv.y * 3.0, i.uv.x * 2.0 + 7.0)))) * smoothstep(0.55, 0.9, across) * 0.6;
                body *= 1.0 - bars;
                // a slow unevenness of the surface (no stripes): +-12 %
                half uneven = 0.88 + 0.24 * Noise(float2(i.uv.y * 1.3, i.uv.x * 1.7));
                return half4(_Lift.rgb * (body * _Strength * uneven), 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
