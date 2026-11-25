using System.Collections;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.Tutorial;
using FiveRivers.UI;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // 시작 컷씬에서 참호 방어로 자연스럽게 잇는 안내 (2026-11-26 피드백).
    // 1) 회차가 시작되어 참호가 밝아지면 분대장이 먼저 말을 건다 → 조작 안내는 TutorialConfig.promptDelay 뒤.
    // 2) 다섯 조작을 다 익히면 "제자리 지켜라" 대사와 목표를 보이고, holdBridge 뒤 정찰 구간이 시작된다.
    // 3) 그 대기와 정찰 구간 동안 골짜기를 보고 있지 않으면 목표 옆에 돌 방향을 붙인다.
    // NarrativeDirector가 Start에서 붙인다 (씬을 다시 만들지 않아도 된다).
    public sealed class TrenchIntroDirector : MonoBehaviour
    {
        [SerializeField] float openingDelay = 1.2f; // 참호가 밝아지는 동안 (IntroCard.Reveal) 첫 대사를 아낀다

        FlowDirector director;
        SubtitleDisplay subtitles;
        ObjectiveDisplay objective;
        GameText text;
        TutorialDirector tutorial;
        Vector3 valley;     // 적이 오는 쪽 (정찰병 엄폐 지점의 가운데)
        bool hasValley;
        bool holding;       // 학습을 마치고 정찰병을 기다리는 중
        bool guiding;
        Coroutine opening;

        public void Configure(FlowDirector director, SubtitleDisplay subtitles, ObjectiveDisplay objective, GameText text, LevelPoints points)
        {
            this.director = director;
            this.subtitles = subtitles;
            this.objective = objective;
            this.text = text;
            hasValley = TryValley(points, out valley);
            director.Flow.SectionChanged += OnSectionChanged;
            tutorial = FindAnyObjectByType<TutorialDirector>();
            if (tutorial != null) tutorial.Completed += OnTutorialCompleted;
        }

        void OnDestroy()
        {
            if (director != null && director.Flow != null) director.Flow.SectionChanged -= OnSectionChanged;
            if (tutorial != null) tutorial.Completed -= OnTutorialCompleted;
        }

        static bool TryValley(LevelPoints points, out Vector3 center)
        {
            center = Vector3.zero;
            if (points == null) return false;
            var marks = points.scoutCovers != null && points.scoutCovers.Length > 0 ? points.scoutCovers : points.enemySpawns;
            if (marks == null) return false;
            int n = 0;
            foreach (var m in marks)
            {
                if (m == null) continue;
                center += m.position;
                n++;
            }
            if (n == 0) return false;
            center /= n;
            return true;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (opening != null)
            {
                StopCoroutine(opening);
                opening = null;
            }
            holding = false;
            guiding = false;
            if (t.To == FlowSection.Tutorial && t.Reason == TransitionReason.RunStart)
                opening = StartCoroutine(Opening());
        }

        IEnumerator Opening()
        {
            yield return new WaitForSecondsRealtime(openingDelay);
            opening = null;
            if (director.Flow.Section != FlowSection.Tutorial) yield break;
            subtitles.Say(text.introLeaderWake);
            subtitles.Say(text.introLeaderPractice);
        }

        void OnTutorialCompleted()
        {
            if (director.Flow.Section != FlowSection.Tutorial) return;
            holding = true;
            subtitles.Say(text.holdTrenchLine);
            objective.Show(text.objectiveHoldTrench);
        }

        void LateUpdate()
        {
            if (!hasValley || director == null) return;
            var flow = director.Flow;
            string goal = flow.Section == FlowSection.Scout ? text.objectiveScout
                : holding && flow.Section == FlowSection.Tutorial ? text.objectiveHoldTrench
                : null;
            if (string.IsNullOrEmpty(goal)) return;
            // 다른 연출이 목표 줄을 바꿨으면 손대지 않는다
            string current = objective.Current;
            if (current != goal && !current.StartsWith(goal + " · ")) return;

            var cam = Camera.main;
            if (cam == null) return;
            Vector3 look = Flat(cam.transform.forward);
            Vector3 toValley = Flat(valley - cam.transform.position);
            if (look == Vector3.zero || toValley == Vector3.zero) return;

            var turn = TrenchGuide.Turn(Vector3.SignedAngle(look, toValley, Vector3.up), guiding);
            guiding = turn != TrenchTurn.None;
            string want = guiding ? goal + " · " + Words(turn) : goal;
            if (current != want) objective.UpdateText(want);
        }

        string Words(TrenchTurn turn)
        {
            switch (turn)
            {
                case TrenchTurn.Left: return text.guideTurnLeft;
                case TrenchTurn.Right: return text.guideTurnRight;
                default: return text.guideTurnBack;
            }
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.zero : v.normalized;
        }
    }
}
