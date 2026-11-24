using System;
using UnityEngine;

namespace FiveRivers.Data
{
    // 탄약수 클립의 로켓 소품 궤적 (source/anim/rigify_rok_20261118/export/A_ROK_Loader_*_prop.json).
    // 프레임마다 로켓 꼬리 끝(원점)의 위치 · 방향, 상태. 루트(애니메이터 원점) 기준, 게임 크기 m — 소총 궤적(AllyRifleTracks.Frame)과 같은 뜻.
    // 메뉴 FiveRivers/Setup/Rocket Team 이 JSON 에서 굽는다. 표시 전용.
    public sealed class LoaderPropTracks : ScriptableObject
    {
        public const string ResourceName = "LoaderPropTracks";

        // 값은 끝에만 추가한다 (에셋이 정수로 저장함).
        public enum PropState { Hidden = 0, Hands = 1, HandR = 2, HandL = 3, Tube = 4, Pile = 5 }

        [Serializable]
        public struct Frame
        {
            public Vector3 position;
            public Quaternion rotation;   // LookRotation(forward = 탄두 쪽, up)
            public PropState state;
            public float insert;          // Tube 일 때 0~1
            public float gripLeft, gripRight;
        }

        [Serializable]
        public sealed class Clip
        {
            public string name;
            public float fps = 30f;
            public Frame[] frames = new Frame[0];
        }

        [Serializable]
        public struct Slot
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        public Clip[] clips = new Clip[0];
        public Slot[] pile = new Slot[0];        // 세워 둔 로켓 자리 (0번을 먼저 집는다)
        public float rocketLength = 0.6f;
        public Vector3 gunnerFeet, gunnerEye, rearMouth, tubeAxis, tapPoint;

        public Clip Find(string clipName)
        {
            if (clips == null || string.IsNullOrEmpty(clipName)) return null;
            foreach (var c in clips) if (c != null && c.name == clipName) return c;
            return null;
        }

        // 한 시각의 프레임 (위치 · 방향은 이웃 프레임 사이를 섞고, 상태는 앞 프레임 것)
        public static Frame Sample(Clip clip, float time, bool loop)
        {
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return new Frame { rotation = Quaternion.identity };
            int count = clip.frames.Length;
            float at = Mathf.Max(0f, time) * Mathf.Max(1f, clip.fps);
            float last = count - 1;
            at = count <= 1 ? 0f : loop ? Mathf.Repeat(at, last) : Mathf.Min(at, last);
            int i = Mathf.Min((int)at, count - 1), j = Mathf.Min(i + 1, count - 1);
            float f = at - i;
            var a = clip.frames[i]; var b = clip.frames[j];
            return new Frame
            {
                position = Vector3.Lerp(a.position, b.position, f),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, f),
                state = a.state,
                insert = Mathf.Lerp(a.insert, b.insert, f),
                gripLeft = Mathf.Lerp(a.gripLeft, b.gripLeft, f),
                gripRight = Mathf.Lerp(a.gripRight, b.gripRight, f),
            };
        }
    }
}
