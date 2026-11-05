using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.Combat
{
    /// <summary>Visual feedback only. Ammo, projectile origin and reload timing remain in Bazooka.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class BazookaPresentation : MonoBehaviour
    {
        [SerializeField] Bazooka bazooka;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] Transform visualFrame;
        [SerializeField] Transform muzzle;
        [SerializeField] Material smokeMaterial;
        [Header("A restrained shoulder impulse for a recoilless launcher")]
        [SerializeField, Min(.01f)] float attackSeconds = .025f;
        [SerializeField, Min(.05f)] float settleSeconds = .42f;
        [SerializeField] Vector3 kickPosition = new Vector3(0f, .004f, -.035f);
        [SerializeField] Vector3 kickEuler = new Vector3(-1.4f, .25f, -.6f);
        [SerializeField, Range(0f, 1f)] float aimedKickScale = .6f;
        [Header("Short muzzle exhaust; existing rear backblast is retained")]
        [SerializeField, Range(0, 32)] int smokeCount = 12;
        [SerializeField, Min(0f)] float flashIntensity = .65f;
        [SerializeField, Min(.01f)] float flashSeconds = .055f;
        Vector3 restPosition;
        Quaternion restRotation;
        ParticleSystem smoke;
        Light flash;
        float firedAt = float.NegativeInfinity;
        float shotScale = 1f;
        bool initialized;

        public bool IsReacting => initialized && Time.time - firedAt < settleSeconds;

        void Awake()
        {
            if (bazooka == null || visualFrame == null || muzzle == null)
            {
                Debug.LogError("Bazooka presentation requires owner, visual frame and muzzle.", this);
                enabled = false;
                return;
            }
            restPosition = visualFrame.localPosition;
            restRotation = visualFrame.localRotation;
            if (smokeMaterial != null) CreateSmoke();
            var lightObject = new GameObject("Bazooka_ShotLight");
            lightObject.transform.SetParent(transform, false);
            flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, .74f, .4f);
            flash.range = 2f;
            flash.shadows = LightShadows.None;
            flash.cullingMask = 1 << 31; // Existing first-person renderer layer.
            flash.enabled = false;
            initialized = true;
        }

        void OnEnable()
        {
            if (bazooka != null) bazooka.Fired += OnFired;
            if (weapons != null) weapons.LoadoutReset += ResetPresentation;
        }

        void OnDisable()
        {
            if (bazooka != null) bazooka.Fired -= OnFired;
            if (weapons != null) weapons.LoadoutReset -= ResetPresentation;
            ResetPresentation();
        }

        void OnFired()
        {
            if (!initialized) return;
            firedAt = Time.time;
            shotScale = Mathf.Lerp(1f, aimedKickScale, bazooka.AimBlend);
            flash.transform.position = muzzle.position;
            flash.enabled = true;
            flash.intensity = flashIntensity;
            if (smoke == null) return;
            smoke.transform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
            smoke.Emit(smokeCount);
        }

        void LateUpdate()
        {
            if (!initialized) return;
            float age = Time.time - firedAt;
            float impulse = age >= 0f && age < settleSeconds
                ? Mathf.SmoothStep(0f, 1f, age / attackSeconds)
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(attackSeconds, settleSeconds, age)))
                : 0f;
            visualFrame.SetLocalPositionAndRotation(restPosition + kickPosition * (impulse * shotScale),
                restRotation * Quaternion.Euler(kickEuler * (impulse * shotScale)));
            flash.enabled = age >= 0f && age < flashSeconds;
            if (flash.enabled) flash.intensity = flashIntensity * (1f - age / flashSeconds);
        }

        void ResetPresentation()
        {
            if (!initialized) return;
            firedAt = float.NegativeInfinity;
            visualFrame.SetLocalPositionAndRotation(restPosition, restRotation);
            flash.enabled = false;
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void CreateSmoke()
        {
            var go = new GameObject("Bazooka_MuzzleExhaust");
            go.transform.SetParent(transform, false);
            smoke = go.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            smoke.useAutoRandomSeed = false;
            smoke.randomSeed = 23871;
            var main = smoke.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .6f;
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.25f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.09f, .18f);
            main.startColor = new Color(.62f, .62f, .59f, .2f);
            var emission = smoke.emission;
            emission.rateOverTime = 0;
            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 17f;
            shape.radius = .035f;
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .2f, 1f, 1.8f));
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.8f, 0f), new GradientAlphaKey(.35f, .35f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
    }
}
