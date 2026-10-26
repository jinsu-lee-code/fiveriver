using UnityEngine;

namespace FiveRivers.Combat
{
    // 불빛 깜빡임 (VFX_Tank_Fire 점광원). 결정론이 필요 없는 연출이라 Perlin 잡음을 쓴다.
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [SerializeField] float baseIntensity = 6f;
        [SerializeField] float amount = 0.35f;
        [SerializeField] float speed = 9f;

        Light target;
        float seed;

        void Awake()
        {
            target = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update()
        {
            target.intensity = baseIntensity * (1f - amount + amount * 2f * Mathf.PerlinNoise(seed, Time.time * speed));
        }
    }
}
