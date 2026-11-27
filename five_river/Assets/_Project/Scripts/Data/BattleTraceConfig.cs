using UnityEngine;

namespace FiveRivers.Data
{
    // 전투 흔적 데칼 (부족한 점 5번, 2026-11-26): 총알 자국 · 그을음 · 핏자국. BALANCE.md 2-8.
    // 데칼은 효과 컨테이너 밑에 남아 체크포인트 복구 · 새 회차에 함께 지워진다. 종류마다 상한을 넘으면 가장 오래된 것부터 지운다.
    [CreateAssetMenu(fileName = "BattleTraceConfig", menuName = "FiveRivers/Battle Trace Config")]
    public sealed class BattleTraceConfig : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Data/BattleTraceConfig.asset";

        [Header("총알 자국 (땅 · 모래주머니 · 참호 벽에 맞은 자리, ImpactDirt)")]
        [Min(0)] public int bulletMost = 120;
        public Vector2 bulletSize = new Vector2(0.22f, 0.32f); // the hole with the sand it threw out
        [Range(0f, 1f)] public float bulletOpacity = 0.85f;

        [Header("그을음 (로켓 · 포탄 폭발)")]
        [Min(0)] public int scorchMost = 12;
        public Vector2 rocketScorchSize = new Vector2(2.2f, 3.0f);
        public Vector2 shellScorchSize = new Vector2(3.0f, 4.2f);
        [Range(0f, 1f)] public float scorchOpacity = 0.8f;

        [Header("핏자국 (쓰러진 몸이 멈춘 자리, 몸 아래 흙에 스민 정도 — 과장하지 않는다)")]
        [Min(0)] public int bloodMost = 40;
        public Vector2 bloodSize = new Vector2(0.7f, 1.1f);
        [Range(0f, 1f)] public float bloodOpacity = 0.55f;

        [Header("투사")]
        [Tooltip("데칼이 표면 앞뒤로 닿는 깊이(m). 얕게 — 지나가는 사람 몸에 찍히지 않게")]
        public float depth = 0.25f;
        [Tooltip("흔적을 남기는 최대 거리(m, 플레이어 카메라에서). 멀면 보이지 않으니 만들지 않는다")]
        public float range = 70f;
    }
}
