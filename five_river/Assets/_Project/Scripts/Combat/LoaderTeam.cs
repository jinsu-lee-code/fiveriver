using System;

namespace FiveRivers.Combat
{
    // 로켓포반 탄약수의 진행 규칙 (2026-11-24). 순수 C#: 엔진 의존 없음 → 편집 모드 테스트 (LoaderTeamTests).
    // 장면 쪽 어댑터(LoaderTeamDirector)가 매 프레임 상황(LoaderInput)을 넣고, 이벤트와 Action 을 보고 클립 · 이동 · 바주카를 움직인다.
    //
    //  Idle      전차전 밖이거나 연출 중 (탄약수는 제 자리, 평소 아군)
    //  Moving    사수 자리로 가는 중 → 도착하면 무릎 꿇기(KneelIn) 동안도 여기
    //  Ready     자리에 있고 발사관이 비었다
    //  Loading   장전 중 (발사 → 로켓 꺼내 맞춤 → 밀어 넣음 → 걸쇠 → 어깨 두 번 → 옆으로 앉음)
    //  Loaded    발사관이 차 있다 (쏘기를 기다림)
    //  Clearing  전차가 멈췄다: 로켓을 내려놓고 소총을 다시 들고 일어선다 (ToRifle)
    //  Substitute 탄약수가 없거나(대체 모드) 이번 장전은 사수 혼자 한다
    public enum LoaderState { Idle = 0, Moving = 1, Ready = 2, Loading = 3, Loaded = 4, Clearing = 5, Substitute = 6 }

    // 탄약수가 지금 하라는 동작 (재생 장치가 같은 이름의 클립으로 바꾼다). None = 컨트롤러에 맡김
    public enum LoaderAction { None = 0, KneelIn = 1, KneelIdle = 2, Reach = 3, Load = 4, Brace = 5, Wave = 6, ToRifle = 7 }

    public sealed class LoaderTeamSettings
    {
        public float ReloadTime = 2.2f;            // WeaponConfig.bazookaReloadTime
        public float LoadClipLength = 2.2f;
        public float ReadyClipTime = 1.92f;        // 클립의 이 시점에 발사관 장전 완료 (두 번째 두드림)
        public float InsertClipTime = 0.80f;
        public float[] TapClipTimes = { 1.78f, 1.92f };
        public float KneelInLength = 2.633f, ReachLength = 0.933f, BraceLength = 1.233f, WaveLength = 2.433f, ToRifleLength = 2.433f;
        public float MinClipSpeed = 0.6f, MaxClipSpeed = 1.6f;
        public float ArriveGap = 0.35f, FollowGap = 1.2f, LoadReach = 1.5f, AbortGap = 2.0f, FollowMax = 8f, WaveBeyond = 3.5f;
        public float WaveCooldown = 8f, GiveUpSeconds = 6f;
    }

    // 매 프레임의 상황 (어댑터가 채운다)
    public struct LoaderInput
    {
        public bool Active;        // 전차전(또는 아군 지원) 중이고 등장 연출이 끝났다
        public bool Present;       // 탄약수가 있고 클립 재생이 된다 (없으면 대체 모드)
        public float SlotGap;      // 탄약수와 그가 서야 할 자리의 거리 (m)
        public float GunnerGap;    // 사수와 탄약수가 기대하는 사수 자리의 거리 (m)
        public bool CanFollow;     // 사수가 둔덕에 한 번 들어와 자리가 사수를 따라간다
        public bool GunnerOk;      // 사수가 서 있고(Z로 낮추지 않음) 시선이 전차 쪽이다
        public bool ReloadWanted;  // 발사관이 비었고 장전이 요청됐다 (발사 직후 자동 · R · 방아쇠)
        public bool TubeLoaded;
        public int Reserve;        // 발사관 밖 예비 로켓
    }

    public sealed class LoaderTeam
    {
        readonly LoaderTeamSettings s;
        float time, settleLeft, clearLeft, nextWaveAt, waited, loadElapsed;
        int tapsDone;
        bool insertDone, postLoad, settling, settled;

