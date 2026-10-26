using System;
using UnityEngine;

namespace FiveRivers.Data
{
    // 화면에 나오는 모든 문구 (안내 · 목표 · 자막 · 결과 화면). D 기획과 검증이 확정한다 (TASKS 1-6).
    // 문구는 코드에 쓰지 않고 이 에셋에서 고친다. 음성 파일명(SOUND_LIST)과 자막을 함께 둔다.
    [CreateAssetMenu(fileName = "GameText", menuName = "FiveRivers/Game Text")]
    public sealed class GameText : ScriptableObject
    {
        [Serializable]
        public struct Line
        {
            public string speaker;
            [TextArea] public string text;
            public float duration;
            public string voice; // SOUND_LIST 파일명 (확장자 없이)
        }

        [Serializable]
        public struct Step
        {
            public string prompt; // 키 표시 옆 짧은 문구
            [TextArea] public string hint; // 30초 정체 때 덧붙는 설명
        }

        [Header("타이틀 · 일시정지")]
        public string gameTitle;
        public string gameSubtitle;
        public string startPrompt;
        public string operatorHint;
        public string pauseTitle;
        public string resumeLabel;
        public string restartLabel;

        [Header("튜토리얼 (순서: 시점 · 이동 · 자세 · 사격 · 장전)")]
        public Step[] tutorialSteps = new Step[5];
        public string tutorialRemainingHeader;
        public string mouseKeyLabel;

        [Header("목표 (화면 왼쪽 위)")]
        public string objectiveLabel;
        public string objectiveTutorial;
        public string objectiveScout;
        public string objectiveAssault;
        public string objectiveSilence;
        public string objectiveTankMove;
        public string objectiveTankFire;
        public string objectiveAllySupport;

        [Header("상호작용 안내")]
        public string resupplyPrompt;
        public string bazookaPrompt;
        public string m1Name;
        public string bazookaName;

        [Header("자막 (음성)")]
        public Line scoutSpotted;
        public Line flareUp;
        public Line flareDown;
        public Line retreat;
        public Line parkCall1;
        public Line parkCall2;
        public Line tankArrives;
        public Line tankRicochet;
        public Line allySupport;

        [Header("결과 화면 (S7)")]
        public string resultDate;
        public string resultName;
        [TextArea(3, 8)] public string resultLetter;
        public string resultTitle;
    }
}
