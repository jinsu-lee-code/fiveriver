using FiveRivers.Audio;
using FiveRivers.Data;
using FiveRivers.UI;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // 대사 립싱크 · 눈 깜빡임 (부족한 점 1번, 2026-11-26). 얇은 어댑터: 자막(SubtitleDisplay)의 줄이 이 사람 대사면
    // LipSyncTrack 으로 입 모양 시간표를 만들고, 블렌드 셰이프(메뉴 FiveRivers/Setup/Face Shapes 가 얹음) 세기로 옮긴다.
    // 화자 → 사람은 FaceConfig.speakers (오브젝트 이름). 블렌드 셰이프가 없는 메시면 스스로 꺼진다.
    [DisallowMultipleComponent]
    public sealed class FaceRig : MonoBehaviour
    {
        [SerializeField] FaceConfig config;
        [SerializeField] FaceModel model = FaceModel.Regular;
        [SerializeField] SkinnedMeshRenderer face;

        public const string JawOpen = "JawOpen", MouthClose = "MouthClose", MouthWide = "MouthWide", MouthRound = "MouthRound",
            BlinkL = "BlinkL", BlinkR = "BlinkR";

        static SubtitleDisplay sharedSubtitles;
        static AudioDirector sharedAudio;
        static int seeds;

        SubtitleDisplay subtitles;
        int jaw = -1, close = -1, wide = -1, round = -1, blinkL = -1, blinkR = -1;
        readonly MouthFollower follower = new MouthFollower();
        BlinkClock blink;
        LipSyncTrack track;
        GameText.Line pending;
        bool hasPending;
        float speakTime, mouthLimit = 1f;
        readonly float[] shown = { -1f, -1f, -1f, -1f, -1f, -1f };

        public bool Speaking => track != null;
        public MouthPose Mouth => follower.Current;
        public FaceModel Model => model;
        public SkinnedMeshRenderer Face => face;

        public void Configure(FaceConfig faceConfig, FaceModel faceModel, SkinnedMeshRenderer renderer)
        {
            config = faceConfig;
            model = faceModel;
            face = renderer;
        }

        void Awake()
        {
            var mesh = face != null ? face.sharedMesh : null;
            if (config == null || mesh == null || (jaw = mesh.GetBlendShapeIndex(JawOpen)) < 0) { enabled = false; return; }
            close = mesh.GetBlendShapeIndex(MouthClose);
            wide = mesh.GetBlendShapeIndex(MouthWide);
            round = mesh.GetBlendShapeIndex(MouthRound);
            blinkL = mesh.GetBlendShapeIndex(BlinkL);
            blinkR = mesh.GetBlendShapeIndex(BlinkR);
            mouthLimit = config.MouthLimit(model);
            blink = new BlinkClock(unchecked(GetInstanceID() * 7919 + ++seeds));
        }

        void OnEnable()
        {
            if (sharedSubtitles == null) sharedSubtitles = FindFirstObjectByType<SubtitleDisplay>();
            subtitles = sharedSubtitles;
            if (subtitles == null) return;
            subtitles.LineStarted += OnLineStarted;
            subtitles.LineEnded += StopSpeaking;
        }

        void OnDisable()
        {
            if (subtitles != null)
            {
                subtitles.LineStarted -= OnLineStarted;
                subtitles.LineEnded -= StopSpeaking;
            }
            StopSpeaking();
            follower.Reset();
            Apply(MouthPose.Closed, 0f, true);
        }

        void OnLineStarted(GameText.Line line)
        {
            // 다른 사람이 말을 시작하면 내 말은 끝난 것
            if (!IsSpeaker(line.speaker)) { StopSpeaking(); return; }
            // 음성은 같은 LineStarted 의 다른 구독자(AudioDirector)가 틀므로 길이는 다음 갱신에 읽는다
            pending = line;
            hasPending = true;
        }

        void StopSpeaking()
        {
            hasPending = false;
            track = null;
        }

        bool IsSpeaker(string speaker)
        {
            string actor = config.ActorFor(speaker);
            if (string.IsNullOrEmpty(actor)) return false;
            for (var t = transform; t != null; t = t.parent) if (t.name == actor) return true;
            return false;
        }

        void LateUpdate()
        {
            if (hasPending)
            {
                hasPending = false;
                if (sharedAudio == null) sharedAudio = FindFirstObjectByType<AudioDirector>();
                float voice = sharedAudio != null ? sharedAudio.VoiceSecondsLeft : 0f;
                float seconds = voice > 0f ? voice : pending.duration * config.speechFill;
                track = LipSyncTrack.Build(pending.text, seconds, config.lipSync);
                speakTime = 0f;
            }

            // 자막과 같은 실시간 시계 (자막은 unscaled 로 넘어간다)
            float dt = Time.unscaledDeltaTime;
            var target = MouthPose.Closed;
            if (track != null)
            {
                speakTime += dt;
                target = track.Sample(speakTime);
                if (speakTime >= track.Length) track = null;
            }
            var mouth = follower.Step(target, dt, config.lipSync);

            float eyes = 0f;
            if (blinkL >= 0 || blinkR >= 0)
                eyes = blink.Step(Time.deltaTime, config.blinkInterval.x, config.blinkInterval.y, config.blinkLength.x, config.blinkLength.y);
            Apply(mouth.Scaled(mouthLimit), eyes, false);
        }

        void Apply(MouthPose mouth, float eyes, bool force)
        {
            if (face == null) return;
            Set(0, jaw, mouth.jaw, force);
            Set(1, close, mouth.close, force);
            Set(2, wide, mouth.wide, force);
            Set(3, round, mouth.round, force);
            Set(4, blinkL, eyes, force);
            Set(5, blinkR, eyes, force);
        }

        // 바뀐 것만 넘긴다
        void Set(int slot, int shape, float value, bool force)
        {
            if (shape < 0) return;
            float weight = Mathf.Clamp01(value) * 100f;
            if (!force && Mathf.Abs(shown[slot] - weight) < 0.05f) return;
            shown[slot] = weight;
            face.SetBlendShapeWeight(shape, weight);
        }
    }
}
