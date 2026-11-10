using System.Reflection;
using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    public sealed class M1ShotHitTests
    {
        [Test]
        public void OwnCapsuleAndChildColliderDoNotBlockTheNextWorldHit()
        {
            var world = new GameObject("Shot test");
            try
            {
                world.transform.position = new Vector3(6000f, 6000f, 6000f);
                var shooter = Child(world.transform, "Shooter", 2f);
                shooter.AddComponent<CharacterController>();
                Child(shooter.transform, "Own equipment", 2f).AddComponent<BoxCollider>();
                var cover = Child(world.transform, "Cover", 6f).AddComponent<BoxCollider>();
                var target = Child(world.transform, "Target", 8f).AddComponent<BoxCollider>();
                var ray = new Ray(world.transform.position, Vector3.forward);
                var method = typeof(M1Rifle).GetMethod("TryGetShotHit", BindingFlags.NonPublic | BindingFlags.Static);
                object[] args = { ray, 12f, ~0, shooter.transform, null };

                Physics.SyncTransforms();
                Assert.IsTrue((bool)method.Invoke(null, args));
                Assert.AreSame(cover, ((RaycastHit)args[4]).collider);
                cover.enabled = false;
                Physics.SyncTransforms();
                Assert.IsTrue((bool)method.Invoke(null, args));
                Assert.AreSame(target, ((RaycastHit)args[4]).collider);
                target.enabled = false;
                Physics.SyncTransforms();
                Assert.IsFalse((bool)method.Invoke(null, args));
            }
            finally
            {
                Object.DestroyImmediate(world);
            }
        }

        static GameObject Child(Transform parent, string name, float z)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = Vector3.forward * z;
            return child;
        }
    }
}
