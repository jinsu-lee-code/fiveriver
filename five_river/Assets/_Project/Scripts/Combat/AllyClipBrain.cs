using System.Collections.Generic;
using FiveRivers.Data;

namespace FiveRivers.Combat
{
    // 참호 아군의 Rigify 클립을 언제 무엇으로 틀지 정하는 순수 로직 (2026-11-18). 엔진 의존 없음 → 편집 모드 테스트.
    //  꺼짐 → (참호 구간 · 제 자리 · 멈춰 섬) 서기→낮춤, 또는 막 사격을 멈춰 이미 낮으면 곧바로 낮춘 대기
    //  낮춘 대기(반복) ↔ 변화 동작: AllyIdleLife 와 같은 규칙(6~14초 간격, 한 번에 하나, 같은 것을 연달아 하지 않음,
    //    남이 하는 것은 고르지 않음, 누가 시작한 지 1.6초 안에는 시작하지 않음, 이름 기반 고정 씨앗). 연출 컷 중에는 새로 시작하지 않는다.
    //  가까운 포탄 → 머리 숙이기 → 낮춘 대기
    //  교전 시작 → 낮춤→무릎 사격 → 컨트롤러(Fire_Crouch). 일어서는 중 · 낮추는 중이면 fireFade(0.12초) 안에 빠짐
    //  참호 구간을 벗어남 · 돌아서기 시작함 → 낮춤→서기 → 컨트롤러(Idle_Stand)
    //  전진 시작 · 다른 코드가 몸을 옮김 · 쓰러짐 · 새 회차 → 바로 빠짐
    // 이야기 클립(Story)은 부르는 쪽이 멈출 때까지 다른 규칙보다 앞선다(새 회차 · 쓰러짐만 예외).
    public sealed class AllyClipRoster
    {
        public float LastStartAt = -100f;
        readonly List<AllyClipBrain> members = new List<AllyClipBrain>();

        public void Add(AllyClipBrain brain) { if (!members.Contains(brain)) members.Add(brain); }
        public void Remove(AllyClipBrain brain) => members.Remove(brain);
        public void Clear() { members.Clear(); LastStartAt = -100f; }

        public bool SomeoneElsePlays(string clip, AllyClipBrain except)
        {
            foreach (var m in members)
                if (m != except && m.Phase == AllyClipPhase.Variation && m.Clip == clip) return true;
            return false;
        }
    }

    public enum AllyClipPhase { Off, Entering, Ready, Variation, Duck, ToFire, ToStand, Story }

    public enum AllyClipOrderKind { None, Play, Leave }

    // 이번 프레임에 재생 장치가 할 일
    public struct AllyClipOrder
    {
        public AllyClipOrderKind Kind;
        public string Clip;
        public float Fade;          // Play: 그래프가 이미 돌면 클립끼리 섞는 시간, 아니면 컨트롤러 자세에서 들어가는 시간
        public float StartTime;     // Play: 클립 어디서 시작하나(초)
        public bool FromController; // Play: 그래프가 아직 없다(컨트롤러 자세에서 들어간다)
        public float LeaveSeconds;  // Leave: 컨트롤러 자세로 돌아가는 시간 (0 = 즉시)

        public static AllyClipOrder None => new AllyClipOrder { Kind = AllyClipOrderKind.None };
    }

    // 매 프레임의 상황 (재생 장치가 채운다)
    public struct AllyClipSituation
    {
        public float Time;
        public bool Alive;           // AllyCombatant 가 켜져 있고 애니메이터가 돈다
        public bool InTrenchSection; // 이 구간에서는 제 자리에서 낮춰 기다린다
        public bool Engaged, Advancing, TakingCover;
        public bool Busy;            // 다른 것이 자세를 가져감(고개 숙임 · 쓰러짐 · 컨트롤러가 다른 상태)
        public bool Teleported;      // 한 프레임에 크게 옮겨짐(컷 전환): 즉시 빠짐
        public bool Walking;         // 누가 걷게 함
        public bool Turning;         // 몸이 돌고 있음(돌아서 말하기)
        public bool FacingAway;      // 들판에서 크게 돌아서 있음
        public bool AwayFromPost;
        public float StillFor;       // 멈춰 선 지 몇 초
        public bool StandFire;       // 교전 중 서서 쏜다: 무릎 사격으로 가지 않고 일어선다
        public bool Low;             // 지금 자세가 낮다(무릎 사격에서 막 멈춤)
        public bool Cutscene;        // 연출 컷 중: 변화 동작을 새로 시작하지 않음
        public bool Duck;            // 가까운 포탄이 곧 떨어진다
        public bool Reset;           // 새 회차 · 재시도
    }

