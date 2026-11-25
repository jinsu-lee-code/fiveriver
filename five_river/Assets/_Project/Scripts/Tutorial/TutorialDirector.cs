using System;
using System.Collections;
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
        bool manualReloadPending;
        int runToken;          // 늦게 끝난 대기가 다음 회차의 학습을 끝내지 않게
        Coroutine pendingGoal;

        public TutorialTracker Tracker => tracker;
        // 다섯 조작을 다 익혔다. 정찰 구간은 TutorialConfig.holdBridge 뒤에 시작한다 (2026-11-26, TrenchIntroDirector가 안내).
        public event Action Completed;

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
            player.StanceChanged += () => { if (player.Crouched) Report(TutorialStep.Stance); };
            target.Hit += () =>
            {
                if (weapons.M1.AimBlend > 0.5f) Report(TutorialStep.ShootTarget);
            };
            weapons.M1.ReloadStarted += _ => manualReloadPending = active && player.Controls.Reload.WasPressedThisFrame();
            weapons.M1.Reloaded += () =>
            {
                if (manualReloadPending) Report(TutorialStep.Reload);
                manualReloadPending = false;
            };
            weapons.LoadoutReset += () => manualReloadPending = false;
            promptGroup.alpha = 0f;
            remainingGroup.alpha = 0f;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To != FlowSection.Tutorial) CancelPendingGoal();
            if (t.To == FlowSection.Tutorial)
            {
                CancelPendingGoal();
                runToken++;
                manualReloadPending = false;
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
            Completed?.Invoke();
            if (config.holdBridge <= 0f) director.Flow.ReportGoalMet(FlowSection.Tutorial);
            else pendingGoal = StartCoroutine(ReportGoalLater(runToken, config.holdBridge));
        }

        // 게임 시간으로 기다린다 (일시정지 중에는 멈춘다).
        IEnumerator ReportGoalLater(int token, float delay)
        {
            yield return new WaitForSeconds(delay);
            pendingGoal = null;
            if (token == runToken) director.Flow.ReportGoalMet(FlowSection.Tutorial);
        }

        void CancelPendingGoal()
        {
            if (pendingGoal == null) return;
            StopCoroutine(pendingGoal);
            pendingGoal = null;
        }

        void Report(TutorialStep step)
        {
            if (active && tracker != null) tracker.Report(step);
        }

        void Update()
        {
            var flow = director.Flow;
            // 첫 안내는 분대장의 첫 대사를 들은 뒤에 띄운다 (TutorialConfig.promptDelay). 그동안 정체 시간도 세지 않는다.
            bool inTutorial = active && flow.Section == FlowSection.Tutorial && flow.SectionTime >= config.promptDelay;
            bool leftover = active && flow.Section != FlowSection.Tutorial && tracker != null && !tracker.Complete;
            // The practice board stands 18 m in front of the trench. It stayed there for the whole night, a black
            // man-shaped board in the middle of the fighting (2026-11-15). It is kept only while it is needed: on the
            // title and in the tutorial, and afterwards while the shot at it is still owed.
            bool showTarget = flow.Section == FlowSection.Title || flow.Section == FlowSection.Tutorial
                || (leftover && tracker.Remaining.Contains(TutorialStep.ShootTarget));
            if (target != null && target.gameObject.activeSelf != showTarget) target.gameObject.SetActive(showTarget);

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
