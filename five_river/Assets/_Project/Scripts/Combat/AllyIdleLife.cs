using System.Collections.Generic;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.Narrative;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FiveRivers.Combat
{
    // 참호 아군의 대기 동작 (2026-11-18). 클립이 대기 하나뿐이라 싸우지 않을 때 거의 움직이지 않았다.
    // 애니메이터가 잡은 자세 위에 뼈대를 조금씩 움직인다: 숨쉬기(항상), 그리고 6~14초마다 한 가지씩
    // 들판 살피기 / 흉벽 너머 내다보기 / 소총 내려다보기 / 옆 사람 보기. 발과 엉덩이는 건드리지 않는다.
    // 교전 · 전진 · 이동 · 돌아서 말하기 · 연출 컷 · 고개 숙임(HeadBow) · 쓰러진 뒤에는 하지 않는다.
    // 표시 전용: 판정 · 진행 · 수치와 무관하다. 난수는 사람마다 고정 씨앗.
    // 몸통은 AllyCombatant 가 소총과 팔을 맞추기 전에(-45), 머리는 그 뒤에(AllyIdleLifeHead, 150) 움직인다.
    //
    // 대기 동작 클립 (2026-11-18, VARCO): 위의 뼈대 움직임만으로는 너무 약해 거의 서 있는 것처럼 보였다.
    // 같은 조건 · 같은 간격으로, 고를 때마다 일부는 실제 동작 클립에서 구운 움직임(Data/AllyIdleClipSet)을 얹는다.
    // 클립을 통째로 섞지 않는다: 받은 클립은 선 방향 · 발 간격 · 소총 든 모양이 지금의 대기 자세와 달라, 그대로
    // 섞으면 몸이 옆으로 돌고 뒤로 젖혀졌다(찍어서 확인). 클립 첫 프레임에 대한 골반 · 척추 · 목 · 머리의 변화량만
    // 지금 자세에 더한다. 컨트롤러(AC_ROK)와 대기 자세는 그대로이고, 반복 클립이라 변화량은 0에서 시작해 0으로 끝난다.
    // 두 발은 애니메이터가 둔 자리에 붙여 두고(골반이 움직인 만큼 다리를 다시 푼다), 몸 기준으로 놓이는 소총은
    // 어깨가 달린 가슴뼈가 움직인 만큼 그대로 따라가게 한다(AllyRiflePose.Ride*: 덜 따라가면 왼손이 총에서 6~10 cm
    // 벗어났다). 어깨뼈 변화량은 쓰지 않는다(팔은 소총에 맞춰 푼다). 클립이 도는 동안 절차적 고개 동작은 쉬고 숨쉬기만 남는다.
    // 교전 · 전진이 시작되면 0.12초 만에 빠진다.
    [DefaultExecutionOrder(-45), DisallowMultipleComponent]
    public sealed class AllyIdleLife : MonoBehaviour
    {
        // 화면을 보고 맞춘 값 (사람이 실제로 하는 범위 안: 고개 좌우 35도, 숙임 25도 이내)
        public static float MaxYaw = 32f, MaxDown = 20f, MaxUp = 9f, MaxStraighten = 5f;
        public static float BreathDegrees = 0.7f;
        public static Vector2 RestSeconds = new Vector2(6f, 14f);
        public static bool Active = true;
        // how far the body may stand off the field and still idle; how far the head may end up off the chest
        public static float MaxOffFront = 50f, MaxHeadOffChest = 42f;
        // idle clips: how often a clip is chosen over a procedural look, blend in / out / out when a fight starts,
        // how far the carried rifle follows the chest
        public static float ClipShare = 0.65f, ClipIn = 0.45f, ClipOut = 0.45f, ClipOutBroken = 0.3f, ClipOutFast = 0.12f;
        public static float RifleFollow = 1f, MaxHipDrop = 0.15f, ClipHeadLimit = 68f;

        enum Act { None, Scan, Peek, CheckRifle, Glance, Clip }
        struct Key { public float time, yaw, pitch, chest, straighten; }

        struct Bone
        {
            public Transform t;
            Quaternion applied, original;
            bool has;
            // The animator may skip a frame (pause, culling): take last frame's offset out first so it never piles up.
            public void Turn(Quaternion worldDelta)
            {
                if (t == null) return;
                if (has && t.localRotation == applied) t.localRotation = original;
                original = t.localRotation;
                t.rotation = worldDelta * t.rotation;
                applied = t.localRotation;
                has = true;
            }
        }

        static readonly List<AllyIdleLife> everyone = new List<AllyIdleLife>();
        static float lastStartAt = -100f;
        static readonly int IdleStandId = Animator.StringToHash("Idle_Stand");
        static readonly int IdleTrenchId = Animator.StringToHash("Idle_Trench");

        AllyCombatant ally;
        Animator animator;
        FlowDirector director;
        PlayerController player;
        Transform body, leftShoulder, rightShoulder, leftHand, rightHand, headBone;
        Bone spine, chest, neck, head;
        System.Random random;
        float seed, breathPeriod, breathPhase;

        Act act = Act.None, lastAct = Act.None;
        readonly List<Key> keys = new List<Key>();
        float actTime, actEnd, nextAt, settle = 0.55f;
        float yaw, pitch, chestYaw, straighten;          // degrees, eased toward the current key
        float yawVel, pitchVel, chestVel, straightenVel;
        float weight, breathWeight = 1f;
        Vector3 home, lastPosition, front;
        LevelPoints points;
        bool frontKnown;
        Quaternion lastRotation;
        float stillFor, checkAt;
        bool headBusy;

        // ---- idle clip layer ----
        static AllyIdleClipSet clipSet;
        static bool clipSetLoaded;
        List<AllyIdleClipSet.Track> standingTracks, kneelingTracks;
        AllyIdleClipSet.Track track, lastTrack;
        float clipTime, clipWeight, clipApplied;
        bool clipLeaving;
        float clipLeaveSeconds;
        Transform hips, chestBone, upperChestBone;
        Transform leftUpperLeg, leftLowerLeg, leftFoot, rightUpperLeg, rightLowerLeg, rightFoot;
        bool clipWrote, headTotalPending;
        Quaternion hipsWritten, headAfterBody, headTotalNow;
        Vector3 hipsWrittenPosition;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string DebugState => $"{act} w{weight:0.00} yaw{yaw:0} pitch{pitch:0} up{straighten:0.0}" + (track != null ? $" {track.clip} {clipApplied:0.00}" : "");
        public string DebugClip => track != null ? track.clip : "";
        public float DebugClipWeight => clipApplied;
        public bool DebugCanIdle => CanIdle();
        // Play one named clip now, whatever the schedule says (the usual conditions still end it).
        public bool DebugPlay(string clipName)
        {
            Tracks(false);
            foreach (var list in new[] { standingTracks, kneelingTracks })
                foreach (var candidate in list)
                    if (candidate.clip == clipName)
                    {
                        StartClip(candidate);
                        act = Act.Clip; actTime = 0f; keys.Clear(); actEnd = float.MaxValue;
                        return true;
                    }
            return false;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { everyone.Clear(); lastStartAt = -100f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachAll();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachAll();

        // Idempotent: one per ally, on the object that carries his animator.
        public static void AttachAll()
        {
            foreach (var combatant in FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var animator = combatant.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman || animator.GetComponent<AllyIdleLife>() != null) continue;
                var life = animator.gameObject.AddComponent<AllyIdleLife>();
                life.Bind(combatant, animator);
                animator.gameObject.AddComponent<AllyIdleLifeHead>().Bind(life);
            }
        }

        void Bind(AllyCombatant combatant, Animator source)
        {
            ally = combatant; animator = source; body = combatant.transform;
            spine.t = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest.t = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest.t == null) chest.t = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            neck.t = animator.GetBoneTransform(HumanBodyBones.Neck);
            head.t = headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            upperChestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            int hash = 17;
            foreach (char c in combatant.name) hash = unchecked(hash * 31 + c); // stable across runs, unlike string.GetHashCode
            random = new System.Random(hash);
            seed = Range(0f, 100f);
            breathPeriod = Range(3.4f, 4.7f);
            breathPhase = Range(0f, Mathf.PI * 2f);
            nextAt = Time.time + Range(2.5f, 9f);
            home = lastPosition = body.position;
            lastRotation = body.rotation;
        }

        void OnEnable() { if (!everyone.Contains(this)) everyone.Add(this); }
        void OnDisable() { everyone.Remove(this); EndClip(); }
        void OnDestroy() => EndClip();

        float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        // ---- when he may move ------------------------------------------------------------------------------
        bool Alive => ally != null && ally.isActiveAndEnabled && animator != null && animator.isActiveAndEnabled;

        // Rigify clips (2026-11-18): while AllyClipPlayer has him (the low wait in the trench and its moves) this layer
        // rests entirely, breathing included: one set of idle moves at a time.
        AllyClipPlayer clips;
        bool ClipsBusy => clips != null && clips.Busy;

        bool CanIdle()
        {
            if (!Active || !Alive || ally.Engaged || ally.Advancing || headBusy || ClipsBusy) return false;
            if (CutsceneRig.Running > 0) return false;
            if (director != null && director.Flow != null)
            {
                var section = director.Flow.Section;
                // the hill assault and the dawn on the crest have their own carry, walking and kneeling
                if (section == FlowSection.HillAssault || section == FlowSection.Dawn || section == FlowSection.Result) return false;
            }
            if (animator.IsInTransition(0)) return false;
            int state = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (state != IdleStandId && state != IdleTrenchId) return false;
            // walking to the body, turning to speak, standing away from his post: leave him to that
            if (stillFor < 0.6f) return false;
            var away = body.position - home; away.y = 0f;
            if (away.sqrMagnitude > 0.6f * 0.6f) return false;
            // turned round to speak to the player (the squad leader, Mansu): not while he faces away from the field
            if (!frontKnown)
            {
                if (points == null) points = FindFirstObjectByType<LevelPoints>();
                front = AllyCombatant.ApproachDirection(home, points, body.forward);
                frontKnown = true;
            }
            var facing = body.forward; facing.y = 0f;
            return Vector3.Angle(facing, front) < MaxOffFront;
        }

        void LateUpdate()
        {
            if (body == null) return;
            float dt = Time.deltaTime;
            if (director == null) director = FindFirstObjectByType<FlowDirector>();
            if (dt > 0f)
            {
                float moved = (body.position - lastPosition).magnitude / dt;
                float turned = Quaternion.Angle(body.rotation, lastRotation) / dt;
                stillFor = moved > 0.06f || turned > 12f ? 0f : stillFor + dt;
                lastPosition = body.position; lastRotation = body.rotation;
            }
            if (Time.time >= checkAt)
            {
                checkAt = Time.time + 0.4f;
                if (clips == null && animator != null) clips = animator.GetComponent<AllyClipPlayer>();
                var bow = animator != null ? animator.GetComponent<HeadBow>() : null;
                headBusy = (bow != null && bow.enabled)
                    || (animator != null && (animator.GetComponent<FallenPose>() != null || animator.GetComponent<PoseBlendIn>() != null));
                // a new run or a retry puts him back on his post; a director may also give him a new one
                if (director != null && director.Flow != null && director.Flow.Section == FlowSection.Tutorial && director.Flow.SectionTime < 1.0)
                    home = body.position;
            }

            bool can = CanIdle();
            TickClip(can, dt);
            weight = Mathf.MoveTowards(weight, can ? 1f : 0f, dt / (can ? 0.9f : 0.28f));
            breathWeight = Mathf.MoveTowards(breathWeight, !Alive || ClipsBusy ? 0f : ally.Engaged || ally.Advancing ? 0.45f : 1f, dt / 0.6f);
            if (!can)
            {
                if (act != Act.None) { act = Act.None; nextAt = Time.time + Range(2.5f, 6f); }
                else if (nextAt < Time.time + 1.5f) nextAt = Time.time + Range(1.5f, 4f);
            }
            else if (act == Act.None && track == null && Time.time >= nextAt && weight > 0.95f)
            {
                // never two men starting together
                if (Time.time - lastStartAt < 1.6f) nextAt = Time.time + Range(1.2f, 2.8f);
                else Begin();
            }

            float targetYaw = 0f, targetPitch = 0f, targetChest = 0f, targetStraighten = 0f;
            if (act != Act.None)
            {
                actTime += dt;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i].time > actTime) break;
                    targetYaw = keys[i].yaw; targetPitch = keys[i].pitch; targetChest = keys[i].chest; targetStraighten = keys[i].straighten;
                }
                if (actTime >= actEnd)
                {
                    lastAct = act; act = Act.None;
                    nextAt = Time.time + Range(RestSeconds.x, RestSeconds.y);
                }
            }
            float time = act == Act.None ? 0.5f : settle;
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, time, 70f, dt);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, time, 50f, dt);
            chestYaw = Mathf.SmoothDamp(chestYaw, targetChest, ref chestVel, time * 1.2f, 20f, dt);
            straighten = Mathf.SmoothDamp(straighten, targetStraighten, ref straightenVel, time * 1.3f, 12f, dt);

            ApplyBody();
            if (headBone != null) headAfterBody = headBone.rotation;
        }

        // ---- the acts ----------------------------------------------------------------------------------------
        void Begin()
        {
            // a real clip over the idle pose, most of the time; the procedural looks fill in between
            var chosen = PickTrack();
            if (chosen != null && random.NextDouble() < ClipShare)
            {
                StartClip(chosen);
                act = Act.Clip; actTime = 0f; keys.Clear(); actEnd = float.MaxValue;
                lastStartAt = Time.time;
                return;
            }
            var options = new List<Act> { Act.Scan, Act.Scan, Act.Peek, Act.CheckRifle, Act.Glance };
            options.RemoveAll(a => a == lastAct);
            foreach (var other in everyone)
                if (other != this && other.act != Act.None) options.RemoveAll(a => a == other.act);
            float glanceYaw = 0f;
            if (options.Contains(Act.Glance) && !FindGlance(out glanceYaw)) options.RemoveAll(a => a == Act.Glance);
            if (options.Count == 0) { nextAt = Time.time + Range(1.5f, 3.5f); return; }
            act = options[random.Next(options.Count)];
            actTime = 0f; keys.Clear();
            lastStartAt = Time.time;
            switch (act)
            {
                case Act.Scan:
                {
                    // across the field, one side then the other, sometimes one side only
                    float side = random.Next(2) == 0 ? -1f : 1f;
                    float first = side * Range(15f, MaxYaw - 4f);
                    float holdA = Range(1.5f, 2.8f);
                    settle = Range(0.50f, 0.68f);
                    float t = 0f;
                    Add(t, first, -1.5f, first * 0.16f, 0f);
                    t += 1.5f + holdA;
                    if (random.NextDouble() < 0.72)
                    {
                        float second = -side * Range(10f, MaxYaw - 8f);
                        Add(t, second, -1f, second * 0.16f, 0f);
                        t += 1.9f + Range(1.3f, 2.4f);
                    }
                    Add(t, 0f, 0f, 0f, 0f);
                    actEnd = t + 1.5f;
                    break;
                }
                case Act.Peek:
                {
                    // straightens to look over the parapet, then settles again
                    float up = Range(MaxStraighten * 0.65f, MaxStraighten);
                    float turn = Range(-7f, 7f);
                    settle = Range(0.55f, 0.72f);
                    Add(0f, turn, -Range(MaxUp * 0.5f, MaxUp), 0f, up);
                    float hold = Range(1.8f, 3.0f);
                    Add(1.3f + hold * 0.5f, -turn * 0.8f, -MaxUp * 0.45f, 0f, up);
                    Add(1.3f + hold, 0f, 0f, 0f, 0f);
                    actEnd = 1.3f + hold + 1.7f;
                    break;
                }
                case Act.CheckRifle:
                {
                    settle = Range(0.42f, 0.55f);
                    Add(0f, Mathf.Clamp(RifleYaw(), -16f, 16f), Range(MaxDown * 0.7f, MaxDown), 0f, 0f);
                    float hold = Range(1.3f, 2.3f);
                    Add(1.1f + hold, 0f, 0f, 0f, 0f);
                    actEnd = 1.1f + hold + 1.4f;
                    break;
                }
                case Act.Glance:
                {
                    settle = Range(0.40f, 0.52f);
                    float to = Mathf.Clamp(glanceYaw, -MaxYaw, MaxYaw);
                    Add(0f, to, 1.5f, to * 0.2f, 0f);
                    float hold = Range(1.1f, 2.0f);
                    Add(1.2f + hold, 0f, 0f, 0f, 0f);
                    actEnd = 1.2f + hold + 1.4f;
                    break;
                }
            }
        }

        void Add(float time, float keyYaw, float keyPitch, float keyChest, float keyStraighten) =>
            keys.Add(new Key { time = time, yaw = keyYaw, pitch = keyPitch, chest = keyChest, straighten = keyStraighten });

        Vector3 Facing()
        {
            var forward = headBone != null ? headBone.forward : body.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.01f ? forward.normalized : body.forward;
        }

        float YawTo(Vector3 point)
        {
            var to = point - (headBone != null ? headBone.position : body.position); to.y = 0f;
            return to.sqrMagnitude < 0.01f ? 0f : Vector3.SignedAngle(Facing(), to, Vector3.up);
        }

        float RifleYaw() => leftHand != null && rightHand != null ? YawTo((leftHand.position + rightHand.position) * 0.5f) : 0f;

        // the nearest man beside him, or the player if he is close; only if it is a turn of the head, not of the body
        bool FindGlance(out float angle)
        {
            angle = 0f;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            float best = 11f * 11f; bool found = false;
            foreach (var other in everyone)
                if (other != this && other.Alive && Consider(other.body.position, ref best, ref angle)) found = true;
            if (player != null && Consider(player.transform.position, ref best, ref angle)) found = true;
            return found;
        }

        bool Consider(Vector3 point, ref float best, ref float angle)
        {
            float d = (point - body.position).sqrMagnitude;
            if (d < 1.2f * 1.2f || d > best) return false;
            float a = YawTo(point + Vector3.up * 1.4f);
            if (Mathf.Abs(a) < 12f || Mathf.Abs(a) > 78f) return false;
            best = d; angle = a;
            return true;
        }

        // ---- idle clips ------------------------------------------------------------------------------------------
        // The tracks baked for his avatar, for the pose he is in.
        List<AllyIdleClipSet.Track> Tracks(bool kneeling)
        {
            if (standingTracks == null)
            {
                standingTracks = new List<AllyIdleClipSet.Track>();
                kneelingTracks = new List<AllyIdleClipSet.Track>();
                if (!clipSetLoaded) { clipSet = Resources.Load<AllyIdleClipSet>(AllyIdleClipSet.ResourceName); clipSetLoaded = true; }
                string avatar = animator != null && animator.avatar != null ? animator.avatar.name : "";
                if (clipSet != null && clipSet.tracks != null)
                    foreach (var candidate in clipSet.tracks)
                        if (candidate != null && candidate.avatar == avatar && candidate.Count > 1 && candidate.length > 1f)
                            (candidate.kneeling ? kneelingTracks : standingTracks).Add(candidate);
            }
            return kneeling ? kneelingTracks : standingTracks;
        }

        // One that fits the pose he is in, that he did not just do and that nobody else is doing now.
        AllyIdleClipSet.Track PickTrack()
        {
            if (hips == null || leftFoot == null || rightFoot == null || leftUpperLeg == null || rightUpperLeg == null) return null;
            var list = Tracks(animator.GetCurrentAnimatorStateInfo(0).shortNameHash == IdleTrenchId);
            if (list.Count == 0) return null;
            var options = new List<AllyIdleClipSet.Track>();
            foreach (var candidate in list)
            {
                if (lastTrack != null && candidate.clip == lastTrack.clip && list.Count > 1) continue;
                bool taken = false;
                foreach (var other in everyone) if (other != this && other.track != null && other.track.clip == candidate.clip) taken = true;
                if (!taken) options.Add(candidate);
            }
            return options.Count > 0 ? options[random.Next(options.Count)] : null;
        }

        void StartClip(AllyIdleClipSet.Track chosen)
        {
            EndClip();
            track = chosen;
            clipTime = clipWeight = clipApplied = 0f;
            clipLeaving = false;
        }

        void EndClip()
        {
            if (track != null) lastTrack = track;
            track = null;
            clipWeight = clipApplied = 0f;
            clipWrote = headTotalPending = false;
            var rifle = ally != null ? ally.RiflePose : null;
            if (rifle != null) { rifle.RideTurn = Quaternion.identity; rifle.RideShift = Vector3.zero; }
        }

        static void AddTurn(Transform bone, Quaternion[] turns, int i, int j, float f, float w)
        {
            if (bone == null || turns == null || turns.Length <= j) return;
            bone.localRotation = bone.localRotation * Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Slerp(turns[i], turns[j], f), w);
        }

        // Runs first in LateUpdate: the bones still hold exactly what the animator wrote this frame.
        void TickClip(bool can, float dt)
        {
            if (track == null) { headTotalPending = false; return; }
            if (!Alive) { EndClip(); if (act == Act.Clip) act = Act.None; return; }
            // The animator did not pose him this frame (pause): last frame's result is still on the bones.
            if (clipWrote && hips.localRotation == hipsWritten && hips.localPosition == hipsWrittenPosition) return;

            if (!can)
            {
                if (!clipLeaving || clipLeaveSeconds > ClipOutBroken) { clipLeaving = true; clipLeaveSeconds = ClipOutBroken; }
                if (ally.Engaged || ally.Advancing) clipLeaveSeconds = ClipOutFast;
            }
            else if (!clipLeaving && clipTime >= track.length - ClipOut)
            {
                clipLeaving = true; clipLeaveSeconds = ClipOut;
            }
            clipWeight = Mathf.MoveTowards(clipWeight, clipLeaving ? 0f : 1f, dt / Mathf.Max(0.02f, clipLeaving ? clipLeaveSeconds : ClipIn));
            clipTime = Mathf.Min(clipTime + dt, track.length);
            if (clipLeaving && clipWeight <= 0f)
            {
                bool finished = act == Act.Clip;
                EndClip();
                if (finished) { lastAct = Act.Clip; act = Act.None; nextAt = Time.time + Range(RestSeconds.x, RestSeconds.y); }
                return;
            }
            clipApplied = Mathf.SmoothStep(0f, 1f, clipWeight);
            float w = clipApplied * Mathf.Max(0f, track.gain);

            float at = clipTime * track.rate;
            int last = track.Count - 1, i = Mathf.Min((int)at, last), j = Mathf.Min(i + 1, last);
            float f = Mathf.Clamp01(at - i);

            // where the animator has him now: the feet stay there, and the rifle was fitted to this chest
            Vector3 leftPin = leftFoot.position, rightPin = rightFoot.position;
            Quaternion leftPinRotation = leftFoot.rotation, rightPinRotation = rightFoot.rotation;
            var chestNow = upperChestBone != null ? upperChestBone : chest.t; // the bone the shoulders hang from
            Vector3 chestBefore = chestNow != null ? chestNow.position : Vector3.zero;
            Quaternion chestBeforeRotation = chestNow != null ? chestNow.rotation : Quaternion.identity;

            // the pelvis shifts in his own left-right / front-back, whichever way the idle pose has him turned
            var right = rightUpperLeg.position - leftUpperLeg.position; right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : body.right;
            var forward = Vector3.Cross(right, Vector3.up);
            var move = Vector3.Lerp(track.hipsMove[i], track.hipsMove[j], f) * (animator.transform.lossyScale.y * w);
            hips.position += right * move.x + Vector3.up * move.y + forward * move.z;
            AddTurn(hips, track.hips, i, j, f, w);
            AddTurn(spine.t, track.spine, i, j, f, w);
            AddTurn(chestBone, track.chest, i, j, f, w);
            AddTurn(upperChestBone, track.upperChest, i, j, f, w);
            AddTurn(neck.t, track.neck, i, j, f, w);
            AddTurn(head.t, track.head, i, j, f, w);

            float drop = Mathf.Max(HipDrop(leftUpperLeg, leftLowerLeg, leftFoot, leftPin), HipDrop(rightUpperLeg, rightLowerLeg, rightFoot, rightPin));
            if (drop > 0f) hips.position -= Vector3.up * drop;
            SolveLeg(leftUpperLeg, leftLowerLeg, leftFoot, leftPin, leftPinRotation, 1f, body.forward);
            SolveLeg(rightUpperLeg, rightLowerLeg, rightFoot, rightPin, rightPinRotation, 1f, body.forward);
            clipWrote = true; hipsWritten = hips.localRotation; hipsWrittenPosition = hips.localPosition;

            var rifle = ally.RiflePose;
            if (rifle != null && chestNow != null)
            {
                rifle.RidePivot = chestBefore;
                rifle.RideTurn = Quaternion.Slerp(Quaternion.identity, chestNow.rotation * Quaternion.Inverse(chestBeforeRotation), RifleFollow);
                rifle.RideShift = (chestNow.position - chestBefore) * RifleFollow;
            }
            // AllyCombatant may aim the head at the field after this (tutorial, the standing-ready fit): ApplyHead then
            // puts the clip's whole turn of the head back on top of that
            headTotalNow = track.headTotal != null && track.headTotal.Length > j
                ? Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Slerp(track.headTotal[i], track.headTotal[j], f), w) : Quaternion.identity;
            headTotalPending = true;
        }

        // How far the hips must come down for this leg to reach its pinned foot.
        static float HipDrop(Transform upper, Transform lower, Transform foot, Vector3 pin)
        {
            if (upper == null || lower == null || foot == null) return 0f;
            float reach = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, foot.position) - 0.004f;
            var d = upper.position - pin;
            float flat = d.x * d.x + d.z * d.z;
            if (flat >= reach * reach) return 0f;
            return Mathf.Clamp(d.y - Mathf.Sqrt(reach * reach - flat), 0f, MaxHipDrop);
        }

        // Two-bone solve that keeps the knee bending the way the pose has it.
        static void SolveLeg(Transform upper, Transform lower, Transform foot, Vector3 pin, Quaternion pinRotation, float pinWeight, Vector3 forward)
        {
            if (upper == null || lower == null || foot == null || pinWeight <= 0f) return;
            Vector3 a = upper.position, b = lower.position, c = foot.position;
            var target = Vector3.Lerp(c, pin, pinWeight);
            float first = Vector3.Distance(a, b), second = Vector3.Distance(b, c);
            var delta = target - a;
            if (first < 0.001f || second < 0.001f || delta.sqrMagnitude < 0.000001f) return;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(first - second) + 0.0001f, first + second - 0.0001f);
            var direction = delta.normalized;
            var legAxis = (c - a).normalized;
            var bend = Quaternion.FromToRotation(legAxis, direction) * Vector3.ProjectOnPlane(b - a, legAxis);
            if (bend.sqrMagnitude < 0.000004f) bend = Vector3.ProjectOnPlane(forward, direction);
            if (bend.sqrMagnitude < 0.000001f) return;
            bend.Normalize();
            float along = (first * first + distance * distance - second * second) / (2f * distance);
            var knee = a + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, first * first - along * along));
            upper.rotation = Quaternion.FromToRotation(b - a, knee - a) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(foot.position - lower.position, target - lower.position) * lower.rotation;
            foot.rotation = Quaternion.Slerp(foot.rotation, pinRotation, pinWeight);
        }

        // ---- applying it ---------------------------------------------------------------------------------------
        void ApplyBody()
        {
            float breath = Mathf.Sin(Time.time * Mathf.PI * 2f / breathPeriod + breathPhase) * BreathDegrees * breathWeight;
            var across = rightShoulder != null && leftShoulder != null ? rightShoulder.position - leftShoulder.position : body.right;
            across.y = 0f;
            across = across.sqrMagnitude > 0.0001f ? across.normalized : body.right;
            float lift = -straighten * weight; // negative about his right = the chest comes up and back
            spine.Turn(Quaternion.AngleAxis(lift * 0.5f, across));
            chest.Turn(Quaternion.AngleAxis(chestYaw * weight, Vector3.up) * Quaternion.AngleAxis(lift * 0.5f - breath, across));
        }

        // Called by AllyIdleLifeHead after AllyCombatant has placed the rifle and aimed the head.
        internal void ApplyHead()
        {
            if (headBone == null || body == null) return;
            // the head was aimed by someone else since the clip moved the body: the clip's turn of the head goes on top
            if (headTotalPending && Quaternion.Angle(headBone.rotation, headAfterBody) > 0.05f)
                headBone.rotation = headBone.rotation * headTotalNow;
            float breath = Mathf.Sin(Time.time * Mathf.PI * 2f / breathPeriod + breathPhase) * BreathDegrees * breathWeight;
            // a living head is never quite still
            float driftYaw = (Mathf.PerlinNoise(Time.time * 0.21f, seed) - 0.5f) * 5f;
            float driftPitch = (Mathf.PerlinNoise(seed, Time.time * 0.17f) - 0.5f) * 3f;
            float y = (yaw + driftYaw) * weight;
            // the clip may already carry the head turned on the shoulders: never past what a neck does
            if (leftShoulder != null && rightShoulder != null)
            {
                var shoulders = rightShoulder.position - leftShoulder.position; shoulders.y = 0f;
                var chestForward = Vector3.Cross(shoulders, Vector3.up);
                var headForward = headBone.forward; headForward.y = 0f;
                if (chestForward.sqrMagnitude > 0.0001f && headForward.sqrMagnitude > 0.0001f)
                {
                    float already = Vector3.SignedAngle(chestForward, headForward, Vector3.up);
                    // a clip's look round on top of a head the idle pose already has turned: never past what a neck does
                    if (track != null && Mathf.Abs(already) > ClipHeadLimit)
                    {
                        float over = (already - Mathf.Clamp(already, -ClipHeadLimit, ClipHeadLimit)) * clipApplied;
                        headBone.rotation = Quaternion.AngleAxis(-over, Vector3.up) * headBone.rotation;
                        already -= over;
                    }
                    y = Mathf.Clamp(y, Mathf.Min(0f, -MaxHeadOffChest - already), Mathf.Max(0f, MaxHeadOffChest - already));
                }
            }
            float p = (Mathf.Clamp(pitch, -MaxUp, MaxDown) + driftPitch) * weight + breath * 0.6f; // the head does not nod with the chest
            var across = headBone.right; across.y = 0f;
            across = across.sqrMagnitude > 0.0001f ? across.normalized : body.right;
            neck.Turn(Quaternion.AngleAxis(y * 0.4f, Vector3.up) * Quaternion.AngleAxis(p * 0.4f, across));
            across = Quaternion.AngleAxis(y * 0.4f, Vector3.up) * across;
            head.Turn(Quaternion.AngleAxis(y * 0.6f, Vector3.up) * Quaternion.AngleAxis(p * 0.6f, across));
        }
    }
}
