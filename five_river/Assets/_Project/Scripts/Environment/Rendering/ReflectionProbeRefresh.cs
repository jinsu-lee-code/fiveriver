using UnityEngine;

namespace FiveRivers.Environment
{
    // A realtime reflection probe rendered only when the light of the scene changes (2026-11-24): once at the start,
    // then when the ambient light moves on with the daybreak (DawnDirector) or back to night on a new run.
    // Inside the trench the rifles, helmets and wet earth reflect the dark trench walls instead of the open sky;
    // the stream in the valley reflects the dawn sky and its banks.
    // It never renders while a flare or a burst lights the scene (its light would stay in the reflection).
    [RequireComponent(typeof(ReflectionProbe))]
    public sealed class ReflectionProbeRefresh : MonoBehaviour
    {
        [Tooltip("Change of the ambient sky colour (0-1 per channel) that asks for a new render.")]
        [SerializeField] float ambientStep = .04f;
        [SerializeField] float minInterval = 1.5f;

        ReflectionProbe probe;
        Color renderedAmbient = new Color(-1f, -1f, -1f);
        float lastRender = -99f;
        bool pending = true;

        void Awake()
        {
            probe = GetComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        }

        void Update()
        {
            var ambient = RenderSettings.ambientSkyColor;
            if (Mathf.Abs(ambient.r - renderedAmbient.r) + Mathf.Abs(ambient.g - renderedAmbient.g) + Mathf.Abs(ambient.b - renderedAmbient.b) > ambientStep) pending = true;
            if (!pending || Time.unscaledTime - lastRender < minInterval || SceneLitByFlash()) return;
            probe.RenderProbe();
            renderedAmbient = ambient;
            lastRender = Time.unscaledTime;
            pending = false;
        }

        // a flare or a shell burst (brighter than the burning tank, which may still smoke at dawn): fog-layer lights
        static bool SceneLitByFlash()
        {
            foreach (var l in Light.GetLights(LightType.Point, 0))
                if (l.isActiveAndEnabled && l.intensity > 9f && ((uint)l.renderingLayerMask & FrontlineVolumetricFog.FogLightLayer) != 0) return true;
            return false;
        }
    }
}
