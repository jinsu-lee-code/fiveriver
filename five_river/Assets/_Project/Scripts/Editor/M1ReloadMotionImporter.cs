using System.IO;
using FiveRivers.Combat;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // Imports the Blender-authored M1 reload (C:/sk_anim/reload_partial_baked.json) as a Resources asset.
    // The JSON holds Unity-space matrices (column-major) for the grip target and the carried clip,
    // and local rotations (x, y, z, w) for every right-hand bone.
    public static class M1ReloadMotionImporter
    {
        const string Source = "C:/sk_anim/reload_partial_baked.json";
        public const string AssetPath = "Assets/_Project/Art/Resources/M1_ReloadMotion.asset";

        [MenuItem("FiveRivers/Animation/Import M1 Reload Motion (Blender)")]
        public static void Import()
        {
            if (!File.Exists(Source)) throw new FileNotFoundException("Blender reload bake not found.", Source);
            var json = JObject.Parse(File.ReadAllText(Source));
            int frames = (int)json["frames"];
            var bones = json["bones"].ToObject<string[]>();
            var wrist = (JArray)json["wrist"];
            var clip = (JArray)json["clip"];
            var local = (JObject)json["local"];
            var motion = AssetDatabase.LoadAssetAtPath<M1ReloadMotion>(AssetPath);
            bool created = motion == null;
            if (created) motion = ScriptableObject.CreateInstance<M1ReloadMotion>();
            motion.frames = frames;
            motion.wristPosition = new Vector3[frames + 1];
            motion.wristRotation = new Quaternion[frames + 1];
            motion.clipOffset = new Vector3[frames + 1];
            var handBones = new System.Collections.Generic.List<string>();
            foreach (var b in bones) if (b != "wrist.R") handBones.Add(b);
            motion.bones = handBones.ToArray();
            motion.boneRotation = new Quaternion[motion.bones.Length * (frames + 1)];
            for (int f = 0; f <= frames; f++)
            {
                var w = Matrix(wrist[f]);
                motion.wristPosition[f] = w.GetColumn(3);
                motion.wristRotation[f] = w.rotation;
                motion.clipOffset[f] = Matrix(clip[f]).GetColumn(3);
                for (int b = 0; b < motion.bones.Length; b++)
                {
                    var q = (JArray)local[motion.bones[b]][f];
                    motion.boneRotation[b * (frames + 1) + f] = new Quaternion((float)q[0], (float)q[1], (float)q[2], (float)q[3]);
                }
            }
            if (!motion.IsValid) throw new InvalidDataException("Imported reload motion is incomplete.");
            if (created) AssetDatabase.CreateAsset(motion, AssetPath);
            EditorUtility.SetDirty(motion);
            AssetDatabase.SaveAssets();
            Debug.Log("[M1 reload] Imported " + (frames + 1) + " frames, " + motion.bones.Length + " hand bones.");
        }

        static Matrix4x4 Matrix(JToken m)
        {
            var a = m.ToObject<float[]>();
            return new Matrix4x4(new Vector4(a[0], a[1], a[2], a[3]), new Vector4(a[4], a[5], a[6], a[7]),
                new Vector4(a[8], a[9], a[10], a[11]), new Vector4(a[12], a[13], a[14], a[15]));
        }
    }
}
