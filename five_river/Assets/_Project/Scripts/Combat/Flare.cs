using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 낙하산 조명탄 하나 (VFX_Flare). 켜지고 · 천천히 흔들리며 내려오고 · 꺼진다.
    public sealed class Flare : MonoBehaviour
    {
        [SerializeField] Light flareLight;
        [SerializeField] Transform visual;

        FlareConfig config;
        Vector3 origin;
        float born;
        const float IgniteTime = 0.6f;

        public float Age => Time.time - born;
        public float Duration => config.burnTime + config.fadeTime;

        // 0~1: 켜지는 중 · 타는 중 1 · 꺼지는 중
        public float Brightness
        {
            get
            {
                float age = Age;
                if (age < IgniteTime) return age / IgniteTime;
                if (age < config.burnTime) return 1f;
                return Mathf.Clamp01(1f - (age - config.burnTime) / config.fadeTime);
            }
        }

        public bool Fading => Age >= config.burnTime;

        public void Ignite(FlareConfig flareConfig)
        {
            config = flareConfig;
            origin = transform.position;
            born = Time.time;
            Apply();
        }

        void Update()
        {
            if (config == null) return;
            Apply();
            if (Age >= Duration) Destroy(gameObject);
        }

        void Apply()
        {
            float age = Age;
            transform.position = origin + new Vector3(Mathf.Sin(age * 0.7f) * config.swayAmplitude, -config.fallSpeed * age, 0f);
            float b = Brightness;
            // a burning magnesium flare sputters: a few percent of flicker makes the shadows on the slope breathe
            float sputter = 1f + 0.12f * (Mathf.PerlinNoise(age * 7f, origin.x) - 0.5f);
            flareLight.intensity = config.lightIntensity * b * sputter;
            flareLight.range = config.lightRange;
            visual.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.4f, b);
            ApplyBounce();
        }

        // ---- 땅에서 튕겨 올라오는 빛 (부족한 점 2번, 2026-11-26) ----
        // 조명탄 빛이 흙바닥에 닿아 흩어지는 빛을, 조명탄을 땅 표면에 대칭으로 비춘 자리(땅 아래)의 그림자 없는 점광으로 흉내 낸다.
        // 위를 향한 면(땅)은 비추지 않고, 아래 · 옆을 향한 면(사람의 턱 · 소매 · 총 아래, 참호 벽)을 흙빛으로 받친다.
        // 안개 산란 층(FogLightLayer)은 빼서 땅 밑에서 안개가 빛나지 않게 한다.
        Light bounce;
        Terrain ground;

        void ApplyBounce()
        {
            float strength = config.bounceAlbedo * config.bounceStrength;
            if (strength <= 0f)
            {
                if (bounce != null) bounce.enabled = false;
                return;
            }
            if (bounce == null) CreateBounce();
            var p = transform.position;
            float groundY = ground != null ? ground.SampleHeight(p) + ground.transform.position.y : 0f;
            float height = Mathf.Max(0.5f, p.y - groundY);
            bounce.transform.position = new Vector3(p.x, groundY - height, p.z);
            bounce.intensity = flareLight.intensity * strength;
            bounce.range = 2f * height + config.bounceRangePad;
            bounce.enabled = flareLight.enabled && bounce.intensity > 0.01f;
        }

        void CreateBounce()
        {
            ground = Terrain.activeTerrain;
            var go = new GameObject("FlareBounce");
            go.transform.SetParent(transform, false);
            bounce = go.AddComponent<Light>();
            bounce.type = LightType.Point;
            bounce.shadows = LightShadows.None;
            var soil = config.bounceSoil;
            float luma = Mathf.Max(0.01f, 0.2126f * soil.r + 0.7152f * soil.g + 0.0722f * soil.b);
            bounce.color = new Color(flareLight.color.r * soil.r / luma, flareLight.color.g * soil.g / luma, flareLight.color.b * soil.b / luma);
            var flareData = UnityEngine.Rendering.Universal.LightExtensions.GetUniversalAdditionalLightData(flareLight);
            uint layers = (flareData != null ? flareData.renderingLayers : (uint)flareLight.renderingLayerMask)
                & ~FiveRivers.Environment.FrontlineVolumetricFog.FogLightLayer;
            var data = UnityEngine.Rendering.Universal.LightExtensions.GetUniversalAdditionalLightData(bounce);
            if (data != null) data.renderingLayers = layers;
            bounce.renderingLayerMask = (int)layers;
            bounce.cullingMask = flareLight.cullingMask;
        }
    }
}
