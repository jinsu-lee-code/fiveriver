using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace FiveRivers.Core
{
    // 씬을 열 때 NavMesh를 한 번 굽는다. 맵은 그레이박스라 자주 바뀌므로 구운 결과를 에셋으로 두지 않는다.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class NavMeshBootstrap : MonoBehaviour
    {
        void Awake()
        {
            // 2026-11-16: people are not ground. The trench floor is 1.8 m wide since the FM 5-15 rebuild, and the
            // capsule of a man kneeling in it, baked in as an obstacle, cut the trench into pieces. A NavMeshModifier
            // cannot do this here: it registers in its own OnEnable, after this Awake.
            var skipped = new List<Collider>();
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || !(IsPerson(c.transform) || IsBackdrop(c))) continue;
                c.enabled = false;
                skipped.Add(c);
            }
            var surface = GetComponent<NavMeshSurface>();
            surface.layerMask = surface.layerMask & WorldQuery.All; // 플레이어 전용 차단체는 적 · 아군 경로에 넣지 않는다
            surface.BuildNavMesh();
            foreach (var c in skipped) c.enabled = true;
        }

        // 2026-11-27 (사용자 지시 "오류 나는 거 고쳐"): the distant ridge backdrop's collider (Ridges_Continuous_Backdrop,
        // 3000 x 169 x 3000 m) is over the NavMesh builder's 1e9 m3 source limit: the editor skipped it with a warning every
        // play ("RuntimeNavMeshBuilder: Editor Only - Ignoring source mesh Ridges_Backdrop ..."). It is scenery nobody walks
        // on, so it is left out of the bake here, in the editor and in a build alike. Its collider is back on after the bake.
        public const float SourceVolumeLimit = 1e9f;
        public static bool TooLargeForNavMesh(Vector3 size) => (double)size.x * size.y * size.z > SourceVolumeLimit;
        static bool IsBackdrop(Collider c) => c is MeshCollider && TooLargeForNavMesh(c.bounds.size);   // (the terrain is not a mesh source)

        static bool IsPerson(Transform t)
        {
            if (t.GetComponentInParent<NavMeshAgent>() != null) return true;
            for (; t != null; t = t.parent) if (t.name == "People") return true;
            return false;
        }
    }
}
