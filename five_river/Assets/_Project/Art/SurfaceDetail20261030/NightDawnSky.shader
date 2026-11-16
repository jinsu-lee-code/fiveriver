Shader "FiveRivers/Night Dawn Sky"
{
    Properties
    {
        [NoScaleOffset] _Night("Night panorama",2D)="grey"{}
        [NoScaleOffset] _Dawn("Dawn panorama",2D)="grey"{}
        _NightExposure("Night exposure",Float)=.06
        _NightTint("Night tint",Color)=(.55,.68,1,1)
        _DawnExposure("Dawn exposure",Float)=.7
        _Rotation("Rotation",Range(0,360))=0
        _DawnBlend("Dawn blend",Range(0,1))=0
        // 2026-11-16 art pass: what the night panorama lacks at a game exposure (all default to off)
        _HorizonGlow("Night horizon glow (rgb, a=height falloff)",Color)=(0,0,0,6)
        _StarBrightness("Star brightness",Float)=0
        _StarDensity("Star density (0..1)",Range(0,1))=.12
        _MoonDir("Moon direction (world, towards the moon)",Vector)=(0,.5,1,0)
        _MoonGlow("Moon glow (rgb, a=tightness)",Color)=(0,0,0,24)
        _SunDir("Dawn sun direction (world, towards the sun)",Vector)=(0,.1,1,0)
        _SunGlow("Dawn sun glow (rgb, a=tightness)",Color)=(0,0,0,6)
        _DawnTint("Dawn tint",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _Night,_Dawn;
            half4 _Night_HDR,_Dawn_HDR,_NightTint,_HorizonGlow,_MoonGlow,_SunGlow,_DawnTint;
            float4 _MoonDir,_SunDir;
            float _NightExposure,_DawnExposure,_Rotation,_DawnBlend,_StarBrightness,_StarDensity;
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 position:SV_POSITION;float3 direction:TEXCOORD0;};
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;
            }
            float Hash(float3 p)
            {
                p=frac(p*float3(.1031,.1030,.0973));
                p+=dot(p,p.yxz+33.33);
                return frac((p.x+p.y)*p.z);
            }
            // point stars on a direction grid: one candidate per cell, a soft dot, size and colour from the hash
            half3 Stars(float3 d)
            {
                const float cells=220;
                float3 g=d*cells;
                float3 cell=floor(g);
                float h=Hash(cell);
                if(h>_StarDensity)return 0;
                float3 centre=cell+.5+(float3(Hash(cell+1.7),Hash(cell+3.1),Hash(cell+5.3))-.5)*.6;
                float dist=length(g-centre);
                float m=h/_StarDensity;               // 0..1 inside the kept cells
                float size=lerp(.20,.38,m*m); // about 1-1.7 px at 1080p / 60 degrees, so they do not shimmer
                float s=saturate(1-dist/size);
                s*=s;
                half3 tint=lerp(half3(.75,.85,1),half3(1,.92,.8),Hash(cell+9.9));
                return tint*s*lerp(.25,1,m*m*m);
            }
            half4 frag(v2f i):SV_Target
            {
                float3 direction=normalize(i.direction);
                float2 uv=float2(.5-atan2(direction.z,direction.x)/(2*UNITY_PI)+_Rotation/360,acos(clamp(direction.y,-1,1))/UNITY_PI);
                // 2026-11-13: where atan2 wraps (u jumps by 1 across one pixel) the screen derivatives were huge, the
                // GPU picked the smallest mip and drew a dotted vertical line up the sky. Take the wrap out of the
                // derivatives so that column samples the same mip as its neighbours.
                float2 dx=ddx(uv),dy=ddy(uv);
                dx.x-=round(dx.x);dy.x-=round(dy.x);
                half3 night=DecodeHDR(tex2Dgrad(_Night,uv,dx,dy),_Night_HDR)*_NightExposure*_NightTint.rgb;
                half3 dawn=DecodeHDR(tex2Dgrad(_Dawn,uv,dx,dy),_Dawn_HDR)*_DawnExposure*_DawnTint.rgb;

                float up=saturate(direction.y);
                // night: a pale band above the ridges so they read as silhouettes, a glow round the moon, stars higher up
                night+=_HorizonGlow.rgb*exp(-up*_HorizonGlow.a);
                float3 moon=normalize(_MoonDir.xyz);
                float mc=saturate(dot(direction,moon));
                night+=_MoonGlow.rgb*(pow(mc,_MoonGlow.a)+.25*pow(mc,_MoonGlow.a*.08));
                night+=Stars(direction)*_StarBrightness*smoothstep(.04,.30,direction.y)*(1-.8*pow(mc,8));
                // dawn: the sky warms towards the sun's side and near the horizon
                float3 sun=normalize(_SunDir.xyz);
                float sc=saturate(dot(direction,sun));
                dawn+=_SunGlow.rgb*(pow(sc,_SunGlow.a)*exp(-up*3)+.35*pow(sc,_SunGlow.a*6));

                return half4(lerp(night,dawn,_DawnBlend),1);
            }
            ENDCG
        }
    }
}
