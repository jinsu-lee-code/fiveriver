using UnityEngine;

namespace FiveRivers.Combat
{
    // 포탄이 떨어진 자리에서 한동안 피어오르는 연기 (2026-11-18). 처음에는 짙고, 정한 시간에 걸쳐 가늘어지다 그친다.
    public sealed class Smolder : MonoBehaviour
    {
        [SerializeField] ParticleSystem smoke;
        float born, seconds, rate;
        bool burning;

        public void Burn(float duration)
        {
            if (smoke == null) return;
            born = Time.time;
            seconds = Mathf.Max(1f, duration);
            rate = smoke.emission.rateOverTime.constant;
            burning = true;
        }

        void Update()
        {
            if (!burning) return;
            float u = (Time.time - born) / seconds;
            var emission = smoke.emission;
            if (u >= 1f)
            {
                emission.rateOverTime = 0f;
                burning = false;
                Destroy(gameObject, smoke.main.startLifetime.constantMax + 1f);
                return;
            }
            emission.rateOverTime = rate * (1f - u) * (1f - u * 0.5f);
        }
    }
}
