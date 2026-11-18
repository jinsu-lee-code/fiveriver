using System;
using UnityEngine;

namespace FiveRivers.Data
{
    // 아군이 Rigify 조절 리그로 만든 동작 클립을 재생할 때 쓰는 자료 (2026-11-18).
    // 원본: source/anim/rigify_rok_20261118/export (*.fbx + *_rifle.json). 메뉴 FiveRivers/Setup/ROK Rigify Clips 가 굽는다.
    //  - clips: 클립과 그 역할, 그리고 JSON 의 소총 궤적(루트 기준 · 게임 크기 m · 프레임마다 손 접촉 0~1).
    //  - fits: 열린 씬의 아군마다(이름 · 아바타) 그 몸에서 클립을 떠서 맞춘 값. 소총은 가슴뼈 기준으로 구워 둔다
    //    (참호 바닥 맞춤 · 다른 체격 · 다른 코드가 몸을 옮겨도 소총이 몸에 붙어 간다).
    //  - 고지 돌격 달리기: 새 달리기 클립의 발 속도(HillAllies 의 RunClipSpeed / AdvanceClipSpeed 자리).
    // 표시 전용: 판정 · 진행 · 수치와 무관하다.
    public sealed class AllyRifleTracks : ScriptableObject
    {
        public const string ResourceName = "AllyRifleTracks";

        [Serializable]
        public struct Frame
        {
            public Vector3 position;   // AlliedM1 원점, 루트(애니메이터 원점) 기준, 게임 크기 m: x 오른쪽, y 위, z 앞
            public Quaternion rotation; // LookRotation(총구 쪽, 가늠자 쪽)
            public float gripLeft, gripRight; // 손이 총에 붙어 있는 정도 0~1
        }

        [Serializable]
        public sealed class Clip
        {
            public string name;              // 클립 이름 = 파일명
            public AllyClipRole role;
            public AnimationClip clip;
            public bool loop;
            public float length;             // 초
            public float fps = 30f;          // 궤적 프레임 수 / 초
            public Frame[] frames = new Frame[0];
        }

        // 열린 씬의 아군 한 명에게 맞춘 값
        [Serializable]
        public sealed class Fit
        {
            public string actor;             // AllyCombatant 오브젝트 이름
            public string avatar;            // 이 이름이 없을 때 같은 아바타끼리 쓴다
            public float floorOffset;        // 참호 클립이 도는 동안 골반을 올릴 양(모델 크기 1 단위, 음수 = 내림): 서서 대기의 발바닥에 맞춘다
            public float kneelFloorOffset;   // 바꿔 끼운 무릎 대기 · 사격(컨트롤러 상태)이 도는 동안 같은 뜻의 값 (메뉴가 잼, 기록용)
            public float scale = 1f;         // JSON m → 모델 단위
            public Vector3 offset;           // 모델 단위
            public HumanBodyBones anchor = HumanBodyBones.UpperChest; // 소총을 구운 기준 뼈
            public Baked[] clips = new Baked[0];
        }

        // 한 클립의 소총 자리: 프레임마다 기준 뼈 공간의 위치 · 회전 (프레임 수 = Clip.frames)
        [Serializable]
        public sealed class Baked
        {
            public string clip;
            public Vector3[] position = new Vector3[0];
            public Quaternion[] rotation = new Quaternion[0];
        }

        public Clip[] clips = new Clip[0];
        public Fit[] fits = new Fit[0];
        public AllyClipTiming timing = new AllyClipTiming();

        // 손목이 닿는 자리(소총 공간, m). 리그를 만든 01_build_rig.py 의 GRIPS 와 같은 값.
        public Vector3 rightWrist = new Vector3(0.030f, -0.058f, -0.205f);
        public Vector3 leftWrist = new Vector3(-0.045f, -0.040f, 0.215f);

