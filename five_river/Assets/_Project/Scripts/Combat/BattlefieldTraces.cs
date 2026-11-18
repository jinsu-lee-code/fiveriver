using System;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 처음부터 있는 전장의 흔적 (2026-11-18): 며칠째 포격을 맞은 들판의 오래된 구덩이와 그을린 땅.
    // 여기에는 자리와 크기만 있다(메뉴 FiveRivers/Setup/Battlefield Traces가 채운다). 실제로 파는 것은
    // ShellCraters이고, 지형 에셋이 아니라 실행 중 사본에 판다. 그루터기 · 연기 기둥은 이 오브젝트의 자식이다.
    public sealed class BattlefieldTraces : MonoBehaviour
    {
        [Serializable]
        public struct OldCrater
        {
            public Vector2 position; // 월드 x, z
            public float radius;
            public float depth;      // 0이면 파지 않고 그을리기만 한다
            public float rim;
            [Range(0f, 1f)] public float scorch;
            public float seed;       // 가장자리 모양 (회차마다 같게)
        }

        public OldCrater[] oldCraters = Array.Empty<OldCrater>();
    }
}
