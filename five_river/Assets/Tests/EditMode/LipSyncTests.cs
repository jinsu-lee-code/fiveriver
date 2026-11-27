using System.Collections.Generic;
using System.Reflection;
using FiveRivers.Data;
using FiveRivers.Narrative;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 대사 립싱크 계산 (부족한 점 1번, 2026-11-26)
    public sealed class LipSyncTests
    {
        static readonly LipSyncSettings S = new LipSyncSettings();

        static MouthPose At(LipSyncTrack track, float syllable, float within) => track.Sample((syllable + within) * track.Unit);

        [Test]
        public void HangulSyllablesSplitIntoInitialMedialFinal()
        {
            Assert.IsTrue(LipSyncTrack.Split('박', out int i, out int m, out int f));
            Assert.AreEqual((7, 0, 1), (i, m, f)); // ㅂ ㅏ ㄱ
            Assert.IsTrue(LipSyncTrack.Split('윤', out i, out m, out f));
            Assert.AreEqual((11, 17, 4), (i, m, f)); // ㅇ ㅠ ㄴ
            Assert.IsFalse(LipSyncTrack.Split('A', out _, out _, out _));
            Assert.IsTrue(LipSyncTrack.BilabialInitial(6) && LipSyncTrack.BilabialInitial(7) && LipSyncTrack.BilabialInitial(17));
            Assert.AreEqual(LipSyncTrack.Coda.Bilabial, LipSyncTrack.FinalKind(16)); // ㅁ
            Assert.AreEqual(LipSyncTrack.Coda.Other, LipSyncTrack.FinalKind(4));     // ㄴ
            Assert.AreEqual(LipSyncTrack.Coda.None, LipSyncTrack.FinalKind(0));
        }

        [Test]
        public void VowelsPickTheirMouthShape()
        {
            var a = LipSyncTrack.Build("아", 0.2f, S).Sample(0.1f);
            var u = LipSyncTrack.Build("우", 0.2f, S).Sample(0.1f);
            var i = LipSyncTrack.Build("이", 0.2f, S).Sample(0.1f);
            Assert.Greater(a.jaw, 0.6f);
            Assert.AreEqual(0f, a.round);
            Assert.Greater(u.round, u.jaw);
            Assert.Greater(u.jaw, 0f, "ㅜ 에도 약한 턱");
            Assert.Greater(i.wide, i.jaw);
            Assert.Greater(i.jaw, 0f, "ㅣ 에도 약한 턱");
        }

        [Test]
        public void BilabialsCloseTheLips()
        {
            var track = LipSyncTrack.Build("밥", 0.2f, S);
            var start = At(track, 0, 0.05f);
            var middle = At(track, 0, 0.5f);
            var end = At(track, 0, 0.95f);
            Assert.AreEqual(1f, start.close, 1e-4f, "초성 ㅂ");
            Assert.AreEqual(0f, start.jaw);
            Assert.AreEqual(0f, middle.close);
            Assert.Greater(middle.jaw, 0.6f);
            Assert.AreEqual(1f, end.close, 1e-4f, "받침 ㅂ");
            // 다른 받침은 닫지 않고 턱만 줄인다
            var han = LipSyncTrack.Build("한", 0.2f, S);
            Assert.AreEqual(0f, At(han, 0, 0.95f).close);
            Assert.Less(At(han, 0, 0.95f).jaw, At(han, 0, 0.5f).jaw);
        }

        [Test]
        public void SpacesAndPunctuationCloseTheMouthAndTrailingStopsDoNotStretchSpeech()
        {
            var withStop = LipSyncTrack.Build("아아. 아아!", 2f, S);
            var bare = LipSyncTrack.Build("아아. 아아", 2f, S);
            Assert.AreEqual(bare.Length, withStop.Length, 1e-4f, "끝의 ! 는 박자에 넣지 않음");
            // "아아" 다음 마침표 · 띄어쓰기 구간은 닫힌 입
            float pause = 2f * withStop.Unit + 0.5f * S.stopUnits * withStop.Unit;
            Assert.AreEqual(0f, withStop.Sample(pause).Largest);
            Assert.AreEqual(0f, withStop.Sample(withStop.Length + 0.01f).Largest, "말이 끝나면 닫힘");
            Assert.AreEqual(0f, withStop.Sample(-0.01f).Largest);
            Assert.AreEqual(0f, LipSyncTrack.Build("...!?", 1f, S).Length);
            Assert.AreEqual(0f, LipSyncTrack.Build(null, 1f, S).Length);
        }

        [Test]
        public void TimingFitsTheSpeakingTimeWithinSyllableLimits()
        {
            const string line = "포항에서 왔습니다. 윤태석입니다.";
            var fit = LipSyncTrack.Build(line, 3.5f * 0.85f, S);
            Assert.AreEqual(14, fit.Syllables);
            Assert.AreEqual(3.5f * 0.85f, fit.Length, 1e-3f);
            Assert.That(fit.Unit, Is.InRange(S.syllableMin, S.syllableMax));

            var rushed = LipSyncTrack.Build(line, 0.3f, S);
            Assert.AreEqual(S.syllableMin, rushed.Unit, 1e-6f, "자막이 짧아도 음절은 최소 길이");
            var slow = LipSyncTrack.Build("만수야!", 20f, S);
            Assert.AreEqual(S.syllableMax, slow.Unit, 1e-6f, "자막이 길어도 음절을 늘어뜨리지 않음");
            Assert.AreEqual(3f * S.syllableMax, slow.Length, 1e-4f);
        }

        [Test]
        public void SyllablesVaryButStayNearTheirShape()
        {
            var track = LipSyncTrack.Build("아아아아아아", 1.2f, S);
            var seen = new HashSet<float>();
            for (int i = 0; i < 6; i++)
            {
                float jaw = At(track, i, 0.5f).jaw;
                Assert.That(jaw, Is.InRange(S.openJaw * (1f - S.variation) - 1e-4f, S.openJaw * (1f + S.variation) + 1e-4f));
                seen.Add(Mathf.Round(jaw * 1000f));
            }
            Assert.Greater(seen.Count, 1);
            var again = LipSyncTrack.Build("아아아아아아", 1.2f, S);
            Assert.AreEqual(At(track, 3, 0.5f).jaw, At(again, 3, 0.5f).jaw, "같은 문장은 늘 같은 입");
        }

        [Test]
        public void FollowerNeverJumpsEvenOnAHitchedFrame()
        {
            var follower = new MouthFollower();
            var open = new MouthPose(1f, 1f, 1f, 1f);
            var first = follower.Step(open, 0.5f, S); // 0.5 초 끊긴 프레임
            Assert.AreEqual(S.jawSpeed * S.maxStep, first.jaw, 1e-5f);
            Assert.AreEqual(S.lipSpeed * S.maxStep, first.close, 1e-5f);
            for (int i = 0; i < 60; i++) follower.Step(open, 1f / 60f, S);
            Assert.AreEqual(1f, follower.Current.jaw, 1e-5f);
            var back = follower.Step(MouthPose.Closed, 1f / 60f, S);
            Assert.AreEqual(1f - S.jawSpeed / 60f, back.jaw, 1e-5f);
            Assert.AreEqual(0.5f, new MouthPose(1f, 0.5f, 0.2f, 0f).Scaled(0.5f).jaw);
        }

        [Test]
        public void BlinksComeEveryFewSecondsAndOpenAgain()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var clock = new BlinkClock(seed);
                float t = 0f, lastStart = -1f, closedFor = 0f;
                bool closing = false;
                var gaps = new List<float>();
                var lengths = new List<float>();
                const float dt = 1f / 120f;
                while (t < 60f)
                {
                    float w = clock.Step(dt, 2f, 6f, 0.1f, 0.15f);
                    Assert.That(w, Is.InRange(0f, 1f));
                    if (w > 0f && !closing) { closing = true; if (lastStart >= 0f) gaps.Add(t - lastStart); lastStart = t; closedFor = 0f; }
                    if (closing) closedFor += dt;
                    if (w == 0f && closing) { closing = false; lengths.Add(closedFor); }
                    t += dt;
                }
                Assert.GreaterOrEqual(gaps.Count, 8, "1분에 여러 번");
                foreach (float g in gaps) Assert.That(g, Is.InRange(2f + 0.08f, 6f + 0.18f));
                foreach (float l in lengths) Assert.That(l, Is.InRange(0.1f - 0.02f, 0.15f + 0.02f));
            }
        }

        [Test]
        public void EverySpokenLineHasSomeoneToSayIt()
        {
            var text = ScriptableObject.CreateInstance<GameText>();
            var face = ScriptableObject.CreateInstance<FaceConfig>();
            try
            {
                int lines = 0;
                foreach (var field in typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.FieldType != typeof(GameText.Line)) continue;
                    var line = (GameText.Line)field.GetValue(text);
                    if (string.IsNullOrEmpty(line.speaker)) continue;
                    lines++;
                    Assert.IsNotEmpty(face.ActorFor(line.speaker), $"{field.Name}: 화자 {line.speaker} 의 인물이 FaceConfig 에 없음");
                    Assert.Greater(LipSyncTrack.Build(line.text, line.duration * face.speechFill, face.lipSync).Length, 0f, field.Name);
                }
                Assert.Greater(lines, 5);
                Assert.Less(face.MouthLimit(FaceModel.Student), face.MouthLimit(FaceModel.Regular), "학도병 입은 낮게 묶음");
            }
            finally
            {
                Object.DestroyImmediate(text);
                Object.DestroyImmediate(face);
            }
        }
    }
}
