using System;
using System.Collections.Generic;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 적의 밀도 (2026-11-18, 사용자 지시 "동시활동은 5명 규칙으로 하지말고 유연하게 만들어 그당시에 상황이
    // 느껴질수있는 선에서 그리고 전쟁중이라고 느껴지는 것을 잊지말고"). 순수 C#.
    //  - 일반판: 구간마다 총수 · 동시 활동 상한이 다르다 (정찰은 소수, 주 공세 · 서쪽 공세는 들판을 메운다).
    //  - 전시용 짧은 흐름(9:00 상한): 종전 그대로 동시 5명.
    //  - 적이 늘어도 플레이어가 받는 사격은 늘지 않는다: 동시에 플레이어를 겨누는 수를 제한하고(AimSlots),
    //    나머지는 참호의 아군과 교전한다. 아군의 사격은 "플레이어의 몫"을 넘는 적만 쓰러뜨린다.
    public static class EnemyDensity
    {
        public struct Rule
        {
            public int total;       // 이 공세에 나오는 적 (합류한 정찰병 제외)
            public int maxActive;   // 동시에 활동하는 적
            public float interval;  // 출현 간격 초
            public int aimAtPlayer; // 동시에 플레이어를 겨누는 적 (int.MaxValue = 제한 없음)
            public int allyFloor;   // 살아 있는 적이 이 수보다 많을 때만 아군이 쓰러뜨린다 (int.MaxValue = 아군은 쓰러뜨리지 않는다)
        }

        public const int Unlimited = int.MaxValue;

        public static Rule For(FlowSection section, bool expanded, WaveConfig c)
        {
            // 전시용 짧은 흐름: 2026-11-18 이전과 같다
            var rule = new Rule { total = 0, maxActive = c.maxActive, interval = c.spawnInterval, aimAtPlayer = Unlimited, allyFloor = Unlimited };
            switch (section)
            {
                case FlowSection.Scout:
                    rule.total = c.scouts;
                    rule.maxActive = Mathf.Max(c.scouts, 1);
                    if (expanded) rule.aimAtPlayer = c.aimAtPlayerMax;
                    break;
                case FlowSection.Assault:
                    rule.total = expanded ? c.expandedMainAssault : c.mainAssault;
                    if (expanded) { rule.maxActive = c.expandedMainMaxActive; rule.interval = c.expandedSpawnInterval; rule.aimAtPlayer = c.aimAtPlayerMax; rule.allyFloor = c.allyFloorMain; }
                    break;
                case FlowSection.WestAssault:
                    rule.total = expanded ? c.expandedWestAssault : c.westAssault;
                    if (expanded) { rule.maxActive = c.expandedWestMaxActive; rule.interval = c.expandedSpawnInterval; rule.aimAtPlayer = c.aimAtPlayerMax; rule.allyFloor = c.allyFloorWest; }
                    break;
                case FlowSection.HillAssault:
                    if (expanded) { rule.aimAtPlayer = c.aimAtPlayerMax; rule.allyFloor = c.allyFloorHill; }
                    break;
            }
            return rule;
        }

        // 아군의 한 발이 이 적을 쓰러뜨려도 되는가: 플레이어의 몫(allyFloor)은 남기고, 플레이어를 겨누고 있는 적은 건드리지 않는다.
        public static bool AllyMayKill(Rule rule, int aliveEnemies, bool targetAimsAtPlayer)
        {
            if (rule.allyFloor == Unlimited) return false;
            return aliveEnemies > rule.allyFloor && !targetAimsAtPlayer;
        }
    }

    // 플레이어를 겨누는 자리. 자리를 가진 적만 플레이어에게 쏘고, 자리가 없으면 아군 쪽으로 쏜다.
    public sealed class AimSlots<T> where T : class
    {
        readonly List<T> holders = new List<T>();
        public int Max { get; set; } = EnemyDensity.Unlimited;
        public int Count => holders.Count;

        public bool Holds(T who) => holders.Contains(who);

        public bool TryTake(T who)
        {
            if (who == null) return false;
            if (holders.Contains(who)) return true;
            if (holders.Count >= Max) return false;
            holders.Add(who);
            return true;
        }

        public void Release(T who) => holders.Remove(who);
        public void Clear() => holders.Clear();
    }

    // 쓰러진 몸이 상한을 넘을 때 어느 것을 치울지. 화면 밖의 가장 먼 것부터, 화면 안은 충분히 멀 때만.
    public static class FallenKeeper
    {
        // positions: 쓰러진 몸의 자리. visible(i): 지금 화면에 보이는가. 치울 것이 없으면 -1 (눈앞에서 사라지게 하지 않는다).
        public static int PickToRemove(IReadOnlyList<Vector3> positions, Func<int, bool> visible, Vector3 eye, float visibleMinDistance)
        {
            int best = -1;
            float bestDistance = -1f;
            for (int i = 0; i < positions.Count; i++)
            {
                if (visible(i)) continue;
                float d = (positions[i] - eye).sqrMagnitude;
                if (d > bestDistance) { bestDistance = d; best = i; }
            }
            if (best >= 0) return best;
            float min = visibleMinDistance * visibleMinDistance;
            for (int i = 0; i < positions.Count; i++)
            {
                float d = (positions[i] - eye).sqrMagnitude;
                if (d >= min && d > bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }
    }
}
