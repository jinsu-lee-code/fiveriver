using System.IO;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 포격 준비 (2026-11-18). 여러 번 실행해도 결과가 같다.
    //  - ShellingConfig.asset: 없을 때만 BALANCE 2-6 값으로 만든다 (있으면 수치를 건드리지 않고 효과 연결만 채운다)
    //  - 효과 프리팹 VFX_Shell_Blast · VFX_Shell_Smolder · VFX_Trench_Dust, 재질 M_VFX_HorizonGlow: 다시 만든다
    //  - 씬: [Shelling] 오브젝트에 ShellingDirector를 두고 설정 에셋을 연결한다
    public static class ShellingSetup
    {
        const string Dir = "Assets/_Project/Art/VFX/Shelling20261118/";
        const string ConfigPath = DabudongSetup.DataDir + "ShellingConfig.asset";
        const string SmokeMat = "Assets/_Project/Art/Materials/M_VFX_Smoke.mat";
        const string GlowMat = "Assets/_Project/Art/Materials/M_VFX_Glow.mat";
        const string DebrisMat = "Assets/_Project/Art/JMO Assets/WarFX/Desktop/Materials/Impacts/WFX_M_Impact Debris.mat";

        [MenuItem("FiveRivers/Setup/Shelling")]
        public static void Run() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) return "not in play mode";
            Directory.CreateDirectory(Dir);
            var config = EnsureConfig();
            config.horizonGlow = HorizonMaterial();
            config.blastPrefab = BlastPrefab();
            config.smolderPrefab = SmolderPrefab();
            config.trenchDustPrefab = TrenchDustPrefab();
            config.dirtShowerPrefab = DirtShowerPrefab();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            if (EditorSceneManager.GetActiveScene().path != DabudongSetup.ScenePath) EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var holder = GameObject.Find("[Shelling]");
            if (holder == null) holder = new GameObject("[Shelling]");
            var director = holder.GetComponent<ShellingDirector>();
            if (director == null) director = holder.AddComponent<ShellingDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(holder.scene);
            EditorSceneManager.SaveScene(holder.scene);
            return $"shelling ready: rules {config.rules.Length}, glow {config.horizonGlow != null}, blast {config.blastPrefab != null}";
        }

        // ---- 수치 (BALANCE.md 2-6의 처음 값. 고칠 때는 에셋을 고치고 문서에 "→ 날짜 변경"으로 적는다) ----
        static ShellingConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShellingConfig>(ConfigPath);
            if (config != null) return config;
            config = ScriptableObject.CreateInstance<ShellingConfig>();
            config.rules = new[]
            {
                //    구간                     먼 포격 간격 · 음량   가까운 포격 간격 · 첫 발 · 최대  구역                                   플레이어 거리  예광탄      이웃 총성
                Rule(FlowSection.Tutorial,    7f, 16f, 0.9f,      34f, 55f, 14f, 0,  ShellingConfig.Zone.Field,        60f, 95f,   30f, 55f,  14f, 30f),
                Rule(FlowSection.Scout,       6f, 14f, 1f,        26f, 40f, 12f, 0,  ShellingConfig.Zone.Field,        38f, 80f,   25f, 45f,  10f, 22f),
                Rule(FlowSection.Assault,     5f, 12f, 1f,        15f, 26f, 9f,  0,  ShellingConfig.Zone.FieldAndRear, 18f, 62f,   18f, 34f,  8f,  18f),
                Rule(FlowSection.Regroup,     8f, 18f, 0.8f,      22f, 32f, 10f, 1,  ShellingConfig.Zone.Field,        34f, 75f,   28f, 50f,  14f, 30f),
                Rule(FlowSection.WestAssault, 5f, 12f, 1f,        15f, 25f, 8f,  0,  ShellingConfig.Zone.FieldAndRear, 18f, 60f,   18f, 34f,  8f,  18f),
                // 정적: 가까운 포격 없음, 예광탄 · 총성 없음. 아주 먼 포성만 낮고 드물게 (이 구간의 조용함이 주제다)
                Rule(FlowSection.Silence,     14f, 26f, 0.4f,     0f, 0f, 0f, 0,     ShellingConfig.Zone.None,         0f, 0f,     0f, 0f,    0f, 0f),
                // 전차: 전차 포와 헷갈리지 않게 드물고 멀리
                Rule(FlowSection.Tank,        7f, 15f, 0.9f,      36f, 55f, 22f, 0,  ShellingConfig.Zone.Field,        45f, 90f,   30f, 55f,  14f, 28f),
                Rule(FlowSection.HillAssault, 5f, 12f, 1f,        16f, 28f, 10f, 0,  ShellingConfig.Zone.AroundPlayer, 20f, 60f,   20f, 38f,  9f,  20f),
                // 새벽 · 결과 · 아군 지원 · 시작 화면: 표에 없음 = 아무것도 없다
            };
            AssetDatabase.CreateAsset(config, ConfigPath);
            Debug.Log("[Setup] 수치 에셋 생성: " + ConfigPath);
            return config;
        }

        static ShellingConfig.SectionRule Rule(FlowSection section, float distantMin, float distantMax, float distantLevel,
            float nearMin, float nearMax, float nearFirst, int nearCount, ShellingConfig.Zone zone, float playerMin, float playerMax,
            float tracerMin, float tracerMax, float gunMin, float gunMax) => new ShellingConfig.SectionRule
        {
            section = section, distantMin = distantMin, distantMax = distantMax, distantLevel = distantLevel,
            nearMin = nearMin, nearMax = nearMax, nearFirstDelay = nearFirst, nearMaxCount = nearCount, zone = zone,
            playerMin = playerMin, playerMax = playerMax, tracerMin = tracerMin, tracerMax = tracerMax, gunfireMin = gunMin, gunfireMax = gunMax,
        };

        static Material HorizonMaterial()
        {
            string path = Dir + "M_VFX_HorizonGlow.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Dir + "HorizonGlow.shader");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetFloat("_Glow", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- 효과 프리팹 ----
        static GameObject Save(GameObject root, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Dir + name + ".prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static ParticleSystem System(Transform parent, string name, string material, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(material);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            return ps;
        }

        static void Burst(ParticleSystem ps, int min, int max, float at = 0f)
        {
            var emission = ps.emission;
            int n = emission.burstCount;
            emission.burstCount = n + 1;
            emission.SetBurst(n, new ParticleSystem.Burst(at, (short)min, (short)max));
        }

        static Gradient Fade(Color a, float aTime, Color b, float bTime, Color c, float cTime, Color d)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, bTime), new GradientColorKey(c, cTime), new GradientColorKey(d, 1f) },
                new[] { new GradientAlphaKey(a.a, 0f), new GradientAlphaKey(b.a, bTime), new GradientAlphaKey(c.a, cTime), new GradientAlphaKey(d.a, 1f) });
            return g;
        }

        static AnimationCurve Grow(float from, float to) => new AnimationCurve(new Keyframe(0f, from, 0f, (to - from) * 2.2f), new Keyframe(1f, to, 0f, 0f));

        // 흙색: 터지는 순간에는 불빛에 비쳐 밝고, 곧 달빛 아래의 흙먼지가 된다. 밤하늘(푸른 .05 .07 .15)을 등져도
        // 지면(달빛 받은 흙)을 등져도 읽히게 하늘보다 밝고 따뜻한 회갈색으로 둔다.
        static readonly Color EarthLit = new Color(0.62f, 0.44f, 0.26f), EarthDark = new Color(0.155f, 0.135f, 0.115f), SmokeDark = new Color(0.105f, 0.105f, 0.115f);
        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        // 공기 저항: 속도 · 크기에 비례시키지 않는다 (기본값은 둘 다 곱해서 솟구치자마자 멈춘다)
        static void Drag(ParticleSystem ps, float amount)
        {
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 1000f;
            drag.dampen = 0f;
            drag.drag = amount;
            drag.multiplyDragByParticleSize = false;
            drag.multiplyDragByParticleVelocity = false;
        }

        static GameObject BlastPrefab()
        {
            var root = new GameObject("VFX_Shell_Blast");
            // 빛
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.74f, 0.48f);
            light.intensity = 0f;
            light.range = 45f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            var flash = root.AddComponent<ShellFlash>();
            var so = new SerializedObject(flash);
            so.FindProperty("flash").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 섬광: 둥근 공 하나가 아니라 흙 사이로 새는 작은 불빛 여러 개, 두세 프레임
            var core = System(root.transform, "Core", GlowMat, new Vector3(0f, 0.5f, 0f));
            var m = core.main;
            m.duration = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f); m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 16f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 2.4f); m.startColor = new Color(1f, 0.7f, 0.4f, 0.8f); m.maxParticles = 12;
            Burst(core, 7, 9);
            var shape = core.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 40f; shape.radius = 0.5f;
            var col = core.colorOverLifetime; col.enabled = true;
            col.color = Fade(new Color(1f, 1f, 1f, 1f), 0f, new Color(1f, 0.8f, 0.6f, 0.8f), 0.3f, new Color(1f, 0.5f, 0.3f, 0.3f), 0.7f, new Color(1f, 0.4f, 0.2f, 0f));
            var size = core.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, Grow(0.6f, 1.2f));

            // 흙기둥: 좁게 치솟았다가 제 무게로 무너져 내린다. 작은 덩이 여럿이 높이마다 흩어져 기둥 모양이 된다.
            var column = System(root.transform, "DirtColumn", SmokeMat, Vector3.zero);
            m = column.main;
            m.duration = 0.5f; m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 17f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.5f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 1.15f; m.maxParticles = 80; m.startColor = Color.white;
            Burst(column, 44, 50); Burst(column, 12, 14, 0.05f);
            shape = column.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15f; shape.radius = 0.7f;
            col = column.colorOverLifetime; col.enabled = true;
            col.color = Fade(A(EarthLit, 0f), 0f, A(EarthLit, 0.9f), 0.03f, A(EarthDark, 0.8f), 0.11f, A(EarthDark, 0f));
            size = column.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, Grow(0.65f, 2.1f));
            var rot = column.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.7f, 0.7f);
            Drag(column, 0.35f);

            // 옆으로 낮게 퍼지는 흙먼지
            var skirt = System(root.transform, "DustSkirt", SmokeMat, new Vector3(0f, 0.3f, 0f));
            m = skirt.main;
            m.duration = 0.5f; m.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4.2f); m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 11f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 0.02f; m.maxParticles = 36; m.startColor = Color.white;
            Burst(skirt, 20, 24);
            shape = skirt.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.8f; shape.radiusThickness = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // the ring lies on the ground and throws outward
            col = skirt.colorOverLifetime; col.enabled = true;
            col.color = Fade(A(EarthLit, 0f), 0f, A(EarthLit, 0.45f), 0.03f, A(EarthDark, 0.36f), 0.15f, A(EarthDark, 0f));
            size = skirt.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, Grow(0.6f, 2.2f));
            Drag(skirt, 2.4f);
            var force = skirt.forceOverLifetime; force.enabled = true; force.y = 0.35f; force.x = 0.25f;

            // 폭연: 기둥이 무너진 자리에서 잿빛 연기가 솟아 바람에 밀린다
            var smoke = System(root.transform, "Smoke", SmokeMat, new Vector3(0f, 0.6f, 0f));
            m = smoke.main;
            m.duration = 1f; m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 9f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.4f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.035f; m.maxParticles = 40; m.startColor = Color.white;
            Burst(smoke, 14, 16, 0.03f); Burst(smoke, 6, 8, 0.25f); Burst(smoke, 4, 5, 0.6f);
            shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 20f; shape.radius = 0.8f;
            col = smoke.colorOverLifetime; col.enabled = true;
            col.color = Fade(new Color(0.6f, 0.38f, 0.2f, 0f), 0f, new Color(0.38f, 0.27f, 0.18f, 0.6f), 0.04f, A(SmokeDark, 0.5f), 0.2f, A(new Color(0.14f, 0.14f, 0.15f), 0f));
            size = smoke.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, Grow(0.6f, 2.6f));
            rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            Drag(smoke, 1.3f);
            force = smoke.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World; force.x = 0.8f; force.y = 0.3f;
            var noise = smoke.noise; noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.3f; noise.scrollSpeed = 0.3f;

            // 흙덩이 · 돌: 포물선으로 흩어진다
            var debris = System(root.transform, "Debris", DebrisMat, new Vector3(0f, 0.2f, 0f));
            m = debris.main;
            m.duration = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(9f, 25f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.26f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 2f; m.maxParticles = 50; m.startColor = new Color(0.16f, 0.135f, 0.11f, 1f);
            Burst(debris, 26, 34);
            shape = debris.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 38f; shape.radius = 0.6f;
            var sheet = debris.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 3; sheet.numTilesY = 3;
            sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.cycleCount = 1;
            rot = debris.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            col = debris.colorOverLifetime; col.enabled = true;
            col.color = Fade(new Color(1f, 1f, 1f, 1f), 0f, new Color(1f, 1f, 1f, 1f), 0.3f, new Color(1f, 1f, 1f, 1f), 0.85f, new Color(1f, 1f, 1f, 0f));

            // 불티
            var sparks = System(root.transform, "Sparks", GlowMat, new Vector3(0f, 0.4f, 0f));
            m = sparks.main;
            m.duration = 0.2f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(12f, 30f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); m.gravityModifier = 1.4f; m.maxParticles = 30;
            m.startColor = new Color(1f, 0.62f, 0.28f, 1f);
            Burst(sparks, 10, 14);
            shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 50f; shape.radius = 0.4f;
            col = sparks.colorOverLifetime; col.enabled = true;
            col.color = Fade(new Color(1f, 1f, 1f, 1f), 0f, new Color(1f, 0.8f, 0.6f, 1f), 0.3f, new Color(1f, 0.5f, 0.3f, 0.6f), 0.7f, new Color(1f, 0.3f, 0.2f, 0f));
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.03f; sr.lengthScale = 1f;

            // 원뿔이 위를 향하게: 파티클 시스템은 +z로 쏘므로 세운다
            foreach (var name in new[] { "Core", "DirtColumn", "Smoke", "Debris", "Sparks" })
                root.transform.Find(name).localRotation = Quaternion.Euler(-90f, 0f, 0f);
            return Save(root, "VFX_Shell_Blast");
        }

        static GameObject SmolderPrefab()
        {
            var root = new GameObject("VFX_Shell_Smolder");
            var smoke = System(root.transform, "Smoke", SmokeMat, new Vector3(0f, 0.15f, 0f));
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var m = smoke.main;
            m.loop = true; m.duration = 5f; m.prewarm = false;
            m.startDelay = 1.2f; // the burst's own smoke comes first
            m.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = -0.012f; m.maxParticles = 60; m.startColor = Color.white;
            var emission = smoke.emission; emission.rateOverTime = 4.5f;
            var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 9f; shape.radius = 0.5f;
            var col = smoke.colorOverLifetime; col.enabled = true;
            col.color = Fade(A(SmokeDark, 0f), 0f, A(new Color(0.13f, 0.13f, 0.14f), 0.42f), 0.12f, A(new Color(0.16f, 0.16f, 0.175f), 0.3f), 0.55f, A(new Color(0.18f, 0.18f, 0.2f), 0f));
            var size = smoke.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, Grow(0.5f, 3.2f));
            var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            var force = smoke.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World; force.x = 0.38f; force.z = 0.1f;
            var noise = smoke.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
            var burn = root.AddComponent<Smolder>();
            var so = new SerializedObject(burn);
            so.FindProperty("smoke").objectReferenceValue = smoke;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(root, "VFX_Shell_Smolder");
        }

        // 진지 바로 앞 낙탄: 흙과 돌이 흉벽을 넘어 머리 위로 쏟아진다. 프리팹의 +z가 폭발에서 플레이어 머리 위쪽을 향한다.
        static GameObject DirtShowerPrefab()
        {
            var root = new GameObject("VFX_Dirt_Shower");
            var clods = System(root.transform, "Clods", DebrisMat, Vector3.zero);
            var m = clods.main;
            m.duration = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(9f, 19f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.075f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 1.5f; m.maxParticles = 110; m.startColor = new Color(0.17f, 0.14f, 0.11f, 1f);
            Burst(clods, 60, 75); Burst(clods, 20, 26, 0.08f);
            var shape = clods.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 24f; shape.radius = 0.8f;
            var sheet = clods.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 3; sheet.numTilesY = 3;
            sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.cycleCount = 1;
            var rot = clods.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            var col = clods.colorOverLifetime; col.enabled = true;
            col.color = Fade(new Color(1f, 1f, 1f, 1f), 0f, new Color(1f, 1f, 1f, 1f), 0.3f, new Color(1f, 1f, 1f, 1f), 0.85f, new Color(1f, 1f, 1f, 0f));
            var hit = clods.collision; hit.enabled = true; hit.type = ParticleSystemCollisionType.World; hit.mode = ParticleSystemCollisionMode.Collision3D;
            hit.bounce = 0.2f; hit.dampen = 0.55f; hit.lifetimeLoss = 0.35f; hit.quality = ParticleSystemCollisionQuality.Low;
            hit.collidesWith = FiveRivers.Core.WorldQuery.Solid;
            // a thin veil of dust carried over with it
            var veil = System(root.transform, "Veil", SmokeMat, Vector3.zero);
            m = veil.main;
            m.duration = 0.4f; m.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f); m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            m.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.6f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 0.05f; m.maxParticles = 16; m.startColor = Color.white;
            Burst(veil, 9, 12);
            shape = veil.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28f; shape.radius = 1f;
            col = veil.colorOverLifetime; col.enabled = true;
            col.color = Fade(A(EarthDark, 0f), 0f, A(EarthDark, 0.26f), 0.1f, A(EarthDark, 0.16f), 0.5f, A(EarthDark, 0f));
            var size = veil.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, Grow(0.6f, 2.2f));
            Drag(veil, 1.6f);
            return Save(root, "VFX_Dirt_Shower");
        }

        // 가까운 폭발이 던져 올린 흙이 잠시 뒤 플레이어 둘레에 후두둑 떨어진다. 프리팹의 +z가 폭발에서 멀어지는 쪽이다.
        // (파일 이름은 처음 시도한 "참호 벽에서 흘러내리는 흙" 그대로 둔다.)
        static GameObject TrenchDustPrefab()
        {
            var root = new GameObject("VFX_Trench_Dust");
            var grains = System(root.transform, "Grains", DebrisMat, Vector3.zero);
            var m = grains.main;
            m.duration = 0.9f; m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.7f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.gravityModifier = 1f; m.maxParticles = 90; m.startColor = new Color(0.17f, 0.14f, 0.11f, 1f);
            Burst(grains, 18, 24); Burst(grains, 16, 22, 0.15f); Burst(grains, 10, 14, 0.35f); Burst(grains, 5, 8, 0.6f);
            var shape = grains.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(6f, 3.5f, 0.3f);
            shape.rotation = new Vector3(100f, 0f, 0f); // falling, and carried a little on away from the burst
            var sheet = grains.textureSheetAnimation; sheet.enabled = true; sheet.numTilesX = 3; sheet.numTilesY = 3;
            sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.cycleCount = 1;
            var rot = grains.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var col = grains.colorOverLifetime; col.enabled = true;
            col.color = Fade(new Color(1f, 1f, 1f, 0f), 0f, new Color(1f, 1f, 1f, 1f), 0.1f, new Color(1f, 1f, 1f, 1f), 0.85f, new Color(1f, 1f, 1f, 0f));
            var hit = grains.collision; hit.enabled = true; hit.type = ParticleSystemCollisionType.World; hit.mode = ParticleSystemCollisionMode.Collision3D;
            hit.bounce = 0.15f; hit.dampen = 0.6f; hit.lifetimeLoss = 0.5f; hit.quality = ParticleSystemCollisionQuality.Low;
            hit.collidesWith = FiveRivers.Core.WorldQuery.Solid; hit.radiusScale = 1f;
            return Save(root, "VFX_Trench_Dust");
        }
    }
}
