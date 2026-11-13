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
            // AlliedM1 normally follows both hands in AllyCombatant.LateUpdate. Follow the
            // firing hand while that controller is suspended so the rifle falls with him.
            mansuRifle = mansu.transform.Find("AlliedM1");
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (mansuRifle != null && hand != null)
            {
                rifleParent = mansuRifle.parent;
                rifleLocalPosition = mansuRifle.localPosition;
                rifleLocalRotation = mansuRifle.localRotation;
                rifleLocalScale = mansuRifle.localScale;
                mansuRifle.SetParent(hand, true);
            }
            fallGraph = PlayableGraph.Create("Choi Mansu fall");
            fallGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(fallGraph, "Body", animator);
            var fall = AnimationClipPlayable.Create(fallGraph, mansuFall);
            // the clip spends its first 0.7 s standing upright; he is at his post, so start where it starts to go down
            fall.SetTime(Mathf.Min(MansuFallStart, mansuFall.length * .7f));
            // The clip starts upright and he is usually kneeling at his post: ease out of the pose he is in
            // (the hit straightens him) instead of snapping to the clip's first frame.
            var mixer = AnimationMixerPlayable.Create(fallGraph, 2);
            if (animator.runtimeAnimatorController != null)
                fallGraph.Connect(AnimatorControllerPlayable.Create(fallGraph, animator.runtimeAnimatorController), 0, mixer, 0);
            fallGraph.Connect(fall, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f);
            mixer.SetInputWeight(1, 0f);
            output.SetSourcePlayable(mixer);
            fallGraph.Play();
            StartCoroutine(BlendIntoFall(fallGraph, mixer));
            // the clip ends lying on the plane he knelt on; the trench floor falls away under his legs (2026-11-13)
            mansuRest = LyingBodyRest.Attach(animator);
        }

        IEnumerator BlendIntoFall(PlayableGraph graph, AnimationMixerPlayable mixer)
        {
            const float blend = MansuFallBlend;
            for (float t = 0f; t < blend; t += Time.deltaTime)
            {
                if (!graph.IsValid()) yield break;
                float w = Mathf.SmoothStep(0f, 1f, t / blend);
                mixer.SetInputWeight(0, 1f - w);
                mixer.SetInputWeight(1, w);
                yield return null;
            }
            if (!graph.IsValid()) yield break;
            mixer.SetInputWeight(0, 0f);
            mixer.SetInputWeight(1, 1f);
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
            if (mansuRest != null) mansuRest.Release();
            mansuRest = null;
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
