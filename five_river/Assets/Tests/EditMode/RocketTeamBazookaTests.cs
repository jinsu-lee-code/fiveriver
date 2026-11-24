using System.Reflection;
using FiveRivers.Combat;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // Bazooka 의 탄약수 장전 연결 (엔진 필요). 컴파일 위험이 있으면 이 파일만 빼도 LoaderTeamTests 와 게임 코드는 그대로다.
    public sealed class RocketTeamBazookaTests
    {
        static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject root;
        WeaponConfig config;
        Bazooka bazooka;
        int started, reloaded, cancelled;

        static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
        static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Rocket team bazooka test");
            root.SetActive(false);
            config = ScriptableObject.CreateInstance<WeaponConfig>();
            config.bazookaRockets = 4;
            config.bazookaReloadTime = 2.2f;
            bazooka = root.AddComponent<Bazooka>();
            Set(bazooka, "config", config);
            var model = new GameObject("model").transform;
            model.SetParent(root.transform);
            Set(bazooka, "model", model);
            bazooka.ResetAmmo();
            started = reloaded = cancelled = 0;
            bazooka.ReloadStarted += () => started++;
            bazooka.Reloaded += () => reloaded++;
            bazooka.ExternalReloadCancelled += () => cancelled++;
            Property(bazooka, "Loaded", 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void LoaderControlledReloadWaitsForTheLoaderAndSkipsFirstPersonReload()
        {
            bazooka.LoaderControlled = true;
            Assert.IsTrue(bazooka.TryReload());
            Assert.IsTrue(bazooka.Reloading);
            Assert.IsTrue(bazooka.ExternalReload);
            Assert.AreEqual(0, started, "the first-person reload motion stays off");
            Assert.AreEqual(0, bazooka.Loaded);
            Assert.IsTrue(bazooka.CompleteReload());
            Assert.AreEqual(1, bazooka.Loaded);
            Assert.AreEqual(2, bazooka.Reserve);
            Assert.AreEqual(1, reloaded);
            Assert.IsFalse(bazooka.Reloading);
        }

        [Test]
        public void WithoutLoaderItReloadsAloneAsBefore()
        {
            Assert.IsTrue(bazooka.TryReload());
            Assert.IsFalse(bazooka.ExternalReload);
            Assert.AreEqual(1, started);
            Assert.IsFalse(bazooka.CompleteReload());
        }

        [Test]
        public void TakeOverTurnsAnExternalReloadIntoAnOrdinaryOne()
        {
            bazooka.LoaderControlled = true;
            bazooka.TryReload();
            Assert.IsTrue(bazooka.TakeOverReload());
            Assert.IsFalse(bazooka.ExternalReload);
            Assert.IsTrue(bazooka.Reloading);
            Assert.AreEqual(1, started);
        }

        [Test]
        public void CancelAndResetReportAnExternalReloadOnly()
        {
            bazooka.LoaderControlled = true;
            bazooka.TryReload();
            bazooka.CancelReload();
            Assert.AreEqual(1, cancelled);
            Assert.IsFalse(bazooka.Reloading);
            bazooka.TryReload();
            bazooka.ResetAmmo();
            Assert.AreEqual(2, cancelled);
            bazooka.LoaderControlled = false;
            Property(bazooka, "Loaded", 0);
            bazooka.TryReload();
            bazooka.CancelReload();
            Assert.AreEqual(2, cancelled);
        }
    }
}
