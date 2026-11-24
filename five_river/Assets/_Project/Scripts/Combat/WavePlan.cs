namespace FiveRivers.Combat
{
    // 한 공세의 출현 계획. 동시 활동 적은 maxActive를 넘지 않는다 (CLAUDE.md 핵심 설계 규칙 5).
    // 2026-11-18: maxActive는 구간마다 다르다 (EnemyDensity.For: 일반판은 구간별 유연 상한, 전시용은 5명).
    // S2 생존 정찰병이 주 공세에 합류하면 처음부터 활동 수에 넣는다.
    // 2026-11-24 (사용자 지시 "방어공세는 조금 짧아도 되지만 그래도 이건 너무짧다"): 일반판 주 공세는 물결로 나온다.
    // 물결마다 인원과 시작 시각(공세 시작부터 초)이 있고, 앞 물결을 다 내보냈어도 다음 물결의 시각 전에는 나오지 않는다
    // (빨리 정리하면 그 사이가 소강: 어둠 속 탄약 보충 · 이동). 앞 물결이 남아 있으면 겹쳐서 나온다(상한 안에서).
    public sealed class WavePlan
    {
        readonly int maxActive;
        readonly float interval;
        readonly int[] waveCounts;
        readonly float[] waveStarts;
        readonly float startTime;
        float nextSpawn;
        int wave = -1, leftInWave;

        public WavePlan(int total, int maxActive, float interval, int joined = 0, float startTime = 0f)
        {
            this.maxActive = maxActive;
            this.interval = interval;
            this.startTime = startTime;
            Remaining = total;
            Active = joined;
            nextSpawn = startTime;
        }

        // waves: counts[i] men from startTime + starts[i] on
        public WavePlan(int[] counts, float[] starts, int maxActive, float interval, int joined = 0, float startTime = 0f)
            : this(Sum(counts), maxActive, interval, joined, startTime)
        {
            waveCounts = counts;
            waveStarts = starts;
        }

        static int Sum(int[] counts) { int s = 0; foreach (var c in counts) s += c; return s; }

        public int Remaining { get; private set; }
        public int Active { get; private set; }
        public bool Done => Remaining == 0 && Active == 0;
        public bool HasWaves => waveCounts != null;
        // the wave of the man the last TrySpawn let out (0 without waves)
        public int SpawnWave { get; private set; }
        public int WaveCount => waveCounts != null ? waveCounts.Length : 1;

        // when the next wave may start (seconds from the start of the assault), or -1 when none is waiting
        public float NextWaveAt
        {
            get
            {
                if (waveCounts == null || Remaining <= 0) return -1f;
                int w = wave < 0 || leftInWave == 0 ? wave + 1 : wave;
                return w < waveCounts.Length ? waveStarts[w] : -1f;
            }
        }

        public bool TrySpawn(float time)
        {
            if (Remaining <= 0 || Active >= maxActive || time < nextSpawn) return false;
            if (waveCounts != null)
            {
                int w = wave;
                int left = leftInWave;
                while (left == 0 && w + 1 < waveCounts.Length) { w++; left = waveCounts[w]; }
                if (left == 0) return false;
                if (time < startTime + waveStarts[w]) return false; // the next wave has not come yet
                wave = w;
                leftInWave = left - 1;
                SpawnWave = w;
            }
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
        public void StopSpawning() { Remaining = 0; leftInWave = 0; wave = waveCounts != null ? waveCounts.Length : wave; }
    }
}
