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
        [SerializeField] Animator animator;
        [SerializeField] Transform reloadFrame;
        [SerializeField] Transform rearMouth;
        [SerializeField] Transform rocketInHand;
        [SerializeField] Transform leftUpper, leftForearm, leftWrist, leftGrip;
        [SerializeField] Transform rightUpper, rightForearm, rightWrist, rightGrip;
        [SerializeField] Vector3 hipViewPosition = new Vector3(.20f, -.09f, .36f);
        [SerializeField] Vector3 aimViewPosition;
        [SerializeField] Quaternion aimViewRotation = Quaternion.identity;
        [SerializeField] Vector3 reloadPosition;
        [SerializeField] Vector3 reloadEuler = new Vector3(-9f, 54f, -12f);
        [SerializeField] float rocketLength = .45f;
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
        bool rocketInserted;
        Vector3 leftGripPosition;
        Quaternion leftGripRotation;
        Quaternion rocketGripRotation;
        AnimationEventSink animationEvents;
        static readonly int Reload = Animator.StringToHash("Reload");
        static readonly int Idle = Animator.StringToHash("Idle");

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
            if (animator != null) animationEvents = animator.GetComponent<AnimationEventSink>();
            if (leftGrip != null)
            {
                leftGripPosition = leftGrip.localPosition;
                leftGripRotation = leftGrip.localRotation;
                var acrossRocket = new Vector3(.94f, .33f, .10f);
                rocketGripRotation = Quaternion.LookRotation(acrossRocket, Vector3.Cross(acrossRocket, Vector3.forward))
                    * Quaternion.Inverse(Quaternion.LookRotation(Vector3.forward, Vector3.left)) * leftGripRotation;
            }
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
            if (animationEvents != null) animationEvents.Raised += OnAnimationEvent;
        }

        void OnDisable()
        {
            if (bazooka != null) bazooka.Fired -= OnFired;
            if (weapons != null) weapons.LoadoutReset -= ResetPresentation;
            if (animationEvents != null) animationEvents.Raised -= OnAnimationEvent;
            ResetPresentation();
        }

        void OnAnimationEvent(string cue)
        {
            if (cue == "RocketInsert") rocketInserted = true;
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
            if (animator == null)
                visualFrame.SetLocalPositionAndRotation(restPosition + kickPosition * (impulse * shotScale),
                    restRotation * Quaternion.Euler(kickEuler * (impulse * shotScale)));
            else
                PoseLauncher();
            flash.enabled = age >= 0f && age < flashSeconds;
            if (flash.enabled) flash.intensity = flashIntensity * (1f - age / flashSeconds);
        }

        void PoseLauncher()
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool reloading = bazooka.Reloading && state.shortNameHash == Reload;
            float p = reloading ? Mathf.Clamp01(state.normalizedTime) : 0f;
            float reveal = Ease(.04f, .24f, p) * (1f - Ease(.69f, .98f, p));
            var position = Vector3.Lerp(hipViewPosition, aimViewPosition, bazooka.AimBlend) - transform.localPosition;
            var rotation = Quaternion.Slerp(Quaternion.identity, aimViewRotation, bazooka.AimBlend);
            if (reloading)
            {
                position += reloadPosition * reveal;
                rotation *= Quaternion.Euler(reloadEuler * reveal);
            }
            else if (state.shortNameHash == Idle)
            {
                // 대기의 미세한 흔들림은 견착하면서 줄어들어 실제 가늠쇠가 다시 시선 중앙에 온다.
                rotation *= Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(animator.transform.localRotation), bazooka.AimBlend);
                position -= rotation * (animator.transform.localPosition * bazooka.AimBlend);
            }
            reloadFrame.SetLocalPositionAndRotation(position, rotation);

            if (!reloading) rocketInserted = false;
            if (leftGrip == null || rightGrip == null || rearMouth == null || rocketInHand == null) return;

            float approach = Ease(.23f, .43f, p);
            float insertion = Ease(.43f, .60f, p);
            var mouth = rearMouth.localPosition;
            var approachStart = mouth + new Vector3(-.04f, -.20f, -rocketLength - .065f);
            var atMouth = mouth + Vector3.back * (rocketLength + .018f);
            var seated = mouth + Vector3.forward * .012f;
            var rocketPosition = Vector3.Lerp(Vector3.Lerp(approachStart, atMouth, approach), seated, insertion);
            rocketInHand.SetLocalPositionAndRotation(rocketPosition, Quaternion.Euler(13f * (1f - approach), 0f, 0f));
            rocketInHand.gameObject.SetActive(reloading && p >= .19f && !rocketInserted && p < .62f);

            float reach = Ease(.06f, .22f, p) * (1f - Ease(.62f, .94f, p));
            var carrying = rocketPosition + new Vector3(-.099f, -.064f, rocketLength * .35f);
            var clear = seated + new Vector3(-.10f, -.135f, .065f);
            var hand = Vector3.Lerp(carrying, clear, Ease(.59f, .72f, p));
            leftGrip.localPosition = Vector3.Lerp(leftGripPosition, hand, reach);
            leftGrip.localRotation = Quaternion.Slerp(leftGripRotation, rocketGripRotation, reach);
            FitArm(leftUpper, leftForearm, leftWrist, leftGrip, -.20f);
            FitArm(rightUpper, rightForearm, rightWrist, rightGrip, .20f);
        }

        void FitArm(Transform upper, Transform forearm, Transform wrist, Transform grip, float side)
        {
            var cameraFrame = transform.parent;
            var target = cameraFrame.InverseTransformPoint(grip.position);
            float depth = Mathf.Max(.03f, target.z - .34f);
            float height = Mathf.Min(-.25f, -.82f * depth - .085f);
            // 팔 시작점은 시야 아래에 두고, 손목은 납품 자세에서 손잡이와 장전 소품에 맞춘다.
            var shoulder = new Vector3(Mathf.Lerp(target.x, side, .25f), height, depth);
            upper.position = cameraFrame.TransformPoint(shoulder);
            var elbow = shoulder + new Vector3(side * .36f, -.07f, .18f);
            M1ArmRig.Solve(upper, forearm, wrist, grip, elbowPosition: cameraFrame.TransformPoint(elbow));
        }

        static float Ease(float from, float to, float p) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, p));

        void ResetPresentation()
        {
            if (!initialized) return;
            firedAt = float.NegativeInfinity;
            rocketInserted = false;
            visualFrame.SetLocalPositionAndRotation(restPosition, restRotation);
            if (reloadFrame != null) reloadFrame.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (rocketInHand != null) rocketInHand.gameObject.SetActive(false);
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
