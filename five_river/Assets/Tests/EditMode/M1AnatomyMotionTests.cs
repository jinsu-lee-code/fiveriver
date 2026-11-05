using System.Reflection;
using FiveRivers.Combat;
using NUnit.Framework;
using UnityEngine;
namespace FiveRivers.Tests
{
    public class M1AnatomyMotionTests
    {
        [TestCase(65f)] [TestCase(-65f)] [TestCase(179f)] [TestCase(-179f)]
        public void ForearmTwistSeparatesRollFromWristBend(float degrees)
        {
            var rotation=Quaternion.AngleAxis(degrees,Vector3.up)*Quaternion.AngleAxis(42f,Vector3.right);
            Assert.AreEqual(degrees,M1ArmRig.TwistDegrees(rotation,Vector3.up),.001f);
            var equivalent=new Quaternion(-rotation.x,-rotation.y,-rotation.z,-rotation.w);
            Assert.AreEqual(degrees,M1ArmRig.TwistDegrees(equivalent,Vector3.up),.001f,"Equivalent quaternion signs must not flip wrist roll");
        }
        [Test] public void DegenerateRollAndHingeInputsRemainFinite()
        {
            Assert.AreEqual(0,M1ArmRig.TwistDegrees(Quaternion.AngleAxis(180,Vector3.right),Vector3.up));
            Assert.AreEqual(0,M1ArmRig.TwistDegrees(Quaternion.identity,Vector3.zero));
            Assert.AreEqual(0,M1Presentation.HingeCorrection(Vector3.right,Vector3.up,Vector3.right));
            Assert.AreEqual(0,M1Presentation.HingeCorrection(Vector3.up,Vector3.forward,Vector3.zero));
        }
        [Test] public void ThumbHingeIgnoresSidewaysTargetOffset()
        {
            float a=M1Presentation.HingeCorrection(Vector3.up,Vector3.forward,Vector3.right);
            float b=M1Presentation.HingeCorrection(Vector3.up,Vector3.forward+Vector3.right*5,Vector3.right);
            Assert.AreEqual(7f,a,.001f);Assert.AreEqual(a,b,.001f);
        }
        [Test] public void ThumbContactKeepsDistalJointsOnTheirFlexionAxes()
        {
            var root=new GameObject("Anatomy joint limits");root.SetActive(false);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try
            {
                var rifle=root.AddComponent<M1Rifle>();var pres=root.AddComponent<M1Presentation>();
                var target=new GameObject("Grip").transform;target.SetParent(root.transform);
                var hand=new GameObject("Hand").transform;hand.SetParent(root.transform);
                var thumb=new Transform[3];
                for(int i=0;i<3;i++){thumb[i]=new GameObject("finger1-"+(i+1)+".R").transform;thumb[i].SetParent(i==0?hand:thumb[i-1]);thumb[i].localPosition=Vector3.up*.025f;thumb[i].localRotation=Quaternion.Euler(i==0?10:0,0,15+i*10);}
                var index=new Transform[3];
                for(int i=0;i<3;i++){index[i]=new GameObject("finger2-"+(i+1)+".R").transform;index[i].SetParent(i==0?hand:index[i-1]);index[i].localPosition=Vector3.up*.025f;index[i].localRotation=Quaternion.Euler(0,0,20+i*5);}
                var clip=new GameObject("Clip").transform;clip.SetParent(root.transform);clip.localPosition=new Vector3(.04f,-.02f,.02f);
                void Set(string name,object value)=>typeof(M1Presentation).GetField(name,flags).SetValue(pres,value);
                Set("rifle",rifle);Set("rightHand",target);Set("rightHandVisual",hand);Set("loadedClip",clip);
                typeof(M1Presentation).GetMethod("Awake",flags).Invoke(pres,null);
                typeof(M1Rifle).GetProperty("Reloading").SetValue(rifle,true);
                typeof(M1Presentation).GetMethod("AnimateFingers",flags).Invoke(pres,new object[]{.56f});
                var pose=(Quaternion[])typeof(M1Presentation).GetField("thumbPose",flags).GetValue(pres);
                var axes=(Vector3[,])typeof(M1Presentation).GetField("fingerHingeAxes",flags).GetValue(pres);
                Assert.LessOrEqual(Quaternion.Angle(pose[0],thumb[0].localRotation),65.01f);
                for(int i=1;i<3;i++)
                {
                    var delta=Quaternion.Inverse(pose[i])*thumb[i].localRotation;
                    Assert.Less(Vector3.Cross(new Vector3(delta.x,delta.y,delta.z),axes[0,i]).magnitude,.00001f);
                    Assert.LessOrEqual(Quaternion.Angle(Quaternion.identity,delta),i==1?40.01f:30.01f);
                }
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
