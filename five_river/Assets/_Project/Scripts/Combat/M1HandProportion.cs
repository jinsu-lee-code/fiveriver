using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Right-hand proportions from the reference photo (wrist to knuckle 86 mm, middle finger 48/31/25 mm): the modelled
    // hand has a long palm and short fingers. Shortens the palm and lengthens the fingers of the right hand only, by
    // stretching each segment along its own bone in the bind pose and moving the bones to match. The thumb and the
    // left hand are untouched. Same rule as C:/sk_anim/scripts/prop_scene.py, which the reload motion is baked on.
    public static class M1HandProportion
    {
        public const float Palm = .85f, Proximal = 1.40f, Middle = 1.20f, Distal = 1.15f;

        // Instances the renderer's mesh and returns true when the right hand was reshaped.
        public static bool Apply(SkinnedMeshRenderer skin)
        {
            if (skin == null || skin.sharedMesh == null) return false;
            var bones = skin.bones; var bind = skin.sharedMesh.bindposes;
            var index = new Dictionary<string, int>();
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null && !index.ContainsKey(bones[i].name)) index.Add(bones[i].name, i);
            Vector3 Head(int i) => bind[i].inverse.GetColumn(3);

            var start = new Dictionary<int, Vector3>(); var dir = new Dictionary<int, Vector3>();
            var length = new Dictionary<int, float>(); var scale = new Dictionary<int, float>(); var delta = new Dictionary<int, Vector3>();
            float[] scales = { Palm, Proximal, Middle, Distal };
            for (int k = 1; k <= 4; k++)
            {
                var names = new[] { "metacarpal" + k + ".R", "finger" + (k + 1) + "-1.R", "finger" + (k + 1) + "-2.R", "finger" + (k + 1) + "-3.R" };
                var ids = new int[4]; bool ok = true;
                for (int j = 0; j < 4; j++) if (!index.TryGetValue(names[j], out ids[j])) ok = false;
                if (!ok) return false;
                var h = new Vector3[5];
                for (int j = 0; j < 4; j++) h[j] = Head(ids[j]);
                h[4] = h[3] + (h[3] - h[2]).normalized * .85f * (h[3] - h[2]).magnitude;
                var moved = h[0];
                for (int j = 0; j < 4; j++)
                {
                    var d = h[j + 1] - h[j];
                    start[ids[j]] = h[j]; dir[ids[j]] = d.normalized; length[ids[j]] = d.magnitude; scale[ids[j]] = scales[j]; delta[ids[j]] = moved - h[j];
                    moved += d * scales[j];
                }
            }

            Vector3 Map(int b, Vector3 v)
            {
                if (!scale.ContainsKey(b)) return v;
                float t = Mathf.Clamp(Vector3.Dot(v - start[b], dir[b]), 0f, length[b]);
                return v + delta[b] + dir[b] * (t * (scale[b] - 1f));
            }
            var mesh = Object.Instantiate(skin.sharedMesh); mesh.name = skin.sharedMesh.name + "_RightProportion";
            var verts = mesh.vertices; var bw = mesh.boneWeights;
            for (int i = 0; i < verts.Length; i++)
            {
                var w = bw[i]; if (!scale.ContainsKey(w.boneIndex0) && !scale.ContainsKey(w.boneIndex1) && !scale.ContainsKey(w.boneIndex2) && !scale.ContainsKey(w.boneIndex3)) continue;
                var v = verts[i]; float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3; if (sum <= 0f) continue;
                verts[i] = (Map(w.boneIndex0, v) * w.weight0 + Map(w.boneIndex1, v) * w.weight1 + Map(w.boneIndex2, v) * w.weight2 + Map(w.boneIndex3, v) * w.weight3) / sum;
            }
            mesh.vertices = verts; mesh.RecalculateBounds();
            // the moved bones keep their rotation; their bind pose follows the new joint position
            var nb = (Matrix4x4[])bind.Clone();
            foreach (var p in delta) nb[p.Key] = bind[p.Key] * Matrix4x4.Translate(-p.Value);
            mesh.bindposes = nb;
            skin.sharedMesh = mesh;
            // move the joints: each child's offset from its parent is the scaled segment, along the same direction
            foreach (var p in scale)
            {
                var b = bones[p.Key];
                foreach (Transform c in b) if (c.name.StartsWith("finger")) c.localPosition *= p.Value;
            }
            return true;
        }
    }
}
