using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // ANIM_SPEC: 그레이박스 클립(tools/make_graybox_anim.py)의 가져오기 설정과 Animator Controller.
    // - 클립 이름 = 파일명, Loop Time, 이벤트 이름(1-6)을 .meta에 둔다 → 최종 FBX로 덮어써도 .meta가 유지되어 설정 · 연결이 남는다.
    // - 설정 · 컨트롤러 모두 "아직 없을 때만" 만든다 (animator 브랜치에서 Humanoid 전환 · 이벤트 시점을 고친 것은 보존).
    static class AnimationGraybox
    {
        public const string Dir = "Assets/_Project/Art/Animations/";

        sealed class Clip
        {
            public string name, folder;
            public bool loop;
            public (string evt, float at)[] events;
        }

        static readonly List<Clip> Clips = new List<Clip>();

        static void C(string name, string folder, bool loop, params (string, float)[] events) =>
            Clips.Add(new Clip { name = name, folder = folder, loop = loop, events = events });

        static AnimationGraybox()
        {
            // 이벤트 시점은 클립 길이 대비 비율(0~1). ANIM_SPEC 3번 "이벤트" 칸
            C("A_KPA_Idle_Aim", "KPA", true);
            C("A_KPA_Walk_Rifle", "KPA", true, ("Footstep", 0f), ("Footstep", 0.5f));
            C("A_KPA_Run_Rifle", "KPA", true, ("Footstep", 0f), ("Footstep", 0.5f));
            C("A_KPA_CrouchRun", "KPA", true, ("Footstep", 0f), ("Footstep", 0.5f));
            C("A_KPA_Crouch_Aim", "KPA", true);
            C("A_KPA_Fire_Stand", "KPA", false, ("Fire", 0.1f));
            C("A_KPA_Fire_Crouch", "KPA", false, ("Fire", 0.1f));
            C("A_KPA_HitReact", "KPA", false);
            C("A_KPA_Death_01", "KPA", false);
            C("A_KPA_Death_02", "KPA", false);
            C("A_KPA_Death_03", "KPA", false);
            C("A_ROK_Idle_Trench", "ROK", true);
            C("A_ROK_Fire_Crouch", "ROK", false, ("Fire", 0.1f));
            C("A_ROK_Call", "ROK", false, ("VoiceStart", 0.1f));
            C("A_ROK_Walk_Tired", "ROK", true, ("Footstep", 0f), ("Footstep", 0.5f));
            C("A_ROK_Idle_Stand", "ROK", true);
            C("A_FP_M1_Idle", "FP", true);
            C("A_FP_M1_ReloadEmpty", "FP", false, ("ClipInsert", 0.45f), ("BoltRelease", 0.7f), ("ReloadDone", 0.92f));
            C("A_FP_M1_ReloadPartial", "FP", false, ("ClipRemove", 0.2f), ("ClipInsert", 0.6f), ("BoltRelease", 0.78f), ("ReloadDone", 0.94f));
            C("A_FP_M1_Equip", "FP", false, ("EquipDone", 0.9f));
            C("A_FP_M1_Unequip", "FP", false, ("UnequipDone", 0.9f));
            C("A_FP_M1_Sprint", "FP", true);
            C("A_FP_Resupply", "FP", false, ("AmmoAdded", 0.65f));
            C("A_FP_Bazooka_Idle", "FP", true);
            C("A_FP_Bazooka_Equip", "FP", false, ("EquipDone", 0.9f));
            C("A_FP_Bazooka_Fire", "FP", false, ("Fire", 0f));
            C("A_FP_Bazooka_Reload", "FP", false, ("RocketInsert", 0.6f), ("ReloadDone", 0.92f));
        }

        public static readonly string[] AllowedEvents =
            { "Fire", "ClipEject", "ClipRemove", "ClipInsert", "BoltRelease", "ReloadDone", "RocketInsert", "EquipDone", "UnequipDone", "AmmoAdded", "VoiceStart", "Footstep" };

        [MenuItem("FiveRivers/Setup/Animation Graybox + Controllers")]
        public static void Run()
        {
            int configured = 0;
            foreach (var c in Clips)
                if (ConfigureImport(c)) configured++;
            var made = new List<string>();
            if (Kpa()) made.Add("AC_KPA");
            if (Rok()) made.Add("AC_ROK");
            if (FpM1()) made.Add("AC_FP_M1");
            if (FpBazooka()) made.Add("AC_FP_Bazooka");
            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] 애니메이션 가져오기 설정 {configured}개, 컨트롤러 생성 {string.Join(", ", made)}");
        }

        static string PathOf(Clip c) => $"{Dir}{c.folder}/{c.name}.fbx";

        static bool ConfigureImport(Clip c)
        {
            var importer = AssetImporter.GetAtPath(PathOf(c)) as ModelImporter;
            if (importer == null || importer.clipAnimations.Length > 0) return false; // 이미 설정됨 → 보존
            var source = importer.defaultClipAnimations.FirstOrDefault();
            if (source == null) return false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            float frames = Mathf.Max(1f, source.lastFrame - source.firstFrame);
            source.name = c.name;
            source.loopTime = c.loop;
            source.events = c.events.Select(e => new AnimationEvent { functionName = e.evt, time = e.at }).ToArray();
            importer.clipAnimations = new[] { source };
            importer.SaveAndReimport();
            return true;
        }

        public static AnimationClip Load(string name)
        {
            var c = Clips.First(x => x.name == name);
            return AssetDatabase.LoadAllAssetsAtPath(PathOf(c)).OfType<AnimationClip>().FirstOrDefault(a => a.name == name);
        }

        // ---- Animator Controller (ANIM_SPEC 1-5 매개변수, 변경 금지) ----
        static AnimatorController NewController(string name, out AnimatorStateMachine sm, out bool created)
        {
            string path = Dir + name + ".controller";
            created = false;
            sm = null;
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) return null;
            created = true;
            GrayboxAssets.EnsureFolder(Dir);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            sm = ac.layers[0].stateMachine;
            return ac;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, string clip, Vector2 pos)
        {
            var s = sm.AddState(name, pos);
            s.motion = Load(clip);
            return s;
        }

        static void Back(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.1f;
        }

        static AnimatorStateTransition When(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string param)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(mode, threshold, param);
            return t;
        }

        static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, string trigger)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            return t;
        }

        static bool Kpa()
        {
            var ac = NewController("AC_KPA", out var sm, out bool created);
            if (!created) return false;
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("DieIndex", AnimatorControllerParameterType.Int);
            ac.AddParameter("Retreat", AnimatorControllerParameterType.Bool);
            var idle = State(sm, "Idle_Aim", "A_KPA_Idle_Aim", new Vector2(300, 0));
            var walk = State(sm, "Walk", "A_KPA_Walk_Rifle", new Vector2(550, -80));
            var run = State(sm, "Run", "A_KPA_Run_Rifle", new Vector2(800, -80));
            var crouch = State(sm, "Crouch_Aim", "A_KPA_Crouch_Aim", new Vector2(300, 150));
            var crouchRun = State(sm, "CrouchRun", "A_KPA_CrouchRun", new Vector2(550, 150));
            var fireStand = State(sm, "Fire_Stand", "A_KPA_Fire_Stand", new Vector2(300, -200));
            var fireCrouch = State(sm, "Fire_Crouch", "A_KPA_Fire_Crouch", new Vector2(50, 150));
            var hit = State(sm, "HitReact", "A_KPA_HitReact", new Vector2(50, -200));
            sm.defaultState = idle;
            When(idle, walk, AnimatorConditionMode.Greater, 0.1f, "Speed");
            When(walk, idle, AnimatorConditionMode.Less, 0.1f, "Speed");
            When(walk, run, AnimatorConditionMode.Greater, 2.5f, "Speed");
            When(run, walk, AnimatorConditionMode.Less, 2.5f, "Speed");
            When(idle, crouch, AnimatorConditionMode.If, 0, "Crouch");
            When(crouch, idle, AnimatorConditionMode.IfNot, 0, "Crouch");
            When(crouch, crouchRun, AnimatorConditionMode.Greater, 0.1f, "Speed");
            When(crouchRun, crouch, AnimatorConditionMode.Less, 0.1f, "Speed");
            Any(sm, fireStand, "Fire").AddCondition(AnimatorConditionMode.IfNot, 0, "Crouch");
            Any(sm, fireCrouch, "Fire").AddCondition(AnimatorConditionMode.If, 0, "Crouch");
            Back(fireStand, idle);
            Back(fireCrouch, crouch);
            Any(sm, hit, "Hit");
            Back(hit, idle);
            for (int i = 0; i < 3; i++)
            {
                var death = State(sm, $"Death_0{i + 1}", $"A_KPA_Death_0{i + 1}", new Vector2(50 + i * 250, 320));
                Any(sm, death, "Die").AddCondition(AnimatorConditionMode.Equals, i, "DieIndex");
            }
            return true;
        }

        static bool Rok()
        {
            var ac = NewController("AC_ROK", out var sm, out bool created);
            if (!created) return false;
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Call", AnimatorControllerParameterType.Trigger);
            var stand = State(sm, "Idle_Stand", "A_ROK_Idle_Stand", new Vector2(300, 0));
            var trench = State(sm, "Idle_Trench", "A_ROK_Idle_Trench", new Vector2(300, 150));
            var walk = State(sm, "Walk_Tired", "A_ROK_Walk_Tired", new Vector2(550, 0));
            var fire = State(sm, "Fire_Crouch", "A_ROK_Fire_Crouch", new Vector2(550, 150));
            var call = State(sm, "Call", "A_ROK_Call", new Vector2(300, -150));
            sm.defaultState = stand;
            When(stand, trench, AnimatorConditionMode.If, 0, "Crouch");
            When(trench, stand, AnimatorConditionMode.IfNot, 0, "Crouch");
            When(stand, walk, AnimatorConditionMode.Greater, 0.1f, "Speed");
            When(walk, stand, AnimatorConditionMode.Less, 0.1f, "Speed");
            Any(sm, fire, "Fire");
            Back(fire, trench);
            Any(sm, call, "Call");
            Back(call, stand);
            return true;
        }

        static bool FpM1()
        {
            var ac = NewController("AC_FP_M1", out var sm, out bool created);
            if (!created) return false;
            foreach (var t in new[] { "Fire", "ReloadEmpty", "ReloadPartial", "Equip", "Unequip", "Resupply" })
                ac.AddParameter(t, AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Sprint", AnimatorControllerParameterType.Bool);
            var idle = State(sm, "Idle", "A_FP_M1_Idle", new Vector2(300, 0));
            var sprint = State(sm, "Sprint", "A_FP_M1_Sprint", new Vector2(550, 0));
            sm.defaultState = idle;
            When(idle, sprint, AnimatorConditionMode.If, 0, "Sprint");
            When(sprint, idle, AnimatorConditionMode.IfNot, 0, "Sprint");
            int y = 150;
            foreach (var (state, clip, trigger) in new[]
                     {
                         ("ReloadEmpty", "A_FP_M1_ReloadEmpty", "ReloadEmpty"), ("ReloadPartial", "A_FP_M1_ReloadPartial", "ReloadPartial"),
                         ("Equip", "A_FP_M1_Equip", "Equip"), ("Unequip", "A_FP_M1_Unequip", "Unequip"), ("Resupply", "A_FP_Resupply", "Resupply"),
                     })
            {
                var s = State(sm, state, clip, new Vector2(300, y));
                Any(sm, s, trigger);
                Back(s, idle);
                y += 80;
            }
            return true;
        }

        static bool FpBazooka()
        {
            var ac = NewController("AC_FP_Bazooka", out var sm, out bool created);
            if (!created) return false;
            foreach (var t in new[] { "Fire", "Reload", "Equip", "Unequip" })
                ac.AddParameter(t, AnimatorControllerParameterType.Trigger);
            var idle = State(sm, "Idle", "A_FP_Bazooka_Idle", new Vector2(300, 0));
            sm.defaultState = idle;
            int y = 150;
            foreach (var (state, clip, trigger) in new[] { ("Fire", "A_FP_Bazooka_Fire", "Fire"), ("Reload", "A_FP_Bazooka_Reload", "Reload"), ("Equip", "A_FP_Bazooka_Equip", "Equip") })
            {
                var s = State(sm, state, clip, new Vector2(300, y));
                Any(sm, s, trigger);
                Back(s, idle);
                y += 80;
            }
            return true;
        }
    }
}
