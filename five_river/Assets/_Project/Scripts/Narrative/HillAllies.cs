using System.Collections.Generic;
using FiveRivers.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Narrative
{
    // During the hill assault the surviving trench riflemen climb the ridge with the player instead of staying
    // behind in the trench. Each keeps a slot behind the player (left, right, rear), walks there along the
    // runtime NavMesh with the in-place Walk_Tired clip, jogs when left far behind, and gives covering fire
    // (AllyCombatant) whenever he has stopped. Positions are restored when the section is left or retried.
    public sealed class HillAllies
    {
        // The player walks at 3.2 m/s, so anyone more than JogBeyond behind closes up faster than that.
        public static float WalkSpeed = 1.8f, JogSpeed = 3.6f, JogBeyond = 6f, JogUntil = 3f, RepathEvery = 0.75f, SlotTolerance = 2.5f;
        // Walk_Tired is authored for about 1.3 m/s, the rifle run (Run_Rifle, from the KPA set) for about 3.4 m/s;
        // animation speed follows the ground speed so feet do not slide.
        const float WalkClipSpeed = 1.3f, RunClipSpeed = 3.4f;
        static readonly Vector3[] Slots = { new Vector3(-3.5f, 0f, -3f), new Vector3(3.5f, 0f, -3.5f), new Vector3(0f, 0f, -6f), new Vector3(-6f, 0f, -6.5f) };
        static readonly int SpeedId = Animator.StringToHash("Speed");
        // Going over the top: the trench faces +Z (PlayerController.frontLineZ). Each man first scrambles straight
        // over the front sandbags to just beyond them, then joins his slot. There is no vault clip, so the walk is
        // kept and the body follows the sandbag tops with an eased rise and drop.
        static readonly Vector3 TrenchFront = Vector3.forward;
        const float WallSearch = 6f, ClearBeyondWall = 2.2f, ClimbSpeed = 1.6f, ClimbRise = 3f, LeadAhead = 1.5f;

        sealed class Follower
        {
            public AllyCombatant combat;
            public Transform body;
            public Animator animator;
            public Vector3 homePosition;
            public Quaternion homeRotation;
            public readonly List<Vector3> path = new List<Vector3>();
            public int corner;
            public Vector3 slot;
            public float nextRepath;
            public bool walking;
            public Vector3 target;
            public bool running;
            public Vector3? climbTo;
            public float outsideLine = float.NegativeInfinity; // front-of-trench line he climbed out to
        }

        readonly List<Follower> followers = new List<Follower>();
        NavMeshPath navPathInstance;
        NavMeshPath navPath => navPathInstance ??= new NavMeshPath();
        Vector3 lastForward = Vector3.forward;

        public bool Active => followers.Count > 0;

        public void Begin()
        {
            End();
            lastForward = Vector3.forward;
            int i = 0;
            foreach (var ally in Object.FindObjectsByType<AllyCombatant>(FindObjectsSortMode.InstanceID))
            {
                if (!ally.isActiveAndEnabled) continue; // a fallen man (Choi Man-su) stays where he is
                var animator = ally.GetComponentInChildren<Animator>();
                if (animator == null) continue;
                followers.Add(new Follower
                {
                    combat = ally,
                    body = ally.transform,
                    animator = animator,
                    homePosition = ally.transform.position,
                    homeRotation = ally.transform.rotation,
                    slot = Slots[i % Slots.Length],
                    nextRepath = Time.time + 0.4f * i, // stagger: they climb out one after another
                    climbTo = OverTheTop(ally.transform),
                });
                i++;
            }
        }

        public void End()
        {
            foreach (var f in followers)
            {
                if (f.body == null) continue;
                f.body.SetPositionAndRotation(f.homePosition, f.homeRotation);
                Stop(f);
            }
            followers.Clear();
        }

        // The crest is held through the dawn arrival: everyone stops where he is, facing the player,
        // and is only sent home by End() when the episode closes.
        public void Hold(Vector3 player)
        {
            foreach (var f in followers)
            {
                if (f.body == null) continue;
                Stop(f);
                var toPlayer = player - f.body.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > .01f) f.body.rotation = Quaternion.LookRotation(toPlayer);
            }
        }

        // forward: direction the assault is heading (player -> current objective)
        public void Tick(Vector3 player, Vector3 forward)
        {
            forward.y = 0f;
            var objective = player + forward;
            // standing on the objective gives no direction: keep the last heading so nobody steps in front
            if (forward.sqrMagnitude < 9f) forward = lastForward;
            else lastForward = forward.normalized;
            var facing = Quaternion.LookRotation(forward.normalized);
            foreach (var f in followers)
            {
                if (f.body == null || !f.body.gameObject.activeInHierarchy) continue;
                if (f.climbTo.HasValue)
                {
                    if (Time.time >= f.nextRepath) Climb(f);
                    continue;
                }
                // Men who went over the top lead while the player is still in the trench: they spread out around the
                // current objective instead of waiting behind him (a slot behind him would be back in the trench).
                // Once he is out they fall in behind him as he passes.
                bool playerInTrench = Vector3.Dot(player, TrenchFront) < f.outsideLine - ClearBeyondWall;
                var target = playerInTrench ? objective + facing * new Vector3(f.slot.x, 0f, LeadAhead) : player + facing * f.slot;
                f.target = target;
                if (Time.time >= f.nextRepath)
                {
                    f.nextRepath = Time.time + RepathEvery;
                    if (Flat(f.body.position - target) > SlotTolerance) Plan(f, target);
                    else if (f.walking && f.corner >= f.path.Count) Stop(f);
                }
                if (f.walking)
                {
                    // jog when well behind his own slot, back to a walk once close (hysteresis: no flicker)
                    float behind = Flat(f.body.position - f.target);
                    bool jog = f.running ? behind > JogUntil : behind > JogBeyond;
                    Walk(f, jog ? JogSpeed : WalkSpeed, forward);
                }
            }
            Separate();
        }

        // The first point beyond the front wall in front of him, or null when no wall is close (already outside).
        static Vector3? OverTheTop(Transform body)
        {
            int n = Physics.RaycastNonAlloc(body.position + Vector3.up * .5f, TrenchFront, hits, WallSearch, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (IsPerson(c, body) || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
            }
            if (nearest == float.MaxValue) return null;
            // Past the wall's far face: probe back from beyond it so a thick wall is cleared completely.
            var beyond = body.position + TrenchFront * (nearest + WallSearch);
            float far = nearest;
            n = Physics.RaycastNonAlloc(beyond + Vector3.up * .5f, -TrenchFront, hits, WallSearch, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!IsPerson(hits[i].collider, body)) far = Mathf.Max(far, nearest + WallSearch - hits[i].distance);
            return body.position + TrenchFront * (far + ClearBeyondWall);
        }

        void Climb(Follower f)
        {
            var to = f.climbTo.Value;
            var pos = f.body.position;
            var flat = new Vector3(to.x - pos.x, 0f, to.z - pos.z);
            float distance = flat.magnitude;
            if (distance < .05f)
            {
                f.outsideLine = Vector3.Dot(to, TrenchFront);
                f.climbTo = null;
                f.nextRepath = 0f; // plan the way to his slot on the next tick
                return;
            }
            if (!f.walking)
            {
                f.walking = true;
                f.combat.Advancing = true;
                f.animator.CrossFade("Walk_Tired", 0.2f);
            }
            float move = ClimbSpeed * Time.deltaTime;
            var next = distance <= move ? new Vector3(to.x, pos.y, to.z) : pos + flat / distance * move;
            next.y = Mathf.MoveTowards(pos.y, Highest(next, f.body), ClimbRise * Time.deltaTime);
            f.body.position = next;
            f.body.rotation = Quaternion.RotateTowards(f.body.rotation, Quaternion.LookRotation(TrenchFront), 300f * Time.deltaTime);
            f.animator.SetFloat(SpeedId, ClimbSpeed);
            f.animator.speed = Mathf.Clamp(ClimbSpeed / WalkClipSpeed, 0.8f, 1.6f);
        }

        // Top surface under him (sandbag tops included) for the scramble over the wall.
        static float Highest(Vector3 p, Transform self)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 2.5f, Vector3.down, hits, 5f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float best = p.y;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (IsPerson(hits[i].collider, self)) continue;
                if (!found || hits[i].point.y > best) best = hits[i].point.y;
                found = true;
            }
            return best;
        }

        static bool IsPerson(Collider c, Transform self) => c.transform.IsChildOf(self) || c.GetComponentInParent<CharacterController>() != null
            || c.GetComponentInParent<AllyCombatant>() != null || c.GetComponentInParent<EnemyAgent>() != null || c.attachedRigidbody != null;

        // Two slots can resolve to the same reachable spot (e.g. both off the walkable area): keep men 0.9 m apart.
        const float Personal = 0.9f;
        void Separate()
        {
            for (int i = 0; i < followers.Count; i++)
                for (int j = i + 1; j < followers.Count; j++)
                {
                    var a = followers[i].body; var b = followers[j].body;
                    if (a == null || b == null || !a.gameObject.activeInHierarchy || !b.gameObject.activeInHierarchy) continue;
                    var d = b.position - a.position; d.y = 0f;
                    float m = d.magnitude;
                    if (m >= Personal) continue;
                    var push = (m > 0.01f ? d / m : Vector3.right) * (Personal - m) * 0.5f;
                    b.position = Ground(b.position + push, b);
                    a.position = Ground(a.position - push, a);
                }
        }

        void Plan(Follower f, Vector3 target)
        {
            if (!NavMesh.SamplePosition(f.body.position, out var from, 2.5f, NavMesh.AllAreas)) return;
            if (!NavMesh.SamplePosition(target, out var to, 4f, NavMesh.AllAreas)) return;
            // the slot may lie off the walkable area: once he is at the nearest reachable point, he has arrived
            if (Flat(to.position - f.body.position) <= SlotTolerance)
            {
                if (f.walking) Stop(f);
                return;
            }
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, navPath) || navPath.status == NavMeshPathStatus.PathInvalid) return;
            f.path.Clear();
            f.path.AddRange(navPath.corners);
            f.corner = 1;
            if (f.corner >= f.path.Count) return;
            if (!f.walking)
            {
                f.walking = true;
                f.combat.Advancing = true;
                f.animator.CrossFade("Walk_Tired", 0.2f);
            }
        }

        void Walk(Follower f, float speed, Vector3 assaultForward)
        {
            if (f.corner >= f.path.Count)
            {
                Stop(f);
                // settle facing up the slope, toward the objective
                f.body.rotation = Quaternion.LookRotation(assaultForward);
                return;
            }
            var next = f.path[f.corner];
            var pos = f.body.position;
            // move and arrive in the horizontal plane only; height comes from the ground below (a 3D distance
            // never closes when the ground-snapped feet sit a few cm off the NavMesh corner height)
            var flat = new Vector3(next.x - pos.x, 0f, next.z - pos.z);
            float distance = flat.magnitude;
            float move = speed * Time.deltaTime;
            if (distance <= move)
            {
                f.body.position = new Vector3(next.x, pos.y, next.z);
                f.corner++;
            }
            else f.body.position = pos + flat / distance * move;
            if (flat.sqrMagnitude > 0.0001f)
                f.body.rotation = Quaternion.RotateTowards(f.body.rotation, Quaternion.LookRotation(flat), 300f * Time.deltaTime);
            // NavMesh corners are sparse on the open slope, so a straight line between them floats over bumps
            // and cuts into hollows: put the feet back on the ground every frame.
            f.body.position = Ground(f.body.position, f.body);
            f.animator.SetFloat(SpeedId, speed);
            bool run = speed > WalkSpeed + .1f;
            f.running = run;
            // keep the wanted locomotion state even if something else (AllyCombatant.StopFiring) cross-faded away
            string want = run ? "Run_Rifle" : "Walk_Tired";
            var now = f.animator.GetCurrentAnimatorStateInfo(0);
            bool heading = f.animator.IsInTransition(0) && f.animator.GetNextAnimatorStateInfo(0).IsName(want);
            if (!now.IsName(want) && !heading) f.animator.CrossFade(want, 0.2f);
            f.animator.speed = Mathf.Clamp(speed / (run ? RunClipSpeed : WalkClipSpeed), 0.8f, 1.6f);
        }

        static void Stop(Follower f)
        {
            f.walking = false;
            f.running = false;
            f.path.Clear();
            f.corner = 0;
            if (f.animator != null && f.animator.isActiveAndEnabled)
            {
                f.animator.speed = 1f;
                f.animator.SetFloat(SpeedId, 0f);
                f.animator.CrossFade("Idle_Stand", 0.25f);
            }
            if (f.combat != null) f.combat.Advancing = false;
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        static readonly RaycastHit[] hits = new RaycastHit[8];
        static Vector3 Ground(Vector3 p, Transform self)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 1.2f, Vector3.down, hits, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            // of the ground surfaces under him, the one closest to where the path puts his feet (not the highest:
            // walking past a cover wall or a truck must not lift him onto it)
            float best = float.NegativeInfinity, bestGap = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.transform.IsChildOf(self) || c.GetComponentInParent<CharacterController>() != null
                    || c.GetComponentInParent<AllyCombatant>() != null || c.GetComponentInParent<EnemyAgent>() != null
                    || c.attachedRigidbody != null) continue; // ground only: no people and no dropped rifle
                float gap = Mathf.Abs(hits[i].point.y - p.y);
                if (gap < bestGap) { bestGap = gap; best = hits[i].point.y; }
            }
            if (best > float.NegativeInfinity) p.y = best;
            return p;
        }
    }
}
