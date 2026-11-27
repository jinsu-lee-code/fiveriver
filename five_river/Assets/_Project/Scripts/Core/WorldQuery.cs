namespace FiveRivers.Core
{
    // 2026-11-17: 플레이어만 막는 충돌체(레이어 8 PlayerBlocker)는 보이는 바위 · 엄폐물 모양 그대로다.
    // 총알 · 시야 · 시체 처리 · 지면 찾기 · 이동 경로 굽기는 이 레이어를 보지 않는다 (기존 판정 유지).
    // 2026-11-26: 쓰러진 적의 래그돌 몸 조각(레이어 9 Ragdoll)도 보지 않는다 — 시신이 총알 · 시야를 막지 않는다.
    // Ragdoll 은 충돌 행렬에서 Default(지형 · 모래주머니)와 자기끼리만 부딪힌다 (메뉴 FiveRivers/Setup/Enemy Ragdoll).
    public static class WorldQuery
    {
        public const int PlayerBlockerLayer = 8;
        public const int RagdollLayer = 9;
        public const int All = ~((1 << PlayerBlockerLayer) | (1 << RagdollLayer));
        public const int Solid = ~((1 << PlayerBlockerLayer) | (1 << 2) | (1 << RagdollLayer)); // Ignore Raycast 도 제외
    }
}
