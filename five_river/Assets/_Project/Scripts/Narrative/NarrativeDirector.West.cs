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
            PoseBlendIn.Attach(animator, MansuFallBlend, mansuRifle);
            fallGraph = PlayableGraph.Create("Choi Mansu fall");
            fallGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(fallGraph, "Body", animator);
            var fall = AnimationClipPlayable.Create(fallGraph, mansuFall);
            // the clip spends its first 0.7 s standing upright; he is at his post, so start where it starts to go down
            fall.SetTime(Mathf.Min(MansuFallStart, mansuFall.length * .7f));
            output.SetSourcePlayable(fall);
            fallGraph.Play();
            // the clip ends lying on the plane he knelt on; the trench floor falls away under his legs (2026-11-13)
            mansuRest = LyingBodyRest.Attach(animator);
            // and its last pose is spread-eagled: the arms come down and the head rolls once he is on his back
            mansuPose = FallenPose.Attach(animator);
        }

        // The rifle leaves his hands as he is hit: it tips forward out of them, lands flat on the trench floor in
        // front of where he knelt and rocks once. Scripted rather than a rigid body so the shot is the same every time.
        Coroutine rifleDrop;
        public static Vector3 MansuRifleRest = new Vector3(0.45f, 0f, 0.55f); // his right, up, his front
        public static float MansuRifleDropTime = 0.55f;

        IEnumerator DropMansuRifle(Transform rifle, Transform owner)
        {
            var fromPosition = rifle.position;
            var fromRotation = rifle.rotation;
            var rest = owner.position + owner.right * MansuRifleRest.x + owner.forward * MansuRifleRest.z;
            var terrain = Terrain.activeTerrain;
            float ground = terrain != null ? terrain.SampleHeight(rest) + terrain.transform.position.y : owner.position.y;
            rest.y = ground + 0.035f;
            // flat on its side, lying roughly the way it pointed, turned a little as it fell
            var along = Vector3.ProjectOnPlane(fromRotation * Vector3.forward, Vector3.up);
            if (along.sqrMagnitude < 0.01f) along = owner.forward;
            var restRotation = Quaternion.LookRotation(Quaternion.AngleAxis(-22f, Vector3.up) * along.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 84f);
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
