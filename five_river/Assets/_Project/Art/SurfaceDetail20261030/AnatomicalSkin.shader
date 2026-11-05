Shader "FiveRivers/Anatomical Skin"
{
 Properties
 {
  _BaseMap("Reviewed skin albedo",2D)="white"{}
  _BaseColor("Skin tint",Color)=(1,1,1,1)
  [Normal] _BumpMap("Anatomical normals",2D)="bump"{}
  _BumpScale("Normal strength",Range(0,2))=.5
  _MetallicGlossMap("Skin packed mask",2D)="white"{}
  _Smoothness("Smoothness",Range(0,1))=.45
  _OcclusionMap("Occlusion",2D)="white"{}
  _OcclusionStrength("Occlusion strength",Range(0,1))=.6
  [Normal] _DetailNormalMap("Pore normal",2D)="bump"{}
  _DetailNormalMapScale("Pore strength",Range(0,2))=.6
  _DetailAlbedoMap("Detail UV transform",2D)="grey"{}
  _MicroRoughness("Skin roughness",2D)="grey"{}
 }
 SubShader
 {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Pass
  {
   Name "SkinForward"
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);
   TEXTURE2D(_OcclusionMap);SAMPLER(sampler_OcclusionMap);
   TEXTURE2D(_DetailNormalMap);SAMPLER(sampler_DetailNormalMap);
   TEXTURE2D(_MicroRoughness);SAMPLER(sampler_MicroRoughness);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_DetailAlbedoMap_ST;
   half4 _BaseColor;
   half _BumpScale,_Smoothness,_OcclusionStrength,_DetailNormalMapScale;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;half4 color:COLOR;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half4 color:TEXCOORD4;half fog:TEXCOORD5;};
   Varyings Vert(Attributes v)
   {
    Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);
    o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;
    o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.color=v.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;
   }
   half4 Frag(Varyings i):SV_Target
   {
    float2 detailUV=i.uv*_DetailAlbedoMap_ST.xy+_DetailAlbedoMap_ST.zw;
    half3 n=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
    half3 d=UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap,sampler_DetailNormalMap,detailUV),_DetailNormalMapScale);
    n=normalize(half3(n.xy+d.xy,n.z*d.z));
    half3 tangent=normalize(i.tangentWS.xyz),normal=normalize(i.normalWS),bitangent=cross(normal,tangent)*i.tangentWS.w;
    InputData inputData=(InputData)0;
    inputData.positionWS=i.positionWS;inputData.normalWS=normalize(tangent*n.x+bitangent*n.y+normal*n.z);
    inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);inputData.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
    inputData.bakedGI=SampleSH(inputData.normalWS);inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);inputData.shadowMask=half4(1,1,1,1);
    SurfaceData surface=(SurfaceData)0;
    half3 sourceAlbedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
    half3 albedo=sourceAlbedo*_BaseColor.rgb;
    // Preserve pale nail patches. Vertex tint describes warm joint/thenar tissue, never painted on the source atlas.
    half nail=smoothstep(.168,.181,sourceAlbedo.r)*smoothstep(.085,.104,sourceAlbedo.g);
    surface.albedo=albedo*lerp(i.color.rgb,half3(1,1,1),nail);
    half packedSmooth=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,i.uv).a;
    half rough=SAMPLE_TEXTURE2D(_MicroRoughness,sampler_MicroRoughness,detailUV).r;
    surface.smoothness=lerp(saturate(packedSmooth*_Smoothness+(rough-.62)*-.2),.45,nail);
    surface.metallic=0;surface.specular=half3(.04,.04,.04);surface.normalTS=n;surface.alpha=1;
    surface.occlusion=lerp(1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,i.uv).g,_OcclusionStrength);
    half4 output=UniversalFragmentPBR(inputData,surface);output.rgb=MixFog(output.rgb,i.fog);return output;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
