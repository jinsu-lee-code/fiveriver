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
    public sealed class NarrativeDirector : MonoBehaviour
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
        [SerializeField] float moundRadius = 9f;
        [Header("S4 정적 연출 (초, 구간 시작 기준)")]
        [SerializeField] float parkCall1At = 4f;
        [SerializeField] float parkCall2At = 9f;
        [SerializeField] float silenceSettleAfterCall = 4f;

        bool flareLineSaid;
        bool darkLineSaid;
        bool ricochetLineSaid;
        bool resupplied;
        bool callsDone;
        Coroutine silence;

        void Start()
        {
            objective.SetLabel(text.objectiveLabel);
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.RetryRequested += _ => subtitles.Clear();
            flares.Launched += () =>
            {
                if (director.Flow.Section != FlowSection.Assault || flareLineSaid) return;
                flareLineSaid = true;
                subtitles.Say(text.flareUp);
            };
            flares.Darkening += () =>
            {
                if (director.Flow.Section != FlowSection.Assault || darkLineSaid) return;
                darkLineSaid = true;
                subtitles.Say(text.flareDown);
            };
            tank.Ricocheted += () =>
            {
                if (ricochetLineSaid) return;
                ricochetLineSaid = true;
                subtitles.Say(text.tankRicochet);
            };
            resupplier.Resupplied += () => resupplied = true;
            blanketBody.SetActive(false);
        }

        void OnSectionChanged(FlowTransition t)
        {
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
                    blanketBody.SetActive(true);
                    silence = StartCoroutine(SilenceSequence());
                    break;
                case FlowSection.Tank:
                    if (t.From != FlowSection.Tank)
                    {
                        subtitles.Clear();
                        subtitles.Say(text.tankArrives);
                    }
                    objective.Show(text.objectiveTankMove);
                    break;
                case FlowSection.AllySupport:
                    subtitles.Clear();
                    subtitles.Say(text.allySupport);
                    objective.Show(text.objectiveAllySupport);
                    break;
                case FlowSection.Dawn:
                case FlowSection.Result:
                    objective.Hide();
                    break;
            }
        }

        IEnumerator SilenceSequence()
        {
            resupplied = false;
            callsDone = false;
            float start = Time.time;
            yield return new WaitUntil(() => Time.time - start >= parkCall1At);
            subtitles.Say(text.parkCall1);
            yield return new WaitUntil(() => Time.time - start >= parkCall2At);
            subtitles.Say(text.parkCall2);
            yield return new WaitForSeconds(silenceSettleAfterCall);
            callsDone = true;
            yield return new WaitUntil(() => resupplied);
            silence = null;
            director.Flow.ReportGoalMet(FlowSection.Silence);
        }

        void Update()
        {
            var flow = director.Flow;
            if (flow.Section == FlowSection.Silence && resupplied && callsDone == false)
                objective.Hide(); // 보충은 끝났고 호출을 듣는 중
            if (flow.Section == FlowSection.Tank && !tank.IsStopped)
            {
                float d = Vector3.Distance(player.transform.position, points.moundTop.position);
                objective.Show(d <= moundRadius ? text.objectiveTankFire : text.objectiveTankMove);
            }
        }
    }
}
