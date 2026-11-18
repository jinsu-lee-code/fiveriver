using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    // ArmBulk (BazookaArmBulk 의 순수 계산) — 바주카 1인칭 아래팔 소매 굵히기, 2026-11-18
    public class BazookaArmBulkTests
    {
        static readonly string[] Names = { "mixamorig:LeftArm", "mixamorig:LeftForeArm", "mixamorig:LeftHand", "mixamorig:LeftHandIndex1" };

        // Bind poses with the joints on the +z axis: upper (0,0,-.27), elbow (0,0,0), wrist (0,0,.2), index knuckle (0,0,.29).
        static Matrix4x4[] Binds() => new[]
        {
            Matrix4x4.Translate(new Vector3(0f, 0f, -.27f)).inverse,
            Matrix4x4.Translate(Vector3.zero).inverse,
            Matrix4x4.Translate(new Vector3(0f, 0f, .2f)).inverse,
            Matrix4x4.Translate(new Vector3(0f, 0f, .29f)).inverse,
        };

        static BoneWeight On(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        static float Radius(Vector3 v) => new Vector2(v.x, v.y).magnitude;

        [Test]
        public void FindsForearmAndHand_NotFingers()
        {
            Assert.IsTrue(ArmBulk.FindForearm(Names, out int fore, out int hand));
            Assert.AreEqual(1, fore);
            Assert.AreEqual(2, hand);
            Assert.IsFalse(ArmBulk.FindForearm(new[] { "Spine", "LeftArm" }, out _, out _));
        }

        // 2026-11-18: each arm skin lists the whole skeleton; the right sleeve must take the right forearm
        [Test]
        public void FindsTheForearmTheMeshIsSkinnedTo()
        {
            var names = new[] { "mixamorig:LeftForeArm", "mixamorig:LeftHand", "mixamorig:RightForeArm", "mixamorig:RightHand" };
            var weights = new[] { On(2), On(2), On(3), new BoneWeight { boneIndex0 = 0, weight0 = .1f, boneIndex1 = 2, weight1 = .9f } };
            Assert.IsTrue(ArmBulk.FindForearm(names, weights, out int fore, out int hand));
            Assert.AreEqual(2, fore);
            Assert.AreEqual(3, hand);
        }

        [Test]
        public void JointPosition_ReadsBindPose()
        {
            var p = ArmBulk.JointPosition(Binds()[2]);
            Assert.AreEqual(.2f, p.z, 1e-5f);
        }

        [Test]
        public void MidForearm_GrowsBySquareRootOfPad()
        {
            var curve = BazookaArmBulk.DefaultPad();
            var v = new Vector3(.03f, 0f, .1f);   // t = .5, radius 3 cm
            var moved = ArmBulk.Pad(v, Vector3.zero, new Vector3(0f, 0f, .2f), 0f, curve, 1f, .4f, .6f);
            float expected = Mathf.Sqrt(.03f * .03f + curve.Evaluate(.5f) * curve.Evaluate(.5f));
            Assert.AreEqual(expected, Radius(moved), 1e-5f);
            Assert.AreEqual(v.z, moved.z, 1e-6f, "moves only across the bone, never along it");
        }

        [Test]
        public void ThinSide_GrowsMoreThanWideSide()
        {
            var curve = BazookaArmBulk.DefaultPad();
            var wide = ArmBulk.Pad(new Vector3(.036f, 0f, .1f), Vector3.zero, new Vector3(0f, 0f, .2f), 0f, curve, 1f, .4f, .6f);
            var thin = ArmBulk.Pad(new Vector3(0f, .025f, .1f), Vector3.zero, new Vector3(0f, 0f, .2f), 0f, curve, 1f, .4f, .6f);
            Assert.Greater(Radius(thin) / .025f, Radius(wide) / .036f, "the flat forearm becomes rounder, like a cloth sleeve");
        }

        [Test]
        public void HandSkin_ElbowRoll_AndUpperArm_AreUntouched()
        {
            var curve = BazookaArmBulk.DefaultPad();
            var elbow = Vector3.zero; var wrist = new Vector3(0f, 0f, .2f);
            var palm = new Vector3(.04f, 0f, .23f);
            Assert.AreEqual(palm, ArmBulk.Pad(palm, elbow, wrist, 1f, curve, 1f, .4f, .6f), "hand weight 1");
            var cuff = new Vector3(.03f, 0f, .19f);
            Assert.AreEqual(cuff, ArmBulk.Pad(cuff, elbow, wrist, .6f, curve, 1f, .4f, .6f), "hand weight at skinTo");
            var roll = new Vector3(.065f, 0f, .015f);   // t .075: the rolled sleeve bunch at the elbow
            Assert.AreEqual(roll, ArmBulk.Pad(roll, elbow, wrist, 0f, curve, 1f, .4f, .6f));
            var upper = new Vector3(.05f, 0f, -.1f);
            Assert.AreEqual(upper, ArmBulk.Pad(upper, elbow, wrist, 0f, curve, 1.2f, .4f, .6f), "outside the curve's keys even with gain");
        }

        [Test]
        public void ZeroPadAndUnitGain_IsIdentity()
        {
            var flat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1.15f, 0f));
            var v = new Vector3(.02f, -.017f, .13f);
            Assert.AreEqual(v, ArmBulk.Pad(v, Vector3.zero, new Vector3(0f, 0f, .2f), 0f, flat, 1f, .4f, .6f));
        }

        [Test]
        public void Apply_MovesOnlyForearmCloth_AndReportsMaxMove()
        {
            var vertices = new[]
            {
                new Vector3(.03f, 0f, .1f),    // forearm
                new Vector3(0f, .025f, .16f),  // forearm, wrist side
                new Vector3(.04f, 0f, .25f),   // palm
                new Vector3(.06f, 0f, -.1f),   // upper arm
            };
            var weights = new[] { On(1), On(1), On(2), On(0) };
            var moved = ArmBulk.Apply(vertices, weights, Names, Binds(), BazookaArmBulk.DefaultPad(), 1f, .4f, .6f, out float max);
            Assert.IsNotNull(moved);
            Assert.Greater(Radius(moved[0]), .03f);
            Assert.Greater(Radius(moved[1]), .025f);
            Assert.AreEqual(vertices[2], moved[2]);
            Assert.AreEqual(vertices[3], moved[3]);
            Assert.Greater(max, 0f);
            Assert.LessOrEqual(max, .031f + 1e-4f, "never more than the largest pad");
        }

        [Test]
        public void Apply_RefusesWrongUnits()
        {
            var binds = Binds();
            binds[2] = Matrix4x4.Translate(new Vector3(0f, 0f, 20f)).inverse;   // a centimetre-unit mesh: forearm 20 "m"
            var moved = ArmBulk.Apply(new[] { Vector3.zero }, new[] { On(1) }, Names, binds, BazookaArmBulk.DefaultPad(), 1f, .4f, .6f, out _);
            Assert.IsNull(moved);
        }
    }
}
