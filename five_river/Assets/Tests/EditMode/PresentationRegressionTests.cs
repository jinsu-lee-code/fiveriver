using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FiveRivers.Tests
{
    public class PresentationRegressionTests
    {
        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void AlliedSuppressionFacesEnemyEntryRatherThanAuthoredActorYaw(float yaw)
        {
            var root = new GameObject("DefenceFacingTest");
            try
            {
                var points = root.AddComponent<LevelPoints>();
                points.enemySpawns = new[] { new GameObject("EntryL").transform, new GameObject("EntryR").transform };
                foreach (var t in points.enemySpawns) t.SetParent(root.transform);
                points.enemySpawns[0].position = new Vector3(-10, 18, 100);
                points.enemySpawns[1].position = new Vector3(15, 12, 100);
                var park = new Vector3(-6.5f, 0, -1.2f);
                var facing = AllyCombatant.ApproachDirection(park, points, Quaternion.Euler(0, yaw, 0) * Vector3.forward);
                Assert.Greater(facing.z, .99f, "Authored Park yaw must not send suppression toward the player.");
                Assert.Greater(Vector3.Angle(facing, new Vector3(0, 0, -.5f) - park), 75f);
                Assert.AreEqual(0f, facing.y, 1e-6f);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MissingDefenceMarkersProduceFiniteHorizontalFallback()
        {
            Assert.AreEqual(Vector3.forward, AllyCombatant.ApproachDirection(Vector3.zero, null, Vector3.up));
            Assert.AreEqual(Vector3.right, AllyCombatant.ApproachDirection(Vector3.zero, null, new Vector3(4, 9, 0)));
        }

        [Test]
        public void ViewmodelCuffUsesActualImportedSupportWristPlane()
        {
            const string path = "Assets/_Project/Scenes/Dabudong.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool close = !scene.isLoaded;
            if (close) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var rig = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<M1ArmRig>(true)).Single();
                var data = new SerializedObject(rig);
                var anchor = data.FindProperty("leftCuffAnchor").objectReferenceValue as Transform;
                Assert.IsNotNull(anchor);
                Assert.AreEqual("SupportHand_M1", anchor.name, "The collapsed original left skeleton is not the visible support hand.");
                Assert.AreEqual(Vector3.forward, data.FindProperty("leftCuffAxis").vector3Value, "The imported cut is on local -Z, not the old skeletal -Y.");
                var vertices = anchor.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices.Select(v => anchor.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
                float plane = vertices.Min(v => v.z);
                var rim = data.FindProperty("leftCuffRim");
                Assert.AreEqual(32, rim.arraySize);
                for (int i = 0; i < rim.arraySize; i++)
                    Assert.AreEqual(plane, rim.GetArrayElementAtIndex(i).vector3Value.z, .0001f, "Cuff must follow the delivered wrist boundary.");
                Assert.Greater(data.FindProperty("leftSkinRadius").floatValue, .018f, "A fingertip-sized rim would leave the wrist uncovered.");
                var right = data.FindProperty("rightVisualWrist").objectReferenceValue as Transform;
                var skin = rig.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.bones.Contains(right));
                var mesh = skin.sharedMesh; int wrist = System.Array.IndexOf(skin.bones,right);
                Assert.AreEqual("M1_HandCuffTrim",mesh.name);
                float cut = data.FindProperty("rightSkinCap").vector3Value.y;
                var weights = mesh.boneWeights;
                float minimum = mesh.vertices.Where((v,i)=>weights[i].boneIndex0==wrist && weights[i].weight0>.5f).Min(v=>mesh.bindposes[wrist].MultiplyPoint3x4(v).y);
                Assert.AreEqual(cut,minimum,.00001f,"The closed wrist stump must not protrude behind the cuff.");
                Assert.AreEqual(mesh.vertexCount,weights.Length,"Every trimmed vertex must retain skinning weights.");
                Assert.AreEqual(mesh.vertexCount,mesh.normals.Length);
            }
            finally { if (close) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
