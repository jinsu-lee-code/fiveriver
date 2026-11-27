using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // 맞는 순간 계산 (부족한 점 6번, 2026-11-26)
    public sealed class HitReactionTests
    {
        static readonly Vector3[] From = { new Vector3(0, 1.0f, 0), new Vector3(0, 1.45f, 0), new Vector3(0, 0.9f, 0), new Vector3(-0.2f, 1.4f, 0), new Vector3(0.1f, 0.9f, 0) };
        static readonly Vector3[] To = { new Vector3(0, 1.45f, 0), new Vector3(0, 1.75f, 0), new Vector3(0, 1.0f, 0), new Vector3(-0.5f, 1.1f, 0), new Vector3(0.12f, 0.45f, 0) };
        static readonly HitReaction.Part[] Parts = { HitReaction.Part.Chest, HitReaction.Part.Head, HitReaction.Part.Hips, HitReaction.Part.LeftArm, HitReaction.Part.RightLeg };

        [Test]
        public void APointBelongsToTheNearestBone()
        {
            Assert.AreEqual(HitReaction.Part.Head, HitReaction.Classify(new Vector3(0.05f, 1.65f, 0.08f), From, To, Parts));
            Assert.AreEqual(HitReaction.Part.Chest, HitReaction.Classify(new Vector3(0.0f, 1.25f, 0.12f), From, To, Parts));
            Assert.AreEqual(HitReaction.Part.LeftArm, HitReaction.Classify(new Vector3(-0.42f, 1.2f, 0.02f), From, To, Parts));
            Assert.AreEqual(HitReaction.Part.RightLeg, HitReaction.Classify(new Vector3(0.15f, 0.6f, 0.05f), From, To, Parts));
            Assert.AreEqual(0.5f, HitReaction.DistanceToSegment(new Vector3(0.5f, 0.5f, 0), Vector3.zero, Vector3.up), 1e-5f);
        }

        [Test]
        public void TheFlinchRisesFastAndFadesAway()
        {
            Assert.AreEqual(0f, HitReaction.Flinch(0f, 0.05f, 0.25f));
            Assert.AreEqual(1f, HitReaction.Flinch(0.05f, 0.05f, 0.25f), 1e-4f);
            Assert.Greater(HitReaction.Flinch(0.025f, 0.05f, 0.25f), 0f);
            Assert.Less(HitReaction.Flinch(0.2f, 0.05f, 0.25f), 1f);
            Assert.AreEqual(0f, HitReaction.Flinch(0.31f, 0.05f, 0.25f));
            // never above 1 and never a jump of more than a quarter in a 60 fps frame
            float last = 0f;
            for (float t = 0f; t < 0.4f; t += 1f / 60f)
            {
                float w = HitReaction.Flinch(t, 0.05f, 0.25f);
                Assert.That(w, Is.InRange(0f, 1f));
                Assert.Less(Mathf.Abs(w - last), 0.55f);
                last = w;
            }
        }

        [Test]
        public void TheShotSideIsReadFromWhereHeFaces()
        {
            Assert.AreEqual(0, HitReaction.Side(Vector3.forward, new Vector3(0.2f, 1f, 5f)));   // shot from in front
            Assert.AreEqual(1, HitReaction.Side(Vector3.forward, new Vector3(0f, 0f, -5f)));    // from behind
            Assert.AreEqual(2, HitReaction.Side(Vector3.forward, new Vector3(5f, 0f, 0.5f)));   // from the side
            Assert.AreEqual(2, HitReaction.Side(Vector3.forward, Vector3.zero));
        }

        // 부족한 점 3번 (2026-11-26): 살아 있는 동안 래그돌은 아무것도 하지 않는다. 시신은 총알 · 시야를 막지 않는다.
        [Test]
        public void TheEnemyRagdollSleepsWhileHeLivesAndStopsNoRound()
        {
            foreach (var path in new[] { "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman.prefab", "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman_B.prefab" })
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                var ragdoll = prefab.GetComponent<EnemyRagdoll>();
                Assert.IsNotNull(ragdoll, path + ": EnemyRagdoll (메뉴 FiveRivers/Setup/Enemy Ragdoll)");
                Assert.AreEqual(11, ragdoll.Bodies.Length, path);
                Assert.IsNotNull(ragdoll.Config, path);
                foreach (var rb in ragdoll.Bodies)
                {
                    Assert.IsTrue(rb.isKinematic, rb.name + ": 살아 있는 동안 Kinematic");
                    Assert.AreEqual(Core.WorldQuery.RagdollLayer, rb.gameObject.layer, rb.name + ": Ragdoll 층");
                    foreach (var c in rb.GetComponents<Collider>()) Assert.IsFalse(c.enabled, rb.name + ": 살아 있는 동안 충돌체 꺼짐");
                }
                foreach (var j in prefab.GetComponentsInChildren<CharacterJoint>(true))
                {
                    Assert.LessOrEqual(j.highTwistLimit.limit, 130f, j.name);
                    Assert.GreaterOrEqual(j.lowTwistLimit.limit, -40f, j.name);
                }
            }
            Assert.AreEqual(0, Core.WorldQuery.All & (1 << Core.WorldQuery.RagdollLayer), "총알 · 폭발 판정은 시신을 보지 않는다");
            Assert.AreEqual(0, Core.WorldQuery.Solid & (1 << Core.WorldQuery.RagdollLayer));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(Core.WorldQuery.RagdollLayer, 0), "지형과는 부딪힌다");
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(Core.WorldQuery.RagdollLayer, Core.WorldQuery.PlayerBlockerLayer));
        }

        [Test]
        public void TheFallFollowsTheShotWhenTheRollSaysSo()
        {
            var order = new[] { 2, 0, 1 };
            HitReaction.PreferBySide(order, 0, 0, 0.1f, 0.65f);
            CollectionAssert.AreEqual(new[] { 0, 2, 1 }, order, "앞에서 맞으면 뒤로 넘어지는 동작을 먼저");
            order = new[] { 0, 2, 1 };
            HitReaction.PreferBySide(order, 0, 1, 0.1f, 0.65f);
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, order, "뒤에서 맞으면 뒤로 넘어지는 동작은 마지막");
            order = new[] { 2, 0, 1 };
            HitReaction.PreferBySide(order, 0, 0, 0.9f, 0.65f);
            CollectionAssert.AreEqual(new[] { 2, 0, 1 }, order, "나머지 확률은 그대로 (변화)");
            HitReaction.PreferBySide(order, 0, 2, 0.1f, 0.65f);
            CollectionAssert.AreEqual(new[] { 2, 0, 1 }, order, "옆에서 맞으면 그대로");
        }
    }
}
