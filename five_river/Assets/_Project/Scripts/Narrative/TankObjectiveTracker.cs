namespace FiveRivers.Narrative
{
    public enum TankObjectiveStep
    {
        MoveToMound,
        EquipBazooka,
        FireAtSide,
    }

    // 완료한 행동은 되돌리지 않는다. CP3 재시도와 새 전차전에서만 처음부터 안내한다.
    public sealed class TankObjectiveTracker
    {
        public TankObjectiveStep Step { get; private set; }

        public void Observe(bool atMound, bool bazookaEquipped)
        {
            if (Step == TankObjectiveStep.MoveToMound && atMound)
                Step = TankObjectiveStep.EquipBazooka;
            if (Step == TankObjectiveStep.EquipBazooka && bazookaEquipped)
                Step = TankObjectiveStep.FireAtSide;
        }

        public void Reset() => Step = TankObjectiveStep.MoveToMound;
    }
}
