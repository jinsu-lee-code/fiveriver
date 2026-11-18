Shader "FiveRivers/Distant Smoke"
{
    // 먼 연기 기둥 (2026-11-18). 능선 너머 어딘가가 타고 있다. 밤에는 하늘보다 조금 어두운 실루엣,
    // 새벽에는 뚜렷한 잿빛. 빛을 받지 않고(조명탄 · 포구 화염에 번쩍이지 않게) 장면 안개도 쓰지 않는다
    // (150 m 밖은 안개에 다 잠긴다). 밤 → 새벽은 먼 산과 같은 전역 _RidgeDawn. 아래쪽은 골짜기의 안개색에 잠긴다.
    // 파티클의 정점 색 알파가 짙기, 정점 색 밝기가 덩이마다의 농담이다.
    Properties
    {
        _MainTex("Smoke puff",2D)="white"{}
        _NightColor("Night colour",Color)=(.03,.04,.075,1)
        _DawnColor("Dawn colour",Color)=(.3,.3,.33,1)
        _Strength("Opacity at dawn",Range(0,1))=.6
        _NightStrength("Opacity at night",Range(0,1))=.4
        _FootHeight("Fades into the haze below this height (m)",Float)=25
        _FootBand("... over this many metres",Float)=45
    }
    SubShader
    {
        Tags {"Queue"="Transparent-350" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half4 _NightColor,_DawnColor;
            half _Strength,_NightStrength;
            float _FootHeight,_FootBand,_RidgeDawn;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;float height:TEXCOORD1;};
            v2f vert(appdata v)
            {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                // it may stand beyond the far plane
                #if defined(UNITY_REVERSED_Z)
                o.position.z=max(o.position.z,o.position.w*2e-6);
                #else
                o.position.z=min(o.position.z,o.position.w*(1-2e-6));
                #endif
                o.uv=v.uv;
                o.color=v.color;
                o.height=mul(unity_ObjectToWorld,v.vertex).y;
                return o;
            }
            half4 frag(v2f i):SV_Target
            {
                half d=saturate(_RidgeDawn);
                half a=tex2D(_MainTex,i.uv).a*i.color.a*lerp(_NightStrength,_Strength,d);
                half3 c=lerp(_NightColor.rgb,_DawnColor.rgb,d)*lerp(.9,1.1,i.color.r);
                half foot=saturate((i.height-_FootHeight)/_FootBand);
                c=lerp(lerp(c,unity_FogColor.rgb,.6),c,foot);
                a*=lerp(.25,1,foot);
                return half4(c,a);
            }
            ENDCG
        }
    }
}
