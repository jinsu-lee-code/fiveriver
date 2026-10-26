using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FiveRivers.Tests
{
    // ANIM_SPEC 공통 체크 N1 · N2 · L1 · P3와 1-5 매개변수(변경 금지). 최종 클립으로 덮어쓴 뒤에도 이 테스트로 확인한다.
    public class AnimSpecTests
    {
        const string Dir = "Assets/_Project/Art/Animations/";

        static readonly string[] Loops =
        {
            "KPA/A_KPA_Idle_Aim", "KPA/A_KPA_Walk_Rifle", "KPA/A_KPA_Run_Rifle", "KPA/A_KPA_CrouchRun", "KPA/A_KPA_Crouch_Aim",
            "ROK/A_ROK_Idle_Trench", "ROK/A_ROK_Walk_Tired", "ROK/A_ROK_Idle_Stand", "FP/A_FP_M1_Idle", "FP/A_FP_M1_Sprint", "FP/A_FP_Bazooka_Idle",
        };

        static readonly string[] OneShots =
        {
            "KPA/A_KPA_Fire_Stand", "KPA/A_KPA_Fire_Crouch", "KPA/A_KPA_HitReact", "KPA/A_KPA_Death_01", "KPA/A_KPA_Death_02", "KPA/A_KPA_Death_03",
            "ROK/A_ROK_Fire_Crouch", "ROK/A_ROK_Call", "FP/A_FP_M1_ReloadEmpty", "FP/A_FP_M1_ReloadPartial", "FP/A_FP_M1_Equip", "FP/A_FP_M1_Unequip",
            "FP/A_FP_Resupply", "FP/A_FP_Bazooka_Equip", "FP/A_FP_Bazooka_Fire", "FP/A_FP_Bazooka_Reload",
        };

        static readonly string[] AllowedEvents =
            { "Fire", "ClipEject", "ClipRemove", "ClipInsert", "BoltRelease", "ReloadDone", "RocketInsert", "EquipDone", "UnequipDone", "AmmoAdded", "VoiceStart", "Footstep" };

        static AnimationClip ClipOf(string rel)
        {
            string name = rel.Substring(rel.LastIndexOf('/') + 1);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Dir + rel + ".fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            Assert.AreEqual(1, clips.Length, "파일 하나에 클립 하나: " + rel);
            Assert.AreEqual(name, clips[0].name, "클립 이름 = 파일명 (N2)");
            return clips[0];
        }

        [Test]
        public void LoopClips_AreLooping()
        {
            foreach (var rel in Loops) Assert.IsTrue(ClipOf(rel).isLooping, "Loop Time: " + rel);
        }

        [Test]
        public void OneShotClips_NotLooping_EventsAllowed()
        {
            foreach (var rel in OneShots.Concat(Loops))
            {
                var clip = ClipOf(rel);
                if (OneShots.Contains(rel)) Assert.IsFalse(clip.isLooping, rel);
                foreach (var e in AnimationUtility.GetAnimationEvents(clip))
                    CollectionAssert.Contains(AllowedEvents, e.functionName, "이벤트 이름 (1-6): " + rel);
            }
        }

        [TestCase("AC_KPA", "Speed:Float,Crouch:Bool,Aim:Bool,Fire:Trigger,Hit:Trigger,Die:Trigger,DieIndex:Int,Retreat:Bool")]
        [TestCase("AC_ROK", "Speed:Float,Crouch:Bool,Fire:Trigger,Call:Trigger")]
        [TestCase("AC_FP_M1", "Fire:Trigger,ReloadEmpty:Trigger,ReloadPartial:Trigger,Sprint:Bool,Equip:Trigger,Unequip:Trigger,Resupply:Trigger")]
        [TestCase("AC_FP_Bazooka", "Fire:Trigger,Reload:Trigger,Equip:Trigger,Unequip:Trigger")]
        public void Controllers_HaveSpecParameters(string controller, string spec)
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + controller + ".controller");
            Assert.IsNotNull(ac, controller);
            var want = spec.Split(',').OrderBy(s => s).ToArray();
            var have = ac.parameters.Select(p => $"{p.name}:{p.type}").OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(want, have, controller + " 매개변수 (ANIM_SPEC 1-5, 변경 금지)");
            foreach (var s in ac.layers[0].stateMachine.states)
                Assert.IsNotNull(s.state.motion, $"{controller}/{s.state.name} 상태의 Motion 연결 (N8)");
        }
    }
}
