using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 플레이어(카메라 · 1인칭 무기 그레이박스)와 효과 프리팹. 씬의 [Player]는 매번 다시 만든다.
    static class PlayerBuilder
    {
        public static PlayerController Build(LevelPoints points, InputGate gate, FlowDirector director, Effects effects)
        {
            var player = GameObject.Find("/[Player]");
            if (player != null) Object.DestroyImmediate(player);
            var old = GameObject.Find("/Main Camera");
            if (old != null) Object.DestroyImmediate(old);

            var playerConfig = SetupUtil.LoadAsset<PlayerConfig>(DabudongSetup.DataDir + "PlayerConfig.asset");
            var weaponConfig = SetupUtil.LoadAsset<WeaponConfig>(DabudongSetup.DataDir + "WeaponConfig.asset");

            player = new GameObject("[Player]");
            player.transform.SetPositionAndRotation(points.playerSpawn.position, points.playerSpawn.rotation);
            var cc = player.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.75f;
            cc.center = new Vector3(0, 0.875f, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(player.transform, false);
            pivot.localPosition = new Vector3(0, playerConfig.standEyeHeight, 0);
            var camGo = new GameObject("PlayerCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 600f;
            cam.fieldOfView = weaponConfig.hipFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            var controller = player.AddComponent<PlayerController>();
            SetupUtil.SetRef(controller, "config", playerConfig);
            SetupUtil.SetRef(controller, "cameraPivot", pivot);
            SetupUtil.SetRef(controller, "gate", gate);

            var health = player.AddComponent<PlayerHealth>();
            SetupUtil.SetRef(health, "config", playerConfig);
            SetupUtil.SetRef(health, "director", director);

            // ---- M1 ----
            var m1Model = new GameObject("VM_M1").transform;
            m1Model.SetParent(camGo.transform, false);
            m1Model.localPosition = new Vector3(0.2f, -0.22f, 0.45f);
            bool m1Fbx = ViewModel(m1Model, "Weapons/M1Garand/SM_WP_M1Garand.fbx");
            if (!m1Fbx)
            {
                GrayboxAssets.Part(m1Model, PrimitiveType.Cube, "Stock", new Vector3(0, 0, -0.15f), new Vector3(0.05f, 0.07f, 0.7f), GrayboxAssets.RifleWood);
                GrayboxAssets.Part(m1Model, PrimitiveType.Cylinder, "Barrel", new Vector3(0, 0.02f, 0.4f), new Vector3(0.022f, 0.25f, 0.022f), GrayboxAssets.Metal, new Vector3(90, 0, 0));
            }
            // 가늠자 · 가늠쇠 (그레이박스 조준 확인용, 최종 모델에는 있으므로 붙이지 않는다)
            if (!m1Fbx)
            {
                GrayboxAssets.Part(m1Model, PrimitiveType.Cube, "RearSight", new Vector3(0, 0.055f, 0.05f), new Vector3(0.03f, 0.03f, 0.02f), GrayboxAssets.Metal);
                GrayboxAssets.Part(m1Model, PrimitiveType.Cube, "FrontSight", new Vector3(0, 0.05f, 0.62f), new Vector3(0.006f, 0.03f, 0.01f), GrayboxAssets.Metal);
            }
            var m1Muzzle = new GameObject("Socket_Muzzle").transform;
            m1Muzzle.SetParent(m1Model, false);
            m1Muzzle.localPosition = new Vector3(0, 0.03f, m1Fbx ? 0.8f : 0.68f);
            var m1Eject = new GameObject("Socket_ClipEject").transform;
            m1Eject.SetParent(m1Model, false);
            m1Eject.localPosition = new Vector3(0, 0.07f, 0.08f); // ClipSlot 위

            var m1 = player.AddComponent<M1Rifle>();
            SetupUtil.SetRef(m1, "config", weaponConfig);
            SetupUtil.SetRef(m1, "player", controller);
            SetupUtil.SetRef(m1, "view", cam);
            SetupUtil.SetRef(m1, "effects", effects);
            SetupUtil.SetRef(m1, "muzzle", m1Muzzle);
            SetupUtil.SetRef(m1, "clipEject", m1Eject);
            SetupUtil.SetRef(m1, "model", m1Model);
            SetVector(m1, "hipPosition", new Vector3(0.2f, -0.22f, 0.45f));
            SetVector(m1, "aimPosition", new Vector3(0f, -0.068f, 0.22f)); // 가늠자 · 가늠쇠가 화면 중앙에 오도록

            // ---- 바주카 ----
            var bzModel = new GameObject("VM_Bazooka").transform;
            bzModel.SetParent(camGo.transform, false);
            bzModel.localPosition = new Vector3(0.2f, -0.13f, 0.1f);
            bool bzFbx = ViewModel(bzModel, "Weapons/Bazooka/SM_WP_Bazooka.fbx");
            if (!bzFbx)
            {
                GrayboxAssets.Part(bzModel, PrimitiveType.Cylinder, "Tube", new Vector3(0, 0, 0.1f), new Vector3(0.09f, 0.76f, 0.09f), GrayboxAssets.Olive, new Vector3(90, 0, 0));
                GrayboxAssets.Part(bzModel, PrimitiveType.Cube, "Grip", new Vector3(0, -0.09f, 0.15f), new Vector3(0.03f, 0.12f, 0.05f), GrayboxAssets.Metal);
                GrayboxAssets.Part(bzModel, PrimitiveType.Cube, "Sight", new Vector3(-0.06f, 0.05f, 0.3f), new Vector3(0.02f, 0.05f, 0.02f), GrayboxAssets.Metal);
            }
            var bzMuzzle = new GameObject("Socket_RocketSpawn").transform;
            bzMuzzle.SetParent(bzModel, false);
            bzMuzzle.localPosition = new Vector3(0, 0, bzFbx ? 1.0f : 0.9f);
            var bzBack = new GameObject("Socket_Backblast").transform;
            bzBack.SetParent(bzModel, false);
            bzBack.localPosition = new Vector3(0, 0, bzFbx ? -0.6f : -0.7f);
            bzBack.localRotation = Quaternion.Euler(0, 180, 0);
            bzModel.gameObject.SetActive(false);

            var bazooka = player.AddComponent<Bazooka>();
            SetupUtil.SetRef(bazooka, "config", weaponConfig);
            SetupUtil.SetRef(bazooka, "player", controller);
            SetupUtil.SetRef(bazooka, "view", cam);
            SetupUtil.SetRef(bazooka, "effects", effects);
            SetupUtil.SetRef(bazooka, "muzzle", bzMuzzle);
            SetupUtil.SetRef(bazooka, "backblast", bzBack);
            SetupUtil.SetRef(bazooka, "model", bzModel);
            SetupUtil.SetRef(bazooka, "rocketPrefab", RocketPrefab().GetComponent<Rocket>());
            SetVector(bazooka, "hipPosition", new Vector3(0.2f, -0.13f, 0.1f));
            SetVector(bazooka, "aimPosition", new Vector3(0.11f, -0.07f, 0.05f));

            var holder = player.AddComponent<WeaponHolder>();
            SetupUtil.SetRef(holder, "config", weaponConfig);
            SetupUtil.SetRef(holder, "player", controller);
            SetupUtil.SetRef(holder, "view", cam);
            SetupUtil.SetRef(holder, "m1", m1);
            SetupUtil.SetRef(holder, "bazooka", bazooka);

            var resupplier = player.AddComponent<Resupplier>();
            SetupUtil.SetRef(resupplier, "config", weaponConfig);
            SetupUtil.SetRef(resupplier, "player", controller);
            SetupUtil.SetRef(resupplier, "weapons", holder);

            // 1인칭 Animator 연결 (팔 · 무기 리그가 오면 m1Animator · bazookaAnimator를 채운다)
            var fpAnim = player.AddComponent<FirstPersonAnimatorBridge>();
            SetupUtil.SetRef(fpAnim, "player", controller);
            SetupUtil.SetRef(fpAnim, "weapons", holder);
            SetupUtil.SetRef(fpAnim, "resupplier", resupplier);
            SetupUtil.SetRef(fpAnim, "m1Animator", FirstPersonRig(m1Model, "AC_FP_M1", "Weapons/M1Garand/SM_WP_M1Garand_Clip.fbx", "ReloadClip"));
            SetupUtil.SetRef(fpAnim, "bazookaAnimator", FirstPersonRig(bzModel, "AC_FP_Bazooka", "Weapons/Bazooka/SM_WP_BazookaRocket.fbx", "ReloadRocket"));

            return controller;
        }

        // 1인칭 팔 + 무기 애니메이션 (ANIM_SPEC 3-3 · 3-4). 클립은 VM 밑 "Arms" · "Model" · 장전 소품 경로를 움직이고
        // 루트 위치도 (0,0,0)으로 덮어쓰므로, Animator는 VM(조준 · 흔들림은 코드)이 아니라 그 밑 FP_Anim에 둔다.
        public static Animator FirstPersonRig(Transform vm, string controller, string propFbx, string propName)
        {
            const string armsFbx = "Characters/ROK_Arms/SK_CH_ROK_Arms.fbx";
            var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Animations/" + controller + ".controller");
            var model = vm.Find("Model") ?? vm.Find("FP_Anim/Model");
            if (ac == null || model == null || !GrayboxModels.Exists(armsFbx)) return null;

            var old = vm.Find("FP_Anim");
            if (old != null)
            {
                model.SetParent(vm, false);
                Object.DestroyImmediate(old.gameObject);
            }
            var rig = new GameObject("FP_Anim").transform;
            rig.SetParent(vm, false);
            model.SetParent(rig, false);

            var arms = (GameObject)PrefabUtility.InstantiatePrefab(GrayboxModels.LoadFbx(armsFbx), rig);
            arms.name = "Arms";
            var armsAnimator = arms.GetComponent<Animator>();
            if (armsAnimator != null) armsAnimator.enabled = false; // 팔은 위 FP_Anim이 움직인다
            if (GrayboxModels.Exists(propFbx))
            {
                var prop = (GameObject)PrefabUtility.InstantiatePrefab(GrayboxModels.LoadFbx(propFbx), rig);
                prop.name = propName;
            }
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = vm.gameObject.layer;

            var animator = rig.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = ac;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rig.gameObject.AddComponent<AnimationEventSink>();
            return animator;
        }

        // 1인칭 무기 모델: FBX가 있으면 넣고 true. 그림자 없이 (화면에 붙은 모델)
        static bool ViewModel(Transform holder, string fbxRelPath)
        {
            if (!GrayboxModels.Exists(fbxRelPath)) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(GrayboxModels.LoadFbx(fbxRelPath), holder);
            go.name = "Model";
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return true;
        }

        static void SetVector(Object target, string field, Vector3 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 효과 프리팹 (ART_SPEC 3-5 이름) ----
        public static void SetupEffects(Effects effects)
        {
            var hitExplosion = Flash("VFX_Tank_HitExplosion", GrayboxAssets.FxOrange, 1.0f, 2.5f, 0.8f, new Color(1f, 0.55f, 0.2f), 8f, 18f);
            var entries = new[]
            {
                Entry(EffectKind.MuzzleFlashM1, Flash("VFX_MuzzleFlash_M1", GrayboxAssets.FxYellow, 0.12f, 1.4f, 0.05f, new Color(1f, 0.75f, 0.4f), 3f, 6f), 0.15f),
                Entry(EffectKind.ImpactDirt, Flash("VFX_Impact_Dirt", GrayboxAssets.FxBrown, 0.15f, 2.2f, 0.3f, default, 0f, 0f), 0.5f),
                Entry(EffectKind.BazookaBackblast, Flash("VFX_Bazooka_Backblast", GrayboxAssets.FxWhite, 0.5f, 3f, 0.6f, new Color(1f, 0.8f, 0.6f), 4f, 8f), 1.5f),
                Entry(EffectKind.RocketExplosion, hitExplosion, 1.2f),
                Entry(EffectKind.TankRicochet, Flash("VFX_Tank_Ricochet", GrayboxAssets.FxYellow, 0.2f, 1.6f, 0.25f, new Color(1f, 0.9f, 0.6f), 5f, 6f), 0.4f),
                Entry(EffectKind.TankHitExplosion, hitExplosion, 1.2f),
                Entry(EffectKind.CannonBlast, hitExplosion, 1.2f),
                Entry(EffectKind.EnemyMuzzle, Flash("VFX_MuzzleFlash_Enemy", GrayboxAssets.FxYellow, 0.1f, 1.3f, 0.05f, new Color(1f, 0.7f, 0.35f), 2.5f, 5f), 0.15f),
                Entry(EffectKind.ClipEject, ClipEjectPrefab(), 4f),
                Entry(EffectKind.TankDestroyed, Flash("VFX_Tank_DestroyExplosion", GrayboxAssets.FxOrange, 2.0f, 4f, 1.2f, new Color(1f, 0.5f, 0.15f), 14f, 30f), 5f),
            };
            var so = new SerializedObject(effects);
            var list = so.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("kind").enumValueIndex = (int)entries[i].kind;
                e.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                e.FindPropertyRelative("lifetime").floatValue = entries[i].lifetime;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Effects.Entry Entry(EffectKind kind, GameObject prefab, float lifetime) => new Effects.Entry { kind = kind, prefab = prefab, lifetime = lifetime };

        static GameObject Flash(string name, Material mat, float size, float endScale, float duration, Color lightColor, float intensity, float range)
        {
            return GrayboxAssets.Prefab(name, () =>
            {
                var root = new GameObject();
                var visual = GrayboxAssets.Part(root.transform, PrimitiveType.Sphere, "Visual", Vector3.zero, Vector3.one * size, mat);
                visual.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Light light = null;
                if (intensity > 0f)
                {
                    light = root.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = lightColor;
                    light.intensity = intensity;
                    light.range = range;
                    light.shadows = LightShadows.None;
                }
                var fx = root.AddComponent<FlashEffect>();
                SetupUtil.SetRef(fx, "flashLight", light);
                SetupUtil.SetRef(fx, "visual", visual.transform);
                var so = new SerializedObject(fx);
                so.FindProperty("duration").floatValue = duration;
                so.FindProperty("endScale").floatValue = endScale;
                so.ApplyModifiedPropertiesWithoutUndo();
                return root;
            });
        }

        // 튀어나가는 빈 클립 (SM_WP_M1Garand_ClipEmpty). 4초 뒤 사라진다
        public static GameObject ClipEjectPrefab() => GrayboxAssets.Prefab("PF_WP_M1Garand_ClipEmpty", () =>
        {
            var root = new GameObject();
            GrayboxModels.Model(root, "Weapons/M1Garand/SM_WP_M1Garand_ClipEmpty.fbx", model =>
                GrayboxAssets.Part(model, PrimitiveType.Cube, "ClipEmpty", new Vector3(0, 0.025f, 0), new Vector3(0.02f, 0.05f, 0.06f), GrayboxAssets.Metal));
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.02f, 0.05f, 0.06f);
            col.center = new Vector3(0, 0.025f, 0);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return root;
        });

        public static GameObject RocketPrefab() => GrayboxAssets.Prefab("PF_WP_BazookaRocket", () =>
        {
            var root = new GameObject();
            GrayboxModels.Model(root, "Weapons/Bazooka/SM_WP_BazookaRocket.fbx", model =>
                GrayboxAssets.Part(model, PrimitiveType.Cylinder, "Body", Vector3.zero, new Vector3(0.09f, 0.3f, 0.09f), GrayboxAssets.Olive, new Vector3(90, 0, 0)));
            var rocket = root.AddComponent<Rocket>();
            // 연기 꼬리: 납품된 VFX_RocketTrail을 꼬리에 붙인다. 없으면 그레이박스 Trail
            var delivered = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/VFX/VFX_RocketTrail.prefab");
            if (delivered != null)
            {
                var fx = (GameObject)PrefabUtility.InstantiatePrefab(delivered, root.transform);
                fx.transform.localPosition = new Vector3(0, 0, -0.3f);
                SetupUtil.SetRef(rocket, "trail", fx.transform);
                return root;
            }
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 1f; // VFX_RocketTrail: 연기 꼬리 1초
            trail.startWidth = 0.18f;
            trail.endWidth = 0.6f;
            trail.sharedMaterial = GrayboxAssets.FxWhite;
            trail.startColor = new Color(0.9f, 0.9f, 0.85f, 0.8f);
            trail.endColor = new Color(0.5f, 0.5f, 0.5f, 0f);
            return root;
        });
    }
}
