Shader "FiveRivers/Distant Slope Detail"
{
    // 안개에 잠긴 구릉의 표면 (2026-11-18). 맵을 두른 구릉은 100 m 밖부터 장면 안개에 잠겨 단색 면으로 보였다.
    // 화면 깊이에서 그 땅의 위치와 기울기를 되찾아, 안개에 잠긴 만큼만 수목 얼룩과 빛을 등진 사면의 음영을
    // 곱해 준다(어둡게만). 가까운 땅과 하늘은 건드리지 않는다. 빛을 받지 않으므로 조명탄 · 포구 화염에 변하지 않는다.
    // 카메라를 감싼 상자 하나(삼각형 12개)로 그린다.
    Properties
    {
        [NoScaleOffset] _MainTex("Tree cover from above (r)",2D)="black"{}
        _Tile("Metres per tile",Float)=80
        _Patch("Tree cover darkening",Range(0,1))=.18
        _Shade("Unlit slope darkening",Range(0,1))=.14
        _Start("Starts at this much fog (0..1)",Range(0,1))=.8
        _HeightBias("More cover higher up (per metre)",Float)=.002
    }
    SubShader
    {
        Tags {"Queue"="Transparent-410" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off ZWrite Off ZTest Always Blend DstColor Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float _Tile,_HeightBias;
            half _Patch,_Shade,_Start;
            float4 _MainLightPosition;
            struct appdata {float4 vertex:POSITION;};
            struct v2f {float4 position:SV_POSITION;float4 screen:TEXCOORD0;float3 ray:TEXCOORD1;};
            v2f vert(appdata v)
            {
                v2f o;
                o.position=UnityObjectToClipPos(v.vertex);
                #if defined(UNITY_REVERSED_Z)
                o.position.z=max(o.position.z,o.position.w*1e-6);
                #else
                o.position.z=min(o.position.z,o.position.w*(1-1e-6));
                #endif
                o.screen=ComputeScreenPos(o.position);
                o.ray=mul(unity_ObjectToWorld,v.vertex).xyz-_WorldSpaceCameraPos;
                return o;
            }
            half4 frag(v2f i):SV_Target
            {
                float raw=SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen));
                float eye=LinearEyeDepth(raw);
                if(eye>_ProjectionParams.z*.97)return half4(1,1,1,1);          // sky
                float3 forward=-UNITY_MATRIX_V[2].xyz;
                float3 world=_WorldSpaceCameraPos+i.ray*(eye/dot(i.ray,forward));
                float dist=length(world-_WorldSpaceCameraPos);
                // exp2 fog, as the scene uses: how much of this ground the fog has already taken
                float f=unity_FogParams.x*dist;
                float fog=1-exp2(-f*f);
                half w=smoothstep(_Start,.985,fog);
                if(w<=0)return half4(1,1,1,1);
                // tree cover seen from the side: bearing round the map centre and height, so the clumps keep their
                // shape on a slope seen at a grazing angle (a sheet laid flat on the ground smeared into stripes)
                float3 rel=world-unity_ObjectToWorld._m03_m13_m23;
                float2 p=float2(atan2(rel.x,rel.z)*200,world.y*1.6)/_Tile;
                float2 q=float2(p.x*.8+p.y*.6,-p.x*.6+p.y*.8)*2.83+.37;
                half cover=tex2D(_MainTex,p).r*.65+tex2D(_MainTex,q).r*.35;
                half patch=smoothstep(.36,.62,cover+world.y*_HeightBias);
                // slope facing from the depth itself; nothing across a silhouette edge, nothing on level ground
                float3 n=cross(ddy(world),ddx(world));
                n=normalize(n*(n.y<0?-1:1));
                half edge=saturate(1.5-fwidth(eye)/(eye*.10))*smoothstep(.992,.955,n.y);
                half away=saturate(.45-dot(n,normalize(_MainLightPosition.xyz))*1.1)*edge;
                half dark=saturate(_Patch*patch*edge+_Shade*away)*w;
                return half4(1-dark,1-dark,1-dark,1);
            }
            ENDCG
        }
    }
}