    public sealed class AllyClipBrain
    {
        public struct ClipInfo
        {
            public string Name;
            public AllyClipRole Role;
            public float Length;
            public ClipInfo(string name, AllyClipRole role, float length) { Name = name; Role = role; Length = length; }
        }

        readonly AllyClipTiming timing;
        readonly AllyClipRoster roster;
        readonly System.Random random;
        readonly Dictionary<string, ClipInfo> byName = new Dictionary<string, ClipInfo>();
        readonly List<ClipInfo> variations = new List<ClipInfo>();
        string baseClip, enterClip, standClip, fireClip, duckClip;

        public AllyClipPhase Phase { get; private set; } = AllyClipPhase.Off;
        public string Clip { get; private set; }
        public float ClipTime { get; private set; }
        public string LastVariation { get; private set; }
        public float NextVariationAt { get; private set; } = float.MaxValue;
        float enterAt = -1f, reenterAt = -100f, lastDuckAt = -100f;
        string storyRequest; float storyFade; bool storyHold, storyStop; float storyStopSeconds;

        public bool Active => Phase != AllyClipPhase.Off;
        public bool HasBase => baseClip != null;

        public AllyClipBrain(IEnumerable<ClipInfo> clips, AllyClipTiming timing, AllyClipRoster roster, int seed)
        {
            this.timing = timing ?? new AllyClipTiming();
            this.roster = roster;
            random = new System.Random(seed);
            foreach (var c in clips)
            {
                if (string.IsNullOrEmpty(c.Name) || c.Length <= 0f || byName.ContainsKey(c.Name)) continue;
                byName[c.Name] = c;
                switch (c.Role)
                {
                    case AllyClipRole.Base: if (baseClip == null) baseClip = c.Name; break;
                    case AllyClipRole.EnterFromStand: if (enterClip == null) enterClip = c.Name; break;
                    case AllyClipRole.ExitToStand: if (standClip == null) standClip = c.Name; break;
                    case AllyClipRole.ExitToFire: if (fireClip == null) fireClip = c.Name; break;
                    case AllyClipRole.Duck: if (duckClip == null) duckClip = c.Name; break;
                    case AllyClipRole.Variation: variations.Add(c); break;
                }
            }
            roster?.Add(this);
        }

        // 이름 문자열 → 같은 사람은 매 회차 같은 씨앗 (string.GetHashCode 는 실행마다 다르다)
        public static int Seed(string name)
        {
            int hash = 17;
            if (name != null) foreach (char c in name) hash = unchecked(hash * 31 + c);
            return hash;
        }

        float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
        float Length(string clip) => clip != null && byName.TryGetValue(clip, out var info) ? info.Length : 0f;

        // 이야기 장면이 부르는 클립 (예: 시신 곁 무릎 꿇기). hold: 끝 프레임에 머문다(StopStory 까지).
        public bool RequestStory(string clip, float fade, bool hold)
        {
            if (clip == null || !byName.ContainsKey(clip)) return false;
            storyRequest = clip; storyFade = fade; storyHold = hold; storyStop = false;
            return true;
        }

        public void StopStory(float seconds) { storyRequest = null; storyStop = true; storyStopSeconds = seconds; }

