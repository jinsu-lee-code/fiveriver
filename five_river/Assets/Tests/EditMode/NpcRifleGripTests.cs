using FiveRivers.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.Tests
{
    public sealed class NpcRifleGripTests
    {
        [Test]
        public void PrefabRetainsIndependentGripOffsetsInRifleSpace()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/NpcRifleGripTest.prefab");
            var actor = new GameObject("Rifleman");
            var rifle = new GameObject("Rifle");
            rifle.transform.SetParent(actor.transform);
            GameObject contents = null;
            try
            {
                rifle.transform.SetPositionAndRotation(new Vector3(3f, 2f, -4f), Quaternion.Euler(0f, 90f, 0f));
                rifle.transform.localScale = new Vector3(2f, 1f, 0.5f);
                var grip = rifle.AddComponent<NpcRifleGrip>();
                var baseline = new Pose(new Vector3(2f, 1f, -3f), Quaternion.Euler(20f, 0f, 15f));
                Assert.That(grip.Adjust(true, baseline).position, Is.EqualTo(baseline.position));
                Assert.That(grip.Adjust(false, baseline).rotation, Is.EqualTo(baseline.rotation));

                var data = new SerializedObject(grip);
                data.FindProperty("rightPositionOffset").vector3Value = Vector3.forward * 0.04f;
                data.FindProperty("leftRotationOffset").vector3Value = new Vector3(0f, 0f, 20f);
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(PrefabUtility.SaveAsPrefabAsset(actor, path), Is.Not.Null);

                contents = PrefabUtility.LoadPrefabContents(path);
                grip = contents.GetComponentInChildren<NpcRifleGrip>();
                var right = grip.Adjust(true, baseline);
                var left = grip.Adjust(false, baseline);
                Assert.That(Vector3.Distance(right.position, baseline.position + Vector3.right * 0.02f), Is.LessThan(0.00001f));
                Assert.That(right.rotation, Is.EqualTo(baseline.rotation));
                Assert.That(left.position, Is.EqualTo(baseline.position));
                var expectedRotation = Quaternion.AngleAxis(20f, Vector3.right) * baseline.rotation;
                Assert.That(Quaternion.Angle(left.rotation, expectedRotation), Is.LessThan(0.05f));

                grip.enabled = false;
                Assert.That(grip.Adjust(true, baseline).position, Is.EqualTo(baseline.position));
                Assert.That(grip.Adjust(false, baseline).rotation, Is.EqualTo(baseline.rotation));
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                Object.DestroyImmediate(actor);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
