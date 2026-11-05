using System.Collections;
using System.Linq;
using System.Reflection;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.Tests
{
    public sealed class IntegrationRegressionTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Fields).SetValue(owner, value);

        [Test]
        public void BazookaRetainsNumberKeyAndAddsTab()
        {
            using (var controls = new GameControls())
            {
                var bindings = controls.Weapon2.bindings.Select(b => b.path).ToArray();
                CollectionAssert.Contains(bindings, "<Keyboard>/2");
                CollectionAssert.Contains(bindings, "<Keyboard>/tab");
            }
        }

        [Test]
        public void DawnObjectiveIgnoresHiddenCombatParentOnlyUntilNormalShowOrHide()
        {
            var root = new GameObject("Objective integration test", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var combat = root.AddComponent<CanvasGroup>();
                combat.alpha = 0f;
                var child = new GameObject("Objective", typeof(RectTransform), typeof(CanvasGroup));
                child.transform.SetParent(root.transform);
                var group = child.GetComponent<CanvasGroup>();
                var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(child.transform);
                var display = root.AddComponent<ObjectiveDisplay>();
                Set(display, "group", group);
                Set(display, "objective", text.GetComponent<TextMeshProUGUI>());
                display.Show("Look behind", ignoreParentGroups: true);
                Assert.IsTrue(group.ignoreParentGroups);
                Assert.AreEqual(0f, combat.alpha, "Dawn cue must not enable combat HUD.");
                display.Show("Look behind");
                Assert.IsFalse(group.ignoreParentGroups, "Even the same text must restore normal parent visibility.");
                display.Show("Look behind", ignoreParentGroups: true);
                display.Hide();
                Assert.IsFalse(group.ignoreParentGroups);
                Assert.AreEqual("", display.Current);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DeathFadeReportsOnlyAnUnprocessedDeathInItsOriginalSection(int scenario)
        {
            var root = new GameObject("Death fade integration test");
            root.SetActive(false);
            var cursor = Cursor.lockState;
            bool visible = Cursor.visible;
            float timeScale = Time.timeScale;
            GameControls controls = null;
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<FlowConfig>("Assets/_Project/Data/FlowConfig.asset");
                var tank = AssetDatabase.LoadAssetAtPath<TankConfig>("Assets/_Project/Data/TankConfig.asset");
                var assist = AssetDatabase.LoadAssetAtPath<AssistConfig>("Assets/_Project/Data/AssistConfig.asset");
                var playerConfig = AssetDatabase.LoadAssetAtPath<PlayerConfig>("Assets/_Project/Data/PlayerConfig.asset");
                var flow = new DemoFlow(config, tank, assist);
                flow.StartRun();
                flow.DebugJumpTo(scenario == 1 ? FlowSection.Tank : FlowSection.Scout);
                var director = root.AddComponent<FlowDirector>();
                typeof(FlowDirector).GetProperty("Flow").SetValue(director, flow);
                var gate = root.AddComponent<InputGate>();
                gate.Mode = ControlMode.Full;
                var player = root.AddComponent<PlayerController>();
                controls = new GameControls();
                typeof(PlayerController).GetProperty("Controls").SetValue(player, controls);
                Set(player, "gate", gate);
                var health = root.AddComponent<PlayerHealth>();
                Set(health, "config", playerConfig);
                var run = root.AddComponent<RunCoordinator>();
                Set(run, "director", director);
                Set(run, "player", player);
                Set(run, "health", health);
                Set(run, "deathFadeTime", 1f);
                int retries = 0;
                flow.RetryRequested += _ => { retries++; Set(run, "retried", true); };
                var sequence = (IEnumerator)typeof(RunCoordinator).GetMethod("DeathSequence", Fields).Invoke(run, null);
                Assert.IsTrue(sequence.MoveNext(), "Death fade must yield before reporting the death.");
                if (scenario == 1) flow.Tick(flow.TankAttemptLimit + .01);
                if (scenario == 2) flow.ReportGoalMet(FlowSection.Scout);
                Set(run, "deathFadeTime", 0f);
                Assert.IsFalse(sequence.MoveNext());
                Assert.AreEqual(scenario == 2 ? 0 : 1, retries);
                Assert.AreEqual(scenario == 0 ? 1 : 0, flow.Checkpoints.Failures(FlowCheckpoint.FirstContact));
                Assert.AreEqual(scenario == 1 ? 1 : 0, flow.Checkpoints.Failures(FlowCheckpoint.Tank));
                Assert.IsTrue(gate.CanAct, "Death input blocker must always be released.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                controls?.Dispose();
                Cursor.lockState = cursor;
                Cursor.visible = visible;
                Time.timeScale = timeScale;
            }
        }
    }
}