        public AllyClipOrder Tick(in AllyClipSituation s, float dt)
        {
            if (Phase != AllyClipPhase.Off) ClipTime += dt;

            if (s.Reset || s.Teleported) { storyRequest = null; storyStop = false; return Active ? Leave(0f) : AllyClipOrder.None; }
            if (!s.Alive) { storyRequest = null; return Active ? Leave(0f) : AllyClipOrder.None; }

            // ---- 이야기 클립 ----
            if (storyRequest != null)
            {
                bool fromController = !Active;
                string clip = storyRequest; storyRequest = null;
                return Play(AllyClipPhase.Story, clip, storyFade, 0f, fromController);
            }
            if (Phase == AllyClipPhase.Story)
            {
                if (storyStop) { storyStop = false; return Leave(storyStopSeconds); }
                if (!storyHold && ClipTime >= Length(Clip) - timing.storyOut) return Leave(timing.storyOut);
                return AllyClipOrder.None;
            }
            storyStop = false;

            if (Phase == AllyClipPhase.Off) return TryEnter(s);

            // ---- 빠지는 조건 (앞선 것부터) ----
            if (s.Advancing) return Leave(timing.advanceOut);
            if (s.Walking || s.AwayFromPost || s.Busy) return Leave(timing.brokenOut);
            if (s.TakingCover) return Leave(timing.coverOut);
            if (s.Engaged && s.StandFire)
            {
                if (Phase == AllyClipPhase.ToStand) return StandTick();
                if (standClip != null && (Phase == AllyClipPhase.Ready || Phase == AllyClipPhase.Variation || Phase == AllyClipPhase.Duck))
                    return Play(AllyClipPhase.ToStand, standClip, timing.standFade, 0f, false);
                return Leave(timing.fireFade);
            }
            if (s.Engaged)
            {
                if (Phase == AllyClipPhase.ToFire) return FireTick();
                if (fireClip != null && (Phase == AllyClipPhase.Ready || Phase == AllyClipPhase.Variation || Phase == AllyClipPhase.Duck))
                    return Play(AllyClipPhase.ToFire, fireClip, timing.fireFade, 0f, false);
                return Leave(timing.fireFade);
            }
            if (Phase == AllyClipPhase.ToFire) return FireTick(); // 교전이 바로 끝나도 무릎까지는 간다
            if (Phase == AllyClipPhase.ToStand) return StandTick();
            if (!s.InTrenchSection || s.Turning || s.FacingAway)
            {
                reenterAt = s.Time + timing.reenterAfter;
                if (standClip != null && Phase != AllyClipPhase.Entering)
                    return Play(AllyClipPhase.ToStand, standClip, timing.standFade, 0f, false);
                return Leave(timing.brokenOut);
            }

            switch (Phase)
            {
                case AllyClipPhase.Entering:
                    if (ClipTime >= Length(Clip) - timing.joinFade) return ToBase(s, timing.joinFade, true);
                    break;
                case AllyClipPhase.Ready:
                    if (s.Duck && duckClip != null && s.Time - lastDuckAt >= timing.duckCooldown)
                    {
                        lastDuckAt = s.Time;
                        return Play(AllyClipPhase.Duck, duckClip, timing.duckFade, 0f, false);
                    }
                    if (!s.Cutscene && s.Time >= NextVariationAt) return PickVariation(s);
                    break;
                case AllyClipPhase.Variation:
                    if (s.Duck && duckClip != null && s.Time - lastDuckAt >= timing.duckCooldown)
                    {
                        lastDuckAt = s.Time;
                        LastVariation = Clip;
                        return Play(AllyClipPhase.Duck, duckClip, timing.duckFade, 0f, false);
                    }
                    if (ClipTime >= Length(Clip) - timing.joinFade)
                    {
                        LastVariation = Clip;
                        return ToBase(s, timing.joinFade, false);
                    }
                    break;
                case AllyClipPhase.Duck:
                    if (ClipTime >= Length(Clip) - timing.joinFade) return ToBase(s, timing.joinFade, false);
                    break;
            }
            return AllyClipOrder.None;
        }

