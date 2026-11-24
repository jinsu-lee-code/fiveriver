using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Rigify 조절 리그로 만든 국군 동작 클립 (2026-11-18, source/anim/rigify_rok_20261118) 을 게임에 넣는 메뉴.
    // 여러 번 실행해도 결과가 같다. 기록: Temp/rok_rigify_clips.txt
    //  1. export/*.fbx 를 Art/Animations/ROK/ 로 복사 (없거나 내용이 다를 때만)
    //  2. Humanoid: 아바타는 A_ROK_Rigify_TPose.fbx 에서 만들고(이름이 Unity 사람 뼈 이름 그대로라 빈 매핑은 이름으로 채움)
    //     나머지 클립은 그 아바타를 복사. 클립 이름 = 파일명, 반복 클립만 Loop Time, 루트 회전 · 높이 · 수평 위치는 원본 기준으로
    //     자세에 굽기(제자리), 압축은 기존 A_ROK_* 와 같게(Keyframe Reduction).
    //  3. export/*_rifle.json (프레임마다 소총 위치 · 방향 · 손 접촉) → Art/Resources/AllyRifleTracks.asset
    //  4. 열린 씬(Dabudong)의 아군마다 그 몸에서 클립을 떠서 맞춘다: 참호 바닥(서서 대기 발바닥 높이) · JSON → 모델 단위 ·
    //     프레임마다 가슴뼈 기준 소총 자리. 손이 총에 붙은 프레임의 손목 – 손잡이 거리를 기록에 남긴다(리타깃 오차 확인).
    //  5. 고지 돌격 달리기의 새 클립 발 속도: 같은 몸에서 기존 클립 대비 비율로 잰다(HillAllies 의 3.1 / 2.5 기준).
    // 아군이 있는 씬이 열려 있지 않으면 4 · 5 는 건너뛰고 기존 값을 둔다. 컨트롤러 AC_ROK 은 바꾸지 않는다.
    public static class RokRigifyClipsSetup
    {
        const string Folder = "Assets/_Project/Art/Animations/ROK/";
        const string TPoseName = "A_ROK_Rigify_TPose";
        const string TPose = Folder + TPoseName + ".fbx";
        const string SetPath = "Assets/_Project/Art/Resources/" + AllyRifleTracks.ResourceName + ".asset";
        const string Report = "Temp/rok_rigify_clips.txt";
        const string ControllerPath = "Assets/_Project/Art/Animations/AC_ROK.controller";
        // 참호 낮춘 대기 첫 프레임의 엉덩이 높이 (Blender 리그, 바닥 기준 게임 크기 m — README 4번 표 0.351~0.353)
        const float SourceReadyHips = 0.352f;
        const float KneelKneeHeight = 0.082f;   // 꿇은 무릎 관절 중심 높이(m) — source rok_poses.KNEEL_KNEE_Z
        // HillAllies 의 기존 클립 발 속도 (사람 크기 1 기준). 새 클립은 같은 몸에서 잰 비율로 환산한다.
        const float OldRunClipSpeed = 3.1f, OldAdvanceClipSpeed = 2.5f;
        // 잴 수 없을 때 쓰는 값 (README 5번의 추정: 3.0 / 0.84, 2.2 / 0.84)
        const float FallbackRunClipSpeed = 3.57f, FallbackAdvanceClipSpeed = 2.62f;

        static string SourceDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "source", "anim", "rigify_rok_20261118", "export"));

        // 파일명, 쓰임, 반복. 아직 없는 파일(예정)은 건너뛴다: 들어오면 메뉴를 다시 실행하면 된다.
        static readonly (string name, AllyClipRole role, bool loop)[] Clips =
        {
            ("A_ROK_Trench_Ready_Idle", AllyClipRole.Base, true),
            ("A_ROK_Stand_To_TrenchReady", AllyClipRole.EnterFromStand, false),
            ("A_ROK_TrenchReady_To_Stand", AllyClipRole.ExitToStand, false),
            ("A_ROK_TrenchReady_To_FireCrouch", AllyClipRole.ExitToFire, false),
            ("A_ROK_Trench_PeekOver", AllyClipRole.Variation, false),
            ("A_ROK_Trench_CheckRifle", AllyClipRole.Variation, false),
            ("A_ROK_Trench_WipeBrow", AllyClipRole.Variation, false),
            ("A_ROK_Trench_LookSide", AllyClipRole.Variation, false),
            ("A_ROK_Trench_ShiftKnee", AllyClipRole.Variation, false),
            ("A_ROK_Trench_Duck", AllyClipRole.Duck, false),
            ("A_ROK_Run_Rifle", AllyClipRole.Run, true),
            ("A_ROK_CrouchRun_Rifle", AllyClipRole.CrouchRun, true),
            ("A_ROK_Run_Stop", AllyClipRole.RunStop, false),
            // 예정 (README 7번): 들어오면 자동으로 들어간다. 이야기 클립은 AllyClipPlayer.PlayStory(이름) 으로 부른다.
            ("A_ROK_Run_Start", AllyClipRole.RunStart, false),
            ("A_ROK_Kneel_Mourn_In", AllyClipRole.Story, false),
            ("A_ROK_Kneel_Mourn_Loop", AllyClipRole.Story, true),
            ("A_ROK_GoProne_In", AllyClipRole.Story, false),
            ("A_ROK_Prone_Aim_Loop", AllyClipRole.Story, true),
            // 무릎 대기 · 무릎 사격 (2026-11-18 저녁): 컨트롤러 상태 Idle_Trench / Fire_Crouch 의 클립을 실행 중에 바꿔 끼운다.
            // 기존 클립은 꿇은 뒷발이 발끝 하나로 세로로 서 있었다 → 새 클립은 뒷발 발등이 땅, 무릎이 땅
            ("A_ROK_Kneel_Watch_Idle", AllyClipRole.KneelIdle, true),
            ("A_ROK_FireCrouch_Fire", AllyClipRole.KneelFire, false),
            // 로켓포반 탄약수 (2026-11-24, 이야기 클립: LoaderTeamDirector 가 PlayStory 로 부른다). 반복은 Kneel_Idle 만. 로켓 궤적(_prop.json)은 RocketTeamSetup 이 읽는다.
            ("A_ROK_Loader_Kneel_In", AllyClipRole.Story, false),
            ("A_ROK_Loader_Kneel_Idle", AllyClipRole.Story, true),
            ("A_ROK_Loader_Load", AllyClipRole.Story, false),
            ("A_ROK_Loader_Reach", AllyClipRole.Story, false),
            ("A_ROK_Loader_Brace", AllyClipRole.Story, false),
            ("A_ROK_Loader_Wave", AllyClipRole.Story, false),
            ("A_ROK_Loader_ToRifle_Stand", AllyClipRole.Story, false),
        };

        [MenuItem("FiveRivers/Setup/ROK Rigify Clips")]
        public static string Apply()
        {
            var log = new StringBuilder();
            log.AppendLine("source " + SourceDir + (Directory.Exists(SourceDir) ? "" : " (없음: 이미 들어온 파일만 다시 맞춘다)"));

            // 1. 파일 넣기
            CopyIn(TPoseName, log);
            var present = new List<(string name, AllyClipRole role, bool loop)>();
            foreach (var entry in Clips)
                if (CopyIn(entry.name, log)) present.Add(entry);
            AssetDatabase.Refresh();
            if (!File.Exists(TPose)) return Finish(log.AppendLine("MISSING " + TPose));

            // 2. 아바타 · 클립
            var avatar = SetupAvatar(log);
            if (avatar == null || !avatar.isValid || !avatar.isHuman) return Finish(log.AppendLine("FAILED avatar"));

            var set = AssetDatabase.LoadAssetAtPath<AllyRifleTracks>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<AllyRifleTracks>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            var clips = new List<AllyRifleTracks.Clip>();
            foreach (var (name, role, loop) in present)
            {
                var clip = SetupClip(Folder + name + ".fbx", name, loop, avatar, log);
                if (clip == null) { log.AppendLine("FAILED clip " + name); continue; }
                // 3. 소총 궤적
                var frames = ReadTrack(name, log);
                if (frames == null) frames = set.Find(name)?.frames; // JSON 이 없으면 전에 구운 것을 둔다
                if (frames == null || frames.Length == 0) { log.AppendLine($"  {name}: 소총 궤적 없음 — 넣지 않음"); continue; }
                clips.Add(new AllyRifleTracks.Clip { name = name, role = role, clip = clip, loop = loop, length = clip.length, fps = 30f, frames = frames });
                log.AppendLine($"{name}: {role} {clip.length:0.00}s ({frames.Length} frames) loop {clip.isLooping} human {clip.humanMotion}"
                    + $" grip L {frames.Count(f => f.gripLeft > 0.99f)}/{frames.Length} R {frames.Count(f => f.gripRight > 0.99f)}/{frames.Length}");
            }
            set.clips = clips.ToArray();

            // 컨트롤러 상태가 쓰는 원래 클립 이름 (바꿔 끼울 대상, AC_ROK 은 그대로)
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimationClip oldRun = null, oldAdvance = null;
            if (controller != null)
                foreach (var child in controller.layers[0].stateMachine.states)
                {
                    if (child.state.name == "Run_Rifle" && child.state.motion is AnimationClip r) { oldRun = r; set.runSourceClip = r.name; }
                    if (child.state.name == "CrouchRun_Rifle" && child.state.motion is AnimationClip a) { oldAdvance = a; set.advanceSourceClip = a.name; }
                    if (child.state.name == "Idle_Trench" && child.state.motion is AnimationClip k) set.kneelSourceClip = k.name;
                    if (child.state.name == "Fire_Crouch" && child.state.motion is AnimationClip f) set.kneelFireSourceClip = f.name;
                }
            log.AppendLine($"run swap: Run_Rifle {set.runSourceClip} → A_ROK_Run_Rifle, CrouchRun_Rifle {set.advanceSourceClip} → A_ROK_CrouchRun_Rifle");
            log.AppendLine($"kneel swap: Idle_Trench {set.kneelSourceClip} → A_ROK_Kneel_Watch_Idle, Fire_Crouch {set.kneelFireSourceClip} → A_ROK_FireCrouch_Fire");

            // 4 · 5. 열린 씬의 아군에 맞추기
            var fits = Fit(set, oldRun, oldAdvance, log);
            if (fits != null) set.fits = fits.ToArray();
            else log.AppendLine("no allies in the open scene: fits and run speeds kept as they are");

            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            log.AppendLine($"set: clips {set.clips.Length}, fits {set.fits.Length}, run {set.runClipSpeed:0.00} advance {set.advanceClipSpeed:0.00}");
            return Finish(log);
        }

        static string Finish(StringBuilder log)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, log.ToString());
            return log.ToString().Split('\n').Reverse().FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        }

        // ---- 1. 파일 ----------------------------------------------------------------------------------------------
        // true = 게임 폴더에 그 파일이 있다 (복사했거나 이미 같은 것이 있음)
        static bool CopyIn(string name, StringBuilder log)
        {
            string source = Path.Combine(SourceDir, name + ".fbx");
            string target = Folder + name + ".fbx";
            if (!File.Exists(source)) return File.Exists(target);
            if (File.Exists(target) && File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(target))) return true;
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            log.AppendLine("copied " + target);
            return true;
        }

        // ---- 2. 아바타 · 클립 -------------------------------------------------------------------------------------
        static Avatar SetupAvatar(StringBuilder log)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(TPose);
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human) { importer.animationType = ModelImporterAnimationType.Human; changed = true; }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (changed) importer.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(TPose).OfType<Avatar>().FirstOrDefault();
            // 내보낸 뼈 이름은 Unity 사람 뼈 이름에서 띄어쓰기만 뺀 것(LeftUpperArm, LeftThumbProximal …).
            // 자동 매핑이 빠뜨린 것이 있으면 이름으로 채운다. Root · Rifle* 는 매핑하지 않는다.
            var description = importer.humanDescription;
            if ((description.human == null || description.human.Length == 0) && avatar != null) description = avatar.humanDescription;
            var bones = new HashSet<string>((description.skeleton ?? new SkeletonBone[0]).Select(s => s.name));
            var human = (description.human ?? new HumanBone[0]).ToList();
            int added = 0;
            foreach (var humanName in HumanTrait.BoneName)
            {
                string bone = humanName.Replace(" ", "");
                if (!bones.Contains(bone) || human.Any(h => h.humanName == humanName)) continue;
                human.RemoveAll(h => h.boneName == bone);
                var entry = new HumanBone { boneName = bone, humanName = humanName };
                entry.limit.useDefaultValues = true;
                human.Add(entry);
                added++;
            }
            if (added > 0)
            {
                description.human = human.ToArray();
                importer.humanDescription = description;
                importer.SaveAndReimport();
                avatar = AssetDatabase.LoadAllAssetsAtPath(TPose).OfType<Avatar>().FirstOrDefault();
            }
            if (avatar != null)
            {
                var mapped = avatar.humanDescription.human ?? new HumanBone[0];
                log.AppendLine($"avatar {avatar.name}: valid {avatar.isValid} human {avatar.isHuman} mapped {mapped.Length} (filled by name this run {added}), skeleton {bones.Count}");
                log.AppendLine("  " + string.Join(", ", mapped.Select(h => h.humanName + "=" + h.boneName)));
            }
            return avatar;
        }

        static AnimationClip SetupClip(string path, string name, bool loop, Avatar avatar, StringBuilder log)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return null;
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human) { importer.animationType = ModelImporterAnimationType.Human; changed = true; }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther) { importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; changed = true; }
            if (importer.sourceAvatar != avatar) { importer.sourceAvatar = avatar; changed = true; }
            if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            if (importer.animationCompression != ModelImporterAnimationCompression.KeyframeReduction) { importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction; changed = true; }
            if (changed) importer.SaveAndReimport();

            var source = importer.defaultClipAnimations;
            if (source == null || source.Length == 0) return null;
            var take = source[0];
            var current = importer.clipAnimations;
            bool same = current != null && current.Length == 1 && current[0].name == name && current[0].loopTime == loop && !current[0].loopPose
                && current[0].lockRootRotation && current[0].keepOriginalOrientation
                && current[0].lockRootHeightY && current[0].keepOriginalPositionY
                && current[0].lockRootPositionXZ && current[0].keepOriginalPositionXZ
                && Mathf.Approximately(current[0].firstFrame, take.firstFrame) && Mathf.Approximately(current[0].lastFrame, take.lastFrame);
            if (!same)
            {
                take.name = name;
                take.loopTime = loop;
                take.loopPose = false;                                               // 첫 · 끝 프레임이 이미 같다
                take.lockRootRotation = true; take.keepOriginalOrientation = true;   // 루트 회전: 자세에 굽기, 원본 기준
                take.lockRootHeightY = true; take.keepOriginalPositionY = true;      // 높이: 자세에 굽기, 원본 기준
                take.lockRootPositionXZ = true; take.keepOriginalPositionXZ = true;  // 수평 위치: 자세에 굽기, 원본 기준 (제자리)
                importer.clipAnimations = new[] { take };
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        }

        // ---- 3. 소총 궤적 JSON ------------------------------------------------------------------------------------
        [Serializable] sealed class JsonTrack { public string clip; public float fps; public JsonFrame[] frames; }
        [Serializable] sealed class JsonFrame { public int f; public float[] pos; public float[] forward; public float[] up; public float grip_L; public float grip_R; }

        static AllyRifleTracks.Frame[] ReadTrack(string name, StringBuilder log)
        {
            string path = Path.Combine(SourceDir, name + "_rifle.json");
            if (!File.Exists(path)) return null;
            var track = JsonUtility.FromJson<JsonTrack>(File.ReadAllText(path));
            if (track == null || track.frames == null || track.frames.Length == 0) { log.AppendLine("  bad json " + path); return null; }
            if (Mathf.Abs(track.fps - 30f) > 0.01f) log.AppendLine($"  {name}: fps {track.fps} (30 이 아님)");
            var frames = new AllyRifleTracks.Frame[track.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                var f = track.frames[i];
                frames[i] = new AllyRifleTracks.Frame
                {
                    position = V(f.pos),
                    rotation = Quaternion.LookRotation(V(f.forward), V(f.up)),
                    gripLeft = Mathf.Clamp01(f.grip_L),
                    gripRight = Mathf.Clamp01(f.grip_R),
                };
            }
            return frames;
        }

        static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

        // ---- 4. 아군마다 맞추기 -----------------------------------------------------------------------------------
        static List<AllyRifleTracks.Fit> Fit(AllyRifleTracks set, AnimationClip oldRun, AnimationClip oldAdvance, StringBuilder log)
        {
            var allies = Object.FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allies.Length == 0) return null;
            var baseClip = set.Find(AllyClipRole.Base);
            if (baseClip == null) { log.AppendLine("no base clip: nothing to fit"); return null; }
            var fits = new List<AllyRifleTracks.Fit>();
            bool speedsMeasured = false;
            foreach (var ally in allies.OrderBy(a => a.name))
            {
                var animator = ally.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman) { log.AppendLine($"{ally.name}: no humanoid animator"); continue; }
                var stand = animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.animationClips.Where(c => c != null && c.name.StartsWith("A_ROK_Idle_Stand")).OrderByDescending(c => c.name.Length).FirstOrDefault()
                    : null;
                var clone = Object.Instantiate(animator.gameObject);
                clone.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    clone.transform.SetParent(null, false);
                    clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    clone.transform.localScale = Vector3.one;
                    var rig = clone.GetComponent<Animator>();
                    var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
                    var anchorBone = rig.GetBoneTransform(HumanBodyBones.UpperChest) != null ? HumanBodyBones.UpperChest : HumanBodyBones.Chest;
                    var anchor = rig.GetBoneTransform(anchorBone);
                    var rightHand = rig.GetBoneTransform(HumanBodyBones.RightHand);
                    var leftHand = rig.GetBoneTransform(HumanBodyBones.LeftHand);
                    if (hips == null || anchor == null || rightHand == null || leftHand == null) { log.AppendLine($"{ally.name}: bones missing"); continue; }

                    // 참호 바닥: 낮춘 대기의 가장 낮은 점을 서서 대기의 가장 낮은 점(발바닥)에 맞춘다
                    Sample(baseClip.clip, clone, 0f);
                    float readyLow = LowestPoint(clone);
                    float hipsY = hips.position.y;
                    var wrists = (rightHand.position + leftHand.position) * 0.5f;
                    float standLow = readyLow;
                    if (stand != null) { Sample(stand, clone, 0.1f); standLow = LowestPoint(clone); }
                    if (float.IsInfinity(readyLow) || float.IsInfinity(standLow)) { log.AppendLine($"{ally.name}: no skinned mesh to measure"); continue; }

                    // JSON(바닥 기준 m) → 모델 단위: 크기는 엉덩이 높이 비율, 자리는 첫 프레임 두 손목의 가운데에 맞춘다
                    float scale = (hipsY - readyLow) / SourceReadyHips;
                    var first = baseClip.frames[0];
                    var jsonWrists = first.position + first.rotation * ((set.rightWrist + set.leftWrist) * 0.5f);
                    var offset = wrists - scale * jsonWrists;
                    var fit = new AllyRifleTracks.Fit
                    {
                        actor = ally.name, avatar = animator.avatar.name, anchor = anchorBone,
                        floorOffset = standLow - readyLow, scale = scale, offset = offset,
                    };
                    float lossy = animator.transform.lossyScale.y;
                    log.AppendLine($"{ally.name} ({fit.avatar}, stand clip {(stand != null ? stand.name : "none")}): floor {fit.floorOffset * lossy * 100f:0.0} cm, scale {scale:0.000} (x lossy {scale * lossy:0.000}), offset {offset}, anchor {anchorBone}");
                    // 바꿔 끼운 무릎 대기 · 기존 무릎 대기: 가장 낮은 점이 서서 대기 발바닥(참호 바닥)과 얼마나 다른가 (화면 확인용 기록)
                    var kneel = set.Find(AllyClipRole.KneelIdle);
                    if (kneel != null)
                    {
                        Sample(kneel.clip, clone, 0f);
                        float kneelLow = LowestPoint(clone);
                        // 꿇은 오른 무릎을 바닥에 맞춘다(관절 중심이 KneelKneeHeight). 가장 낮은 점으로 맞추면 체격이 다른 박용팔은
                        // 앞발 밑창이 무릎보다 낮아 6 cm만 내려가고 무릎이 9 cm 떴다(2026-11-18 밤). 앞발은 발 IK가 바닥에 맞춘다.
                        var kneelKnee = rig.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                        fit.kneelFloorOffset = kneelKnee != null ? standLow + KneelKneeHeight * scale - kneelKnee.position.y : standLow - kneelLow;
                        var oldKneel = animator.runtimeAnimatorController != null
                            ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name == set.kneelSourceClip) : null;
                        float oldLow = kneelLow;
                        if (oldKneel != null) { Sample(oldKneel, clone, 0f); oldLow = LowestPoint(clone); }
                        log.AppendLine($"  kneel: new lowest {(kneelLow - standLow) * lossy * 100f:0.0} cm, old {set.kneelSourceClip} {(oldLow - standLow) * lossy * 100f:0.0} cm above the standing sole");
                    }

                    var baked = new List<AllyRifleTracks.Baked>();
                    foreach (var c in set.clips)
                    {
                        int count = c.frames.Length;
                        var b = new AllyRifleTracks.Baked { clip = c.name, position = new Vector3[count], rotation = new Quaternion[count] };
                        float worstLeft = 0f, worstRight = 0f;
                        for (int k = 0; k < count; k++)
                        {
                            Sample(c.clip, clone, Mathf.Min(k / c.fps, c.clip.length));
                            var f = c.frames[k];
                            var p = scale * f.position + offset;
                            b.position[k] = anchor.InverseTransformPoint(p);
                            b.rotation[k] = Quaternion.Inverse(anchor.rotation) * f.rotation;
                            // 손이 총에 붙은 프레임: 클립의 손목이 소총 손잡이에서 얼마나 떨어졌나 (m, 게임 크기)
                            if (f.gripRight > 0.99f) worstRight = Mathf.Max(worstRight, Vector3.Distance(rightHand.position, p + f.rotation * (set.rightWrist * scale)) * lossy);
                            if (f.gripLeft > 0.99f) worstLeft = Mathf.Max(worstLeft, Vector3.Distance(leftHand.position, p + f.rotation * (set.leftWrist * scale)) * lossy);
                        }
                        baked.Add(b);
                        log.AppendLine($"  {c.name}: wrist-to-grip worst L {worstLeft * 100f:0.0} cm R {worstRight * 100f:0.0} cm (IK closes this)");
                    }
                    fit.clips = baked.ToArray();
                    fits.Add(fit);

                    // 5. 달리기 발 속도: 일반 병사 한 명에서 기존 클립 대비 비율
                    if (!speedsMeasured && !AlliedRifleFit.IsParkAvatar(animator))
                    {
                        speedsMeasured = true;
                        set.runClipSpeed = Speed(set.Find(AllyClipRole.Run), oldRun, OldRunClipSpeed, FallbackRunClipSpeed, clone, "run", log);
                        set.advanceClipSpeed = Speed(set.Find(AllyClipRole.CrouchRun), oldAdvance, OldAdvanceClipSpeed, FallbackAdvanceClipSpeed, clone, "advance", log);
                    }
                }
                finally { Object.DestroyImmediate(clone); }
            }
            return fits;
        }

        static void Sample(AnimationClip clip, GameObject clone, float time)
        {
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.transform.localScale = Vector3.one;
            clip.SampleAnimation(clone, time);
        }

        // 피부 정점 중 가장 낮은 점 (복제본 공간). AllyGroundedIdleSetup.SoleGap 과 같은 계산.
        static float LowestPoint(GameObject clone)
        {
            float lowest = float.PositiveInfinity;
            foreach (var renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null || !renderer.enabled) continue;
                var vertices = mesh.vertices; var weights = mesh.boneWeights; var binds = mesh.bindposes; var skin = renderer.bones;
                if (weights.Length != vertices.Length || skin.Length == 0 || binds.Length != skin.Length) continue;
                var matrices = new Matrix4x4[skin.Length];
                for (int i = 0; i < skin.Length; i++) matrices[i] = skin[i] != null ? skin[i].localToWorldMatrix * binds[i] : Matrix4x4.zero;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i]; var v = vertices[i];
                    var p = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                          + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    if (p.y < lowest) lowest = p.y;
                }
            }
            return lowest;
        }

        // ---- 5. 달리기 발 속도 ------------------------------------------------------------------------------------
        static float Speed(AllyRifleTracks.Clip fresh, AnimationClip old, float oldSpeed, float fallback, GameObject clone, string label, StringBuilder log)
        {
            if (fresh == null || fresh.clip == null) { log.AppendLine($"{label}: new clip missing, {fallback:0.00} kept"); return fallback; }
            float now = FootSpeed(fresh.clip, clone), before = old != null ? FootSpeed(old, clone) : 0f;
            if (now <= 0f || before <= 0f || now / before < 0.5f || now / before > 2f)
            {
                log.AppendLine($"{label}: planted foot new {now:0.000} old {before:0.000} (model units/s) — not usable, {fallback:0.00} kept");
                return fallback;
            }
            float result = oldSpeed * now / before;
            log.AppendLine($"{label}: planted foot new {now:0.000} old {before:0.000} (model units/s) → clip speed {oldSpeed:0.00} x {now / before:0.000} = {result:0.00}");
            return result;
        }

        // The planted foot of an in-place run slides back at ground speed: the median of its speed while it is down.
        static float FootSpeed(AnimationClip clip, GameObject clone)
        {
            var rig = clone.GetComponent<Animator>();
            var leftToes = rig.GetBoneTransform(HumanBodyBones.LeftToes);
            var rightToes = rig.GetBoneTransform(HumanBodyBones.RightToes);
            var feet = new[]
            {
                leftToes != null ? leftToes : rig.GetBoneTransform(HumanBodyBones.LeftFoot),
                rightToes != null ? rightToes : rig.GetBoneTransform(HumanBodyBones.RightFoot),
            };
            var hips = rig.GetBoneTransform(HumanBodyBones.Hips);
            if (feet[0] == null || feet[1] == null || hips == null || clip.length <= 0f) return 0f;
            const int n = 90;
            var at = new Vector3[2, n + 1];
            float low = float.PositiveInfinity, height = 0f;
            for (int i = 0; i <= n; i++)
            {
                Sample(clip, clone, clip.length * i / n);
                for (int s = 0; s < 2; s++) { at[s, i] = feet[s].position; low = Mathf.Min(low, at[s, i].y); }
                height = Mathf.Max(height, hips.position.y);
            }
            float contact = low + 0.02f * height;
            var speeds = new List<float>();
            float dt = clip.length / n;
            for (int i = 1; i <= n; i++)
                for (int s = 0; s < 2; s++)
                    if (at[s, i].y < contact && at[s, i - 1].y < contact)
                    {
                        var d = at[s, i] - at[s, i - 1]; d.y = 0f;
                        speeds.Add(d.magnitude / dt);
                    }
            if (speeds.Count < 4) return 0f;
            speeds.Sort();
            return speeds[speeds.Count / 2];
        }
    }
}
