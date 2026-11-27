using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-27 (아트 검수 "참호 바닥 켜진 등불 상자가 하얗게 날아감 — 야간 진지에 불"):
    // the candle by the ammunition boxes (AmmoCrateM1917Setup: Supply_bay_lantern) carried the supply bay's old light, 0.9 at
    // 4.2 m. The open box stands 11 cm from the flame, so its side burnt out to plain white and the floor round it glowed for
    // two metres: a lit fire in a night position. Now a stub of candle behind a tin: the light is just enough to read the
    // yellow stencils on the box beside it and to mark the place from along the trench (the boxes must still be found in the dark).
    // Numbers: BALANCE.md 4-6. Changes the Dabudong scene (saves it). Re-running gives the same result.
    public static class TrenchCandle20261127
    {
        public const float Intensity = 0.12f, Range = 2.2f;   // was 0.9, 4.2

        [MenuItem("FiveRivers/Setup/Trench Candle 20261127")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            var lantern = map != null ? map.transform.Find("Frontline_Art_20261102/Supply_bay_lantern") : null;
            var light = lantern != null ? lantern.GetComponent<Light>() : null;
            if (light == null) throw new System.InvalidOperationException("Supply_bay_lantern 의 Light 가 없음");
            string report = $"candle light intensity {light.intensity:0.###} -> {Intensity}, range {light.range:0.##} -> {Range}";
            if (Mathf.Approximately(light.intensity, Intensity) && Mathf.Approximately(light.range, Range)) return "candle light: nothing to change";
            Undo.RecordObject(light, "Trench candle");
            light.intensity = Intensity; light.range = Range;
            EditorUtility.SetDirty(light);
            EditorSceneManager.MarkSceneDirty(lantern.gameObject.scene); EditorSceneManager.SaveScene(lantern.gameObject.scene);
            Debug.Log("[TrenchCandle] " + report);
            return report;
        }
    }
}
