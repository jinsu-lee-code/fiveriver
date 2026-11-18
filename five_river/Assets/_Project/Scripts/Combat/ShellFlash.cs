using UnityEngine;

namespace FiveRivers.Combat
{
    // 포탄이 터지는 순간의 빛 (2026-11-18): 한두 프레임에 가장 밝고 곧 꺼진다. 밝기 · 범위 · 시간은 ShellingConfig.
    public sealed class ShellFlash : MonoBehaviour
    {
        [SerializeField] Light flash;
        float born, peak, seconds;
        bool fired;

        public void Fire(float intensity, float range, float duration)
        {
            if (flash == null) return;
            born = Time.time;
            peak = intensity;
            seconds = Mathf.Max(0.02f, duration);
            flash.range = range;
            flash.intensity = intensity;
            flash.enabled = true;
            fired = true;
        }

        void Update()
        {
            if (!fired) return;
            float u = (Time.time - born) / seconds;
            if (u >= 1f) { flash.enabled = false; fired = false; return; }
            flash.intensity = peak * (1f - u) * (1f - u);
        }
    }
}
