using System;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    public static class FirstPersonAnimationSetup
    {
        const string Art = "Assets/_Project/Art/";
        const string FP = Art + "Animations/FP/";
        const string Arms = Art + "Models/Characters/ROK_Arms/SK_CH_ROK_Arms.fbx";
        const string LauncherSleeves = Art + "Models/Characters/ROK_Arms/M_CH_ROK_Bazooka_FP.mat";
        const string Rocket = Art + "Models/Weapons/Bazooka/SM_WP_BazookaRocket.fbx";
        const string Smoke = "eb2412df5134fe749b8468bef9340100";

        [MenuItem("FiveRivers/Animation/Connect First Person")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player == null) throw new InvalidOperationException("다부동 플레이어를 찾을 수 없습니다.");
            Configure(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        internal static void Configure(PlayerController player)
        {
            var bridge = player.GetComponent<FirstPersonAnimatorBridge>();
            var m1 = player.GetComponent<M1Rifle>();
            var bazooka = player.GetComponent<Bazooka>();
            var m1View = m1.Model.GetComponentInChildren<M1Presentation>(true);
            if (m1View == null) throw new InvalidOperationException("보정된 M1 표시 오브젝트가 필요합니다.");

            var m1Controller = Controller("AC_FP_M1");
            var bazookaController = Controller("AC_FP_Bazooka");
            ValidateRootCurves(m1Controller);
            ValidateRootCurves(bazookaController);
            PrepareController(m1Controller);
            PrepareController(bazookaController, reverseEquip: true);

            var m1Animator = ConnectAnimator(m1.Model, m1Controller);
            m1View.transform.SetParent(m1Animator.transform, false);
            SetupUtil.SetRef(m1View, "animationFrame", m1Animator.transform);

            var bazookaAnimator = ConnectLauncher(bazooka, bazookaController);
            SetupUtil.SetRef(bridge, "m1Animator", m1Animator);
            SetupUtil.SetRef(bridge, "bazookaAnimator", bazookaAnimator);
        }

        static Animator ConnectAnimator(Transform parent, AnimatorController controller)
        {
            var frame = SetupUtil.Child(parent, "FP_Anim");
            var animator = SetupUtil.Ensure<Animator>(frame);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.keepAnimatorStateOnDisable = false;
            SetupUtil.Ensure<AnimationEventSink>(frame);
            return animator;
        }

        static Animator ConnectLauncher(Bazooka bazooka, AnimatorController controller)
        {
            var model = bazooka.Model;
            var oldGrip = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Grip");
            var oldSight = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sight");
            var sightMaterial = oldSight != null ? oldSight.GetComponent<Renderer>()?.sharedMaterial
                : AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/M_WP_Bazooka.mat");
            var shot = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ShotPresentation");
            if (shot == null)
            {
                shot = SetupUtil.Child(model, "ShotPresentation").transform;
                var body = model.Find("Model");
                if (body != null) body.SetParent(shot, false);
            }

            // 회전·이동은 화면의 총에만 적용한다. 실제 로켓·후폭풍 소켓은 VM 아래에 그대로 둔다.
            var reloadFrame = SetupUtil.Child(model, "FP_ReloadFrame").transform;
            var animator = ConnectAnimator(reloadFrame, controller);
            shot.SetParent(animator.transform, false);
            var arms = animator.transform.Find("Arms");
            if (arms == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(Arms), animator.transform);
                instance.name = "Arms";
                arms = instance.transform;
                foreach (var childAnimator in arms.GetComponentsInChildren<Animator>(true))
                    childAnimator.enabled = false;
            }

            var launcher = SetupUtil.Ensure<BazookaPresentation>(model.gameObject);
            var data = new SerializedObject(launcher);
            void Reference(string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
            Reference("bazooka", bazooka);
            Reference("weapons", bazooka.GetComponent<WeaponHolder>());
            Reference("visualFrame", shot);
            Reference("muzzle", Marker(shot, "Muzzle_Visual", new Vector3(0f, .028f, .94f)));
            if (data.FindProperty("smokeMaterial").objectReferenceValue == null)
                Reference("smokeMaterial", AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(Smoke)));
            Reference("animator", animator);
            Reference("reloadFrame", reloadFrame);
            data.FindProperty("reloadPosition").vector3Value = new Vector3(.29f, .045f, .47f);

            var rear = Marker(shot, "Sight_RearCentre", new Vector3(0f, .148f, .235f));
            var front = Marker(shot, "Sight_FrontTip", new Vector3(0f, .209f, .500f));
            BuildRearAperture(rear, sightMaterial);
            BazookaBoreSetup.OpenRear(shot, sightMaterial);
            var aimRotation = Quaternion.Inverse(Quaternion.LookRotation(front.localPosition - rear.localPosition, Vector3.up));
            data.FindProperty("aimViewPosition").vector3Value = Vector3.forward * .14f - aimRotation * rear.localPosition;
            data.FindProperty("aimViewRotation").quaternionValue = aimRotation;
            if (oldGrip != null) oldGrip.gameObject.SetActive(false);
            if (oldSight != null) oldSight.gameObject.SetActive(false);

            Reference("rearMouth", Marker(shot, "Rocket_RearMouth", new Vector3(0f, .004f, -.582f)));
            var rocket = SetupUtil.Child(shot, "RocketInHand");
            if (rocket.transform.childCount == 0)
            {
                var prop = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(Rocket), rocket.transform);
                prop.transform.localScale = Vector3.one * .75f;
                prop.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            rocket.SetActive(false);
            Reference("rocketInHand", rocket.transform);

            var idle = Clip("A_FP_Bazooka_Idle");
            idle.SampleAnimation(animator.gameObject, 0f);
            var bones = arms.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            BindGrip("Left", new Vector3(-.047f, -.197f, .459f));
            BindGrip("Right", new Vector3(.047f, -.198f, -.033f));
            void BindGrip(string side, Vector3 position)
            {
                string prefix = "mixamorig:" + side;
                string field = side.ToLowerInvariant();
                var wrist = bones[prefix + "Hand"];
                var forward = wrist.InverseTransformDirection(bones[prefix + "HandMiddle1"].position - wrist.position);
                var across = wrist.InverseTransformDirection(bones[prefix + "HandIndex1"].position - bones[prefix + "HandPinky1"].position);
                var hand = Quaternion.LookRotation(forward, Vector3.Cross(forward, across));
                var grip = Marker(shot, "Grip_" + side, position);
                grip.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.left) * Quaternion.Inverse(hand);
                Reference(field + "Upper", bones[prefix + "Arm"]);
                Reference(field + "Forearm", bones[prefix + "ForeArm"]);
                Reference(field + "Wrist", wrist);
                Reference(field + "Grip", grip);
            }

            foreach (var renderer in animator.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.layer = 31;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    skin.updateWhenOffscreen = true;
                    skin.sharedMaterial = LauncherArmMaterial(skin.sharedMaterial);
                }
            }
            var fill = SetupUtil.Ensure<Light>(SetupUtil.Child(shot, "Bazooka_MoonFill"));
            fill.transform.localPosition = new Vector3(-.26f, .26f, .32f);
            fill.type = LightType.Point;
            fill.intensity = .24f;
            fill.range = 1.5f;
            fill.color = new Color(.92f, .94f, 1f);
            fill.shadows = LightShadows.None;
            fill.cullingMask = 1 << 31;
            data.ApplyModifiedPropertiesWithoutUndo();
            return animator;
        }

        static Material LauncherArmMaterial(Material source)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(LauncherSleeves);
            if (material != null || source == null) return material;
            material = new Material(source) { name = "M_CH_ROK_Bazooka_FP" };
            material.SetFloat("_Smoothness", .24f);
            material.SetColor("_BaseColor", new Color(.93f, .86f, .80f));
            AssetDatabase.CreateAsset(material, LauncherSleeves);
            return material;
        }

        static void BuildRearAperture(Transform rear, Material material)
        {
            // 얇은 금속 링과 지지대. 중앙에는 앞 가늠쇠를 볼 수 있는 실제 구멍이 있다.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var turn = Quaternion.Euler(0f, 0f, angle);
                Part("Rim_" + i, turn * Vector3.up * .0095f, turn, new Vector3(.0085f, .0021f, .005f));
            }
            Part("Base", new Vector3(0f, -.0345f, 0f), Quaternion.identity, new Vector3(.0085f, .051f, .010f));

            void Part(string name, Vector3 position, Quaternion rotation, Vector3 size)
            {
                var part = rear.Find(name);
                if (part == null)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(cube.GetComponent<Collider>());
                    cube.name = name;
                    part = cube.transform;
                    part.SetParent(rear, false);
                }
                part.SetLocalPositionAndRotation(position, rotation);
                part.localScale = size;
                if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        static Transform Marker(Transform parent, string name, Vector3 position)
        {
            var marker = SetupUtil.Child(parent, name).transform;
            marker.localPosition = position;
            return marker;
        }

        static void ValidateRootCurves(AnimatorController controller)
        {
            foreach (var clip in controller.animationClips)
                if (!AnimationUtility.GetCurveBindings(clip).Any(b => b.path.Length == 0 && b.type == typeof(Transform)))
                    throw new InvalidOperationException($"{clip.name}에 1인칭 표시 계층을 움직이는 루트 곡선이 없습니다.");
        }

        static void PrepareController(AnimatorController controller, bool reverseEquip = false)
        {
            var machine = controller.layers[0].stateMachine;
            if (reverseEquip)
            {
                var reverse = ReverseLauncherEquip();
                var lower = machine.states.FirstOrDefault(s => s.state.name == "Unequip").state;
                if (lower == null)
                {
                    lower = machine.AddState("Unequip", new Vector3(570f, 270f, 0f));
                    var transition = machine.AddAnyStateTransition(lower);
                    transition.hasExitTime = false;
                    transition.hasFixedDuration = true;
                    transition.duration = .035f;
                    transition.canTransitionToSelf = false;
                    transition.AddCondition(AnimatorConditionMode.If, 0f, "Unequip");
                }
                lower.motion = reverse;
            }

            foreach (var child in machine.states)
            {
                if (child.state.name == "Unequip")
                {
                    foreach (var transition in child.state.transitions) child.state.RemoveTransition(transition);
                    continue;
                }
                foreach (var transition in child.state.transitions)
                    if (transition.hasExitTime)
                    {
                        transition.exitTime = .99f;
                        transition.hasFixedDuration = true;
                        transition.duration = .035f;
                    }
            }
            EditorUtility.SetDirty(controller);
        }

        static AnimationClip ReverseLauncherEquip()
        {
            var source = Clip("A_FP_Bazooka_Equip");
            string path = FP + "A_FP_Bazooka_Unequip.anim";
            var reverse = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (reverse == null)
            {
                reverse = new AnimationClip { name = "A_FP_Bazooka_Unequip" };
                AssetDatabase.CreateAsset(reverse, path);
            }
            reverse.frameRate = source.frameRate;
            var bindings = AnimationUtility.GetCurveBindings(source);
            var curves = new AnimationCurve[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                var curve = AnimationUtility.GetEditorCurve(source, bindings[i]);
                var keys = curve.keys.Reverse().Select(key => new Keyframe(
                    Mathf.Max(0f, source.length - key.time), key.value, -key.outTangent, -key.inTangent)).ToArray();
                curves[i] = new AnimationCurve(keys);
            }
            AnimationUtility.SetEditorCurves(reverse, bindings, curves);
            reverse.EnsureQuaternionContinuity();
            AnimationUtility.SetAnimationEvents(reverse, new[] { new AnimationEvent { functionName = "UnequipDone", time = reverse.length * .90f } });
            EditorUtility.SetDirty(reverse);
            return reverse;
        }

        static AnimatorController Controller(string name) =>
            AssetDatabase.LoadAssetAtPath<AnimatorController>(Art + "Animations/" + name + ".controller")
            ?? throw new InvalidOperationException($"컨트롤러를 찾을 수 없습니다. {name}");

        static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(FP + name + ".fbx").OfType<AnimationClip>().Single(clip => clip.name == name);

        static GameObject LoadModel(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException($"모델을 찾을 수 없습니다. {path}");
    }
}
