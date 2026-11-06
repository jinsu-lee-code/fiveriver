using UnityEngine;

namespace FiveRivers.Data
{
    public sealed partial class GameText
    {
        [Header("새벽 합류와 고지 점령")]
        public string findTaeSeok = "참호 뒤 보충병 윤태석을 찾아라";
        public string watchTruck = "트럭 쪽을 돌아보고 합류한 학도병을 확인하라";
        public string meetTaeSeok = "F  윤태석과 대화";
        public string hillExit = "오른쪽 참호 끝으로 나가라";
        public string hillLower = "비탈 아래 첫 엄폐물로 전진";
        public string hillUpper = "두 번째 엄폐선을 확보하라";
        public string hillRidge = "오른쪽 능선 엄폐물로 전진";
        public string hillSummit = "윤태석과 정상에 올라라";
        public string hillReport = "F  윤태석에게 고지 확보 신호";
        public string hillDefenderCount = "{0} · 수비병 {1}명";
        public string hillNavigation = "{0} · {1} 약 {2}m";
        public string hillAhead = "앞";
        public string hillBehind = "뒤";
        public string hillRight = "오른쪽";
        public string hillLeft = "왼쪽";

        public Line taeSeokMeets = TaeSeokLine("포항에서 왔습니다. 윤태석입니다.");
        public Line taeSeokDeparts = TaeSeokLine("오른쪽 참호 끝으로 갑시다. 제가 뒤를 보겠습니다.");
        public Line taeSeokLower = TaeSeokLine("첫 엄폐물 앞에 적 하나! 소총으로 제압합시다.");
        public Line taeSeokUpper = TaeSeokLine("저 위 두 번째 엄폐선입니다. 오른쪽 비탈로 붙읍시다.");
        public Line taeSeokRidge = TaeSeokLine("능선 오른편에 한 명 더 있습니다!");
        public Line taeSeokSummit = TaeSeokLine("마지막 능선입니다. 끝까지 같이 갑시다.");

        static Line TaeSeokLine(string content) => new Line { speaker = "윤태석", text = content, duration = 3.5f };
    }
}
