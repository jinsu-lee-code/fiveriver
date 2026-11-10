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
            output.SetSourcePlayable(fall);
            fallGraph.Play();
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
