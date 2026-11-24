namespace FiveRivers.Combat
{
    // 전차포 예고 (결정 2026-11-24, "위험은 읽을 수 있게"). 엔진 의존 없음.
    // 쏘기 lead 초 전에 한 번 알린다(포탑 회전 소리 + 포탑이 마지막으로 살짝 조정되는 움직임). 노출 판정은 쏘는 순간 그대로라서
    // 예고를 듣고 숙이면 산다. 알릴 수 없는 때(등장 연출 · 어두운 재시도 화면 · 전투 잠금)는 그 발의 예고를 건너뛴다.
    // 늦게 알릴 수 있게 된 경우에는 남은 시간이 lead 의 minRemainingShare 보다 짧으면 건너뛴다(들려도 숙일 시간이 없는 예고는 하지 않는다).
    public sealed class TankFireWarning
    {
        readonly float lead, nudgeDelay, nudgeSeconds, nudgeYaw, nudgePitch, minRemaining;
        float warnedFor = float.NegativeInfinity;   // 이미 처리한(알렸거나 건너뛴) 발의 nextFire
        float startedAt;

        public TankFireWarning(float lead, float nudgeDelay, float nudgeSeconds, float nudgeYawDegrees, float nudgePitchDegrees, float minRemainingShare = 0.6f)
        {
            this.lead = lead < 0f ? 0f : lead;
            this.nudgeDelay = nudgeDelay < 0f ? 0f : nudgeDelay;
            this.nudgeSeconds = nudgeSeconds < 0f ? 0f : nudgeSeconds;
            nudgeYaw = nudgeYawDegrees;
            nudgePitch = nudgePitchDegrees;
            minRemaining = this.lead * (minRemainingShare < 0f ? 0f : minRemainingShare > 1f ? 1f : minRemainingShare);
        }

        public float Lead => lead;
        public bool Active { get; private set; }

        // 매 프레임. 예고가 시작되는 프레임에만 true. now · nextFire 는 같은 시계(Time.time).
        public bool Tick(float now, float nextFire, bool canWarn)
        {
            if (lead <= 0f) return false;
            if (Active && (!canWarn || now >= nextFire || nextFire != warnedFor)) Active = false;
            if (!canWarn || Active || nextFire == warnedFor) return false;
            if (now < nextFire - lead || now >= nextFire) return false;
            warnedFor = nextFire;   // 이 발은 알리든 건너뛰든 한 번만 판단한다
            if (nextFire - now < minRemaining) return false;
            Active = true;
            startedAt = now;
            return true;
        }

        // 쏘았거나 전차가 다시 나타났을 때(nextFire 가 새로 정해질 때)
        public void Cancel()
        {
            Active = false;
            warnedFor = float.NegativeInfinity;
        }

        // 쏘았다: 이 발은 끝났다
        public void Fired() => Active = false;

        // 포탑 좌우 · 주포 위아래의 마지막 조정(도). 예고가 켜진 동안만 0이 아니고, 쏘기 전에 0으로 돌아와 판정에 닿지 않는다.
        public float NudgeYaw(float now) => Active ? nudgeYaw * Bump(now) : 0f;
        public float NudgePitch(float now) => Active ? nudgePitch * Bump(now) : 0f;

        float Bump(float now)
        {
            if (nudgeSeconds <= 0f) return 0f;
            float u = (now - startedAt - nudgeDelay) / nudgeSeconds;
            if (u <= 0f || u >= 1f) return 0f;
            // 한 번 틀었다가 제자리로: 가운데가 가장 크다
            return (float)System.Math.Sin(System.Math.PI * u);
        }
    }
}
