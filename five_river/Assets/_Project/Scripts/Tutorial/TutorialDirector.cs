using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using TMPro;
using UnityEngine;

namespace FiveRivers.Tutorial
{
    // S1 조작 학습: 행동을 판정하고 안내를 한 번에 하나씩 보인다 (GDD 7번 · BALANCE 4번).
    // 105초 상한으로 넘어가면 못 한 행동은 화면 구석 힌트로 남는다 (GDD 14번 1 결정).
    public sealed class TutorialDirector : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] TutorialConfig config;
        [SerializeField] GameText text;
        [SerializeField] PlayerController player;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] TutorialTarget target;

        [Header("화면")]
        [SerializeField] CanvasGroup promptGroup;
        [SerializeField] RectTransform promptBox;
        [SerializeField] TMP_Text promptText;
        [SerializeField] TMP_Text hintText;
        [SerializeField] CanvasGroup remainingGroup;
        [SerializeField] TMP_Text remainingText;
        [SerializeField] float pulseScale = 0.06f;

        TutorialTracker tracker;
        bool active; // 추적 중 (S1 또는 시간 초과 뒤 남은 힌트)

        public TutorialTracker Tracker => tracker;

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
            player.StanceChanged += () => { if (player.Crouched) Report(TutorialStep.Stance); };
            target.Hit += () => Report(TutorialStep.ShootTarget);
            weapons.M1.ReloadStarted += _ => Report(TutorialStep.Reload);
            promptGroup.alpha = 0f;
            remainingGroup.alpha = 0f;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Tutorial)
            {
                tracker = new TutorialTracker(config.highlightAfter, config.hintAfter);
                tracker.Completed += OnCompleted;
                player.ResetTutorialCounters();
                active = true;
            }
            else if (t.To >= FlowSection.Silence || t.To == FlowSection.Title || t.Reason == TransitionReason.DebugJump)
            {
                active = false; // 남은 힌트는 주 공세까지만 (디버그 구간 이동은 학습을 건너뛴 것으로 본다)
            }
        }

        void OnCompleted()
        {
            active = false;
            director.Flow.ReportGoalMet(FlowSection.Tutorial);
        }

        void Report(TutorialStep step)
        {
            if (active && tracker != null) tracker.Report(step);
        }

        void Update()
        {
            var flow = director.Flow;
            bool inTutorial = active && flow.Section == FlowSection.Tutorial;
            bool leftover = active && flow.Section != FlowSection.Tutorial && tracker != null && !tracker.Complete;

            if (active && tracker != null)
            {
                if (player.LookedDegrees >= config.lookDegrees) tracker.Report(TutorialStep.Look);
                if (player.MovedDistance >= config.moveMeters) tracker.Report(TutorialStep.Move);
                if (inTutorial) tracker.Tick(Time.deltaTime);
            }

            promptGroup.alpha = Mathf.MoveTowards(promptGroup.alpha, inTutorial && !tracker.Complete ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            remainingGroup.alpha = Mathf.MoveTowards(remainingGroup.alpha, leftover ? 1f : 0f, Time.unscaledDeltaTime * 4f);

            if (inTutorial && !tracker.Complete)
            {
                var step = tracker.Current;
                promptText.text = $"{Keys(step)}  {text.tutorialSteps[(int)step].prompt}";
                hintText.text = tracker.ShowHint ? text.tutorialSteps[(int)step].hint : "";
                float pulse = tracker.Highlight ? 1f + pulseScale * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
                promptBox.localScale = Vector3.one * pulse;
            }
            if (leftover)
            {
                remainingText.text = text.tutorialRemainingHeader + "\n" +
                                     string.Join("\n", tracker.Remaining.Select(s => $"{Keys(s)} {text.tutorialSteps[(int)s].prompt}"));
            }
        }

        string Keys(TutorialStep step)
        {
            var c = player.Controls;
            switch (step)
            {
                case TutorialStep.Look: return UiStyle.Key(text.mouseKeyLabel);
                case TutorialStep.Move: return UiStyle.Key(GameControls.KeyLabel(c.Move).Replace("/", " "));
                case TutorialStep.Stance: return UiStyle.Key(c.Stance);
                case TutorialStep.ShootTarget: return $"{UiStyle.Key(c.Aim)} {UiStyle.Key(c.Fire)}";
                default: return UiStyle.Key(c.Reload);
            }
        }
    }
}
