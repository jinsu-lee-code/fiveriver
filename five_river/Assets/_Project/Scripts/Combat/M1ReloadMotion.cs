using UnityEngine;

namespace FiveRivers.Combat
{
    // Right-hand reload motion authored in Blender (C:/sk_anim) and baked per frame in the M1 shot-motion space.
    // Sampled by reload progress, so the gameplay reload duration stays the single source of timing.
    public sealed class M1ReloadMotion : ScriptableObject
    {
        public int frames;
        public string[] bones;            // hand bones under the visual wrist, in bone-major order
        public Vector3[] wristPosition;   // right grip target, local to its parent
        public Quaternion[] wristRotation;
        public Vector3[] clipOffset;      // carried clip offset from its seated pose
        public Quaternion[] boneRotation; // [bone * (frames + 1) + frame] local rotations

        public bool IsValid => frames > 0 && wristPosition != null && wristPosition.Length == frames + 1
            && wristRotation != null && wristRotation.Length == frames + 1 && clipOffset != null && clipOffset.Length == frames + 1
            && bones != null && boneRotation != null && boneRotation.Length == bones.Length * (frames + 1);

        void Frame(float progress, out int a, out int b, out float t)
        {
            float f = Mathf.Clamp01(progress) * frames;
            a = Mathf.Min(Mathf.FloorToInt(f), frames);
            b = Mathf.Min(a + 1, frames);
            t = f - a;
        }

        public void SampleWrist(float progress, out Vector3 position, out Quaternion rotation, out Vector3 clip)
        {
            Frame(progress, out int a, out int b, out float t);
            position = Vector3.Lerp(wristPosition[a], wristPosition[b], t);
            rotation = Quaternion.Slerp(wristRotation[a], wristRotation[b], t);
            clip = Vector3.Lerp(clipOffset[a], clipOffset[b], t);
        }

        public Quaternion SampleBone(int bone, float progress)
        {
            Frame(progress, out int a, out int b, out float t);
            int row = bone * (frames + 1);
            return Quaternion.Slerp(boneRotation[row + a], boneRotation[row + b], t);
        }
    }
}
