#ifndef FR_TERRAIN_GRASS_LIGHTING_INCLUDED
#define FR_TERRAIN_GRASS_LIGHTING_INCLUDED

// 지형 풀 빌보드의 조명 (2026-11-27). FR_TerrainGrassBillboard.shader 가 URP 17.3.0 의 WavingGrassInput.hlsl ·
// WavingGrassPasses.hlsl 를 그대로 include 한 뒤 이 파일을 include 한다. 정점 · 바람결 · 거리 페이드 · 표면(InitializeSimpleLitSurfaceData)
// · 입력(InitializeInputData) 은 URP 원본 그대로이고, 조각(fragment) 셰이더의 빛 합성만 바꾼다.
//
// URP 원본(LitPassFragmentGrass)과 다른 점:
//  1. 간접광: 원본 빌보드 셰이더에는 프로브 볼륨(APV) 변형이 없고, 지형 디테일에는 물체별 SH(unity_SH*)가 채워지지 않아
//     간접광이 0이었다 (Fable 측정: 방향광을 끄면 땅 5/255, 풀 0). 이 셰이더는 ProbeVolumeVariants 로 APV 를 받고,
//     그래도 0이면(APV 꺼짐 · 범위 밖) 장면의 삼색 앰비언트(unity_AmbientSky/Equator/Ground, URP 가 매 프레임 전역으로 넣음)를 쓴다.
//  2. 뿌리 그늘: 간접광은 포기 속(풀 높이의 아래쪽)과 알파가 얇은 가장자리에서 덜 들어온다. 직접광 쪽 뿌리 그늘은 이미
//     텍스처(…_Rooted.png, TerrainGrassLook20261127) 에 들어 있어 여기서는 간접광에만 건다.
//  3. 끝의 직접광: 조명탄(점광원 · 위)에서 풀 끝이 땅보다 25~38 % 밝았다. 높이 위쪽의 직접광을 조금 줄인다.
// 값은 아래 기본값. 전역 벡터 _FRGrassTune(x 간접광 배율, y 끝 직접광 배율, z 뿌리 간접광 배율, w>0.5 이면 사용)으로
// 실행 중에 바꿔 볼 수 있다 (Shader.SetGlobalVector, 저장되지 않음). 최종 값은 화면으로 정해 이 파일의 기본값에 넣는다.

#define FR_GRASS_GI_SCALE       2.0   // 간접광 배율 (2026-11-27 결정 (나): 간접광 ×2 + 색 권장 중간값)
#define FR_GRASS_TIP_DIRECT     0.8   // 풀 끝(높이 1)의 직접광 배율. 높이 0.35 아래는 1
#define FR_GRASS_ROOT_GI        0.55  // 뿌리(높이 0)의 간접광 배율. 높이 FR_GRASS_ROOT_HEIGHT 에서 1
#define FR_GRASS_ROOT_HEIGHT    0.5
#define FR_GRASS_ALPHA_SHADE    0.15  // 알파가 오림 기준에 가까운 가장자리의 간접광을 이만큼 덜

float4 _FRGrassTune;

half4 FRGrassTune()
{
    return _FRGrassTune.w > 0.5 ? half4(_FRGrassTune.xyz, 1.0) : half4(FR_GRASS_GI_SCALE, FR_GRASS_TIP_DIRECT, FR_GRASS_ROOT_GI, 1.0);
}

// the scene's trilight ambient by the normal (URP sets these every frame from RenderSettings)
half3 FRGrassAmbient(half3 normalWS)
{
    half up = normalWS.y;
    half3 above = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(up));
    half3 below = lerp(unity_AmbientEquator.rgb, unity_AmbientGround.rgb, saturate(-up));
    return up >= 0 ? above : below;
}

half3 FRGrassIndirect(half3 bakedGI, half3 normalWS)
{
    // nothing came from the probes (no probe-volume variant in use, or no per-object SH on terrain details)
    if (dot(bakedGI, half3(0.2126, 0.7152, 0.0722)) < 1e-5) bakedGI = FRGrassAmbient(normalWS);
    return bakedGI;
}

// how much indirect light reaches this point of the tuft: less low in it and at the thin edges of the blades
half FRGrassIndirectShade(half height, half alpha, half rootGI)
{
    half root = lerp(rootGI, 1.0, smoothstep(0.0, FR_GRASS_ROOT_HEIGHT, height));
    half edge = lerp(1.0 - FR_GRASS_ALPHA_SHADE, 1.0, saturate((alpha - _Cutoff) / max(1.0 - _Cutoff, 1e-3)));
    return root * edge;
}

half FRGrassDirectShade(half height, half tipDirect)
{
    return lerp(1.0, tipDirect, smoothstep(0.35, 1.0, height));
}

#ifdef TERRAIN_GBUFFER
GBufferFragOutput FRLitPassFragmentGrass(GrassVertexOutput input)
#else
half4 FRLitPassFragmentGrass(GrassVertexOutput input) : SV_Target
#endif
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    InitializeSimpleLitSurfaceData(input, surfaceData);

    InputData inputData;
    InitializeInputData(input, inputData);
    SETUP_DEBUG_TEXTURE_DATA_FOR_TEX(inputData, input.uv, _MainTex);

    half4 tune = FRGrassTune();
    half height = saturate(input.uv.y); // billboard UV: 0 at the root, 1 at the tip
    half3 gi = FRGrassIndirect(inputData.bakedGI, inputData.normalWS) * tune.x
        * FRGrassIndirectShade(height, surfaceData.alpha, tune.z);

#ifdef TERRAIN_GBUFFER
    inputData.bakedGI = gi;
    return PackGBuffersSurfaceData(surfaceData, inputData, gi * surfaceData.albedo + surfaceData.emission);
#else
    // the direct lights alone (main, additional, vertex), then the indirect light added on its own
    InputData directOnly = inputData;
    directOnly.bakedGI = half3(0, 0, 0);
    half4 color = UniversalFragmentBlinnPhong(directOnly, surfaceData);
    color.rgb = color.rgb * FRGrassDirectShade(height, tune.y) + gi * surfaceData.albedo;
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return half4(color.rgb, OutputAlpha(surfaceData.alpha, IsSurfaceTypeTransparent(_Surface)));
#endif
}

#endif
