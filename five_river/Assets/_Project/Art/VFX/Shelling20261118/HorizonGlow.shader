Shader "FiveRivers/Horizon Glow"
{
    // 지평선 섬광 · 먼 예광탄 (2026-11-18). 먼 능선 너머의 포격이 하늘 가장자리를 잠깐 밝힌다.
    // 하늘 다음, 먼 산 띠(FiveRivers/Ridge Backdrop, Transparent-400)보다 먼저 그리고 깊이를 쓰지 않으므로
    // 산 띠가 그 위를 덮는다: 빛은 언제나 능선 뒤에 있다. 가까운 땅 · 나무(불투명)는 깊이로 가린다.
    // 더하기 혼합이고 빛 · 안개를 받지 않는다. 밝기와 색은 렌더러마다 MaterialPropertyBlock으로 준다.
    Properties
    {
        _GlowColor("Colour",Color)=(1,.72,.45,1)
        _Glow("Brightness",Float)=0
        _Core("Core size (0..1 of the quad)",Range(.02,1))=.28
        _Halo("Halo strength",Range(0,1))=.35
    }
    SubShader
    {
        Tags {"Queue"="Transparent-405" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off ZWrite Off ZTest LEqual Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            half4 _GlowColor;
            half _Glow,_Core,_Halo;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v)
            {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                // never cut by the far plane: it lies beyond the far ridge
                #if defined(UNITY_REVERSED_Z)
                o.position.z=max(o.position.z,o.position.w*1e-6);
                #else
                o.position.z=min(o.position.z,o.position.w*(1-1e-6));
                #endif
                o.uv=v.uv*2-1;
                return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float r2=dot(i.uv,i.uv);
                half core=exp(-r2/(_Core*_Core));
                half halo=exp(-r2*3.2)*_Halo;
                // nothing at the quad's edge, whatever the sizes
                half edge=saturate((1-r2)*2.5);
                half g=(core+halo)*edge*_Glow;
                return half4(_GlowColor.rgb*g,0);
            }
            ENDCG
        }
    }
}
