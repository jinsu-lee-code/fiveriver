using FiveRivers.Combat;
using NUnit.Framework;

namespace FiveRivers.Tests
{
    // 로켓포반 탄약수 진행 규칙 (순수 C#, 엔진 없음)
    public sealed class LoaderTeamTests
    {
        LoaderTeamSettings settings;
        LoaderTeam team;
        LoaderInput input;
        int completed, aborted, taps, substituted, inserted, started;

        [SetUp]
        public void SetUp()
        {
            settings = new LoaderTeamSettings();
            Build();
        }

        void Build()
        {
            team = new LoaderTeam(settings);
            completed = aborted = taps = substituted = inserted = started = 0;
            team.LoadCompleted += () => completed++;
            team.LoadAborted += () => aborted++;
            team.Tapped += _ => taps++;
            team.SubstituteBegan += () => substituted++;
            team.Inserted += () => inserted++;
            team.LoadStarted += () => started++;
            input = new LoaderInput { Active = true, Present = true, SlotGap = 0f, GunnerGap = 0f, CanFollow = true, GunnerOk = true, TubeLoaded = true, Reserve = 4 };
        }

        void Run(float seconds, float step = 0.05f)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += step) team.Tick(step, input);
        }

        void Settle()
        {
            Run(settings.KneelInLength + 0.5f);
            Assert.AreEqual(LoaderState.Loaded, team.State);
        }

        // 쏘았다: 발사관이 비고 장전이 요청된다 (Bazooka.Launch → TryReload)
        void Fire(bool reloadWanted = true)
        {
            input.TubeLoaded = false;
            input.ReloadWanted = reloadWanted && input.Reserve > 0;
        }

        [Test]
        public void ArrivesAtSlotAndKneelsBeforeReady()
        {
            input.SlotGap = 4f;
            team.Tick(0.05f, input);
            Assert.AreEqual(LoaderState.Moving, team.State);
            Assert.IsTrue(team.Walking);
            input.SlotGap = 0f;
            team.Tick(0.05f, input);
            Assert.AreEqual(LoaderAction.KneelIn, team.Action);
            Assert.IsFalse(team.Walking);
            Run(settings.KneelInLength + 0.2f);
            Assert.AreEqual(LoaderState.Loaded, team.State);
            Assert.AreEqual(LoaderAction.KneelIdle, team.Action);
        }

        [Test]
        public void EmptyTubeLoadsAutomaticallyAfterFiring()
        {
            Settle();
            Fire();
            team.Tick(0.05f, input);
            Assert.AreEqual(LoaderState.Loading, team.State);
            Assert.AreEqual(LoaderAction.Load, team.Action);
            Assert.AreEqual(1, started);
        }

        [Test]
        public void LoadTakesExactlyTheConfiguredReloadTime()
        {
            foreach (float reload in new[] { 2.2f, 3.0f, 1.8f })
            {
                settings.ReloadTime = reload;
                Build();
                Settle();
                Fire();
                Run(reload - 0.06f);
                Assert.AreEqual(LoaderState.Loading, team.State, "not yet at " + reload);
                Assert.AreEqual(0, completed);
                Run(0.12f);
                Assert.AreEqual(LoaderState.Loaded, team.State, "done at " + reload);
                Assert.AreEqual(1, completed);
            }
        }

        [Test]
        public void ClipSpeedFitsReadyCueToReloadTime()
        {
            settings.ReloadTime = 2.2f;
            Assert.AreEqual(1.92f / 2.2f, new LoaderTeam(settings).ClipSpeed, 1e-4f);
            settings.ReloadTime = 0.1f;
            Assert.AreEqual(settings.MaxClipSpeed, new LoaderTeam(settings).ClipSpeed, 1e-4f);
        }

        [Test]
        public void CuesComeOncePerLoadAndNeverBeforeInsert()
        {
            Settle();
            Fire();
            Run(0.3f);
            Assert.AreEqual(0, inserted);
            Run(settings.ReloadTime);
            input.TubeLoaded = true; input.ReloadWanted = false; input.Reserve = 3;
            Run(1f);
            Assert.AreEqual(1, inserted);
            Assert.AreEqual(2, taps);
        }

        [Test]
        public void CannotFireWhileLoading()
        {
            Settle();
            Fire();
            Run(1f);
            // 발사관은 아직 비어 있다: 바주카는 Reloading 동안 방아쇠를 받지 않는다 (Bazooka.Tick)
            Assert.AreEqual(LoaderState.Loading, team.State);
            Assert.IsFalse(input.TubeLoaded);
            Assert.AreEqual(0, completed);
        }

        [Test]
        public void PausesWhileGunnerIsCrouchedAndCountsOnlyStandingTime()
        {
            Settle();
            Fire();
            Run(1.0f);
            input.GunnerOk = false;
            Run(3.0f);
            Assert.IsTrue(team.Paused);
            Assert.AreEqual(LoaderState.Loading, team.State);
            Assert.AreEqual(0, completed);
            input.GunnerOk = true;
            Run(settings.ReloadTime - 1.0f + 0.1f);
            Assert.AreEqual(1, completed);
            Assert.IsFalse(team.Paused);
        }

        [Test]
        public void DoesNotStartLoadWhileGunnerIsCrouchedAndDoesNotGiveUp()
        {
            Settle();
            input.GunnerOk = false;
            Fire();
            Run(settings.GiveUpSeconds + 3f);
            Assert.AreNotEqual(LoaderState.Loading, team.State);
            Assert.AreEqual(0, substituted);
            input.GunnerOk = true;
            Run(0.1f);
            Assert.AreEqual(LoaderState.Loading, team.State);
        }

        [Test]
        public void GunnerFarAwayAbortsLoadAndFollowsHim()
        {
            Settle();
            Fire();
            Run(0.8f);
            input.GunnerGap = 2.5f;
            input.SlotGap = 2.5f;
            team.Tick(0.05f, input);
            Assert.AreEqual(1, aborted);
            Assert.AreEqual(LoaderState.Moving, team.State);
            Assert.IsTrue(input.ReloadWanted, "the request stays: it loads again when he is back");
            input.GunnerGap = input.SlotGap = 0f;
            Run(settings.KneelInLength + 0.3f);
            Run(0.1f);
            Assert.AreEqual(LoaderState.Loading, team.State);
            Assert.AreEqual(0, completed);
        }

        [Test]
        public void WaitingTooLongForLoaderMakesGunnerReloadAlone()
        {
            input.SlotGap = 6f; // 탄약수는 먼 자리에서 움직이지 못하는 상황
            input.CanFollow = true;
            Fire();
            Run(settings.GiveUpSeconds + 0.5f);
            Assert.AreEqual(1, substituted);
            Assert.AreEqual(LoaderState.Substitute, team.State);
        }

        [Test]
        public void NoLoaderMeansSubstituteMode()
        {
            input.Present = false;
            team.Tick(0.05f, input);
            Assert.AreEqual(LoaderState.Substitute, team.State);
            Assert.IsTrue(team.Unavailable);
            Assert.AreEqual(1, substituted);
            Run(2f);
            Assert.AreEqual(1, substituted);
            input.Present = true;
            team.Tick(0.05f, input);
            Assert.IsFalse(team.Unavailable);
        }

        [Test]
        public void InactiveSectionKeepsLoaderIdle()
        {
            input.Active = false;
            Run(3f);
            Assert.AreEqual(LoaderState.Idle, team.State);
            Assert.AreEqual(LoaderAction.None, team.Action);
            Assert.IsFalse(team.Deployed);
        }

        [Test]
        public void ResetOnRetryClearsEverythingEvenMidLoad()
        {
            Settle();
            Fire();
            Run(1f);
            team.Reset();
            Assert.AreEqual(1, aborted);
            Assert.AreEqual(LoaderState.Idle, team.State);
            Assert.AreEqual(LoaderAction.None, team.Action);
            Assert.IsFalse(team.Deployed);
            Assert.AreEqual(0f, team.LoadElapsed);
            input.TubeLoaded = true; input.ReloadWanted = false; input.Reserve = 4;
            team.Tick(0.05f, input);
            Assert.AreEqual(LoaderAction.KneelIn, team.Action); // 처음부터 다시
        }

        [Test]
        public void ForceReadyPutsLoaderAtPostKneeling()
        {
            team.ForceReady();
            Assert.AreEqual(LoaderState.Ready, team.State);
            Assert.AreEqual(LoaderAction.KneelIdle, team.Action);
            team.Tick(0.05f, input);
            Assert.AreEqual(LoaderState.Loaded, team.State);
        }

        [Test]
        public void ClearingPlaysToRifleThenReturnsIdle()
        {
            Settle();
            int cleared = 0;
            team.Cleared += () => cleared++;
            team.BeginClearing();
            Assert.AreEqual(LoaderState.Clearing, team.State);
            Assert.AreEqual(LoaderAction.ToRifle, team.Action);
            Assert.AreEqual(settings.ToRifleLength, team.ClearSecondsLeft, 1e-3f);
            input.Active = false;
            Run(settings.ToRifleLength + 0.2f);
            Assert.AreEqual(LoaderState.Idle, team.State);
            Assert.AreEqual(1, cleared);
            Assert.IsFalse(team.Deployed);
        }

        [Test]
        public void ClearingMidLoadAbortsIt()
        {
            Settle();
            Fire();
            Run(1f);
            team.BeginClearing();
            Assert.AreEqual(1, aborted);
            Assert.AreEqual(0, completed);
            Assert.AreEqual(LoaderState.Clearing, team.State);
        }

        [Test]
        public void ClearingWithoutEverDeployingDoesNothing()
        {
            team.BeginClearing();
            Assert.AreEqual(LoaderState.Idle, team.State);
            Assert.AreEqual(0f, team.ClearSecondsLeft);
        }

        [Test]
        public void SupportEntersCleanlyFromAnyState()
        {
            // 아군 지원 · 구간 이탈: Active 가 꺼지면 진행 중인 장전은 접고 Idle
            Settle();
            Fire();
            Run(1f);
            input.Active = false;
            team.Tick(0.05f, input);
            Assert.AreEqual(1, aborted);
            Assert.AreEqual(LoaderState.Idle, team.State);
        }

        [Test]
        public void LastRocketFiredBracesOnlyAndReachesAfterResupply()
        {
            input.Reserve = 0;
            Settle();
            Fire();
            Assert.IsFalse(input.ReloadWanted);
            team.Tick(0.05f, input);
            Assert.AreEqual(LoaderAction.Brace, team.Action);
            Run(settings.BraceLength + 0.3f);
            Assert.AreEqual(LoaderAction.Brace, team.Action, "holds the last pose: nothing to pick up");
            input.Reserve = 4; // F 보충
            Run(0.1f);
            Assert.AreEqual(LoaderAction.Reach, team.Action);
        }

        [Test]
        public void AfterLoadPicksNextRocketThenKneelsIdle()
        {
            Settle();
            Fire();
            Run(settings.ReloadTime + 0.1f);
            input.TubeLoaded = true; input.ReloadWanted = false; input.Reserve = 3;
            Run(1.0f);
            Assert.AreEqual(LoaderAction.Reach, team.Action);
            Run(settings.ReachLength + 0.2f);
            Assert.AreEqual(LoaderAction.KneelIdle, team.Action);
        }

        [Test]
        public void FarGunnerGetsAWave()
        {
            input.CanFollow = false; // 둔덕에 아직 들어오지 않았다
            input.SlotGap = 0f;
            input.GunnerGap = 5f;
            Run(settings.KneelInLength + 0.5f);
            Assert.AreEqual(LoaderAction.Wave, team.Action);
        }
    }
}
