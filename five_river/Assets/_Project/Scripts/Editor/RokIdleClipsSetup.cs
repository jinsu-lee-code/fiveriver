using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Data;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // VARCO 대기 · 경계 동작 21개의 임포트 설정과 재생 목록 (2026-11-18).
    // 원본: source/anim/varco_rok_idle_20261118/raw (모델 포함 30MB) → strip_to_anim_fbx.py 가 뼈대 + 동작만 남겨
    // Art/Animations/ROK/A_ROK_*.fbx 로 내보낸다. 이 메뉴는 그 파일들을 Humanoid 로 맞추고(아바타는 A_ROK_Varco_TPose 한 개를
    // 복사해 쓴다), 클립 이름 = 파일명, 반복 클립은 Loop Time, 루트 회전 · 위치는 원본 기준으로 자세에 굽는다(제자리).
    // 그리고 화면에서 통과한 클립만, 열려 있는 씬의 아군 아바타마다 "첫 프레임 대비 몸통 · 목 · 머리 · 골반 변화량"으로
    // 구워 Resources/AllyIdleClips.asset 에 넣는다(Data/AllyIdleClipSet 설명 참고). 여러 번 실행해도 결과가 같다.
    // 아군이 있는 씬(Dabudong)이 열려 있지 않으면 굽기는 건너뛰고 기존 값을 둔다.
    // 기록: Temp/rok_idle_clips.txt
    public static class RokIdleClipsSetup
    {
        const string Folder = "Assets/_Project/Art/Animations/ROK/";
        const string TPose = Folder + "A_ROK_Varco_TPose.fbx";
        const string SetPath = "Assets/_Project/Art/Resources/" + AllyIdleClipSet.ResourceName + ".asset";
        const string Report = "Temp/rok_idle_clips.txt";

        enum Use { No, Stand, Kneel }

        // 파일명, 반복 여부, 재생 목록(화면 확인 결과), 뺀 이유
        static readonly (string name, bool loop, Use use, float gain, string note)[] Clips =
        {
            // 잔잔한 세 클립(숨쉬기 · 체중 이동)은 원래 크기로는 5 m 밖에서 보이지 않아 2배로 키운다
            ("A_ROK_Stand_Idle_01", true, Use.Stand, 2f, ""),
            ("A_ROK_Stand_Idle_02", true, Use.Stand, 2f, ""),
            ("A_ROK_Stand_Guard", true, Use.Stand, 2f, ""),
            ("A_ROK_Stand_Look", true, Use.Stand, 1f, ""),
            ("A_ROK_Stand_HandAdjust", true, Use.No, 1f, "총을 고쳐 잡는 팔 동작은 쓸 수 없고(팔은 소총에 맞춰 푼다) 상체만 33도 돌았다 온다: 이유 없이 몸을 트는 것으로 보이고 왼손이 총에서 11 cm 벗어났다"),
            // 무릎 계열: 싸우지 않는 구간의 아군은 서 있다(무릎 대기는 교전 중의 사격 자세뿐). 고개 · 상체 움직임이 큰 세 개만 서 있는 자세에 얹어 본다
            ("A_ROK_Trench_Idle_01", true, Use.Stand, 1f, ""),
            ("A_ROK_Trench_Scan_01", true, Use.Stand, 1f, ""),
            ("A_ROK_Trench_LookOver_02", true, Use.Stand, 1f, ""),
            ("A_ROK_Trench_Idle_02", true, Use.No, 1f, "무릎 대기에 얹을 클립인데 싸우지 않는 구간의 아군은 서 있다. 움직임도 서서 대기 클립과 같은 수준(머리 4도)이라 겹친다"),
            ("A_ROK_Trench_Idle_03", true, Use.No, 1f, "위와 같음 (머리 6도)"),
            ("A_ROK_Trench_Idle_Alert", true, Use.No, 1f, "위와 같음 (머리 4도)"),
            ("A_ROK_Trench_Scan_02", true, Use.No, 1f, "위와 같음 (머리 1.5도). 원본은 엉덩이가 높은 반쯤 선 자세"),
            ("A_ROK_Trench_LookOver_01", true, Use.No, 1f, "위와 같음 (머리 5도)"),
            ("A_ROK_Trench_LookOver_03", true, Use.No, 1f, "위와 같음 (머리 7도)"),
            ("A_ROK_Trench_Scan_TurnRight", false, Use.No, 1f, "제자리에서 한 바퀴(360도) 돈다: 참호 안에서 등을 들판에 돌리고 발이 미끄러진다"),
            ("A_ROK_Trench_Scan_TurnLeft", true, Use.No, 1f, "몸을 100도 넘게 돌리며 엉덩이가 선 키의 20% 오르내린다: 발이 미끄러지고 머리가 흉벽 위로 나온다"),
            ("A_ROK_Crouch_Idle", true, Use.No, 1f, "쪼그려 앉은 자세: 기존 무릎 대기와 다리 모양이 달라 넘어갈 때 다리가 엇갈린다"),
            ("A_ROK_StandToKneel_01", false, Use.No, 1f, "끝 자세가 맨손으로 주저앉은 자세(엉덩이 0.12, 무릎이 바닥 아래): 소총을 든 대기로 이어지지 않는다"),
            ("A_ROK_StandToKneel_02", false, Use.No, 1f, "끝 자세가 맨손으로 주저앉은 자세(엉덩이 0.13): 소총을 든 대기로 이어지지 않는다"),
            ("A_ROK_Flinch_Stagger", false, Use.No, 1f, "13.8초 동안 크게 비틀거린다(몸 107도 회전): 대기 동작이 아니다"),
            ("A_ROK_Flinch", false, Use.No, 1f, "움찔: 그 신호가 오는 때는 교전 중이라 견착 자세와 겹친다"),
        };

        [MenuItem("FiveRivers/Setup/ROK Idle Clips")]
        public static string Apply()
        {
            var log = new StringBuilder();
            if (!File.Exists(TPose)) return Finish(log.AppendLine("MISSING " + TPose + " (strip_to_anim_fbx.py 를 먼저 실행)"));

            var avatar = SetupAvatar(log);
            if (avatar == null || !avatar.isValid || !avatar.isHuman) return Finish(log.AppendLine("FAILED avatar"));

            var chosen = new List<(AnimationClip clip, bool kneeling, float gain)>();
            foreach (var (name, loop, use, gain, note) in Clips)
            {
                string path = Folder + name + ".fbx";
                if (!File.Exists(path)) { log.AppendLine("missing " + path); continue; }
                var clip = SetupClip(path, name, loop, avatar, log);
                if (clip == null) { log.AppendLine("FAILED clip " + name); continue; }
                log.AppendLine($"{name}: {clip.length:0.00}s loop {clip.isLooping} human {clip.humanMotion} use {use}{(note.Length > 0 ? " — " + note : "")}");
                if (use != Use.No) chosen.Add((clip, use == Use.Kneel, gain));
            }

            var set = AssetDatabase.LoadAssetAtPath<AllyIdleClipSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<AllyIdleClipSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            var tracks = Bake(chosen, log);
            if (tracks != null)
            {
                set.tracks = tracks.ToArray();
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssetIfDirty(set);
            }
            log.AppendLine($"set: tracks {set.tracks.Length} (standing {set.tracks.Count(t => !t.kneeling)}, kneeling {set.tracks.Count(t => t.kneeling)})");
            return Finish(log);
        }

        static string Finish(StringBuilder log)
        {
            File.WriteAllText(Report, log.ToString());
            return log.ToString().Split('\n').Reverse().FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        }

        // ---- 굽기: 아군 아바타마다 클립을 표본으로 떠서 첫 프레임 대비 변화량만 남긴다 ----------------------------
        const float Rate = 15f;
        static readonly HumanBodyBones[] Baked =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
            HumanBodyBones.Head,
        };

        static List<AllyIdleClipSet.Track> Bake(List<(AnimationClip clip, bool kneeling, float gain)> clips, StringBuilder log)
        {
            var rigs = new SortedDictionary<string, Animator>();
            foreach (var ally in Object.FindObjectsByType<AllyCombatant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var animator = ally.GetComponentInChildren<Animator>(true);
                if (animator != null && animator.avatar != null && animator.avatar.isHuman && !rigs.ContainsKey(animator.avatar.name))
                    rigs[animator.avatar.name] = animator;
            }
            if (rigs.Count == 0) { log.AppendLine("no allies in the open scene: baked tracks kept as they are"); return null; }
            var tracks = new List<AllyIdleClipSet.Track>();
            foreach (var rig in rigs)
            {
                var clone = Object.Instantiate(rig.Value.gameObject);
                clone.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    clone.transform.SetParent(null, false);
                    var animator = clone.GetComponent<Animator>();
                    var bones = Baked.Select(b => animator.GetBoneTransform(b)).ToArray();
                    var leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    var rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    if (bones[0] == null || bones[5] == null || leftThigh == null || rightThigh == null) { log.AppendLine($"{rig.Key}: bones missing"); continue; }
                    foreach (var (clip, kneeling, gain) in clips)
                    {
                        int count = Mathf.CeilToInt(clip.length * Rate) + 1;
                        var lists = Baked.Select(b => new Quaternion[count]).ToArray();
                        var headTotal = new Quaternion[count];
                        var move = new Vector3[count];
                        var reference = new Quaternion[Baked.Length];
                        Quaternion headReference = Quaternion.identity;
                        Vector3 hipsReference = Vector3.zero, right = Vector3.right, forward = Vector3.forward;
                        float[] largest = new float[Baked.Length];
                        float largestHead = 0f, largestMove = 0f;
                        for (int i = 0; i < count; i++)
                        {
                            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                            clone.transform.localScale = Vector3.one;
                            clip.SampleAnimation(clone, Mathf.Min(i / Rate, clip.length));
                            if (i == 0)
                            {
                                for (int b = 0; b < Baked.Length; b++) reference[b] = bones[b] != null ? bones[b].localRotation : Quaternion.identity;
                                headReference = Quaternion.Inverse(clone.transform.rotation) * bones[5].rotation;
                                hipsReference = clone.transform.InverseTransformPoint(bones[0].position);
                                right = clone.transform.InverseTransformDirection(rightThigh.position - leftThigh.position); right.y = 0f; right.Normalize();
                                forward = Vector3.Cross(right, Vector3.up);
                            }
                            for (int b = 0; b < Baked.Length; b++)
                            {
                                lists[b][i] = bones[b] != null ? Quaternion.Inverse(reference[b]) * bones[b].localRotation : Quaternion.identity;
                                largest[b] = Mathf.Max(largest[b], Quaternion.Angle(Quaternion.identity, lists[b][i]));
                            }
                            headTotal[i] = Quaternion.Inverse(headReference) * (Quaternion.Inverse(clone.transform.rotation) * bones[5].rotation);
                            largestHead = Mathf.Max(largestHead, Quaternion.Angle(Quaternion.identity, headTotal[i]));
                            var d = clone.transform.InverseTransformPoint(bones[0].position) - hipsReference;
                            move[i] = new Vector3(Vector3.Dot(d, right), d.y, Vector3.Dot(d, forward));
                            largestMove = Mathf.Max(largestMove, move[i].magnitude);
                        }
                        Quaternion[] Or(int b) => bones[b] != null ? lists[b] : new Quaternion[0];
                        tracks.Add(new AllyIdleClipSet.Track
                        {
                            clip = clip.name, avatar = rig.Key, kneeling = kneeling, length = clip.length, rate = Rate, gain = gain,
                            hips = Or(0), spine = Or(1), chest = Or(2), upperChest = Or(3), neck = Or(4), head = Or(5),
                            headTotal = headTotal, hipsMove = move,
                        });
                        log.AppendLine($"  baked {clip.name} on {rig.Key}: {count} samples, largest hips {largest[0]:0.0} spine {largest[1]:0.0} chest {largest[2]:0.0} upper {largest[3]:0.0} neck {largest[4]:0.0} head {largest[5]:0.0} deg, head in all {largestHead:0.0} deg, hips moved {largestMove * 100f:0.0} cm, end {Quaternion.Angle(Quaternion.identity, headTotal[count - 1]):0.0} deg");
                    }
                }
                finally { Object.DestroyImmediate(clone); }
            }
            return tracks;
        }

        // ---- 아바타: 동작 없는 기준 자세 파일 하나에서 만든다 ---------------------------------------------------
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
            if (avatar == null) return null;

            // 손가락 뼈 이름(HandLoRA_Bip01_L_Finger0~42)은 자동 매핑이 알아보지 못한다: 직접 지정한다.
            // Finger0 = 엄지, 1 = 검지, 2 = 가운데, 3 = 약지, 4 = 새끼 / 뒤 숫자 없음 · 1 · 2 = 첫 · 가운데 · 끝 마디.
            var description = importer.humanDescription;
            if (description.human == null || description.human.Length == 0) description = avatar.humanDescription;
            var human = description.human != null ? description.human.ToList() : new List<HumanBone>();
            var bones = new HashSet<string>(description.skeleton.Select(s => s.name));
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
            string[] joints = { "Proximal", "Intermediate", "Distal" };
            int added = 0;
            foreach (var side in new[] { ("L", "Left"), ("R", "Right") })
                for (int f = 0; f < 5; f++)
                    for (int j = 0; j < 3; j++)
                    {
                        string bone = $"HandLoRA_Bip01_{side.Item1}_Finger{f}{(j == 0 ? "" : j.ToString())}";
                        string humanName = $"{side.Item2} {fingers[f]} {joints[j]}";
                        if (!bones.Contains(bone) || human.Any(h => h.humanName == humanName)) continue;
                        human.RemoveAll(h => h.boneName == bone);
                        var entry = new HumanBone { boneName = bone, humanName = humanName };
                        entry.limit.useDefaultValues = true;
                        human.Add(entry);
                        added++;
                    }
            if (added > 0 || importer.humanDescription.human == null || importer.humanDescription.human.Length == 0)
            {
                description.human = human.ToArray();
                importer.humanDescription = description;
                importer.SaveAndReimport();
                avatar = AssetDatabase.LoadAllAssetsAtPath(TPose).OfType<Avatar>().FirstOrDefault();
            }
            if (avatar != null)
            {
                var mapped = avatar.humanDescription.human;
                log.AppendLine($"avatar {avatar.name}: valid {avatar.isValid} human {avatar.isHuman} mapped {mapped.Length} (fingers added this run {added})");
                log.AppendLine("  " + string.Join(", ", mapped.Select(h => h.humanName + "=" + h.boneName)));
            }
            return avatar;
        }

        // ---- 클립 --------------------------------------------------------------------------------------------------
        static AnimationClip SetupClip(string path, string name, bool loop, Avatar avatar, StringBuilder log)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human) { importer.animationType = ModelImporterAnimationType.Human; changed = true; }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther) { importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; changed = true; }
            if (importer.sourceAvatar != avatar) { importer.sourceAvatar = avatar; changed = true; }
            if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }
            if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
            // 기존 A_ROK_* 클립과 같은 압축
            if (importer.animationCompression != ModelImporterAnimationCompression.KeyframeReduction) { importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction; changed = true; }
            if (changed) importer.SaveAndReimport();

            var source = importer.defaultClipAnimations;
            if (source == null || source.Length == 0) return null;
            var take = source[0];
            var current = importer.clipAnimations;
            bool same = current != null && current.Length == 1 && current[0].name == name && current[0].loopTime == loop
                && current[0].lockRootRotation && current[0].keepOriginalOrientation
                && current[0].lockRootHeightY && current[0].keepOriginalPositionY
                && current[0].lockRootPositionXZ && current[0].keepOriginalPositionXZ
                && Mathf.Approximately(current[0].firstFrame, take.firstFrame) && Mathf.Approximately(current[0].lastFrame, take.lastFrame);
            if (!same)
            {
                take.name = name;
                take.loopTime = loop;
                take.loopPose = false;
                take.lockRootRotation = true; take.keepOriginalOrientation = true;   // 루트 회전: 자세에 굽기, 원본 기준
                take.lockRootHeightY = true; take.keepOriginalPositionY = true;      // 높이: 자세에 굽기, 원본 기준
                take.lockRootPositionXZ = true; take.keepOriginalPositionXZ = true;  // 수평 위치: 자세에 굽기, 원본 기준 (제자리)
                importer.clipAnimations = new[] { take };
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
        }
    }
}
