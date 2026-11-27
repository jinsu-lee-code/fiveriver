using FiveRivers.UI;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 타이틀의 "운영자 재시작 Ctrl + Shift + R" 은 전시판에서만 (2026-11-27, 검수 itch_20261126/06_title_a)
    public sealed class TitleOperatorHintTests
    {
        [Test]
        public void TheOperatorHintShowsOnlyOnTheExhibitionRun()
        {
            Assert.IsFalse(TitleScreen.ShowsOperatorHint(true));  // general release (IsExpanded)
            Assert.IsTrue(TitleScreen.ShowsOperatorHint(false));  // exhibition run (legacyExhibitMode)
        }
    }
}
