using Unity.AI.Navigation;
using UnityEngine;

namespace FiveRivers.Core
{
    // 씬을 열 때 NavMesh를 한 번 굽는다. 맵은 그레이박스라 자주 바뀌므로 구운 결과를 에셋으로 두지 않는다.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class NavMeshBootstrap : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<NavMeshSurface>().BuildNavMesh();
        }
    }
}
