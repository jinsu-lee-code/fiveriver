using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Presentation only. Dawn timing and result progression remain owned by DemoFlow.
    public sealed class DawnTruckSequence : MonoBehaviour
    {
        [Header("Truck-local route, ordered rear passenger first")]
        public Transform[] passengerSockets;
        public Transform[] exitPath;
        public Transform[] joinPath;
        public Transform[] wheelPivots;
        public GameObject exitRamp;
        public AudioSource engineSource;
        [Min(.1f)] public float approachDuration = 4f;
        [Min(0f)] public float stopSettle = .6f;
        [Min(.1f)] public float departureInterval = .9f;
        [Min(.01f)] public float wheelRadius = .48f;
        [Min(.1f)] public float turnSpeed = 240f;
        // 2026-11-24: the students walk A_ROK_Walk_Tired_v2 (AOC_ROK_Student; knees bent, head down - the old clip read as a
        // straight-legged parade step). It covers 1.0 m/s at rate 1, so the rate is the walking speed (1.1 m/s).
        [Min(.1f)] public float walkPlaybackRate = 1.1f;
        [Range(1f, 45f)] public float moveAngleTolerance = 15f;

        [Header("Narrated arrival: skip exposed dismount/walking")]
        public bool narratedArrival = true;
        public Vector3 narratedJoinOffset = new Vector3(-6f, 0f, -1.4f);
        [Header("Jump-down arrival (2026-11-16): ride in the back, walk to the tailgate, jump, gather behind the truck")]
        public bool jumpDown = true;
        public string dismountState = "Idle_Stand"; // the pose under DismountPose (no dismount clip exists)
        [Min(.1f)] public float dismountLength = 1.85f;        // A_ROK_Dismount_Truck
        public float dismountTakeoff = .42f, dismountLand = 1.06f; // feet leave the tailgate / touch the ground (clip time)
        [Min(0f)] public float landingReach = 1.2f;             // how far past the tailgate the feet come down
        public Vector3 gatherOrigin = new Vector3(2.6f, 0f, -5.4f); // truck-local spot of the first man, the others to his right; just behind the tailgate so the summit's second shot sees them whole beside the truck (at -7.4 they stood in front of that camera, filling a quarter of the frame)
        public event System.Action Arrived;
        // 2026-11-18: the landing of the first man raised Arrived on every frame from touchdown until he walked off
        // (about 40 frames). Each one queued the 7-second arrival narration again, and the lines spoken at the summit
        // afterwards ("포항에서 왔습니다", "만수 자리다") waited behind minutes of it and were never shown.
        bool arrivedRaised;
        void RaiseArrived()
        {
            if (arrivedRaised) return;
            arrivedRaised = true;
            Arrived?.Invoke();
        }
        bool arrivalPresented;
        Camera arrivalView;

        sealed class Passenger
        {
            public Transform body;
            public Animator animator;
            public DawnFootPlacement footPlacement;
            public readonly List<Vector3> route = new List<Vector3>();
            public int next;
            public int groundFrom;
            public bool released;
            public int stage;
            public float stageTime;
            public Vector3 jumpFrom, jumpTo;
            public DismountPose pose;
            public Quaternion face;
        }

        readonly List<Passenger> passengers = new List<Passenger>();
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly float[] StudentHeights = { 1f, 0.955f, 1.035f, 0.975f };
        static readonly float[] StudentPace = { 1f, 0.9f, 1.08f, 0.95f };
        static readonly Vector3[] StudentHeads = { Vector3.zero, new Vector3(6f, -14f, 0f), new Vector3(-4f, 9f, 3f), new Vector3(9f, 4f, -3f) };

        Terrain ground;
        Transform joinEnd;
        Vector3 approachStart, approachEnd;
        Quaternion startRotation, stopRotation;
        float elapsed, walkingSpeed;
        bool stopped, audioPaused, engineEnded;
        float engineVolume;

        public int PassengerCount => passengers.Count;
        public Transform JoinedLead => JoinedCount > 0 && passengers.Count > 0 ? passengers[0].body : null;
        public int JoinedCount { get; private set; }
        public bool Stopped => stopped;

        public bool Begin(GameObject passengerPrefab, int count, Transform destination,
            float speed, Terrain terrain, Transform arrivalStart, Transform arrivalStop)
        {
            // Validate before spawning so missing authoring data cannot produce floating passengers.
            if (passengerPrefab == null || destination == null || arrivalStart == null || arrivalStop == null
                || count < 1 || passengerSockets == null || passengerSockets.Length < count
                || exitPath == null || exitPath.Length < 2)
                return false;
            for (int i = 0; i < count; i++) if (passengerSockets[i] == null) return false;
            foreach (var point in exitPath) if (point == null) return false;
            if (joinPath != null) foreach (var point in joinPath) if (point == null) return false;

            ground = terrain;
            joinEnd = destination;
            walkingSpeed = Mathf.Max(.1f, speed);
            approachStart = arrivalStart.position;
            approachEnd = arrivalStop.position;
            startRotation = arrivalStart.rotation;
            stopRotation = arrivalStop.rotation;
            Ground(ref approachStart);
            Ground(ref approachEnd);
            transform.SetPositionAndRotation(approachStart, startRotation);
            elapsed = 0f;
            stopped = false;
            JoinedCount = 0; arrivedRaised = false;
            arrivalPresented = false;
            arrivalView = Camera.main;
            audioPaused = false;
            engineEnded = false;
            if (engineSource != null && !engineEnded)
            {
                engineVolume = engineSource.volume;
                engineSource.Play();
            }
            if (exitRamp != null) exitRamp.SetActive(false);
            for (int i = 0; i < count; i++)
            {
                var socket = passengerSockets[i];
                var body = Instantiate(passengerPrefab, socket.position, socket.rotation, transform).transform;
                body.name = "DawnStudent_" + (i + 1);
                var animator = body.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.SetFloat(SpeedId, 0f);
                    animator.Play("Idle_Stand", 0, i * .23f);
                }
                // four copies of one model in a row: each a little taller or shorter, idling at his own pace and
                // holding his head his own way, so they do not read as one boy four times (2026-11-15)
                body.localScale *= StudentHeights[i % StudentHeights.Length];
                body.gameObject.AddComponent<IdleVariation>().Set(StudentPace[i % StudentPace.Length], StudentHeads[i % StudentHeads.Length]);

                var footPlacement = body.gameObject.AddComponent<DawnFootPlacement>();
                footPlacement.Configure(animator, transform, exitPath[1], exitPath[exitPath.Length - 1], ground);
                var pose = body.gameObject.AddComponent<DismountPose>();
                pose.Configure(animator);
                passengers.Add(new Passenger { body = body, animator = animator, footPlacement = footPlacement, pose = pose });
                if (narratedArrival && !jumpDown)
                {
                    footPlacement.enabled = false;
                    body.gameObject.SetActive(false);
                }
            }
            return true;
        }

        // The ending letter owns Result audio; the new arrival engine must not bleed into it.
        // A staged camera cut (SummitDawnDirector) shows the men where they stand: no on-screen pop across a cut,
        // so they are revealed at once instead of waiting until the player looks away.
        bool revealAnywhere;
        public void RevealNow()
        {
            if (jumpDown) { foreach (var p in passengers) if (p.body != null) p.body.gameObject.SetActive(true); return; }
            if (!narratedArrival || !stopped) return;
            revealAnywhere = true;
            elapsed = Mathf.Max(elapsed, Mathf.Max(.1f, approachDuration) + stopSettle);
            PresentStationaryArrival();
        }

        public void StopEngine()
        {
            engineEnded = true;
            if (engineSource != null) engineSource.Stop();
        }
        public void SetPaused(bool paused)
        {
            if (audioPaused == paused) return;
            audioPaused = paused;
            if (engineSource == null || engineEnded) return;
            if (paused) engineSource.Pause(); else engineSource.UnPause();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || audioPaused) return;
            elapsed += deltaTime;
            float duration = Mathf.Max(.1f, approachDuration);
            if (engineSource != null && !engineEnded)
            {
                float braking = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duration * .65f, duration + stopSettle, elapsed));
                engineSource.volume = engineVolume * Mathf.Lerp(1f, .35f, braking);
                engineSource.pitch = Mathf.Lerp(1f, .75f, braking);
            }
            if (!stopped)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease into and out of motion; no abrupt full-speed stop.
                float eased = t * t * (3f - 2f * t);
                Vector3 before = transform.position;
                Vector3 position = Vector3.Lerp(approachStart, approachEnd, eased);
                Ground(ref position);
                transform.SetPositionAndRotation(position, Quaternion.Slerp(startRotation, stopRotation, eased));
                if (wheelPivots != null)
                {
                    float angle = Vector3.Distance(before, position) / Mathf.Max(.01f, wheelRadius) * Mathf.Rad2Deg;
                    foreach (var wheel in wheelPivots)
                        if (wheel != null) wheel.Rotate(Vector3.right, angle, Space.Self);
                }
                if (t < 1f) return;
                stopped = true;
            }
            if (elapsed < duration + stopSettle) return;
            if (jumpDown) { TickJumpDown(deltaTime, duration); return; }
            if (narratedArrival)
            {
                PresentStationaryArrival();
                return;
            }
            if (exitRamp != null && !exitRamp.activeSelf) exitRamp.SetActive(true);
            for (int i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                if (passenger.body == null) continue;
                if (!passenger.released)
                {
                    if (elapsed < duration + stopSettle + i * departureInterval) continue;
                    Release(passenger, i);
                }
                Walk(passenger, deltaTime);
            }
        }

        // Forward offsets and headings relative to the authored join marker.
        static readonly float[] JoinDepth = { .22f, -.48f, .42f, -.16f };
        static readonly float[] JoinYaw = { 14f, -8f, -17f, 7f };

        void PresentStationaryArrival()
        {
            if (!arrivalPresented)
            {
                arrivalPresented = true;
                for (int i = 0; i < passengers.Count; i++)
                {
                    var passenger = passengers[i];
                    if (passenger.body == null) continue;
                    float lateral = (i - (passengers.Count - 1) * .5f) * 1.1f;
                    int slot = i % JoinDepth.Length;
                    var offset = new Vector3(lateral, 0f, JoinDepth[slot]);
                    var position = joinEnd.TransformPoint(narratedJoinOffset + offset);
                    Ground(ref position);
                    var heading = joinEnd.rotation * Quaternion.Euler(0f, JoinYaw[slot], 0f);
                    passenger.body.SetPositionAndRotation(position, heading);
                    passenger.released = true;
                    JoinedCount++;
                }
                RaiseArrived();
            }
            // Reveal only outside the player's view: never show an on-screen teleport.
            var planes = arrivalView != null ? GeometryUtility.CalculateFrustumPlanes(arrivalView) : null;
            for (int i = 0; i < passengers.Count; i++)
            {
                var passenger = passengers[i];
                if (passenger.body == null || passenger.body.gameObject.activeSelf) continue;
                var bounds = new Bounds(passenger.body.position + Vector3.up, new Vector3(1.2f, 2.2f, 1.2f));
                if (!revealAnywhere && planes != null && GeometryUtility.TestPlanesAABB(planes, bounds)) continue;
                passenger.body.gameObject.SetActive(true);
                if (passenger.animator != null)
                {
                    passenger.animator.speed = 1f;
                    passenger.animator.SetFloat(SpeedId, 0f);
                    passenger.animator.Play("Idle_Stand", 0, Mathf.Repeat(i * .23f, 1f));
                    passenger.animator.Update(0f);
                }
            }
        }

        void Release(Passenger passenger, int index)
        {
            passenger.released = true;
            if (passenger.footPlacement != null) passenger.footPlacement.enabled = true;
            // Keep all passengers owned by this sequence for one-step restart/destruction cleanup.
            // First move sideways into the aisle at the passenger's own longitudinal position.
            Vector3 local = transform.InverseTransformPoint(passenger.body.position);
            Vector3 aisle = transform.InverseTransformPoint(exitPath[0].position);
            local.x = aisle.x;
            passenger.route.Add(transform.TransformPoint(local));
            foreach (var point in exitPath) passenger.route.Add(point.position);
            passenger.groundFrom = passenger.route.Count - 1;
            if (joinPath != null) foreach (var point in joinPath) passenger.route.Add(point.position);
            float lateral = (index - (passengers.Count - 1) * .5f) * 1.1f;
            Vector3 destination = joinEnd.position + joinEnd.right * lateral;
            passenger.route.Add(destination);
            for (int n = passenger.groundFrom; n < passenger.route.Count; n++)
            {
                Vector3 position = passenger.route[n];
                Ground(ref position);
                passenger.route[n] = position;
            }
            if (passenger.animator != null)
            {
                passenger.animator.speed = walkPlaybackRate;
                passenger.animator.SetFloat(SpeedId, walkingSpeed);
                passenger.animator.CrossFadeInFixedTime("Walk_Tired", .15f);
            }
        }

        void Walk(Passenger passenger, float deltaTime)
        {
            if (passenger.next >= passenger.route.Count) return;
            Vector3 target = passenger.route[passenger.next];
            Vector3 direction = target - passenger.body.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .0001f)
                passenger.body.rotation = Quaternion.RotateTowards(passenger.body.rotation,
                    Quaternion.LookRotation(direction), turnSpeed * deltaTime);
            // The feet must turn with the body before advancing around a sharp route corner.
            // Keep the walking state during the short pivot; do not bounce through Idle_Stand.
            float angle = direction.sqrMagnitude > .0001f ? Vector3.Angle(passenger.body.forward, direction) : 0f;
            float advance = Mathf.InverseLerp(moveAngleTolerance, 0f, angle);
            if (passenger.animator != null)
                passenger.animator.speed = Mathf.Max(.35f, walkPlaybackRate * advance);
            Vector3 position = Vector3.MoveTowards(passenger.body.position, target, walkingSpeed * advance * deltaTime);
            // Ramp nodes have authored heights. Ground sampling starts only after reaching the ramp foot.
            if (passenger.next > passenger.groundFrom) Ground(ref position);
            passenger.body.position = position;
            if ((position - target).sqrMagnitude > .0004f) return;
            passenger.next++;
            if (passenger.next != passenger.route.Count) return;
            JoinedCount++;
            if (passenger.footPlacement != null) passenger.footPlacement.enabled = false;
            if (passenger.animator != null)
            {
                passenger.animator.speed = 1f;
                passenger.animator.SetFloat(SpeedId, 0f);
            }
        }

        // ---- jump-down arrival -------------------------------------------------------------------------
        // gather on the edge, push off, legs drawn up in the air, a deep squat on landing, up again (seconds)
        // 2026-11-17, measured frame by frame: with the old curves he was already in the full squat in the air (hips
        // 0.33 m over his feet at touchdown), then sat there for half a second, and the last 13 cm came back in one
        // frame when the walk began. Now he comes down with the legs nearly straight (0.30), the landing drives him
        // into the squat over 0.14 s (0.92 at 1.20 s), and he rises out of it as he steps off; what is left when the
        // walk begins is let out gradually (EaseOutOfSquat). Keys are smoothed so the motion does not pause on each.
        public static readonly AnimationCurve CrouchCurve = Smooth(new Keyframe(0f, 0f), new Keyframe(.28f, .45f), new Keyframe(.42f, .08f), new Keyframe(.75f, .12f), new Keyframe(1.06f, .30f), new Keyframe(1.20f, .92f), new Keyframe(1.38f, .78f), new Keyframe(1.65f, .30f), new Keyframe(1.85f, 0f));
        public static readonly AnimationCurve TuckCurve = Smooth(new Keyframe(.42f, 0f), new Keyframe(.62f, .75f), new Keyframe(.90f, .40f), new Keyframe(1.06f, 0f));
        public static readonly AnimationCurve LeanCurve = Smooth(new Keyframe(0f, 0f), new Keyframe(.28f, .35f), new Keyframe(.42f, .2f), new Keyframe(.75f, .3f), new Keyframe(1.06f, .5f), new Keyframe(1.22f, 1f), new Keyframe(1.40f, .8f), new Keyframe(1.65f, .35f), new Keyframe(1.85f, 0f));
        const float SquatRelease = 2.2f; // per second, once he is walking

        static AnimationCurve Smooth(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        // the rest of the landing squat goes out of him over his first steps instead of in one frame
        static void EaseOutOfSquat(Passenger p, float deltaTime)
        {
            if (p.pose == null || !p.pose.enabled) return;
            p.pose.Tuck = 0f;
            p.pose.Crouch = Mathf.MoveTowards(p.pose.Crouch, 0f, SquatRelease * deltaTime);
            p.pose.Lean = Mathf.MoveTowards(p.pose.Lean, 0f, SquatRelease * deltaTime);
            if (p.pose.Crouch <= 0f && p.pose.Lean <= 0f) p.pose.enabled = false;
        }

        void TickJumpDown(float deltaTime, float duration)
        {
            float start = duration + stopSettle;
            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                if (p.body == null) continue;
                if (!p.released)
                {
                    if (elapsed < start + i * departureInterval) continue;
                    BeginJumpDown(p);
                }
                StepJumpDown(p, i, deltaTime);
            }
        }
        void BeginJumpDown(Passenger p)
        {
            p.released = true;
            p.route.Clear();
            p.next = 0;
            p.stage = 0;
            // along the aisle of the cargo bed to the tailgate, at bed height
            Vector3 local = transform.InverseTransformPoint(p.body.position);
            Vector3 edge = transform.InverseTransformPoint(exitPath[1].position);
            p.route.Add(transform.TransformPoint(new Vector3(edge.x, local.y, local.z)));
            p.route.Add(transform.TransformPoint(new Vector3(edge.x, local.y, edge.z)));
            SetWalk(p, true);
        }
        void StepJumpDown(Passenger p, int index, float deltaTime)
        {
            switch (p.stage)
            {
                case 0:
                    if (!WalkRoute(p, deltaTime, false)) return;
                    p.stage = 1;
                    p.stageTime = 0f;
                    var back = -transform.forward; back.y = 0f; back.Normalize();
                    p.jumpFrom = p.body.position;
                    p.jumpTo = p.jumpFrom + back * landingReach;
                    Ground(ref p.jumpTo);
                    p.body.rotation = Quaternion.LookRotation(back);
                    if (p.animator != null)
                    {
                        p.animator.speed = 1f;
                        p.animator.SetFloat(SpeedId, 0f);
                        string pose = p.animator.isInitialized && !p.animator.HasState(0, Animator.StringToHash(dismountState)) ? "Idle_Stand" : dismountState; // an instance may still carry the removed "Dismount" state name
                        if (p.animator.isInitialized && p.animator.HasState(0, Animator.StringToHash(pose))) p.animator.CrossFadeInFixedTime(pose, .1f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        else Debug.LogWarning($"[DawnTruck] dismount pose state '{pose}' (asked '{dismountState}') unavailable: initialized {p.animator.isInitialized} controller {(p.animator.runtimeAnimatorController != null ? p.animator.runtimeAnimatorController.name : "none")} active {p.animator.isActiveAndEnabled}");
#endif // the state may be missing on a variant rig: keep the current pose rather than log an error every frame
                    }
                    if (p.pose != null) p.pose.enabled = true;
                    return;
                case 1:
                {
                    p.stageTime += deltaTime;
                    float t = p.stageTime;
                    // a ballistic hop off the tailgate that touches down exactly at the clip's landing frame
                    float flight = Mathf.Max(.05f, dismountLand - dismountTakeoff);
                    float fall = p.jumpFrom.y - p.jumpTo.y;
                    float up = (.5f * 9.81f * flight * flight - fall) / flight;
                    float air = Mathf.Clamp(t - dismountTakeoff, 0f, flight);
                    Vector3 position = Vector3.Lerp(p.jumpFrom, p.jumpTo, Mathf.SmoothStep(0f, 1f, air / flight));
                    position.y = t >= dismountLand ? p.jumpTo.y : p.jumpFrom.y + up * air - .5f * 9.81f * air * air;
                    p.body.position = position;
                    if (p.pose != null) { p.pose.Crouch = Mathf.Clamp01(CrouchCurve.Evaluate(t)); p.pose.Tuck = Mathf.Clamp01(TuckCurve.Evaluate(t)); p.pose.Lean = Mathf.Clamp01(LeanCurve.Evaluate(t)); }
                    if (t >= dismountLand && JoinedCount == 0 && index == 0) RaiseArrived();
                    if (t < dismountLength - .2f) return;
                    p.stage = 2; // the pose stays on: EaseOutOfSquat lets the rest of the squat out as he walks
                    p.route.Clear();
                    p.next = 0;
                    var slot = gatherOrigin + new Vector3(index * 1.05f, 0f, (index % 2) * .45f);
                    var target = transform.TransformPoint(slot);
                    Ground(ref target);
                    p.route.Add(target);
                    SetWalk(p, true);
                    return;
                }
                case 2:
                    EaseOutOfSquat(p, deltaTime);
                    if (!WalkRoute(p, deltaTime, true)) return;
                    p.stage = 3;
                    JoinedCount++;
                    SetWalk(p, false);
                    // stand facing the way the truck came from, toward the line they have come to hold
                    var face = -transform.forward; face.y = 0f;
                    p.face = Quaternion.LookRotation(Quaternion.Euler(0f, JoinYaw[index % JoinYaw.Length], 0f) * face);
                    return;
                case 3:
                    EaseOutOfSquat(p, deltaTime);
                    // turn on the spot to face the line, at a walking turn rate instead of a one-frame snap
                    p.body.rotation = Quaternion.RotateTowards(p.body.rotation, p.face, 120f * deltaTime);
                    return;
            }
        }
        void SetWalk(Passenger p, bool walking)
        {
            if (p.animator == null) return;
            p.animator.speed = walking ? walkPlaybackRate : 1f;
            p.animator.SetFloat(SpeedId, walking ? walkingSpeed : 0f);
            p.animator.CrossFadeInFixedTime(walking ? "Walk_Tired" : "Idle_Stand", .2f);
        }
        // true when the last route point is reached
        bool WalkRoute(Passenger p, float deltaTime, bool onGround)
        {
            if (p.next >= p.route.Count) return true;
            Vector3 target = p.route[p.next];
            Vector3 direction = target - p.body.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .0001f)
                p.body.rotation = Quaternion.RotateTowards(p.body.rotation, Quaternion.LookRotation(direction), turnSpeed * deltaTime);
            float angle = direction.sqrMagnitude > .0001f ? Vector3.Angle(p.body.forward, direction) : 0f;
            float advance = Mathf.InverseLerp(moveAngleTolerance, 0f, angle);
            if (p.animator != null) p.animator.speed = Mathf.Max(.35f, walkPlaybackRate * advance);
            Vector3 position = Vector3.MoveTowards(p.body.position, target, walkingSpeed * advance * deltaTime);
            if (onGround) Ground(ref position); else position.y = target.y;
            p.body.position = position;
            if ((position - target).sqrMagnitude > .0004f) return false;
            p.next++;
            return p.next >= p.route.Count;
        }
        void Ground(ref Vector3 position)
        {
            if (ground == null || ground.terrainData == null) return;
            var local = position - ground.transform.position;
            var size = ground.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) return;
            position.y = ground.SampleHeight(position) + ground.transform.position.y + .008f;
        }
    }
}





