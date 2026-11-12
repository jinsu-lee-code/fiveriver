using System;
using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Narrative
{
    // Tank entrance authored in Blender (2026-11-10, "Blender take"): camera, tank and turret are keyframed in
    // source/animation/tank_entrance/TankEntrance.blend against a stand-in of this level, then baked frame by
    // frame to Resources/Cinematics/TankEntrance_Blender.json (bake_take.py). The numbers are already in Unity
    // world space. Frames are blended for smooth playback at any frame rate, except across a cut.
    public sealed class TankEntranceBakedTake : TankEntranceTake
    {
        public const string Resource = "Cinematics/TankEntrance_Blender";
        const int CameraStride = 10; // position, forward, up, vertical field of view
        const int TankStride = 7;    // x, z, hull yaw, turret yaw, gun pitch, shake, cut (1 = a new shot starts here)

        [Serializable]
        sealed class Cue
        {
            public float time;
            public string kind;
            public float x, y, z;
        }

        [Serializable]
        sealed class Data
        {
            public int fps;
            public float[] camera;
            public float[] tank;
            public Cue[] cues;
        }

        Data data;
        int frames;
        readonly List<TankEntranceCue> cues = new List<TankEntranceCue>();

        public override string Name => "Blender";
        public override float Duration => data != null ? (frames - 1) / (float)data.fps : 0f;
        public override IReadOnlyList<TankEntranceCue> Cues => cues;

        public override bool Begin(TankEntranceContext context)
        {
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return false;
            data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null || data.fps <= 0 || data.camera == null || data.tank == null) return false;
            frames = Mathf.Min(data.camera.Length / CameraStride, data.tank.Length / TankStride);
            if (frames < 2) return false;
            cues.Clear();
            if (data.cues != null)
                foreach (var cue in data.cues)
                {
                    TankEntranceCueKind kind;
                    switch (cue.kind)
                    {
                        case "flare": kind = TankEntranceCueKind.Flare; break;
                        case "line": kind = TankEntranceCueKind.Line; break;
                        case "fire": kind = TankEntranceCueKind.Fire; break;
                        default: continue;
                    }
                    cues.Add(new TankEntranceCue { time = cue.time, kind = kind, point = new Vector3(cue.x, cue.y, cue.z) });
                }
            cues.Sort((a, b) => a.time.CompareTo(b.time));
            return true;
        }

        public override TankEntranceFrame Evaluate(float time)
        {
            float at = Mathf.Clamp(time * data.fps, 0f, frames - 1);
            int a = Mathf.Min((int)at, frames - 2), b = a + 1;
            float k = at - a;
            if (data.tank[b * TankStride + 6] > 0.5f) k = 0f; // never blend across a cut
            var frame = new TankEntranceFrame();
            frame.cameraPosition = Vector3.Lerp(CameraVector(a, 0), CameraVector(b, 0), k);
            frame.cameraRotation = Quaternion.Slerp(Quaternion.LookRotation(CameraVector(a, 3), CameraVector(a, 6)),
                Quaternion.LookRotation(CameraVector(b, 3), CameraVector(b, 6)), k);
            frame.fieldOfView = Mathf.Lerp(data.camera[a * CameraStride + 9], data.camera[b * CameraStride + 9], k);
            int ta = a * TankStride, tb = b * TankStride;
            // the tank itself never cuts: it is one continuous drive under all the shots
            float tk = at - a;
            frame.tankPosition = new Vector3(Mathf.Lerp(data.tank[ta], data.tank[tb], tk), 0f, Mathf.Lerp(data.tank[ta + 1], data.tank[tb + 1], tk));
            frame.hullYaw = Mathf.LerpAngle(data.tank[ta + 2], data.tank[tb + 2], tk);
            frame.turretYaw = Mathf.LerpAngle(data.tank[ta + 3], data.tank[tb + 3], tk);
            frame.gunPitch = Mathf.Lerp(data.tank[ta + 4], data.tank[tb + 4], tk);
            frame.shake = Mathf.Lerp(data.tank[ta + 5], data.tank[tb + 5], k);
            return frame;
        }

        Vector3 CameraVector(int frame, int offset)
        {
            int i = frame * CameraStride + offset;
            return new Vector3(data.camera[i], data.camera[i + 1], data.camera[i + 2]);
        }
    }
}
