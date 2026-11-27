// 플립북 폭발을 "밝기만" 그리는 입자 셰이더 (2026-11-27, 아트 검수 "폭발 · 연기 · 피격 효과가 밤 화면과 따로 놂").
// Explosion FREE 1 의 그림은 순수한 주황-빨강 불꽃이라 밤 그레이딩(채도 -60)에서 분홍 솜사탕이 됐다. 곱하는 색으로는 못 고친다:
// 빨강이 먼저 잘려 밝은 곳은 분홍, 덜 밝은 곳은 녹색이 이겨 녹색 구름이 된다(vfx/final_hit2_eyes_0.10.png). 그래서 그림의
// 밝기(채널 최댓값)만 쓰고 색은 전부 입자 색(수명에 따른 색 곡선) × _BaseColor 가 정한다 — 어느 밝기에서도 같은 색조.
// 플립북 프레임 섞기 · 부드러운 입자 · 카메라 가까이 사라짐은 원래 재질(URP Particles/Unlit)과 같게 둔다(왜곡은 뺌).
// 정점 스트림: Position, Color, UV, UV2, AnimBlend. 값은 메뉴 FiveRivers/Setup/Battle VFX Look 20261127 (BALANCE 4-7).
Shader "FiveRivers/ParticleFlipbookValue"
{
    Properties
    {
        _BaseMap ("Flip-book sheet", 2D) = "white" {}
        [HDR] _BaseColor ("Tint (x particle colour)", Color) = (1, 1, 1, 1)
        _KeepHue ("Sheet's own colour kept (0 = brightness only)", Range(0,1)) = 0
        _SmokeColor ("Colour of the sheet's black smoke (not multiplied by the particle colour)", Color) = (0.075, 0.07, 0.065, 1)
        _SoftFade ("Soft particle fade (m)", Float) = 0.5
        _CameraNear ("Camera fade from (m)", Float) = 1
        _CameraFar ("Camera fade to (m)", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-1" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor, _SmokeColor; half _KeepHue; float _SoftFade, _CameraNear, _CameraFar;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; float blend : TEXCOORD1; };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; float3 blendEyeFog : TEXCOORD1; };
            V Vert(A v)
            {
                V o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.color = v.color;
                o.uv = v.uv;
                o.blendEyeFog = float3(v.blend, -TransformWorldToView(ws).z, ComputeFogFactor(o.positionCS.z));
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                half4 c = lerp(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.xy), SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.zw), saturate(i.blendEyeFog.x));
                // the sheet's smoke is all but black: unlit, a black hole in the night picture. Where the sheet is dark it is drawn in
                // _SmokeColor instead, whatever the particle colour (which puts the fire out within a tenth of a second).
                half value = max(c.r, max(c.g, c.b));
                half3 rgb = lerp(half3(value, value, value), c.rgb, _KeepHue) * _BaseColor.rgb * i.color.rgb;
                rgb += (1.0 - value) * _SmokeColor.rgb;
                half alpha = c.a * _BaseColor.a * i.color.a;
                float scene = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)), _ZBufferParams);
                alpha *= saturate((scene - i.blendEyeFog.y) / max(_SoftFade, 0.001));
                alpha *= saturate((i.blendEyeFog.y - _CameraNear) / max(_CameraFar - _CameraNear, 0.001));
                rgb = MixFog(rgb, i.blendEyeFog.z);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