        public LoaderTeam(LoaderTeamSettings settings) { s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public LoaderState State { get; private set; } = LoaderState.Idle;
        public LoaderAction Action { get; private set; } = LoaderAction.None;
        public float ActionTime { get; private set; }
        public bool Deployed { get; private set; }       // 이번 시도에서 제 자리를 떠났다
        public bool Unavailable { get; private set; }    // 탄약수가 없다: 대체 모드 고정
        public bool Paused { get; private set; }         // 장전이 멈춰 기다리는 중 (사수가 숙임 · 시선 · 자리)
        public bool Walking => State == LoaderState.Moving && !settling;
        public bool Settled => settled;
        public float LoadElapsed => loadElapsed;
        public float LoadProgress => s.ReloadTime <= 0f ? 1f : Math.Min(1f, loadElapsed / s.ReloadTime);
        public float ClearSecondsLeft => State == LoaderState.Clearing ? Math.Max(0f, clearLeft) : 0f;
        // 클립 배속: 클립의 ReadyClipTime 이 ReloadTime 에 닿도록
        public float ClipSpeed => s.ReloadTime <= 0f ? 1f : Clamp(s.ReadyClipTime / s.ReloadTime, s.MinClipSpeed, s.MaxClipSpeed);
        public float ActionSpeed => Action == LoaderAction.Load ? ClipSpeed : 1f;

        public event Action<LoaderAction> ActionChanged;
        public event Action Arrived;            // 자리에 닿아 걷기를 멈춘다
        public event Action LoadStarted;
        public event Action Inserted;           // 로켓이 발사관에 들어가기 시작 (소리)
        public event Action<int> Tapped;        // 사수 어깨 두드림 (0, 1): 아주 약한 흔들림 · 소리
        public event Action LoadCompleted;      // 발사관 장전 완료 → Bazooka.CompleteReload
        public event Action LoadAborted;        // 끝내지 못하고 접었다 (사수가 크게 자리를 뜸 · 요청 취소 · 연출)
        public event Action<bool> PauseChanged; // 장전이 멈췄다 / 다시 간다 (클립 배속 0 ↔ ClipSpeed)
        public event Action Called;             // 손짓 시작 (자막)
        public event Action SubstituteBegan;    // 이번 장전은 사수 혼자 (Bazooka.TakeOverReload)
        public event Action Cleared;            // 소총을 다시 들고 일어섰다

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        // ---- 바깥에서 부르는 것 ----

        // 재시도 · 새 회차 · 구간 전환: 모든 상태를 처음으로 (탄약수 위치는 어댑터가 되돌린다)
        public void Reset()
        {
            if (State == LoaderState.Loading) LoadAborted?.Invoke();
            ResetPause();
            State = LoaderState.Idle;
            settling = settled = postLoad = insertDone = false;
            Deployed = Unavailable = false;
            settleLeft = clearLeft = waited = loadElapsed = 0f;
            tapsDone = 0;
            nextWaveAt = 0f;
            SetAction(LoaderAction.None);
        }

        // 체크포인트 재시도: 화면이 어두운 사이 탄약수를 자리에 세워 두고 곧바로 무릎 대기
        public void ForceReady()
        {
            Reset();
            Deployed = settled = true;
            State = LoaderState.Ready;
            SetAction(LoaderAction.KneelIdle);
        }

        // 장전만 접는다 (탄약 초기화 · 무기 바꿈): 탄약수는 로켓을 든 채 대기
        public void AbortLoad()
        {
            if (State == LoaderState.Loading) { LoadAborted?.Invoke(); State = LoaderState.Ready; SetAction(LoaderAction.KneelIdle); }
            postLoad = false;
            ResetPause();
        }

        // 전차가 멈췄다 → 고지 돌격: 소총을 다시 들고 일어선다
        public void BeginClearing()
        {
            if (State == LoaderState.Clearing) return;
            if (State == LoaderState.Loading) LoadAborted?.Invoke();
            ResetPause();
            postLoad = false;
            if (!Deployed || Unavailable) { State = LoaderState.Idle; SetAction(LoaderAction.None); return; }
            if (!settled) { Finish(); return; }
            State = LoaderState.Clearing;
            clearLeft = s.ToRifleLength;
            SetAction(LoaderAction.ToRifle);
        }

        void Finish()
        {
            State = LoaderState.Idle;
            settled = settling = false;
            Deployed = false;
            SetAction(LoaderAction.None);
            Cleared?.Invoke();
        }

        // ---- 매 프레임 ----
        public void Tick(float dt, in LoaderInput input)
        {
            if (dt < 0f) dt = 0f;
            time += dt;
            ActionTime += dt;

            if (State == LoaderState.Clearing)
            {
                clearLeft -= dt;
                if (clearLeft <= 0f) Finish();
                return;
            }
            if (!input.Active)
            {
                if (State != LoaderState.Idle) GoIdle();
                return;
            }
            if (!input.Present)
            {
                if (!Unavailable) EnterUnavailable();
                return;
            }
            if (Unavailable) { Unavailable = false; State = LoaderState.Idle; }

            switch (State)
            {
                case LoaderState.Idle:
                    Deployed = true;
                    waited = 0f;
                    if (input.SlotGap <= s.ArriveGap) Arrive();
                    else { State = LoaderState.Moving; SetAction(LoaderAction.None); }
                    break;

                case LoaderState.Moving:
                    if (settling)
                    {
                        settleLeft -= dt;
                        if (settleLeft <= 0f)
                        {
                            settling = false;
                            settled = true;
                            State = input.TubeLoaded ? LoaderState.Loaded : LoaderState.Ready;
                            SetAction(LoaderAction.KneelIdle);
                        }
                    }
                    else if (input.SlotGap <= s.ArriveGap) Arrive();
                    GiveUpCheck(dt, input);
                    break;

                case LoaderState.Ready:
                case LoaderState.Loaded:
                    TickSettled(dt, input);
                    break;

                case LoaderState.Loading:
                    TickLoading(dt, input);
                    break;

                case LoaderState.Substitute:
                    // 사수 혼자 하던 장전이 끝났거나 취소됐다: 탄약수는 자리에 있던 대로 돌아간다
                    if (!input.ReloadWanted)
                    {
                        waited = 0f;
                        State = settled ? (input.TubeLoaded ? LoaderState.Loaded : LoaderState.Ready) : LoaderState.Moving;
                    }
                    break;
            }
        }

        void Arrive()
        {
            settling = true;
            settleLeft = s.KneelInLength;
            State = LoaderState.Moving;
            SetAction(LoaderAction.KneelIn);
            Arrived?.Invoke();
        }

        void LeaveSlot()
        {
            settled = settling = false;
            State = LoaderState.Moving;
            SetAction(LoaderAction.None);
        }

        void GoIdle()
        {
            if (State == LoaderState.Loading) LoadAborted?.Invoke();
            ResetPause();
            postLoad = false;
            settled = settling = false;
            State = LoaderState.Idle;
            SetAction(LoaderAction.None);
        }

        void EnterUnavailable()
        {
            if (State == LoaderState.Loading) LoadAborted?.Invoke();
            ResetPause();
            postLoad = false;
            Unavailable = true;
            settled = settling = false;
            State = LoaderState.Substitute;
            SetAction(LoaderAction.None);
            SubstituteBegan?.Invoke();
        }

        // 장전 요청 뒤 탄약수가 준비되지 못한 채 오래 기다리면 이번 장전은 사수 혼자 한다
        // (사수가 숙이거나 딴 데를 보는 시간은 세지 않는다: 그동안은 사수 쪽이 못 쏘는 것이다)
        void GiveUpCheck(float dt, in LoaderInput input)
        {
            if (input.ReloadWanted && !input.TubeLoaded && input.Reserve > 0 && input.GunnerOk) waited += dt;
            else if (!input.ReloadWanted) waited = 0f;
            if (waited < s.GiveUpSeconds) return;
            waited = 0f;
            State = LoaderState.Substitute;
            SubstituteBegan?.Invoke();
        }

        void TickSettled(float dt, in LoaderInput input)
        {
            bool following = input.CanFollow && input.SlotGap <= s.FollowMax;
            if (following && input.SlotGap > s.FollowGap) { LeaveSlot(); return; }
            // 멀면 손짓 ("이리 와"): 따라올 수 없는 거리이거나 아직 자리를 사수에게 맞추지 않았을 때
            if (!following && input.GunnerGap > s.WaveBeyond && time >= nextWaveAt && Action != LoaderAction.Wave
                && (Action == LoaderAction.KneelIdle || Action == LoaderAction.KneelIn))
            {
                nextWaveAt = time + s.WaveCooldown;
                SetAction(LoaderAction.Wave);
                Called?.Invoke();
            }

            if (State == LoaderState.Loaded && !input.TubeLoaded)
            {
                // 쏘았다: 이어 장전할 것이면 장전 클립이 웅크림부터 한다. 이을 장전이 없으면 웅크림만.
                State = LoaderState.Ready;
                if (!(input.ReloadWanted && input.Reserve > 0)) SetAction(LoaderAction.Brace);
            }
            else if (State == LoaderState.Ready && input.TubeLoaded) State = LoaderState.Loaded;

            if (State == LoaderState.Ready && input.ReloadWanted && !input.TubeLoaded && input.Reserve > 0
                && input.GunnerOk && input.GunnerGap <= s.LoadReach)
            {
                StartLoad();
                return;
            }
            if (State == LoaderState.Ready) GiveUpCheck(dt, input); else waited = 0f;

            if (postLoad) RunLoadCues(dt);
            AdvanceAction(input);
        }

        void StartLoad()
        {
            State = LoaderState.Loading;
            loadElapsed = 0f;
            tapsDone = 0;
            insertDone = false;
            postLoad = false;
            waited = 0f;
            ResetPause();
            SetAction(LoaderAction.Load);
            LoadStarted?.Invoke();
        }

        void TickLoading(float dt, in LoaderInput input)
        {
            if (!input.ReloadWanted) // 요청이 취소됐다 (무기 바꿈 · 메뉴)
            {
                LoadAborted?.Invoke();
                ResetPause();
                State = LoaderState.Ready;
                SetAction(LoaderAction.KneelIdle);
                return;
            }
            if (input.GunnerGap > s.AbortGap) // 사수가 크게 자리를 떴다: 접고 따라간다 (또는 기다린다)
            {
                LoadAborted?.Invoke();
                ResetPause();
                if (input.CanFollow && input.SlotGap <= s.FollowMax) LeaveSlot();
                else { State = LoaderState.Ready; SetAction(LoaderAction.KneelIdle); }
                return;
            }
            bool wait = !input.GunnerOk || input.GunnerGap > s.LoadReach;
            if (wait != Paused) { Paused = wait; PauseChanged?.Invoke(wait); }
            if (wait) return;
            RunLoadCues(dt);
            if (loadElapsed >= s.ReloadTime)
            {
                State = LoaderState.Loaded;
                postLoad = loadElapsed < RealOf(s.LoadClipLength);
                LoadCompleted?.Invoke();
            }
        }

        float RealOf(float clipSeconds) => clipSeconds / ClipSpeed;

        void RunLoadCues(float dt)
        {
            loadElapsed += dt;
            if (!insertDone && loadElapsed >= RealOf(s.InsertClipTime)) { insertDone = true; Inserted?.Invoke(); }
            var taps = s.TapClipTimes;
            while (taps != null && tapsDone < taps.Length && loadElapsed >= RealOf(taps[tapsDone])) Tapped?.Invoke(tapsDone++);
            if (postLoad && loadElapsed >= RealOf(s.LoadClipLength)) postLoad = false;
        }

        void ResetPause()
        {
            if (!Paused) return;
            Paused = false;
            PauseChanged?.Invoke(false);
        }

        // 한 번에 끝나는 동작 뒤의 다음 동작 (클립은 마지막 자세에 머문다)
        void AdvanceAction(in LoaderInput input)
        {
            switch (Action)
            {
                case LoaderAction.Load:
                    if (!postLoad && State != LoaderState.Loading && input.Reserve > 0) SetAction(LoaderAction.Reach);
                    break;
                case LoaderAction.Reach:
                    if (ActionTime >= s.ReachLength) SetAction(LoaderAction.KneelIdle);
                    break;
                case LoaderAction.Brace:
                    if (ActionTime >= s.BraceLength && input.Reserve > 0) SetAction(LoaderAction.Reach);
                    break;
                case LoaderAction.Wave:
                    if (ActionTime >= s.WaveLength) SetAction(LoaderAction.KneelIdle);
                    break;
            }
        }

        void SetAction(LoaderAction action)
        {
            if (Action == action && action != LoaderAction.Load && action != LoaderAction.Brace && action != LoaderAction.Wave) { return; }
            Action = action;
            ActionTime = 0f;
            ActionChanged?.Invoke(action);
        }
    }
}
