using UnityEngine;

namespace FiveRivers.Combat
{
    // 2026-11-18 사용자 지적 "1인칭 바주카 팔 · 손이 M1에 비해 너무 얇다".
    // 측정(source/unity_staging_20261118/task1_measure): 납품 팔 SK_CH_ROK_Arms 의 아래팔은 손바닥보다 가늘다(가운데 7.5 x 6.3 cm,
    // 손목 쪽 7.2 x 4.9 cm, 손바닥 폭 9.1 cm — 팔 배율 전). M1 은 손보다 굵은 헐렁한 천 소매 관(손의 1.1~1.4배)이라 같은 굵기로 읽히지
    // 않는다. 팔 전체 배율(BazookaPresentation.armScale)은 손까지 같이 키우므로 이 비율을 바꾸지 못한다.
    // 여기서는 메시의 기준 자세(바인드 공간)에서 아래팔 소매만 뼈 축 둘레로 부풀린다: 새 반지름 = sqrt((r * gain)^2 + pad(t)^2).
    // 얇은 쪽(두께)이 더 많이 늘어 단면이 둥근 천 소매에 가까워진다. 손(손 · 손가락 가중치가 큰 정점)은 움직이지 않는다.
    // 뼈대 · 바인드 포즈 · UV · 가중치는 그대로라 애니메이션 · FitArm · 소매 텍스처(BazookaSleeveSetup)는 바뀌지 않는다.
    // 표시 전용. 회차 상태가 없다(메시는 한 번 만들고 끝까지 같음).
    public sealed class BazookaArmBulk : MonoBehaviour
    {
        [SerializeField] SkinnedMeshRenderer[] arms;
        [Tooltip("아래팔 위치 t(0 팔꿈치 · 1 손목, 메시 기준 자세)마다 더하는 소매 두께(m, 팔 배율 전). 첫 · 끝 키 밖은 건드리지 않는다.")]
        [SerializeField] AnimationCurve pad = DefaultPad();
        [Tooltip("원래 반지름에 곱하는 배율. 1 이면 pad 만 더한다.")]
        [SerializeField, Range(.9f, 1.3f)] float gain = 1f;
        [Tooltip("손 · 손가락 뼈 가중치가 이 값부터 줄여 skinTo 에서 0 (손목의 소매 끝 접힘은 .30~.45, 손 피부는 .45 이상)")]
        [SerializeField, Range(0f, 1f)] float skinFrom = .40f;
        [SerializeField, Range(0f, 1f)] float skinTo = .60f;

        Mesh[] sources, built;
        Bounds[] sourceBounds;

        public static AnimationCurve DefaultPad() => new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(.12f, 0f),          // 팔꿈치의 걷어 올린 소매 뭉치(이미 13 cm)는 그대로
            new Keyframe(.25f, .031f), new Keyframe(.70f, .031f),  // 아래팔: 7.5x6.3 → 9.7x8.7 cm (팔 배율 전)
            new Keyframe(1f, .024f), new Keyframe(1.15f, .024f));   // 손목 쪽 소매 끝: 7.2x4.9 → 8.9x7.3 cm

        void Awake() => Build();

        void OnDestroy() => Release();

#if UNITY_EDITOR
        // 플레이 중 인스펙터에서 값을 바꾸면 바로 다시 만든다.
        void OnValidate()
        {
            if (Application.isPlaying && built != null) Build();
        }
#endif

        void Build()
        {
            if (arms == null) return;
            if (sources == null || sources.Length != arms.Length)
            {
                sources = new Mesh[arms.Length];
                built = new Mesh[arms.Length];
                sourceBounds = new Bounds[arms.Length];
            }
            for (int i = 0; i < arms.Length; i++)
            {
                var skin = arms[i];
                if (skin == null) continue;
                if (sources[i] == null)
                {
                    sources[i] = skin.sharedMesh;
                    sourceBounds[i] = skin.localBounds;
                }
                var source = sources[i];
                if (source == null || !source.isReadable)
                {
                    Debug.LogWarning("BazookaArmBulk: arm mesh is missing or not readable.", this);
                    continue;
                }
                var bones = skin.bones;
                var names = new string[bones.Length];
                for (int b = 0; b < bones.Length; b++) names[b] = bones[b] != null ? bones[b].name : "";
                var moved = ArmBulk.Apply(source.vertices, source.boneWeights, names, source.bindposes, pad, gain, skinFrom, skinTo, out float maxMove);
                if (moved == null)
                {
                    Debug.LogWarning($"BazookaArmBulk: no forearm/hand bones or unexpected units on {skin.name}.", this);
                    continue;
                }
                if (built[i] == null)
                {
                    built[i] = Instantiate(source);
                    built[i].name = source.name + "_Bulk";
                }
                built[i].vertices = moved;
                built[i].RecalculateBounds();
                skin.sharedMesh = built[i];
                var bounds = sourceBounds[i];
                bounds.Expand(maxMove * 2f);
                skin.localBounds = bounds;
            }
        }

