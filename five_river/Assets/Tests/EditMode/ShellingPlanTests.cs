using System.Collections.Generic;
using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 포격 일정 (2026-11-18): 구간 규칙 · 정적 구간의 조용함 · 막힌 동안 시작 금지 · 안전 거리.
    public class ShellingPlanTests
    {
        const string ConfigPath = "Assets/_Project/Data/ShellingConfig.asset";
        System.Random rng;

        float Rand() => (float)rng.NextDouble();

        [SetUp]
        public void SetUp() => rng = new System.Random(7);

        static ShellingConfig Asset()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShellingConfig>(ConfigPath);
            Assert.IsNotNull(config, "ShellingConfig.asset 없음 (FiveRivers/Setup/Shelling)");
            return config;
        }

        List<ShellingPlan.Event> Run(ShellingConfig config, FlowSection section, bool expanded, float seconds, System.Func<float, bool> blocked = null)
        {
            var plan = new ShellingPlan(config, Rand);
            plan.Enter(section, expanded);
            var all = new List<ShellingPlan.Event>();
            for (float t = 0f; t < seconds; t += 0.1f) plan.Tick(0.1f, blocked != null && blocked(t), all);
            return all;
        }

        [Test]
        public void Silence_HasNoCloseShelling_OnlyFarAndFaint()
        {
            var config = Asset();
            var events = Run(config, FlowSection.Silence, true, 3000f);
            Assert.AreEqual(0, events.Count(e => e.kind == ShellingPlan.Kind.NearShell), "정적 구간에 가까운 포격");
            Assert.AreEqual(0, events.Count(e => e.kind == ShellingPlan.Kind.Tracer || e.kind == ShellingPlan.Kind.Gunfire), "정적 구간에 예광탄 · 총성");
            var far = events.Where(e => e.kind == ShellingPlan.Kind.DistantFlash).ToArray();
            Assert.Greater(far.Length, 0, "정적 구간에도 아주 먼 포성은 있다");
            Assert.IsTrue(config.TryRule(FlowSection.Assault, out var assault));
            Assert.Less(far.Max(e => e.level), assault.distantLevel * 0.6f, "정적 구간의 먼 포성은 낮아야 한다");
        }

        [Test]
        public void SectionsWithoutARule_AreQuiet()
        {
            var config = Asset();
            foreach (var section in new[] { FlowSection.Title, FlowSection.Dawn, FlowSection.Result, FlowSection.AllySupport })
                Assert.AreEqual(0, Run(config, section, true, 1000f).Count, section + " 구간에 포격");
        }

        [Test]
        public void EveryFightingSection_GetsCloseShells_WithinItsDistanceBand()
        {
            var config = Asset();
            foreach (var section in new[] { FlowSection.Tutorial, FlowSection.Scout, FlowSection.Assault, FlowSection.WestAssault, FlowSection.Tank, FlowSection.HillAssault })
            {
                Assert.IsTrue(config.TryRule(section, out var rule), section + " 규칙 없음");
                var near = Run(config, section, true, 1500f).Where(e => e.kind == ShellingPlan.Kind.NearShell).ToArray();
                Assert.Greater(near.Length, 0, section + " 구간에 가까운 포격이 없다");
                foreach (var e in near)
                {
                    Assert.GreaterOrEqual(e.distance, rule.playerMin - 0.001f, section + " 너무 가깝다");
                    Assert.LessOrEqual(e.distance, rule.playerMax + 0.001f, section + " 너무 멀다");
                }
                // 간격: 정한 최소 간격보다 잦지 않다
                Assert.LessOrEqual(near.Length, 1500f / rule.nearMin + 1f, section + " 너무 잦다");
            }
        }

        [Test]
        public void Tutorial_IsFarAndRare_AssaultIsCloserAndMoreOften()
        {
            var config = Asset();
            config.TryRule(FlowSection.Tutorial, out var tutorial);
            config.TryRule(FlowSection.Assault, out var assault);
            config.TryRule(FlowSection.Tank, out var tank);
            Assert.Greater(tutorial.playerMin, assault.playerMin * 2f);
            Assert.Greater(tutorial.nearMin, assault.nearMin * 1.5f);
            Assert.Greater(tank.nearMin, assault.nearMin * 1.5f, "전차 구간은 전차 포와 헷갈리지 않게 드물어야 한다");
        }

        [Test]
        public void Regroup_DropsNoMoreThanItsCount()
        {
            var config = Asset();
            config.TryRule(FlowSection.Regroup, out var rule);
            Assert.Greater(rule.nearMaxCount, 0);
            var near = Run(config, FlowSection.Regroup, true, 2000f).Count(e => e.kind == ShellingPlan.Kind.NearShell);
            Assert.AreEqual(rule.nearMaxCount, near);
        }

        [Test]
        public void NoCloseShell_StartsWhileBlocked_AndNotStraightAfter()
        {
            var config = Asset();
            var plan = new ShellingPlan(config, Rand);
            plan.Enter(FlowSection.Assault, true);
            var events = new List<ShellingPlan.Event>();
            // 연출 컷 · 대사가 300초 동안 이어진다
            for (float t = 0f; t < 300f; t += 0.1f) plan.Tick(0.1f, true, events);
            Assert.AreEqual(0, events.Count(e => e.kind == ShellingPlan.Kind.NearShell), "막힌 동안 가까운 포격이 시작됐다");
            Assert.Greater(events.Count(e => e.kind == ShellingPlan.Kind.DistantFlash), 0, "먼 포격은 막히지 않는다");
            events.Clear();
            float waited = 0f;
            while (!events.Any(e => e.kind == ShellingPlan.Kind.NearShell) && waited < 200f) { plan.Tick(0.1f, false, events); waited += 0.1f; }
            Assert.GreaterOrEqual(waited, config.holdAfterBlock - 0.11f, "풀리자마자 떨어졌다");
            Assert.Less(waited, 200f, "풀린 뒤에도 떨어지지 않는다");
        }

        [Test]
        public void ExhibitFlow_HasSmallBarragesOnly_NoBigOneAndNoBreather()
        {
            var config = Asset();
            // 전시용 짧은 흐름(9:00 상한): 숨 고르기 시간이 드는 큰 탄막은 없다. 작은 탄막과 먼 포격은 있다.
            Assert.IsFalse(ShellingPlan.BigBarrageAllowed(config, false));
            Assert.IsTrue(ShellingPlan.BigBarrageAllowed(config, true));
            Assert.IsTrue(config.nearInExhibit);
            var events = Run(config, FlowSection.Assault, false, 1500f);
            Assert.Greater(events.Count(e => e.kind == ShellingPlan.Kind.NearShell), 0);
            Assert.Greater(events.Count(e => e.kind == ShellingPlan.Kind.DistantFlash), 0);
            Assert.AreEqual(0, Run(config, FlowSection.Silence, false, 1500f).Count(e => e.kind == ShellingPlan.Kind.NearShell), "전시용 정적 구간에도 가까운 포격은 없다");
        }

        // ---- 탄막 (2026-11-18 추가 지시: "한 발씩이 아니라 여러 발 떨구고 우리 진지 앞에도 떨어지고") ----
        [Test]
        public void CloseShelling_ComesAsSeveralRounds()
        {
            var config = Asset();
            Assert.GreaterOrEqual(config.smallRoundsMin, 2, "한 발씩이 아니라 여러 발");
            Assert.LessOrEqual(config.smallRoundsMax, 4);
            var near = Run(config, FlowSection.Assault, true, 1500f).Where(e => e.kind == ShellingPlan.Kind.NearShell).ToArray();
            foreach (var e in near)
            {
                Assert.GreaterOrEqual(e.rounds, config.smallRoundsMin);
                Assert.LessOrEqual(e.rounds, config.smallRoundsMax);
            }
            Assert.IsTrue(near.Any(e => e.rounds > config.smallRoundsMin), "발수가 늘 같다");
        }

        static List<ShellSafety.Keepout> BarrageKeepouts(ShellingConfig config, Vector2 player, out Vector3[] allies, out Vector3[] crates)
        {
            allies = new[] { new Vector3(-14f, 0f, -0.4f), new Vector3(11f, 0f, -0.4f), new Vector3(16f, 0f, -0.4f), new Vector3(-6.5f, 0f, -1.2f) };
            crates = new[] { new Vector3(5f, 0f, -1.6f), new Vector3(17.5f, 0f, -1.6f) };
            var keep = new List<ShellSafety.Keepout> { new ShellSafety.Keepout(new Vector3(player.x, 0f, player.y), config.bigKeepPlayer) };
            foreach (var a in allies) keep.Add(new ShellSafety.Keepout(a, config.bigKeepAlly));
            foreach (var c in crates) keep.Add(new ShellSafety.Keepout(c, config.bigKeepCrate));
            foreach (var cover in new[] { new Vector3(-16, 0, 29), new Vector3(0, 0, 28), new Vector3(21, 0, 30), new Vector3(-2, 0, 40) })
                keep.Add(new ShellSafety.Keepout(cover, config.keepCover));
            return keep;
        }

        [Test]
        public void BigBarrage_WalksIn_AndEndsRightInFrontOfTheParapet_ButNeverInTheTrenchOrOnAnyone()
        {
            var config = Asset();
            Assert.GreaterOrEqual(config.bigRoundsMin, 4);
            Assert.LessOrEqual(config.bigRoundsMax, 10);
            Assert.GreaterOrEqual(config.bigSeconds, 2f);
            Assert.LessOrEqual(config.bigSeconds, 6f);
            Assert.GreaterOrEqual(config.bigCloseMin, 5f);
            Assert.LessOrEqual(config.bigCloseMax, 12f);
            // 플레이어가 참호 어디에 서 있든 (가운데 · 왼쪽 끝 · 오른쪽 끝 · 탄약 상자 곁)
            foreach (var player in new[] { new Vector2(0f, -0.5f), new Vector2(-17f, -0.6f), new Vector2(19f, -0.8f), new Vector2(5.5f, -1f) })
                for (int seed = 0; seed < 40; seed++)
                {
                    rng = new System.Random(seed);
                    var keep = BarrageKeepouts(config, player, out var allies, out var crates);
                    var rounds = Barrage.Walk(config, player, Trench, keep, Rand);
                    Assert.GreaterOrEqual(rounds.Count, 4, "탄막이 너무 성기다");
                    Assert.LessOrEqual(rounds.Count, config.bigRoundsMax);
                    float last = -1f;
                    foreach (var r in rounds)
                    {
                        Assert.GreaterOrEqual(r.time, last, "순서대로 떨어진다");
                        last = r.time;
                        Assert.LessOrEqual(r.time, config.bigSeconds + 0.001f);
                        // 참호 안 · 흉벽 위에는 떨어지지 않는다
                        Assert.IsFalse(r.point.x > Trench.xMin - config.bigKeepTrench && r.point.x < Trench.xMax + config.bigKeepTrench
                            && r.point.y > Trench.yMin - config.bigKeepTrench && r.point.y < Trench.yMax + config.bigKeepTrench, "참호에 떨어졌다");
                        Assert.GreaterOrEqual((r.point - player).magnitude, config.bigKeepPlayer - 0.001f, "플레이어 안전 거리");
                        foreach (var a in allies) Assert.GreaterOrEqual((r.point - new Vector2(a.x, a.z)).magnitude, config.bigKeepAlly - 0.001f, "아군 안전 거리");
                        foreach (var c in crates) Assert.GreaterOrEqual((r.point - new Vector2(c.x, c.z)).magnitude, config.bigKeepCrate - 0.001f, "탄약 상자 안전 거리");
                        if (r.close)
                        {
                            float ahead = r.point.y - Trench.yMax;
                            Assert.GreaterOrEqual(ahead, config.bigCloseMin - 0.001f, "흉벽에 너무 가깝다");
                            Assert.LessOrEqual(ahead, config.bigCloseMax + 0.001f, "진지 바로 앞이 아니다");
                        }
                    }
                    // 들판에서 참호 쪽으로 걸어 들어온다: 진지 바로 앞의 발은 끝에 오고, 첫 발은 멀다
                    Assert.IsTrue(rounds.Any(r => r.close), "진지 바로 앞에 떨어지는 발이 없다");
                    Assert.IsFalse(rounds[0].close);
                    Assert.Greater(rounds[0].point.y - Trench.yMax, 35f, "첫 발은 들판 먼 곳");
                    int firstClose = rounds.FindIndex(r => r.close);
                    for (int i = firstClose; i < rounds.Count; i++) Assert.IsTrue(rounds[i].close, "가까운 발 뒤에 먼 발이 다시 온다");
                    Assert.Less(rounds[rounds.Count - 1].point.y, rounds[0].point.y - 30f);
                }
        }

        [Test]
        public void Breather_NoEnemyFire_NoNewShelling_ForItsWholeLength()
        {
            var config = Asset();
            Assert.GreaterOrEqual(config.breatherSeconds, 3f);
            Assert.LessOrEqual(config.breatherSeconds, 5f);
            var timeline = new BarrageTimeline();
            Assert.IsFalse(timeline.EnemiesHoldFire);
            timeline.Begin(6f, config.breatherSeconds);
            var plan = new ShellingPlan(config, Rand);
            plan.Enter(FlowSection.Scout, true);
            var events = new List<ShellingPlan.Event>();
            float falling = 0f, breather = 0f;
            bool began = false, finished = false;
            for (int i = 0; i < 4000 && !finished; i++)
            {
                var phase = timeline.Now;
                // 떨어지는 동안과 숨 고르기 동안: 적은 쏘지 않고, 대본의 발 말고는 아무것도 새로 떨어지지 않는다
                Assert.IsTrue(timeline.EnemiesHoldFire);
                Assert.IsTrue(timeline.ShellingHeld);
                plan.Tick(0.05f, false, events, timeline.ShellingHeld);
                var step = timeline.Tick(0.05f);
                if (phase == BarrageTimeline.Phase.Falling) falling += 0.05f; else breather += 0.05f;
                if (step == BarrageTimeline.Step.BreatherBegan) began = true;
                if (step == BarrageTimeline.Step.Finished) finished = true;
            }
            Assert.IsTrue(began && finished);
            Assert.AreEqual(0, events.Count, "탄막 · 숨 고르기 동안 다른 포격(먼 포격 포함)이 시작됐다");
            Assert.AreEqual(6f, falling, 0.06f);
            Assert.AreEqual(config.breatherSeconds, breather, 0.06f, "숨 고르기 길이");
            Assert.IsFalse(timeline.EnemiesHoldFire, "끝난 뒤에는 풀린다");
            Assert.IsFalse(timeline.ShellingHeld);
            // 끝난 뒤에는 일정이 다시 돈다
            for (float t = 0f; t < 200f; t += 0.1f) plan.Tick(0.1f, false, events, timeline.ShellingHeld);
            Assert.Greater(events.Count, 0);
            // 도중에 그만두면(새 회차 · 재시도) 바로 풀린다
            timeline.Begin(6f, 4f);
            timeline.Cancel();
            Assert.IsFalse(timeline.EnemiesHoldFire);
        }

        [Test]
        public void SmallBarrage_EveryRoundKeepsTheSafetyDistances()
        {
            var config = Asset();
            var player = new Vector2(0f, -0.5f);
            for (int seed = 0; seed < 60; seed++)
            {
                rng = new System.Random(seed);
                var keep = new List<ShellSafety.Keepout> { new ShellSafety.Keepout(new Vector3(player.x, 0f, player.y), 18f), new ShellSafety.Keepout(new Vector3(11f, 0f, -0.4f), config.keepAlly) };
                if (!ShellSafety.TryPick(player, 18f, 40f, config.fieldArea, keep, Trench, config.keepTrench, Rand, 16, out var first)) continue;
                var rounds = Barrage.Small(config, first, 4, config.fieldArea, keep, Trench, config.keepTrench, Rand);
                Assert.GreaterOrEqual(rounds.Count, 1);
                foreach (var r in rounds)
                {
                    Assert.IsTrue(ShellSafety.IsSafe(r.point, keep, Trench, config.keepTrench), "작은 탄막의 한 발이 안전 거리 안에 떨어졌다");
                    Assert.IsTrue(config.fieldArea.Contains(r.point));
                }
                for (int i = 1; i < rounds.Count; i++) Assert.Greater(rounds[i].time, rounds[i - 1].time);
            }
        }

        [Test]
        public void CloseRound_FeedbackStaysShortAndBearable()
        {
            var config = Asset();
            Assert.LessOrEqual(config.closeShakeDegrees, 3f, "가장 센 흔들림도 3도 이내");
            Assert.LessOrEqual(config.closeShakeSeconds, 0.6f, "0.5초 안팎");
            Assert.Greater(config.closeShakeDegrees, config.shakeDegrees, "진지 바로 앞이 가장 세다");
            Assert.GreaterOrEqual(config.closeDeafenSeconds, 2f);
            Assert.LessOrEqual(config.closeDeafenSeconds, 4f);
            Assert.LessOrEqual(config.dustSeconds, config.breatherSeconds + 0.5f, "먼지는 숨 고르기 안에 걷힌다 (뒤의 조명탄 연출이 안개를 쓴다)");
            Assert.IsNotNull(config.dirtShowerPrefab);
            Assert.GreaterOrEqual(config.bigKeepTrench, 4f);
            Assert.GreaterOrEqual(config.bigKeepPlayer, 6f);
        }

        [Test]
        public void FarReport_ArrivesAtTheSpeedOfSound()
        {
            var config = Asset();
            var far = Run(config, FlowSection.Assault, true, 600f).Where(e => e.kind == ShellingPlan.Kind.DistantFlash).ToArray();
            foreach (var e in far)
            {
                Assert.AreEqual(e.distance / config.soundSpeed, e.soundDelay, 0.001f);
                Assert.GreaterOrEqual(e.distance, config.distantNear);
                Assert.LessOrEqual(e.distance, config.distantFar);
            }
            // 2 km면 약 6초
            Assert.AreEqual(5.9f, 2000f / config.soundSpeed, 0.2f);
        }

        [Test]
        public void EnteringASection_ResetsTheCountAndSchedule()
        {
            var config = Asset();
            var plan = new ShellingPlan(config, Rand);
            plan.Enter(FlowSection.Regroup, true);
            var events = new List<ShellingPlan.Event>();
            for (float t = 0f; t < 600f; t += 0.1f) plan.Tick(0.1f, false, events);
            Assert.IsFalse(plan.NearAllowed);
            plan.Enter(FlowSection.Regroup, true); // 새 회차 · 재시도
            Assert.AreEqual(0, plan.NearCount);
            Assert.IsTrue(plan.NearAllowed);
            plan.Enter(FlowSection.Silence, true);
            Assert.IsFalse(plan.NearAllowed);
        }

        // ---- 안전 거리 ----
        static readonly Rect Trench = new Rect(-19.3f, -3.7f, 40.9f, 4.9f);

        [Test]
        public void Safety_RejectsTrenchAndKeepouts()
        {
            var keep = new List<ShellSafety.Keepout>
            {
                new ShellSafety.Keepout(new Vector3(0f, 0f, 30f), 10f),   // 아군
                new ShellSafety.Keepout(new Vector3(20f, 0f, 52f), 5f),   // 엄폐물
            };
            Assert.IsFalse(ShellSafety.IsSafe(new Vector2(0f, -1f), keep, Trench, 7f), "참호 안");
            Assert.IsFalse(ShellSafety.IsSafe(new Vector2(0f, 6f), keep, Trench, 7f), "참호 바로 앞");
            Assert.IsFalse(ShellSafety.IsSafe(new Vector2(-25f, -1f), keep, Trench, 7f), "참호 끝 바로 옆");
            Assert.IsFalse(ShellSafety.IsSafe(new Vector2(4f, 36f), keep, Trench, 7f), "아군 곁");
            Assert.IsFalse(ShellSafety.IsSafe(new Vector2(22f, 55f), keep, Trench, 7f), "엄폐물 곁");
            Assert.IsTrue(ShellSafety.IsSafe(new Vector2(-30f, 45f), keep, Trench, 7f), "빈 들판");
        }

        [Test]
        public void Pick_NeverLandsInsideAKeepout_OrOutsideTheArea()
        {
            var config = Asset();
            var player = new Vector2(0f, -0.5f);
            var keep = new List<ShellSafety.Keepout> { new ShellSafety.Keepout(new Vector3(player.x, 0f, player.y), 18f) };
            foreach (var cover in new[] { new Vector3(-18, 0, 50), new Vector3(3, 0, 54), new Vector3(20, 0, 52), new Vector3(-2, 0, 40), new Vector3(0, 0, 28) })
                keep.Add(new ShellSafety.Keepout(cover, config.keepCover));
            int found = 0;
            for (int i = 0; i < 500; i++)
            {
                if (!ShellSafety.TryPick(player, 18f, 62f, config.fieldArea, keep, Trench, config.keepTrench, Rand, 16, out var p, Vector2.up, 0.6f)) continue;
                found++;
                Assert.IsTrue(config.fieldArea.Contains(p));
                Assert.GreaterOrEqual((p - player).magnitude, 18f - 0.001f);
                Assert.IsTrue(ShellSafety.IsSafe(p, keep, Trench, config.keepTrench));
            }
            Assert.Greater(found, 400, "들판에서 자리를 거의 못 찾는다");
        }

        [Test]
        public void Pick_GivesUp_WhenNowhereIsSafe()
        {
            var keep = new List<ShellSafety.Keepout> { new ShellSafety.Keepout(Vector3.zero, 500f) };
            Assert.IsFalse(ShellSafety.TryPick(Vector2.zero, 20f, 60f, null, keep, Trench, 7f, Rand, 16, out _));
        }

        [Test]
        public void Config_KeepsItsSafetyDistances()
        {
            var config = Asset();
            Assert.GreaterOrEqual(config.keepAlly, 8f);
            Assert.GreaterOrEqual(config.keepEnemy, 5f);
            Assert.GreaterOrEqual(config.keepCover, 4f);
            Assert.GreaterOrEqual(config.keepCrate, 6f);
            Assert.GreaterOrEqual(config.keepTrench, 5f);
            foreach (var rule in config.rules)
                if (rule.nearMax > 0f) Assert.GreaterOrEqual(rule.playerMin, 15f, rule.section + ": 플레이어에게 너무 가깝다");
            Assert.LessOrEqual(config.shakeDegrees, 2f, "화면 흔들림은 약하게");
            Assert.LessOrEqual(config.shakeSeconds, 0.6f, "화면 흔들림은 짧게");
            Assert.IsNotNull(config.blastPrefab);
            Assert.IsNotNull(config.horizonGlow);
        }
    }
}
