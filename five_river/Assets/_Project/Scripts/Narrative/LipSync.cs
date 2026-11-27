using System;
using System.Collections.Generic;

namespace FiveRivers.Narrative
{
    // 대사 립싱크 계산 (부족한 점 1번, 2026-11-26). 순수 C#: 자막 문장과 말하는 시간으로 입 모양 시간표를 만든다.
    // 한글 음절을 초성 · 중성 · 종성으로 나눠 중성으로 입 모양, 초성 · 종성 ㅁ · ㅂ · ㅍ로 입술 닫힘을 정한다.
    // 화면에 붙이는 일(블렌드 셰이프 세기)은 FaceRig가 한다. 수치는 FaceConfig(LipSyncSettings).

    // 입 모양 네 가지 (0~1). 블렌드 셰이프 JawOpen · MouthClose · MouthWide · MouthRound 에 그대로 간다.
    public struct MouthPose
    {
        public float jaw, close, wide, round;

        public MouthPose(float jaw, float close, float wide, float round)
        {
            this.jaw = jaw; this.close = close; this.wide = wide; this.round = round;
        }

        public static MouthPose Closed => default;

        public MouthPose Scaled(float k) => new MouthPose(jaw * k, close * k, wide * k, round * k);

        public float Largest => Math.Max(Math.Max(jaw, close), Math.Max(wide, round));
    }

    [Serializable]
    public sealed class LipSyncSettings
    {
        // 모음 입 모양 (블렌드 셰이프 100 = 1)
        public float openJaw = 0.85f;      // ㅏ ㅑ ㅘ
        public float eoJaw = 0.65f;        // ㅓ ㅕ ㅝ
        public float midJaw = 0.42f;       // ㅐ ㅔ ㅒ ㅖ ㅙ ㅞ 의 턱
        public float midWide = 0.45f;      // ㅐ ㅔ 의 옆으로 벌림
        public float wide = 0.75f;         // ㅣ ㅢ ㅟ
        public float wideJaw = 0.15f;      // ㅣ 의 약한 턱
        public float round = 0.8f;         // ㅗ ㅛ ㅜ ㅠ ㅚ
        public float roundJaw = 0.28f;     // ㅗ ㅜ 의 약한 턱
        public float euWide = 0.35f;       // ㅡ (입술을 둥글게 하지 않는 모음: 조금 옆으로, 턱 약하게)
        public float euJaw = 0.12f;
        public float close = 1f;           // 초성 · 종성 ㅁ ㅂ ㅍ (입술이 닿음)
        public float codaJaw = 0.5f;       // ㅁ ㅂ ㅍ 아닌 받침(ㄴ ㅇ ㄹ ㄱ …)이 있으면 끝부분 턱이 이만큼으로 줄어듦
        public float glideShare = 0.35f;   // ㅘ ㅝ ㅙ ㅞ ㅚ ㅟ 의 앞부분 둥근 입 비율
        public float onsetShare = 0.3f;    // 초성 ㅁ ㅂ ㅍ 이 음절 앞에서 차지하는 비율
        public float codaShare = 0.3f;     // 받침이 음절 끝에서 차지하는 비율
        public float variation = 0.12f;    // 음절마다 세기를 ± 이만큼 흔듦 (모두 같은 크기로 벌리면 기계 같음)

        // 박자 (음절 1개 = 1칸)
        public float syllableMin = 0.1f;   // 한 음절 최소 · 최대 길이(초). 자막 시간이 너무 짧거나 길어도 이 안에서 말한다
        public float syllableMax = 0.24f;
        public float spaceUnits = 0.3f;    // 띄어쓰기
        public float commaUnits = 1.2f;    // , ·
        public float stopUnits = 1.8f;     // . ! ?
        public float ellipsisUnits = 2.5f; // … / ...
        public float otherUnits = 0.6f;    // 한글 아닌 글자 · 숫자 한 개

        // 화면에서 섞기 (끊긴 프레임에도 한 번에 이만큼 넘게 움직이지 않는다)
        public float jawSpeed = 7f;        // 초당 세기 변화
        public float lipSpeed = 11f;       // 닫힘 · 옆 · 둥근 입
        public float maxStep = 1f / 30f;   // 한 프레임에 계산하는 시간 상한(초)
    }

    public sealed class LipSyncTrack
    {
        struct Key
        {
            public float start, end;
            public MouthPose pose;
        }

        readonly List<Key> keys = new List<Key>();
        int cursor;

        public float Length { get; private set; }   // 실제로 말하는 시간(초). 그 뒤는 닫힌 입
        public float Unit { get; private set; }     // 음절 한 칸 길이(초)
        public int Syllables { get; private set; }

        public static LipSyncTrack Empty => new LipSyncTrack();

