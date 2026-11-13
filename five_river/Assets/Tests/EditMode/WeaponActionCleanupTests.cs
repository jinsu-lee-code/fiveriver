using System.Reflection;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    public sealed class WeaponActionCleanupTests
    {
        static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        WeaponConfig config;
        WeaponHolder holder;
        PlayerController player;
        InputGate gate;
        M1Rifle rifle;
        Bazooka bazooka;
        int inserted, reloaded, switched;
        CursorLockMode previousLock;
        bool previousCursorVisible;

        static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Fields).Invoke(target, args);
        Transform Model(string name)
        {
            var model = new GameObject(name).transform;
            model.SetParent(root.transform);
            return model;
        }

        [SetUp]
        public void SetUp()
        {
            previousLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            root = new GameObject("Weapon action cleanup test");
            root.SetActive(false);
            config = ScriptableObject.CreateInstance<WeaponConfig>();
            config.m1ClipSize = 8;
            config.m1StartReserve = 40;
            config.m1MaxReserve = 64;
            config.m1ReloadPartialTime = 3.2f;
            config.bazookaRockets = 4;
            config.bazookaReloadTime = 4f;
            config.m1UnequipTime = .5f;
            config.bazookaEquipTime = .7f;
            config.hipFov = 60f;
            player = root.AddComponent<PlayerController>();
            Property(player, "Controls", new GameControls());
            gate = root.AddComponent<InputGate>();
            Set(player, "gate", gate);
            gate.Mode = ControlMode.Full;
            rifle = root.AddComponent<M1Rifle>();
            Set(rifle, "config", config);
            Set(rifle, "player", player);
            Set(rifle, "model", Model("Rifle model"));
            Property(rifle, "Magazine", new M1Magazine(8, 40, 64));
            bazooka = root.AddComponent<Bazooka>();
            Set(bazooka, "config", config);
            Set(bazooka, "player", player);
            Set(bazooka, "model", Model("Bazooka model"));
            bazooka.ResetAmmo();
            holder = root.AddComponent<WeaponHolder>();
            Set(holder, "config", config);
            Set(holder, "player", player);
            Set(holder, "view", root.AddComponent<Camera>());
            Set(holder, "m1", rifle);
            Set(holder, "bazooka", bazooka);
            inserted = reloaded = switched = 0;
            rifle.ClipInserted += () => inserted++;
            rifle.Reloaded += () => reloaded++;
            bazooka.Reloaded += () => reloaded++;
            holder.Switched += _ => switched++;
        }

        [TearDown]
        public void TearDown()
        {
            player.Controls.Dispose();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
            Cursor.lockState = previousLock;
            Cursor.visible = previousCursorVisible;
        }

        void BeginRifleReload()
        {
            rifle.Magazine.Fire(out _);
            Assert.IsTrue(rifle.TryReload());
        }
        void MakeRifleReloadDue()
        {
            Set(rifle, "reloadStart", Time.time - 4f);
            Set(rifle, "reloadEnd", Time.time - 1f);
        }

        [TestCase(FlowSection.Title)]
        [TestCase(FlowSection.Result)]
        public void MenuCancelsOverdueReloadWithoutAmmoOrLateCues(FlowSection section)
        {
            BeginRifleReload();
            MakeRifleReloadDue();
            gate.Mode = InputGate.ModeFor(section);
            Call(holder, "Update");
            Call(holder, "Update");
            Assert.IsFalse(rifle.Reloading);
            Assert.AreEqual(7, rifle.Magazine.InClip);
            Assert.AreEqual(40, rifle.Magazine.Reserve);
            Assert.AreEqual(0, inserted + reloaded);
            Assert.IsFalse(rifle.Model.gameObject.activeSelf);
        }

        [TestCase(FlowSection.Title)]
        [TestCase(FlowSection.Result)]
        public void MenuCancelsSwitchBeforeItCanComplete(FlowSection section)
        {
            Call(holder, "BeginSwitch", WeaponSlot.Bazooka);
            Set(holder, "hideAt", Time.time - 2f);
            Set(holder, "switchEnd", Time.time - 1f);
            gate.Mode = InputGate.ModeFor(section);
            Call(holder, "Update");
            Assert.IsFalse(holder.Switching);
            Assert.AreEqual(WeaponSlot.M1, holder.Current);
            Assert.AreEqual(0, switched);
            Assert.IsFalse(rifle.Model.gameObject.activeSelf);
            Assert.IsFalse(bazooka.Model.gameObject.activeSelf);
        }

        [TestCase(FlowSection.Title)]
        [TestCase(FlowSection.Result)]
        public void MenuAlsoCancelsBazookaReloadWithoutConsumingReserve(FlowSection section)
        {
            Property(holder, "Current", WeaponSlot.Bazooka);
            Property(bazooka, "Loaded", 0);
            Assert.IsTrue(bazooka.TryReload());
            Set(bazooka, "reloadEnd", Time.time - 1f);
            gate.Mode = InputGate.ModeFor(section);
            Call(holder, "Update");
            Assert.IsFalse(bazooka.Reloading);
            Assert.AreEqual(0, bazooka.Loaded);
            Assert.AreEqual(3, bazooka.Reserve);
            Assert.AreEqual(0, reloaded);
        }

        [Test]
        public void FullModeBlockerPreservesReloadAndCompletionAfterUnblock()
        {
            BeginRifleReload();
            Set(rifle, "reloadStart", Time.time);
            Set(rifle, "reloadEnd", Time.time + 10f);
            gate.Block(this);
            Call(holder, "Update");
            Assert.IsFalse(gate.CanAct);
            Assert.IsTrue(rifle.Reloading);
            Assert.AreEqual(0, inserted + reloaded);
            gate.Unblock(this);
            MakeRifleReloadDue();
            Call(holder, "Update");
            Assert.IsFalse(rifle.Reloading);
            Assert.AreEqual(8, rifle.Magazine.InClip);
            Assert.AreEqual(39, rifle.Magazine.Reserve);
            Assert.AreEqual(1, inserted);
            Assert.AreEqual(1, reloaded);
        }

        [Test]
        public void SwitchingShowsAndRaisesIncomingWeaponOnceBeforeItCanAct()
        {
            int lowered = 0, raised = 0;
            holder.Lowering += (slot, _) =>
            {
                Assert.AreEqual(WeaponSlot.M1, slot);
                Assert.IsTrue(holder.Switching);
                lowered++;
            };
            holder.Raising += (slot, _) =>
            {
                Assert.AreEqual(WeaponSlot.Bazooka, slot);
                Assert.AreEqual(WeaponSlot.M1, holder.Current);
                Assert.IsTrue(holder.Switching);
                Assert.IsTrue(bazooka.Model.gameObject.activeSelf);
                Assert.IsFalse(rifle.Model.gameObject.activeSelf);
                raised++;
            };
            Call(holder, "BeginSwitch", WeaponSlot.Bazooka);
            Set(holder, "hideAt", Time.time - 1f);
            Set(holder, "switchEnd", Time.time + 10f);
            Call(holder, "Update");
            Call(holder, "Update");
            Assert.AreEqual(1, lowered);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(0, switched);
            Set(holder, "switchEnd", Time.time - .5f);
            Call(holder, "Update");
            Assert.AreEqual(1, raised);
            Assert.AreEqual(1, switched);
            Assert.AreEqual(WeaponSlot.Bazooka, holder.Current);
        }

        [Test]
        public void FullModeBlockerPreservesPendingSwitch()
        {
            Call(holder, "BeginSwitch", WeaponSlot.Bazooka);
            Set(holder, "hideAt", Time.time + 5f);
            Set(holder, "switchEnd", Time.time + 10f);
            gate.Block(this);
            Call(holder, "Update");
            Assert.IsTrue(holder.Switching);
            Assert.AreEqual(0, switched);
            gate.Unblock(this);
            Set(holder, "switchEnd", Time.time - 1f);
            Call(holder, "Update");
            Assert.IsFalse(holder.Switching);
            Assert.AreEqual(WeaponSlot.Bazooka, holder.Current);
            Assert.AreEqual(1, switched);
        }
    }
}
