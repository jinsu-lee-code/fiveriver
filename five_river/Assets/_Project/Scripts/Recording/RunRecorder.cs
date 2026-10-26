using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.Recording
{
    // 회차 기록기 (FLOW.md 7번, TASKS 2-7): 회차마다 runs.csv에 한 줄. 관찰 테스트 기록용이며 측정 봇은 없다.
    // 위치: Application.persistentDataPath/runs.csv (Windows: %USERPROFILE%\AppData\LocalLow\FiveRivers\FiveRivers)
    public sealed class RunRecorder : MonoBehaviour
    {
        [SerializeField] FlowDirector director;
        [SerializeField] PlayerController player;
        [SerializeField] PlayerHealth health;
        [SerializeField] TankController tank;

        static readonly FlowSection[] Sections =
        {
            FlowSection.Tutorial, FlowSection.Scout, FlowSection.Assault, FlowSection.Silence,
            FlowSection.Tank, FlowSection.AllySupport, FlowSection.Dawn, FlowSection.Result,
        };

        sealed class RunData
        {
            public string id;
            public DateTime start;
            public readonly Dictionary<FlowSection, double> entered = new Dictionary<FlowSection, double>();
            public readonly Dictionary<FlowSection, float> fpsSum = new Dictionary<FlowSection, float>();
            public readonly Dictionary<FlowSection, int> fpsFrames = new Dictionary<FlowSection, int>();
            public readonly Dictionary<FlowSection, float> fpsMin = new Dictionary<FlowSection, float>();
            public readonly List<string> deaths = new List<string>();
            public int frontHits, sideHits;
            public double pausedTime;
        }

        RunData run;
        string path;

        public string FilePath => path;

        void Awake()
        {
            path = Path.Combine(Application.persistentDataPath, "runs.csv");
        }

        void Start()
        {
            director.Flow.SectionChanged += OnSectionChanged;
            health.Died += () =>
            {
                if (run == null) return;
                var p = player.transform.position;
                run.deaths.Add(string.Format(CultureInfo.InvariantCulture, "{0}@{1:0},{2:0}", director.Flow.Section, p.x, p.z));
            };
            tank.Ricocheted += () => { if (run != null) run.frontHits++; };
            tank.SideHit += _ => { if (run != null) run.sideHits++; };
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.Reason == TransitionReason.RunStart)
            {
                run = new RunData { id = DateTime.Now.ToString("yyyyMMdd-HHmmss"), start = DateTime.Now };
            }
            if (run == null) return;
            if (t.To != FlowSection.Title && !run.entered.ContainsKey(t.To)) run.entered[t.To] = t.Time;
            if (t.To == FlowSection.Title)
            {
                string reason = t.Reason == TransitionReason.Timer ? "정상"
                    : t.Reason == TransitionReason.HardCap ? "강제"
                    : t.Reason == TransitionReason.Abort ? "운영자재시작"
                    : t.Reason.ToString();
                Write(reason, t.Time);
                run = null;
            }
        }

        void Update()
        {
            if (run == null) return;
            var flow = director.Flow;
            if (flow.Paused) run.pausedTime += Time.unscaledDeltaTime;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            float fps = 1f / dt;
            var s = flow.Section;
            run.fpsSum.TryGetValue(s, out float sum);
            run.fpsFrames.TryGetValue(s, out int frames);
            run.fpsSum[s] = sum + fps;
            run.fpsFrames[s] = frames + 1;
            if (!run.fpsMin.TryGetValue(s, out float min) || fps < min) run.fpsMin[s] = fps;
        }

        void OnApplicationQuit()
        {
            if (run != null) Write("이탈", director.Flow.RunTime);
        }

        void Write(string endReason, double endTime)
        {
            var flow = director.Flow;
            var inv = CultureInfo.InvariantCulture;
            bool newFile = !File.Exists(path);
            var sb = new StringBuilder();
            if (newFile)
            {
                sb.Append("run_id,start_time,build_version");
                foreach (var s in Sections) sb.Append(",enter_" + s);
                sb.Append(",fails_cp1,fails_cp2,fails_cp3,assist,tank_front_hits,tank_side_hits,ally_support,forced_700,forced_815,paused_s,deaths");
                foreach (var s in Sections) sb.Append($",fps_avg_{s},fps_min_{s}");
                sb.AppendLine(",end_reason,end_time_s");
            }
            sb.Append(run.id).Append(',').Append(run.start.ToString("yyyy-MM-dd HH:mm:ss")).Append(',').Append(Application.version);
            foreach (var s in Sections)
                sb.Append(',').Append(run.entered.TryGetValue(s, out var at) ? at.ToString("0.0", inv) : "");
            sb.Append(',').Append(flow.Checkpoints.Failures(FlowCheckpoint.FirstContact));
            sb.Append(',').Append(flow.Checkpoints.Failures(FlowCheckpoint.MainAssault));
            sb.Append(',').Append(flow.Checkpoints.Failures(FlowCheckpoint.Tank));
            sb.Append(',').Append(flow.Checkpoints.AssistApplied ? 1 : 0);
            sb.Append(',').Append(run.frontHits).Append(',').Append(run.sideHits);
            sb.Append(',').Append(run.entered.ContainsKey(FlowSection.AllySupport) ? 1 : 0);
            sb.Append(',').Append(flow.ForcedTankTriggered ? 1 : 0).Append(',').Append(flow.ForcedEndingTriggered ? 1 : 0);
            sb.Append(',').Append(run.pausedTime.ToString("0.0", inv));
            sb.Append(',').Append(string.Join(" ", run.deaths));
            foreach (var s in Sections)
            {
                bool has = run.fpsFrames.TryGetValue(s, out int frames) && frames > 0;
                sb.Append(',').Append(has ? (run.fpsSum[s] / frames).ToString("0", inv) : "");
                sb.Append(',').Append(has ? run.fpsMin[s].ToString("0", inv) : "");
            }
            sb.Append(',').Append(endReason).Append(',').AppendLine(endTime.ToString("0.0", inv));
            try
            {
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(newFile));
            }
            catch (IOException e)
            {
                Debug.LogWarning("[RunRecorder] FiveRivers: 기록 실패 " + e.Message);
            }
        }
    }
}
