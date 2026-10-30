using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 납품 캐릭터 FBX 중 철모 · 머리 부품이 뼈 가중치 없이 와서 Unity가 Hips(0번 뼈)에 붙인 경우를 머리 뼈로 옮긴다.
    // 조건: 서브메시 전체가 0번 뼈 100% + 목보다 위. 허리 장구처럼 아래에 있는 부품과 정상 가중치 모델은 건드리지 않는다.
    // Blender에서 가중치를 고친 FBX가 오면 조건에 걸리지 않으므로 그대로 둔다.
    public sealed class CharacterHeadGearFix : AssetPostprocessor
    {
        const string CharacterDir = "Assets/_Project/Art/Models/Characters/";

        public override uint GetVersion() => 2;

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(CharacterDir)) return;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || smr.bones == null) continue;
                int head = System.Array.FindIndex(smr.bones, b => b != null && b.name.EndsWith(":Head"));
                int neck = System.Array.FindIndex(smr.bones, b => b != null && b.name.EndsWith(":Neck"));
                if (head < 0 || neck < 0) continue;
                // 메시 공간의 '위' 방향은 FBX마다 다를 수 있어(Y-up / Z-up) Hips → Head 방향으로 높이를 잰다
                Vector3 BonePos(int b) => mesh.bindposes[b].inverse.MultiplyPoint3x4(Vector3.zero);
                var hips = BonePos(0);
                var up = (BonePos(head) - hips).normalized;
                float Height(Vector3 p) => Vector3.Dot(p - hips, up);
                float neckH = Height(BonePos(neck));

                var weights = mesh.boneWeights;
                var verts = mesh.vertices;
                int moved = 0;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var idx = mesh.GetTriangles(s).Distinct().ToArray();
                    if (idx.Length == 0) continue;
                    bool rigidOnRoot = idx.All(i => weights[i].boneIndex0 == 0 && weights[i].weight0 >= 0.999f);
                    if (!rigidOnRoot || idx.Average(i => Height(verts[i])) <= neckH) continue;
                    foreach (var i in idx) weights[i] = new BoneWeight { boneIndex0 = head, weight0 = 1f };
                    moved += idx.Length;
                }
                if (moved == 0) continue;
                mesh.boneWeights = weights;
                Debug.Log($"[HeadGearFix] {assetPath}: 머리 높이 정점 {moved}개를 Hips → Head 뼈로 옮김");
            }
        }
    }
}
