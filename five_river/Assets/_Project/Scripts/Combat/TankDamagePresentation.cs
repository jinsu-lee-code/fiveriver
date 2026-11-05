using System;
using FiveRivers.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.Combat
{
    // Presentation only. TankController owns hits, movement, colliders, fire and goal timing.
    [DisallowMultipleComponent]
    public sealed class TankDamagePresentation : MonoBehaviour
    {
        [Header("Existing scene references")]
        [SerializeField] TankController tank;
        [SerializeField] Effects effects;
        [SerializeField] FlowDirector director;
        [SerializeField] Transform body;
        [Tooltip("MeshRenderer leaves only. Nodes with children or colliders are ignored so muzzle and hit geometry never move.")]
        [SerializeField] Renderer[] recoilRenderers = Array.Empty<Renderer>();
        [SerializeField] Material sparkMaterial;
        [SerializeField] Material smokeMaterial;
        [Tooltip("Existing first-hit smoke only. TankController still owns its activation.")]
        [SerializeField] ParticleSystem[] damageSmokeSystems = Array.Empty<ParticleSystem>();
        [Tooltip("Existing stopped-tank smoke only; exclude fire and embers. No additional looping plume is created.")]
        [SerializeField] ParticleSystem[] stoppedSmokeSystems = Array.Empty<ParticleSystem>();

        [Header("Short armour impact: sparks, then dusty debris")]
        [SerializeField] int randomSeed = 34085;
        [SerializeField, Min(0f)] float impactBoundsPadding = 0.75f;
        [SerializeField, Min(1)] int sparkCount = 24;
        [SerializeField, Min(1)] int ricochetSparkCount = 16;
        [SerializeField, Min(0)] int dustCount = 9;
        [SerializeField] Vector2 sparkSpeed = new Vector2(4f, 9f);
        [SerializeField] Vector2 sparkLifetime = new Vector2(0.16f, 0.42f);
        [SerializeField] Vector2 sparkSize = new Vector2(0.025f, 0.065f);
        [SerializeField] Vector2 dustSpeed = new Vector2(0.45f, 1.5f);
        [SerializeField] Vector2 dustLifetime = new Vector2(0.35f, 0.8f);
        [SerializeField] Vector2 dustSize = new Vector2(0.18f, 0.42f);
        [SerializeField] Color sparkColor = new Color(1f, 0.66f, 0.27f, 0.95f);
        [SerializeField] Color dustColor = new Color(0.36f, 0.32f, 0.27f, 0.38f);
        [SerializeField, Min(0f)] float outwardSpread = 0.65f;
        [SerializeField, Min(0f)] float upwardBias = 0.35f;
        [SerializeField, Min(0f)] float surfaceOffset = 0.06f;
        [SerializeField] Vector3 impactCenterLocal = new Vector3(0f, 1.1f, 0f);
        [SerializeField, Min(1)] int sparkCapacity = 96;
        [SerializeField, Min(1)] int dustCapacity = 36;

        [Header("Heavy, damped suspension response (visual leaves only)")]
        [SerializeField, Min(0.05f)] float reactionDuration = 0.6f;
        [SerializeField, Min(0f)] float rollDegrees = 1.1f;
        [SerializeField, Min(0f)] float lateralTravel = 0.025f;
        [SerializeField, Min(0f)] float suspensionTravel = 0.018f;
        [SerializeField, Min(0f)] float oscillations = 1.2f;
        [SerializeField, Min(0f)] float damping = 4.5f;
        [SerializeField, Range(0f, 1f)] float ricochetReaction = 0.22f;
        [SerializeField, Min(0f)] float stoppedReaction = 1.25f;
        [SerializeField] Vector3 suspensionPivotLocal = new Vector3(0f, 0.5f, 0f);

        [Header("Existing damage smoke: density without oversized billboards")]
        [SerializeField, Min(0f)] float damagedSmokeRate = 1.15f;
        [SerializeField, Min(0f)] float stoppedSmokeRate = 1.35f;
        [SerializeField, Range(0.1f, 2f)] float damageSmokeSize = 0.9f;

        struct VisualPose
        {
            public Transform part;
            public Vector3 position;
            public Quaternion rotation;
        }

        struct SmokeSettings
        {
            public ParticleSystem particles;
            public float emissionRate;
            public float size;
        }

        VisualPose[] visualPoses = Array.Empty<VisualPose>();
        SmokeSettings[] damageSmoke = Array.Empty<SmokeSettings>();
        SmokeSettings[] stoppedSmoke = Array.Empty<SmokeSettings>();
        ParticleSystem sparks;
        ParticleSystem dust;
        Transform runtimeRoot;
        System.Random random;
        Collider impactBoundsCollider;
        DemoFlow subscribedFlow;
        bool subscribed;
        bool reacting;
        float reactionAge;
        float reactionSign = 1f;
        float reactionStrength;

        void Awake()
        {
            random = new System.Random(randomSeed);
            impactBoundsCollider = body != null ? body.GetComponent<Collider>() : null;
            CacheVisualPoses();
            damageSmoke = CacheSmoke(damageSmokeSystems);
            stoppedSmoke = CacheSmoke(stoppedSmokeSystems);
            runtimeRoot = new GameObject("Tank impact particles (reused)").transform;
            runtimeRoot.SetParent(transform, false);
            sparks = CreateParticles("Armour sparks", sparkMaterial, sparkCapacity, true);
            dust = CreateParticles("Armour dust", smokeMaterial, dustCapacity, false);
        }

        void OnEnable()
        {
            if (tank == null || effects == null || body == null) return;
            tank.SideHit += OnSideHit;
            tank.Stopped += OnStopped;
            tank.Ricocheted += OnRicocheted;
            effects.Spawned += OnEffectSpawned;
            subscribedFlow = director != null ? director.Flow : null;
            if (subscribedFlow != null)
            {
                subscribedFlow.SectionChanged += OnSectionChanged;
                subscribedFlow.RetryRequested += OnRetry;
            }
            subscribed = true;
        }

        void OnDisable()
        {
            if (subscribed)
            {
                if (tank != null)
                {
                    tank.SideHit -= OnSideHit;
                    tank.Stopped -= OnStopped;
                    tank.Ricocheted -= OnRicocheted;
                }
                if (effects != null) effects.Spawned -= OnEffectSpawned;
                if (subscribedFlow != null)
                {
                    subscribedFlow.SectionChanged -= OnSectionChanged;
                    subscribedFlow.RetryRequested -= OnRetry;
                }
            }
            subscribed = false;
            subscribedFlow = null;
            ResetPresentation();
        }

        void OnDestroy()
        {
            // All particle systems are owned children; no cloned materials or meshes are allocated.
            ResetPresentation();
            if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
        }

        void CacheVisualPoses()
        {
            var poses = new VisualPose[recoilRenderers.Length];
            int count = 0;
            for (int i = 0; i < recoilRenderers.Length; i++)
            {
                var renderer = recoilRenderers[i];
                if (!(renderer is MeshRenderer)) continue;
                var part = renderer.transform;
                if (body == null || !part.IsChildOf(body) || part == body || part.childCount != 0 || part.GetComponent<Collider>() != null) continue;
                bool duplicate = false;
                for (int j = 0; j < count; j++) duplicate |= poses[j].part == part;
                if (duplicate) continue;
                poses[count++] = new VisualPose { part = part, position = part.localPosition, rotation = part.localRotation };
            }
            Array.Resize(ref poses, count);
            visualPoses = poses;
        }

        static SmokeSettings[] CacheSmoke(ParticleSystem[] systems)
        {
            var settings = new SmokeSettings[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var particles = systems[i];
                if (particles == null) continue;
                settings[i] = new SmokeSettings
                {
                    particles = particles,
                    emissionRate = particles.emission.rateOverTimeMultiplier,
                    size = particles.main.startSizeMultiplier
                };
            }
            return settings;
        }

        ParticleSystem CreateParticles(string objectName, Material material, int capacity, bool streaks)
        {
            if (material == null) return null;
            var root = new GameObject(objectName);
            root.transform.SetParent(runtimeRoot, false);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false;
            particles.randomSeed = unchecked((uint)randomSeed);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = Mathf.Max(1, capacity);
            main.gravityModifier = streaks ? 0.65f : -0.015f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var size = particles.sizeOverLifetime;
            size.enabled = !streaks;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.6f));
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 1.8f;
            renderer.velocityScale = 0.06f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        void OnEffectSpawned(EffectKind kind, Vector3 point)
        {
            if (tank == null || !tank.Visible || body == null) return;
            bool ricochet = kind == EffectKind.TankRicochet;
            if (!ricochet && kind != EffectKind.TankHitExplosion) return;
            if (impactBoundsCollider != null && impactBoundsCollider.bounds.SqrDistance(point) > impactBoundsPadding * impactBoundsPadding) return;
            var outward = point - body.TransformPoint(impactCenterLocal);
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.001f ? outward.normalized : body.right;
            EmitImpact(sparks, point, outward, ricochet ? ricochetSparkCount : sparkCount,
                sparkSpeed, sparkLifetime, sparkSize, sparkColor);
            if (!ricochet)
                EmitImpact(dust, point, outward, dustCount, dustSpeed, dustLifetime, dustSize, dustColor);
            reactionSign = Vector3.Dot(outward, body.right) >= 0f ? -1f : 1f;

        }

        void EmitImpact(ParticleSystem particles, Vector3 point, Vector3 outward, int count,
            Vector2 speed, Vector2 lifetime, Vector2 size, Color color)
        {
            if (particles == null) return;
            particles.Play(false);
            var emit = new ParticleSystem.EmitParams();
            emit.startColor = color;
            for (int i = 0; i < count; i++)
            {
                var scatter = new Vector3(SignedRandom(), SignedRandom(), SignedRandom());
                var direction = (outward + scatter * outwardSpread + Vector3.up * upwardBias).normalized;
                emit.position = point + outward * surfaceOffset;
                emit.velocity = direction * Range(speed);
                emit.startLifetime = Range(lifetime);
                emit.startSize = Range(size);
                emit.rotation = Range(new Vector2(0f, 360f));
                particles.Emit(emit, 1);
            }
        }

        float SignedRandom() => (float)random.NextDouble() * 2f - 1f;
        float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

        void OnRicocheted()
        {
            BeginReaction(ricochetReaction);
        }

        void OnSideHit(int hits)
        {
            if (hits <= 0) return;
            ApplySmoke(damageSmoke, damagedSmokeRate, damageSmokeSize);
            BeginReaction(1f);
        }

        void OnStopped()
        {
            ApplySmoke(stoppedSmoke, stoppedSmokeRate, damageSmokeSize);
            BeginReaction(stoppedReaction);
        }

        void BeginReaction(float strength)
        {
            RestoreVisualPoses();
            reactionAge = 0f;
            reactionStrength = strength;
            reacting = true;
        }

        void LateUpdate()
        {
            if (!reacting || body == null) return;
            if (!body.gameObject.activeInHierarchy)
            {
                ResetPresentation();
                return;
            }
            reactionAge += Time.deltaTime;
            float t = Mathf.Clamp01(reactionAge / reactionDuration);
            if (t >= 1f)
            {
                RestoreVisualPoses();
                reacting = false;
                return;
            }
            float wave = Mathf.Sin(t * oscillations * Mathf.PI * 2f) * Mathf.Exp(-damping * t) * reactionStrength;
            var tilt = Quaternion.AngleAxis(wave * rollDegrees * reactionSign, body.forward);
            var pivot = body.TransformPoint(suspensionPivotLocal);
            var displacement = body.right * (wave * lateralTravel * reactionSign)
                - body.up * (Mathf.Abs(wave) * suspensionTravel);
            for (int i = 0; i < visualPoses.Length; i++)
            {
                var pose = visualPoses[i];
                if (pose.part == null) continue;
                var parent = pose.part.parent;
                var position = parent != null ? parent.TransformPoint(pose.position) : pose.position;
                var rotation = parent != null ? parent.rotation * pose.rotation : pose.rotation;
                pose.part.SetPositionAndRotation(pivot + tilt * (position - pivot) + displacement, tilt * rotation);
            }
        }

        static void ApplySmoke(SmokeSettings[] settings, float rateScale, float sizeScale)
        {
            for (int i = 0; i < settings.Length; i++)
            {
                var state = settings[i];
                if (state.particles == null) continue;
                var emission = state.particles.emission;
                emission.rateOverTimeMultiplier = state.emissionRate * rateScale;
                var main = state.particles.main;
                main.startSizeMultiplier = state.size * sizeScale;
            }
        }

        void OnRetry(FlowCheckpoint checkpoint) => ResetPresentation();

        void OnSectionChanged(FlowTransition transition)
        {
            // Stopped immediately advances to Dawn. Keep the final impact and fire readable there.
            bool hiddenSection = transition.To == FlowSection.Title || transition.To == FlowSection.Tutorial
                || transition.To == FlowSection.Scout || transition.To == FlowSection.Assault
                || transition.To == FlowSection.Silence;
            bool freshTank = transition.To == FlowSection.Tank && transition.From != FlowSection.Tank;
            if (hiddenSection || freshTank) ResetPresentation();
        }

        void RestoreVisualPoses()
        {
            for (int i = 0; i < visualPoses.Length; i++)
            {
                var pose = visualPoses[i];
                if (pose.part == null) continue;
                pose.part.localPosition = pose.position;
                pose.part.localRotation = pose.rotation;
            }
        }

        static void ClearSmoke(SmokeSettings[] settings)
        {
            for (int i = 0; i < settings.Length; i++)
                if (settings[i].particles != null)
                    settings[i].particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void ResetPresentation()
        {
            reacting = false;
            random = new System.Random(randomSeed);
            RestoreVisualPoses();
            ApplySmoke(damageSmoke, 1f, 1f);
            ApplySmoke(stoppedSmoke, 1f, 1f);
            ClearSmoke(damageSmoke);
            ClearSmoke(stoppedSmoke);
            if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}



