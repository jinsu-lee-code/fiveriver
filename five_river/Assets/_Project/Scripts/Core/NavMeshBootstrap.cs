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
                if (!c.enabled || !IsPerson(c.transform)) continue;
                c.enabled = false;
                skipped.Add(c);
            }
            GetComponent<NavMeshSurface>().BuildNavMesh();
            foreach (var c in skipped) c.enabled = true;
        }

        static bool IsPerson(Transform t)
        {
            if (t.GetComponentInParent<NavMeshAgent>() != null) return true;
            for (; t != null; t = t.parent) if (t.name == "People") return true;
            return false;
        }
    }
}
