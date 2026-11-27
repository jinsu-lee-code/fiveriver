using System.Collections.Generic;
using System.Linq;
using System.Text;
using FiveRivers.Combat;
using FiveRivers.Data;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 적 부분 래그돌 뼈대 (부족한 점 3번, 2026-11-26). 11조각(엉덩이 · 가슴 · 머리 · 위팔 · 아래팔 · 넓적다리 · 종아리)의
    // Rigidbody · 충돌체 · CharacterJoint 를 만든다. 관절 범위는 Unity 래그돌 생성기(RagdollBuilder)의 기본값과 같은 규칙
    // (이 버전에서는 그 마법사를 코드로 만들 수 없어 같은 규칙을 여기서 따름): 넓적다리 -20~70° · 옆 30°, 무릎 -80~0°,
    // 가슴 ±20° · 옆 10°, 위팔 -70~10° · 옆 50°, 팔꿈치 -90~0°, 머리 -40~25° · 옆 25°. 질량은 사람 몸 비율.
    // 살아 있는 동안은 Kinematic · 충돌체 꺼짐. 몸 재질(마찰), EnemyRagdoll 연결. 다시 실행하면 지우고 같게 다시 만든다.
    public static class EnemyRagdollSetup
    {
        static readonly string[] PrefabPaths = { "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman.prefab", "Assets/_Project/Prefabs/PF_CH_KPA_Rifleman_B.prefab" };
        const string MaterialPath = "Assets/_Project/Art/Physics/PM_Body.physicMaterial";
        // +1: a positive twist of a CharacterJoint is a positive Quaternion turn about its axis; -1 if PhysX counts it the other way
        public static float FlexSign = -1f; // measured 2026-11-26: with +1 the knees and elbows bent the wrong way (ClaudeRagdollProbe)

        // world sizes (m) of the colliders, for a man of about 1.7 m
        const float ThighRadius = 0.075f, ShinRadius = 0.055f, UpperArmRadius = 0.05f, ForearmRadius = 0.045f, HeadRadius = 0.11f, Depth = 0.22f;

        [MenuItem("FiveRivers/Setup/Enemy Ragdoll")]
        public static void Run() => Debug.Log(Apply());

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "play mode";
            var sb = new StringBuilder();
            var config = AssetDatabase.LoadAssetAtPath<BodyPhysicsConfig>(BodyPhysicsConfig.AssetPath);
            if (config == null) { config = ScriptableObject.CreateInstance<BodyPhysicsConfig>(); AssetDatabase.CreateAsset(config, BodyPhysicsConfig.AssetPath); }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
            if (material == null) { material = new PhysicsMaterial("PM_Body"); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.dynamicFriction = config.friction;
            material.staticFriction = 1f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Maximum;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);

            sb.AppendLine(EnsureLayer());
            foreach (var path in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { sb.AppendLine(Build(root, config, material, path)); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        // Layer 9 "Ragdoll": the bodies meet only the ground and each other (Default holds the terrain and the sandbags;
        // the player, the squad and the living men on Default are ignored per body at the handover, EnemyRagdoll).
        // WorldQuery.All / Solid leave it out, so the dead stop no rounds and block no sight.
        static string EnsureLayer()
        {
            int layer = FiveRivers.Core.WorldQuery.RagdollLayer;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            var slot = layers.GetArrayElementAtIndex(layer);
            if (slot.stringValue != "Ragdoll")
            {
                if (!string.IsNullOrEmpty(slot.stringValue)) return $"layer {layer} is already '{slot.stringValue}': not changed";
                slot.stringValue = "Ragdoll";
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, i != 0 && i != layer);
            var dynamics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if (dynamics.Length > 0) EditorUtility.SetDirty(dynamics[0]);
            return $"layer {layer} Ragdoll: collides with Default and itself only";
        }

        static Transform Bone(Animator animator, HumanBodyBones bone)
        {
            string name = HumanTrait.BoneName[(int)bone];
            foreach (var hb in animator.avatar.humanDescription.human)
                if (hb.humanName == name) return animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == hb.boneName);
            return null;
        }

        sealed class Part
        {
            public Transform bone, parent;
            public float mass, min, max, swing, swing2;
            public Vector3 axis, normal; // world
        }

        static string Build(GameObject root, BodyPhysicsConfig config, PhysicsMaterial material, string path)
        {
            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return path + ": no humanoid animator";
            Transform B(HumanBodyBones b) => Bone(animator, b);
            Transform hips = B(HumanBodyBones.Hips), spine = B(HumanBodyBones.Spine), chest = B(HumanBodyBones.Chest), neck = B(HumanBodyBones.Neck), head = B(HumanBodyBones.Head);
            Transform lThigh = B(HumanBodyBones.LeftUpperLeg), lShin = B(HumanBodyBones.LeftLowerLeg), lFoot = B(HumanBodyBones.LeftFoot), lToes = B(HumanBodyBones.LeftToes);
            Transform rThigh = B(HumanBodyBones.RightUpperLeg), rShin = B(HumanBodyBones.RightLowerLeg), rFoot = B(HumanBodyBones.RightFoot), rToes = B(HumanBodyBones.RightToes);
            Transform lArm = B(HumanBodyBones.LeftUpperArm), lFore = B(HumanBodyBones.LeftLowerArm), lHand = B(HumanBodyBones.LeftHand);
            Transform rArm = B(HumanBodyBones.RightUpperArm), rFore = B(HumanBodyBones.RightLowerArm), rHand = B(HumanBodyBones.RightHand);
            var needed = new[] { hips, spine, chest, neck, head, lThigh, lShin, lFoot, rThigh, rShin, rFoot, lArm, lFore, lHand, rArm, rFore, rHand };
            if (needed.Any(t => t == null)) return path + ": missing humanoid bones";

            // clear any previous ragdoll on the bones (the root's own capsule stays)
            foreach (var j in animator.GetComponentsInChildren<CharacterJoint>(true)) Object.DestroyImmediate(j);
            foreach (var c in animator.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in animator.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);

            // the body's axes in its bind pose (T-pose), as RagdollBuilder.CalculateAxes
            var up = (head.position - hips.position).normalized;
            var right = (rArm.position - lArm.position); right -= Vector3.Dot(right, up) * up; right.Normalize();
            var forward = Vector3.Cross(right, up).normalized;

            // Each twist axis is chosen so that bending the way a man bends (flexion) is a positive turn about it
            // (Quaternion.AngleAxis): min = how far the other way (extension), max = how far it flexes. Swing limits are
            // symmetric. Ranges are the ordinary human ones (no joint goes the wrong way). If the physics engine counts
            // the twist the other way round, FlexSign flips them all (checked by ClaudeRagdollProbe.JointSigns).
            float k = FlexSign;
            var parts = new List<Part>
            {
                new Part { bone = hips, mass = 11f },
                // hip: flex 80 (leg forward), extend 20; abduct 35; turn of the leg 20. Not 90: lying on his back a straight leg balanced upright on that stop and stayed raised (filmed 2026-11-26)
                new Part { bone = lThigh, parent = hips, mass = 7f, axis = -right * k, normal = forward, min = -20f, max = 80f, swing = 35f, swing2 = 20f },
                new Part { bone = rThigh, parent = hips, mass = 7f, axis = -right * k, normal = forward, min = -20f, max = 80f, swing = 35f, swing2 = 20f },
                // knee: bends back only, 4-120 (a held-straight knee overshoots its stop by a few degrees; 4 keeps the overshoot inside 0-5 the wrong way, 2026-11-26)
                new Part { bone = lShin, parent = lThigh, mass = 4f, axis = right * k, normal = forward, min = 4f, max = 120f },
                new Part { bone = rShin, parent = rThigh, mass = 4f, axis = right * k, normal = forward, min = 4f, max = 120f },
                // spine: bend forward 35, back 20; to the side 15; turn 20
                new Part { bone = chest, parent = hips, mass = 22f, axis = right * k, normal = forward, min = -20f, max = 35f, swing = 15f, swing2 = 20f },
                // head with the neck: nod 45, back 40; to the side 30; turn 50
                new Part { bone = head, parent = chest, mass = 5.5f, axis = right * k, normal = forward, min = -40f, max = 45f, swing = 30f, swing2 = 50f },
                // shoulder from the T-pose: turn of the arm 40; 50 every way (the air test with 85/60 let the arm swing 70-84 degrees behind the body, past a man's 45-60; the two swing limits mix on the arm, 2026-11-26)
                new Part { bone = lArm, parent = chest, mass = 2.2f, axis = -right, normal = forward, min = -40f, max = 40f, swing = 50f, swing2 = 50f },
                new Part { bone = rArm, parent = chest, mass = 2.2f, axis = right, normal = forward, min = -40f, max = 40f, swing = 50f, swing2 = 50f },
                // elbow: bends forward only, 5-130 (the overshoot stays inside 10 the wrong way; the arms are mirrored, so the axes are)
                new Part { bone = lFore, parent = lArm, mass = 1.6f, axis = up * k, normal = forward, min = 5f, max = 130f },
                new Part { bone = rFore, parent = rArm, mass = 1.6f, axis = -up * k, normal = forward, min = 5f, max = 130f },
            };

            // colliders
            Box(hips, new[] { hips.position, lThigh.position, rThigh.position, spine.position }, Depth, material);
            Box(chest, new[] { chest.position, neck.position, lArm.position, rArm.position, spine.position + (chest.position - spine.position) * 0.5f }, Depth, material);
            var sphere = head.gameObject.AddComponent<SphereCollider>();
            float s = head.lossyScale.x;
            sphere.radius = HeadRadius / s;
            sphere.center = head.InverseTransformPoint(head.position + up * 0.08f + forward * 0.02f);
            sphere.sharedMaterial = material;
            Capsule(lThigh, lShin.position, ThighRadius, material);
            Capsule(rThigh, rShin.position, ThighRadius, material);
            Capsule(lShin, lToes != null ? Vector3.Lerp(lFoot.position, lToes.position, 0.5f) : lFoot.position, ShinRadius, material);
            Capsule(rShin, rToes != null ? Vector3.Lerp(rFoot.position, rToes.position, 0.5f) : rFoot.position, ShinRadius, material);
            Capsule(lArm, lFore.position, UpperArmRadius, material);
            Capsule(rArm, rFore.position, UpperArmRadius, material);
            Capsule(lFore, lHand.position + (lHand.position - lFore.position) * 0.1f, ForearmRadius, material);
            Capsule(rFore, rHand.position + (rHand.position - rFore.position) * 0.1f, ForearmRadius, material);

            // bodies
            foreach (var p in parts)
            {
                var rb = p.bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = p.mass;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.maxDepenetrationVelocity = 2f;
                rb.solverIterations = 20;
                rb.solverVelocityIterations = 8;
                rb.linearDamping = 0.05f;
                rb.angularDamping = config.spinDamping;
                foreach (var c in p.bone.GetComponents<Collider>()) c.enabled = false;
                p.bone.gameObject.layer = FiveRivers.Core.WorldQuery.RagdollLayer; // bones carry no renderer: only the colliders move layer
            }
            // joints
            var limits = new StringBuilder();
            foreach (var p in parts.Where(x => x.parent != null))
            {
                var j = p.bone.gameObject.AddComponent<CharacterJoint>();
                j.connectedBody = p.parent.GetComponent<Rigidbody>();
                j.anchor = Vector3.zero;
                j.axis = Snap(p.bone.InverseTransformDirection(p.axis));
                j.swingAxis = Snap(p.bone.InverseTransformDirection(p.normal));
                j.lowTwistLimit = new SoftJointLimit { limit = p.min };
                j.highTwistLimit = new SoftJointLimit { limit = p.max };
                j.swing1Limit = new SoftJointLimit { limit = p.swing };
                j.swing2Limit = new SoftJointLimit { limit = p.swing2 };
                j.enableProjection = true; // off was tried (2026-11-26): the elbows then flailed past their limits and the shins still sank
                j.projectionDistance = 0.02f;
                j.projectionAngle = 5f; // 1 for the hinges was tried: the projection kept snapping them and they flailed the wrong way (2026-11-26)
                j.enablePreprocessing = false;
                limits.Append($"{p.bone.name.Replace("mixamorig:", "")}[{p.min:0}~{p.max:0}/{p.swing:0}/{p.swing2:0} axis {j.axis} swing {j.swingAxis}] ");
            }
            var bodies = animator.GetComponentsInChildren<Rigidbody>(true);
            var ragdoll = root.GetComponent<EnemyRagdoll>() ?? root.AddComponent<EnemyRagdoll>();
            ragdoll.Configure(config, animator, bodies);
            EditorUtility.SetDirty(ragdoll);
            return $"{path}: bodies {bodies.Length}, mass {bodies.Sum(b => b.mass):0.0} kg, joints: {limits}";
        }

        // RagdollBuilder.CalculateDirectionAxis: the bone's own axis nearest the wanted one
        static Vector3 Snap(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            if (x >= y && x >= z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (y >= z) return new Vector3(0f, Mathf.Sign(v.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        static void Capsule(Transform bone, Vector3 end, float radius, PhysicsMaterial material)
        {
            var local = bone.InverseTransformPoint(end);
            int dir = Mathf.Abs(local.x) >= Mathf.Abs(local.y) && Mathf.Abs(local.x) >= Mathf.Abs(local.z) ? 0 : Mathf.Abs(local.y) >= Mathf.Abs(local.z) ? 1 : 2;
            float length = local[dir];
            float r = radius / bone.lossyScale.x;
            var c = bone.gameObject.AddComponent<CapsuleCollider>();
            c.direction = dir;
            var centre = Vector3.zero; centre[dir] = length * 0.5f;
            c.center = centre;
            c.radius = r;
            c.height = Mathf.Abs(length) + 2f * r * 0.6f; // round ends reach a little past the joints
            c.sharedMaterial = material;
        }

        static void Box(Transform bone, Vector3[] worldPoints, float depth, PhysicsMaterial material)
        {
            var b = new Bounds(bone.InverseTransformPoint(worldPoints[0]), Vector3.zero);
            foreach (var p in worldPoints) b.Encapsulate(bone.InverseTransformPoint(p));
            float thick = depth / bone.lossyScale.x;
            var size = b.size;
            for (int i = 0; i < 3; i++) size[i] = Mathf.Max(size[i], thick);
            var box = bone.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = size;
            box.sharedMaterial = material;
        }
    }
}
