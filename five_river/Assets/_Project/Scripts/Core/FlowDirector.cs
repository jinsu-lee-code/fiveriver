using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Core
{
    // DemoFlow를 씬에서 돌리는 얇은 어댑터. 회차 시계는 timeScale과 무관한 unscaled 시간으로 진행한다.
    [DefaultExecutionOrder(-100)]
    public sealed class FlowDirector : MonoBehaviour
    {
        [SerializeField] FlowConfig config;
        [SerializeField] TankConfig tankConfig;
        [SerializeField] AssistConfig assistConfig;
        [SerializeField] InputGate inputGate;

        public DemoFlow Flow { get; private set; }
        public FlowConfig Config => config;
        public AssistConfig Assist => assistConfig;
        public InputGate InputGate => inputGate;

        // 실패 보조 배율 (GDD 5-4). 원본 데이터는 그대로 두고 쓰는 쪽이 곱한다.
        public float EnemyAccuracyScale => Flow.Checkpoints.AssistApplied ? assistConfig.enemyAccuracy : 1f;
        public float EnemyDamageScale => Flow.Checkpoints.AssistApplied ? assistConfig.enemyDamage : 1f;

        void Awake()
        {
            Flow = new DemoFlow(config, tankConfig, assistConfig);
            Flow.PauseReleased += ResumeTime;
            Flow.SectionChanged += OnSectionChanged;
            inputGate.Mode = InputGate.ModeFor(Flow.Section);
        }

        void Update()
        {
            Flow.Tick(Time.unscaledDeltaTime);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        // 일시정지는 게임 시간만 멈춘다. 회차 시계는 계속 흐른다 (GDD 14번 9).
        public void SetPaused(bool paused)
        {
            Flow.SetPaused(paused);
            Time.timeScale = Flow.Paused ? 0f : 1f;
        }

        void ResumeTime() => Time.timeScale = 1f;

        void OnSectionChanged(FlowTransition t)
        {
            if (!Flow.Paused) Time.timeScale = 1f;
            inputGate.Mode = InputGate.ModeFor(t.To);
        }
    }
}
