using UnityEngine;

namespace FiveRivers.Data
{
    // 회차 진행 시간 값. BALANCE.md 1-1 · 1-3번과 1:1. 값은 FlowConfig.asset에만 둔다.
    [CreateAssetMenu(fileName = "FlowConfig", menuName = "FiveRivers/Flow Config")]
    public sealed class FlowConfig : ScriptableObject
    {
        [Header("구간 상한 (초) : BALANCE 1-1")]
        public float s1Cap;
        public float s2Cap;
        public float s3Cap;
        public float s4Min;
        public float s4Max;
        public float s5Cap;
        public float dawnDuration;
        public float resultDuration;

        [Header("기준 길이 (초) : BALANCE 1-1, 디버그 구간 바로가기의 기준 시각")]
        public float s1Base;
        public float s2Base;
        public float s3Base;
        public float s4Base;
        public float s5Base;

        [Header("전체 상한 · 강제 전환 (회차 시각, 초) : BALANCE 1-3")]
        public float forceTankAt;
        public float forcedSilence;
        public float tankEntryDeadline;
        public float forceEndingAt;
        public float allySupportDuration;
        public float hardCap;
    }
}
