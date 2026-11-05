using System.Collections.Generic;
using System.Reflection;
using FiveRivers.Combat;
using FiveRivers.Data;
using NUnit.Framework;
using UnityEngine;

namespace FiveRivers.Tests
{
    public sealed class M1ReloadCueTests
    {
        GameObject root;
        M1Rifle rifle;
        WeaponConfig config;
        readonly List<string> cues = new List<string>();
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        void Set(string name,object value)=>typeof(M1Rifle).GetField(name,Flags).SetValue(rifle,value);
        float StartTime=>(float)typeof(M1Rifle).GetField("reloadStart",Flags).GetValue(rifle);
        void Advance(float elapsed)=>typeof(M1Rifle).GetMethod("AdvanceReload",Flags).Invoke(rifle,new object[]{StartTime+elapsed});
        [SetUp] public void SetUp()
        {
            root=new GameObject("M1 reload cue test");root.SetActive(false);rifle=root.AddComponent<M1Rifle>();
            config=ScriptableObject.CreateInstance<WeaponConfig>();config.m1ClipSize=8;config.m1StartReserve=40;config.m1MaxReserve=64;config.m1ReloadEmptyTime=2.5f;config.m1ReloadPartialTime=3.2f;
            Set("config",config);
            typeof(M1Rifle).GetProperty("Magazine").SetValue(rifle,new M1Magazine(8,40,64));
            rifle.ClipInserted+=()=>cues.Add("insert");rifle.Reloaded+=()=>cues.Add("done");cues.Clear();
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(config);}
        void Begin(bool empty)
        {
            for(int i=0;i<(empty?8:1);i++)rifle.Magazine.Fire(out _);
            Assert.That(rifle.TryReload(),Is.True);
        }
        [TestCase(false,3.2f)] [TestCase(true,2.5f)]
        public void InsertionOccursOnceAt66PercentBeforeCompletion(bool empty,float duration)
        {
            Begin(empty);Advance(duration*.659f);Assert.That(cues,Is.Empty);
            Advance(duration*.661f);Advance(duration*.8f);Assert.That(cues,Is.EqualTo(new[]{"insert"}));Assert.That(rifle.Reloading,Is.True);
            Advance(duration+.01f);Assert.That(cues,Is.EqualTo(new[]{"insert","done"}));Assert.That(rifle.Magazine.InClip,Is.EqualTo(8));
        }
        [Test] public void LargeFrameStepStillInsertsBeforeCompletion()
        {
            Begin(true);Advance(20f);Advance(21f);Assert.That(cues,Is.EqualTo(new[]{"insert","done"}));
        }
        [TestCase(false)] [TestCase(true)] public void CancelOrResetCannotEmitStaleInsertion(bool reset)
        {
            Begin(false);Advance(.5f);if(reset)rifle.ResetAmmo();else rifle.CancelReload();Advance(20f);Assert.That(cues,Is.Empty);
        }
        [Test] public void CancelAndRestartRearmsOneInsertionCue()
        {
            Begin(false);rifle.CancelReload();Assert.That(rifle.TryReload(),Is.True);Advance(10f);Assert.That(cues,Is.EqualTo(new[]{"insert","done"}));
        }
    }
}
