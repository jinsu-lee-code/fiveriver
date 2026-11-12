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
        [Min(.1f)] public float walkPlaybackRate = 1.32f;
        [Range(1f, 45f)] public float moveAngleTolerance = 15f;

        [Header("Narrated arrival: skip exposed dismount/walking")]
        public bool narratedArrival = true;
        public Vector3 narratedJoinOffset = new Vector3(-6f, 0f, -1.4f);
        public event System.Action Arrived;
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
        }

        readonly List<Passenger> passengers = new List<Passenger>();
        static readonly int SpeedId = Animator.StringToHash("Speed");
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
            JoinedCount = 0;
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
                var footPlacement = body.gameObject.AddComponent<DawnFootPlacement>();
                footPlacement.Configure(animator, transform, exitPath[1], exitPath[exitPath.Length - 1], ground);
                passengers.Add(new Passenger { body = body, animator = animator, footPlacement = footPlacement });
                if (narratedArrival)
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
                Arrived?.Invoke();
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





