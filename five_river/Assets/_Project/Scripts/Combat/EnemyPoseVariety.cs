using UnityEngine;

namespace FiveRivers.Combat
{
    // 적 보병의 자세 · 박자 흩기 (2026-11-27, 검수 캡처 reveal/s0.6_022_cut: 같은 박자 · 같은 자세로 걷고, 엄폐한 적이
    // 모래주머니 뒤에서 상반신을 꼿꼿이 세움). 순수 계산 (테스트 EnemyPoseVarietyTests), 엔진 쪽은 EnemyAgent.
    //  - 사람마다 애니메이터 시작 위상(0~1)과 재생 속도(±spread, 지면 속도에 맞추는 달리기 상태는 제외)를 달리한다.
    //  - 엄폐 지점에서 기다리는 동안(쏘려고 일어서기 전 · 쏜 뒤 내려앉은 뒤)은 몸을 웅크리고 총구를 낮춘다.
    //    일어서서 쏘는 순간은 지금처럼 꼿꼿이: 쏘는 박자 · 명중 · 밀도 규칙(BALANCE 2-1 · 2-1b)은 그대로다.
    // 표시 전용.
    public static class EnemyPoseVariety
    {
        // u in 0..1 -> 1 - spread .. 1 + spread
        public static float Pace(float u, float spread) => 1f + (Mathf.Clamp01(u) * 2f - 1f) * Mathf.Max(0f, spread);

        // Waiting at his cover he keeps low; only standing up to fire (and the moment before it) he comes upright.
        public static bool KeepsLow(bool enabled, bool inCover, bool standing, bool shotPending) => enabled && inCover && !standing && !shotPending;

        // eased weight toward the target, at rate per second
        public static float Ease(float weight, bool low, float rate, float dt) => Mathf.MoveTowards(weight, low ? 1f : 0f, Mathf.Max(0f, rate) * dt);
    }
}