        // speakSeconds: 음성이 있으면 음성 길이, 없으면 자막 시간 중 말하는 부분
        public static LipSyncTrack Build(string text, float speakSeconds, LipSyncSettings s)
        {
            var track = new LipSyncTrack();
            if (string.IsNullOrEmpty(text) || s == null) return track;

            // 1) 글자 → 칸 (음절 · 쉼). 앞뒤의 쉼은 박자 맞추기에서 뺀다.
            var cells = new List<Cell>();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '.' && i + 2 < text.Length && text[i + 1] == '.' && text[i + 2] == '.')
                {
                    cells.Add(Cell.Pause(s.ellipsisUnits));
                    i += 2;
                    continue;
                }
                var cell = Classify(c, s);
                if (cell.units > 0f) cells.Add(cell);
            }
            int first = cells.FindIndex(x => x.syllable), last = cells.FindLastIndex(x => x.syllable);
            if (first < 0) return track;

            float units = 0f;
            int syllables = 0;
            for (int i = first; i <= last; i++) { units += cells[i].units; if (cells[i].syllable) syllables++; }

            float unit = speakSeconds > 0f ? speakSeconds / units : s.syllableMax;
            if (unit < s.syllableMin) unit = s.syllableMin;
            if (unit > s.syllableMax) unit = s.syllableMax;
            track.Unit = unit;
            track.Syllables = syllables;

