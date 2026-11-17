using System.Collections;
using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FiveRivers.Narrative
{
    public sealed partial class NarrativeDirector
    {
        [SerializeField] AnimationClip mansuFall;
        [SerializeField] float mansuTalkDistance = 3.3f;
        [SerializeField] float mansuLossAt = 30f;

        AllyCombatant mansu;
        EnemyDirector westEnemies;
        Coroutine westBeat;
        PlayableGraph fallGraph;
        Transform mansuRifle;
        Transform rifleParent;
        Vector3 rifleLocalPosition;
        Quaternion rifleLocalRotation;
        Vector3 rifleLocalScale;
        bool mansuLostThisRun;
        LyingBodyRest mansuRest;
        FallenPose mansuPose;
        // Where the fall clip is joined. It stands upright until 0.7 s; joined there (as it was until 2026-11-13) the
        // kneeling man sprang to his feet in a fifth of a second and only then went down. Joined where the clip has
        // already buckled at the knees, he sags from his kneel and falls back.
        const float MansuFallStart = 1.55f, MansuFallBlend = 0.32f;
        // Hit on his feet (nobody in his sights at that moment, so he was not kneeling): joined that late, the clip had
        // 0.45 s left and he went from upright to flat on his back in six frames, like a plank (seen frame by frame,
        // 2026-11-17). Standing, the clip is joined where its body first gives way (0.27 s of 1.5 s), so the whole
        // fall plays: thrown back, sagging at the knees, down. Standing = hips above this share of his standing height.
        const float MansuFallStartStanding = 0.27f, MansuFallBlendStanding = 0.2f, MansuStandingHips = 0.62f;
        Vector3 blanketOriginalPosition;

        void OnWestSectionChanged(FlowTransition transition)
        {
            StopWestBeat();
            if (transition.To == FlowSection.Title || transition.To == FlowSection.Tutorial)
            {
                RestoreMansu();
                return;
            }
            if (!director.Flow.IsExpanded) return;

            switch (transition.To)
            {
                case FlowSection.Regroup:
                    ResolveWestActors();
                    resupplied = false;
                    objective.Show(text.objectiveRegroupSupply);
                    westBeat = StartCoroutine(RegroupDialogue());
                    break;
                case FlowSection.WestAssault:
                    StartWestBeat();
                    break;
                case FlowSection.Silence:
                    if (mansu != null && mansuLostThisRun)
                    {
                        blanketBody.transform.position = mansu.transform.position;
                        mansu.gameObject.SetActive(false);
                        StopFall();
                    }
                    break;
            }
        }

        void ResolveWestActors()
        {
            if (westEnemies == null) westEnemies = FindFirstObjectByType<EnemyDirector>();
            if (mansu != null) return;
            foreach (var ally in FindObjectsByType<AllyCombatant>(FindObjectsSortMode.None))
            {
                if (ally.gameObject.name != "Ally_1") continue;
                mansu = ally;
                blanketOriginalPosition = blanketBody.transform.position;
                return;
            }
            Debug.LogError("Western trench rifleman Ally_1 is missing.", this);
        }

        IEnumerator RegroupDialogue()
        {
            yield return new WaitUntil(() => resupplied);
            objective.Show(text.objectiveRegroupTalk);
            yield return new WaitUntil(() => mansu != null &&
                Vector3.Distance(player.transform.position, mansu.transform.position) <= mansuTalkDistance);
            objective.Hide();
            subtitles.Say(text.mansuLetter);
            subtitles.Say(text.mansuMorning);
            yield return new WaitUntil(() => !subtitles.Speaking && !director.Flow.Paused);
            westBeat = null;
            director.Flow.ReportGoalMet(FlowSection.Regroup);
        }

        void OnWestRetry(FlowCheckpoint checkpoint)
        {
            if (checkpoint == FlowCheckpoint.WestAssault) StartWestBeat();
        }

        void StartWestBeat()
        {
            StopWestBeat();
            ResolveWestActors();
            objective.Show(text.objectiveWest);
            subtitles.Say(text.westWarning);
            westBeat = StartCoroutine(WestAssaultBeat());
        }

        IEnumerator WestAssaultBeat()
        {
            if (!mansuLostThisRun)
            {
                // Fast volleys can finish the wave early. Play the loss on the last volley in that case.
                yield return new WaitUntil(() => !director.Flow.Paused &&
                    (director.Flow.SectionTime >= mansuLossAt || westEnemies.WestWaveCleared));
                // the view goes to him a moment before he is hit (MansuFallDirector)
                if (mansuCut != null && mansu != null && mansuCut.Begin(mansu.transform))
                    yield return new WaitUntil(() => !mansuCut.Playing || mansuCut.Elapsed >= MansuFallDirector.HitAt);
                ShowMansuLoss();
            }
            yield return new WaitUntil(() => westEnemies.WestWaveCleared &&
                !subtitles.Speaking && !director.Flow.Paused);
            westBeat = null;
            director.Flow.ReportGoalMet(FlowSection.WestAssault);
        }

        void ShowMansuLoss()
        {
            mansuLostThisRun = true;
            subtitles.Say(text.mansuLost);
            if (mansu == null) return;
            mansu.enabled = false;
            var animator = mansu.GetComponentInChildren<Animator>();
            if (animator == null || mansuFall == null)
            {
                mansu.gameObject.SetActive(false);
                return;
            }
            // AlliedM1 normally follows both hands in AllyCombatant.LateUpdate. Until 2026-11-15 it was hung on the
            // firing hand for the fall, and that hand is flung up and stays up in the clip: the rifle stood in the
            // air over him for the rest of the scene. He lets go of it when he is hit and it drops where he knelt.
            mansuRifle = mansu.transform.Find("AlliedM1");
            if (mansuRifle != null)
            {
                rifleParent = mansuRifle.parent;
                rifleLocalPosition = mansuRifle.localPosition;
                rifleLocalRotation = mansuRifle.localRotation;
                rifleLocalScale = mansuRifle.localScale;
                if (rifleDrop != null) StopCoroutine(rifleDrop);
                rifleDrop = StartCoroutine(DropMansuRifle(mansuRifle, mansu.transform));
            }
            // he goes down from the pose he is in (kneeling at his post), not from the controller's standing default
            var hipBone = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            // humanScale is in the avatar's own units; his model is scaled 0.777, so without lossyScale a man standing
            // (hips 0.71 m over his root) read as kneeling (threshold 0.73) and the plank fall stayed (filmed 2026-11-17 evening)
            bool onHisFeet = hipBone != null && hipBone.position.y - animator.transform.position.y
                > MansuStandingHips * animator.humanScale * animator.transform.lossyScale.y;
            PoseBlendIn.Attach(animator, onHisFeet ? MansuFallBlendStanding : MansuFallBlend, mansuRifle);
            fallGraph = PlayableGraph.Create("Choi Mansu fall");
            fallGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(fallGraph, "Body", animator);
            var fall = AnimationClipPlayable.Create(fallGraph, mansuFall);
            // the clip spends its first 0.7 s standing upright; he is at his post, so start where it starts to go down
            float joinAt = Mathf.Min(MansuFallStart, mansuFall.length * .7f);
            fall.SetTime(onHisFeet ? Mathf.Min(MansuFallStartStanding, joinAt) : joinAt);
            output.SetSourcePlayable(fall);
            fallGraph.Play();
            // From his feet the whole fall plays and carries him back 1.6 m; the rear wall is 1.3 m behind his post, so
            // he went down against it and lay propped up on it, stiff, boots in the air (filmed 2026-11-17 evening).
            // Spun by the hit, he turns as he goes down towards where the trench floor has room for him.
            if (onHisFeet)
            {
                float turn = FallTurn(mansu.transform);
                if (mansuTurn != null) StopCoroutine(mansuTurn);
                mansuTurn = Mathf.Abs(turn) > 1f ? StartCoroutine(TurnAsHeFalls(mansu.transform, turn)) : null;
            }
            // the clip ends lying on the plane he knelt on; the trench floor falls away under his legs (2026-11-13)
            mansuRest = LyingBodyRest.Attach(animator);
            // and its last pose is spread-eagled: the arms come down and the head rolls once he is on his back
            mansuPose = FallenPose.Attach(animator);
            // from his feet he reaches the ground that much later: the arms go slack only once he is down
            if (mansuPose != null && onHisFeet) mansuPose.Delay = joinAt - MansuFallStartStanding;
        }

        // The rifle leaves his hands as he is hit: it tips out of them, lands flat on the trench floor at his right
        // and rocks once. Scripted rather than a rigid body so the shot is the same every time.
        Coroutine rifleDrop;
        // his right, up, his front (the front wall is ~0.5 m ahead): the butt end lies 0.38 m from where his feet stay
        public static Vector3 MansuRifleRest = new Vector3(0.75f, 0f, 0.05f);

        // The floor the rifle lands on: the highest solid surface under the point, not the terrain height (the trench is
        // dug below it), leaving out Choi Mansu himself and the rifle.
        static readonly RaycastHit[] floorHits = new RaycastHit[12];
        static float TrenchFloor(Vector3 point, Transform owner, Transform rifle)
        {
            int n = Physics.RaycastNonAlloc(point + Vector3.up * 1.2f, Vector3.down, floorHits, 3f, FiveRivers.Core.WorldQuery.Solid, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                var t = floorHits[i].collider.transform;
                if (t.IsChildOf(owner) || (rifle != null && t.IsChildOf(rifle))) continue;
                best = Mathf.Max(best, floorHits[i].point.y);
            }
            if (!float.IsNegativeInfinity(best)) return best;
            var terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(point) + terrain.transform.position.y : owner.position.y;
        }
        public static float MansuRifleDropTime = 0.55f;

        IEnumerator DropMansuRifle(Transform rifle, Transform owner)
        {
            var fromPosition = rifle.position;
            var fromRotation = rifle.rotation;
            var rest = owner.position + owner.right * MansuRifleRest.x + owner.forward * MansuRifleRest.z;
            rest.y = TrenchFloor(rest, owner, rifle) + 0.035f;
            // Flat on its side along the trench, muzzle away from him. Laid the way it pointed (at the front wall, which
            // is half a metre in front of him) and at the terrain height above the trench, the barrel went into the wall
            // and only the butt stood up out of it; laid muzzle towards him it reached back under his boots (both
            // filmed 2026-11-17 evening).
            var along = Vector3.ProjectOnPlane(owner.right, Vector3.up).normalized;
            var restRotation = Quaternion.LookRotation(Quaternion.AngleAxis(-10f, Vector3.up) * along, Vector3.up) * Quaternion.Euler(0f, 0f, 84f);
            for (float t = 0f; t < MansuRifleDropTime; t += Time.deltaTime)
            {
                if (rifle == null) yield break;
                float u = t / MansuRifleDropTime;
                var p = Vector3.Lerp(fromPosition, rest, u);
                p.y = Mathf.Lerp(fromPosition.y, rest.y, u * u) + 0.08f * Mathf.Sin(u * Mathf.PI); // falls faster as it goes
                rifle.SetPositionAndRotation(p, Quaternion.Slerp(fromRotation, restRotation, Mathf.SmoothStep(0f, 1f, u)));
                yield return null;
            }
            // one small bounce of the muzzle end
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                if (rifle == null) yield break;
                float u = t / 0.22f;
                rifle.SetPositionAndRotation(rest + Vector3.up * (0.025f * Mathf.Sin(u * Mathf.PI)),
                    restRotation * Quaternion.Euler(-3f * Mathf.Sin(u * Mathf.PI), 0f, 0f));
                yield return null;
            }
            if (rifle != null) rifle.SetPositionAndRotation(rest, restRotation);
            rifleDrop = null;
        }

        // How far he is turned as he falls from his feet: the smallest turn (either way, up to MansuMaxTurn) whose
        // backward line is clear for MansuFallRoom along the trench floor at shin height; else the clearest one.
        Coroutine mansuTurn;
        public static float MansuFallRoom = 2.0f, MansuMaxTurn = 75f, MansuTurnFor = 0.55f;
        static readonly RaycastHit[] roomHits = new RaycastHit[12];

        static float FallTurn(Transform body)
        {
            var origin = body.position;
            var start = new Vector3(origin.x, TrenchFloor(origin, body, null) + 0.35f, origin.z);
            var back = Vector3.ProjectOnPlane(-body.forward, Vector3.up).normalized;
            float bestTurn = 0f, bestRoom = Room(start, back, body);
            if (bestRoom >= MansuFallRoom) return 0f;
            for (float turn = 15f; turn <= MansuMaxTurn + 0.1f; turn += 15f)
                foreach (float sign in new[] { 1f, -1f })
                {
                    float room = Room(start, Quaternion.AngleAxis(sign * turn, Vector3.up) * back, body);
                    if (room >= MansuFallRoom) return sign * turn;
                    if (room > bestRoom) { bestRoom = room; bestTurn = sign * turn; }
                }
            return bestTurn;
        }

        static float Room(Vector3 start, Vector3 direction, Transform body)
        {
            int n = Physics.RaycastNonAlloc(start, direction, roomHits, MansuFallRoom + 1f, FiveRivers.Core.WorldQuery.Solid, QueryTriggerInteraction.Ignore);
            float room = MansuFallRoom + 1f;
            for (int i = 0; i < n; i++)
                if (!roomHits[i].collider.transform.IsChildOf(body)) room = Mathf.Min(room, roomHits[i].distance);
            return room;
        }

        // Turns him about where he stands over the first part of the fall (the knees are going, he staggers round).
        IEnumerator TurnAsHeFalls(Transform body, float degrees)
        {
            var pivot = body.position;
            float done = 0f;
            for (float t = 0f; t < MansuTurnFor; t += Time.deltaTime)
            {
                if (body == null) yield break;
                float step = degrees * Mathf.SmoothStep(0f, 1f, t / MansuTurnFor) - done;
                body.RotateAround(pivot, Vector3.up, step);
                done += step;
                yield return null;
            }
            if (body != null) body.RotateAround(pivot, Vector3.up, degrees - done);
            mansuTurn = null;
        }

        void StopWestBeat()
        {
            if (westBeat == null) return;
            StopCoroutine(westBeat);
            westBeat = null;
        }

        void StopFall()
        {
            if (fallGraph.IsValid()) fallGraph.Destroy();
        }

        void RestoreMansu()
        {
            StopFall();
            if (rifleDrop != null) { StopCoroutine(rifleDrop); rifleDrop = null; }
            if (mansuTurn != null) { StopCoroutine(mansuTurn); mansuTurn = null; }
            if (mansuRest != null) mansuRest.Release();
            mansuRest = null;
            if (mansuPose != null) mansuPose.Release();
            mansuPose = null;
            if (mansu != null)
            {
                if (mansuRifle != null && rifleParent != null)
                {
                    mansuRifle.SetParent(rifleParent, false);
                    mansuRifle.SetLocalPositionAndRotation(rifleLocalPosition, rifleLocalRotation);
                    mansuRifle.localScale = rifleLocalScale;
                    rifleParent = null;
                }
                blanketBody.transform.position = blanketOriginalPosition;
                mansu.gameObject.SetActive(true);
                mansu.enabled = true;
                // His own Title handler may have run while he was disabled; reset him here as well.
                mansu.ResetForNewRun();
                if (mansuLostThisRun)
                {
                    var animator = mansu.GetComponentInChildren<Animator>();
                    if (animator != null) animator.Rebind();
                }
            }
            mansuLostThisRun = false;
        }

        void OnDestroy() => StopFall();
    }
}
