using System.Collections.Generic;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Narrative
{
    // The path follows the right trench opening and the authored east-slope cover positions.
    public sealed class HillAssaultDirector : MonoBehaviour
    {
        enum Stage { Exit, LowerSlope, UpperSlope, Ridge, Summit }

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
        bool started;
        bool summitReached;
        readonly List<Transform> pendingDefenders = new List<Transform>();
        float nextSpawnAttempt;
        Vector3? companionGoal;
        readonly List<EnemyAgent> defenders = new List<EnemyAgent>();
        readonly Plane[] planes = new Plane[6];

        public void Configure(FlowDirector flow, DawnDirector arrival, LevelPoints level, Camera camera)
        {
            director = flow;
            dawn = arrival;
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
            if (change.From == FlowSection.HillAssault || change.To == FlowSection.HillAssault)
                ResetAssault();
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            if (checkpoint == FlowCheckpoint.HillAssault) ResetAssault();
        }

        void ResetAssault()
        {
            started = false;
            summitReached = false;
            pendingDefenders.Clear();
            nextSpawnAttempt = 0f;
            stage = Stage.Exit;
            companionGoal = null;
            defenders.Clear(); // EnemyDirector owns actual agent cleanup on retry/exit.
            player.SetHillExit(null);
        }

        void Update()
        {
            var flow = director.Flow;
            if (!flow.IsExpanded || flow.Paused) return;
            if (flow.Section == FlowSection.Dawn)
            {
                var student = dawn.TaeSeok;
                if (student == null) return;
                float distance = Distance(student.position);
                bool near = distance <= 3.2f && student.gameObject.activeInHierarchy;
                string cue = near ? copy.meetTaeSeok : distance <= 4f ? copy.watchTruck : Guide(copy.findTaeSeok, student.position);
                objective.Show(cue, true);
                if (near && player.Gate.CanAct && player.Controls.Resupply.WasPressedThisFrame())
                {
                    subtitles.Say(copy.taeSeokMeets);
                    flow.ReportGoalMet(FlowSection.Dawn);
                }
                return;
            }
            if (flow.Section != FlowSection.HillAssault) return;
            if (!started)
            {
                started = true;
                player.SetHillExit(ExitPoint.x);
                subtitles.Say(copy.taeSeokDeparts);
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
            MoveCompanionWhenUnseen();
            var goal = StageGoal();
            int remaining = pendingDefenders.Count;
            foreach (var defender in defenders)
                if (defender != null && defender.Alive) remaining++;
            string target = StageName();
            string cueText = remaining > 0 ? string.Format(copy.hillDefenderCount, target, remaining) : target;
            if (remaining != 0 || Distance(goal) > (stage == Stage.Exit ? 4f : 5.5f))
            {
                objective.Show(Guide(cueText, goal), true);
                return;
            }
            if (stage == Stage.Summit)
            {
                if (!summitReached)
                {
                    summitReached = true;
                    AssignCompanionCover(goal);
                }
                // Deliberate action at the captured crest closes the episode.
                objective.Show(copy.hillReport, true);
                if (player.Gate.CanAct && player.Controls.Resupply.WasPressedThisFrame())
                    flow.ReportGoalMet(FlowSection.HillAssault);
                return;
            }
            AssignCompanionCover(goal);
            stage++;
            defenders.Clear();
            switch (stage)
            {
                case Stage.LowerSlope:
                    Spawn(points.assaultCovers[8]);
                    subtitles.Say(copy.taeSeokLower);
                    break;
                case Stage.UpperSlope:
                    Spawn(points.assaultCovers[2]);
                    Spawn(points.assaultCovers[1]);
                    subtitles.Say(copy.taeSeokUpper);
                    break;
                case Stage.Ridge:
                    Spawn(points.assaultCovers[9]);
                    subtitles.Say(copy.taeSeokRidge);
                    break;
                case Stage.Summit:
                    Spawn(points.enemySpawns[2]);
                    subtitles.Say(copy.taeSeokSummit);
                    break;
            }
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
                default: return points.enemySpawns[2].position;
            }
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

        void AssignCompanionCover(Vector3 secured)
        {
            Vector3 offset = stage == Stage.Summit ? Vector3.back * 4.5f : new Vector3(-2.5f, 0f, -3f);
            companionGoal = NavMesh.SamplePosition(secured + offset, out var hit, 5f, NavMesh.AllAreas)
                ? hit.position : secured;
        }

        void MoveCompanionWhenUnseen()
        {
            if (!companionGoal.HasValue) return;
            var student = dawn.TaeSeok;
            if (student == null) { companionGoal = null; return; }
            var next = companionGoal.Value;
            GeometryUtility.CalculateFrustumPlanes(view, planes);
            var body = new Bounds(student.position + Vector3.up, new Vector3(1.3f, 2.4f, 1.3f));
            if (GeometryUtility.TestPlanesAABB(planes, body)) return;
            body.center = next + Vector3.up;
            if (GeometryUtility.TestPlanesAABB(planes, body)) return;
            var heading = StageGoal() - next;
            heading.y = 0f;
            var facing = heading.sqrMagnitude > .01f ? Quaternion.LookRotation(heading) : student.rotation;
            student.SetPositionAndRotation(next, facing);
            companionGoal = null;
        }
    }
}
