using System.Linq;
using FiveRivers.Combat;
using FiveRivers.Core;
using FiveRivers.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 전차 그레이박스 (PF_VH_T34_85: 실측 크기 박스, 부품 이름 Hull / Turret / Gun / TrackL / TrackR, ART_SPEC 3-3)
    // 연기 · 화재(VFX_Tank_Smoke · VFX_Tank_Fire)는 프리팹 소켓 밑에 둔다. 씬의 [Tank]는 매번 다시 만든다.
    static class TankBuilder
    {
        const string ParticleSource = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        public static void Build(FlowDirector director, LevelPoints points, PlayerController player, Effects effects)
        {
            var old = GameObject.Find("/[Tank]");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("[Tank]");
            var prefab = TankPrefab();
            var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            body.transform.SetPositionAndRotation(points.tankHold.position, points.tankHold.rotation);

            var tank = root.AddComponent<TankController>();
            SetupUtil.SetRef(tank, "director", director);
            SetupUtil.SetRef(tank, "config", SetupUtil.LoadAsset<TankConfig>(DabudongSetup.DataDir + "TankConfig.asset"));
            SetupUtil.SetRef(tank, "points", points);
            SetupUtil.SetRef(tank, "effects", effects);
            SetupUtil.SetRef(tank, "player", player);
            SetupUtil.SetRef(tank, "playerHealth", player.GetComponent<PlayerHealth>());
            SetupUtil.SetRef(tank, "body", body.transform);
            var turret = body.transform.Find("Model/Turret");
            SetupUtil.SetRef(tank, "turret", turret);
            SetupUtil.SetRef(tank, "gunMuzzle", turret.GetComponentsInChildren<Transform>().First(t => t.name == "Socket_Muzzle"));
            SetupUtil.SetRef(tank, "smoke", body.transform.Find("Socket_Smoke/VFX_Tank_Smoke").gameObject);
            SetupUtil.SetRef(tank, "fire", body.transform.Find("Socket_Fire/VFX_Tank_Fire").gameObject);
            // 불탄 상태 재질 (ART_SPEC 3-3 M_VH_T34_Burnt)
            SetupUtil.SetRef(tank, "burntMaterial", GrayboxModels.SlotMaterial("M_VH_T34_Burnt"));

            // 궤도(재질 M_VH_T34_Track)를 뺀 차체 · 포탑 · 포
            var renderers = body.transform.Find("Model").GetComponentsInChildren<Renderer>()
                .Where(r => !r.name.StartsWith("Track")).ToArray();
            var so = new SerializedObject(tank);
            var list = so.FindProperty("bodyRenderers");
            list.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject TankPrefab() => GrayboxAssets.Prefab("PF_VH_T34_85", () =>
        {
            var root = new GameObject();
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(3.0f, 2.7f, 6.4f);
            col.center = new Vector3(0, 1.35f, 0);
            var model = GrayboxModels.Model(root, "Vehicles/T34_85/SM_VH_T34_85.fbx", m =>
            {
                GrayboxAssets.Part(m, PrimitiveType.Cube, "Hull", new Vector3(0, 0.95f, 0), new Vector3(3.0f, 1.0f, 6.0f), GrayboxAssets.Tank);
                GrayboxAssets.Part(m, PrimitiveType.Cube, "TrackL", new Vector3(-1.35f, 0.5f, 0), new Vector3(0.5f, 1.0f, 6.4f), GrayboxAssets.Track);
                GrayboxAssets.Part(m, PrimitiveType.Cube, "TrackR", new Vector3(1.35f, 0.5f, 0), new Vector3(0.5f, 1.0f, 6.4f), GrayboxAssets.Track);
                var t = new GameObject("Turret").transform;
                t.SetParent(m, false);
                t.localPosition = new Vector3(0, 1.45f, -0.3f);
                GrayboxAssets.Part(t, PrimitiveType.Cube, "TurretBody", new Vector3(0, 0.4f, 0), new Vector3(2.1f, 0.8f, 2.6f), GrayboxAssets.Tank);
                var g = new GameObject("Gun").transform;
                g.SetParent(t, false);
                g.localPosition = new Vector3(0, 0.45f, 1.3f);
                GrayboxAssets.Part(g, PrimitiveType.Cylinder, "Barrel", new Vector3(0, 0, 2.2f), new Vector3(0.18f, 2.2f, 0.18f), GrayboxAssets.Tank, new Vector3(90, 0, 0));
            });
            // 정면 경사 장갑 표시 (그레이박스: "비스듬해서 튕긴다"로 읽히게) — 최종 모델이 오면 지운다
            GrayboxAssets.Part(root.transform, PrimitiveType.Cube, "Glacis", new Vector3(0, 0.95f, 3.25f), new Vector3(2.9f, 0.9f, 0.9f), GrayboxModels.SlotMaterial("M_VH_T34_Body"), new Vector3(-35f, 0, 0));
            // 코드가 붙는 위치는 프리팹 안 빈 오브젝트 (ART_SPEC 1-1): 포구
            var gun = model.GetComponentsInChildren<Transform>().First(t => t.name == "Gun");
            var muzzle = new GameObject("Socket_Muzzle").transform;
            muzzle.SetParent(gun, false);
            muzzle.localPosition = new Vector3(0, 0, gun.name == "Gun" && gun.GetComponent<Renderer>() != null ? 4.4f : 4.5f);

            var smokeSocket = new GameObject("Socket_Smoke").transform;
            smokeSocket.SetParent(root.transform, false);
            smokeSocket.localPosition = new Vector3(0, 1.6f, -2.2f);
            var smoke = (GameObject)PrefabUtility.InstantiatePrefab(SmokePrefab(), smokeSocket);
            smoke.SetActive(false);

            var fireSocket = new GameObject("Socket_Fire").transform;
            fireSocket.SetParent(root.transform, false);
            fireSocket.localPosition = new Vector3(0, 1.6f, -1.6f);
            var fire = (GameObject)PrefabUtility.InstantiatePrefab(FirePrefab(), fireSocket);
            fire.SetActive(false);
            return root;
        });

        static Material ParticleMat(string name, Color color, bool additive)
        {
            string path = GrayboxAssets.MaterialDir + "M_Graybox_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            GrayboxAssets.EnsureFolder(GrayboxAssets.MaterialDir);
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>(ParticleSource)) { name = "M_Graybox_" + name };
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // VFX_Tank_Smoke: 검회색 연기 기둥, 루프 (측면 1발 = "한 발 더 맞히면 된다")
        public static GameObject SmokePrefab() => GrayboxAssets.Prefab("VFX_Tank_Smoke", () =>
        {
            var root = new GameObject();
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 5f;
            main.startSpeed = 1.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
            main.startColor = new Color(0.12f, 0.12f, 0.12f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = grad;
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Smoke", new Color(0.2f, 0.2f, 0.2f, 0.75f), false);
            return root;
        });

        // VFX_Tank_Fire: 불꽃 + 짙은 연기 + 깜빡이는 점광원, 루프 (측면 2발 · 정지)
        public static GameObject FirePrefab() => GrayboxAssets.Prefab("VFX_Tank_Fire", () =>
        {
            var root = new GameObject();
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 2.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startColor = new Color(1f, 0.55f, 0.15f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 28f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.8f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = ParticleMat("Fire", new Color(1f, 0.6f, 0.2f, 0.9f), true);

            var smoke = (GameObject)PrefabUtility.InstantiatePrefab(SmokePrefab(), root.transform);
            smoke.transform.localPosition = new Vector3(0, 1f, 0);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.15f);
            light.range = 16f;
            light.intensity = 8f;
            light.shadows = LightShadows.None;
            root.AddComponent<FlickerLight>();
            return root;
        });
    }
}
