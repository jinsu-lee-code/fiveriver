using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 아군 동작 클립에 주는 바깥 신호 (2026-11-18). 지금은 가까운 포탄 하나.
    // 포격 쪽(ShellingDirector)이 포탄을 예약할 때 ShellIncoming(떨어질 자리, 떨어질 시각)을 부르면, 그 자리에서
    // AllyClipPlayer.DuckRadius 안의 참호 아군이 떨어지기 DuckLead 초 전에 머리를 숙인다.
    // 부르는 곳이 없으면 아무 일도 일어나지 않는다(숙이기만 빠지고 나머지는 그대로).
    public static class AllyClipCues
    {
        struct Shell { public int id; public Vector3 point; public float at; }

        static readonly List<Shell> shells = new List<Shell>();
        static int serial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() { shells.Clear(); serial = 0; }

        // impactTime: Time.time 기준으로 떨어질 시각
        public static void ShellIncoming(Vector3 point, float impactTime)
        {
            shells.RemoveAll(s => s.at < Time.time - 2f);
            if (shells.Count > 32) shells.RemoveAt(0);
            shells.Add(new Shell { id = ++serial, point = point, at = impactTime });
        }

        // 이 사람이 아직 반응하지 않은 가까운 포탄이 지금 숙일 때인가 (handled: 이 사람이 이미 본 포탄 번호)
        internal static bool TakeNear(Vector3 at, float radius, float lead, HashSet<int> handled)
        {
            float now = Time.time;
            bool found = false;
            foreach (var s in shells)
            {
                if (handled.Contains(s.id) || now < s.at - lead) continue; // 이미 봤다 / 아직 이르다
                handled.Add(s.id);
                if (now > s.at + 0.4f) continue;                         // 지나갔다
                var d = s.point - at; d.y = 0f;
                if (d.sqrMagnitude <= radius * radius) found = true;
            }
            if (handled.Count > 256) handled.Clear(); // 오래된 번호는 목록에서 이미 빠졌다
            return found;
        }
    }
}
