namespace FiveRivers.Narrative
{
    public enum TrenchTurn { None, Left, Right, Back }

    // 골짜기(적이 오는 쪽)를 보고 있지 않을 때 어느 쪽으로 돌라고 할지 (순수 계산, 테스트 대상).
    // signedAngle: 지금 보는 방향에서 골짜기까지의 각도(위에서 볼 때 시계 방향이 +, 즉 오른쪽).
    // 안내가 떠 있는 동안은 더 가까이 돌아와야 사라진다 (경계에서 깜빡이지 않게).
    public static class TrenchGuide
    {
        public const float ShowAbove = 60f;
        public const float HideBelow = 40f;
        public const float BackAbove = 135f;

        public static TrenchTurn Turn(float signedAngle, bool showing)
        {
            float a = signedAngle < 0f ? -signedAngle : signedAngle;
            if (a < (showing ? HideBelow : ShowAbove)) return TrenchTurn.None;
            if (a >= BackAbove) return TrenchTurn.Back;
            return signedAngle > 0f ? TrenchTurn.Right : TrenchTurn.Left;
        }
    }
}
