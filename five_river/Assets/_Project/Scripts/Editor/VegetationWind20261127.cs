using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FiveRivers.EditorTools
{
    // 2026-11-27 (감독 세션 전달 — 사용자 지시 "나무가 흔들림이 없어서 바람이 없는 것 같다 … 사실적인 디테일은 놓치지 말고"):
    // the leaves, needles and grass move in one light summer-night wind (1-3 m/s, now and then a gust), all the same way as the
    // smoke, the volumetric fog and the poplars already went (east, a little north). Trunks and stems stay as they are.
    //  - Lit vegetation materials (shrubs, the old pines, poplar leaves, the cut boughs' needles) go onto FiveRivers/WindLit, a
    //    copy of URP Lit that only adds the wind in the vertex stage: every texture and setting of the material stays.
    //  - The grass and the new red pines' needles (FiveRivers/WildGrass) get the same direction.
    //  - The burning stumps' smoke leaned a little south while everything else leaned north: turned to the same side.
    // Re-running gives the same result. Undo: put the materials back on "Universal Render Pipeline/Lit".
    public static class VegetationWind20261127
    {
        public static readonly Vector4 Direction = new Vector4(.95f, 0f, .32f, 0f);
        const string Art = "Assets/_Project/Art/";

        // material, plant height (m), sway at the top (m), leaf flutter (m)
        static readonly (string path, float height, float sway, float flutter)[] LitPlants =
        {
            ("Maps/DabudongRefined/Source/Art/EnvironmentCharacters20261023/Summer_Shrub/Summer_Shrub_PBR.mat", 1.2f, .05f, .015f),
            ("Maps/ClaudePolish20261030/Nature/M_BG_Bush.mat", .7f, .04f, .012f),
            ("Maps/Dabudong/Source/Art/Background/M_BG_Bush.mat", .7f, .04f, .012f),
            ("Maps/ClaudePolish20261030/Nature/M_BG_Pine.mat", 7f, .16f, .02f),
            ("Maps/Dabudong/Source/Art/Background/M_BG_Pine.mat", 7f, .16f, .02f),
            ("Maps/DabudongRefined/Source/Art/EnvironmentCharacters20261023/Korean_Red_Pine/Korean_Red_Pine_PBR.mat", 6f, .14f, .02f),
            // the poplars lean as a whole already (ValleyAmbience); their leaves only shimmer
            ("Maps/Dabudong1950/Poplar/M_Poplar_Leaves.mat", 14f, 0f, .03f),
            ("Maps/Dabudong1950/Poplar/M_Poplar_Far.mat", 14f, 0f, .02f),
            // the cut boughs lie on the parapet: the needles stir a little, the bough does not move
            ("MapPolish20261105/PositionDressing20261125/M_PD_PineNeedles.mat", .5f, .006f, .008f),
            ("MapPolish20261105/PositionDressing20261125/M_PD_PineNeedlesDry.mat", .5f, .006f, .008f),
        };

        [MenuItem("FiveRivers/Setup/Vegetation Wind 20261127")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var wind = Shader.Find("FiveRivers/WindLit");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (wind == null) throw new System.InvalidOperationException("FiveRivers/WindLit 셰이더가 없음 (source/environment/wind_20261127/make_windlit.py)");
            var sb = new StringBuilder($"wind direction {Direction}\n");
            foreach (var (path, height, sway, flutter) in LitPlants)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(Art + path);
                if (m == null) { sb.AppendLine("  missing " + path); continue; }
                if (m.shader != wind && m.shader != lit) { sb.AppendLine($"  left as it is (shader {m.shader.name}) {path}"); continue; }
                Undo.RecordObject(m, "Vegetation Wind");
                if (m.shader != wind)
                {
                    int queue = m.renderQueue;
                    m.shader = wind;   // same property names: every texture and value stays
                    m.renderQueue = queue;
                }
                m.SetFloat("_WindHeight", height); m.SetFloat("_WindStrength", sway); m.SetFloat("_WindFlutter", flutter);
                m.SetFloat("_WindSpeed", 1.1f); m.SetVector("_WindDirection", Direction);
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                sb.AppendLine($"  {Path.GetFileNameWithoutExtension(path)}: height {height} m, sway {sway} m, flutter {flutter} m");
            }
            int grass = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || m.shader == null || m.shader.name != "FiveRivers/WildGrass") continue;
                Undo.RecordObject(m, "Vegetation Wind");
                m.SetVector("_WindDirection", Direction);
                EditorUtility.SetDirty(m); grass++;
            }
            sb.AppendLine($"  WildGrass materials given the same direction: {grass}");
            // the stumps' smoke (BattlefieldTracesSetup): the wind leaned it toward -z; everything else leans toward +z
            int smoke = 0;
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var f = ps.forceOverLifetime;
                if (!f.enabled || Mathf.Abs(f.x.constant - .055f) > .001f || f.z.constant > 0f) continue;
                Undo.RecordObject(ps, "Vegetation Wind");
                f.z = new ParticleSystem.MinMaxCurve(.055f * Direction.z / Direction.x);
                smoke++;
            }
            sb.AppendLine($"  smoke columns turned to the same wind: {smoke}");
            AssetDatabase.SaveAssets();
            if (smoke > 0) { EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); }
            File.WriteAllText("Temp/vegetation_wind.txt", sb.ToString());
            Debug.Log("[VegetationWind20261127]\n" + sb);
            return sb.ToString();
        }
    }
}
