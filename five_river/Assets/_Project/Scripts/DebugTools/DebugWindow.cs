using System.Linq;
using FiveRivers.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FiveRivers.DebugTools
{
    // 디버그 창에 기능을 덧붙이는 컴포넌트. 창을 열 때 씬에서 찾아 아래에 그린다.
    public interface IDebugPanel
    {
        string DebugTitle { get; }
        void DrawDebug();
    }

    // 제작 중 빠른 확인용 디버그 창 (TASKS 1-1b). F1로 연다. 에디터 · 개발 빌드에서만 동작한다.
    // 열려 있는 동안 게임 입력을 막고, 회차 시계는 그대로 흐른다.
    public sealed class DebugWindow : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 시계 강제 버튼이 전환 몇 초 전으로 옮기는지
        const float LeadSeconds = 5f;

        [SerializeField] FlowDirector director;

        bool open;
        Rect rect = new Rect(16, 16, 440, 620);
        Vector2 scroll;
        IDebugPanel[] panels = new IDebugPanel[0];

        static readonly FlowSection[] JumpTargets =
        {
            FlowSection.Tutorial, FlowSection.Scout, FlowSection.Assault, FlowSection.Silence,
            FlowSection.Tank, FlowSection.AllySupport, FlowSection.Dawn, FlowSection.Result,
        };

        public bool IsOpen => open;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) SetOpen(!open);
        }

        void OnDisable()
        {
            if (open) SetOpen(false);
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (open)
            {
                panels = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IDebugPanel>().ToArray();
                director.InputGate.Block(this);
            }
            else
            {
                director.InputGate.Unblock(this);
            }
        }

        void OnGUI()
        {
            if (!open) return;
            rect = GUILayout.Window(GetInstanceID(), rect, DrawWindow, "디버그 (F1)");
        }

        void DrawWindow(int id)
        {
            var flow = director.Flow;
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label($"구간: {SectionLabel(flow.Section)}   회차 {Clock(flow.RunTime)}   구간 {flow.SectionTime:0.0}초");
            GUILayout.Label($"7:00 전환 {Mark(flow.ForcedTankTriggered)}   8:15 전환 {Mark(flow.ForcedEndingTriggered)}   결말 {Mark(flow.EndingStarted)}");
            GUILayout.Label($"일시정지 {Mark(flow.Paused)}   전투 잠금 {Mark(flow.CombatLocked)}   튜토리얼 시간 초과 {Mark(flow.TutorialTimedOut)}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("새 회차")) StartNewRun();
            if (GUILayout.Button("타이틀로")) flow.AbortToTitle();
            if (GUILayout.Button("구간 완료 보고")) flow.ReportGoalMet(flow.Section);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("구간 바로가기 (시계는 기준 시각으로)");
            for (int row = 0; row < JumpTargets.Length; row += 4)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < Mathf.Min(row + 4, JumpTargets.Length); i++)
                    if (GUILayout.Button(SectionLabel(JumpTargets[i]))) flow.DebugJumpTo(JumpTargets[i]);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("회차 시계 강제: 각 전환 5초 전 (구간 경과 시간은 유지)");
            GUILayout.BeginHorizontal();
            var config = director.Config;
            SetClockButton(flow, config.forceTankAt - LeadSeconds);
            SetClockButton(flow, config.forceEndingAt - LeadSeconds);
            SetClockButton(flow, config.hardCap - LeadSeconds);
            if (GUILayout.Button("+10초")) flow.DebugSetRunTime(flow.RunTime + 10);
            if (GUILayout.Button("+60초")) flow.DebugSetRunTime(flow.RunTime + 60);
            GUILayout.EndHorizontal();

            foreach (var panel in panels)
            {
                if (panel as Object == null) continue;
                GUILayout.Space(8);
                GUILayout.Label("■ " + panel.DebugTitle);
                panel.DrawDebug();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        void StartNewRun()
        {
            director.Flow.AbortToTitle();
            director.Flow.StartRun();
        }

        static void SetClockButton(DemoFlow flow, double seconds)
        {
            if (GUILayout.Button(Clock(seconds)) && flow.IsRunning) flow.DebugSetRunTime(seconds);
        }

        static string Clock(double seconds) => $"{(int)(seconds / 60)}:{seconds % 60:00.0}";

        static string Mark(bool on) => on ? "●" : "○";

        public static string SectionLabel(FlowSection section)
        {
            switch (section)
            {
                case FlowSection.Tutorial: return "S1 학습";
                case FlowSection.Scout: return "S2 정찰";
                case FlowSection.Assault: return "S3 공세";
                case FlowSection.Silence: return "S4 정적";
                case FlowSection.Tank: return "S5 전차";
                case FlowSection.AllySupport: return "S5 지원";
                case FlowSection.Dawn: return "S6 새벽";
                case FlowSection.Result: return "S7 결과";
                default: return "타이틀";
            }
        }
#endif
    }
}
