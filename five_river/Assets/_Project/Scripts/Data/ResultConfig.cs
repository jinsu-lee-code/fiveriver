using UnityEngine;

namespace FiveRivers.Data
{
    // 결과 화면(S7) 시간 배분. BALANCE.md 1-4번. 합계는 FlowConfig.resultDuration과 같아야 한다.
    [CreateAssetMenu(fileName = "ResultConfig", menuName = "FiveRivers/Result Config")]
    public sealed class ResultConfig : ScriptableObject
    {
        public float dateDuration;
        public float nameDuration;
        public float letterDuration;
        public float titleDuration;

        public float Total => dateDuration + nameDuration + letterDuration + titleDuration;
    }
}
