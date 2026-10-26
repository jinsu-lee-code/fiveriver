using System;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 탄약 상자 가까이에서 F: M1 예비탄을 가득 채우고, 바주카를 받았으면 로켓도 채운다.
    public sealed class Resupplier : MonoBehaviour
    {
        [SerializeField] WeaponConfig config;
        [SerializeField] PlayerController player;
        [SerializeField] WeaponHolder weapons;

        AmmoCrate[] crates = Array.Empty<AmmoCrate>();

        public AmmoCrate Nearby { get; private set; }
        public event Action Resupplied;

        void Start()
        {
            crates = FindObjectsByType<AmmoCrate>(FindObjectsSortMode.None);
        }

        void Update()
        {
            Nearby = null;
            float best = config.resupplyDistance;
            var p = transform.position;
            foreach (var crate in crates)
            {
                if (crate == null || !crate.isActiveAndEnabled) continue;
                float d = Vector3.Distance(p, crate.transform.position);
                if (d <= best)
                {
                    best = d;
                    Nearby = crate;
                }
            }

            if (Nearby != null && player.Gate.CanAct && player.Controls.Resupply.WasPressedThisFrame())
            {
                weapons.M1.Magazine.Refill();
                if (weapons.BazookaUnlocked) weapons.Bazooka.Refill();
                Resupplied?.Invoke();
            }
        }
    }
}
