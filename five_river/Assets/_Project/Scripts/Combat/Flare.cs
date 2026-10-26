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
            flareLight.intensity = config.lightIntensity * b;
            flareLight.range = config.lightRange;
            visual.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.4f, b);
        }
    }
}
