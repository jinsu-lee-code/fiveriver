namespace FiveRivers.Data
{
    public sealed partial class GameText
    {
        // 2026-11-24 로켓포반. 키와 임시 문구만: 문구는 기획 확인 필요 (docs/dialogue_rocket_team 제안 참고).
        // 기존 GameText.asset 에는 이 값이 없으므로 코드의 초기값이 쓰인다 (에셋을 다시 저장하면 그 값이 에셋에 들어간다).
        [UnityEngine.Header("로켓포반 (탄약수) — 문구는 기획 확인 필요")]
        public Line loaderCall = new Line { speaker = "탄약수", text = "사수님, 이쪽입니다!", duration = 2.5f };
        public Line loaderReady = new Line { speaker = "탄약수", text = "장전 끝!", duration = 1.5f };
    }
}
