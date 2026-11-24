using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Environment
{
    // Renders FrontlineVolumetricFog (2026-11-24). Written for URP 17.3 render graph.
    // Half-resolution depth (checkerboard min/max) -> ray march at half resolution (in-scatter + transmittance)
    // -> depth-aware blur, horizontal and vertical -> depth-aware upsample and composite onto the camera colour
    // before the transparents (smoke, sparks and the flare sprite stay crisp on top of the fog).
    // The depth downsample and the bilateral blur follow the approach of Cristian Qiu's URP volumetric light
    // (MIT, see LICENSE_VolumetricFog.txt next to this file); the rest is written for this project.
    // The shader is a serialized reference (set by the menu FiveRivers/Setup/Volumetric Fog 20261124), so it is
    // packed into the player build with the renderer asset; Shader.Find is not used at runtime.
    [DisallowMultipleRendererFeature("Frontline Volumetric Fog")]
    public sealed class FrontlineVolumetricFogFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader;
        [SerializeField] RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingTransparents;

        Material material;
        FogPass pass;
        SkyGlowPass skyPass;

        public Shader Shader { get => shader; set { shader = value; Create(); } }

        public override void Create()
        {
            if (material != null && (shader == null || material.shader != shader)) { CoreUtils.Destroy(material); material = null; }
            if (material == null && shader != null && shader.isSupported) material = CoreUtils.CreateEngineMaterial(shader);
            pass = new FogPass(material) { renderPassEvent = passEvent };
            skyPass = new SkyGlowPass(material) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null) return;
            var type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;
            var stack = VolumeManager.instance.stack;
            var fog = stack != null ? stack.GetComponent<FrontlineVolumetricFog>() : null;
            if (fog == null || !fog.IsActive()) return;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(pass);
            renderer.EnqueuePass(skyPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        // what the fog pass leaves for the sky-glow pass later in the frame
        sealed class FogFrameData : ContextItem
        {
            public TextureHandle scatter, halfDepth;
            public Vector4 fullSize, halfSize;
            public bool valid;
            public override void Reset() { scatter = TextureHandle.nullHandle; halfDepth = TextureHandle.nullHandle; valid = false; }
        }

        class PassData
        {
            public Material material;
            public TextureHandle source, halfDepth, fullDepth;
            public int pass;
            public Vector4 fullSize, halfSize;
        }

        // The far ridge band and the horizon glow are transparent and drawn after the fog pass; over the sky (where the
        // depth texture is empty, the ridge band included) the lights' glow is added here, after all transparents,
        // so a flare's glow lies over the ridges in front of which it burns instead of being cut by them.
        sealed class SkyGlowPass : ScriptableRenderPass
        {
            const int SkyPass = 5;
            static readonly int FullSizeId = Shader.PropertyToID("_FogFullSize");
            static readonly int HalfSizeId = Shader.PropertyToID("_FogHalfSize");
            static readonly int HalfDepthId = Shader.PropertyToID("_FogHalfDepth");
            static readonly int FullDepthId = Shader.PropertyToID("_FogFullDepth");
            readonly Material material;
            public SkyGlowPass(Material material) { this.material = material; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || !frameData.Contains<FogFrameData>()) return;
                var fogData = frameData.Get<FogFrameData>();
                if (!fogData.valid) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!depth.IsValid()) return;
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Sky Glow", out var data))
                {
                    data.material = material; data.source = fogData.scatter; data.halfDepth = fogData.halfDepth; data.fullDepth = depth; data.pass = SkyPass;
                    data.fullSize = fogData.fullSize; data.halfSize = fogData.halfSize;
                    builder.UseTexture(fogData.scatter);
                    builder.UseTexture(fogData.halfDepth);
                    builder.UseTexture(depth);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetVector(FullSizeId, d.fullSize); d.material.SetVector(HalfSizeId, d.halfSize);
                        d.material.SetTexture(HalfDepthId, d.halfDepth);
                        d.material.SetTexture(FullDepthId, d.fullDepth);
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, d.pass);
                    });
                }
            }
        }

        sealed class FogPass : ScriptableRenderPass
        {
            const int DepthPass = 0, MarchPass = 1, BlurHPass = 2, BlurVPass = 3, CompositePass = 4;
            static readonly int FullSizeId = Shader.PropertyToID("_FogFullSize");
            static readonly int HalfSizeId = Shader.PropertyToID("_FogHalfSize");
            static readonly int HalfDepthId = Shader.PropertyToID("_FogHalfDepth");
            static readonly int FullDepthId = Shader.PropertyToID("_FogFullDepth");
            static readonly int Params0Id = Shader.PropertyToID("_FogParams0"); // density, base height, 1/falloff, distance
            static readonly int Params1Id = Shader.PropertyToID("_FogParams1"); // ambient, main scatter, main g, steps
            static readonly int Params2Id = Shader.PropertyToID("_FogParams2"); // light scatter, light g, near radius^2, light layer (as float bits)
            static readonly int Params3Id = Shader.PropertyToID("_FogParams3"); // noise amount, noise scale, time
            static readonly int TintId = Shader.PropertyToID("_FogTint");
            static readonly int WindId = Shader.PropertyToID("_FogWind");
            static readonly int Params4Id = Shader.PropertyToID("_FogParams4"); // light ceiling

            readonly Material material;

            public FogPass(Material material) { this.material = material; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var fog = VolumeManager.instance.stack.GetComponent<FrontlineVolumetricFog>();
                if (fog == null || !fog.IsActive() || material == null) return;
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle depth = resources.cameraDepthTexture;
                if (!depth.IsValid()) return;

                var desc = camera.cameraTargetDescriptor;
                int fullW = desc.width, fullH = desc.height;
                int halfW = Mathf.Max(1, fullW / 2), halfH = Mathf.Max(1, fullH / 2);
                var fullSize = new Vector4(fullW, fullH, 1f / fullW, 1f / fullH);
                var halfSize = new Vector4(halfW, halfH, 1f / halfW, 1f / halfH);

                // shared values, set once per frame (render funcs only bind textures)
                material.SetVector(Params0Id, new Vector4(fog.density.value, fog.baseHeight.value, 1f / Mathf.Max(0.01f, fog.heightFalloff.value), fog.distance.value));
                material.SetVector(Params1Id, new Vector4(fog.ambient.value, fog.mainLightScattering.value, fog.mainLightAnisotropy.value, fog.steps.value));
                float r = fog.lightNearRadius.value;
                material.SetVector(Params2Id, new Vector4(fog.lightScattering.value, fog.lightAnisotropy.value, r * r, FrontlineVolumetricFog.FogLightLayer));
                material.SetVector(Params3Id, new Vector4(fog.noise.value, fog.noiseScale.value, Time.time, fog.airDensity.value));
                material.SetColor(TintId, fog.tint.value.linear);
                material.SetVector(WindId, fog.wind.value);
                material.SetVector(Params4Id, new Vector4(fog.lightCeiling.value, 0f, 0f, 0f));

                var halfDesc = new RenderTextureDescriptor(halfW, halfH) { msaaSamples = 1, depthBufferBits = 0, graphicsFormat = GraphicsFormat.R32_SFloat };
                TextureHandle halfDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, halfDesc, "_FogHalfDepth", false);
                var scatterDesc = halfDesc; scatterDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                TextureHandle scatter = UniversalRenderer.CreateRenderGraphTexture(renderGraph, scatterDesc, "_FogScatter", false, FilterMode.Bilinear);
                TextureHandle scatterB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, scatterDesc, "_FogScatterB", false, FilterMode.Bilinear);

                // 1. half-resolution depth
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Half Depth", out var data))
                {
                    data.material = material; data.source = depth; data.pass = DepthPass; data.fullSize = fullSize; data.halfSize = halfSize;
                    builder.UseTexture(depth);
                    builder.SetRenderAttachment(halfDepth, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetVector(FullSizeId, d.fullSize); d.material.SetVector(HalfSizeId, d.halfSize);
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, d.pass);
                    });
                }

                // 2. ray march (reads the shadow maps and the light lists, all globals)
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fog March", out var data))
                {
                    data.material = material; data.source = halfDepth; data.pass = MarchPass; data.fullSize = fullSize; data.halfSize = halfSize;
                    builder.UseTexture(halfDepth);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(scatter, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetVector(FullSizeId, d.fullSize); d.material.SetVector(HalfSizeId, d.halfSize);
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, d.pass);
                    });
                }

                // 3. depth-aware blur, both directions
                Blur(renderGraph, "Fog Blur H", scatter, scatterB, halfDepth, BlurHPass, fullSize, halfSize);
                Blur(renderGraph, "Fog Blur V", scatterB, scatter, halfDepth, BlurVPass, fullSize, halfSize);

                var hand = frameData.GetOrCreate<FogFrameData>();
                hand.scatter = scatter; hand.halfDepth = halfDepth; hand.fullSize = fullSize; hand.halfSize = halfSize; hand.valid = true;

                // 4. composite: colour * transmittance + in-scatter (geometry only; the sky is done after the transparents)
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Composite", out var data))
                {
                    data.material = material; data.source = scatter; data.halfDepth = halfDepth; data.fullDepth = depth; data.pass = CompositePass;
                    data.fullSize = fullSize; data.halfSize = halfSize;
                    builder.UseTexture(scatter);
                    builder.UseTexture(halfDepth);
                    builder.UseTexture(depth);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetVector(FullSizeId, d.fullSize); d.material.SetVector(HalfSizeId, d.halfSize);
                        d.material.SetTexture(HalfDepthId, d.halfDepth);
                        d.material.SetTexture(FullDepthId, d.fullDepth);
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, d.pass);
                    });
                }
            }

            void Blur(RenderGraph renderGraph, string name, TextureHandle from, TextureHandle to, TextureHandle halfDepth, int passIndex, Vector4 fullSize, Vector4 halfSize)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(name, out var data))
                {
                    data.material = material; data.source = from; data.halfDepth = halfDepth; data.pass = passIndex; data.fullSize = fullSize; data.halfSize = halfSize;
                    builder.UseTexture(from);
                    builder.UseTexture(halfDepth);
                    builder.SetRenderAttachment(to, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        d.material.SetVector(FullSizeId, d.fullSize); d.material.SetVector(HalfSizeId, d.halfSize);
                        d.material.SetTexture(HalfDepthId, d.halfDepth);
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, d.pass);
                    });
                }
            }
        }
    }
}
