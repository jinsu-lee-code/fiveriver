using System;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 2026-11-18: puts BazookaArmBulk on the first-person bazooka (VM_Bazooka) and points it at the two delivered arm
    // skins. The thickening itself happens at runtime on a copy of the mesh; the FBX, its import settings, the skeleton
    // and the animation clips are not touched. Idempotent (absolute values); edit mode only.
    // Report: Temp/bazooka_arm_bulk.txt (forearm cross-section before/after, same numbers as the Blender measurement).
    public static class BazookaArmBulkSetup
    {
        [MenuItem("FiveRivers/Setup/Bazooka Arm Bulk")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode first.");
            var player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (player == null) throw new InvalidOperationException("Open the Dabudong gameplay scene.");
            var bazooka = player.GetComponent<Bazooka>();
            if (bazooka == null || bazooka.Model == null) throw new InvalidOperationException("Bazooka viewmodel is missing.");
            var model = bazooka.Model;
            var arms = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(s => s.sharedMesh != null && s.bones.Any(b => b != null && b.name.Contains("ForeArm")))
                .ToArray();
            if (arms.Length == 0) throw new InvalidOperationException("Bazooka arm skins (with a ForeArm bone) not found.");

            var bulk = model.GetComponent<BazookaArmBulk>();
            if (bulk == null) bulk = model.gameObject.AddComponent<BazookaArmBulk>();
            var data = new SerializedObject(bulk);
            var list = data.FindProperty("arms");
            list.arraySize = arms.Length;
            for (int i = 0; i < arms.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = arms[i];
            data.ApplyModifiedPropertiesWithoutUndo();

            File.WriteAllText("Temp/bazooka_arm_bulk.txt", Report(bulk, arms));
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            Debug.Log("[BazookaArmBulk] set on " + model.name + " for " + arms.Length + " arm skins. Report: Temp/bazooka_arm_bulk.txt");
        }

        // Widths of the forearm section (vertices within 2.5 % of the bone length of t, hand weight below .3), before
        // and after, in mesh units (metres before armScale). Width = extent along the widest direction, thickness = across.
        static string Report(BazookaArmBulk bulk, SkinnedMeshRenderer[] arms)
        {
            var data = new SerializedObject(bulk);
            var pad = data.FindProperty("pad").animationCurveValue;
            float gain = data.FindProperty("gain").floatValue;
            float from = data.FindProperty("skinFrom").floatValue, to = data.FindProperty("skinTo").floatValue;
            var text = new StringBuilder();
            var presentation = bulk.GetComponent<BazookaPresentation>();
            float armScale = presentation != null ? new SerializedObject(presentation).FindProperty("armScale").floatValue : 1f;
            text.AppendLine($"armScale (BazookaPresentation) {armScale:0.00} — in-game widths = mesh widths x armScale");
            foreach (var skin in arms)
            {
                var mesh = skin.sharedMesh;
                var names = skin.bones.Select(b => b != null ? b.name : "").ToArray();
                var moved = ArmBulk.Apply(mesh.vertices, mesh.boneWeights, names, mesh.bindposes, pad, gain, from, to, out float max);
                if (moved == null) { text.AppendLine($"{skin.name}: forearm/hand bones not found"); continue; }
                ArmBulk.FindForearm(names, mesh.boneWeights, out int fore, out int hand);
                var elbow = ArmBulk.JointPosition(mesh.bindposes[fore]);
                var wrist = ArmBulk.JointPosition(mesh.bindposes[hand]);
                text.AppendLine($"{skin.name} ({mesh.name}): forearm {Vector3.Distance(elbow, wrist):0.000} m, max move {max * 100f:0.00} cm");
                foreach (float t in new[] { .3f, .5f, .7f, .8f, .9f })
                {
                    Measure(mesh.vertices, mesh.boneWeights, names, elbow, wrist, t, out float w0, out float h0);
                    Measure(moved, mesh.boneWeights, names, elbow, wrist, t, out float w1, out float h1);
                    text.AppendLine($"  t {t:0.0}: {w0 * 100f:0.0} x {h0 * 100f:0.0} cm -> {w1 * 100f:0.0} x {h1 * 100f:0.0} cm" +
                                    $"  (in game {w0 * armScale * 100f:0.0} x {h0 * armScale * 100f:0.0} -> {w1 * armScale * 100f:0.0} x {h1 * armScale * 100f:0.0})");
                }
            }
            return text.ToString();
        }

        static void Measure(Vector3[] v, BoneWeight[] w, string[] names, Vector3 elbow, Vector3 wrist, float t, out float width, out float thickness)
        {
            var axis = (wrist - elbow).normalized;
            float length = Vector3.Distance(elbow, wrist);
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            var ring = Enumerable.Range(0, v.Length)
                .Where(i => Mathf.Abs(Vector3.Dot(v[i] - elbow, axis) / length - t) < .025f && ArmBulk.HandWeight(w[i], names) < .3f)
                .Select(i => Vector3.ProjectOnPlane(v[i] - elbow, axis)).ToArray();
            width = thickness = 0f;
            if (ring.Length < 4) return;
            // the widest direction over 36 trial angles, thickness across it
            for (int k = 0; k < 36; k++)
            {
                var d = Quaternion.AngleAxis(k * 5f, axis) * side;
                var n = Vector3.Cross(axis, d);
                float a = ring.Max(p => Vector3.Dot(p, d)) - ring.Min(p => Vector3.Dot(p, d));
                if (a <= width) continue;
                width = a;
                thickness = ring.Max(p => Vector3.Dot(p, n)) - ring.Min(p => Vector3.Dot(p, n));
            }
        }
    }
}
