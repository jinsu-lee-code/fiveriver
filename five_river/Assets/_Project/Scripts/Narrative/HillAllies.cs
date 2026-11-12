using System.Collections.Generic;
using FiveRivers.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Narrative
{
    // During the hill assault the surviving trench riflemen climb the ridge with the player instead of staying
    // behind in the trench. They leave the trench the way Park orders and the player goes: along the trench and
    // out of its open east end, one after another (2026-11-10: they used to walk straight over the front
    // sandbags, which no clip supports). Each keeps a slot behind the player (left, right, rear), moves there
    // along the runtime NavMesh at a crouched rifle run, runs upright when left far behind, and gives covering
    // fire (AllyCombatant) whenever he has stopped. Positions are restored when the section is left or retried.
    public sealed class HillAllies
    {
        // Ground speeds: WalkSpeed is the crouched advance between covers, JogSpeed the upright run that closes
        // a gap. The player walks at 3.2 m/s, so anyone more than JogBeyond behind closes up faster than that.
        // Both sit close to the pace their clips were made for, so the legs neither spin nor drag.
        public static float WalkSpeed = 2.2f, JogSpeed = 4.4f, JogBeyond = 6f, JogUntil = 3f, RepathEvery = 0.75f, SlotTolerance = 2.5f;
        // How fast the planted foot travels in each clip for a rig of human scale 1 (measured in play with a
        // planted-foot slip meter on the delivered rigs, 2026-11-10: about 2.3 and 4.8 m/s on the riflemen).
        // Animation speed follows ground speed / (this * rig scale) so feet do not slide.
        const float AdvanceClipSpeed = 2.5f, RunClipSpeed = 5.2f, TiredWalkClipSpeed = 0.95f;
        const float MinAnimSpeed = 0.75f, MaxAnimSpeed = 1.3f;
        // Between the two gaits' ranges: above this ground speed the run clip takes over, below it gives way again.
        const float RunGaitAbove = 3.3f, RunGaitBelow = 2.9f;
        // Starting, turning and stopping: he turns towards his way before stepping off, picks up and loses speed
        // over a few steps, and slows down for the last stretch instead of stopping dead from a run.
        const float Accel = 5f, Brake = 8f, TurnRate = 260f, TurnFirst = 100f, TurnFreely = 45f, BrakeDistance = 1.6f, MinSpeed = 1.3f;
        const float StartGap = 0.55f, SettleRate = 200f, SettleFor = 1.5f, StepOffSpeed = 0.25f;
        // Path corners are moved this far off the NavMesh edge so a shoulder does not brush the sandbag end,
        // and are rounded from this far out.
        const float CornerClearance = 0.7f, CornerReach = 0.9f;
        static readonly Vector3[] Slots = { new Vector3(-3.5f, 0f, -3f), new Vector3(3.5f, 0f, -3.5f), new Vector3(0f, 0f, -6f), new Vector3(-6f, 0f, -6.5f) };
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int IdleState = Animator.StringToHash("Idle_Stand");
        static readonly int RunState = Animator.StringToHash("Run_Rifle");
        // Added by FiveRivers/Animation/Add Ally Crouch Run 20261110. Without it the old tired walk is used.
        static readonly int AdvanceState = Animator.StringToHash("CrouchRun_Rifle");
        static readonly int TiredWalkState = Animator.StringToHash("Walk_Tired");
        // The trench faces +Z (PlayerController.frontLineZ). While the player is still behind its front wall the
        // men who are already out wait in a line beyond the exit instead of falling in behind him.
        static readonly Vector3 TrenchFront = Vector3.forward;
        const float WallSearch = 6f, ClearBeyondWall = 2.2f, LeadAhead = 5f, LeadStagger = 1.5f, RejoinBeyond = 15f;
        // Where the leaders wait, left and right of the way out: a gap is left in the middle for the player.
        static readonly float[] LeadAcross = { -3.5f, 3.5f, -6f, 6f };

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
            public int order;                   // 0 = first out of the trench
            public float nextRepath;
            public bool walking;
            public Vector3 target;
            public bool running;                // wants the run: well behind his slot
            public bool runGait;                // is in the run clip: fast enough for it
            public float speed;                 // current ground speed
            public float rigScale = 1f;         // clip foot speed scales with the rig (Park's legs are shorter)
            public bool hasAdvance;             // CrouchRun_Rifle exists in his controller
            public float startAt;               // they leave one after another
            public Quaternion settle;           // facing to ease into after arriving
            public float settleUntil;
            public float frontLine = float.NegativeInfinity; // just beyond the trench front wall ahead of his post
        }

        readonly List<Follower> followers = new List<Follower>();
        NavMeshPath navPathInstance;
        NavMeshPath navPath => navPathInstance ??= new NavMeshPath();
        Vector3 lastForward = Vector3.forward;
        Vector3 playerNow;
        Vector3 exit;

        public bool Active => followers.Count > 0;

        // trenchExit: where the squad leaves the trench. The man nearest to it goes first.
        public void Begin(Vector3 trenchExit)
        {
            End();
            exit = trenchExit;
            lastForward = Vector3.forward;
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
                    rigScale = animator.humanScale * animator.transform.lossyScale.y,
                    hasAdvance = animator.HasState(0, AdvanceState),
                    frontLine = FrontLine(ally.transform),
                });
            }
            followers.Sort((a, b) => Flat(a.body.position - exit).CompareTo(Flat(b.body.position - exit)));
            for (int i = 0; i < followers.Count; i++)
            {
                followers[i].order = i;
                followers[i].slot = Slots[i % Slots.Length];
                followers[i].startAt = Time.time + 0.3f + StartGap * i;
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

        // The crest is held through the dawn arrival: everyone stops where he is and turns to the player
        // (Settle eases the turn), and is only sent home by End() when the episode closes.
        public void Hold(Vector3 player)
        {
            foreach (var f in followers)
            {
                if (f.body == null) continue;
                Stop(f);
                var toPlayer = player - f.body.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude <= .01f) continue;
                f.settle = Quaternion.LookRotation(toPlayer);
                f.settleUntil = Time.time + SettleFor;
            }
        }

        // Eases a man who has just stopped into the facing he was given. Called every frame while they hold.
        public void Settle()
        {
            foreach (var f in followers)
                if (f.body != null && !f.walking && Time.time < f.settleUntil && !f.combat.Engaged)
                    f.body.rotation = Quaternion.RotateTowards(f.body.rotation, f.settle, SettleRate * Time.deltaTime);
        }

        // forward: direction the assault is heading (player -> current objective)
        public void Tick(Vector3 player, Vector3 forward)
        {
            forward.y = 0f;
            playerNow = player;
            // standing on the objective gives no direction: keep the last heading so nobody steps in front
            if (forward.sqrMagnitude < 9f) forward = lastForward;
            else lastForward = forward.normalized;
            var facing = Quaternion.LookRotation(forward.normalized);
            foreach (var f in followers)
            {
                if (f.body == null || !f.body.gameObject.activeInHierarchy || Time.time < f.startAt) continue;
                // While the player is still in the trench the men lead: they wait in a loose line beyond the way
                // out, clear of where he will come through, instead of waiting behind him (a slot behind him
                // would be back in the trench). The line is laid out from the trench exit and front, not from
                // where he stands or which objective is current, so it does not shift about while he moves inside.
                bool playerInTrench = Vector3.Dot(player, TrenchFront) < f.frontLine - ClearBeyondWall;
                var face = playerInTrench ? TrenchFront : forward.normalized;
                var target = playerInTrench
                    ? exit + Vector3.Cross(Vector3.up, TrenchFront) * LeadAcross[f.order % LeadAcross.Length] + TrenchFront * (LeadAhead + (f.order % 2) * LeadStagger)
                    : player + facing * f.slot;
                f.target = target;
                // Once he is out they fall in behind him as he passes. A man who is further up the assault than
                // his place (he led the way out, or the player stopped short of him) waits where he is until the
                // player has gone far enough past: he does not turn round and run back down to a place behind
                // him. Only when the player has left him well behind the other way does he come back.
                bool waits = !playerInTrench && Vector3.Dot(f.body.position - target, face) > 0f && Flat(f.body.position - player) < RejoinBeyond;
                if (Time.time >= f.nextRepath)
                {
                    f.nextRepath = Time.time + RepathEvery;
                    if (!waits && Flat(f.body.position - target) > SlotTolerance) Plan(f, target);
                    else if (f.walking && f.corner >= f.path.Count) Arrive(f, face);
                    // waiting, but still on his way somewhere that would take him right away from the player
                    else if (waits && f.walking && f.path.Count > 0 && Flat(f.path[f.path.Count - 1] - player) > RejoinBeyond) Arrive(f, face);
                }
                if (f.walking)
                {
                    // run when well behind his own slot, back to the advance once close (hysteresis: no flicker)
                    float behind = Flat(f.body.position - f.target);
                    f.running = f.running ? behind > JogUntil : behind > JogBeyond;
                    Move(f, face);
                }
            }
            Separate(player);
            Settle();
        }

        // The line just beyond the front wall ahead of him, or -infinity when no wall is close (already outside).
        static float FrontLine(Transform body)
        {
            int n = Physics.RaycastNonAlloc(body.position + Vector3.up * .5f, TrenchFront, hits, WallSearch, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (IsPerson(c, body) || hits[i].distance >= nearest) continue;
                nearest = hits[i].distance;
            }
            if (nearest == float.MaxValue) return float.NegativeInfinity;
            // Past the wall's far face: probe back from beyond it so a thick wall is cleared completely.
            var beyond = body.position + TrenchFront * (nearest + WallSearch);
            float far = nearest;
            n = Physics.RaycastNonAlloc(beyond + Vector3.up * .5f, -TrenchFront, hits, WallSearch, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!IsPerson(hits[i].collider, body)) far = Mathf.Max(far, nearest + WallSearch - hits[i].distance);
            return Vector3.Dot(body.position + TrenchFront * (far + ClearBeyondWall), TrenchFront);
        }

        static bool IsPerson(Collider c, Transform self) => c.transform.IsChildOf(self) || c.GetComponentInParent<CharacterController>() != null
            || c.GetComponentInParent<AllyCombatant>() != null || c.GetComponentInParent<EnemyAgent>() != null || c.attachedRigidbody != null;

        // Two slots can resolve to the same reachable spot (e.g. both off the walkable area): keep men 0.9 m apart.
        // Nobody walks through the player either: a man who comes too close steps aside, the player is never pushed.
        const float Personal = 0.9f, PlayerSpace = 1.1f, StepAside = 2.5f, AvoidAhead = 4f, AvoidWidth = 1.5f;
        void Separate(Vector3 player)
        {
            for (int i = 0; i < followers.Count; i++)
            {
                var a = followers[i].body;
                if (a == null || !a.gameObject.activeInHierarchy) continue;
                var away = a.position - player; away.y = 0f;
                float gap = away.magnitude;
                if (gap < PlayerSpace)
                    a.position = Ground(a.position + (gap > 0.01f ? away / gap : Vector3.right) * Mathf.Min(PlayerSpace - gap, StepAside * Time.deltaTime), a);
                for (int j = i + 1; j < followers.Count; j++)
                {
                    var b = followers[j].body;
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    var d = b.position - a.position; d.y = 0f;
                    float m = d.magnitude;
                    if (m >= Personal) continue;
                    var push = (m > 0.01f ? d / m : Vector3.right) * (Personal - m) * 0.5f;
                    b.position = Ground(b.position + push, b);
                    a.position = Ground(a.position - push, a);
                }
            }
        }

        void Plan(Follower f, Vector3 target)
        {
            if (!NavMesh.SamplePosition(target, out var to, 4f, NavMesh.AllAreas)) return;
            // His firing pit and his own obstacle leave him just off the walkable area. Join it on the side he is
            // heading for: the nearest point can lie behind him, which sent him backwards out of the pit first.
            var heading = to.position - f.body.position;
            heading.y = 0f;
            heading.Normalize();
            NavMeshHit from = default;
            bool joined = false;
            for (float ahead = 0f; ahead <= 2.01f && !joined; ahead += 0.5f)
                joined = NavMesh.SamplePosition(f.body.position + heading * ahead, out from, 0.35f, NavMesh.AllAreas);
            if (!joined && !NavMesh.SamplePosition(f.body.position, out from, 2.5f, NavMesh.AllAreas)) return;
            // the slot may lie off the walkable area: once he is at the nearest reachable point, he has arrived
            if (Flat(to.position - f.body.position) <= SlotTolerance)
            {
                if (f.walking) Arrive(f, lastForward);
                return;
            }
            if (!NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, navPath) || navPath.status == NavMeshPathStatus.PathInvalid) return;
            f.path.Clear();
            f.path.AddRange(navPath.corners);
            // NavMesh paths hug the corners they turn around (the sandbag end at the trench opening)
            for (int i = 1; i < f.path.Count - 1; i++)
            {
                if (!NavMesh.FindClosestEdge(f.path[i], out var edge, NavMesh.AllAreas) || edge.distance >= CornerClearance) continue;
                var off = f.path[i] - edge.position; off.y = 0f;
                if (off.sqrMagnitude < 0.0001f) off = edge.normal;
                var moved = edge.position + off.normalized * CornerClearance;
                if (NavMesh.SamplePosition(moved, out var onMesh, 0.5f, NavMesh.AllAreas)) f.path[i] = onMesh.position;
            }
            f.corner = 1;
            // A corner right at his feet (the edge of that hole) would start him with a sidestep that reads as a
            // stumble: head for the first real corner instead.
            while (f.corner < f.path.Count - 1 && Flat(f.path[f.corner] - f.body.position) < 1f) f.corner++;
            if (f.corner >= f.path.Count) return;
            f.walking = true;
            f.combat.Advancing = true;
        }

        void Move(Follower f, Vector3 assaultForward)
        {
            if (f.corner >= f.path.Count)
            {
                Arrive(f, assaultForward);
                return;
            }
            var pos = f.body.position;
            // Round the corners on the way instead of running up to each one and pivoting on it: within reach
            // of a corner he is already heading for the next (he used to brake to a crouch at the sandbag end).
            while (f.corner < f.path.Count - 1 && Flat(f.path[f.corner] - pos) < CornerReach) f.corner++;
            var next = f.path[f.corner];
            // move and arrive in the horizontal plane only; height comes from the ground below (a 3D distance
            // never closes when the ground-snapped feet sit a few cm off the NavMesh corner height)
            var flat = new Vector3(next.x - pos.x, 0f, next.z - pos.z);
            float distance = flat.magnitude;
            float remaining = distance;
            for (int i = f.corner; i < f.path.Count - 1; i++) remaining += Flat(f.path[i + 1] - f.path[i]);

            // Go round the player, not through him: when he stands in the way just ahead, head past his side
            // (the one this man is already on, if there is ground to walk on) until he is behind.
            var steer = flat;
            bool detour = false;
            if (distance > 0.01f)
            {
                var way = flat / distance;
                var toPlayer = new Vector3(playerNow.x - pos.x, 0f, playerNow.z - pos.z);
                float ahead = Vector3.Dot(toPlayer, way);
                var right = Vector3.Cross(Vector3.up, way);
                float aside = Vector3.Dot(toPlayer, right); // + = the player is to this man's right
                if (ahead > 0f && ahead < AvoidAhead && ahead < distance && Mathf.Abs(aside) < AvoidWidth)
                {
                    float own = aside > 0f ? -1f : 1f;
                    for (int attempt = 0; attempt < 2 && !detour; attempt++, own = -own)
                    {
                        var pass = playerNow + right * (own * AvoidWidth);
                        if (!NavMesh.SamplePosition(pass, out var free, 0.4f, NavMesh.AllAreas)) continue;
                        steer = new Vector3(free.position.x - pos.x, 0f, free.position.z - pos.z);
                        detour = steer.sqrMagnitude > 0.01f;
                    }
                    if (!detour) steer = flat;
                }
            }

            float want = f.running ? JogSpeed : WalkSpeed;
            if (remaining < BrakeDistance) want = Mathf.Min(want, Mathf.Lerp(MinSpeed, WalkSpeed, remaining / BrakeDistance));
            // turn towards the way before stepping off, and ease round corners instead of sliding sideways
            float off = distance > 0.01f ? Vector3.Angle(Vector3.ProjectOnPlane(f.body.forward, Vector3.up), steer) : 0f;
            want *= 1f - Mathf.InverseLerp(TurnFreely, TurnFirst, off);
            f.speed = Mathf.MoveTowards(f.speed, want, (want > f.speed ? Accel : Brake) * Time.deltaTime);

            float move = f.speed * Time.deltaTime;
            if (!detour && distance <= move)
            {
                f.body.position = new Vector3(next.x, pos.y, next.z);
                f.corner++;
            }
            else f.body.position = pos + steer.normalized * move;
            if (steer.sqrMagnitude > 0.0001f)
                f.body.rotation = Quaternion.RotateTowards(f.body.rotation, Quaternion.LookRotation(steer), TurnRate * Time.deltaTime);
            // NavMesh corners are sparse on the open slope, so a straight line between them floats over bumps
            // and cuts into hollows: put the feet back on the ground every frame.
            f.body.position = Ground(f.body.position, f.body);

            // The gait follows the ground speed he really has, not the one he wants: standing while he turns on
            // the spot, the crouched advance for the first steps, the run once he is up to speed (a run clip at
            // walking pace spins its legs on the spot). Keep the wanted state even if something else
            // (AllyCombatant.StopFiring) cross-faded away.
            bool stepping = f.speed > StepOffSpeed;
            f.runGait = f.speed > (f.runGait ? RunGaitBelow : RunGaitAbove);
            int state = !stepping ? IdleState : f.runGait ? RunState : f.hasAdvance ? AdvanceState : TiredWalkState;
            float clip = f.runGait ? RunClipSpeed : f.hasAdvance ? AdvanceClipSpeed : TiredWalkClipSpeed;
            Play(f.animator, state, stepping ? 0.2f : 0.15f);
            f.animator.speed = stepping ? Mathf.Clamp(f.speed / (clip * f.rigScale), MinAnimSpeed, MaxAnimSpeed) : 1f;
            // The controller's own Idle <-> Walk_Tired transitions listen to Speed: only the old walk may use it.
            f.animator.SetFloat(SpeedId, stepping && state == TiredWalkState ? f.speed : 0f);
        }

        static void Play(Animator animator, int state, float fade)
        {
            bool changing = animator.IsInTransition(0);
            if (changing ? animator.GetNextAnimatorStateInfo(0).shortNameHash == state
                : animator.GetCurrentAnimatorStateInfo(0).shortNameHash == state) return;
            animator.CrossFadeInFixedTime(state, fade);
        }

        // He has reached his place: stop, then ease round to face up the slope, towards the objective.
        static void Arrive(Follower f, Vector3 assaultForward)
        {
            Stop(f);
            assaultForward.y = 0f;
            if (assaultForward.sqrMagnitude <= .01f) return;
            f.settle = Quaternion.LookRotation(assaultForward);
            f.settleUntil = Time.time + SettleFor;
        }

        static void Stop(Follower f)
        {
            f.walking = false;
            f.running = false;
            f.runGait = false;
            f.speed = 0f;
            f.path.Clear();
            f.corner = 0;
            if (f.animator != null && f.animator.isActiveAndEnabled)
            {
                f.animator.speed = 1f;
                f.animator.SetFloat(SpeedId, 0f);
                Play(f.animator, IdleState, 0.3f);
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
