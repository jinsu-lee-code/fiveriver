using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveRivers.Data;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace FiveRivers.EditorTools
{
    // SOUND_LIST 표의 파일명 그대로 그레이박스 소리를 합성하고(없는 파일만), SoundLibrary를 채운다.
    // 음성(VO_)은 tools/make_graybox_vo.ps1 (Windows 기본 한국어 음성)로 만든다.
    // 최종 소리가 sound 브랜치에서 같은 이름으로 오면 파일만 덮어쓰면 된다 (.meta 유지 → 연결 유지).
    static class AudioGraybox
    {
        const string Root = "Assets/_Project/Audio/";
        const int Rate = 48000;
        const string LibraryPath = DabudongSetup.DataDir + "SoundLibrary.asset";

        // id, 폴더, 변형 수(0 = 번호 없음), 길이(초), 스테레오, 버스, 볼륨, 3D, 루프, 합성
        sealed class Spec
        {
            public string id, folder;
            public int variants;
            public float seconds;
            public bool stereo;
            public SoundBus bus;
            public float volume = 1f;
            public bool spatial, loop;
            public float cooldown;
            public Func<float, Random, float> synth; // (t초, 난수) → -1~1
        }

        static readonly List<Spec> Specs = new List<Spec>();

        static void S(string id, string folder, int variants, float seconds, SoundBus bus, Func<float, Random, float> synth,
            float volume = 1f, bool spatial = false, bool loop = false, bool stereo = false, float cooldown = 0f)
        {
            Specs.Add(new Spec
            {
                id = id, folder = folder, variants = variants, seconds = seconds, bus = bus, synth = synth,
                volume = volume, spatial = spatial, loop = loop, stereo = stereo, cooldown = cooldown,
            });
        }

        static AudioGraybox()
        {
            const string W = "SFX/Weapon/", T = "SFX/Tank/", E = "SFX/Env/", F = "SFX/Foley/", A = "AMB/", B = "BGM/", U = "UI/", V = "VO/";
            // 무기 (SOUND_LIST 3-1)
            S("SFX_M1_Fire", W, 3, 1.0f, SoundBus.Sfx, (t, r) => Crack(t, r, 0.18f, 180f) + Thump(t, 70f, 0.12f) * 0.6f);
            S("SFX_M1_Ping", W, 0, 0.8f, SoundBus.Sfx, (t, r) => Tone(t, 2600f, 0.25f) * 0.7f + Tone(t, 3900f, 0.15f) * 0.3f);
            S("SFX_M1_ClipInsert", W, 0, 0.6f, SoundBus.Sfx, (t, r) => Click(t, 0f, r) + Click(t, 0.28f, r) * 1.2f);
            S("SFX_M1_ClipRemove", W, 0, 0.5f, SoundBus.Sfx, (t, r) => Click(t, 0f, r) * 0.8f + Noise(r) * Env(t, 0.05f, 0.3f) * 0.2f);
            S("SFX_M1_DryFire", W, 0, 0.2f, SoundBus.Sfx, (t, r) => Click(t, 0f, r) * 0.6f);
            S("SFX_Weapon_Switch", W, 2, 0.6f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.05f, 0.5f) * 0.5f + Click(t, 0.35f, r) * 0.4f);
            S("SFX_Bazooka_Fire", W, 0, 2.0f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.005f, 1.6f) + Thump(t, 50f, 0.4f) * 0.8f);
            S("SFX_Bazooka_Load", W, 0, 1.0f, SoundBus.Sfx, (t, r) => Click(t, 0f, r) * 0.6f + Click(t, 0.5f, r));
            S("SFX_Rocket_HitSide", W, 2, 2.5f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.005f, 2.0f) * 1.2f + Thump(t, 40f, 0.8f), spatial: true, maxDistance: 250f);
            S("SFX_Tank_Ricochet", W, 2, 1.2f, SoundBus.Sfx, (t, r) => Tone(t, 3100f - t * 1500f, 0.5f) * 0.8f + Click(t, 0f, r), spatial: true, maxDistance: 250f);
            S("SFX_Enemy_RifleFire", W, 3, 1.5f, SoundBus.Sfx, (t, r) => Crack(t, r, 0.6f, 120f) * 0.6f, spatial: true, maxDistance: 200f);
            S("SFX_Enemy_SMG", W, 2, 1.2f, SoundBus.Sfx, (t, r) => Burst(t, r, 0.07f, 0.9f) * 0.5f, spatial: true, maxDistance: 200f);
            S("SFX_Bullet_Whiz", W, 4, 0.4f, SoundBus.Sfx, (t, r) => Noise(r) * Env(t, 0.08f, 0.25f) * 0.5f * Mathf.Sin(t * 900f), cooldown: 0.15f);
            S("SFX_Impact_Dirt", W, 3, 0.35f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.002f, 0.25f) * 0.7f, spatial: true, maxDistance: 60f, cooldown: 0.05f);
            S("SFX_Player_Hit", W, 2, 0.5f, SoundBus.Sfx, (t, r) => Thump(t, 60f, 0.3f) + Brown(r) * Env(t, 0.002f, 0.2f) * 0.3f);
            // 전차 (3-2)
            S("SFX_Tank_EngineIdle_Loop", T, 0, 4f, SoundBus.Sfx, (t, r) => Hum(t, 38f) * 0.6f, spatial: true, loop: true, maxDistance: 180f);
            S("SFX_Tank_Move_Loop", T, 0, 4f, SoundBus.Sfx, (t, r) => Hum(t, 46f) * 0.6f + Burst(t % 0.5f, r, 0.25f, 0.4f) * 0.25f, spatial: true, loop: true, maxDistance: 180f);
            S("SFX_Tank_Turret_Loop", T, 0, 2f, SoundBus.Sfx, (t, r) => Tone(t, 420f, 99f) * 0.2f, spatial: true, loop: true, maxDistance: 80f);
            S("SFX_Tank_Cannon", T, 0, 3.0f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.003f, 2.6f) * 1.3f + Thump(t, 35f, 1.0f), spatial: true, maxDistance: 300f);
            S("SFX_Tank_MG", T, 2, 1.5f, SoundBus.Sfx, (t, r) => Burst(t, r, 0.09f, 1.3f) * 0.5f, spatial: true, maxDistance: 200f);
            S("SFX_Tank_Smoke_Loop", T, 0, 4f, SoundBus.Sfx, (t, r) => Noise(r) * 0.12f, spatial: true, loop: true, maxDistance: 60f);
            S("SFX_Tank_Fire_Loop", T, 0, 4f, SoundBus.Sfx, (t, r) => Brown(r) * 0.35f + (r.NextDouble() < 0.0006 ? 0.8f : 0f), spatial: true, loop: true, maxDistance: 100f);
            S("SFX_Tank_EngineStall", T, 0, 2.5f, SoundBus.Sfx, (t, r) => Hum(t, 46f - t * 14f) * Env(t, 0.01f, 2.3f) * 0.8f, spatial: true, maxDistance: 180f);
            // 조명탄 · 환경 (3-3)
            S("SFX_Flare_Launch", E, 0, 2.0f, SoundBus.Sfx, (t, r) => Thump(t, 90f, 0.15f) * 0.7f + Tone(t, 600f + t * 900f, 99f) * 0.15f * Env(t, 0.2f, 1.6f));
            S("SFX_Flare_Burn_Loop", E, 0, 4f, SoundBus.Sfx, (t, r) => Noise(r) * 0.15f, loop: true);
            S("SFX_Artillery_Distant", E, 3, 4.0f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.05f, 3.5f) * 0.4f, stereo: true, volume: 0.6f);
            // 폴리 (3-4)
            S("SFX_Foot_Dirt", F, 4, 0.25f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.003f, 0.15f) * 0.35f, volume: 0.5f);
            S("SFX_Foot_Dirt_Run", F, 4, 0.25f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.003f, 0.18f) * 0.5f, volume: 0.6f);
            S("SFX_Stance_Change", F, 2, 0.6f, SoundBus.Sfx, (t, r) => Brown(r) * Env(t, 0.04f, 0.45f) * 0.4f);
            S("SFX_Ammo_Pickup", F, 0, 1.0f, SoundBus.Sfx, (t, r) => Click(t, 0f, r) + Click(t, 0.25f, r) * 0.7f + Click(t, 0.5f, r) * 0.9f);
            // 환경음 (3-5) — 그레이박스는 10초 루프 (최종은 30초)
            S("AMB_Night_Hills_Loop", A, 0, 10f, SoundBus.Amb, (t, r) => Brown(r) * 0.12f + Chirp(t, 4300f, 0.37f) * 0.05f, loop: true, stereo: true);
            S("AMB_Battle_Distant_Loop", A, 0, 10f, SoundBus.Amb, (t, r) => Brown(r) * 0.18f + (r.NextDouble() < 0.0002 ? 0.5f : 0f), loop: true, stereo: true);
            S("AMB_Silence_Loop", A, 0, 10f, SoundBus.Amb, (t, r) => Brown(r) * 0.06f, loop: true, stereo: true);
            S("AMB_Dawn_Loop", A, 0, 10f, SoundBus.Amb, (t, r) => Brown(r) * 0.05f + Chirp(t, 2400f, 1.3f) * 0.06f, loop: true, stereo: true);
            // 음악 (3-6) — 그레이박스 임시본: 무음
            S("BGM_Title_Loop", B, 0, 60f, SoundBus.Bgm, (t, r) => 0f, loop: true, stereo: true);
            S("BGM_Combat_Bed_Loop", B, 0, 60f, SoundBus.Bgm, (t, r) => 0f, loop: true, stereo: true);
            S("BGM_Ending", B, 0, 40f, SoundBus.Bgm, (t, r) => 0f, stereo: true);
            // UI (3-7) — 보상음처럼 들리지 않게 아주 작게
            S("UI_Objective_Show", U, 0, 0.4f, SoundBus.Ui, (t, r) => Noise(r) * Env(t, 0.02f, 0.3f) * 0.15f, volume: 0.5f);
            S("UI_Hint_Show", U, 0, 0.3f, SoundBus.Ui, (t, r) => Tone(t, 500f, 0.1f) * 0.2f, volume: 0.5f);
            S("UI_Menu_Click", U, 0, 0.2f, SoundBus.Ui, (t, r) => Click(t, 0f, r) * 0.5f, volume: 0.5f);
            // 음성 (3-8 ~ 3-11) — 합성하지 않음(TTS 스크립트), 신음만 합성
            S("VO_OSG_Hurt", V, 3, 0.6f, SoundBus.Vo, (t, r) => Tone(t, 160f - t * 60f, 0.4f) * 0.4f, cooldown: 0.5f);
            foreach (var vo in new[] { "VO_SQD_Scout_01", "VO_SQD_Flare_01", "VO_SQD_Flare_02", "VO_SQD_Retreat_01", "VO_PARK_Call_01", "VO_PARK_Call_02", "VO_SQD_Tank_01", "VO_SQD_Tank_02", "VO_SQD_Ally_01" })
                S(vo, V, 0, 0f, SoundBus.Vo, null);
            S("VO_KPA_Shout", V, 3, 0f, SoundBus.Vo, null, spatial: true, maxDistance: 150f, cooldown: 4f);
        }

        static void S(string id, string folder, int variants, float seconds, SoundBus bus, Func<float, Random, float> synth,
            bool spatial, float maxDistance, float volume = 1f, bool loop = false, bool stereo = false, float cooldown = 0f)
        {
            S(id, folder, variants, seconds, bus, synth, volume, spatial, loop, stereo, cooldown);
            Specs[Specs.Count - 1].spatial = spatial;
            Specs[Specs.Count - 1].cooldown = cooldown;
            maxDistances[id] = maxDistance;
        }

        static readonly Dictionary<string, float> maxDistances = new Dictionary<string, float>();

        // ---- 합성 재료 ----
        static float Env(float t, float attack, float release) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / Mathf.Max(0.001f, release) * 4f);
        static float Noise(Random r) => (float)(r.NextDouble() * 2 - 1);
        static float brown;
        static float Brown(Random r)
        {
            brown = Mathf.Clamp(brown * 0.98f + Noise(r) * 0.12f, -1f, 1f);
            return brown * 2.5f;
        }
        static float Tone(float t, float hz, float decay) => Mathf.Sin(2f * Mathf.PI * hz * t) * (decay >= 99f ? 1f : Mathf.Exp(-t / decay));
        static float Thump(float t, float hz, float decay) => Mathf.Sin(2f * Mathf.PI * hz * t * (1f - t)) * Mathf.Exp(-t / decay);
        static float Crack(float t, Random r, float tail, float hz) => Noise(r) * Env(t, 0.001f, tail) + Thump(t, hz, 0.05f) * 0.5f;
        static float Click(float t, float at, Random r) => t >= at && t < at + 0.03f ? Noise(r) * (1f - (t - at) / 0.03f) : 0f;
        static float Burst(float t, Random r, float gap, float length) => t < length && (t % gap) < 0.025f ? Noise(r) : 0f;
        static float Hum(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * hz * 2.03f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * hz * 3.1f * t) * 0.15f;
        static float Chirp(float t, float hz, float every) => (t % every) < 0.06f ? Mathf.Sin(2f * Mathf.PI * hz * t) : 0f;

        // ---- 파일 쓰기 ----
        static IEnumerable<string> Names(Spec s)
        {
            if (s.variants <= 0) yield return s.id;
            else for (int i = 1; i <= s.variants; i++) yield return $"{s.id}_{i:00}";
        }

        [MenuItem("FiveRivers/Setup/Audio Graybox + Sound Library")]
        public static void Run()
        {
            int made = 0;
            foreach (var s in Specs)
            {
                if (s.synth == null) continue;
                int variant = 0;
                foreach (var name in Names(s))
                {
                    string dir = Root + s.folder;
                    string path = dir + name + ".wav";
                    variant++;
                    if (File.Exists(path)) continue; // 최종본 · 손댄 파일 보호
                    GrayboxAssets.EnsureFolder(dir);
                    WriteWav(path, s, variant);
                    made++;
                }
            }
            AssetDatabase.Refresh();
            BuildLibrary();
            Debug.Log($"[Setup] 그레이박스 소리 {made}개 생성, SoundLibrary 갱신");
        }

        static void WriteWav(string path, Spec s, int variant)
        {
            var rng = new Random(path.GetHashCode() & 0x7fffffff);
            brown = 0f;
            int channels = s.stereo ? 2 : 1;
            int frames = Mathf.Max(1, Mathf.RoundToInt(s.seconds * Rate));
            float pitch = 1f + (variant - 1) * 0.06f;
            var data = new short[frames * channels];
            float peak = 0f;
            var samples = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float v = s.synth(i / (float)Rate * pitch, rng);
                if (s.loop) v *= Mathf.Clamp01(Mathf.Min(i, frames - 1 - i) / (Rate * 0.05f)); // 이음새 부드럽게
                samples[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            float gain = peak > 0.891f ? 0.891f / peak : 1f; // 피크 −1 dBFS 이하
            for (int i = 0; i < frames; i++)
            {
                short v = (short)Mathf.Clamp(samples[i] * gain * short.MaxValue, short.MinValue, short.MaxValue);
                for (int c = 0; c < channels; c++) data[i * channels + c] = v;
            }
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = data.Length * 2;
                w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
                w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)channels);
                w.Write(Rate); w.Write(Rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write("data".ToCharArray()); w.Write(bytes);
                foreach (var v in data) w.Write(v);
            }
        }

        // 라이브러리: SOUND_LIST id마다 같은 이름 클립을 찾아 넣는다. 찾은 게 없으면 기존 연결을 그대로 둔다.
        static void BuildLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            var old = lib.entries.ToDictionary(e => e.id, e => e);
            var entries = new List<SoundLibrary.Entry>();
            foreach (var s in Specs)
            {
                var clips = Names(s)
                    .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + s.folder + n + ".wav"))
                    .Where(c => c != null).ToArray();
                old.TryGetValue(s.id, out var prev);
                var e = prev ?? new SoundLibrary.Entry { id = s.id, volume = s.volume };
                if (clips.Length > 0) e.clips = clips;
                e.bus = s.bus;
                e.spatial = s.spatial;
                e.loop = s.loop;
                e.cooldown = s.cooldown;
                if (maxDistances.TryGetValue(s.id, out float d)) e.maxDistance = d;
                entries.Add(e);
            }
            lib.entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }
    }
}
