using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Character_Rigged_Models3 납품본. 게임 컴포넌트와 프리팹 GUID를 보존하며 Model만 교체한다.
    public static class CharacterImportSetup
    {
        public const string ImportTag = "RiggedCharacterModels3";
        internal const string RegularModel = "ROK_Soldier/SK_CH_ROK_Soldier_Person1";
        const string ModelDir = "Assets/_Project/Art/Models/Characters/";
        const string PrefabDir = "Assets/_Project/Prefabs/";
        static readonly string[] Models = {
            "KPA_Rifleman/SK_CH_KPA_Rifleman", "KPA_Rifleman_B/SK_CH_KPA_Rifleman_B",
            "ROK_ParkYongpal/SK_CH_ROK_ParkYongpal", "ROK_Protagonist/SK_CH_ROK_Protagonist",
            "ROK_Soldier/SK_CH_ROK_Soldier"
        };

        [MenuItem("FiveRivers/Setup/Apply Rigged Character Models3")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying characters.");
            foreach (string model in Models) ConfigureModel(ModelDir + model + ".fbx");
            ReplacePrefab("PF_CH_KPA_Rifleman", Models[0], 1.68f, true);
            ReplacePrefab("PF_CH_ROK_ParkYongpal", Models[2], 1.72f, false);
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "PF_CH_ROK_Soldier.prefab");
            var soldierModel = soldier != null ? soldier.transform.Find("Model") : null;
            if (soldierModel == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(soldierModel.gameObject) != ModelDir + RegularModel + ".fbx")
                ReplacePrefab("PF_CH_ROK_Soldier", RegularModel, 1.70f, false);
            CreateVariant("PF_CH_KPA_Rifleman", "PF_CH_KPA_Rifleman_B", Models[1], 1.68f, true);
            CreateVariant("PF_CH_ROK_Soldier", "PF_CH_ROK_Protagonist", Models[3], 1.70f, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Characters] Five rigged models imported; KPA, Park and allied soldier prefabs replaced.");
        }

        [MenuItem("FiveRivers/Setup/Apply Repaired Character Variants")]
        public static void ApplyRepairedVariants()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying repaired character variants.");
            // 수리된 B형·주인공만 갱신한다. 현재 게임에 쓰는 세 캐릭터는 재임포트하지 않는다.
            ConfigureModel(ModelDir + Models[1] + ".fbx");
            ConfigureModel(ModelDir + Models[3] + ".fbx");
            CreateVariant("PF_CH_KPA_Rifleman", "PF_CH_KPA_Rifleman_B", Models[1], 1.68f, true);
            CreateVariant("PF_CH_ROK_Soldier", "PF_CH_ROK_Protagonist", Models[3], 1.70f, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[Characters] Repaired B and protagonist variants updated; active character prefabs retained.");
        }

        static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing model: " + path);
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.userData = ImportTag;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            var currentModel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var currentAvatar = currentModel != null ? currentModel.GetComponentInChildren<Animator>(true)?.avatar : null;
            if (currentAvatar == null || !currentAvatar.isValid || !currentAvatar.isHuman)
                ConfigureMixamoMapping(importer, currentModel);
            var materialDir = Path.GetDirectoryName(path).Replace('\\', '/') + "/Materials";
            var textureDir = Path.GetDirectoryName(path).Replace('\\', '/') + "/Textures";
            Directory.CreateDirectory(materialDir);
            Directory.CreateDirectory(textureDir);
            // 이전 그레이박스 색상 remap은 새 납품본의 텍스처를 가리지 않게 제거한다.
            foreach (var entry in importer.GetExternalObjectMap())
                if (entry.Key.type == typeof(Material)) importer.RemoveRemap(entry.Key);
            importer.SaveAndReimport();
            importer.ExtractTextures(textureDir);
            AssetDatabase.Refresh();
            importer.SaveAndReimport();
            var materials = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray();
            foreach (var source in materials)
            {
                string safeName = string.Concat(source.name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string materialPath = materialDir + "/" + safeName + ".mat";
                var target = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (target == null)
                {
                    target = new Material(source);
                    AssetDatabase.CreateAsset(target, materialPath);
                }
                Texture baseMap = TextureOf(source, "_BaseMap", "_MainTex");
                Texture normalMap = TextureOf(source, "_BumpMap", "_NormalMap");
                Texture metalMap = TextureOf(source, "_MetallicGlossMap");
                Color tint = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                target.shader = Shader.Find("Universal Render Pipeline/Lit");
                target.SetColor("_BaseColor", baseMap != null ? Color.white : tint);
                target.SetTexture("_BaseMap", baseMap);
                target.SetFloat("_Smoothness", 0.2f);
                if (normalMap != null)
                {
                    var normalImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normalMap)) as TextureImporter;
                    if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
                    {
                        normalImporter.textureType = TextureImporterType.NormalMap;
                        normalImporter.SaveAndReimport();
                    }
                    target.SetTexture("_BumpMap", normalMap);
                    target.EnableKeyword("_NORMALMAP");
                }
                if (metalMap != null)
                {
                    target.SetTexture("_MetallicGlossMap", metalMap);
                    target.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                EditorUtility.SetDirty(target);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), target);
            }
            importer.SaveAndReimport();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var animator = fbx.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Humanoid Avatar validation failed: " + path);
        }

        static Texture TextureOf(Material material, params string[] names)
        {
            foreach (string name in names)
                if (material.HasProperty(name) && material.GetTexture(name) != null) return material.GetTexture(name);
            return null;
        }

        static void ConfigureMixamoMapping(ModelImporter importer, GameObject model)
        {
            if (model == null) throw new InvalidOperationException("Cannot inspect the character skeleton: " + importer.assetPath);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var mapping = new Dictionary<HumanBodyBones, string> {
                { HumanBodyBones.Hips, "Hips" }, { HumanBodyBones.Spine, "Spine" },
                { HumanBodyBones.Chest, "Spine1" }, { HumanBodyBones.UpperChest, "Spine2" },
                { HumanBodyBones.Neck, "Neck" }, { HumanBodyBones.Head, "Head" },
                { HumanBodyBones.LeftShoulder, "LeftShoulder" }, { HumanBodyBones.RightShoulder, "RightShoulder" },
                { HumanBodyBones.LeftUpperArm, "LeftArm" }, { HumanBodyBones.RightUpperArm, "RightArm" },
                { HumanBodyBones.LeftLowerArm, "LeftForeArm" }, { HumanBodyBones.RightLowerArm, "RightForeArm" },
                { HumanBodyBones.LeftHand, "LeftHand" }, { HumanBodyBones.RightHand, "RightHand" },
                { HumanBodyBones.LeftUpperLeg, "LeftUpLeg" }, { HumanBodyBones.RightUpperLeg, "RightUpLeg" },
                { HumanBodyBones.LeftLowerLeg, "LeftLeg" }, { HumanBodyBones.RightLowerLeg, "RightLeg" },
                { HumanBodyBones.LeftFoot, "LeftFoot" }, { HumanBodyBones.RightFoot, "RightFoot" },
                { HumanBodyBones.LeftToes, "LeftToeBase" }, { HumanBodyBones.RightToes, "RightToeBase" }
            };
            foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                string sourceFinger = finger == "Little" ? "Pinky" : finger;
                var segments = new[] { "Proximal", "Intermediate", "Distal" };
                for (int i = 0; i < segments.Length; i++)
                    mapping[(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + finger + segments[i])] = side + "Hand" + sourceFinger + (i + 1);
            }
            var human = new List<HumanBone>();
            foreach (var entry in mapping)
            {
                var bone = transforms.FirstOrDefault(t => t.name == entry.Value || t.name.EndsWith(":" + entry.Value) || t.name.EndsWith("_" + entry.Value));
                if (bone == null)
                {
                    if (HumanTrait.RequiredBone((int)entry.Key)) throw new InvalidOperationException("Required character bone missing: " + entry.Value);
                    continue;
                }
                human.Add(new HumanBone { boneName = bone.name, humanName = HumanTrait.BoneName[(int)entry.Key], limit = new HumanLimit { useDefaultValues = true } });
            }
            var description = importer.humanDescription;
            description.human = human.ToArray();
            importer.humanDescription = description;
        }

        static void ReplacePrefab(string name, string model, float height, bool enemy)
        {
            string path = PrefabDir + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = name;
                ReplaceModel(root, model, height, enemy);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void CreateVariant(string original, string name, string model, float height, bool enemy)
        {
            string path = PrefabDir + name + ".prefab";
            if (!File.Exists(path)) AssetDatabase.CopyAsset(PrefabDir + original + ".prefab", path);
            ReplacePrefab(name, model, height, enemy);
        }

        static bool SameAssetIdentity(Object first, Object second)
        {
            return first != null && second != null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string firstGuid, out long firstId)
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(second, out string secondGuid, out long secondId)
                && !string.IsNullOrEmpty(firstGuid) && firstGuid == secondGuid && firstId == secondId;
        }
        static void ReplaceModel(GameObject root, string model, float height, bool enemy)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model + ".fbx");
            if (prefab == null) throw new InvalidOperationException("Character model missing: " + model);
            var old = root.transform.Find("Model");
            string fitJson = null;
            bool fitEnabled = true;
            if (old != null)
            {
                var oldSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(old.gameObject);
                var oldAnimator = old.GetComponentInChildren<Animator>(true);
                var newAnimator = prefab.GetComponentInChildren<Animator>(true);
                var fit = oldAnimator != null ? oldAnimator.GetComponent<AlliedRifleFit>() : null;
                // An opt-in correction belongs to this exact model and avatar, never another character.
                if (fit != null && SameAssetIdentity(oldSource, prefab) && newAnimator != null
                    && SameAssetIdentity(oldAnimator.avatar, newAnimator.avatar))
                {
                    fitJson = JsonUtility.ToJson(fit);
                    fitEnabled = fit.enabled;
                }
            }
            var rifle = old != null ? old.Find("Socket_Rifle") : null;
            if (rifle != null) rifle.SetParent(root.transform, false);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            // 이전 박용팔 식별용 임시 수건은 납품 모델의 목 장비와 겹치지 않게 제거한다.
            var towel = root.transform.Find("Towel");
            if (towel != null) Object.DestroyImmediate(towel.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            instance.name = "Model";
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Character has no renderers: " + model);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = height / bounds.size.y;
            instance.transform.localScale *= scale;
            instance.transform.localPosition -= new Vector3(bounds.center.x - root.transform.position.x, bounds.min.y - root.transform.position.y, bounds.center.z - root.transform.position.z) * scale;
            var animator = instance.GetComponentInChildren<Animator>(true);
            animator.applyRootMotion = false;
            if (fitJson != null)
            {
                var fit = animator.GetComponent<AlliedRifleFit>();
                if (fit == null) fit = animator.gameObject.AddComponent<AlliedRifleFit>();
                JsonUtility.FromJsonOverwrite(fitJson, fit);
                fit.enabled = fitEnabled;
            }
            if (animator.GetComponent<CharacterAnimationEvents>() == null)
                animator.gameObject.AddComponent<CharacterAnimationEvents>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Animations/AC_" + (enemy ? "KPA" : "ROK") + ".controller");
            if (enemy)
            {
                var enemyAgent = root.GetComponent<EnemyAgent>();
                SetupUtil.SetRef(enemyAgent, "model", instance.transform);
                SetupUtil.SetRef(enemyAgent, "animator", animator);
                if (rifle != null)
                {
                    // 기존 소총 위치를 유지한다. 새 팔 리그에 대한 양손 IK는 별도 애니메이션 작업이다.
                    rifle.SetParent(root.transform, false);
                    rifle.localPosition = new Vector3(0.2f, 1.25f, 0.05f);
                }
            }
        }
    }
}


