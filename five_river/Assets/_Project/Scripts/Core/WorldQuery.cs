namespace FiveRivers.Core
{
    // 2026-11-17: 플레이어만 막는 충돌체(레이어 8 PlayerBlocker)는 보이는 바위 · 엄폐물 모양 그대로다.
    // 총알 · 시야 · 시체 처리 · 지면 찾기 · 이동 경로 굽기는 이 레이어를 보지 않는다 (기존 판정 유지).
    public static class WorldQuery
    {
        public const int PlayerBlockerLayer = 8;
        public const int All = ~(1 << PlayerBlockerLayer);
        public const int Solid = ~((1 << PlayerBlockerLayer) | (1 << 2)); // Ignore Raycast 도 제외
    }
}
