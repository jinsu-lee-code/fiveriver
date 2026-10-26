using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Core;
using UnityEngine;

namespace FiveRivers.DebugTools
{
    // 실행 빌드 점검 모드 (개발 빌드 전용): 실행 인자 -fiverivers-smoke 로 띄우면
    // 시작 → S1~S5 → 결말 → 시작 화면을 3회 자동으로 돌리고, 회차마다 초기화 상태를 검사한 뒤 종료한다.
    // 결과: 실행 파일 옆 smoke_report.txt (TASKS 1-7 관문 "실행 빌드 3회 연속"의 자동 확인)
    public sealed class SmokeRunner : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const string Arg = "-fiverivers-smoke";
        [SerializeField] FlowDirector director;
        [SerializeField] float speed = 8f;
        [SerializeField] int runs = 3;

        readonly StringBuilder report = new StringBuilder();
        int failures;

        void Start()
        {
            if (!System.Environment.GetCommandLineArgs().Contains(Arg)) return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            director.BaseTimeScale = speed;
            director.ClockScale = speed;
            Time.timeScale = speed;
            var flow = director.Flow;
            var health = FindFirstObjectByType<PlayerHealth>();
            var enemies = FindFirstObjectByType<EnemyDirector>();
            var tank = FindFirstObjectByType<TankController>();
            var weapons = FindFirstObjectByType<WeaponHolder>();
            var player = FindFirstObjectByType<PlayerController>();
            var points = FindFirstObjectByType<LevelPoints>();
            Log($"smoke start {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} version {Application.version} speed x{speed}");
            yield return new WaitForSecondsRealtime(2f);

            for (int run = 1; run <= runs; run++)
            {
                flow.StartRun();
                health.Invulnerable = true;
                yield return null;
                Check(run, "start section", flow.Section == FlowSection.Tutorial);
                Check(run, "player at spawn", Vector3.Distance(player.transform.position, points.playerSpawn.position) < 0.5f);
                Check(run, "ammo full", weapons.M1.Magazine.InClip == weapons.M1.Magazine.ClipSize && !weapons.BazookaUnlocked);
                Check(run, "no enemies", enemies.AliveCount == 0);
                Check(run, "tank hidden", !tank.Visible);
                Check(run, "assist off", !flow.Checkpoints.AssistApplied);

                flow.ReportGoalMet(FlowSection.Tutorial);
                while (flow.Section == FlowSection.Scout || flow.Section == FlowSection.Assault)
                {
                    foreach (var e in FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None))
                        if (e.Alive) e.TakeDamage(9999f, e.transform.position);
                    yield return new WaitForSecondsRealtime(0.3f);
                }
                Check(run, "reached silence", flow.Section == FlowSection.Silence);
                flow.ReportGoalMet(FlowSection.Silence);
                yield return new WaitUntil(() => flow.Section != FlowSection.Silence);
                Check(run, "tank section", flow.Section == FlowSection.Tank && weapons.BazookaUnlocked && tank.Visible);
                yield return new WaitForSecondsRealtime(1f);
                var body = tank.transform.GetChild(0);
                tank.OnRocketHit(body.position, body.position - body.right * 40f);
                tank.OnRocketHit(body.position, body.position - body.right * 40f);
                Check(run, "tank stopped -> dawn", flow.Section == FlowSection.Dawn);
                double endingStart = flow.RunTime;
                yield return new WaitUntil(() => flow.Section == FlowSection.Title);
                Check(run, "ending 40s", System.Math.Abs(flow.RunTime - endingStart - 40.0) < 1.0);
                Log($"run {run} done: run time {flow.RunTime:0.0}s");
            }

            Log(failures == 0 ? "RESULT PASS" : $"RESULT FAIL ({failures})");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "smoke_report.txt"), report.ToString());
            Application.Quit();
        }

        void Check(int run, string what, bool ok)
        {
            if (!ok) failures++;
            Log($"run {run} {(ok ? "ok  " : "FAIL")} {what}");
        }

        void Log(string line)
        {
            report.AppendLine(line);
            Debug.Log("[Smoke] " + line);
        }
#endif
    }
}
