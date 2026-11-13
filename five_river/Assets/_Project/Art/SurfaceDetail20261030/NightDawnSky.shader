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
            half4 _Night_HDR,_Dawn_HDR,_NightTint;
            float _NightExposure,_DawnExposure,_Rotation,_DawnBlend;
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 position:SV_POSITION;float3 direction:TEXCOORD0;};
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.direction=v.vertex.xyz;return o;
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
                half3 dawn=DecodeHDR(tex2Dgrad(_Dawn,uv,dx,dy),_Dawn_HDR)*_DawnExposure;
                return half4(lerp(night,dawn,_DawnBlend),1);
            }
            ENDCG
        }
    }
}
