using System.Collections.Generic;
using FiveRivers.Combat;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // Rigify 아군 클립의 순수 로직 (2026-11-18): 언제 낮추고 · 무엇을 틀고 · 교전 · 전진 · 새 회차에 어떻게 빠지나.
    public class AllyClipBrainTests
    {
        const float Dt = 1f / 30f;

        static readonly AllyClipBrain.ClipInfo[] Clips =
        {
            new AllyClipBrain.ClipInfo("Base", AllyClipRole.Base, 5f),
            new AllyClipBrain.ClipInfo("Enter", AllyClipRole.EnterFromStand, 1.5f),
            new AllyClipBrain.ClipInfo("Stand", AllyClipRole.ExitToStand, 1.5f),
            new AllyClipBrain.ClipInfo("Fire", AllyClipRole.ExitToFire, 0.67f),
            new AllyClipBrain.ClipInfo("Peek", AllyClipRole.Variation, 3.5f),
            new AllyClipBrain.ClipInfo("Check", AllyClipRole.Variation, 3.6f),
            new AllyClipBrain.ClipInfo("Wipe", AllyClipRole.Variation, 2.8f),
            new AllyClipBrain.ClipInfo("Duck", AllyClipRole.Duck, 1.6f),
            new AllyClipBrain.ClipInfo("Mourn", AllyClipRole.Story, 2f),
        };

        static AllyClipSituation Calm(float time) => new AllyClipSituation
        {
            Time = time, Alive = true, InTrenchSection = true, StillFor = 10f,
        };

        // runs the brain until 'until' seconds, changing the situation with 'edit'; returns every order given
        static List<(float time, AllyClipOrder order)> Run(AllyClipBrain brain, float from, float until, System.Func<AllyClipSituation, AllyClipSituation> edit = null)
        {
            var orders = new List<(float, AllyClipOrder)>();
            for (float t = from; t < until; t += Dt)
            {
                var s = Calm(t);
                if (edit != null) s = edit(s);
                var order = brain.Tick(s, Dt);
                if (order.Kind != AllyClipOrderKind.None) orders.Add((t, order));
            }
            return orders;
        }

        static AllyClipBrain Brain(AllyClipRoster roster = null, string name = "Ally_1") =>
            new AllyClipBrain(Clips, new AllyClipTiming(), roster ?? new AllyClipRoster(), AllyClipBrain.Seed(name));

        [Test]
        public void Standing_GoesDownThroughTheEnterClip_ThenTheLowWait()
        {
            var brain = Brain();
            var orders = Run(brain, 0f, 3f);
            Assert.That(orders.Count, Is.GreaterThanOrEqualTo(2));
            Assert.AreEqual("Enter", orders[0].order.Clip);
            Assert.IsTrue(orders[0].order.FromController, "first clip comes in from the controller's pose");
            Assert.That(orders[0].time, Is.InRange(0.1f - Dt, 0.9f + Dt), "staggered start");
            Assert.AreEqual("Base", orders[1].order.Clip);
            Assert.IsFalse(orders[1].order.FromController);
            Assert.AreEqual(0f, orders[1].order.StartTime, "the enter clip ends on the low wait's first frame");
            Assert.AreEqual(AllyClipPhase.Ready, brain.Phase);
        }

        [Test]
        public void NotYetStill_DoesNotGoDown()
        {
            var brain = Brain();
            var orders = Run(brain, 0f, 3f, s => { s.StillFor = 0.2f; return s; });
            Assert.IsEmpty(orders);
            Assert.AreEqual(AllyClipPhase.Off, brain.Phase);
        }

        [Test]
        public void AlreadyLow_DropsStraightIntoTheLowWait()
        {
            var brain = Brain();
            var orders = Run(brain, 0f, 0.2f, s => { s.Low = true; s.StillFor = 0f; return s; });
            Assert.AreEqual(1, orders.Count);
            Assert.AreEqual("Base", orders[0].order.Clip);
            Assert.IsTrue(orders[0].order.FromController);
            Assert.AreEqual(new AllyClipTiming().enterLowBlend, orders[0].order.Fade);
        }

        [Test]
        public void FightStarts_FromTheLowWait_KneelsThroughTheFireClip_ThenHandsBack()
        {
            var brain = Brain();
            Run(brain, 0f, 3f);
            Assert.AreEqual(AllyClipPhase.Ready, brain.Phase);
            var orders = Run(brain, 3f, 5f, s => { s.Engaged = true; return s; });
            Assert.AreEqual("Fire", orders[0].order.Clip);
            Assert.AreEqual(new AllyClipTiming().fireFade, orders[0].order.Fade, 0.0001f);
            Assert.That(orders[0].time, Is.LessThan(3f + Dt * 1.5f), "at once");
            Assert.AreEqual(AllyClipOrderKind.Leave, orders[1].order.Kind);
            Assert.That(orders[1].time - orders[0].time, Is.InRange(0.67f - 0.15f - Dt * 2, 0.67f - 0.15f + Dt * 2));
            Assert.AreEqual(AllyClipPhase.Off, brain.Phase);
        }

        [Test]
        public void FightStarts_WhileGoingDown_LeavesWithinTheFastFade()
        {
            var brain = Brain();
            var orders = Run(brain, 0f, 1.2f); // inside the 1.5 s enter clip
            Assert.AreEqual(AllyClipPhase.Entering, brain.Phase);
            var after = Run(brain, 1.2f, 1.4f, s => { s.Engaged = true; return s; });
            Assert.AreEqual(AllyClipOrderKind.Leave, after[0].order.Kind);
            Assert.That(after[0].order.LeaveSeconds, Is.LessThanOrEqualTo(0.12f + 0.0001f));
            Assert.That(after[0].time, Is.LessThan(1.2f + Dt * 1.5f));
        }

        [Test]
        public void SectionLeft_StandsUpThroughTheStandClip_ThenHandsBack()
        {
            var brain = Brain();
            Run(brain, 0f, 3f);
            var orders = Run(brain, 3f, 5f, s => { s.InTrenchSection = false; return s; });
            Assert.AreEqual("Stand", orders[0].order.Clip);
            Assert.AreEqual(AllyClipOrderKind.Leave, orders[1].order.Kind);
            Assert.AreEqual(AllyClipPhase.Off, brain.Phase);
        }

        [Test]
        public void TurningToSpeak_StandsUp_AndComesBackDownOnlyAfterTheWait()
        {
            var brain = Brain();
            Run(brain, 0f, 3f);
            var orders = Run(brain, 3f, 3.1f, s => { s.Turning = true; return s; });
            Assert.AreEqual("Stand", orders[0].order.Clip);
            // facing away while he speaks, then back to his post: not down again before reenterAfter
            Run(brain, 3.1f, 4.4f, s => { s.FacingAway = true; return s; });
            var back = Run(brain, 4.4f, 9f);
            Assert.AreEqual("Enter", back[0].order.Clip);
            Assert.That(back[0].time, Is.GreaterThanOrEqualTo(3f + new AllyClipTiming().reenterAfter - Dt));
        }

        [Test]
        public void AdvancingAndNewRun_LeaveAtOnce()
        {
            var brain = Brain();
            Run(brain, 0f, 3f);
            var advance = Run(brain, 3f, 3.1f, s => { s.Advancing = true; return s; });
            Assert.AreEqual(AllyClipOrderKind.Leave, advance[0].order.Kind);
            Assert.AreEqual(new AllyClipTiming().advanceOut, advance[0].order.LeaveSeconds, 0.0001f);

            var again = Brain(name: "Ally_2");
            Run(again, 0f, 3f);
            var reset = Run(again, 3f, 3.05f, s => { s.Reset = true; return s; });
            Assert.AreEqual(AllyClipOrderKind.Leave, reset[0].order.Kind);
            Assert.AreEqual(0f, reset[0].order.LeaveSeconds);
        }

        [Test]
        public void SomethingElseTakesHim_LeavesQuickly()
        {
            var brain = Brain();
            Run(brain, 0f, 3f);
            var orders = Run(brain, 3f, 3.1f, s => { s.Busy = true; return s; });
            Assert.AreEqual(AllyClipOrderKind.Leave, orders[0].order.Kind);
            Assert.AreEqual(new AllyClipTiming().brokenOut, orders[0].order.LeaveSeconds, 0.0001f);
        }

        [Test]
        public void Variations_KeepTheIdleLifeRules()
        {
            var roster = new AllyClipRoster();
            var a = Brain(roster, "Ally_1");
            var b = Brain(roster, "Ally_2");
            var timing = new AllyClipTiming();
            var startsA = new List<(float t, string clip)>();
            var startsB = new List<(float t, string clip)>();
            for (float t = 0f; t < 240f; t += Dt)
            {
                var oa = a.Tick(Calm(t), Dt);
                var ob = b.Tick(Calm(t), Dt);
                if (oa.Kind == AllyClipOrderKind.Play && a.Phase == AllyClipPhase.Variation) startsA.Add((t, oa.Clip));
                if (ob.Kind == AllyClipOrderKind.Play && b.Phase == AllyClipPhase.Variation) startsB.Add((t, ob.Clip));
                // never the same move on two men at once
                if (a.Phase == AllyClipPhase.Variation && b.Phase == AllyClipPhase.Variation) Assert.AreNotEqual(a.Clip, b.Clip, $"t {t:0.0}");
            }
            Assert.That(startsA.Count, Is.GreaterThan(5));
            Assert.That(startsB.Count, Is.GreaterThan(5));
            // never the same move twice in a row
            for (int i = 1; i < startsA.Count; i++) Assert.AreNotEqual(startsA[i - 1].clip, startsA[i].clip);
            // never two men starting within the stagger
            foreach (var x in startsA) foreach (var y in startsB) Assert.That(Mathf.Abs(x.t - y.t), Is.GreaterThanOrEqualTo(timing.stagger - Dt));
            // the first move comes 2.5~9 s after he settled; the gap between moves is at least 6 s of the low wait
            Assert.That(startsA[0].t, Is.InRange(1.5f + timing.firstRest.x - 1f, 1.5f + 0.9f + timing.firstRest.y + 3f));
        }

        [Test]
        public void Cutscene_StartsNoNewMove()
        {
            var brain = Brain();
            var orders = Run(brain, 0f, 60f, s => { s.Cutscene = true; return s; });
            foreach (var o in orders) Assert.AreNotEqual(AllyClipPhase.Variation.ToString(), o.order.Clip);
            Assert.AreEqual(AllyClipPhase.Ready, brain.Phase);
            Assert.That(orders.Count, Is.EqualTo(2), "enter, low wait — nothing else");
        }

        [Test]
        public void CloseShell_Ducks_ThenBackToTheLowWait()
        {
            var brain = Brain();
            Run(brain, 0f, 3f);
            var orders = Run(brain, 3f, 3f + Dt * 1.5f, s => { s.Duck = true; return s; });
            Assert.AreEqual("Duck", orders[0].order.Clip);
            var after = Run(brain, 3.05f, 5f);
            Assert.AreEqual("Base", after[0].order.Clip);
            Assert.AreEqual(0f, after[0].order.StartTime);
            // a second cue right away is ignored (cooldown)
            var again = Run(brain, 5f, 5f + Dt * 1.5f, s => { s.Duck = true; return s; });
            Assert.IsEmpty(again);
        }

        [Test]
        public void StoryClip_TakesPrecedence_UntilStopped()
        {
            var brain = Brain();
            Assert.IsTrue(brain.RequestStory("Mourn", 0.4f, true));
            var orders = Run(brain, 0f, 5f, s => { s.InTrenchSection = false; s.Engaged = true; return s; });
            Assert.AreEqual(1, orders.Count);
            Assert.AreEqual("Mourn", orders[0].order.Clip);
            Assert.IsTrue(orders[0].order.FromController);
            Assert.AreEqual(AllyClipPhase.Story, brain.Phase, "held on its last frame");
            brain.StopStory(0.5f);
            var stop = Run(brain, 5f, 5.1f, s => { s.InTrenchSection = false; return s; });
            Assert.AreEqual(AllyClipOrderKind.Leave, stop[0].order.Kind);
            Assert.AreEqual(0.5f, stop[0].order.LeaveSeconds, 0.0001f);
            Assert.IsFalse(brain.RequestStory("NoSuchClip", 0.4f, true));
        }

        [Test]
        public void SameName_SameSchedule()
        {
            var a = Brain(new AllyClipRoster(), "Ally_3");
            var b = Brain(new AllyClipRoster(), "Ally_3");
            var oa = Run(a, 0f, 60f);
            var ob = Run(b, 0f, 60f);
            Assert.AreEqual(oa.Count, ob.Count);
            for (int i = 0; i < oa.Count; i++) { Assert.AreEqual(oa[i].order.Clip, ob[i].order.Clip); Assert.AreEqual(oa[i].time, ob[i].time, 0.0001f); }
        }

        [Test]
        public void Track_LoopsOverOneCycle_AndClampsAOneShot()
        {
            var frames = new AllyRifleTracks.Frame[4];
            for (int i = 0; i < 4; i++) frames[i] = new AllyRifleTracks.Frame { position = new Vector3(i, 0f, 0f), rotation = Quaternion.identity, gripLeft = i == 1 ? 0f : 1f, gripRight = 1f };
            frames[3].position = frames[0].position; // a loop ends where it starts
            // 30 fps: frame 1.5 at 0.05 s
            var mid = AllyRifleTracks.Sample(frames, 30f, 0.05f, false);
            Assert.AreEqual(1.5f, mid.position.x, 0.001f);
            Assert.AreEqual(0.5f, mid.gripLeft, 0.001f);
            // loop: one cycle = 3 frames = 0.1 s
            var wrapped = AllyRifleTracks.Sample(frames, 30f, 0.1f + 1f / 30f, true);
            Assert.AreEqual(1f, wrapped.position.x, 0.001f);
            // one-shot: holds the last frame
            var held = AllyRifleTracks.Sample(frames, 30f, 5f, false);
            Assert.AreEqual(0f, held.position.x, 0.001f);
            Assert.AreEqual(0f, AllyRifleTracks.FrameAt(1, 30f, 3f, true));
        }
    }
}
