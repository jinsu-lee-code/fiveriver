using System;
using System.Collections.Generic;

namespace FiveRivers.Tutorial
{
    // 값은 끝에만 추가한다.
    public enum TutorialStep
    {
        Look = 0,        // 시점
        Move = 1,        // 이동
        Stance = 2,      // Z 자세
        ShootTarget = 3, // 조준 사격 · 표적 1회 명중
        Reload = 4,      // R 장전
    }

    // S1 조작 학습 (GDD 7번): 한 번에 행동 1개. 정체 15초면 강조, 30초면 힌트.
    // 순서보다 먼저 해낸 행동은 차례가 오면 바로 넘어간다.
    public sealed class TutorialTracker
    {
        static readonly TutorialStep[] Order =
        {
            TutorialStep.Look, TutorialStep.Move, TutorialStep.Stance, TutorialStep.ShootTarget, TutorialStep.Reload,
        };

        readonly float highlightAfter;
        readonly float hintAfter;
        readonly bool[] done = new bool[Order.Length];
        int index;

        public TutorialTracker(float highlightAfter, float hintAfter)
        {
            this.highlightAfter = highlightAfter;
            this.hintAfter = hintAfter;
        }

        public bool Complete => index >= Order.Length;
        public TutorialStep Current => Order[Math.Min(index, Order.Length - 1)];
        public float StepTime { get; private set; }
        public bool Highlight => !Complete && StepTime >= highlightAfter;
        public bool ShowHint => !Complete && StepTime >= hintAfter;

        public event Action<TutorialStep> StepChanged;
        public event Action Completed;

        public IEnumerable<TutorialStep> Remaining
        {
            get
            {
                for (int i = index; i < Order.Length; i++)
                    if (!done[(int)Order[i]]) yield return Order[i];
            }
        }

        public void Tick(float dt)
        {
            if (!Complete) StepTime += dt;
        }

        public void Report(TutorialStep step)
        {
            if (Complete || done[(int)step]) return;
            done[(int)step] = true;
            Advance();
        }

        public bool IsDone(TutorialStep step) => done[(int)step];

        void Advance()
        {
            bool moved = false;
            while (index < Order.Length && done[(int)Order[index]])
            {
                index++;
                moved = true;
            }
            if (!moved) return;
            StepTime = 0f;
            if (Complete) Completed?.Invoke();
            else StepChanged?.Invoke(Current);
        }
    }
}
