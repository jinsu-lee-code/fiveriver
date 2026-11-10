using UnityEngine;

namespace FiveRivers.Data
{
    public sealed partial class GameText
    {
        // 순서 (결정 2026-11-09): 전차 → 박용팔과 능선 탈환 → 정상에서 새벽, 보충병 윤태석 합류 → 결말
        [Header("고지 탈환과 새벽 합류")]
        public string hillExit = "오른쪽 참호 끝으로 나가라";
        public string hillLower = "비탈 아래 첫 엄폐물로 전진";
        public string hillUpper = "두 번째 엄폐선을 확보하라";
        public string hillRidge = "오른쪽 능선 엄폐물로 전진";
        public string hillSummit = "정상 맨 위 바위까지 올라라";
        public string hillDefenderCount = "{0} · 수비병 {1}명";
        public string hillNavigation = "{0} · {1} 약 {2}m";
        public string hillAhead = "앞";
        public string hillBehind = "뒤";
        public string hillRight = "오른쪽";
        public string hillLeft = "왼쪽";
        public string watchTruck = "비탈 아래 보충병 트럭을 내려다보라";
        public string findTaeSeok = "올라오는 보충병 윤태석을 맞아라";
        public string meetTaeSeok = "F  윤태석과 대화";

        public Line parkDeparts = ParkLine("전차는 잡았다. 날 새기 전에 능선 되찾는다잉. 오른쪽 참호 끝으로!");
        public Line parkLower = ParkLine("첫 엄폐물 앞에 하나 있다! 소총으로 눌러!");
        public Line parkUpper = ParkLine("저 위 두 번째 엄폐선이다. 오른쪽 비탈로 붙어!");
        public Line parkRidge = ParkLine("능선 오른편에 한 놈 더 있다잉!");
        public Line parkSummit = ParkLine("마지막 능선이다. 끝까지 올라가!");

        public Line taeSeokMeets = new Line { speaker = "윤태석", text = "포항에서 왔습니다. 윤태석입니다.", duration = 3.5f };
        public Line parkMansuPlace = ParkLine("만수 자리다. 거기 엎드려라.");

        static Line ParkLine(string content) => new Line { speaker = "박용팔", text = content, duration = 3.5f };
    }
}
