// Volumetric fog for FrontlineVolumetricFogFeature (2026-11-24). URP 17.3, Forward+ (cluster light loop).
// Passes: 0 half-resolution depth, 1 ray march, 2/3 depth-aware blur, 4 depth-aware upsample + composite.
// Depth downsample and bilateral blur after Cristian Qiu's URP volumetric light (MIT, LICENSE_VolumetricFog.txt).
Shader "Hidden/FiveRivers/FrontlineVolumetricFog"
{
    HLSLINCLUDE
    #define USE_FULL_PRECISION_BLIT_TEXTURE 1
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float4 _FogFullSize;   // xy pixels, zw 1/pixels
    float4 _FogHalfSize;
    TEXTURE2D_X_FLOAT(_FogHalfDepth);
    TEXTURE2D_X_FLOAT(_FogFullDepth);

    // TrenchShade (2026-11-24): how far below the trench rim a point lies -> darker
    TEXTURE2D(_TrenchRimMap); SAMPLER(sampler_TrenchRimMap);
    float4 _TrenchRimBounds;   // min x, min z, 1/size x, 1/size z
    float4 _TrenchShadeParams; // strength, start depth, full depth, near clear distance

    float TrenchShade(float2 uv, float rawDepth, float eyeDepth)
    {
        if (_TrenchShadeParams.x <= 0.0) return 1.0;
    #if !UNITY_REVERSED_Z
        rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
    #endif
        float3 p = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        float2 m = (p.xz - _TrenchRimBounds.xy) * _TrenchRimBounds.zw;
        if (m.x <= 0.0 || m.y <= 0.0 || m.x >= 1.0 || m.y >= 1.0) return 1.0;
        float rim = SAMPLE_TEXTURE2D_LOD(_TrenchRimMap, sampler_TrenchRimMap, m, 0).r;
        float occ = smoothstep(_TrenchShadeParams.y, _TrenchShadeParams.z, rim - p.y);
        float nearFade = saturate(eyeDepth - _TrenchShadeParams.w);            // the hands and the rifle stay as they are
        float edge = saturate(min(min(m.x, 1.0 - m.x), min(m.y, 1.0 - m.y)) * 25.0);
        return 1.0 - _TrenchShadeParams.x * occ * nearFade * edge;
    }

    bool IsSkyDepth(float raw)
    {
    #if UNITY_REVERSED_Z
        return raw <= 1e-7;
    #else
        return raw >= 1.0 - 1e-7;
    #endif
    }

    float LoadHalfDepth(int2 p)
    {
        p = clamp(p, int2(0, 0), int2(_FogHalfSize.xy) - 1);
        return LOAD_TEXTURE2D_X(_FogHalfDepth, p).r;
    }

    static const float BlurWeights[5] = { 0.2026, 0.1790, 0.1240, 0.0672, 0.0285 };
    float4 BlurDir(Varyings input, int2 dir)
    {
        int2 c = int2(input.positionCS.xy);
        float zc = LinearEyeDepth(LoadHalfDepth(c), _ZBufferParams);
        float tol = 0.1 + zc * 0.04;
        int2 m = int2(_FogHalfSize.xy) - 1;
        float4 sum = LOAD_TEXTURE2D_X(_BlitTexture, c) * BlurWeights[0];
        float wsum = BlurWeights[0];
        [unroll] for (int i = 1; i <= 4; i++)
        {
            [unroll] for (int s = -1; s <= 1; s += 2)
            {
                int2 q = clamp(c + dir * (i * s), int2(0, 0), m);
                float zq = LinearEyeDepth(LoadHalfDepth(q), _ZBufferParams);
                float r = (zq - zc) / tol;
                float w = BlurWeights[i] * exp(-r * r);
                sum += LOAD_TEXTURE2D_X(_BlitTexture, q) * w;
                wsum += w;
            }
        }
        return sum / wsum;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        // 0: half-resolution depth. Alternates the nearest and the farthest of each 2x2 block in a checkerboard,
        //    so both sides of an edge survive for the upsample.
        Pass
        {
            Name "FogHalfDepth"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int2 h = int2(input.positionCS.xy);
                int2 p = h * 2;
                int2 m = int2(_FogFullSize.xy) - 1;
                float a = LOAD_TEXTURE2D_X(_BlitTexture, min(p, m)).r;
                float b = LOAD_TEXTURE2D_X(_BlitTexture, min(p + int2(1, 0), m)).r;
                float c = LOAD_TEXTURE2D_X(_BlitTexture, min(p + int2(0, 1), m)).r;
                float d = LOAD_TEXTURE2D_X(_BlitTexture, min(p + int2(1, 1), m)).r;
                float lo = min(min(a, b), min(c, d)), hi = max(max(a, b), max(c, d));
                return ((h.x + h.y) & 1) ? lo : hi;
            }
            ENDHLSL
        }

        // 1: ray march. rgb = light scattered toward the camera, a = transmittance.
        Pass
        {
            Name "FogMarch"
            Blend Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"

            float4 _FogParams0; // density, base height, 1/falloff, distance
            float4 _FogParams1; // ambient, main scatter, main g, steps
            float4 _FogParams2; // light scatter, light g, near radius^2, light layer
            float4 _FogParams3; // noise amount, noise scale, time, air density
            float4 _FogTint;
            float4 _FogWind;
            float4 _FogParams4; // light ceiling

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Density(float3 p)
            {
                float above = max(p.y - _FogParams0.y, 0.0);
                float d = _FogParams0.x * exp(-above * _FogParams0.z);
                if (_FogParams3.x > 0.001)
                {
                    float n = ValueNoise(p * _FogParams3.y + _FogWind.xyz * _FogParams3.z * _FogParams3.y);
                    d *= lerp(1.0, saturate(n * 1.6 - 0.1) * 1.3, _FogParams3.x);
                }
                return d + _FogParams3.w;
            }

            float3 MainLightIn(float3 p, float phase)
            {
            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                shadow = lerp(shadow, 1.0, GetMainLightShadowFade(p));
            #else
                float shadow = 1.0;
            #endif
                return _MainLightColor.rgb * (shadow * phase * _FogParams1.y);
            }

            float3 AdditionalLightsIn(float3 p, float3 rd, float2 tileUV)
            {
                float3 sum = 0;
            #if USE_CLUSTER_LIGHT_LOOP
                uint layer = (uint)_FogParams2.w;
                uint lightIndex;
                ClusterIterator it = ClusterInit(tileUV, p, 0);
                [loop] while (ClusterNext(it, lightIndex))
                {
                    lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT;
                #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                    uint mask = _AdditionalLightsBuffer[lightIndex].layerMask;
                    float4 lp = _AdditionalLightsBuffer[lightIndex].position;
                #else
                    uint mask = asuint(_AdditionalLightsLayerMasks[lightIndex]);
                    float4 lp = _AdditionalLightsPosition[lightIndex];
                #endif
                    if ((mask & layer) == 0u) continue;
                    Light l = GetAdditionalPerObjectLight(lightIndex, p);
                    float3 v = lp.xyz - p;
                    float d2 = max(dot(v, v), 1e-4);
                    // no hot spot: inside the near radius the light stops growing brighter
                    float atten = l.distanceAttenuation * d2 / max(d2, _FogParams2.z);
                    if (atten < 1e-5) continue;
                #if defined(_ADDITIONAL_LIGHT_SHADOWS)
                    atten *= AdditionalLightRealtimeShadow(lightIndex, p, l.direction);
                #endif
                    float phase = CornetteShanksPhaseFunction(_FogParams2.y, dot(rd, l.direction));
                    sum += l.color * (atten * phase);
                }
            #endif
                return sum * _FogParams2.x;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float raw = LoadHalfDepth(int2(input.positionCS.xy));
            #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
            #endif
            #if UNITY_REVERSED_Z
                bool sky = raw <= 1e-7;
            #else
                bool sky = raw >= 1.0 - 1e-7;
            #endif
                float3 end = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 ro = GetCameraPositionWS();
                float3 toEnd = end - ro;
                float len = length(toEnd);
                float3 rd = toEnd / max(len, 1e-4);
                float maxLen = min(len, _FogParams0.w);
                if (maxLen < 0.05) return float4(0, 0, 0, 1);

                int steps = (int)_FogParams1.w;
                float jitter = InterleavedGradientNoise(input.positionCS.xy, 0);
                float mainPhase = CornetteShanksPhaseFunction(_FogParams1.z, dot(rd, _MainLightPosition.xyz));
                // the sky and the far ridge band drawn over it keep their own haze (they blend to the distance-fog colour
                // themselves and are drawn after this pass): over the sky only the lights' glow is added, no extinction
                float3 ambient = sky ? 0 : unity_FogColor.rgb * _FogParams1.x;
                if (sky) maxLen = min(maxLen, _FogParams0.w * 0.75);
                float3 tint = _FogTint.rgb;

                float3 scatter = 0;      // the haze colour (ambient)
                float3 scatterLight = 0; // the moon and the lights
                float transmittance = 1.0;
                float prev = 0.0;
                // more samples near the camera: d = maxLen * u^2
                [loop] for (int i = 0; i < steps; i++)
                {
                    float u = (i + jitter) / steps;
                    float dist = maxLen * u * u;
                    float dt = dist - prev;
                    prev = dist;
                    float3 p = ro + rd * dist;
                    float sigma = Density(p);
                    if (sigma < 1e-5) continue;
                    float3 lightIn = MainLightIn(p, mainPhase) + AdditionalLightsIn(p, rd, uv);
                    float ext = exp(-sigma * dt);
                    scatter += transmittance * (1.0 - ext) * ambient * tint;
                    scatterLight += transmittance * (1.0 - ext) * lightIn * tint;
                    if (!sky) transmittance *= ext;
                    if (transmittance < 0.01) break;
                }
                // soft ceiling: s / (1 + s / c) - the faint glow stays, a burning white patch does not
                if (_FogParams4.x > 0.0) scatterLight = scatterLight / (1.0 + scatterLight / _FogParams4.x);
                scatter += scatterLight;
                if (sky) return float4(scatter, 1.0);
                // the last stretch to the end of the ray
                float3 pe = ro + rd * maxLen;
                float sigmaE = Density(pe);
                float extE = exp(-sigmaE * (maxLen - prev));
                scatter += transmittance * (1.0 - extE) * ambient * tint;
                transmittance *= extE;
                return float4(scatter, transmittance);
            }
            ENDHLSL
        }

        // 2, 3: depth-aware Gaussian blur at half resolution (BlurDir above)

        Pass
        {
            Name "FogBlurH"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); return BlurDir(input, int2(1, 0)); }
            ENDHLSL
        }

        Pass
        {
            Name "FogBlurV"
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); return BlurDir(input, int2(0, 1)); }
            ENDHLSL
        }

        // 4: upsample (weights by depth match to the full-resolution pixel) and composite: dst * a + rgb (not over the sky)
        Pass
        {
            Name "FogComposite"
            Blend One SrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int2 fp = int2(input.positionCS.xy);
                float rawFull = LOAD_TEXTURE2D_X(_FogFullDepth, fp).r;
                if (IsSkyDepth(rawFull)) return float4(0, 0, 0, 1);
                float z = LinearEyeDepth(rawFull, _ZBufferParams);
                float2 hp = (float2(fp) + 0.5) * 0.5 - 0.5;
                int2 b = int2(floor(hp));
                float2 f = hp - b;
                int2 m = int2(_FogHalfSize.xy) - 1;
                float tol = 0.05 + z * 0.03;
                float4 sum = 0; float wsum = 0;
                float4 nearest = 0; float best = 1e9;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    int2 o = int2(k & 1, k >> 1);
                    int2 q = clamp(b + o, int2(0, 0), m);
                    float zq = LinearEyeDepth(LoadHalfDepth(q), _ZBufferParams);
                    float4 s = LOAD_TEXTURE2D_X(_BlitTexture, q);
                    float bil = (o.x ? f.x : 1.0 - f.x) * (o.y ? f.y : 1.0 - f.y);
                    float dz = abs(zq - z);
                    float w = bil * exp(-(dz / tol) * (dz / tol)) + 1e-5 * bil;
                    sum += s * w; wsum += w;
                    if (dz < best) { best = dz; nearest = s; }
                }
                float4 result = wsum > 1e-3 ? sum / wsum : nearest;
                return float4(result.rgb, saturate(result.a) * TrenchShade(input.texcoord, rawFull, z));
            }
            ENDHLSL
        }

        // 5: over the sky only, after the transparents: add the lights' glow (rgb of the sky samples)
        Pass
        {
            Name "FogSkyGlow"
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int2 fp = int2(input.positionCS.xy);
                if (!IsSkyDepth(LOAD_TEXTURE2D_X(_FogFullDepth, fp).r)) return 0;
                float2 hp = (float2(fp) + 0.5) * 0.5 - 0.5;
                int2 b = int2(floor(hp));
                float2 f = hp - b;
                int2 m = int2(_FogHalfSize.xy) - 1;
                float3 sum = 0; float wsum = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    int2 o = int2(k & 1, k >> 1);
                    int2 q = clamp(b + o, int2(0, 0), m);
                    float w = (o.x ? f.x : 1.0 - f.x) * (o.y ? f.y : 1.0 - f.y);
                    w *= IsSkyDepth(LoadHalfDepth(q)) ? 1.0 : 0.0; // only sky samples: no ground haze bleeds onto the sky
                    sum += LOAD_TEXTURE2D_X(_BlitTexture, q).rgb * w; wsum += w;
                }
                return float4(wsum > 1e-4 ? sum / wsum : 0, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
