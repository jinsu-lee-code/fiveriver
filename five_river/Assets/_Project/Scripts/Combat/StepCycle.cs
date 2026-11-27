using System;

namespace FiveRivers.Combat
{
    // 플레이어 걸음 위상 (부족한 점 8번, 2026-11-26). 시간이 아니라 실제로 간 거리로 돈다: 한 주기 = 두 걸음(왼발 · 오른발).
    // Phase 0 = 왼발이 땅에 닿는 순간, 0.5 = 오른발. 발소리(AudioDirector) · 총 흔들림(M1Presentation) · 카메라 흔들림
    // (PlayerController) · 1인칭 달리기 클립 재생 위치(FirstPersonAnimatorBridge)가 모두 이 값 하나를 따라가서 박자가 어긋나지 않는다.
    // 순수 C# — 단위 테스트 StepCycleTests.
    public sealed class StepCycle
    {
        // 한 걸음 길이 (m). AudioConfig.walkStride / runStride 가 넣는다 (발소리 간격 그대로).
        public float WalkStride = 1.6f, RunStride = 2.2f;

        public float Phase { get; private set; }
        // 두 걸음 주기가 1초에 몇 번 도는지 (부드럽게 따라감). 서 있으면 0 으로 내려간다.
        public float CyclesPerSecond { get; private set; }
        // 0 = 서 있음, 1 = 걷기 이상으로 움직임 (흔들림 크기를 서서히 키우고 줄이는 데 씀)
        public float Moving { get; private set; }

        // 발이 땅에 닿을 때: 0 = 왼발, 1 = 오른발
        public event Action<int> Landed;

        // 한 프레임에 이보다 멀리 가면 순간이동(복구 · 연출 배치)으로 보고 위상을 돌리지 않는다
        public const float TeleportDistance = 2f;

        public void Advance(float distance, bool running, float dt)
        {
            if (dt <= 0f) return;
            if (distance < 0f || distance > TeleportDistance) distance = 0f;
            float stride = Math.Max(0.1f, running ? RunStride : WalkStride);
            float step = distance / (2f * stride);
            float before = Phase;
            float after = before + step;
            // a foot lands each time the phase passes 0.5 or 1 (wrapping to 0)
            if (before < 0.5f && after >= 0.5f) Landed?.Invoke(1);
            if (after >= 1f) { after -= (float)Math.Floor(after); Landed?.Invoke(0); }
            Phase = after;
            float k = 1f - (float)Math.Exp(-dt * 10f);
            CyclesPerSecond += (step / dt - CyclesPerSecond) * k;
            float speed = distance / dt;
            Moving += ((speed > 0.3f ? 1f : speed / 0.3f) - Moving) * (1f - (float)Math.Exp(-dt * 6f));
        }

        public void Reset()
        {
            Phase = 0f; CyclesPerSecond = 0f; Moving = 0f;
        }
    }
}
