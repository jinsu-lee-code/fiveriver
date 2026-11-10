using System;
using UnityEngine;

namespace FiveRivers.Data
{
    // 화면에 나오는 모든 문구 (안내 · 목표 · 자막 · 결과 화면). D 기획과 검증이 확정한다 (TASKS 1-6).
    // 문구는 코드에 쓰지 않고 이 에셋에서 고친다. 음성 파일명(SOUND_LIST)과 자막을 함께 둔다.
    [CreateAssetMenu(fileName = "GameText", menuName = "FiveRivers/Game Text")]
    public sealed partial class GameText : ScriptableObject
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
        public string combatBrightHint;
        public string combatDarkHint;
        public string objectiveSilence;
        public string objectiveTankMove;
        public string objectiveTankEquip;
        public string objectiveTankFire;
        public string objectiveTankFinish;
        public string objectiveTankWait;
        public string objectiveAllySupport;
        public string objectiveDawn;

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
        public Line tankFirstHit;
        public Line allySupport;

        [Header("결과 화면 (S7)")]
        public string resultDate;
        public string resultName;
        [TextArea(3, 8)] public string resultLetter;
        public string resultTitle;

        [Header("다부동 확장 임무")]
        public string objectiveRegroupSupply = "탄약 상자에서 예비탄을 보충하라";
        public string objectiveRegroupTalk = "참호 왼쪽 끝의 최만수에게 다가가라";
        public string objectiveWest = "참호 왼쪽을 지켜라 · 서쪽 비탈을 막아라";
        public string mansuPage = "최만수가 어머니께 보내는 편지";
        public string seokguPage = "오석구가 아내에게 쓰는 편지";
        public string letterNext = "[Enter]  다음 장";
        public string letterClose = "[Enter]  편지 접기";
        public string missionReturn = "[Enter]  처음 화면으로";
        public string seokguLetter = "여보, 오늘 밤에도 하늘에 해가 몇 번이나 떴다 졌소.\n만수가 제 얘기도 한 줄 써 달라기에 그러마 했소.\n오늘 새로 온 아이 이름은 태석이오.\n이름을 잊을까 봐, 아침이 가기 전에 적어 두오.\n\n다부동에서, 석구";
        public Line mansuLetter = new Line { speaker = "최만수", text = "형님, 편지 쓰실 때 제 얘기도 한 줄 써 주이소. 제가 글을 모르가지고예.", duration = 5f };
        public Line mansuMorning = new Line { speaker = "최만수", text = "아침 되면… 좀 조용해지겠지예.", duration = 3f };
        public Line westWarning = new Line { speaker = "박용팔", text = "옆구리다! 서쪽이다잉!", duration = 3f };
        public Line mansuLost = new Line { speaker = "박용팔", text = "만수야!", duration = 2f };
    }
}