        // 고지 돌격: 컨트롤러 상태 Run_Rifle / CrouchRun_Rifle 이 쓰는 원래 클립 이름(메뉴가 AC_ROK 에서 읽는다)과
        // 새 클립의 발 속도(사람 크기 1 기준, HillAllies 와 같은 뜻). 메뉴가 기존 클립 대비 비율로 잰다.
        public string runSourceClip = "A_ROK_Run_PortArms";
        public string advanceSourceClip = "A_KPA_CrouchRun";
        public float runClipSpeed = 3.57f;
        // 무릎 대기 · 무릎 사격: 컨트롤러 상태 Idle_Trench / Fire_Crouch 가 쓰는 원래 클립 이름(메뉴가 AC_ROK 에서 읽는다)
        public string kneelSourceClip = "A_ROK_Idle_Trench";
        public string kneelFireSourceClip = "A_ROK_Fire_Crouch";
        public float advanceClipSpeed = 2.62f;

        public Clip Find(string clipName)
        {
            if (clips == null || string.IsNullOrEmpty(clipName)) return null;
            foreach (var c in clips) if (c != null && c.name == clipName) return c;
            return null;
        }

        public Clip Find(AllyClipRole role)
        {
            if (clips == null) return null;
            foreach (var c in clips) if (c != null && c.role == role && c.clip != null) return c;
            return null;
        }

        // 이름이 같은 사람 → 없으면 같은 아바타 → 없으면 null
        public Fit FitFor(string actor, string avatar)
        {
            if (fits == null) return null;
            foreach (var f in fits) if (f != null && f.actor == actor) return f;
            foreach (var f in fits) if (f != null && !string.IsNullOrEmpty(avatar) && f.avatar == avatar) return f;
            return null;
        }

        public static Baked BakedFor(Fit fit, string clipName)
        {
            if (fit == null || fit.clips == null) return null;
            foreach (var b in fit.clips) if (b != null && b.clip == clipName && b.position != null && b.position.Length > 0) return b;
            return null;
        }

        // 프레임 위치(소수): 반복 클립은 마지막 프레임 = 첫 프레임이라 한 바퀴 = (프레임 수 - 1)
        public static float FrameAt(int count, float fps, float time, bool loop)
        {
            if (count <= 1) return 0f;
            float at = Mathf.Max(0f, time) * Mathf.Max(1f, fps);
            float last = count - 1;
            return loop ? Mathf.Repeat(at, last) : Mathf.Min(at, last);
        }

        public static Frame Sample(Frame[] frames, float fps, float time, bool loop)
        {
            if (frames == null || frames.Length == 0) return new Frame { rotation = Quaternion.identity, gripLeft = 1f, gripRight = 1f };
            float at = FrameAt(frames.Length, fps, time, loop);
            int i = Mathf.Min((int)at, frames.Length - 1), j = Mathf.Min(i + 1, frames.Length - 1);
            float f = at - i;
            var a = frames[i]; var b = frames[j];
            return new Frame
            {
                position = Vector3.Lerp(a.position, b.position, f),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, f),
                gripLeft = Mathf.Lerp(a.gripLeft, b.gripLeft, f),
                gripRight = Mathf.Lerp(a.gripRight, b.gripRight, f),
            };
        }