        void Release()
        {
            if (built == null) return;
            for (int i = 0; i < built.Length; i++)
            {
                if (arms != null && i < arms.Length && arms[i] != null && sources[i] != null)
                {
                    arms[i].sharedMesh = sources[i];
                    arms[i].localBounds = sourceBounds[i];
                }
                if (built[i] != null) Destroy(built[i]);
            }
            built = null;
            sources = null;
        }
    }

    /// <summary>Pure math for <see cref="BazookaArmBulk"/>: radial padding of a forearm in a skinned mesh's bind space.</summary>
    public static class ArmBulk
    {
        // Mesh units are metres for SK_CH_ROK_Arms (forearm .199 m). Anything far outside means a unit mismatch: do nothing.
        public const float MinForearm = .05f, MaxForearm = 1f;

        public static bool FindForearm(string[] boneNames, out int forearm, out int hand)
        {
            forearm = hand = -1;
            if (boneNames == null) return false;
            for (int i = 0; i < boneNames.Length; i++)
            {
                var name = boneNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                if (forearm < 0 && name.Contains("ForeArm")) forearm = i;
                else if (hand < 0 && name.EndsWith("Hand")) hand = i;
            }
            return forearm >= 0 && hand >= 0;
        }

        /// <summary>
        /// The forearm the mesh is actually skinned to (2026-11-18): each arm skin lists the whole skeleton, so the
        /// first "ForeArm" by name was the left one for the right arm too and the right sleeve was never padded.
        /// Picks the ForeArm bone carrying the most vertex weight and the Hand bone of the same side.
        /// </summary>
        public static bool FindForearm(string[] boneNames, BoneWeight[] weights, out int forearm, out int hand)
        {
            forearm = hand = -1;
            if (boneNames == null) return false;
            if (weights == null || weights.Length == 0) return FindForearm(boneNames, out forearm, out hand);
            var total = new float[boneNames.Length];
            void Add(int bone, float w) { if (bone >= 0 && bone < total.Length) total[bone] += w; }
            foreach (var w in weights) { Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3); }
            float best = 0f;
            for (int i = 0; i < boneNames.Length; i++)
                if (!string.IsNullOrEmpty(boneNames[i]) && boneNames[i].Contains("ForeArm") && total[i] > best) { best = total[i]; forearm = i; }
            if (forearm < 0) return FindForearm(boneNames, out forearm, out hand);
            string handName = boneNames[forearm].Replace("ForeArm", "Hand");
            hand = System.Array.IndexOf(boneNames, handName);
            return hand >= 0;
        }

        /// <summary>The bone's joint position in mesh (bind) space.</summary>
        public static Vector3 JointPosition(Matrix4x4 bindpose) => bindpose.inverse.MultiplyPoint3x4(Vector3.zero);

        /// <summary>Total weight of the vertex on hand and finger bones (names containing "Hand").</summary>
        public static float HandWeight(BoneWeight w, string[] boneNames)
        {
            float sum = 0f;
            if (w.weight0 > 0f && IsHand(boneNames, w.boneIndex0)) sum += w.weight0;
            if (w.weight1 > 0f && IsHand(boneNames, w.boneIndex1)) sum += w.weight1;
            if (w.weight2 > 0f && IsHand(boneNames, w.boneIndex2)) sum += w.weight2;
            if (w.weight3 > 0f && IsHand(boneNames, w.boneIndex3)) sum += w.weight3;
            return sum;
        }

        static bool IsHand(string[] names, int index) =>
            index >= 0 && index < names.Length && names[index] != null && names[index].Contains("Hand");

        /// <summary>One vertex. t along elbow→wrist decides the pad; hand weight fades it out over [skinFrom, skinTo].</summary>
        public static Vector3 Pad(Vector3 v, Vector3 elbow, Vector3 wrist, float handWeight, AnimationCurve pad, float gain, float skinFrom, float skinTo)
        {
            if (pad == null || pad.length == 0) return v;
            var axis = wrist - elbow;
            float length = axis.magnitude;
            if (length < 1e-5f) return v;
            axis /= length;
            float along = Vector3.Dot(v - elbow, axis), t = along / length;
            if (t < pad[0].time || t > pad[pad.length - 1].time) return v;
            float keep = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(skinFrom, skinTo, handWeight));
            if (keep <= 0f) return v;
            var centre = elbow + axis * along;
            var radial = v - centre;
            float r = radial.magnitude;
            if (r < 1e-5f) return v;
            float p = Mathf.Max(0f, pad.Evaluate(t)), g = r * gain;
            float target = Mathf.Sqrt(g * g + p * p);
            if (Mathf.Approximately(target, r)) return v;   // no pad here: keep the vertex bit-for-bit
            return centre + radial * (Mathf.Lerp(r, target, keep) / r);
        }

        /// <summary>Whole mesh. Returns null when the forearm/hand bones are not found or the units look wrong.</summary>
        public static Vector3[] Apply(Vector3[] vertices, BoneWeight[] weights, string[] boneNames, Matrix4x4[] bindposes,
            AnimationCurve pad, float gain, float skinFrom, float skinTo, out float maxDisplacement)
        {
            maxDisplacement = 0f;
            if (vertices == null || weights == null || bindposes == null || weights.Length != vertices.Length) return null;
            if (!FindForearm(boneNames, weights, out int forearm, out int hand) || forearm >= bindposes.Length || hand >= bindposes.Length) return null;
            var elbow = JointPosition(bindposes[forearm]);
            var wrist = JointPosition(bindposes[hand]);
            float length = Vector3.Distance(elbow, wrist);
            if (length < MinForearm || length > MaxForearm) return null;
            var result = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                result[i] = Pad(vertices[i], elbow, wrist, HandWeight(weights[i], boneNames), pad, gain, skinFrom, skinTo);
                maxDisplacement = Mathf.Max(maxDisplacement, (result[i] - vertices[i]).magnitude);
            }
            return result;
        }
    }
}