            // 2) 칸 → 시간표
            float t = 0f;
            int index = 0;
            for (int i = first; i <= last; i++)
            {
                var cell = cells[i];
                float length = cell.units * unit;
                if (!cell.syllable) track.Add(t, t + length, MouthPose.Closed);
                else track.AddSyllable(t, length, cell, Wobble(index++, s.variation), s);
                t += length;
            }
            track.Length = t;
            return track;
        }

        public MouthPose Sample(float time)
        {
            if (keys.Count == 0 || time < 0f || time >= Length) return MouthPose.Closed;
            // 시간은 거의 늘 앞으로만 가므로 지난번 자리부터 찾는다
            if (cursor >= keys.Count || keys[cursor].start > time) cursor = 0;
            while (cursor < keys.Count - 1 && keys[cursor].end <= time) cursor++;
            return keys[cursor].pose;
        }

        void Add(float start, float end, MouthPose pose)
        {
            if (end > start) keys.Add(new Key { start = start, end = end, pose = pose });
        }

        void AddSyllable(float t, float length, Cell cell, float wobble, LipSyncSettings s)
        {
            var vowel = cell.vowel.Scaled(wobble);
            float onset = cell.bilabialOnset ? length * s.onsetShare : 0f;
            float coda = cell.coda != Coda.None ? length * s.codaShare : 0f;
            float vowelStart = t + onset, vowelEnd = t + length - coda;
            if (onset > 0f) Add(t, vowelStart, new MouthPose(0f, s.close, 0f, 0f));
            if (cell.glide)
            {
                float glideEnd = vowelStart + (vowelEnd - vowelStart) * s.glideShare;
                Add(vowelStart, glideEnd, new MouthPose(s.roundJaw, 0f, 0f, s.round).Scaled(wobble));
                vowelStart = glideEnd;
            }
            Add(vowelStart, vowelEnd, vowel);
            if (cell.coda == Coda.Bilabial) Add(vowelEnd, t + length, new MouthPose(0f, s.close, 0f, 0f));
            else if (cell.coda == Coda.Other)
                Add(vowelEnd, t + length, new MouthPose(vowel.jaw * s.codaJaw, 0f, vowel.wide * 0.6f, vowel.round * 0.6f));
        }

        // 음절마다 1 ± variation 사이의 정해진 값 (같은 문장은 늘 같은 모양)
        static float Wobble(int index, float variation)
        {
            uint h = (uint)(index + 1) * 2654435761u;
            h ^= h >> 15;
            float r = (h & 0xffff) / 65535f; // 0~1
            return 1f + variation * (r * 2f - 1f);
        }

        // ---- 한글 ----------------------------------------------------------------------------------------------------
        public enum Coda { None, Bilabial, Other }

        struct Cell
        {
            public float units;
            public bool syllable, bilabialOnset, glide;
            public Coda coda;
            public MouthPose vowel;

            public static Cell Pause(float units) => new Cell { units = units };
        }

        public const int HangulFirst = 0xAC00, HangulLast = 0xD7A3;

        // 한글 음절 → 초성(0~18) · 중성(0~20) · 종성(0~27). 한글 음절이 아니면 false
        public static bool Split(char c, out int initial, out int medial, out int final)
        {
            initial = medial = final = -1;
            if (c < HangulFirst || c > HangulLast) return false;
            int code = c - HangulFirst;
            initial = code / 588;
            medial = code % 588 / 28;
            final = code % 28;
            return true;
        }

        // 초성 ㅁ(6) ㅂ(7) ㅃ(8) ㅍ(17)
        public static bool BilabialInitial(int initial) => initial == 6 || initial == 7 || initial == 8 || initial == 17;

        // 종성 ㅁ(16) ㅂ(17) ㅄ(18) ㅍ(26), 소리가 ㅁ · ㅂ 인 겹받침 ㄻ(10) ㄿ(14)
        public static Coda FinalKind(int final)
        {
            if (final <= 0) return Coda.None;
            if (final == 16 || final == 17 || final == 18 || final == 26 || final == 10 || final == 14) return Coda.Bilabial;
            return Coda.Other;
        }

        // 중성 → 입 모양. glide: ㅘ ㅝ ㅙ ㅞ ㅚ ㅟ 처럼 앞에 둥근 입(w)이 붙는 모음
        public static MouthPose Vowel(int medial, LipSyncSettings s, out bool glide)
        {
            glide = medial == 9 || medial == 10 || medial == 11 || medial == 14 || medial == 15 || medial == 16;
            switch (medial)
            {
                case 0: case 2: case 9: return new MouthPose(s.openJaw, 0f, 0f, 0f);                 // ㅏ ㅑ ㅘ
                case 4: case 6: case 14: return new MouthPose(s.eoJaw, 0f, 0f, 0f);                  // ㅓ ㅕ ㅝ
                case 1: case 3: case 5: case 7: case 10: case 15:
                    return new MouthPose(s.midJaw, 0f, s.midWide, 0f);                               // ㅐ ㅒ ㅔ ㅖ ㅙ ㅞ
                case 8: case 12: case 13: case 17: case 11:
                    return new MouthPose(s.roundJaw, 0f, 0f, s.round);                               // ㅗ ㅛ ㅜ ㅠ ㅚ
                case 18: return new MouthPose(s.euJaw, 0f, s.euWide, 0f);                            // ㅡ
                default: return new MouthPose(s.wideJaw, 0f, s.wide, 0f);                            // ㅣ ㅢ ㅟ
            }
        }

        static Cell Classify(char c, LipSyncSettings s)
        {
            if (Split(c, out int initial, out int medial, out int final))
            {
                var vowel = Vowel(medial, s, out bool glide);
                return new Cell { units = 1f, syllable = true, bilabialOnset = BilabialInitial(initial), glide = glide, coda = FinalKind(final), vowel = vowel };
            }
            if (char.IsWhiteSpace(c)) return Cell.Pause(s.spaceUnits);
            switch (c)
            {
                case ',': case '·': case '、': return Cell.Pause(s.commaUnits);
                case '.': case '!': case '?': return Cell.Pause(s.stopUnits);
                case '…': return Cell.Pause(s.ellipsisUnits);
            }
            if (char.IsLetterOrDigit(c))
                return new Cell { units = s.otherUnits, syllable = true, vowel = new MouthPose(s.midJaw, 0f, 0f, 0f) };
            return default; // 따옴표 · 물결 등은 박자 없음
        }
    }

    // 입 모양을 목표로 부드럽게 옮긴다. 한 프레임 계산 시간은 maxStep 까지라 끊긴 프레임에도 한 번에 튀지 않는다.
    public sealed class MouthFollower
    {
        public MouthPose Current;

        public void Reset() => Current = MouthPose.Closed;

        public MouthPose Step(MouthPose target, float dt, LipSyncSettings s)
        {
            if (dt < 0f) dt = 0f;
            if (dt > s.maxStep) dt = s.maxStep;
            Current.jaw = Toward(Current.jaw, target.jaw, s.jawSpeed * dt);
            Current.close = Toward(Current.close, target.close, s.lipSpeed * dt);
            Current.wide = Toward(Current.wide, target.wide, s.lipSpeed * dt);
            Current.round = Toward(Current.round, target.round, s.lipSpeed * dt);
            return Current;
        }

        static float Toward(float from, float to, float most)
        {
            float d = to - from;
            if (d > most) return from + most;
            if (d < -most) return from - most;
            return to;
        }
    }

    // 눈 깜빡임 시계: interval 초 사이 무작위로 기다렸다가 length 초 동안 감았다 뜬다 (앞 40 % 감기, 뒤 60 % 뜨기).
    public sealed class BlinkClock
    {
        readonly Random random;
        float wait, blink, length;

        public BlinkClock(int seed) { random = new Random(seed); wait = -1f; }

        public float NextWait { get { return wait; } }

        // 0 = 뜬 눈, 1 = 감은 눈
        public float Step(float dt, float intervalMin, float intervalMax, float lengthMin, float lengthMax)
        {
            if (wait < 0f) wait = Range(intervalMin, intervalMax);
            if (blink > 0f || length > 0f)
            {
                blink += dt;
                if (blink >= length)
                {
                    blink = length = 0f;
                    wait = Range(intervalMin, intervalMax);
                    return 0f;
                }
                float p = blink / length;
                return p < 0.4f ? p / 0.4f : 1f - (p - 0.4f) / 0.6f;
            }
            wait -= dt;
            if (wait > 0f) return 0f;
            length = Range(lengthMin, lengthMax);
            blink = Math.Min(-wait, length * 0.4f); // 기다림을 넘긴 만큼 이미 감기 시작 (끊긴 프레임에도 감은 눈 중간까지만)
            if (blink <= 0f) blink = 1e-4f;
            float q = blink / length;
            return q < 0.4f ? q / 0.4f : 1f;
        }

        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
    }
}
