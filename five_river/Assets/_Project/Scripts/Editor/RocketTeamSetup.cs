using System;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using FiveRivers.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 로켓포반 설정 (2026-11-24). 여러 번 실행해도 결과가 같다. Dabudong 씬을 연 상태에서, 플레이 중이 아닐 때. 기록: Temp/rocket_team.txt
    //  1. 수치 에셋 Data/RocketTeamConfig.asset (없을 때만 만든다 — 손으로 고친 값은 지키지 않고 덮지도 않는다)
    //  2. 탄약수 클립 7개 + 소총 궤적 + 몸 맞춤: ROK Rigify Clips 메뉴(RokRigifyClipsSetup.Apply)를 부른다 (열린 씬의 아군에 맞춘다)
    //  3. 로켓 소품 궤적 *_prop.json → Art/Resources/LoaderPropTracks.asset
    //  4. 씬: [RocketTeam] 오브젝트에 LoaderTeamDirector 를 붙이고 연결 (탄약수 = 씬의 Ally_3, 로켓 모델 = SM_WP_BazookaRocket.fbx)
    public static class RocketTeamSetup
    {
        const string PropAsset = "Assets/_Project/Art/Resources/" + LoaderPropTracks.ResourceName + ".asset";
        const string RocketModel = "Assets/_Project/Art/Models/Weapons/Bazooka/SM_WP_BazookaRocket.fbx";
        const string Report = "Temp/rocket_team.txt";
        static readonly string[] Clips =
        {
            "A_ROK_Loader_Kneel_In", "A_ROK_Loader_Kneel_Idle", "A_ROK_Loader_Load", "A_ROK_Loader_Reach",
            "A_ROK_Loader_Brace", "A_ROK_Loader_Wave", "A_ROK_Loader_ToRifle_Stand",
        };

        static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "source", "anim", "rigify_rok_20261118", "export"));

        [MenuItem("FiveRivers/Setup/Rocket Team")]
        public static string Apply()
        {
            var log = new StringBuilder();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Finish(log.AppendLine("플레이 중에는 실행하지 않는다"));

            // 1. 수치
            var config = AssetDatabase.LoadAssetAtPath<RocketTeamConfig>(RocketTeamConfig.AssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RocketTeamConfig>();
                AssetDatabase.CreateAsset(config, RocketTeamConfig.AssetPath);
                log.AppendLine("created " + RocketTeamConfig.AssetPath);
            }

            // 2. 클립 · 소총 궤적 · 몸 맞춤
            string clipResult = RokRigifyClipsSetup.Apply();
            log.AppendLine("ROK Rigify Clips: " + clipResult);
            var tracks = AssetDatabase.LoadAssetAtPath<AllyRifleTracks>("Assets/_Project/Art/Resources/" + AllyRifleTracks.ResourceName + ".asset");
            foreach (var name in Clips)
                log.AppendLine($"  {name}: {(tracks != null && tracks.Find(name) != null && tracks.Find(name).clip != null ? "ok" : "MISSING (fbx · _rifle.json 확인)")}");
            if (tracks != null && tracks.fits.All(f => f.actor != config.loaderName))
                log.AppendLine($"  경고: {config.loaderName} 의 몸 맞춤(Fit)이 없다 — Dabudong 씬을 연 채로 다시 실행");

            // 3. 로켓 소품 궤적
            var props = BuildProps(log);

            // 4. 씬
            var scene = EditorSceneManager.GetActiveScene();
            var points = Object.FindFirstObjectByType<LevelPoints>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var director = Object.FindFirstObjectByType<FlowDirector>();
            if (points == null || player == null || director == null)
            {
                log.AppendLine("씬(Dabudong)이 열려 있지 않다: 자료만 만들었다. 씬을 열고 다시 실행");
                AssetDatabase.SaveAssets();
                return Finish(log);
            }
            var loader = Object.FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(a => a.name == config.loaderName);
            if (loader == null) log.AppendLine("경고: 씬에 " + config.loaderName + " 이(가) 없다 — 대체 모드(혼자 장전)로 돈다");

            var go = GameObject.Find("/[RocketTeam]") ?? new GameObject("[RocketTeam]");
            var team = SetupUtil.Ensure<LoaderTeamDirector>(go);
            SetupUtil.SetRef(team, "config", config);
            SetupUtil.SetRef(team, "director", director);
            SetupUtil.SetRef(team, "points", points);
            SetupUtil.SetRef(team, "player", player);
            SetupUtil.SetRef(team, "weapons", player.GetComponent<WeaponHolder>());
            SetupUtil.SetRef(team, "subtitles", Object.FindFirstObjectByType<SubtitleDisplay>());
            SetupUtil.SetRef(team, "text", AssetDatabase.LoadAssetAtPath<GameText>("Assets/_Project/Data/GameText.asset"));
            SetupUtil.SetRef(team, "rocketModel", AssetDatabase.LoadAssetAtPath<GameObject>(RocketModel));
            SetupUtil.SetRef(team, "loader", loader);
            EditorUtility.SetDirty(team);
            log.AppendLine($"[RocketTeam] 연결: loader {(loader != null ? loader.name : "없음")}, 소품 궤적 {(props != null ? props.clips.Length + "개" : "없음")}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return Finish(log);
        }

        static string Finish(StringBuilder log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, log.ToString());
            Debug.Log("[RocketTeam] " + log.ToString().Split('\n').Reverse().FirstOrDefault(l => l.Trim().Length > 0));
            return log.ToString();
        }

        // ---- 로켓 소품 궤적 ---------------------------------------------------------------------------------------
        [Serializable] sealed class PropJson { public string clip; public float fps; public PropFrameJson[] frames; public float rocket_length_m; public SlotJson[] pile_slots; public GunnerJson gunner; }
        [Serializable] sealed class PropFrameJson { public int f; public float[] pos; public float[] forward; public float[] up; public string state; public float insert; public float grip_L; public float grip_R; }
        [Serializable] sealed class SlotJson { public float[] pos; public float[] forward; }
        [Serializable] sealed class GunnerJson { public float[] feet; public float[] eye; public float[] rear_mouth; public float[] tube_axis; public float[] tap; }

        static LoaderPropTracks BuildProps(StringBuilder log)
        {
            var set = AssetDatabase.LoadAssetAtPath<LoaderPropTracks>(PropAsset);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<LoaderPropTracks>();
                AssetDatabase.CreateAsset(set, PropAsset);
            }
            var clips = new System.Collections.Generic.List<LoaderPropTracks.Clip>();
            foreach (var name in Clips)
            {
                string path = Path.Combine(SourceDir, name + "_prop.json");
                if (!File.Exists(path)) { var old = set.Find(name); if (old != null) clips.Add(old); log.AppendLine("  prop json 없음: " + name + (old != null ? " (전에 구운 것을 둔다)" : "")); continue; }
                var json = JsonUtility.FromJson<PropJson>(File.ReadAllText(path));
                if (json == null || json.frames == null || json.frames.Length == 0) { log.AppendLine("  bad json " + path); continue; }
                if (Mathf.Abs(json.fps - 30f) > 0.01f) log.AppendLine($"  {name}: fps {json.fps} (30 이 아님)");
                var clip = new LoaderPropTracks.Clip { name = name, fps = json.fps > 0f ? json.fps : 30f, frames = new LoaderPropTracks.Frame[json.frames.Length] };
                for (int i = 0; i < clip.frames.Length; i++)
                {
                    var f = json.frames[i];
                    clip.frames[i] = new LoaderPropTracks.Frame
                    {
                        position = V(f.pos),
                        rotation = Quaternion.LookRotation(NonZero(V(f.forward)), NonZero(V(f.up), Vector3.up)),
                        state = State(f.state),
                        insert = f.insert,
                        gripLeft = Mathf.Clamp01(f.grip_L),
                        gripRight = Mathf.Clamp01(f.grip_R),
                    };
                }
                clips.Add(clip);
                // 첫 클립의 공통 자료 (사수 자리 · 세워 둔 로켓)
                if (json.pile_slots != null && json.pile_slots.Length > 0)
                    set.pile = json.pile_slots.Select(p => new LoaderPropTracks.Slot { position = V(p.pos), rotation = Quaternion.LookRotation(NonZero(V(p.forward)), Vector3.back) }).ToArray();
                if (json.rocket_length_m > 0f) set.rocketLength = json.rocket_length_m;
                if (json.gunner != null)
                {
                    set.gunnerFeet = V(json.gunner.feet); set.gunnerEye = V(json.gunner.eye); set.rearMouth = V(json.gunner.rear_mouth);
                    set.tubeAxis = V(json.gunner.tube_axis); set.tapPoint = V(json.gunner.tap);
                }
                log.AppendLine($"  prop {name}: {clip.frames.Length} frames, states " + string.Join(" ", clip.frames.GroupBy(f => f.state).Select(g => g.Key + ":" + g.Count())));
            }
            set.clips = clips.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            return set;
        }

        static LoaderPropTracks.PropState State(string s)
        {
            switch (s)
            {
                case "hands": return LoaderPropTracks.PropState.Hands;
                case "hand_R": return LoaderPropTracks.PropState.HandR;
                case "hand_L": return LoaderPropTracks.PropState.HandL;
                case "tube": return LoaderPropTracks.PropState.Tube;
                case "pile": return LoaderPropTracks.PropState.Pile;
                default: return LoaderPropTracks.PropState.Hidden;
            }
        }

        static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;
        static Vector3 NonZero(Vector3 v, Vector3? fallback = null) => v.sqrMagnitude > 1e-6f ? v : fallback ?? Vector3.forward;
    }
}
