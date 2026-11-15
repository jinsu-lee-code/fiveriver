using UnityEngine;

namespace FiveRivers.Combat
{
    // The North Koreans' signal flare. "The North Koreans used a system of flares to signal various actions and to
    // co-ordinate them ... green flares were used to signal an attack on a given area" (Appleman, South to the
    // Naktong, ch. 19, of the fighting north of Tabu-dong, August 1950). One goes up from behind their line as an
    // assault begins. It is a signal, not illumination: dim, green, and it takes no part in the game's light.
    public sealed class KpaSignalFlare : MonoBehaviour
    {
        public static float Rise = 2.2f, Burn = 6f, Height = 30f, Intensity = 3f, Range = 60f, Size = 2.2f;
        static readonly Color Green = new Color(0.25f, 1f, 0.35f);

        Vector3 from, drift;
        float born;
        Light glow;
        Transform star;
        Material starMaterial;

        public static void Fire(Vector3 from)
        {
            var go = new GameObject("KPA signal flare");
            go.transform.position = from;
            var flare = go.AddComponent<KpaSignalFlare>();
            flare.from = from;
            flare.drift = new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-2f, 2f));
            flare.born = Time.time;
            flare.glow = go.AddComponent<Light>();
            flare.glow.type = LightType.Point; flare.glow.color = Green; flare.glow.range = Range; flare.glow.shadows = LightShadows.None; flare.glow.intensity = 0f;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(go.transform, false);
            flare.star = ball.transform;
            flare.starMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            flare.starMaterial.SetColor("_BaseColor", Green * 3f);
            var renderer = ball.GetComponent<Renderer>();
            renderer.sharedMaterial = flare.starMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the streak it leaves going up
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.9f; trail.startWidth = 0.5f; trail.endWidth = 0f; trail.sharedMaterial = flare.starMaterial;
            trail.startColor = Green; trail.endColor = new Color(Green.r, Green.g, Green.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            float age = Time.time - born;
            if (age >= Rise + Burn) { Destroy(gameObject); return; }
            // up fast, slowing to the top; then a slow fall as it burns out
            float up = age < Rise ? 1f - Mathf.Pow(1f - age / Rise, 2f) : 1f - 0.22f * (age - Rise) / Burn;
            transform.position = from + Vector3.up * (Height * up) + drift * (age / (Rise + Burn));
            float b = age < Rise ? Mathf.Clamp01(age / 0.3f) * 0.6f : Mathf.Clamp01(1f - (age - Rise - Burn * 0.7f) / (Burn * 0.3f));
            b *= 0.85f + 0.15f * Mathf.Sin(age * 31f); // it sputters
            glow.intensity = Intensity * b;
            star.localScale = Vector3.one * Size * Mathf.Lerp(0.2f, 1f, b);
        }

        void OnDestroy()
        {
            if (starMaterial != null) Destroy(starMaterial);
        }
    }
}
