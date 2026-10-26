using System;

namespace FiveRivers.Core
{
    // 값은 끝에만 추가한다.
    public enum FlowCheckpoint
    {
        None = 0,
        FirstContact = 1, // CP1 첫 교전 (S2)
        MainAssault = 2,  // CP2 주 공세 (S3)
        Tank = 3,         // CP3 전차 직전 (S5)
    }

    // 체크포인트별 실패 횟수와 실패 보조 (FLOW.md 4번, GDD 5-4).
    // 같은 체크포인트에서 기준 횟수만큼 실패하면 보조를 한 번만 켜고, 그 회차가 끝날 때까지 유지한다.
    public sealed class CheckpointState
    {
        readonly int threshold;
        readonly int[] failures = new int[Enum.GetValues(typeof(FlowCheckpoint)).Length];

        public CheckpointState(int threshold)
        {
            this.threshold = threshold;
        }

        public bool AssistApplied { get; private set; }

        public event Action AssistActivated;

        public int Failures(FlowCheckpoint checkpoint) => failures[(int)checkpoint];

        public void RecordFailure(FlowCheckpoint checkpoint)
        {
            if (checkpoint == FlowCheckpoint.None) return;
            failures[(int)checkpoint]++;
            if (!AssistApplied && failures[(int)checkpoint] >= threshold)
            {
                AssistApplied = true;
                AssistActivated?.Invoke();
            }
        }

        public void Reset()
        {
            Array.Clear(failures, 0, failures.Length);
            AssistApplied = false;
        }

        public static FlowCheckpoint For(FlowSection section)
        {
            switch (section)
            {
                case FlowSection.Scout: return FlowCheckpoint.FirstContact;
                case FlowSection.Assault: return FlowCheckpoint.MainAssault;
                case FlowSection.Tank: return FlowCheckpoint.Tank;
                default: return FlowCheckpoint.None;
            }
        }
    }
}
