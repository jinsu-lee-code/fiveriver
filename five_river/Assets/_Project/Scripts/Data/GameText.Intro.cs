namespace FiveRivers.Data
{
    public sealed partial class GameText
    {
        // 2026-11-26 피드백 "처음에 컷씬 하나, 검은 배경에 다부동 전투 날짜, 그다음 참호 방어로 자연스럽게 이어지는 가이드".
        // 새로 쓴 문구: 기획(D) 확인 필요. 에셋에 아직 없으므로 코드의 초기값이 쓰인다.
        // 날짜 근거 (HISTORY_CHECK §8): 『6·25전쟁사 5권 낙동강선 방어작전』 168~169쪽 — 8월 21일 밤 신주막–다부동 접근로
        // 양측 능선과 5번 도로로 전차 · 보병 야간 공격, 미군 조명탄, 국군 제1사단이 좌우 감제고지를 장악(168쪽).
        [UnityEngine.Header("시작 컷씬 (검은 화면)")]
        public string introDate = "1950년 8월 21일 밤";
        public string introPlace = "경상북도 칠곡군 다부동";
        [UnityEngine.TextArea] public string introContext = "대구로 가는 길목.\n국군 제1사단이 골짜기 양쪽 능선을 지키고 있었다.";
        public string introSkip = "[Enter]  건너뛰기";

        [UnityEngine.Header("참호 방어로 이어지는 안내")]
        public Line introLeaderWake = new Line { speaker = "분대장", text = "오석구, 정신 차려라. 놈들은 어두울 때 온다.", duration = 3.5f };
        public Line introLeaderPractice = new Line { speaker = "분대장", text = "총부터 손에 익혀 둬라.", duration = 2.5f };
        public Line holdTrenchLine = new Line { speaker = "분대장", text = "됐다. 이제 제자리 지켜라. 놈들은 저 골짜기로 온다.", duration = 3.5f };
        public string objectiveHoldTrench = "참호에서 골짜기 쪽을 지켜라";
        public string guideTurnLeft = "왼쪽으로 돌아라";
        public string guideTurnRight = "오른쪽으로 돌아라";
        public string guideTurnBack = "뒤로 돌아라";

        // 조작 한 가지를 해냈을 때 분대장의 짧은 대답 (차례로 돌려 씀, 마지막 단계는 holdTrenchLine). 다른 대사 중이면 건너뜀.
        public Line[] tutorialAcks =
        {
            new Line { speaker = "분대장", text = "그렇지.", duration = 1.4f },
            new Line { speaker = "분대장", text = "좋아.", duration = 1.4f },
            new Line { speaker = "분대장", text = "옳지, 그렇게.", duration = 1.6f },
            new Line { speaker = "분대장", text = "잘했다.", duration = 1.4f },
        };
    }
}
