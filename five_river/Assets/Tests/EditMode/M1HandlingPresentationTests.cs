using System.Linq;
using System.Reflection;
using FiveRivers.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace FiveRivers.Tests
{
    public class M1HandlingPresentationTests
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
        GameObject root;M1Rifle rifle;M1Presentation presentation;Transform[] fingers;
        [SetUp] public void Setup()
        {
            root=new GameObject("Reload hand presentation regression");root.SetActive(false);
            rifle=root.AddComponent<M1Rifle>();
            var target=new GameObject("GripTarget").transform;target.SetParent(root.transform);
            var hand=new GameObject("VisualHand").transform;hand.SetParent(root.transform);
            fingers=new Transform[15];
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)
            {
                var bone=new GameObject("finger"+(f+1)+"-"+(j+1)+".R").transform;
                bone.SetParent(j==0?hand:fingers[f*3+j-1]);bone.localPosition=Vector3.up*.02f;fingers[f*3+j]=bone;
            }
            presentation=root.AddComponent<M1Presentation>();Set(presentation,"rifle",rifle);Set(presentation,"rightHand",target);Set(presentation,"rightHandVisual",hand);Call(presentation,"Awake");
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);}
        [Test] public void ReloadArticulatesAllFiveFingersAndRestoresAfterCancel()
        {
            typeof(M1Rifle).GetProperty("Reloading").SetValue(rifle,true);
            Call(presentation,"AnimateFingers",.34f);
            foreach(var bone in fingers)Assert.Greater(Quaternion.Angle(Quaternion.identity,bone.localRotation),2f,bone.name+" must articulate rather than remain frozen");
            var carry=fingers.Select(f=>f.localRotation).ToArray();Call(presentation,"AnimateFingers",.78f);
            for(int i=0;i<fingers.Length;i++)Assert.Greater(Quaternion.Angle(carry[i],fingers[i].localRotation),2f,fingers[i].name+" must release the clip before returning");
            typeof(M1Rifle).GetProperty("Reloading").SetValue(rifle,false);Call(presentation,"AnimateFingers",0f);
            foreach(var bone in fingers)Assert.Less(Quaternion.Angle(Quaternion.identity,bone.localRotation),.001f);
        }
        [Test] public void RepeatingSameReloadPhaseDoesNotAccumulateFingerTwist()
        {
            typeof(M1Rifle).GetProperty("Reloading").SetValue(rifle,true);Call(presentation,"AnimateFingers",.56f);
            var first=fingers.Select(f=>f.localRotation).ToArray();for(int i=0;i<30;i++)Call(presentation,"AnimateFingers",.56f);
            for(int i=0;i<fingers.Length;i++)Assert.Less(Quaternion.Angle(first[i],fingers[i].localRotation),.01f);
        }
        [Test] public void RearSightHasOneClearApertureAndNoSecondHoleAboveIt()
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Weapons/M1Handling20261102/M1_RearAperture.asset");Assert.IsNotNull(mesh);
            foreach(var normal in mesh.normals)Assert.That(normal.sqrMagnitude,Is.InRange(.99f,1.01f),"Small sight surfaces need unit normals to receive lighting");
            var probe=new GameObject("SightGeometryProbe");
            try
            {
                var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
                Assert.IsFalse(collider.Raycast(new Ray(new Vector3(0,.0797f,-.05f),Vector3.forward),out _,.1f),"The central sight line must remain open");
                Assert.IsTrue(collider.Raycast(new Ray(new Vector3(0,.0828f,-.05f),Vector3.forward),out _,.1f),"The former extra hole must be solid steel");
            }
            finally{Object.DestroyImmediate(probe);}
        }
    }
}
