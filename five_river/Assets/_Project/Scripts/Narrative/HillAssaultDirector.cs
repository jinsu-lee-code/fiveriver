using System.Collections.Generic;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Narrative
{
    // Expanded order (2026-11-09): after the tank, Park Yong-pal leads the ridge assault along the right trench
    // opening and the authored east-slope covers; the surviving riflemen follow (HillAllies). Reporting the crest
    // starts the dawn: the squad holds the summit, the new student soldier Yun Tae-seok climbs up to the player,
    // and after his greeting Park gives him Man-su's place before the names and letters.
    public sealed class HillAssaultDirector : MonoBehaviour
    {
        enum Stage { Exit, LowerSlope, UpperSlope, Ridge, Summit }
        enum Arrival { Waiting, Climbing, Talking, Closing }

        // Tae-seok appears out of view on the slope below the crest and walks the last stretch up.
        const float ClimbStartDistance = 18f, ClimbSpeed = 1.8f, WalkClipSpeed = 1.3f, MeetDistance = 3.2f, StandOff = 2.6f;
        // The crest is captured by reaching the highest boulder on the hill top (the player cannot stand on it).
        const float GreetTurn = 2f, GreetTurnSpeed = 160f, FaceHeight = 1.5f;
        float greetUntil;
        const float SummitReach = 3.5f, SummitRockSearch = 40f;
        static readonly int SpeedId = Animator.StringToHash("Speed");

        FlowDirector director;
        DawnDirector dawn;
        LevelPoints points;
        PlayerController player;
        EnemyDirector enemies;
        ObjectiveDisplay objective;
        SubtitleDisplay subtitles;
        Camera view;
        GameText copy;
        Stage stage;
        // 0 at the trench exit, 1 at the summit boulder; DawnDirector lets the sky lighten only this far
        public float ClimbProgress => started ? Mathf.Clamp01((float)stage / (float)Stage.Summit) : 0f;
        bool started;
        readonly List<Transform> pendingDefenders = new List<Transform>();
        float nextSpawnAttempt;
        readonly List<EnemyAgent> defenders = new List<EnemyAgent>();
        readonly Plane[] planes = new Plane[6];
        HillAllies alliesInstance; // created on demand: field initialisers do not run on every play-mode entry path
        HillAllies allies => alliesInstance ??= new HillAllies();

        Arrival arrival;
        bool arrivalStarted;
        Transform climber;
        Animator climberAnimator;
        bool climberWalking;
        readonly List<Vector3> climbPath = new List<Vector3>();
        int climbCorner;
        float nextClimbPath;
        float nextLineAt;
        NavMeshPath navPathInstance;
        NavMeshPath navPath => navPathInstance ??= new NavMeshPath();
        Vector3? summitRock;

        public void Configure(FlowDirector flow, DawnDirector arrivalDirector, LevelPoints level, Camera camera)
        {
            director = flow;
            dawn = arrivalDirector;
            copy = FindFirstObjectByType<NarrativeDirector>().Text;
            points = level;
            view = camera;
            player = FindFirstObjectByType<PlayerController>();
            enemies = FindFirstObjectByType<EnemyDirector>();
            objective = FindFirstObjectByType<ObjectiveDisplay>();
            subtitles = FindFirstObjectByType<SubtitleDisplay>();
        }

        void Start()
        {
            director.Flow.RetryRequested += OnRetry;
            director.Flow.SectionChanged += OnSectionChanged;
        }

        void OnDestroy()
        {
            if (director == null || director.Flow == null) return;
            director.Flow.RetryRequested -= OnRetry;
            director.Flow.SectionChanged -= OnSectionChanged;
        }

        void OnSectionChanged(FlowTransition change)
        {
            // The captured crest stays manned through the dawn; everything is sent home when the episode closes.
            if (change.From == FlowSection.HillAssault && change.To == FlowSection.Dawn)
            {
                ResetArrival();
                return;
            }
            if (change.From == FlowSection.HillAssault || change.To == FlowSection.HillAssault
                || change.From == FlowSection.Dawn || change.To == FlowSection.Dawn)
                ResetAssault();
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            if (checkpoint == FlowCheckpoint.HillAssault) ResetAssault();
        }

        void ResetAssault()
        {
            started = false;
            pendingDefenders.Clear();
            nextSpawnAttempt = 0f;
            stage = Stage.Exit;
            defenders.Clear(); // EnemyDirector owns actual agent cleanup on retry/exit.
            allies.End();
            player.SetHillExit(null);
            ResetArrival();
        }

        void ResetArrival()
        {
            arrival = Arrival.Waiting;
            arrivalStarted = false;
            climber = null; // DawnDirector owns and destroys the body
            climberAnimator = null;
            climberWalking = false;
            climbPath.Clear();
            climbCorner = 0;
            nextClimbPath = 0f;
            nextLineAt = 0f;
        }

        void Update()
        {
            var flow = director.Flow;
            if (!flow.IsExpanded || flow.Paused) return;
            if (flow.Section == FlowSection.Dawn)
            {
                UpdateArrival(flow);
                return;
            }
            if (flow.Section != FlowSection.HillAssault) return;
            if (!started)
            {
                started = true;
                allies.Begin(ExitPoint);
                player.SetHillExit(ExitPoint.x);
                subtitles.Say(copy.parkDeparts);
            }

            if (pendingDefenders.Count > 0 && Time.time >= nextSpawnAttempt)
            {
                nextSpawnAttempt = Time.time + 1f;
                for (int i = pendingDefenders.Count - 1; i >= 0; i--)
                {
                    var defender = enemies.SpawnDefender(pendingDefenders[i]);
                    if (defender == null) continue;
                    defenders.Add(defender);
                    pendingDefenders.RemoveAt(i);
                }
            }
            var goal = StageGoal();
            allies.Tick(player.transform.position, goal - player.transform.position);
            int remaining = pendingDefenders.Count;
            foreach (var defender in defenders)
                if (defender != null && defender.Alive) remaining++;
            string target = StageName();
            string cueText = remaining > 0 ? string.Format(copy.hillDefenderCount, target, remaining) : target;
            if (stage == Stage.Summit)
            {
                // Reaching the top boulder captures the crest and brings the dawn; a last defender does not block it.
                if (Distance(goal) <= SummitReach) flow.ReportGoalMet(FlowSection.HillAssault);
                else ShowGuide(cueText, Guide(cueText, goal));
                return;
            }
            if (remaining != 0 || Distance(goal) > (stage == Stage.Exit ? 4f : 5.5f))
            {
                ShowGuide(cueText, Guide(cueText, goal));
                return;
            }
            stage++;
            defenders.Clear();
            switch (stage)
            {
                case Stage.LowerSlope:
                    Spawn(points.assaultCovers[8]);
                    subtitles.Say(copy.parkLower);
                    break;
                case Stage.UpperSlope:
                    Spawn(points.assaultCovers[2]);
                    Spawn(points.assaultCovers[1]);
                    subtitles.Say(copy.parkUpper);
                    break;
                case Stage.Ridge:
                    Spawn(points.assaultCovers[9]);
                    subtitles.Say(copy.parkRidge);
                    break;
                case Stage.Summit:
                    Spawn(points.enemySpawns[2]);
                    subtitles.Say(copy.parkSummit);
                    break;
            }
        }

        // Dawn on the crest: hold the squad, bring Tae-seok up the slope, greet him, then Park's line ends the episode.
        void UpdateArrival(DemoFlow flow)
        {
            if (!arrivalStarted)
            {
                arrivalStarted = true;
                allies.Hold(player.transform.position);
            }
            allies.Settle();
            if (CutsceneRig.Running > 0) return; // the summit dawn sequence plays first
            switch (arrival)
            {
                case Arrival.Waiting:
                    if (!TrySpawnClimber())
                    {
                        ShowGuide(copy.watchTruck, copy.watchTruck);
                        return;
                    }
                    arrival = Arrival.Climbing;
                    break;
                case Arrival.Climbing:
                    if (climber == null) { arrival = Arrival.Waiting; return; }
                    TickClimber();
                    bool near = Distance(climber.position) <= MeetDistance;
                    string key = near ? copy.meetTaeSeok : copy.findTaeSeok;
                    ShowGuide(key, near ? key : Guide(key, climber.position));
                    if (near && player.Gate.CanAct && player.Controls.Resupply.WasPressedThisFrame())
                    {
                        StopClimber();
                        objective.Hide();
                        lastGuideKey = lastGuideText = null;
                        subtitles.Say(copy.taeSeokMeets);
                        nextLineAt = Time.time + copy.taeSeokMeets.duration + .3f;
                        greetUntil = Time.time + GreetTurn;
                        arrival = Arrival.Talking;
                    }
                    break;
                case Arrival.Talking:
                    FaceClimberToPlayer();
                    // he stands lower on the slope: bring his face into frame as the greeting starts, then leave the view alone
                    if (Time.time < greetUntil && climber != null)
                        player.LookToward(climber.position + Vector3.up * FaceHeight, GreetTurnSpeed * Time.deltaTime);
                    if (Time.time < nextLineAt) return;
                    subtitles.Say(copy.parkMansuPlace);
                    nextLineAt = Time.time + copy.parkMansuPlace.duration + .5f;
                    arrival = Arrival.Closing;
                    break;
                case Arrival.Closing:
                    FaceClimberToPlayer();
                    if (Time.time >= nextLineAt) flow.ReportGoalMet(FlowSection.Dawn);
                    break;
            }
        }

        // Pick a NavMesh spot on the slope below the crest that the player cannot see right now.
        bool TrySpawnClimber()
        {
            var crest = player.transform.position;
            var downhill = points.assaultCovers[9].position - crest; // the way the assault came up
            downhill.y = 0f;
            if (downhill.sqrMagnitude < .01f) downhill = -player.transform.forward;
            downhill.Normalize();
            GeometryUtility.CalculateFrustumPlanes(view, planes);
            for (int i = 0; i < 8; i++)
            {
                // Straight down the slope first, then fanning out to either side.
                float yaw = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 35f;
                var candidate = crest + Quaternion.Euler(0f, yaw, 0f) * downhill * ClimbStartDistance;
                if (!NavMesh.SamplePosition(candidate, out var hit, 6f, NavMesh.AllAreas)) continue;
                // Generous box: a spawn just past the screen edge would still read as popping in.
                var body = new Bounds(hit.position + Vector3.up, new Vector3(5f, 5f, 5f));
                if (GeometryUtility.TestPlanesAABB(planes, body)) continue;
                var facing = crest - hit.position;
                facing.y = 0f;
                climber = dawn.SpawnClimber(hit.position, facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : Quaternion.identity);
                if (climber == null) return false;
                climberAnimator = climber.GetComponentInChildren<Animator>();
                nextClimbPath = 0f;
                return true;
            }
            return false;
        }

        void TickClimber()
        {
            if (Distance(climber.position) <= StandOff)
            {
                StopClimber();
                FaceClimberToPlayer();
                return;
            }
            if (Time.time >= nextClimbPath)
            {
                nextClimbPath = Time.time + 1f;
                climbPath.Clear();
                climbCorner = 1;
                if (NavMesh.SamplePosition(player.transform.position, out var goal, 3f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(climber.position, goal.position, NavMesh.AllAreas, navPath)
                    && navPath.status != NavMeshPathStatus.PathInvalid)
                    climbPath.AddRange(navPath.corners);
            }
            if (climbCorner >= climbPath.Count)
            {
                StopClimber();
                FaceClimberToPlayer();
                return;
            }
            var next = climbPath[climbCorner];
            var flat = next - climber.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < .04f)
            {
                climbCorner++;
                return;
            }
            climber.rotation = Quaternion.RotateTowards(climber.rotation, Quaternion.LookRotation(flat), 240f * Time.deltaTime);
            var position = Vector3.MoveTowards(climber.position, next, ClimbSpeed * Time.deltaTime);
            var terrain = Terrain.activeTerrain;
            if (terrain != null) position.y = terrain.SampleHeight(position) + terrain.transform.position.y + .008f;
            climber.position = position;
            if (!climberWalking && climberAnimator != null)
            {
                climberAnimator.speed = ClimbSpeed / WalkClipSpeed; // feet keep pace with the ground speed
                climberAnimator.SetFloat(SpeedId, ClimbSpeed);
                climberAnimator.CrossFadeInFixedTime("Walk_Tired", .15f);
            }
            climberWalking = true;
        }

        void StopClimber()
        {
            if (!climberWalking) return;
            climberWalking = false;
            if (climberAnimator == null) return;
            climberAnimator.speed = 1f;
            climberAnimator.SetFloat(SpeedId, 0f);
            climberAnimator.CrossFadeInFixedTime("Idle_Stand", .25f);
        }

        void FaceClimberToPlayer()
        {
            if (climber == null) return;
            var toPlayer = player.transform.position - climber.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > .01f)
                climber.rotation = Quaternion.RotateTowards(climber.rotation, Quaternion.LookRotation(toPlayer), 180f * Time.deltaTime);
        }

        Vector3 ExitPoint => points.moundTop.position + new Vector3(6f, 0f, 8f);

        Vector3 StageGoal()
        {
            switch (stage)
            {
                case Stage.Exit: return ExitPoint;
                case Stage.LowerSlope: return points.assaultCovers[8].position;
                case Stage.UpperSlope: return points.assaultCovers[2].position;
                case Stage.Ridge: return points.assaultCovers[9].position;
                default: return SummitRock();
            }
        }

        // The highest boulder within reach of the authored summit point, found once per scene.
        Vector3 SummitRock()
        {
            if (summitRock.HasValue) return summitRock.Value;
            var anchor = points.enemySpawns[2].position;
            Vector3 best = anchor;
            float bestHeight = float.NegativeInfinity;
            foreach (var rock in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var parent = rock.transform.parent;
                if (parent == null || !parent.name.StartsWith("Granite_Boulder")) continue;
                var centre = rock.bounds.center;
                if (new Vector2(centre.x - anchor.x, centre.z - anchor.z).magnitude > SummitRockSearch) continue;
                if (centre.y > bestHeight) { bestHeight = centre.y; best = centre; }
            }
            summitRock = best;
            return best;
        }

        string StageName()
        {
            switch (stage)
            {
                case Stage.Exit: return copy.hillExit;
                case Stage.LowerSlope: return copy.hillLower;
                case Stage.UpperSlope: return copy.hillUpper;
                case Stage.Ridge: return copy.hillRidge;
                default: return copy.hillSummit;
            }
        }

        void Spawn(Transform cover)
        {
            var defender = enemies.SpawnDefender(cover);
            if (defender == null)
                pendingDefenders.Add(cover);
            else defenders.Add(defender);
        }


        float Distance(Vector3 target)
        {
            var delta = target - player.transform.position;
            return new Vector2(delta.x, delta.z).magnitude;
        }

        // The objective sound and entrance play only when the objective itself changes; live direction
        // and distance updates replace the text silently.
        string lastGuideKey, lastGuideText;
        void ShowGuide(string key, string text)
        {
            if (key != lastGuideKey || objective.Current != lastGuideText) objective.Show(text, true);
            else objective.UpdateText(text, true);
            lastGuideKey = key;
            lastGuideText = text;
        }

        string Guide(string text, Vector3 target)
        {
            var delta = target - player.transform.position;
            delta.y = 0f;
            float angle = Vector3.SignedAngle(player.transform.forward, delta, Vector3.up);
            string heading = Mathf.Abs(angle) < 25f ? copy.hillAhead : Mathf.Abs(angle) > 150f ? copy.hillBehind : angle > 0 ? copy.hillRight : copy.hillLeft;
            // Five-metre bands keep the existing objective entrance animation readable while walking.
            int metres = Mathf.Max(5, Mathf.RoundToInt(delta.magnitude / 5f) * 5);
            return string.Format(copy.hillNavigation, text, heading, metres);
        }
    }
}
