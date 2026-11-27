using UnityEngine;

namespace FiveRivers.Combat
{
    // The North Koreans' signal flare. "The North Koreans used a system of flares to signal various actions and to
    // co-ordinate them ... green flares were used to signal an attack on a given area" (Appleman, South to the
    // Naktong, ch. 19, of the fighting north of Tabu-dong, August 1950). One goes up from behind their line as an
    // assault begins. It is a signal, not illumination: dim, green, and it takes no part in the game's light.
    public sealed class KpaSignalFlare : MonoBehaviour
    {
        // 2026-11-27 (아트 검수 "조명탄 머리가 연녹색 납작 원반 · 녹색 세로 가시로 보임" — 녹색은 이 신호탄의 의도, 모양만 정리):
        // the star was a 2.2 m unlit ball (a flat pale-green disc from the trench) and its trail a 0.5 m wide band (a green spike
        // under it). Now a small bright core that the bloom spreads, and a thin streak only while it climbs. BALANCE 2-5.
        public static float Rise = 2.2f, Burn = 6f, Height = 30f, Intensity = 3f, Range = 60f, Size = 1.0f;
        public static float TrailWidth = 0.12f, TrailSeconds = 0.45f, CoreBright = 4f;
        // the star's own colour: a purer green than the light's, so that at CoreBright its red and blue stay under 1 and the tone
        // curve leaves it green (at (0.25, 1, 0.35) x 6 all three channels clipped: a white dot among the stars, 0 % green)
        static readonly Color Core = new Color(0.12f, 1f, 0.22f);
        static readonly Color Green = new Color(0.25f, 1f, 0.35f);

        Vector3 from, drift;
        float born;
        Light glow;
        TrailRenderer streak;
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
            FiveRivers.Environment.FrontlineVolumetricFog.MarkLight(flare.glow); // a faint green glow in the night haze (2026-11-24)
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(go.transform, false);
            flare.star = ball.transform;
            // a player build strips shaders no material asset uses, so Unlit can be missing there (2026-11-17 smoke run):
            // fall back to shaders that are always in the build instead of throwing in the middle of a section change
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Lit");
            flare.starMaterial = new Material(shader);
            if (flare.starMaterial.HasProperty("_Color")) flare.starMaterial.SetColor("_Color", Core * CoreBright);
            if (flare.starMaterial.HasProperty("_EmissionColor")) { flare.starMaterial.EnableKeyword("_EMISSION"); flare.starMaterial.SetColor("_EmissionColor", Core * CoreBright); }
            flare.starMaterial.SetColor("_BaseColor", Core * CoreBright);
            var renderer = ball.GetComponent<Renderer>();
            renderer.sharedMaterial = flare.starMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the streak it leaves going up
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = TrailSeconds; trail.startWidth = TrailWidth; trail.endWidth = 0f; trail.sharedMaterial = flare.starMaterial;
            trail.startColor = new Color(Green.r, Green.g, Green.b, 0.5f); trail.endColor = new Color(Green.r, Green.g, Green.b, 0f);
            flare.streak = trail;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            float age = Time.time - born;
            if (age >= Rise + Burn) { Destroy(gameObject); return; }
            // up fast, slowing to the top; then a slow fall as it burns out
            if (streak != null && age >= Rise * 0.85f) streak.emitting = false;   // no streak hanging from it as it burns
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
