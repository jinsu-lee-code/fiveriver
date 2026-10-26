using UnityEngine;

namespace FiveRivers.Data
{
    // 실패 보조. BALANCE.md 3번. 원본 값은 바꾸지 않고 전투 쪽이 배율로 곱해 쓴다.
    [CreateAssetMenu(fileName = "AssistConfig", menuName = "FiveRivers/Assist Config")]
    public sealed class AssistConfig : ScriptableObject
    {
        public int failThreshold;
        public float enemyAccuracy;
        public float enemyDamage;
    }
}
