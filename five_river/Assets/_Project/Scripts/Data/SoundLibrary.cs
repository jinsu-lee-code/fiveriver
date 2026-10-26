using System;
using UnityEngine;

namespace FiveRivers.Data
{
    public enum SoundBus
    {
        Sfx = 0,
        Amb = 1,
        Bgm = 2,
        Vo = 3,
        Ui = 4,
    }

    // 소리 재생 규칙 (SOUND_LIST 1-6): 소리마다 클립(변형 _01~) · 버스 · 볼륨 · 음높이 범위 · 3D 여부 · 쿨다운.
    // 클립은 Setup 메뉴가 SOUND_LIST 파일명으로 찾아 넣는다. 파일이 없으면 기존 연결을 지우지 않는다.
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "FiveRivers/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;          // 파일명 (변형 번호 없이): SFX_M1_Fire
            public AudioClip[] clips = Array.Empty<AudioClip>();
            public SoundBus bus;
            [Range(0f, 1f)] public float volume = 1f;
            public float pitchVariance = 0.05f; // ±5% (SOUND_LIST 1-6)
            public bool spatial;
            public float maxDistance = 120f;
            public bool loop;
            public float cooldown;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry Find(string id)
        {
            foreach (var e in entries)
                if (e.id == id) return e;
            return null;
        }
    }
}
