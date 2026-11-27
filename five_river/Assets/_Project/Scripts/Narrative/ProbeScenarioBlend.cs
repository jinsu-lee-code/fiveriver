using UnityEngine.Rendering;

namespace FiveRivers.Narrative
{
    // 간접광(APV) 밤 · 새벽 시나리오 섞기 (부족한 점 2번, 2026-11-26). DawnDirector 가 달빛 · 주변광을 섞는 같은 비율로
    // 구워 둔 두 시나리오를 섞는다. 구운 데이터가 없거나 APV 가 꺼져 있으면 아무것도 하지 않는다.
    // 굽기: 메뉴 FiveRivers/Setup/Probe Volumes (APV) Bake Night + Dawn
    public static class ProbeScenarioBlend
    {
        public const string Night = "Night", Dawn = "Dawn";
        const float Step = 0.01f; // 이만큼 바뀔 때만 다시 섞는다 (섞기는 셀마다 GPU 작업)

        static float last = -1f;

        static bool Has(ProbeVolumeBakingSet set, string scenario)
        {
            var list = set.lightingScenarios;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++) if (list[i] == scenario) return true;
            return false;
        }

        public static void Blend(float dawn)
        {
            var volume = ProbeReferenceVolume.instance;
            if (volume == null || !volume.isInitialized) return;
            var set = volume.currentBakingSet;
            if (set == null || !Has(set, Night) || !Has(set, Dawn)) return;
            if (volume.lightingScenario != Night) { volume.lightingScenario = Night; last = -1f; }
            if (last >= 0f && System.Math.Abs(dawn - last) < Step && dawn > 0f && dawn < 1f) return;
            last = dawn;
            volume.BlendLightingScenario(Dawn, dawn);
        }
    }
}
