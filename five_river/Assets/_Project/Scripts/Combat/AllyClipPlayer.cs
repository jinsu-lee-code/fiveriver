using System.Collections.Generic;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.Narrative;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace FiveRivers.Combat
{
    // Rigify 동작 클립 재생 장치 (2026-11-18). 컨트롤러(AC_ROK: 상태 · 매개변수 그대로)는 건드리지 않고 그 위에
    // PlayableGraph 로 클립을 얹는다. 언제 무엇을 틀지는 AllyClipBrain(순수 로직)이 정한다.
    //  - 참호 구간(재정비 · 정적 · 전차, 그리고 싸움이 아직 그에게 오지 않은 교전 구간)에서 서 있는 대신 "참호 낮춘 대기".
    //    그 위에서 변화 동작을 AllyIdleLife 와 같은 규칙으로 엇갈려 튼다. 그동안 AllyIdleLife(VARCO 변화량 · 절차적 고개)는 쉰다.
    //  - 가까운 포탄(AllyClipCues) → 머리 숙이기. 교전 시작 → 낮춤→무릎 사격 → 컨트롤러의 무릎 사격.
    //  - 클립이 도는 동안 소총은 클립의 궤적(AllyRifleTracks: JSON 을 이 사람의 가슴뼈 기준으로 구운 것)대로 놓고,
    //    접촉이 1인 손만 총에 붙인다(AllyRiflePose.SolveArm). AllyCombatant 의 TryApplyCarry · ApplyPortArms · Apply · LookAt 은 쉰다.
    //  - 컨트롤러 자세 ↔ 클립은 PoseBlendIn 과 같은 방식(들어가는 순간 / 나가는 순간의 자세를 붙들고 그 자세에서 섞음)으로 잇는다.
    //  - 고지 돌격: 컨트롤러 상태 Run_Rifle / CrouchRun_Rifle 의 클립만 실행 중에 새 클립으로 바꿔 끼운다(AnimatorOverrideController,
    //    에셋은 그대로). 상태 이름 · 전환 · HillAllies 의 박자는 그대로이고, 소총은 그 상태의 재생 시각으로 궤적을 읽는다.
    // 표시 전용: 판정 · 진행 · 수치와 무관하다. 에셋(Resources/AllyRifleTracks)이 없으면 붙지 않는다(예전 그대로).
    // 실행 순서: StrideGrounding(-60) 다음, AllyIdleLife(-45) · AllyCombatant(0) 앞.
    [DefaultExecutionOrder(-55), DisallowMultipleComponent]
    public sealed class AllyClipPlayer : MonoBehaviour
    {
        // 디버그 · 화면 맞춤용 (AllyIdleLife 처럼 실행 중에 바꿀 수 있게 static)
        public static bool Active = true;
        public static bool UseRigifyRun = true;
        // 무릎 대기 · 무릎 사격(컨트롤러 Idle_Trench / Fire_Crouch)의 클립도 새 클립으로 (2026-11-18 저녁: 꿇은 뒷발 발등이 땅)
        public static bool UseRigifyKneel = true;
        public static float DuckRadius = 30f, DuckLead = 0.3f;
        // 서 있음 = 엉덩이가 사람 크기의 이 비율보다 높다 (NarrativeDirector.MansuStandingHips 와 같은 값)
        public static float LowHips = 0.62f;
        public static float MaxOffFront = 50f, MaxFromPost = 0.6f, TeleportStep = 0.5f;
        // 구간 컷(CutsceneRig) 중에도 낮춘 대기는 둔다(새 변화 동작만 쉰다)
        public static bool KeepLowInCutscene = true;
        // 교전 시작 때 낮춤→무릎 사격 클립(0.67초)을 거친다. false = 0.12초 만에 컨트롤러의 무릎 사격으로 (다음 장면부터)
        public static bool UseFireTransition = true;

        static readonly FlowSection[] TrenchSections =
        {
            FlowSection.Regroup, FlowSection.Silence, FlowSection.Tank, FlowSection.AllySupport,
            // 교전 구간: 적이 그의 눈에 들기 전까지 (교전이 시작되면 낮춤→무릎 사격)
            FlowSection.Scout, FlowSection.Assault, FlowSection.WestAssault,
        };

        static readonly int IdleStandId = Animator.StringToHash("Idle_Stand");
        static readonly int IdleTrenchId = Animator.StringToHash("Idle_Trench");
        static readonly int FireCrouchId = Animator.StringToHash("Fire_Crouch");
        static readonly int RunRifleId = Animator.StringToHash("Run_Rifle");
        static readonly int CrouchRunRifleId = Animator.StringToHash("CrouchRun_Rifle");

        static AllyRifleTracks data;
        static bool dataLoaded;
        static readonly AllyClipRoster roster = new AllyClipRoster();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { roster.Clear(); data = null; dataLoaded = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachAll();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachAll();

        static AllyRifleTracks Data
        {
            get
            {
                if (!dataLoaded) { data = Resources.Load<AllyRifleTracks>(AllyRifleTracks.ResourceName); dataLoaded = true; }
                return data;
            }
        }

        // Idempotent: one per ally, on the object that carries his animator (like AllyIdleLife).
        public static void AttachAll()
        {
            if (Data == null) return;
            foreach (var combatant in FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var animator = combatant.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman || animator.GetComponent<AllyClipPlayer>() != null) continue;
                animator.gameObject.AddComponent<AllyClipPlayer>().Bind(combatant, animator);
            }
        }

        // HillAllies: the foot speed of the run clip this man really plays (the new clip's, once it is in)
        public static float RunClipSpeedFor(Animator animator, float fallback)
        {
            var player = animator != null ? animator.GetComponent<AllyClipPlayer>() : null;
            return player != null && player.runInstalled && Data != null && Data.runClipSpeed > 0f ? Data.runClipSpeed : fallback;
        }

        public static float AdvanceClipSpeedFor(Animator animator, float fallback)
        {
            var player = animator != null ? animator.GetComponent<AllyClipPlayer>() : null;
            return player != null && player.advanceInstalled && Data != null && Data.advanceClipSpeed > 0f ? Data.advanceClipSpeed : fallback;
        }

        AllyCombatant ally;
        Animator animator;
        Transform body, model, hips, anchor;
        AllyRifleTracks.Fit fit;
        AllyClipBrain brain;
        FlowDirector director;
        bool bound, subscribed, pendingReset;

        // ---- graph ----
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable current, previous;
        AllyRifleTracks.Clip currentClip, previousClip;
        float fadeTime, fadeLength;
        AllyClipPhase phaseBefore;

        // ---- pose blend between the controller and the graph ----
        Transform[] bones;
        Quaternion[] from;
        Vector3 hipsFrom;
        float blendTime, blendLength = 1f;
        bool blending, blendingIn;
        float graphWeight;
        bool wrote;
        Vector3 writtenPosition;
        Quaternion writtenRotation;

        // ---- the rifle ----
        sealed class Arm
        {
            public Transform upper, fore, hand;
            public Transform[] chain;     // upper, fore, hand, then the fingers
            public Quaternion[] clip, carry;
        }
        Arm left, right;
        bool armsSnapped;
        Vector3 carryPosition, frozenLocalPosition;
        Quaternion carryRotation, frozenLocalRotation;
        float frozenGripLeft = 1f, frozenGripRight = 1f;
        bool frozenValid;
        bool runInstalled, advanceInstalled, kneelInstalled;

        // ---- what he is doing ----
        Vector3 home, lastPosition, front;
        Quaternion lastRotation;
        bool frontKnown;
        float stillFor, walkFor, turnFor, checkAt;
        bool teleported, componentBusy;
        LevelPoints points;
        readonly HashSet<int> shellsSeen = new HashSet<int>();
        float duckReaction;

        public bool Busy => bound && (brain.Active || blending);
        public float RifleWeight => graphWeight;
        public bool OwnsRifle => graphWeight >= 0.999f;
        // no rounds while the clip has the rifle (it points wherever the clip carries it)
        public bool HoldsFire => graph.IsValid() || blending;
        public bool RunClipsInstalled => runInstalled;
        public bool AdvanceClipsInstalled => advanceInstalled;
        public bool KneelClipsInstalled => kneelInstalled;
        public AllyClipPhase Phase => bound ? brain.Phase : AllyClipPhase.Off;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string DebugState => bound ? $"{brain.Phase} {brain.Clip} t{brain.ClipTime:0.00} w{graphWeight:0.00}" + (fit == null ? " (no fit)" : "") : "unbound";
        public string DebugClip => bound && brain.Clip != null ? brain.Clip : "";
#endif

        void Bind(AllyCombatant combatant, Animator source)
        {
            ally = combatant; animator = source; body = combatant.transform; model = source.transform;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null) { enabled = false; return; }
            string avatarName = animator.avatar != null ? animator.avatar.name : "";
            fit = Data.FitFor(combatant.name, avatarName);
            anchor = animator.GetBoneTransform(fit != null ? fit.anchor : HumanBodyBones.UpperChest);
            if (anchor == null) anchor = animator.GetBoneTransform(HumanBodyBones.Chest);

            var list = new List<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var t = animator.GetBoneTransform((HumanBodyBones)i);
                if (t != null && !list.Contains(t)) list.Add(t);
            }
            bones = list.ToArray();
            from = new Quaternion[bones.Length];
            left = MakeArm(true);
            right = MakeArm(false);

            var infos = new List<AllyClipBrain.ClipInfo>();
            foreach (var c in Data.clips)
                if (c != null && c.clip != null && c.role != AllyClipRole.Run && c.role != AllyClipRole.CrouchRun && c.role != AllyClipRole.RunStop && c.role != AllyClipRole.RunStart
                    && c.role != AllyClipRole.KneelIdle && c.role != AllyClipRole.KneelFire
                    && (UseFireTransition || c.role != AllyClipRole.ExitToFire))
                    infos.Add(new AllyClipBrain.ClipInfo(c.name, c.role, c.clip.length));
            brain = new AllyClipBrain(infos, Data.timing, roster, AllyClipBrain.Seed(combatant.name));
            duckReaction = (Mathf.Abs(AllyClipBrain.Seed(combatant.name + "duck")) % 1000) / 1000f * 0.35f - 0.2f; // -0.2 ~ +0.15 s

            home = lastPosition = body.position;
            lastRotation = body.rotation;
            combatant.Clips = this;
            bound = true;
            // before AllyCombatant.Start (this runs after the scene has loaded, before any Start): swapping the
            // controller resets the animator, which is harmless before anyone has set a state on it
            if (UseRigifyRun || UseRigifyKneel) InstallRunClips();
        }

        Arm MakeArm(bool isLeft)
        {
            var arm = new Arm
            {
                upper = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm),
                fore = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm),
                hand = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand),
            };
            var chain = new List<Transform> { arm.upper, arm.fore, arm.hand };
            int first = (int)(isLeft ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            int last = (int)(isLeft ? HumanBodyBones.LeftLittleDistal : HumanBodyBones.RightLittleDistal);
            for (int i = first; i <= last; i++) chain.Add(animator.GetBoneTransform((HumanBodyBones)i));
            arm.chain = chain.ToArray();
            arm.clip = new Quaternion[arm.chain.Length];
            arm.carry = new Quaternion[arm.chain.Length];
            return arm;
        }

        void OnDisable() => Release();

        void OnDestroy()
        {
            Release();
            if (brain != null) brain.Forget();
            if (subscribed && director != null && director.Flow != null)
            {
                director.Flow.SectionChanged -= OnSectionChanged;
                director.Flow.RetryRequested -= OnRetry;
            }
        }

        // Drops the clip at once (AllyCombatant switched off: a director takes the man over, e.g. Choi Man-su's fall).
        public void Release()
        {
            StopGraph();
            blending = false;
            graphWeight = 0f;
            wrote = false;
            frozenValid = false;
            if (brain != null) brain.Tick(new AllyClipSituation { Reset = true, Time = Time.time }, 0f);
        }

        // ---- story clips: a director asks for one by name (Park kneeling by the body, Yun going prone; to come) ----
        public bool PlayStory(string clipName, float fadeIn = 0.4f, bool hold = true, float speed = 1f) => bound && brain.RequestStory(clipName, fadeIn, hold, speed);
        // 로켓포반(2026-11-24): 지금 틀고 있는 클립의 배속을 바꾼다 (0 = 멈춤). 그래프가 없으면 아무 일도 없다.
        public void SetClipSpeed(float speed) { if (graph.IsValid() && current.IsValid()) current.SetSpeed(Mathf.Max(0f, speed)); }
        // 지금 틀고 있는 클립의 이름과 시각(초, 클립 시간). 클립이 없으면 false.
        public bool TryGetClipTime(out string clip, out float time)
        {
            clip = null; time = 0f;
            if (!graph.IsValid() || !current.IsValid() || currentClip == null) return false;
            clip = currentClip.name; time = (float)current.GetTime();
            return true;
        }
        public void StopStory(float fadeOut = 0.4f) { if (bound) brain.StopStory(fadeOut); }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Title || t.To == FlowSection.Tutorial) pendingReset = true;
        }

        void OnRetry(FlowCheckpoint checkpoint) => pendingReset = true;

        // ---------------------------------------------------------------------------------------------------------
        void Update()
        {
            if (!bound) return;
            if (director == null) director = FindFirstObjectByType<FlowDirector>();
            if (!subscribed && director != null && director.Flow != null)
            {
                director.Flow.SectionChanged += OnSectionChanged;
                director.Flow.RetryRequested += OnRetry;
                subscribed = true;
            }
            float dt = Time.deltaTime;
            Watch(dt);
            var situation = Situation();
            phaseBefore = brain.Phase;
            var order = brain.Tick(situation, dt);
            Carry(order);
            TickFade(dt);
        }

        void Watch(float dt)
        {
            teleported = false;
            if (dt > 0f)
            {
                var step = body.position - lastPosition;
                float moved = step.magnitude;
                float turned = Quaternion.Angle(body.rotation, lastRotation);
                teleported = moved > TeleportStep;
                walkFor = moved / dt > 0.06f && !teleported ? walkFor + dt : 0f;
                // a snap of the facing in one frame (AllyCombatant.ResetCombat) is not a turn
                turnFor = turned / dt > 20f && turned < 30f ? turnFor + dt : 0f;
                stillFor = moved / dt > 0.06f || turned / dt > 12f ? 0f : stillFor + dt;
                lastPosition = body.position;
                lastRotation = body.rotation;
            }
            if (Time.time >= checkAt)
            {
                checkAt = Time.time + 0.25f;
                var bow = animator.GetComponent<HeadBow>();
                componentBusy = (bow != null && bow.enabled) || animator.GetComponent<FallenPose>() != null
                    || animator.GetComponent<PoseBlendIn>() != null || animator.GetComponent<LyingBodyRest>() != null;
                // a new run puts him back on his post (like AllyIdleLife)
                if (director != null && director.Flow != null && director.Flow.Section == FlowSection.Tutorial && director.Flow.SectionTime < 1.0)
                    home = body.position;
            }
        }

        AllyClipSituation Situation()
        {
            var flow = director != null ? director.Flow : null;
            var s = new AllyClipSituation
            {
                Time = Time.time,
                Alive = Active && ally != null && ally.isActiveAndEnabled && animator.isActiveAndEnabled,
                Reset = pendingReset,
                Teleported = teleported,
                Engaged = ally.Engaged,
                StandFire = ally.StandsToFire,
                Advancing = ally.Advancing,
                TakingCover = ally.TakingCover,
                StillFor = stillFor,
                Walking = walkFor > 0.1f,
                Turning = turnFor > 0.12f,
                Cutscene = CutsceneRig.Running > 0,
            };
            pendingReset = false;
            if (flow != null && flow.IsRunning)
            {
                foreach (var section in TrenchSections) if (flow.Section == section) s.InTrenchSection = true;
                if (!KeepLowInCutscene && s.Cutscene) s.InTrenchSection = false;
            }
            // something else poses him: a head bow, the fall, or the controller in a state of its own (the call, a walk)
            bool controllerOwn = !ControllerFree() && !s.Engaged && !s.TakingCover && brain.Phase != AllyClipPhase.ToFire;
            s.Busy = componentBusy || controllerOwn;
            var away = body.position - home; away.y = 0f;
            s.AwayFromPost = away.sqrMagnitude > MaxFromPost * MaxFromPost;
            if (!frontKnown)
            {
                if (points == null) points = FindFirstObjectByType<LevelPoints>();
                front = AllyCombatant.ApproachDirection(home, points, body.forward);
                frontKnown = true;
            }
            var facing = body.forward; facing.y = 0f;
            s.FacingAway = facing.sqrMagnitude > 0.0001f && Vector3.Angle(facing, front) > MaxOffFront;
            // he is down already (has just stopped firing): straight into the low wait
            s.Low = hips.position.y - model.position.y <= LowHips * animator.humanScale * model.lossyScale.y;
            // 사람마다 반응이 다르다: 넷이 같은 프레임에 같은 동작으로 숙이면 기계처럼 보였다 (2026-11-18)
            s.Duck = AllyClipCues.TakeNear(body.position, DuckRadius, DuckLead + duckReaction, shellsSeen);
            return s;
        }

        bool ControllerFree()
        {
            if (!animator.isActiveAndEnabled) return false;
            if (!animator.IsInTransition(0)) return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == IdleStandId;
            return animator.GetNextAnimatorStateInfo(0).shortNameHash == IdleStandId;
        }

        bool StateIs(int state)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == state) return true;
            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == state;
        }

        void Carry(AllyClipOrder order)
        {
            switch (order.Kind)
            {
                case AllyClipOrderKind.Play:
                {
                    var clip = Data.Find(order.Clip);
                    if (clip == null || clip.clip == null) { Release(); return; }
                    bool fresh = order.FromController || !graph.IsValid();
                    if (fresh)
                    {
                        StartGraph();
                        BeginBlend(order.Fade, true);
                    }
                    PlayInGraph(clip, order.StartTime, fresh ? 0f : order.Fade, order.Speed > 0f ? order.Speed : 1f);
                    break;
                }
                case AllyClipOrderKind.Leave:
                {
                    if (!graph.IsValid() && !blending) break;
                    // the kneel to fire ends on one knee: the controller must be kneeling under it by now, whatever it
                    // made of the Crouch flag while the clip had the pose
                    if (phaseBefore == AllyClipPhase.ToFire && ally.Engaged && animator.isActiveAndEnabled
                        && !StateIs(IdleTrenchId) && !StateIs(FireCrouchId))
                        animator.Play(IdleTrenchId, 0, 0f);
                    FreezeTrack();
                    StopGraph();
                    if (order.LeaveSeconds > 0f) BeginBlend(order.LeaveSeconds, false);
                    else { blending = false; graphWeight = 0f; wrote = false; }
                    break;
                }
            }
        }

        void StartGraph()
        {
            StopGraph();
            graph = PlayableGraph.Create("Ally clips " + body.name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Ally clips", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
            graph.Play();
        }

        void StopGraph()
        {
            if (graph.IsValid()) graph.Destroy();
            current = previous = default;
            currentClip = previousClip = null;
            fadeLength = fadeTime = 0f;
        }

        void PlayInGraph(AllyRifleTracks.Clip clip, float startTime, float crossFade, float speed = 1f)
        {
            if (previous.IsValid())
            {
                graph.Disconnect(mixer, 1);
                previous.Destroy();
                previous = default;
                previousClip = null;
            }
            if (current.IsValid() && crossFade > 0f)
            {
                graph.Disconnect(mixer, 0);
                graph.Connect(current, 0, mixer, 1);
                previous = current;
                previousClip = currentClip;
            }
            else if (current.IsValid())
            {
                graph.Disconnect(mixer, 0);
                current.Destroy();
            }
            current = AnimationClipPlayable.Create(graph, clip.clip);
            current.SetApplyFootIK(false);
            current.SetTime(startTime);
            current.SetSpeed(speed);
            currentClip = clip;
            graph.Connect(current, 0, mixer, 0);
            fadeTime = 0f;
            fadeLength = previous.IsValid() ? crossFade : 0f;
            Weights();
        }

        void TickFade(float dt)
        {
            if (!graph.IsValid() || !previous.IsValid()) return;
            fadeTime += dt;
            if (fadeTime >= fadeLength)
            {
                graph.Disconnect(mixer, 1);
                previous.Destroy();
                previous = default;
                previousClip = null;
            }
            Weights();
        }

        float CurrentShare => previous.IsValid() && fadeLength > 0f ? Mathf.SmoothStep(0f, 1f, fadeTime / fadeLength) : 1f;

        void Weights()
        {
            float w = CurrentShare;
            mixer.SetInputWeight(0, w);
            mixer.SetInputWeight(1, previous.IsValid() ? 1f - w : 0f);
        }

        // Holds the pose he has now (last frame's, with every correction on it) and eases from it into what the
        // animator plays next: the graph when going in, the controller when coming out (as PoseBlendIn does).
        void BeginBlend(float seconds, bool goingIn)
        {
            for (int i = 0; i < bones.Length; i++) from[i] = bones[i].localRotation;
            hipsFrom = hips.localPosition;
            blendTime = 0f;
            blendLength = Mathf.Max(0.01f, seconds);
            blending = seconds > 0f;
            blendingIn = goingIn;
            if (!blending) graphWeight = goingIn ? 1f : 0f;
            wrote = false;
        }

        void LateUpdate()
        {
            if (!bound) return;
            bool graphOn = graph.IsValid();
            if (!graphOn && !blending)
            {
                wrote = false;
                graphWeight = 0f;
                ApplyKneelFloor();
                if (runInstalled || advanceInstalled) SnapArms();
                return;
            }
            // the animator did not pose him this frame (pause, culling): last frame's result is still on the bones
            if (wrote && hips.localPosition == writtenPosition && hips.localRotation == writtenRotation) return;
            // the clips stand on the floor the standing idle stands on (measured by the menu for this man)
            if (graphOn && fit != null && fit.floorOffset != 0f)
                hips.position += model.up * (fit.floorOffset * model.lossyScale.y);
            // easing out of a clip into the controller's kneel: lower that pose first, as when no clip plays
            else if (!graphOn) ApplyKneelFloor();
            if (blending)
            {
                blendTime += Time.deltaTime;
                float w = Mathf.SmoothStep(0f, 1f, blendTime / blendLength);
                for (int i = 0; i < bones.Length; i++) bones[i].localRotation = Quaternion.Slerp(from[i], bones[i].localRotation, w);
                hips.localPosition = Vector3.Lerp(hipsFrom, hips.localPosition, w);
                graphWeight = blendingIn ? w : 1f - w;
                if (blendTime >= blendLength) { blending = false; graphWeight = blendingIn ? 1f : 0f; }
            }
            else graphWeight = graphOn ? 1f : 0f;
            SnapArms();
            wrote = graphOn || blending;
            writtenPosition = hips.localPosition;
            writtenRotation = hips.localRotation;
        }

        // ---- the swapped kneel (Idle_Trench / Fire_Crouch): its clip stands on the floor the standing idle stands on ----
        // The new kneeling clips play about 13 cm high on these bodies, as the trench clips do (fit.floorOffset); the
        // menu measured the drop for each man. In proportion to how much of the kneeling states is on screen.
        bool kneelWrote;
        Vector3 kneelWritten;

        float KneelWeight()
        {
            if (!kneelInstalled || !animator.isActiveAndEnabled) return 0f;
            int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            float from = current == IdleTrenchId || current == FireCrouchId ? 1f : 0f;
            if (!animator.IsInTransition(0)) return from;
            int next = animator.GetNextAnimatorStateInfo(0).shortNameHash;
            float to = next == IdleTrenchId || next == FireCrouchId ? 1f : 0f;
            return Mathf.Lerp(from, to, Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime));
        }

        // ParkCombatFootPlacement: how far this frame's kneeling pose will be lowered after its IK pass (world m, <= 0)
        public float KneelDropNow => bound && fit != null && !graph.IsValid() ? fit.kneelFloorOffset * KneelWeight() * model.lossyScale.y : 0f;

        void ApplyKneelFloor()
        {
            float w = fit != null && fit.kneelFloorOffset != 0f ? KneelWeight() : 0f;
            if (w <= 0f) { kneelWrote = false; return; }
            // the animator did not pose him this frame (pause, culling): he was lowered already
            if (kneelWrote && hips.localPosition == kneelWritten) return;
            hips.position += model.up * (fit.kneelFloorOffset * w * model.lossyScale.y);
            kneelWrote = true;
            kneelWritten = hips.localPosition;
        }

        void SnapArms()
        {
            Snap(left); Snap(right);
            armsSnapped = true;
        }

        static void Snap(Arm arm)
        {
            for (int i = 0; i < arm.chain.Length; i++) if (arm.chain[i] != null) arm.clip[i] = arm.chain[i].localRotation;
        }

        // ---- the rifle: called by AllyCombatant.LateUpdate after its own carry / aim ------------------------------
        // weight < 1 (going in or out): what AllyCombatant did is kept in proportion.
        internal void HoldRifle(Transform weapon)
        {
            float w = graphWeight;
            if (w <= 0f || weapon == null || !armsSnapped) return;
            if (!TrenchTrack(out var position, out var rotation, out float gripLeft, out float gripRight)) return;
            Hold(weapon, w, position, rotation, gripLeft, gripRight);
        }

        // Hill assault: the new run clips carry the rifle in both hands. run / advance: how much of each moving state
        // is on screen (AllyCombatant.StateWeight); the standing port arms placed before this keeps the rest.
        internal void HoldMovingRifle(Transform weapon, float run, float advance)
        {
            if (weapon == null || !armsSnapped) return;
            if (!runInstalled) run = 0f;
            if (!advanceInstalled) advance = 0f;
            float w = Mathf.Clamp01(run + advance);
            if (w <= 0f) return;
            Vector3 runPosition = Vector3.zero, advancePosition = Vector3.zero;
            Quaternion runRotation = Quaternion.identity, advanceRotation = Quaternion.identity;
            bool hasRun = run > 0f && StateTrack(AllyClipRole.Run, RunRifleId, out runPosition, out runRotation);
            bool hasAdvance = advance > 0f && StateTrack(AllyClipRole.CrouchRun, CrouchRunRifleId, out advancePosition, out advanceRotation);
            Vector3 position; Quaternion rotation;
            if (hasRun && hasAdvance)
            {
                float share = run / (run + advance);
                position = Vector3.Lerp(advancePosition, runPosition, share);
                rotation = Quaternion.Slerp(advanceRotation, runRotation, share);
            }
            else if (hasRun) { position = runPosition; rotation = runRotation; w = Mathf.Clamp01(run); }
            else if (hasAdvance) { position = advancePosition; rotation = advanceRotation; w = Mathf.Clamp01(advance); }
            else return;
            Hold(weapon, w, position, rotation, 1f, 1f);
        }

        void Hold(Transform weapon, float w, Vector3 position, Quaternion rotation, float gripLeft, float gripRight)
        {
            bool mix = w < 0.999f;
            if (mix)
            {
                carryPosition = weapon.position; carryRotation = weapon.rotation;
                Save(left); Save(right);
            }
            Restore(left); Restore(right);
            if (mix)
            {
                position = Vector3.Lerp(carryPosition, position, w);
                rotation = Quaternion.Slerp(carryRotation, rotation, w);
            }
            weapon.SetPositionAndRotation(position, rotation);
            Contact(right, gripRight, Data.rightWrist, weapon);
            Contact(left, gripLeft, Data.leftWrist, weapon);
            if (mix) { Mix(left, w); Mix(right, w); }
        }

        static void Save(Arm arm)
        {
            for (int i = 0; i < arm.chain.Length; i++) if (arm.chain[i] != null) arm.carry[i] = arm.chain[i].localRotation;
        }

        static void Restore(Arm arm)
        {
            for (int i = 0; i < arm.chain.Length; i++) if (arm.chain[i] != null) arm.chain[i].localRotation = arm.clip[i];
        }

        static void Mix(Arm arm, float w)
        {
            for (int i = 0; i < arm.chain.Length; i++)
                if (arm.chain[i] != null) arm.chain[i].localRotation = Quaternion.Slerp(arm.carry[i], arm.chain[i].localRotation, w);
        }

        // A hand that holds the rifle in the clip (contact 1) goes back onto its grip with the clip's own wrist turn;
        // a hand that has let go (0) keeps the clip pose; in between, in proportion.
        static void Contact(Arm arm, float contact, Vector3 wrist, Transform weapon)
        {
            if (contact <= 0.001f || arm.upper == null || arm.fore == null || arm.hand == null) return;
            var a = arm.upper.localRotation; var b = arm.fore.localRotation; var c = arm.hand.localRotation;
            AllyRiflePose.SolveArm(arm.upper, arm.fore, arm.hand, weapon.TransformPoint(wrist), arm.hand.rotation);
            if (contact >= 0.999f) return;
            arm.upper.localRotation = Quaternion.Slerp(a, arm.upper.localRotation, contact);
            arm.fore.localRotation = Quaternion.Slerp(b, arm.fore.localRotation, contact);
            arm.hand.localRotation = Quaternion.Slerp(c, arm.hand.localRotation, contact);
        }

        // Where the clip(s) now playing in the graph carry the rifle; frozen at the last frame while easing out.
        bool TrenchTrack(out Vector3 position, out Quaternion rotation, out float gripLeft, out float gripRight)
        {
            if (graph.IsValid() && current.IsValid() && currentClip != null)
            {
                ClipPose(currentClip, (float)current.GetTime(), out position, out rotation, out gripLeft, out gripRight);
                if (previous.IsValid() && previousClip != null)
                {
                    ClipPose(previousClip, (float)previous.GetTime(), out var p, out var r, out float l, out float g);
                    float share = CurrentShare;
                    position = Vector3.Lerp(p, position, share);
                    rotation = Quaternion.Slerp(r, rotation, share);
                    gripLeft = Mathf.Lerp(l, gripLeft, share);
                    gripRight = Mathf.Lerp(g, gripRight, share);
                }
                return true;
            }
            if (frozenValid && anchor != null)
            {
                position = anchor.TransformPoint(frozenLocalPosition);
                rotation = anchor.rotation * frozenLocalRotation;
                gripLeft = frozenGripLeft; gripRight = frozenGripRight;
                return true;
            }
            position = Vector3.zero; rotation = Quaternion.identity; gripLeft = gripRight = 0f;
            return false;
        }

        // The rifle stays where it was on the body while the held pose eases back to the controller's.
        void FreezeTrack()
        {
            frozenValid = false;
            if (anchor == null || !TrenchTrack(out var position, out var rotation, out frozenGripLeft, out frozenGripRight)) return;
            frozenLocalPosition = anchor.InverseTransformPoint(position);
            frozenLocalRotation = Quaternion.Inverse(anchor.rotation) * rotation;
            frozenValid = true;
        }

        bool StateTrack(AllyClipRole role, int state, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            var clip = Data.Find(role);
            if (clip == null || clip.clip == null) return false;
            float normalized;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == state) normalized = info.normalizedTime;
            else if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == state)
                normalized = animator.GetNextAnimatorStateInfo(0).normalizedTime;
            else return false;
            ClipPose(clip, Mathf.Repeat(normalized, 1f) * clip.clip.length, out position, out rotation, out _, out _);
            return true;
        }

        // The rifle of one clip at one time, in the world: from the bake for this man's chest when there is one
        // (it then rides any shift of his body), else straight from the JSON about his animator's origin.
        void ClipPose(AllyRifleTracks.Clip clip, float time, out Vector3 position, out Quaternion rotation, out float gripLeft, out float gripRight)
        {
            var frame = AllyRifleTracks.Sample(clip.frames, clip.fps, time, clip.loop);
            gripLeft = frame.gripLeft; gripRight = frame.gripRight;
            var baked = AllyRifleTracks.BakedFor(fit, clip.name);
            if (baked != null && anchor != null)
            {
                AllyRifleTracks.Sample(baked, clip.fps, time, clip.loop, out var local, out var localRotation);
                position = anchor.TransformPoint(local);
                rotation = anchor.rotation * localRotation;
                return;
            }
            position = model.position + model.rotation * frame.position;
            rotation = model.rotation * frame.rotation;
        }

        // ---- the hill assault run: only the clips of two controller states are swapped, at run time -------------
        // (2026-11-18 저녁) the kneeling wait and the kneeling shot as well: Idle_Trench / Fire_Crouch keep their names,
        // transitions and timing; only the clip under them changes. The arms and the rifle stay with AllyRiflePose.Apply.
        void InstallRunClips()
        {
            var run = UseRigifyRun ? Data.Find(AllyClipRole.Run) : null;
            var advance = UseRigifyRun ? Data.Find(AllyClipRole.CrouchRun) : null;
            var kneel = UseRigifyKneel ? Data.Find(AllyClipRole.KneelIdle) : null;
            var kneelFire = UseRigifyKneel ? Data.Find(AllyClipRole.KneelFire) : null;
            if (run == null && advance == null && kneel == null && kneelFire == null) return;
            var controller = animator.runtimeAnimatorController;
            if (controller == null) return;
            RuntimeAnimatorController basis = controller;
            var existing = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            if (controller is AnimatorOverrideController wrapper)
            {
                basis = wrapper.runtimeAnimatorController;
                if (basis == null || basis is AnimatorOverrideController) return; // two levels of overrides: leave it as it is
                wrapper.GetOverrides(existing);
            }
            var overrides = new AnimatorOverrideController(basis) { name = controller.name + " + Rigify" };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                var original = pairs[i].Key;
                AnimationClip replacement = null;
                foreach (var e in existing) if (e.Key == original) replacement = e.Value;
                if (original != null && run != null && original.name == Data.runSourceClip) { replacement = run.clip; runInstalled = true; }
                else if (original != null && advance != null && original.name == Data.advanceSourceClip) { replacement = advance.clip; advanceInstalled = true; }
                else if (original != null && kneel != null && original.name == Data.kneelSourceClip) { replacement = kneel.clip; kneelInstalled = true; }
                else if (original != null && kneelFire != null && original.name == Data.kneelFireSourceClip) { replacement = kneelFire.clip; kneelInstalled = true; }
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
            }
            if (!runInstalled && !advanceInstalled && !kneelInstalled) { Destroy(overrides); return; }
            overrides.ApplyOverrides(pairs);
            animator.runtimeAnimatorController = overrides;
        }
    }
}
