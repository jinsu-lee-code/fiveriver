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
                var axis = data.FindProperty("leftCuffAxis").vector3Value;
                Assert.AreEqual(1f, axis.magnitude, .0001f, "Wrist plane must have a unit normal.");
                // User-approved support hand (AGENTS.md): original FBX mesh, position (0,.026,.20), scale .65, angle (-14,-5,-30).
                Assert.AreEqual(Vector3.forward, axis, "Support wrist plane axis changed.");
                Assert.Less(Vector3.Distance(anchor.localPosition, new Vector3(0f, .026f, .20f)), .0005f, "Approved support hand position changed.");
                Assert.Less(Vector3.Distance(anchor.localScale, Vector3.one * .65f), .0005f, "Approved support hand scale changed.");
                Assert.Less(Quaternion.Angle(anchor.localRotation, Quaternion.Euler(-14f, -5f, -30f)), .05f, "Approved support hand angle changed.");
                var handMesh = anchor.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual("Assets/_Project/Art/Models/Characters/ROK_Arms/SupportHand/SM_CH_ROK_SupportHand_M1.fbx", AssetDatabase.GetAssetPath(handMesh), "Support hand must use the original model.");
                var vertices = anchor.GetComponentsInChildren<MeshFilter>().SelectMany(f => f.sharedMesh.vertices.Select(v => anchor.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
                float plane = vertices.Min(v => Vector3.Dot(v, axis));
                var boundary = vertices.Where(v => Vector3.Dot(v, axis) <= plane + .0001f).Distinct().ToArray();
                Assert.GreaterOrEqual(boundary.Length, 3, "A wrist cut needs a planar outline, not a single extremal vertex.");
                var cap = data.FindProperty("leftSkinCap").vector3Value;
                Assert.AreEqual(plane, Vector3.Dot(cap, axis), .0001f, "Cuff centre must sit on the imported cut.");
                var rim = data.FindProperty("leftCuffRim");
                Assert.AreEqual(32, rim.arraySize);
                var samples = Enumerable.Range(0, rim.arraySize).Select(i => rim.GetArrayElementAtIndex(i).vector3Value).ToArray();
                foreach (var sample in samples)
                    Assert.AreEqual(plane, Vector3.Dot(sample, axis), .0001f, "Cuff must follow the delivered wrist plane.");

                // Compare silhouettes, since each cuff sample can fall between imported vertices.
                var tangent = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
                for (int i = 0; i < samples.Length; i++)
                {
                    var direction = Quaternion.AngleAxis(360f * i / samples.Length, axis) * tangent;
                    float skinExtent = boundary.Max(v => Vector3.Dot(v - cap, direction));
                    float cuffExtent = samples.Max(v => Vector3.Dot(v - cap, direction));
                    float worldScale = anchor.TransformVector(direction).magnitude;
                    Assert.Greater(skinExtent, 0f, "The cuff centre must lie inside the cut outline.");
                    Assert.AreEqual(skinExtent * worldScale, cuffExtent * worldScale, .0035f, "Cuff must cover the measured wrist outline within 3.5 mm.");
                }
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