        public static void Sample(Baked baked, float fps, float time, bool loop, out Vector3 position, out Quaternion rotation)
        {
            int count = baked.position.Length;
            float at = FrameAt(count, fps, time, loop);
            int i = Mathf.Min((int)at, count - 1), j = Mathf.Min(i + 1, count - 1);
            float f = at - i;
            position = Vector3.Lerp(baked.position[i], baked.position[j], f);
            rotation = baked.rotation != null && baked.rotation.Length == count
                ? Quaternion.Slerp(baked.rotation[i], baked.rotation[j], f) : Quaternion.identity;
        }
    }

    // 클립의 쓰임. 값은 끝에만 추가한다(에셋이 정수로 저장함).
    public enum AllyClipRole
    {
        Base = 0,            // 참호 낮춘 대기 (반복)
        EnterFromStand = 1,  // 서기 → 낮춤
        ExitToStand = 2,     // 낮춤 → 서기
        ExitToFire = 3,      // 낮춤 → 무릎 사격
        Variation = 4,       // 낮춘 대기 위의 변화 동작 (낮춘 대기 첫 프레임에서 시작해 그리로 끝남)
        Duck = 5,            // 가까운 포탄에 머리 숙이기
        Run = 6,             // 고지 돌격 달리기 (컨트롤러 상태 Run_Rifle 자리)
        CrouchRun = 7,       // 낮춰 달리기 (CrouchRun_Rifle 자리)
        RunStop = 8,         // 달리다 멈춤 (지금은 쓰는 곳 없음)
        Story = 9,           // 이야기 장면이 이름으로 부르는 클립 (박용팔 무릎 꿇기 · 윤태석 엎드리기 등, 예정)
        RunStart = 10,       // 달리기 출발 (예정)
        KneelIdle = 11,      // 무릎 꿇고 경계 (컨트롤러 상태 Idle_Trench 자리, 2026-11-18 저녁: 꿇은 뒷발 발등이 땅)
        KneelFire = 12,      // 무릎 사격 한 발 (컨트롤러 상태 Fire_Crouch 자리)
    }

    // 클립 재생 박자 (화면을 보고 맞출 값). 데이터 에셋에 둔다.
    [Serializable]
    public sealed class AllyClipTiming
    {
        [Tooltip("서 있다가 낮출 때: 멈춰 선 지 이만큼 지나야 시작")] public float enterStill = 0.6f;
        [Tooltip("여러 사람이 한꺼번에 낮추지 않게 사람마다 늦추는 범위(초)")] public Vector2 enterStagger = new Vector2(0.1f, 0.9f);
        [Tooltip("컨트롤러 자세 → 서기→낮춤 클립")] public float enterBlend = 0.25f;
        [Tooltip("사격 · 무릎 자세 → 낮춘 대기 (곧바로)")] public float enterLowBlend = 0.4f;
        [Tooltip("클립끼리 이어 붙일 때")] public float joinFade = 0.2f;
        [Tooltip("교전 시작: 낮춤→무릎 사격으로 넘어갈 때 / 그 클립이 없거나 일어서는 중이면 이 시간에 빠짐")] public float fireFade = 0.12f;
        [Tooltip("낮춤→무릎 사격 끝에서 컨트롤러의 사격 자세로")] public float fireOut = 0.15f;
        [Tooltip("낮춤→서기 끝에서 컨트롤러의 서서 대기로")] public float standOut = 0.25f;
        [Tooltip("낮춤→서기로 넘어갈 때")] public float standFade = 0.15f;
        [Tooltip("고지 돌격 출발 · 다른 코드가 몸을 옮길 때 빠지는 시간")] public float advanceOut = 0.35f;
        [Tooltip("그 밖의 중단(돌아서 말하기 시작 · 연출이 자세를 가져감)")] public float brokenOut = 0.3f;
        [Tooltip("탄막 엄폐(TakeCover)가 시작될 때")] public float coverOut = 0.2f;
        [Tooltip("일어섰다가 다시 낮추기까지 최소")] public float reenterAfter = 2f;
        [Tooltip("변화 동작 첫 번째까지")] public Vector2 firstRest = new Vector2(2.5f, 9f);
        [Tooltip("변화 동작 사이 (AllyIdleLife 와 같은 6~14초)")] public Vector2 rest = new Vector2(6f, 14f);
        [Tooltip("두 사람이 이 시간 안에 함께 시작하지 않는다 (AllyIdleLife 와 같음)")] public float stagger = 1.6f;
        [Tooltip("숙이기 사이 최소")] public float duckCooldown = 4f;
        [Tooltip("숙이기로 넘어갈 때")] public float duckFade = 0.12f;
        [Tooltip("이야기 클립이 끝나고 컨트롤러로")] public float storyOut = 0.4f;
    }
}
