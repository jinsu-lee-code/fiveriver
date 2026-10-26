using System;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 값은 끝에만 추가한다.
    public enum EffectKind
    {
        MuzzleFlashM1 = 0,
        ImpactDirt = 1,
        BazookaBackblast = 2,
        RocketExplosion = 3,
        TankRicochet = 4,
        TankHitExplosion = 5,
        CannonBlast = 6,
        EnemyMuzzle = 7,
    }

    // 일회성 효과(VFX_ 프리팹)를 만들고, 체크포인트 복구 · 새 회차 때 한꺼번에 지운다 (ART_SPEC 공통 체크 V5).
    public sealed class Effects : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public EffectKind kind;
            public GameObject prefab;
            public float lifetime;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        Transform container;

        public Transform Container
        {
            get
            {
                if (container == null)
                {
                    container = new GameObject("[Effects]").transform;
                    container.SetParent(transform, false);
                }
                return container;
            }
        }

        public GameObject Spawn(EffectKind kind, Vector3 position, Quaternion rotation)
        {
            foreach (var e in entries)
            {
                if (e.kind != kind || e.prefab == null) continue;
                var go = Instantiate(e.prefab, position, rotation, Container);
                if (e.lifetime > 0f) Destroy(go, e.lifetime);
                return go;
            }
            return null;
        }

        // 효과 컨테이너 밑에 붙여 함께 지울 오브젝트 (로켓 등)
        public T SpawnTracked<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            return Instantiate(prefab, position, rotation, Container);
        }

        public void ClearAll()
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }
    }
}
