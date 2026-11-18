Shader "FiveRivers/Ridge Backdrop"
{
    // 먼 산 띠 (2026-11-18). 빛을 받지 않는다: 조명탄 · 포구 화염에 번쩍이지 않게 색은 재질과
    // 안개색 · 주광(달 → 새벽 해)에서만 나온다. 장면 안개(150 m 밖은 단색)를 쓰지 않고, 띠마다 정한
    // 만큼만 옅어지며 아래쪽은 장면 안개색에 잠긴다. 밤 → 새벽은 전역 _RidgeDawn (RidgeBackdrop.cs).
    Properties
    {
        _MainTex("Band (rgb slope, a crest outline)",2D)="grey"{}
        _NightColor("Night colour",Color)=(.02,.03,.05,1)
        _NightContrast("Night surface contrast",Range(0,1))=.35
        _NightHaze("Night far colour (rgb), amount (a)",Color)=(.02,.03,.06,.3)
        _DawnColor("Dawn colour",Color)=(.4,.4,.42,1)
        _DawnSaturation("Dawn surface colour",Range(0,1))=.5
        _DawnContrast("Dawn surface contrast",Range(0,1))=.7
        _DawnHaze("Dawn haze towards the fog colour",Range(0,1))=.5
        _Shade("Shading from the main light",Range(0,1))=.3
        _SunGain("Dawn sun on the facing slopes",Range(0,2))=.3
        _FadeLow("Fog covers below (0..1 of the slope)",Range(0,1))=.08
        _FadeHigh("Fog clears above (0..1 of the slope)",Range(0,1))=.6
    }
    SubShader
    {
        // After the opaque world and the sky, before particles and other transparents. The slope writes depth.
        Tags {"Queue"="Transparent-400" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off ZWrite On Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _NightColor,_NightHaze,_DawnColor;
            half _NightContrast,_DawnSaturation,_DawnContrast,_DawnHaze,_Shade,_SunGain,_FadeLow,_FadeHigh;
            float _RidgeDawn;
            float4 _MainLightPosition;
            half4 _MainLightColor;
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct v2f {float4 position:SV_POSITION;float2 uv:TEXCOORD0;half2 light:TEXCOORD1;};
            v2f vert(appdata v)
            {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                // never cut by the far plane, wherever the camera stands
                #if defined(UNITY_REVERSED_Z)
                o.position.z=max(o.position.z,o.position.w*1e-6);
                #else
                o.position.z=min(o.position.z,o.position.w*(1-1e-6));
                #endif
                o.uv=v.uv*_MainTex_ST.xy+_MainTex_ST.zw;
                float ndl=dot(UnityObjectToWorldNormal(v.normal),normalize(_MainLightPosition.xyz));
                o.light=half2(ndl*.5+.5,saturate(ndl));
                return o;
            }
            half4 frag(v2f i):SV_Target
            {
                half4 t=tex2D(_MainTex,i.uv);
                clip(t.a-.03);
                half d=saturate(_RidgeDawn);
                half lum=dot(t.rgb,half3(.3,.5,.2));
                half rel=lum/.36;                               // 1 = the band's average
                half3 night=_NightColor.rgb*lerp(1,rel,_NightContrast);
                half3 surface=lerp(lum.xxx,t.rgb,_DawnSaturation)/.36;
                half3 dawn=_DawnColor.rgb*lerp(half3(1,1,1),surface,_DawnContrast);
                half shade=lerp(1-_Shade,1,i.light.x);
                half3 c=lerp(night,dawn,d)*shade;
                c+=t.rgb*_MainLightColor.rgb*(i.light.y*_SunGain*d);
                // further bands sit nearer the sky's own colour
                c=lerp(c,lerp(_NightHaze.rgb,unity_FogColor.rgb,d),lerp(_NightHaze.a,_DawnHaze,d));
                // the foot of every ridge stands in the valley haze
                half slope=saturate(i.uv.y/.75);
                c=lerp(unity_FogColor.rgb,c,smoothstep(_FadeLow,_FadeHigh,slope));
                return half4(c,t.a);
            }
            ENDCG
        }
    }
}
