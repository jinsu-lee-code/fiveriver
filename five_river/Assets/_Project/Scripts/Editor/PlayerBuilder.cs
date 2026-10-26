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
            GrayboxAssets.Part(m1Model, PrimitiveType.Cube, "Stock", new Vector3(0, 0, -0.15f), new Vector3(0.05f, 0.07f, 0.7f), GrayboxAssets.RifleWood);
            GrayboxAssets.Part(m1Model, PrimitiveType.Cylinder, "Barrel", new Vector3(0, 0.02f, 0.4f), new Vector3(0.022f, 0.25f, 0.022f), GrayboxAssets.Metal, new Vector3(90, 0, 0));
            GrayboxAssets.Part(m1Model, PrimitiveType.Cube, "RearSight", new Vector3(0, 0.055f, 0.05f), new Vector3(0.03f, 0.03f, 0.02f), GrayboxAssets.Metal);
            GrayboxAssets.Part(m1Model, PrimitiveType.Cube, "FrontSight", new Vector3(0, 0.05f, 0.62f), new Vector3(0.006f, 0.03f, 0.01f), GrayboxAssets.Metal);
            var m1Muzzle = new GameObject("Socket_Muzzle").transform;
            m1Muzzle.SetParent(m1Model, false);
            m1Muzzle.localPosition = new Vector3(0, 0.02f, 0.68f);

            var m1 = player.AddComponent<M1Rifle>();
            SetupUtil.SetRef(m1, "config", weaponConfig);
            SetupUtil.SetRef(m1, "player", controller);
            SetupUtil.SetRef(m1, "view", cam);
            SetupUtil.SetRef(m1, "effects", effects);
            SetupUtil.SetRef(m1, "muzzle", m1Muzzle);
            SetupUtil.SetRef(m1, "model", m1Model);
            SetVector(m1, "hipPosition", new Vector3(0.2f, -0.22f, 0.45f));
            SetVector(m1, "aimPosition", new Vector3(0f, -0.068f, 0.22f)); // 가늠자 · 가늠쇠가 화면 중앙에 오도록

            // ---- 바주카 ----
            var bzModel = new GameObject("VM_Bazooka").transform;
            bzModel.SetParent(camGo.transform, false);
            bzModel.localPosition = new Vector3(0.2f, -0.13f, 0.1f);
            GrayboxAssets.Part(bzModel, PrimitiveType.Cylinder, "Tube", new Vector3(0, 0, 0.1f), new Vector3(0.09f, 0.76f, 0.09f), GrayboxAssets.Olive, new Vector3(90, 0, 0));
            GrayboxAssets.Part(bzModel, PrimitiveType.Cube, "Grip", new Vector3(0, -0.09f, 0.15f), new Vector3(0.03f, 0.12f, 0.05f), GrayboxAssets.Metal);
            GrayboxAssets.Part(bzModel, PrimitiveType.Cube, "Sight", new Vector3(-0.06f, 0.05f, 0.3f), new Vector3(0.02f, 0.05f, 0.02f), GrayboxAssets.Metal);
            var bzMuzzle = new GameObject("Socket_RocketSpawn").transform;
            bzMuzzle.SetParent(bzModel, false);
            bzMuzzle.localPosition = new Vector3(0, 0, 0.9f);
            var bzBack = new GameObject("Socket_Backblast").transform;
            bzBack.SetParent(bzModel, false);
            bzBack.localPosition = new Vector3(0, 0, -0.7f);
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

            return controller;
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

        public static GameObject RocketPrefab() => GrayboxAssets.Prefab("PF_WP_BazookaRocket", () =>
        {
            var root = new GameObject();
            var model = GrayboxAssets.ModelRoot(root);
            GrayboxAssets.Part(model, PrimitiveType.Cylinder, "Body", Vector3.zero, new Vector3(0.09f, 0.3f, 0.09f), GrayboxAssets.Olive, new Vector3(90, 0, 0));
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 1f; // VFX_RocketTrail: 연기 꼬리 1초
            trail.startWidth = 0.18f;
            trail.endWidth = 0.6f;
            trail.sharedMaterial = GrayboxAssets.FxWhite;
            trail.startColor = new Color(0.9f, 0.9f, 0.85f, 0.8f);
            trail.endColor = new Color(0.5f, 0.5f, 0.5f, 0f);
            root.AddComponent<Rocket>();
            return root;
        });
    }
}
