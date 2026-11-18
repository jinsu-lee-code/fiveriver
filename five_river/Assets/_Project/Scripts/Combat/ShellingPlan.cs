using System;
using System.Collections.Generic;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 포격 일정 (2026-11-18). 순수 C#: 구간 규칙(ShellingConfig)만 보고 "언제 · 얼마나 멀리"를 정한다.
    // 어디에 떨어지는지 · 무엇이 보이고 들리는지는 ShellingDirector가 맡는다.
    // 지키는 것: 표에 없는 구간에는 아무 일도 없다 / 가까운 포격이 없는 구간(정적 · 새벽)에서는 가까운 포격이
    // 절대 나오지 않는다 / 막힌 동안(연출 컷 · 대사 · 일시정지 · 전차 포 직후)에는 가까운 포격을 시작하지 않는다 /
    // 구간별 최대 발수 / 플레이어와의 거리 범위 / 전시용 짧은 흐름에서는 (설정이 켜지지 않는 한) 먼 포격만.
    public sealed class ShellingPlan
    {
        public enum Kind { DistantFlash, NearShell, Tracer, Gunfire }

        public struct Event
        {
            public Kind kind;
            public float distance;   // DistantFlash: 섬광까지 거리 m / NearShell: 플레이어에게서 떨어질 거리 m
            public float soundDelay; // DistantFlash: 섬광 뒤 포성이 오기까지 초 (거리 / 소리 속도)
            public float level;      // DistantFlash: 구간의 음량 배율
            public float far01;      // DistantFlash: 0 가까운 능선 너머, 1 골짜기 아래쪽 먼 곳
            public bool front;       // DistantFlash: 적 쪽 반원에서
            public bool rear;        // NearShell: 참호 뒤 구역
            public int rounds;       // NearShell: 이어 떨어지는 발수 (작은 탄막)
            public ShellingConfig.Zone zone;
        }

        readonly ShellingConfig config;
        readonly Func<float> random; // 0..1
        ShellingConfig.SectionRule rule;
        bool hasRule, expanded;
        float nextDistant, nextNear, nextTracer, nextGunfire;
        int salvoLeft;
        bool wasBlocked;

        public FlowSection Section { get; private set; } = FlowSection.Title;
        public int NearCount { get; private set; }
        public bool NearAllowed => hasRule && rule.nearMax > 0f && rule.zone != ShellingConfig.Zone.None
            && (expanded || config.nearInExhibit) && (rule.nearMaxCount <= 0 || NearCount < rule.nearMaxCount);

        public ShellingPlan(ShellingConfig config, Func<float> random)
        {
            this.config = config;
            this.random = random;
        }

        float Range(float min, float max) => min + (max - min) * random();

        // 구간에 들어설 때 (새 회차 · 체크포인트 재시도 포함): 일정을 처음부터 다시 잡는다.
        public void Enter(FlowSection section, bool isExpanded)
        {
            Section = section;
            expanded = isExpanded;
            hasRule = config.TryRule(section, out rule);
            NearCount = 0;
            salvoLeft = 0;
            wasBlocked = false;
            nextDistant = hasRule && rule.distantMax > 0f ? Range(rule.distantMin, rule.distantMax) * 0.6f : float.PositiveInfinity;
            nextNear = NearAllowed ? Mathf.Max(rule.nearFirstDelay, Range(rule.nearMin, rule.nearMax) * 0.7f) : float.PositiveInfinity;
            nextTracer = hasRule && rule.tracerMax > 0f ? Range(rule.tracerMin, rule.tracerMax) * 0.5f : float.PositiveInfinity;
            nextGunfire = hasRule && rule.gunfireMax > 0f ? Range(rule.gunfireMin, rule.gunfireMax) * 0.5f : float.PositiveInfinity;
        }

        // 큰 탄막(대본)은 일반판에서만: 숨 고르기 시간이 든다 (전시용 짧은 흐름은 9:00 상한에 여유가 없다).
        public static bool BigBarrageAllowed(ShellingConfig config, bool isExpanded) => config.bigBarrage && isExpanded;

        // dt: 지난 게임 시간. nearBlocked: 지금은 가까운 포격을 시작하면 안 된다.
        // hold: 큰 탄막과 그 뒤의 숨 고르기 동안 — 먼 포격 · 예광탄 · 총성까지 모두 멈춘다(시계도 세지 않는다).
        public void Tick(float dt, bool nearBlocked, List<Event> events, bool hold = false)
        {
            if (!hasRule || dt <= 0f || hold) return;

            nextDistant -= dt;
            while (nextDistant <= 0f)
            {
                float far = random();
                float distance = Mathf.Lerp(config.distantNear, config.distantFar, far);
                events.Add(new Event
                {
                    kind = Kind.DistantFlash, distance = distance, far01 = far, level = rule.distantLevel,
                    soundDelay = distance / Mathf.Max(1f, config.soundSpeed), front = random() < config.frontShare,
                });
                if (salvoLeft > 0)
                {
                    salvoLeft--;
                    nextDistant += Range(config.salvoGapMin, config.salvoGapMax);
                }
                else
                {
                    nextDistant += Range(rule.distantMin, rule.distantMax);
                    salvoLeft = random() < config.salvoChance ? 1 + (random() < 0.5f ? 1 : 0) : 0;
                }
            }

            nextTracer -= dt;
            if (nextTracer <= 0f)
            {
                events.Add(new Event { kind = Kind.Tracer });
                nextTracer = Range(rule.tracerMin, rule.tracerMax);
            }
            nextGunfire -= dt;
            if (nextGunfire <= 0f)
            {
                events.Add(new Event { kind = Kind.Gunfire });
                nextGunfire = Range(rule.gunfireMin, rule.gunfireMax);
            }

            if (!NearAllowed) { nextNear = float.PositiveInfinity; return; }
            if (nearBlocked)
            {
                // 막힌 동안에는 시계를 세지 않는다. 풀린 뒤에도 조금 더 기다린다.
                wasBlocked = true;
                return;
            }
            if (wasBlocked)
            {
                wasBlocked = false;
                nextNear = Mathf.Max(nextNear, config.holdAfterBlock);
            }
            nextNear -= dt;
            if (nextNear > 0f) return;
            NearCount++;
            bool rear = rule.zone == ShellingConfig.Zone.Rear
                || rule.zone == ShellingConfig.Zone.FieldAndRear && random() < config.rearShare;
            events.Add(new Event
            {
                kind = Kind.NearShell, zone = rule.zone, rear = rear,
                rounds = Mathf.Max(1, config.smallRoundsMin + (int)(random() * (config.smallRoundsMax - config.smallRoundsMin + 0.999f))),
                distance = Range(rule.playerMin, Mathf.Max(rule.playerMin, rule.playerMax)),
            });
            nextNear = NearAllowed ? Range(rule.nearMin, rule.nearMax) : float.PositiveInfinity;
        }
    }

    // 탄막 (2026-11-18 추가 지시). 순수 C#: 어디에 · 언제 떨어지는지만 정한다.
    public static class Barrage
    {
        public struct Round
        {
            public Vector2 point; // 월드 x, z
            public float time;    // 첫 발부터 초
            public bool close;    // 진지 바로 앞 낙탄
        }

        // 큰 탄막: 들판 먼 곳에서 시작해 참호 쪽으로 걸어 들어오고, 마지막 몇 발은 흉벽 바로 앞에 떨어진다.
        // trench: 참호가 차지하는 사각형(월드 x, z). 들판은 +z 쪽이고 흉벽은 trench.yMax다.
        // keepouts: 탄막용 안전 거리로 만든 금지 원(플레이어 · 아군 · 탄약 상자 · 엄폐물). 자리를 못 찾은 발은 뺀다.
        public static List<Round> Walk(ShellingConfig c, Vector2 player, Rect trench, IReadOnlyList<ShellSafety.Keepout> keepouts, Func<float> random)
        {
            var rounds = new List<Round>();
            int count = Mathf.Max(2, c.bigRoundsMin + (int)(random() * (c.bigRoundsMax - c.bigRoundsMin + 0.999f)));
            int close = Mathf.Clamp(c.bigCloseRounds, 1, count - 1), far = count - close;
            // 불규칙한 간격: 고르게 나눈 것을 0.45~1.55배로 흔든 뒤 전체 길이에 맞춘다
            var gaps = new float[count];
            float sum = 0f;
            for (int i = 1; i < count; i++) { gaps[i] = 0.45f + 1.1f * random(); sum += gaps[i]; }
            // 진지 바로 앞의 발은 먼 쪽부터: 마지막 발이 가장 가깝다
            var closeAt = new float[close];
            for (int i = 0; i < close; i++) closeAt[i] = c.bigCloseMin + (c.bigCloseMax - c.bigCloseMin) * random();
            Array.Sort(closeAt);
            Array.Reverse(closeAt);
            float time = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) time += gaps[i] / sum * c.bigSeconds;
                bool isClose = i >= far;
                float ahead = isClose ? closeAt[i - far]
                    : Mathf.Lerp(c.bigStartDistance, c.bigCloseMax + 9f, far > 1 ? i / (float)(far - 1) : 0f) + (random() - 0.5f) * 6f;
                float lateral = isClose ? c.bigLateral * 0.5f : c.bigLateral;
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    var p = new Vector2(player.x + (random() * 2f - 1f) * lateral, trench.yMax + ahead);
                    if (!ShellSafety.IsSafe(p, keepouts, trench, c.bigKeepTrench)) continue;
                    rounds.Add(new Round { point = p, time = time, close = isClose });
                    break;
                }
            }
            return rounds;
        }

        // 작은 탄막: 첫 발 둘레에 몇 발이 이어 떨어진다. first는 이미 안전한 자리로 고른 것이다.
        public static List<Round> Small(ShellingConfig c, Vector2 first, int count, Rect? area, IReadOnlyList<ShellSafety.Keepout> keepouts,
            Rect trench, float trenchMargin, Func<float> random)
        {
            var rounds = new List<Round> { new Round { point = first, time = 0f } };
            float time = 0f;
            for (int i = 1; i < count; i++)
            {
                time += c.smallGapMin + (c.smallGapMax - c.smallGapMin) * random();
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    float angle = random() * Mathf.PI * 2f, distance = c.smallSpreadMin + (c.smallSpreadMax - c.smallSpreadMin) * random();
                    var p = first + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    if (area.HasValue && !area.Value.Contains(p)) continue;
                    if (!ShellSafety.IsSafe(p, keepouts, trench, trenchMargin)) continue;
                    bool crowded = false;
                    foreach (var r in rounds) if ((r.point - p).sqrMagnitude < 16f) { crowded = true; break; }
                    if (crowded) continue;
                    rounds.Add(new Round { point = p, time = time });
                    break;
                }
            }
            return rounds;
        }
    }

    // 큰 탄막의 흐름: 떨어지는 동안 → 숨 고르기 → 끝. 떨어지는 동안과 숨 고르기 동안에는 적이 쏘지 않고,
    // 숨 고르기 동안에는 새 포격(먼 포격 포함)도 시작하지 않는다. "살아남았다"의 몇 초다.
    public sealed class BarrageTimeline
    {
        public enum Phase { Idle, Falling, Breather }

        float fallingLeft, breatherLeft;
        public Phase Now { get; private set; } = Phase.Idle;
        public bool Active => Now != Phase.Idle;
        public bool EnemiesHoldFire => Now != Phase.Idle;
        public bool ShellingHeld => Now != Phase.Idle; // 대본의 발 말고는 아무것도 새로 떨어지지 않는다
        public float BreatherProgress { get; private set; } // 0..1

        // falling: 마지막 발이 터질 때까지 초. breather: 그 뒤의 숨 고르기 초.
        public void Begin(float falling, float breather)
        {
            fallingLeft = Mathf.Max(0f, falling);
            breatherLeft = breatherFor = Mathf.Max(0f, breather);
            BreatherProgress = 0f;
            Now = Phase.Falling;
        }

        float breatherFor;

        public void Cancel()
        {
            Now = Phase.Idle;
            fallingLeft = breatherLeft = 0f;
        }

        // 돌려주는 값: 이번 틱에 일어난 일
        public enum Step { None, BreatherBegan, Finished }

        public Step Tick(float dt)
        {
            if (Now == Phase.Idle || dt <= 0f) return Step.None;
            if (Now == Phase.Falling)
            {
                fallingLeft -= dt;
                if (fallingLeft > 0f) return Step.None;
                Now = Phase.Breather;
                return Step.BreatherBegan;
            }
            breatherLeft -= dt;
            BreatherProgress = breatherFor > 0f ? Mathf.Clamp01(1f - breatherLeft / breatherFor) : 1f;
            if (breatherLeft > 0f) return Step.None;
            Now = Phase.Idle;
            return Step.Finished;
        }
    }

    // 가까운 포격이 떨어져도 되는 자리인가. 사람 · 엄폐물 · 탄약 상자 · 참호 안에는 떨어지지 않는다.
    public static class ShellSafety
    {
        public struct Keepout
        {
            public Vector2 centre; // 월드 x, z
            public float radius;
            public Keepout(Vector3 world, float radius) { centre = new Vector2(world.x, world.z); this.radius = radius; }
        }

        // trench: 참호가 차지하는 사각형 (월드 x, z). margin만큼 넓혀서 본다.
        public static bool IsSafe(Vector2 point, IReadOnlyList<Keepout> keepouts, Rect trench, float trenchMargin)
        {
            if (point.x > trench.xMin - trenchMargin && point.x < trench.xMax + trenchMargin
                && point.y > trench.yMin - trenchMargin && point.y < trench.yMax + trenchMargin) return false;
            for (int i = 0; i < keepouts.Count; i++)
                if ((keepouts[i].centre - point).sqrMagnitude < keepouts[i].radius * keepouts[i].radius) return false;
            return true;
        }

        // 거리 ring(min~max) 안이면서 area 안인 점을 tries번까지 찾아본다. 못 찾으면 false (그 발은 쏘지 않는다).
        public static bool TryPick(Vector2 player, float ringMin, float ringMax, Rect? area, IReadOnlyList<Keepout> keepouts,
            Rect trench, float trenchMargin, Func<float> random, int tries, out Vector2 point,
            Vector2 preferDirection = default, float preferShare = 0f)
        {
            for (int i = 0; i < tries; i++)
            {
                float angle;
                if (preferDirection.sqrMagnitude > 0.01f && random() < preferShare)
                    angle = Mathf.Atan2(preferDirection.y, preferDirection.x) + (random() - 0.5f) * Mathf.Deg2Rad * 80f;
                else angle = random() * Mathf.PI * 2f;
                float distance = ringMin + (ringMax - ringMin) * random();
                point = player + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                if (area.HasValue && !area.Value.Contains(point)) continue;
                if (IsSafe(point, keepouts, trench, trenchMargin)) return true;
            }
            point = default;
            return false;
        }
    }
}
