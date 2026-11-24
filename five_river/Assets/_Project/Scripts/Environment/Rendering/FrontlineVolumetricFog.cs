using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Environment
{
    // Volumetric fog for the night trench and the valley at daybreak (2026-11-24, engine-features graphics pass).
    // A low height fog that the lights shine through: the flares, the shell flashes, the burning tank and the
    // enemy muzzle flashes light the air around them (halo and shafts through their shadows), the moon draws
    // faint shafts. Its colour away from the lights is the existing distance-fog colour (RenderSettings.fogColor,
    // which DawnDirector turns from night to dawn), so the two read as one haze.
    // The night values live in Frontline_Night_Profile, the dawn values in Frontline_Dawn_Grade (its weight follows
    // the daybreak), set by the menu FiveRivers/Setup/Volumetric Fog 20261124.
    // Rendering: FrontlineVolumetricFogFeature on PC_Renderer. Only lights carrying the rendering layer
    // FogLightLayer scatter, so the first-person fill lights and the player's own flashes never fog the view.
    [System.Serializable]
    [VolumeComponentMenu("FiveRivers/Frontline Volumetric Fog")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class FrontlineVolumetricFog : VolumeComponent
    {
        // "Light Layer 7" of the project's rendering layers. Adding it to a light's rendering layers does not change
        // what the light lights (no renderer carries this bit); it only lets the light scatter in the fog.
        public const uint FogLightLayer = 1u << 7;

        [Tooltip("Extinction per metre at and below the base height (0 = off).")]
        public ClampedFloatParameter density = new ClampedFloatParameter(0f, 0f, 0.2f);
        [Tooltip("Thin air everywhere, whatever the height (a flare high up glows in it).")]
        public ClampedFloatParameter airDensity = new ClampedFloatParameter(0f, 0f, 0.02f);
        [Tooltip("World height under which the fog is at full density.")]
        public FloatParameter baseHeight = new FloatParameter(0f);
        [Tooltip("Metres above the base height over which the density falls to 1/e.")]
        public ClampedFloatParameter heightFalloff = new ClampedFloatParameter(6f, 0.5f, 100f);
        [Tooltip("How far from the camera the fog is marched (metres). The distance fog does the rest.")]
        public ClampedFloatParameter distance = new ClampedFloatParameter(120f, 10f, 400f);
        [Tooltip("Colour of the scattering medium.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);
        [Tooltip("Uniform in-scatter of the haze colour (the distance-fog colour).")]
        public ClampedFloatParameter ambient = new ClampedFloatParameter(1f, 0f, 3f);
        public ClampedFloatParameter mainLightScattering = new ClampedFloatParameter(0.5f, 0f, 8f);
        public ClampedFloatParameter mainLightAnisotropy = new ClampedFloatParameter(0.6f, 0f, 0.95f);
        [Tooltip("Strength of the point and spot lights (FogLightLayer only) in the fog.")]
        public ClampedFloatParameter lightScattering = new ClampedFloatParameter(1f, 0f, 16f);
        public ClampedFloatParameter lightAnisotropy = new ClampedFloatParameter(0.3f, 0f, 0.95f);
        [Tooltip("Inside this radius a point light stops growing brighter in the fog (no hot spot at the source).")]
        public ClampedFloatParameter lightNearRadius = new ClampedFloatParameter(1.5f, 0.1f, 6f);
        [Tooltip("Soft ceiling of the lights' glow (linear, before exposure; 0 = none). A flare close overhead no longer burns a white patch into the sky; faint glow is left as it is.")]
        public ClampedFloatParameter lightCeiling = new ClampedFloatParameter(0f, 0f, 2f);
        [Tooltip("How patchy the fog is (0 = even).")]
        public ClampedFloatParameter noise = new ClampedFloatParameter(0.3f, 0f, 1f);
        public ClampedFloatParameter noiseScale = new ClampedFloatParameter(0.05f, 0.005f, 0.5f);
        public Vector3Parameter wind = new Vector3Parameter(new Vector3(0.4f, 0f, 0.15f));
        [Tooltip("Ray-march steps per half-resolution pixel.")]
        public ClampedIntParameter steps = new ClampedIntParameter(24, 8, 64);

        public bool IsActive() => active && (density.value > 0f || airDensity.value > 0f);

        // Lets a light shine in the fog (adds FogLightLayer to its rendering layers, keeps the rest).
        public static void MarkLight(Light light)
        {
            if (light == null) return;
            var data = light.GetUniversalAdditionalLightData();
            if (data != null) data.renderingLayers |= FogLightLayer;
        }
    }
}
