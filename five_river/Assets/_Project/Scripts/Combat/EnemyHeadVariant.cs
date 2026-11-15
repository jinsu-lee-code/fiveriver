using UnityEngine;

namespace FiveRivers.Combat
{
    // The enemy riflemen all wore one face. The body carries its head as a renderer of its own on the same bones,
    // and each man is given one of several heads when he appears (2026-11-15).
    public sealed class EnemyHeadVariant : MonoBehaviour
    {
        [SerializeField] SkinnedMeshRenderer head;
        [SerializeField] Mesh[] meshes;
        [SerializeField] Material[] materials;
        static int next; // dealt in turn, so two men side by side seldom share a face

        public void Configure(SkinnedMeshRenderer renderer, Mesh[] headMeshes, Material[] headMaterials)
        {
            head = renderer; meshes = headMeshes; materials = headMaterials;
        }

        void OnEnable()
        {
            if (head == null || meshes == null || meshes.Length == 0) return;
            int i = next++ % meshes.Length;
            head.sharedMesh = meshes[i];
            if (materials != null && i < materials.Length && materials[i] != null) head.sharedMaterial = materials[i];
        }
    }
}
