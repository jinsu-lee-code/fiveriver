using System.Collections;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // 구간별 목표 문구 · 분대장 · 박용팔 대사(자막)와 S4 정적 연출.
    // S4: 탄약 보충 + 박용팔의 호출(만수의 부재)이 끝나면 구간 완료를 보고한다 (GDD 4 · 8번). 정적 구간에는 음악 없음.
    public sealed partial class NarrativeDirector : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] GameText text;
        [SerializeField] ObjectiveDisplay objective;
        [SerializeField] SubtitleDisplay subtitles;
        [SerializeField] LevelPoints points;
        [SerializeField] PlayerController player;
        [SerializeField] Resupplier resupplier;
        [SerializeField] FlareDirector flares;
        [SerializeField] TankController tank;
        [SerializeField] GameObject blanketBody;
        [SerializeField] Animator parkAnimator; // AC_ROK "Call" (박용팔 리그가 오면 연결)
        [SerializeField] float moundRadius = 9f;
        [Header("S4 정적 연출 (초, 구간 시작 기준)")]
        [SerializeField] float parkCall1At = 4f;
        [SerializeField] float parkCall2At = 9f;
        [SerializeField] float silenceSettleAfterCall = 4f;

        public GameText Text => text;

        FlowSection lastCombatHintSection = FlowSection.Title;
        bool lastCombatHintBright;
        bool flareLineSaid;
        bool darkLineSaid;
        bool ricochetLineSaid;
        bool resupplied;
        Coroutine silence;
        WeaponHolder weapons;
        TankEntranceDirector tankEntrance;
        MansuFallDirector mansuCut;
        readonly TankObjectiveTracker tankObjective = new TankObjectiveTracker();

        void OnEnable()
        {
            if (tank != null) tank.SideHit += OnTankSideHit;
        }

        void OnDisable()
        {
            if (tank != null) tank.SideHit -= OnTankSideHit;
        }

        void OnTankSideHit(int hits)
        {
            if (hits != 1 || director.Flow.Section != FlowSection.Tank) return;
            subtitles.Say(text.tankFirstHit);
        }

        void Start()
        {
            objective.SetLabel(text.objectiveLabel);
            weapons = player.GetComponent<WeaponHolder>();
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.RetryRequested += OnRetry;
            flares.Launched += () =>
            {
                if (director.Flow.Section != FlowSection.Assault || flareLineSaid) return;
                flareLineSaid = true;
                subtitles.Say(text.flareUp);
            };
            tank.Ricocheted += () =>
            {
                if (ricochetLineSaid) return;
                ricochetLineSaid = true;
                subtitles.Say(text.tankRicochet);
            };
            resupplier.Resupplied += () => resupplied = true;
            blanketBody.SetActive(false);
            // staged entrance of the tank (TankConfig.entranceTake; 0 = none, the section starts as before)
            tankEntrance = gameObject.AddComponent<TankEntranceDirector>();
            tankEntrance.Configure(director, tank, player, points, subtitles, objective, text, flares);
            // staged first flare of the main assault (expanded run, once per run)
            gameObject.AddComponent<FlareRevealDirector>().Configure(director, player, flares, objective, text);
            mansuCut = gameObject.AddComponent<MansuFallDirector>();
            mansuCut.Configure(director, player, objective, text);
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            subtitles.Clear();
            OnWestRetry(checkpoint);
            lastCombatHintSection = FlowSection.Title;
            if (checkpoint != FlowCheckpoint.Tank) return;
            tankObjective.Reset();
            ricochetLineSaid = false;
            objective.Show(text.objectiveTankMove);
        }

        void OnSectionChanged(FlowTransition t)
        {
            lastCombatHintSection = FlowSection.Title;
            if (silence != null)
            {
                StopCoroutine(silence);
                silence = null;
            }
            switch (t.To)
            {
                case FlowSection.Title:
                    subtitles.Clear();
                    objective.Hide();
                    tankObjective.Reset();
                    blanketBody.SetActive(false);
                    break;
                case FlowSection.Tutorial:
                    subtitles.Clear();
                    flareLineSaid = darkLineSaid = ricochetLineSaid = false;
                    blanketBody.SetActive(false);
                    objective.Show(text.objectiveTutorial);
                    break;
                case FlowSection.Scout:
                    objective.Show(text.objectiveScout);
                    subtitles.Say(text.scoutSpotted);
                    break;
                case FlowSection.Assault:
                    objective.Show(text.objectiveAssault);
                    break;
                case FlowSection.Silence:
                    subtitles.Clear();
                    subtitles.Say(text.retreat);
                    objective.Show(text.objectiveSilence);
                    // The covered body is Choi Man-su: in the expanded run it appears only after he has fallen.
                    blanketBody.SetActive(!director.Flow.IsExpanded || mansuLostThisRun);
                    silence = StartCoroutine(SilenceSequence());
                    break;
                case FlowSection.Tank:
                    tankObjective.Reset();
                    if (t.From != FlowSection.Tank)
                    {
                        subtitles.Clear();
                        // a staged entrance says the line itself, at its own moment
                        if (tankEntrance == null || !tankEntrance.WillPlay(t)) subtitles.Say(text.tankArrives);
                    }
                    objective.Show(text.objectiveTankMove);
                    break;
                case FlowSection.AllySupport:
                    subtitles.Clear();
                    subtitles.Say(text.allySupport);
                    objective.Show(text.objectiveAllySupport);
                    break;
                case FlowSection.Dawn:
                    subtitles.Clear();
                    objective.Show(text.objectiveDawn, ignoreParentGroups: true);
                    break;
                case FlowSection.Result:
                    objective.Hide();
                    break;
            }
            OnWestSectionChanged(t);
        }

        IEnumerator SilenceSequence()
        {
            resupplied = false;
            float start = Time.time;
            yield return new WaitUntil(() => Time.time - start >= parkCall1At);
            if (parkAnimator != null) parkAnimator.SetTrigger("Call");
            subtitles.Say(text.parkCall1);
            yield return new WaitUntil(() => Time.time - start >= parkCall2At);
            subtitles.Say(text.parkCall2);
            yield return new WaitForSeconds(silenceSettleAfterCall);
            yield return new WaitUntil(() => resupplied);
            silence = null;
            director.Flow.ReportGoalMet(FlowSection.Silence);
        }

        void Update()
        {
            var flow = director.Flow;
            if (flow.Section == FlowSection.Scout || flow.Section == FlowSection.Assault)
            {
                // Read the same illumination that drives enemy accuracy. Do not
                // announce movement at fade start while the flare is still bright.
                bool bright = flares.Illumination > 0.1f;
                if (flow.Section != lastCombatHintSection || bright != lastCombatHintBright)
                {
                    if (!bright && lastCombatHintBright && lastCombatHintSection == FlowSection.Assault && !darkLineSaid)
                    {
                        darkLineSaid = true;
                        subtitles.Say(text.flareDown);
                    }
                    string goal = flow.Section == FlowSection.Scout ? text.objectiveScout : text.objectiveAssault;
                    string hint = bright ? text.combatBrightHint : text.combatDarkHint;
                    objective.Show(string.IsNullOrEmpty(hint) ? goal : goal + " · " + hint);
                    lastCombatHintSection = flow.Section;
                    lastCombatHintBright = bright;
                }
            }
            if (flow.Section == FlowSection.Silence && resupplied)
                objective.Hide(); // 보충은 끝났고 호출을 듣는 중
            if (flow.Section == FlowSection.Tank && !tank.IsStopped && (tankEntrance == null || !tankEntrance.Playing))
            {
                float d = Vector3.Distance(player.transform.position, points.moundTop.position);
                tankObjective.Observe(d <= moundRadius,
                    weapons != null && weapons.Current == WeaponSlot.Bazooka && !weapons.Switching);
                switch (tankObjective.Step)
                {
                    case TankObjectiveStep.MoveToMound:
                        // which way and how far, like the hill guide: the step only ends within moundRadius (1.5 m in the
                        // scene) of the trench end, and stopped a few metres short the line stayed "go to the trench end"
                        // with nothing to say how close he was (played through by hand, 2026-11-17 night)
                        string guide = TankMoveGuide();
                        if (lastTankGuide != null && objective.Current == lastTankGuide) objective.UpdateText(guide);
                        else objective.Show(guide);
                        lastTankGuide = guide;
                        break;
                    case TankObjectiveStep.EquipBazooka: lastTankGuide = null; objective.Show(text.objectiveTankEquip); break;
                    case TankObjectiveStep.FireAtSide: lastTankGuide = null; objective.Show(tank.SideHits > 0 ? text.objectiveTankFinish : tank.IsMoving ? text.objectiveTankWait : text.objectiveTankFire); break;
                }
            }
            else lastTankGuide = null;
        }

        string lastTankGuide;

        string TankMoveGuide()
        {
            var delta = points.moundTop.position - player.transform.position;
            delta.y = 0f;
            float angle = Vector3.SignedAngle(player.transform.forward, delta, Vector3.up);
            string heading = Mathf.Abs(angle) < 25f ? text.hillAhead : Mathf.Abs(angle) > 150f ? text.hillBehind : angle > 0 ? text.hillRight : text.hillLeft;
            // metres one by one close in (the step ends 1.5 m from the end), in fives further off
            float distance = delta.magnitude;
            int metres = distance < 7.5f ? Mathf.Max(1, Mathf.RoundToInt(distance)) : Mathf.RoundToInt(distance / 5f) * 5;
            return string.Format(text.hillNavigation, text.objectiveTankMove, heading, metres);
        }
    }
}
