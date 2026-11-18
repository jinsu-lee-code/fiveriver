using System.Collections.Generic;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 적의 밀도 (2026-11-18, CLAUDE.md 핵심 설계 규칙 5 변경): 일반판은 구간별 유연 상한, 전시용은 5명 유지.
    // 적이 늘어도 플레이어를 겨누는 수는 제한되고, 아군은 플레이어의 몫을 남긴다.
    public class EnemyDensityTests
    {
        static WaveConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<WaveConfig>("Assets/_Project/Data/WaveConfig.asset");
            Assert.IsNotNull(config);
            return config;
        }

        static int PeakActive(EnemyDensity.Rule rule, int joined = 0)
        {
            var plan = new WavePlan(rule.total, rule.maxActive, rule.interval, joined);
            int peak = 0, spawned = 0;
            for (float t = 0f; t < 600f; t += 0.25f)
            {
                if (plan.TrySpawn(t)) spawned++;
                peak = Mathf.Max(peak, plan.Active);
                Assert.LessOrEqual(plan.Active, rule.maxActive);
                if (t > 60f && Mathf.Approximately(t % 5f, 0f)) plan.OnEnemyGone();
                if (plan.Done) break;
            }
            Assert.AreEqual(rule.total, spawned);
            return peak;
        }

        [Test]
        public void ExhibitFlow_KeepsTheFiveManCap_AndItsOldNumbers()
        {
            var c = Config();
            Assert.AreEqual(5, c.maxActive, "전시용 짧은 흐름은 동시 5명 그대로");
            foreach (var section in new[] { FlowSection.Assault, FlowSection.WestAssault })
            {
                var rule = EnemyDensity.For(section, false, c);
                Assert.AreEqual(c.maxActive, rule.maxActive);
                Assert.AreEqual(c.spawnInterval, rule.interval);
                Assert.AreEqual(EnemyDensity.Unlimited, rule.aimAtPlayer, "전시용은 종전대로 모두 플레이어를 겨눈다");
                Assert.AreEqual(EnemyDensity.Unlimited, rule.allyFloor, "전시용에서 아군 사격은 연출만");
                Assert.LessOrEqual(PeakActive(rule, section == FlowSection.Assault ? c.scouts : 0), 5);
            }
            Assert.AreEqual(c.mainAssault, EnemyDensity.For(FlowSection.Assault, false, c).total);
            Assert.AreEqual(c.westAssault, EnemyDensity.For(FlowSection.WestAssault, false, c).total);
        }

        [Test]
        public void GeneralFlow_EachSectionHasItsOwnCap()
        {
            var c = Config();
            var scout = EnemyDensity.For(FlowSection.Scout, true, c);
            var main = EnemyDensity.For(FlowSection.Assault, true, c);
            var west = EnemyDensity.For(FlowSection.WestAssault, true, c);
            Assert.AreEqual(c.scouts, scout.total);
            Assert.LessOrEqual(scout.total, 3, "정찰은 소수");
            Assert.Greater(main.maxActive, 5, "주 공세는 들판을 메운다");
            Assert.Greater(main.total, main.maxActive);
            Assert.Greater(west.maxActive, 5);
            Assert.GreaterOrEqual(main.maxActive, west.maxActive, "주 공세가 가장 크다");
            Assert.AreEqual(main.maxActive, PeakActive(main));
            Assert.AreEqual(west.maxActive, PeakActive(west));
            Assert.Less(main.interval, c.spawnInterval, "밀려오는 느낌: 종전보다 촘촘히 나온다");
            // 프레임 · 화면이 감당하는 선
            Assert.LessOrEqual(main.maxActive, 12);
            Assert.LessOrEqual(main.total, 24);
        }

        [Test]
        public void GeneralFlow_RiflesOnThePlayer_AreNoMoreThanBefore()
        {
            var c = Config();
            foreach (var section in new[] { FlowSection.Scout, FlowSection.Assault, FlowSection.WestAssault, FlowSection.HillAssault })
            {
                var rule = EnemyDensity.For(section, true, c);
                Assert.LessOrEqual(rule.aimAtPlayer, 5, section + ": 종전의 동시 5명보다 많은 총구가 플레이어를 향한다");
                Assert.GreaterOrEqual(rule.aimAtPlayer, 1);
            }
        }

        [Test]
        public void AimSlots_NeverExceedTheLimit_AndPassOn()
        {
            var slots = new AimSlots<string> { Max = 3 };
            var names = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i" };
            int holding = 0;
            foreach (var n in names) if (slots.TryTake(n)) holding++;
            Assert.AreEqual(3, holding);
            Assert.AreEqual(3, slots.Count);
            Assert.IsTrue(slots.TryTake("a"), "이미 가진 자리는 그대로");
            Assert.IsFalse(slots.TryTake("d"));
            slots.Release("b"); // 쓰러지거나 자리를 옮기면
            Assert.IsTrue(slots.TryTake("d"), "빈 자리는 다음 사람이 잡는다");
            Assert.IsFalse(slots.TryTake("e"));
            Assert.AreEqual(3, slots.Count);
            slots.Clear();
            Assert.AreEqual(0, slots.Count);
        }

        [Test]
        public void AimSlots_Unlimited_ForTheExhibitFlow()
        {
            var slots = new AimSlots<string>();
            for (int i = 0; i < 40; i++) Assert.IsTrue(slots.TryTake("e" + i));
        }

        [Test]
        public void Allies_LeaveThePlayersShare_AndNeverTakeAManFiringAtThePlayer()
        {
            var c = Config();
            var main = EnemyDensity.For(FlowSection.Assault, true, c);
            Assert.GreaterOrEqual(main.allyFloor, 1, "주 공세를 아군이 끝내 주면 안 된다");
            Assert.LessOrEqual(main.allyFloor, c.mainAssault + c.scouts, "플레이어의 몫이 종전(지스타 압축판 4명 + 정찰병)보다 늘면 안 된다");
            Assert.IsTrue(EnemyDensity.AllyMayKill(main, main.allyFloor + 1, false));
            Assert.IsFalse(EnemyDensity.AllyMayKill(main, main.allyFloor, false), "플레이어의 몫");
            Assert.IsFalse(EnemyDensity.AllyMayKill(main, main.maxActive, true), "플레이어를 겨누는 적은 플레이어의 것");
            var west = EnemyDensity.For(FlowSection.WestAssault, true, c);
            Assert.LessOrEqual(west.allyFloor, c.westAssault);
            // 전시용 · 정찰: 아군 사격은 연출만
            Assert.IsFalse(EnemyDensity.AllyMayKill(EnemyDensity.For(FlowSection.Assault, false, c), 99, false));
            Assert.IsFalse(EnemyDensity.AllyMayKill(EnemyDensity.For(FlowSection.Scout, true, c), 99, false));
            Assert.Greater(c.allyKillChance, 0f);
            Assert.LessOrEqual(c.allyKillChance, 0.5f, "아군이 적을 쓸어 버리면 안 된다");
        }

        [Test]
        public void Fallen_OutOfViewAndFarthestGoesFirst_NothingVanishesInFrontOfThePlayer()
        {
            var eye = Vector3.zero;
            var bodies = new List<Vector3> { new Vector3(0, 0, 10), new Vector3(0, 0, 40), new Vector3(0, 0, -30), new Vector3(0, 0, -12), new Vector3(0, 0, 80) };
            // 앞(z > 0)은 보이고 뒤는 안 보인다
            int pick = FallenKeeper.PickToRemove(bodies, i => bodies[i].z > 0f, eye, 55f);
            Assert.AreEqual(2, pick, "화면 밖의 가장 먼 것");
            // 전부 보이면: 충분히 먼 것만
            pick = FallenKeeper.PickToRemove(bodies, i => true, eye, 55f);
            Assert.AreEqual(4, pick);
            // 전부 가깝고 보이면 치우지 않는다
            var near = new List<Vector3> { new Vector3(0, 0, 10), new Vector3(3, 0, 25) };
            Assert.AreEqual(-1, FallenKeeper.PickToRemove(near, i => true, eye, 55f));
            Assert.LessOrEqual(Config().maxBodies, 40);
            Assert.GreaterOrEqual(Config().maxBodies, 12, "싸움의 흔적이 남아야 한다");
        }

        // 2026-11-18 사용자 지시 "총알 두 대 맞으면 죽는 걸로 다시 설정해": 일반판은 두 발째에 쓰러진다(회복 전에 맞으면).
        // 실패 보조(피해 배율)가 켜지면 세 발. 전시용 짧은 흐름은 종전 값(18) 그대로.
        [Test]
        public void GeneralRun_TwoRiflesHitsKill_AssistMakesItThree_ExhibitUnchanged()
        {
            var wave = Config();
            var player = AssetDatabase.LoadAssetAtPath<PlayerConfig>("Assets/_Project/Data/PlayerConfig.asset");
            var assist = AssetDatabase.LoadAssetAtPath<AssistConfig>("Assets/_Project/Data/AssistConfig.asset");
            Assert.IsNotNull(player); Assert.IsNotNull(assist);
            int HitsToKill(float damage) { float h = player.maxHealth; int n = 0; while (h > 0f && n < 99) { h -= damage; n++; } return n; }
            Assert.AreEqual(50f, wave.expandedDamage);
            Assert.AreEqual(2, HitsToKill(wave.expandedDamage), "일반판: 두 발");
            Assert.AreEqual(3, HitsToKill(wave.expandedDamage * assist.enemyDamage), "실패 보조가 켜지면 세 발");
            Assert.AreEqual(18f, wave.damage, "전시용 짧은 흐름은 그대로");
            Assert.Greater(HitsToKill(wave.damage), 2);
        }
    }
}
