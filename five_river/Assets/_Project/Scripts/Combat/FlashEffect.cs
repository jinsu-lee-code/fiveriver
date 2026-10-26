using UnityEngine;

namespace FiveRivers.Combat
{
    // 그레이박스 효과: 점광원과 발광 덩어리가 duration 동안 커지며 사라진다. 최종 VFX가 오면 이 컴포넌트를 뺀다.
    public sealed class FlashEffect : MonoBehaviour
    {
        [SerializeField] float duration = 0.1f;
        [SerializeField] Light flashLight;
        [SerializeField] Transform visual;
        [SerializeField] float endScale = 1.5f;

        float start;
        float baseIntensity;
        Vector3 baseScale;

        void OnEnable()
        {
            start = Time.time;
            if (flashLight != null) baseIntensity = flashLight.intensity;
            if (visual != null) baseScale = visual.localScale;
        }

        void Update()
        {
            float t = duration > 0f ? (Time.time - start) / duration : 1f;
            if (flashLight != null) flashLight.intensity = baseIntensity * Mathf.Clamp01(1f - t);
            if (visual != null)
            {
                visual.localScale = baseScale * Mathf.Lerp(1f, endScale, t);
                if (t >= 1f) visual.gameObject.SetActive(false);
            }
        }
    }
}
