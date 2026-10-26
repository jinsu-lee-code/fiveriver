using FiveRivers.Combat;
using UnityEngine;

namespace FiveRivers.DebugTools
{
    // 디버그 창: 무적 · 탄약 채우기 · 바주카 받기 (TASKS 1-1b 이후 추가 항목)
    public sealed class PlayerDebugPanel : MonoBehaviour, IDebugPanel
    {
        [SerializeField] PlayerController player;

        public string DebugTitle => "플레이어";

        public void DrawDebug()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var health = player.GetComponent<PlayerHealth>();
            var weapons = player.GetComponent<WeaponHolder>();
            GUILayout.Label($"체력 {health.Current:0}/{health.Max:0}   M1 {weapons.M1.Magazine.InClip}/{weapons.M1.Magazine.Reserve}   바주카 {weapons.Bazooka.Loaded}+{weapons.Bazooka.Reserve}");
            GUILayout.BeginHorizontal();
            health.Invulnerable = GUILayout.Toggle(health.Invulnerable, "무적");
            if (GUILayout.Button("탄약 채우기"))
            {
                weapons.M1.Magazine.Refill();
                weapons.Bazooka.Refill();
            }
            if (GUILayout.Button("바주카 받기")) weapons.GiveBazooka();
            if (GUILayout.Button("체력 회복")) health.Restore();
            GUILayout.EndHorizontal();
#endif
        }
    }
}
