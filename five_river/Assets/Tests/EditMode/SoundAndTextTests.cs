using System.Linq;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEditor;

namespace FiveRivers.Tests
{
    // 소리 · 문구 연결이 빠지지 않았는지 (SOUND_LIST 공통 체크 S10 "덮어쓴 뒤 사운드 라이브러리 연결 유지")
    public class SoundAndTextTests
    {
        const string LibraryPath = "Assets/_Project/Data/SoundLibrary.asset";
        const string TextPath = "Assets/_Project/Data/GameText.asset";

        [Test]
        public void EveryLibraryEntry_HasClips()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            Assert.IsNotNull(lib);
            var missing = lib.entries.Where(e => e.clips == null || e.clips.Length == 0 || e.clips.Any(c => c == null)).Select(e => e.id).ToArray();
            CollectionAssert.IsEmpty(missing, string.Join(", ", missing));
        }

        [Test]
        public void EverySubtitleLine_HasVoiceInLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            var text = AssetDatabase.LoadAssetAtPath<GameText>(TextPath);
            var lines = new[] { text.scoutSpotted, text.flareUp, text.flareDown, text.retreat, text.parkCall1, text.parkCall2, text.tankArrives, text.tankRicochet, text.allySupport };
            foreach (var line in lines)
            {
                Assert.IsFalse(string.IsNullOrEmpty(line.text), "빈 자막");
                Assert.IsNotNull(lib.Find(line.voice), "음성 없음: " + line.voice);
            }
        }

        [Test]
        public void TutorialSteps_AreFilled()
        {
            var text = AssetDatabase.LoadAssetAtPath<GameText>(TextPath);
            Assert.AreEqual(5, text.tutorialSteps.Length);
            Assert.IsTrue(text.tutorialSteps.All(s => !string.IsNullOrEmpty(s.prompt)));
        }

        [Test]
        public void NoRewardSounds()
        {
            // 처치 보상음 · 히트마커 소리를 넣지 않는다 (CLAUDE.md 핵심 설계 규칙 6)
            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            Assert.IsFalse(lib.entries.Any(e => e.id.Contains("Kill") || e.id.Contains("Hitmarker") || e.id.Contains("Reward")));
        }
    }
}