        AllyClipOrder TryEnter(in AllyClipSituation s)
        {
            bool can = baseClip != null && s.InTrenchSection && !s.Engaged && !s.Advancing && !s.TakingCover && !s.Busy
                && !s.Walking && !s.Turning && !s.FacingAway && !s.AwayFromPost && s.Time >= reenterAt;
            if (!can) { enterAt = -1f; return AllyClipOrder.None; }
            // 막 사격을 멈춘 무릎 자세: 일어서지 않고 곧바로 낮춘 대기로 (박자는 사람마다 다르게)
            if (s.Low) return ToBaseFrom(s, true);
            if (s.StillFor < timing.enterStill) { enterAt = -1f; return AllyClipOrder.None; }
            if (enterAt < 0f) enterAt = s.Time + Range(timing.enterStagger.x, timing.enterStagger.y);
            if (s.Time < enterAt) return AllyClipOrder.None;
            enterAt = -1f;
            if (enterClip == null) return ToBaseFrom(s, true);
            return Play(AllyClipPhase.Entering, enterClip, timing.enterBlend, 0f, true);
        }

        AllyClipOrder ToBaseFrom(in AllyClipSituation s, bool fromController)
        {
            enterAt = -1f;
            NextVariationAt = s.Time + Range(timing.firstRest.x, timing.firstRest.y);
            // 사격 자세에서 곧바로 들어갈 때는 숨 박자가 사람마다 엇갈리게 아무 데서나 시작한다
            return Play(AllyClipPhase.Ready, baseClip, timing.enterLowBlend, Range(0f, Length(baseClip)), fromController);
        }

        // 변화 · 숙이기 · 서기→낮춤은 낮춘 대기 첫 프레임 자세로 끝난다: 그 첫 프레임부터 잇는다
        AllyClipOrder ToBase(in AllyClipSituation s, float fade, bool first)
        {
            NextVariationAt = s.Time + (first ? Range(timing.firstRest.x, timing.firstRest.y) : Range(timing.rest.x, timing.rest.y));
            return Play(AllyClipPhase.Ready, baseClip, fade, 0f, false);
        }

        AllyClipOrder PickVariation(in AllyClipSituation s)
        {
            if (s.Time - (roster != null ? roster.LastStartAt : -100f) < timing.stagger)
            {
                NextVariationAt = s.Time + Range(1.2f, 2.8f);
                return AllyClipOrder.None;
            }
            var options = new List<ClipInfo>();
            foreach (var v in variations)
            {
                if (variations.Count > 1 && v.Name == LastVariation) continue;
                if (roster != null && roster.SomeoneElsePlays(v.Name, this)) continue;
                options.Add(v);
            }
            if (options.Count == 0)
            {
                NextVariationAt = s.Time + Range(1.5f, 3.5f);
                return AllyClipOrder.None;
            }
            var chosen = options[random.Next(options.Count)];
            if (roster != null) roster.LastStartAt = s.Time;
            NextVariationAt = float.MaxValue;
            return Play(AllyClipPhase.Variation, chosen.Name, timing.joinFade, 0f, false);
        }

        AllyClipOrder FireTick() => ClipTime >= Length(Clip) - timing.fireOut ? Leave(timing.fireOut) : AllyClipOrder.None;

        AllyClipOrder StandTick() => ClipTime >= Length(Clip) - timing.standOut ? Leave(timing.standOut) : AllyClipOrder.None;

        AllyClipOrder Play(AllyClipPhase phase, string clip, float fade, float start, bool fromController)
        {
            Phase = phase;
            Clip = clip;
            ClipTime = start;
            return new AllyClipOrder { Kind = AllyClipOrderKind.Play, Clip = clip, Fade = fade, StartTime = start, FromController = fromController };
        }

        AllyClipOrder Leave(float seconds)
        {
            if (Phase == AllyClipPhase.Variation) LastVariation = Clip;
            Phase = AllyClipPhase.Off;
            Clip = null;
            ClipTime = 0f;
            enterAt = -1f;
            NextVariationAt = float.MaxValue;
            return new AllyClipOrder { Kind = AllyClipOrderKind.Leave, LeaveSeconds = seconds < 0f ? 0f : seconds };
        }

        public void Forget() => roster?.Remove(this);
    }
}
