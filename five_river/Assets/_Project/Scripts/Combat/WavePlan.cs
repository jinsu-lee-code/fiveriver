namespace FiveRivers.Combat
{
    // 한 공세의 출현 계획. 동시 활동 적은 maxActive를 넘지 않는다 (CLAUDE.md 핵심 설계 규칙 5).
    // 2026-11-18: maxActive는 구간마다 다르다 (EnemyDensity.For: 일반판은 구간별 유연 상한, 전시용은 5명).
    // S2 생존 정찰병이 주 공세에 합류하면 처음부터 활동 수에 넣는다.
    public sealed class WavePlan
    {
        readonly int maxActive;
        readonly float interval;
        float nextSpawn;

        public WavePlan(int total, int maxActive, float interval, int joined = 0, float startTime = 0f)
        {
            this.maxActive = maxActive;
            this.interval = interval;
            Remaining = total;
            Active = joined;
            nextSpawn = startTime;
        }

        public int Remaining { get; private set; }
        public int Active { get; private set; }
        public bool Done => Remaining == 0 && Active == 0;

        public bool TrySpawn(float time)
        {
            if (Remaining <= 0 || Active >= maxActive || time < nextSpawn) return false;
            Remaining--;
            Active++;
            nextSpawn = time + interval;
            return true;
        }

        // 쓰러짐 · 철수 완료
        public void OnEnemyGone()
        {
            if (Active > 0) Active--;
        }

        // 철수 명령: 더 나오지 않는다
        public void StopSpawning() => Remaining = 0;
    }
}
