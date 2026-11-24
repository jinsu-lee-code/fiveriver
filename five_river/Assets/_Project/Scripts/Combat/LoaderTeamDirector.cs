using System.Collections.Generic;
using FiveRivers.Audio;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.Narrative;
using FiveRivers.UI;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Combat
{
    // 고지 돌격으로 넘어갈 때 HillAllies 가 탄약수를 합류시키는 데 쓰는 신호 (2026-11-24).
    public static class LoaderHandoff
    {
        internal static LoaderTeamDirector Current;

        // 소총을 다시 드는 동작(ToRifle)이 끝날 때까지 남은 시간. 탄약수가 아니면 0.
        public static float DelayFor(AllyCombatant ally) => Current != null ? Current.HandoffDelay(ally) : 0f;

        // 돌아갈 원래 참호 자리 (사수 자리에서 출발했어도)
        public static bool TryGetPost(AllyCombatant ally, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            return Current != null && Current.TryGetPost(ally, out position, out rotation);
        }
    }

    // 로켓포반 어댑터 (2026-11-24): 사수 = 플레이어(바주카), 탄약수 = 아군 하나(씬 Ally_3).
    // 진행 규칙은 순수 로직 LoaderTeam 이 정하고, 여기서는 그 결과대로 아군을 움직이고(걷기), 동작 클립(AllyClipPlayer.PlayStory)을 틀고,
    // 로켓 소품 궤적을 그리고, Bazooka 의 장전을 맡긴다. 아군 사격 · 대기 동작은 전차 구간에 원래 쉬고 있고(AllyCombatant 의
    // CombatActive 가 아님) 이야기 클립이 도는 동안 AllyClipPlayer · AllyIdleLife 가 쉰다.
    // 탄약수가 없거나(대체 모드) 클립 · 데이터가 모자라거나 전시용 흐름이면 Bazooka.LoaderControlled 를 켜지 않아 예전 그대로 혼자 장전한다.
    // 표시 전용: 전차 판정 · 진행 · 수치는 건드리지 않는다. 장전 시간은 WeaponConfig.bazookaReloadTime 그대로.
    [DefaultExecutionOrder(10)]
    public sealed class LoaderTeamDirector : MonoBehaviour
    {
        const string ClipKneelIn = "A_ROK_Loader_Kneel_In", ClipKneelIdle = "A_ROK_Loader_Kneel_Idle", ClipLoad = "A_ROK_Loader_Load",
            ClipReach = "A_ROK_Loader_Reach", ClipBrace = "A_ROK_Loader_Brace", ClipWave = "A_ROK_Loader_Wave", ClipToRifle = "A_ROK_Loader_ToRifle_Stand";
        static readonly string[] AllClips = { ClipKneelIn, ClipKneelIdle, ClipLoad, ClipReach, ClipBrace, ClipWave, ClipToRifle };
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int IdleState = Animator.StringToHash("Idle_Stand");
        static readonly int AdvanceState = Animator.StringToHash("CrouchRun_Rifle");
        static readonly int TiredWalkState = Animator.StringToHash("Walk_Tired");

        [SerializeField] RocketTeamConfig config;
        [SerializeField] FlowDirector director;
        [SerializeField] LevelPoints points;
        [SerializeField] PlayerController player;
        [SerializeField] WeaponHolder weapons;
        [SerializeField] SubtitleDisplay subtitles;
        [SerializeField] GameText text;
        [SerializeField, Tooltip("SM_WP_BazookaRocket.fbx (탄약수 손의 로켓 소품). 비우면 소품 없이 동작만")] GameObject rocketModel;
        [SerializeField, Tooltip("비우면 config.loaderName 으로 찾는다")] AllyCombatant loader;
        [SerializeField, Tooltip("발사 직후 빈 발사관을 탄약수가 스스로 장전한다 (끄면 R · 방아쇠로만)")] bool autoLoad = true;

        LoaderTeam team;
        Bazooka bazooka;
        AudioDirector audioDirector;
        TankEntranceDirector entrance;
        AllyRifleTracks rifleTracks;
        LoaderPropTracks propTracks;
        AllyRifleTracks.Fit fit;
        AllyClipPlayer clips;
        Animator animator;
        Transform body, model;
        StrideGrounding stride;
        Vector3 postPosition;
        Quaternion postRotation;
        bool resolved, dataOk, warned, controlled, joined, storyOn, readySaid, waitEntrance, seenEntrance, clipsBroken;
        float waitEntranceAt;
        int snapFrame = -10;
        LoaderAction deferred = LoaderAction.None;
        bool hasDeferred;

        // ---- 걷기 ----
        readonly List<Vector3> path = new List<Vector3>();
        NavMeshPath navPathInstance;
        int corner;
        float nextRepath, walkSpeed;
        Vector3 lastTarget;
        bool walkingAnim;
        float rigScale = 1f;
        bool hasAdvance;

        // ---- 로켓 소품 ----
        Transform propRoot;
        Transform[] propHolders;   // 0 = 움직이는 로켓, 1~3 = 세워 둔 예비
        bool propsBuilt;

        public LoaderTeam Team => team;
        public bool Controlled => controlled;

        // ------------------------------------------------------------------------------------------------------------
        void Start()
        {
            if (config == null || director == null || points == null || player == null || weapons == null)
            {
                Debug.LogWarning("[LoaderTeam] 연결이 모자라 꺼 둔다 (FiveRivers/Setup/Rocket Team 을 실행): 바주카는 예전처럼 혼자 장전한다.", this);
                enabled = false;
                return;
            }
            bazooka = weapons.Bazooka;
            team = new LoaderTeam(BuildSettings());
            team.ActionChanged += OnActionChanged;
            team.Arrived += OnArrived;
            team.Inserted += OnInserted;
            team.Tapped += OnTapped;
            team.LoadCompleted += OnLoadCompleted;
            team.PauseChanged += OnPauseChanged;
            team.Called += OnCalled;
            team.SubstituteBegan += OnSubstituteBegan;
            team.Cleared += OnCleared;
            var flow = director.Flow;
            flow.SectionChanged += OnSectionChanged;
            flow.RetryRequested += OnRetry;
            bazooka.ExternalReloadCancelled += OnExternalCancelled;
            weapons.LoadoutReset += OnLoadoutReset;
            LoaderHandoff.Current = this;
        }

        void OnDestroy()
        {
            if (LoaderHandoff.Current == this) LoaderHandoff.Current = null;
            if (team == null) return;
            if (director != null && director.Flow != null)
            {
                director.Flow.SectionChanged -= OnSectionChanged;
                director.Flow.RetryRequested -= OnRetry;
            }
            if (bazooka != null)
            {
                bazooka.ExternalReloadCancelled -= OnExternalCancelled;
                bazooka.LoaderControlled = false;
            }
            if (weapons != null) weapons.LoadoutReset -= OnLoadoutReset;
        }

        LoaderTeamSettings BuildSettings()
        {
            var s = new LoaderTeamSettings
            {
                ReloadTime = bazooka.ReloadDuration,
                LoadClipLength = config.loadClipSeconds,
                ReadyClipTime = config.readyClipSeconds,
                InsertClipTime = config.insertClipSeconds,
                TapClipTimes = config.tapClipSeconds != null && config.tapClipSeconds.Length > 0 ? config.tapClipSeconds : new[] { config.readyClipSeconds },
                KneelInLength = config.kneelInSeconds, ReachLength = config.reachSeconds, BraceLength = config.braceSeconds,
                WaveLength = config.waveSeconds, ToRifleLength = config.toRifleSeconds,
                MinClipSpeed = config.minClipSpeed, MaxClipSpeed = config.maxClipSpeed,
                ArriveGap = config.arriveGap, FollowGap = config.followGap, LoadReach = config.loadReach, AbortGap = config.abortGap,
                FollowMax = config.followMax, WaveBeyond = config.waveBeyond, WaveCooldown = config.waveCooldown, GiveUpSeconds = config.giveUpSeconds,
            };
            return s;
        }

        // 탄약수 · 클립 · 궤적을 찾는다. 하나라도 모자라면 대체 모드(혼자 장전)
        void Resolve()
        {
            if (resolved) return;
            resolved = true;
            dataOk = TryResolve();
        }

        bool Fail(string why)
        {
            Debug.LogWarning("[LoaderTeam] 대체 모드(혼자 장전): " + why, this);
            return false;
        }

        bool TryResolve()
        {
            rifleTracks = Resources.Load<AllyRifleTracks>(AllyRifleTracks.ResourceName);
            propTracks = Resources.Load<LoaderPropTracks>(LoaderPropTracks.ResourceName);
            if (loader == null) loader = FindLoader();
            if (loader == null || rifleTracks == null || propTracks == null) return Fail("탄약수 · 클립 자료(AllyRifleTracks · LoaderPropTracks)를 찾지 못함");
            foreach (var name in AllClips)
            {
                var c = rifleTracks.Find(name);
                if (c == null || c.clip == null || propTracks.Find(name) == null) return Fail(name + " 클립 또는 소품 궤적이 없음 (FiveRivers/Setup/Rocket Team 을 다시 실행)");
            }
            animator = loader.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman) return Fail("탄약수 애니메이터가 사람 형이 아님");
            clips = animator.GetComponent<AllyClipPlayer>();
            if (clips == null) return Fail("탄약수에 AllyClipPlayer 가 붙지 않음");
            body = loader.transform;
            model = animator.transform;
            postPosition = body.position;
            postRotation = body.rotation;
            fit = rifleTracks.FitFor(loader.name, animator.avatar != null ? animator.avatar.name : "");
            if (fit == null) Debug.LogWarning("[LoaderTeam] 탄약수의 몸 맞춤(Fit)이 없어 로켓 소품 자리가 어긋날 수 있다 (ROK Rigify Clips 메뉴를 열린 씬에서 실행)", this);
            rigScale = animator.humanScale * model.lossyScale.y;
            hasAdvance = animator.HasState(0, AdvanceState);
            return true;
        }

        AllyCombatant FindLoader()
        {
            AllyCombatant nearest = null;
            float best = float.MaxValue;
            foreach (var ally in FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ally.name == config.loaderName) return ally;
                if (ally.name == "ParkYongpal" || ally.name == "Ally_1" || ally.name == "Ally_2" || points.moundTop == null) continue;
                float d = (ally.transform.position - points.moundTop.position).sqrMagnitude;
                if (d < best) { best = d; nearest = ally; }
            }
            return nearest;
        }

        bool LoaderUsable()
        {
            Resolve();
            return dataOk && !clipsBroken && director.Flow.IsExpanded && loader != null && loader.isActiveAndEnabled
                && animator != null && animator.isActiveAndEnabled && clips != null && clips.isActiveAndEnabled;
        }

        // ------------------------------------------------------------------------------------------------------------
        // 구간 · 재시도 · 탄약 초기화
        static bool InFight(FlowSection s) => s == FlowSection.Tank || s == FlowSection.AllySupport;

        void OnSectionChanged(FlowTransition t)
        {
            if (InFight(t.To))
            {
                if (!InFight(t.From)) BeginFight(t);
                return;
            }
            if (t.To == FlowSection.HillAssault)
            {
                SetControl(false);
                if (team.State != LoaderState.Idle || team.Deployed) team.BeginClearing();
                return;
            }
            if (t.To == FlowSection.Dawn || t.To == FlowSection.Result) return;
            FullReset(true); // 타이틀 · 튜토리얼 · 앞 구간들: 새 회차 / 처음 상태
        }

        void BeginFight(FlowTransition t)
        {
            FullReset(false);
            readySaid = false;
            if (entrance == null) entrance = FindFirstObjectByType<TankEntranceDirector>();
            waitEntrance = entrance != null && t.To == FlowSection.Tank && entrance.WillPlay(t);
            seenEntrance = false;
            waitEntranceAt = Time.time;
        }

        void OnRetry(FlowCheckpoint checkpoint)
        {
            if (checkpoint != FlowCheckpoint.Tank)
            {
                if (checkpoint != FlowCheckpoint.HillAssault) FullReset(true);
                return;
            }
            // 전차 재시도: 화면이 어두운 사이 탄약수를 자리에 세워 둔다 (제자리 · 제 상태, 핵심 설계 규칙 3)
            FullReset(false);
            readySaid = false;
            waitEntrance = false;
            Resolve();
            if (!LoaderUsable()) return;
            joined = false;
            var fwd = Facing(points.moundTop.position);
            var slot = SlotFor(points.moundTop.position, fwd);
            body.SetPositionAndRotation(slot, Quaternion.LookRotation(fwd));
            snapFrame = Time.frameCount;
            team.ForceReady();
        }

        void OnLoadoutReset()
        {
            // 탄약 · 자세가 처음으로 돌아갔다: 진행 중인 장전은 접는다 (고지 돌격 정리 중이면 건드리지 않는다)
            if (team != null && team.State != LoaderState.Clearing) team.AbortLoad();
        }

        void OnExternalCancelled()
        {
            if (team != null && team.State != LoaderState.Clearing) team.AbortLoad();
        }

        void FullReset(bool restorePost)
        {
            SetControl(false);
            if (team != null) team.Reset();
            joined = false;
            hasDeferred = false;
            StopWalking();
            StopStory();
            HideProps();
            if (restorePost && dataOk && body != null && (body.position - postPosition).sqrMagnitude > 0.0001f)
            {
                body.SetPositionAndRotation(postPosition, postRotation);
                snapFrame = Time.frameCount;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        void Update()
        {
            if (team == null) return;
            var flow = director.Flow;
            bool fight = InFight(flow.Section) && flow.IsExpanded;
            if (!fight)
            {
                if (controlled) SetControl(false);
                if (team.State == LoaderState.Clearing) { team.Tick(Time.deltaTime, default); ApplyBody(); UpdateProps(); }
                return;
            }
            if (waitEntrance)
            {
                if (entrance != null && entrance.Playing) seenEntrance = true;
                else if (seenEntrance || Time.time > waitEntranceAt + 2f) waitEntrance = false;
            }
            bool active = !waitEntrance && (entrance == null || !entrance.Playing);
            bool usable = LoaderUsable();
            if (usable && active) SetControl(true);
            else if (!usable) SetControl(false, true);

            float dt = flow.Paused ? 0f : Time.deltaTime;
            var input = BuildInput(active, usable);
            // 빈 발사관 자동 장전: 탄약수가 자리에 있고 사수가 닿는 곳에 있으면 요청한다 (발사 직후 · 보충 직후 · 무기 바꾼 뒤)
            if (autoLoad && controlled && team.State == LoaderState.Ready && !bazooka.Reloading && bazooka.Loaded == 0 && bazooka.Reserve > 0
                && input.GunnerOk && input.GunnerGap <= config.loadReach)
            {
                bazooka.TryReload();
                input.ReloadWanted = bazooka.Reloading && bazooka.ExternalReload;
            }
            team.Tick(dt, input);
            ApplyBody();
            if (hasDeferred && Time.frameCount >= snapFrame + 3) { hasDeferred = false; PlayAction(deferred); }
            UpdateProps();
        }

        LoaderInput BuildInput(bool active, bool usable)
        {
            var input = new LoaderInput { Active = active, Present = usable };
            if (!usable || !active) return input;
            var playerPos = player.transform.position;
            if (!joined && Flat(playerPos - points.moundTop.position).magnitude <= config.joinRadius) joined = true;
            var anchor = joined ? playerPos : points.moundTop.position;
            var fwd = Facing(anchor);
            var slot = SlotFor(anchor, fwd);
            var right = Vector3.Cross(Vector3.up, fwd);
            var expectedGunner = body.position - (right * config.slotOffset.x + fwd * config.slotOffset.y);
            input.SlotGap = Flat(body.position - slot).magnitude;
            input.GunnerGap = Flat(playerPos - expectedGunner).magnitude;
            input.CanFollow = joined;
            var look = Flat(player.transform.forward);
            bool lookOk = look.sqrMagnitude < 0.0001f || Vector3.Angle(look, fwd) <= config.maxLookAngle;
            bool crouchOk = !(config.pauseWhileCrouched && player.Crouched);
            input.GunnerOk = crouchOk && lookOk;
            input.ReloadWanted = bazooka.Reloading && bazooka.ExternalReload;
            input.TubeLoaded = bazooka.Loaded > 0;
            input.Reserve = bazooka.Reserve;
            wantedSlot = slot;
            wantedFacing = fwd;
            return input;
        }

        Vector3 wantedSlot, wantedFacing = Vector3.forward;

        Vector3 Facing(Vector3 from)
        {
            var d = Flat(points.tankHold.position - from);
            return d.sqrMagnitude > 0.01f ? d.normalized : Flat(player.transform.forward).normalized;
        }

        // 사수 오른쪽 · 뒤의 바닥 (NavMesh 가 있으면 걸을 수 있는 자리로)
        Vector3 SlotFor(Vector3 anchor, Vector3 fwd)
        {
            var right = Vector3.Cross(Vector3.up, fwd);
            var p = anchor + right * config.slotOffset.x + fwd * config.slotOffset.y;
            if (NavMesh.SamplePosition(p, out var hit, 1.2f, NavMesh.AllAreas)) p = new Vector3(hit.position.x, p.y, hit.position.z);
            return Ground(p, body);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------------------------------------------------------
        // 바주카 장전을 탄약수에게 맡기고 거두기
        void SetControl(bool on, bool takeOver = false)
        {
            if (on == controlled || bazooka == null) return;
            if (on)
            {
                if (bazooka.Reloading && !bazooka.ExternalReload) return; // 혼자 하던 장전이 끝난 뒤에
                bazooka.LoaderControlled = true;
                controlled = true;
            }
            else
            {
                bazooka.LoaderControlled = false;
                controlled = false;
                if (bazooka.ExternalReload) { if (takeOver) bazooka.TakeOverReload(); else bazooka.CancelReload(); }
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // 이벤트 → 장면
        void OnLoadCompleted() => bazooka.CompleteReload();

        void OnSubstituteBegan()
        {
            if (team.Unavailable) SetControl(false, true);
            else if (bazooka.ExternalReload) bazooka.TakeOverReload(); // 이번 장전만 사수 혼자: 1인칭 장전 동작이 다시 돈다
        }

        void OnPauseChanged(bool paused)
        {
            if (clips != null && storyOn) clips.SetClipSpeed(paused ? 0f : team.ActionSpeed);
        }

        void OnInserted()
        {
            Audio()?.Play2D(config.insertSoundId);
        }

        void OnTapped(int index)
        {
            // 1인칭에는 탄약수가 보이지 않으므로 "장전 중 · 다 됐다"는 두드림의 흔들림 · 소리로 전한다 (화면만 흔들리고 조준은 그대로)
            player.AddShake(config.tapShakeDegrees, config.tapShakeSeconds);
            Audio()?.Play2D(config.tapSoundId);
            if (index == 1 && !readySaid && subtitles != null && text != null) { readySaid = true; subtitles.Say(text.loaderReady); }
        }

        void OnCalled()
        {
            if (subtitles != null && text != null) subtitles.Say(text.loaderCall);
        }

        void OnArrived() => StopWalking();

        void OnCleared()
        {
            StopWalking();
            StopStory(0.35f);
            HideProps();
        }

        AudioDirector Audio()
        {
            if (audioDirector == null) audioDirector = FindFirstObjectByType<AudioDirector>();
            return audioDirector;
        }

        // ------------------------------------------------------------------------------------------------------------
        // 클립
        void OnActionChanged(LoaderAction action)
        {
            if (!dataOk) return;
            if (Time.frameCount < snapFrame + 3) { deferred = action; hasDeferred = true; return; } // 순간 이동 직후엔 클립 장치가 요청을 지운다
            hasDeferred = false;
            PlayAction(action);
        }

        void PlayAction(LoaderAction action)
        {
            if (!dataOk || clips == null) return;
            switch (action)
            {
                case LoaderAction.None: StopStory(0.35f); break;
                case LoaderAction.KneelIn: Play(ClipKneelIn, 0.25f); break;
                case LoaderAction.KneelIdle: Play(ClipKneelIdle, 0.25f); break;
                case LoaderAction.Reach: Play(ClipReach, 0.2f); break;
                case LoaderAction.Load: Play(ClipLoad, 0.2f); break;
                case LoaderAction.Brace: Play(ClipBrace, 0.1f); break;
                case LoaderAction.Wave: Play(ClipWave, 0.3f); break;
                case LoaderAction.ToRifle: Play(ClipToRifle, 0.3f); break;
            }
        }

        void Play(string clip, float fade)
        {
            float speed = team.Action == LoaderAction.Load ? (team.Paused ? 0.0001f : team.ActionSpeed) : 1f;
            storyOn = clips.PlayStory(clip, fade, true, speed);
            if (!storyOn) { clipsBroken = true; Debug.LogWarning("[LoaderTeam] 클립을 틀지 못해 대체 모드로 간다: " + clip, this); }
        }

        void StopStory(float fade = 0.2f)
        {
            if (clips != null && storyOn) clips.StopStory(fade);
            storyOn = false;
        }

        // ------------------------------------------------------------------------------------------------------------
        // 걷기와 방향
        void ApplyBody()
        {
            if (!dataOk || body == null) return;
            if (team.Walking) Walk(Time.deltaTime);
            else
            {
                if (walkingAnim) StopWalking();
                // 자리에서 전차 쪽을 본다 (장전 · 웅크림 중에는 돌리지 않는다)
                var a = team.Action;
                if ((a == LoaderAction.KneelIdle || a == LoaderAction.KneelIn) && wantedFacing.sqrMagnitude > 0.01f)
                {
                    var want = Quaternion.LookRotation(wantedFacing);
                    if (Quaternion.Angle(body.rotation, want) > 18f || team.State == LoaderState.Moving)
                        body.rotation = Quaternion.RotateTowards(body.rotation, want, config.turnRate * 0.35f * Time.deltaTime);
                }
            }
        }

        void Walk(float dt)
        {
            if (dt <= 0f) return;
            var target = wantedSlot;
            if (path.Count == 0 || Time.time >= nextRepath || Flat(target - lastTarget).sqrMagnitude > 0.25f) PlanPath(target);
            var pos = body.position;
            while (corner < path.Count - 1 && Flat(path[corner] - pos).magnitude < 0.5f) corner++;
            var next = corner < path.Count ? path[corner] : target;
            if (corner >= path.Count - 1 && Flat(target - pos).magnitude < 3f) next = target;
            var flat = Flat(next - pos);
            float distance = flat.magnitude;
            float want = Mathf.Min(config.moveSpeed, Mathf.Max(0.8f, distance * 2.5f));
            walkSpeed = Mathf.MoveTowards(walkSpeed, want, 5f * dt);
            if (distance > 0.01f)
            {
                var steer = flat / distance;
                // 사수의 몸을 뚫고 지나지 않는다 (처음 걸음이 사수 곁을 스쳤다): 1 m 안이면 비켜 간다
                var toPlayer = Flat(player.transform.position - pos);
                float playerGap = toPlayer.magnitude;
                if (playerGap < 1.0f && playerGap > 0.01f) steer = (steer - toPlayer / playerGap * (1.0f - playerGap) * 1.5f).normalized;
                float off = Vector3.Angle(Flat(body.forward), steer);
                walkSpeed *= 1f - Mathf.InverseLerp(45f, 100f, off) * 0.8f;
                body.position = Ground(pos + steer * Mathf.Min(distance, walkSpeed * dt), body);
                body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(steer), config.turnRate * dt);
            }
            SetWalkAnimation(true);
        }

        void PlanPath(Vector3 target)
        {
            path.Clear();
            corner = 0;
            nextRepath = Time.time + 0.75f;
            lastTarget = target;
            navPathInstance ??= new NavMeshPath();
            if (NavMesh.SamplePosition(body.position, out var from, 2.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(target, out var to, 2.5f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, navPathInstance)
                && navPathInstance.status != NavMeshPathStatus.PathInvalid && navPathInstance.corners.Length > 1)
            {
                path.AddRange(navPathInstance.corners);
                corner = 1;
            }
            else { path.Add(body.position); path.Add(target); corner = 1; }
        }

        void SetWalkAnimation(bool on)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            if (on)
            {
                if (!walkingAnim)
                {
                    walkingAnim = true;
                    loader.Advancing = true; // 사격 · 대기 동작은 쉬고 소총은 이동 자세로
                    if (stride == null) stride = StrideGrounding.Attach(animator, body);
                }
                int state = hasAdvance ? AdvanceState : TiredWalkState;
                bool inState = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).shortNameHash == state
                    : animator.GetCurrentAnimatorStateInfo(0).shortNameHash == state;
                if (!inState) animator.CrossFadeInFixedTime(state, 0.2f);
                float clip = hasAdvance ? AllyClipPlayer.AdvanceClipSpeedFor(animator, 2.5f) : 0.95f;
                animator.speed = Mathf.Clamp(walkSpeed / (clip * rigScale), 0.75f, 1.32f);
                animator.SetFloat(SpeedId, state == TiredWalkState ? walkSpeed : 0f);
                if (stride != null) stride.Active = true;
            }
        }

        void StopWalking()
        {
            path.Clear();
            walkSpeed = 0f;
            if (!walkingAnim) return;
            walkingAnim = false;
            if (loader != null) loader.Advancing = false;
            if (stride != null) stride.Active = false;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.speed = 1f;
                animator.SetFloat(SpeedId, 0f);
                animator.CrossFadeInFixedTime(IdleState, 0.2f);
            }
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];
        static Vector3 Ground(Vector3 p, Transform self)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 1.2f, Vector3.down, hits, 3f, WorldQuery.Solid, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity, bestGap = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if ((self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<CharacterController>() != null
                    || c.GetComponentInParent<AllyCombatant>() != null || c.GetComponentInParent<EnemyAgent>() != null || c.attachedRigidbody != null) continue;
                float gap = Mathf.Abs(hits[i].point.y - p.y);
                if (gap < bestGap) { bestGap = gap; best = hits[i].point.y; }
            }
            if (best > float.NegativeInfinity) p.y = best;
            return p;
        }

        // ------------------------------------------------------------------------------------------------------------
        // 로켓 소품 (탄약수가 들고 발사관에 밀어 넣는 로켓 + 옆에 세워 둔 예비)
        void BuildProps()
        {
            propsBuilt = true;
            if (rocketModel == null) { Debug.LogWarning("[LoaderTeam] 로켓 모델이 없어 소품 없이 동작만 보인다", this); return; }
            propRoot = new GameObject("LoaderRocketProps").transform;
            propRoot.SetParent(transform, false);
            propHolders = new Transform[4];
            for (int i = 0; i < propHolders.Length; i++)
            {
                var holder = new GameObject(i == 0 ? "Rocket_Carried" : "Rocket_Pile_" + i).transform;
                holder.SetParent(propRoot, false);
                var visual = Instantiate(rocketModel, holder);
                visual.name = "Model";
                visual.transform.SetLocalPositionAndRotation(config.propModelOffset, Quaternion.Euler(config.propModelEuler));
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
                foreach (var col in visual.GetComponentsInChildren<Collider>(true)) Destroy(col);
                holder.gameObject.SetActive(false);
                propHolders[i] = holder;
            }
        }

        void HideProps()
        {
            if (propHolders == null) return;
            foreach (var h in propHolders) if (h != null) h.gameObject.SetActive(false);
        }

        Vector3 ToWorld(Vector3 p)
        {
            float scale = fit != null ? fit.scale : 1f;
            var offset = fit != null ? fit.offset : Vector3.zero;
            float floor = fit != null ? fit.floorOffset : 0f;
            return model.TransformPoint(p * scale + offset) + model.up * (floor * model.lossyScale.y);
        }

        void UpdateProps()
        {
            bool visible = dataOk && team != null && team.Deployed && !team.Unavailable && team.State != LoaderState.Idle
                && (team.State != LoaderState.Moving || team.Settled || team.Action == LoaderAction.KneelIn);
            bool show = visible || (dataOk && team != null && team.State == LoaderState.Clearing);
            if (!show || rocketModel == null) { HideProps(); return; }
            if (!propsBuilt) BuildProps();
            if (propHolders == null) return;

            float scale = (fit != null ? fit.scale : 1f) * model.lossyScale.y;
            bool carried = false;
            if (clips.TryGetClipTime(out var name, out float time))
            {
                var clip = propTracks.Find(name);
                if (clip != null)
                {
                    var f = LoaderPropTracks.Sample(clip, time, name == ClipKneelIdle);
                    bool has = bazooka.Reserve > 0 || team.Action == LoaderAction.Load;
                    carried = has && f.state != LoaderPropTracks.PropState.Hidden;
                    if (carried)
                    {
                        var holder = propHolders[0];
                        holder.SetPositionAndRotation(ToWorld(f.position), model.rotation * f.rotation);
                        holder.localScale = Vector3.one * scale;
                    }
                }
            }
            propHolders[0].gameObject.SetActive(carried);

            // 세워 둔 예비 로켓: 들고 있는 한 발을 뺀 나머지 (0번 자리는 집는 동작 중에는 비운다)
            int pile = config.showPile && team.State != LoaderState.Clearing ? Mathf.Max(0, bazooka.Reserve - 1) : 0;
            bool takingFromPile = team.Action == LoaderAction.KneelIn || team.Action == LoaderAction.Reach || team.Action == LoaderAction.ToRifle;
            for (int i = 0; i < 3; i++)
            {
                var holder = propHolders[i + 1];
                bool shown = i < pile && i < propTracks.pile.Length && !(i == 0 && takingFromPile);
                holder.gameObject.SetActive(shown);
                if (!shown) continue;
                holder.SetPositionAndRotation(ToWorld(propTracks.pile[i].position), model.rotation * propTracks.pile[i].rotation);
                holder.localScale = Vector3.one * scale;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // HillAllies 연결
        internal float HandoffDelay(AllyCombatant ally)
        {
            if (team == null || ally != loader) return 0f;
            return team.State == LoaderState.Clearing ? team.ClearSecondsLeft + 0.3f : 0f;
        }

        internal bool TryGetPost(AllyCombatant ally, out Vector3 position, out Quaternion rotation)
        {
            position = postPosition; rotation = postRotation;
            return dataOk && ally == loader && team != null && (team.State == LoaderState.Clearing || (body.position - postPosition).sqrMagnitude > 0.01f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string DebugState => team == null ? "off" :
            $"{team.State}/{team.Action} load {team.LoadElapsed:0.00}/{bazooka.ReloadDuration:0.0}{(team.Paused ? " PAUSED" : "")} ctrl {controlled} joined {joined}"
            + (loader != null ? $" loader {loader.name}" : " no loader") + (clipsBroken ? " CLIPS-BROKEN" : "");
#endif
    }
}
