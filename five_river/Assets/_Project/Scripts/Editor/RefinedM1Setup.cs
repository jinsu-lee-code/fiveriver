using System;
using System.Linq;
using FiveRivers.Combat;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Uses the user-corrected M1_Reviewed weapon (same prefab as the allies) with the reviewed grip.
    // The art-branch FBX is kept only for the loaded/empty clip props.
    public static class RefinedM1Setup
    {
        const string Weapon = "Assets/_Project/Art/Models/Weapons/M1Garand/";
        const string Reviewed = "Assets/_Project/Art/Weapons/M1Refined/Prefabs/M1_Reviewed.prefab";
        const string Arms = "Assets/_Project/Art/Models/Characters/ROK_Arms/";
        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(Reviewed) != null;

        [MenuItem("FiveRivers/Setup/Apply Refined M1")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null) throw new InvalidOperationException("Open the Dabudong gameplay scene.");
            Configure(player);
            FirstPersonAnimationSetup.Configure(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        public static void Configure(PlayerController player)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Reviewed);
            if (prefab == null) throw new InvalidOperationException("User-corrected M1_Reviewed is missing.");
            var rifle = player.GetComponent<M1Rifle>();
            var data = new SerializedObject(rifle);
            var holder = data.FindProperty("model").objectReferenceValue as Transform;
            if (holder == null) throw new InvalidOperationException("M1 viewmodel holder is missing.");
            var previousChildren = holder.Cast<Transform>().ToArray();
            var presentationRoot = new GameObject("ArtM1_Presentation").transform;
            presentationRoot.SetParent(holder, false);
            try
            {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, presentationRoot);
            visual.name = "ArtM1";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            visual.transform.localScale = Vector3.one;
            // M1MeshRefinement targets the art FBX aperture; the reviewed prefab is already corrected.
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var boltMotion = PrepareReviewedVisual(visual);
            var pose = JObject.Parse(System.IO.File.ReadAllText(Arms + "ROK_Arms_Poses.json"));
            var grip = pose["grips"].First(p => (string)p["id"] == "M1");
            var weaponMatrix = Matrix(grip["weaponToCamera"]);
            var armsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Weapons/M1Refined/Prefabs/M1_ViewArms.prefab");
            if (armsPrefab == null) throw new InvalidOperationException("Art delivery arms are missing.");
            var arms = (GameObject)PrefabUtility.InstantiatePrefab(armsPrefab, presentationRoot);
            PrefabUtility.UnpackPrefabInstance(arms, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            arms.name = "ArtM1_ViewArms";
            foreach (var animator in arms.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var bones = arms.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            // Keep the user's corrected 40-bone hand pose and skin; only the weapon uses art delivery.
            // Roll the support grip around the handguard so fingertips clear the real sight line.
            var support = bones["wrist.L"];
            var supportPivot = new Vector3(0f, 0.025f, 0f);
            var supportRoll = Quaternion.Euler(0f, 0f, 18f);
            support.localPosition = supportPivot + supportRoll * (support.localPosition - supportPivot);
            support.localRotation = supportRoll * support.localRotation;            // Close the ring and little fingers around the stock instead of leaving them upright.
            void Curl(string name,float degrees){var finger=bones[name];finger.localRotation*=Quaternion.Euler(degrees,0f,0f);}
            Curl("finger3-2.L",10f);Curl("finger3-3.L",10f);
            Curl("finger4-2.L",35f);Curl("finger4-3.L",25f);
            Curl("finger5-2.L",45f);Curl("finger5-3.L",35f);
            // The reviewed stock wrist sits higher than the art FBX pistol-grip line; raise the trigger hand 27mm
            // so the fingers wrap the wrist and the thumb crosses its top.
            bones["wrist.R"].position += presentationRoot.TransformVector(new Vector3(0f, 0.027f, 0f));
            // Support fingertips otherwise reach the ADS eye line (y~.079) beside the front sight.
            bones["wrist.L"].position += presentationRoot.TransformVector(new Vector3(0f, -0.008f, 0f));
            foreach (var r in presentationRoot.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                if (r is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            var hip = new Vector3(0.15f, -0.17f, 0.18f);
            data.FindProperty("hipPosition").vector3Value = hip;
            // Eye on the line from the reviewed peep-hole centre (y~.0797 at z=-.021) to the front post top (y=.0821); eye relief 94mm.
            data.FindProperty("aimPosition").vector3Value = new Vector3(0f, -0.0794f, 0.11454f);
            data.FindProperty("hipEuler").vector3Value = weaponMatrix.rotation.eulerAngles;
            var fill = new GameObject("M1_MoonFill").AddComponent<Light>();
            fill.transform.SetParent(presentationRoot, false);
            fill.transform.localPosition = new Vector3(-0.30f, 0.35f, -0.06f);
            fill.type = LightType.Point;
            fill.intensity = 0.4f;
            fill.range = 1.5f;
            fill.color = new Color(0.92f, 0.94f, 1f);
            fill.shadows = LightShadows.None;
            fill.cullingMask = 1 << 31;
            foreach (var r in presentationRoot.GetComponentsInChildren<Renderer>(true)) r.gameObject.layer = 31;
            // Reviewed Muzzle and Clip_Eject markers.
            data.FindProperty("muzzle").objectReferenceValue = SetSocket(presentationRoot, "Socket_Muzzle", new Vector3(0f, 0.06197f, 0.72401f));
            data.FindProperty("clipEject").objectReferenceValue = SetSocket(presentationRoot, "Socket_ClipEject", new Vector3(0f, 0.06681f, 0.04493f));
            var presentation = presentationRoot.gameObject.AddComponent<M1Presentation>();
            var settings = new SerializedObject(presentation);
            settings.FindProperty("rifle").objectReferenceValue = rifle;
            settings.FindProperty("bolt").objectReferenceValue = boltMotion;
            settings.FindProperty("rightHand").objectReferenceValue = bones["wrist.R"];
            settings.FindProperty("sleeves").objectReferenceValue = arms.GetComponentInChildren<MeshFilter>();
            var loadedClip = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon + "SM_WP_M1Garand_Clip.fbx"), presentationRoot);
            loadedClip.name = "ReloadClip";
            foreach (var r in loadedClip.GetComponentsInChildren<Renderer>()) r.gameObject.layer = 31;
            loadedClip.SetActive(false);
            settings.FindProperty("loadedClip").objectReferenceValue = loadedClip.transform;
            M1FpsRigSetup.Configure(arms, presentationRoot, rifle, settings);
            // Seat the loaded clip in the reviewed receiver well (static clip z centre .0523, top y .0714),
            // 1cm high at p=.66, the last visible clip frame of M1Presentation's reload curve.
            // Reference live-fire loading: palm down onto the clip, fingers forward and across the receiver, straight wrist.
            var hand = bones["wrist.R"];
            var fingerDir = (bones["finger3-1.R"].position - hand.position).normalized;
            var palmDir = Vector3.Cross(fingerDir, bones["finger2-1.R"].position - bones["finger5-1.R"].position).normalized;
            var localPalm = hand.InverseTransformDirection(palmDir);
            var localFingers = hand.InverseTransformDirection(fingerDir);
            var reloadRotation = Quaternion.LookRotation(new Vector3(-0.25f, -0.97f, 0f).normalized, new Vector3(-0.45f, -0.10f, 0.89f).normalized)
                * Quaternion.Inverse(Quaternion.LookRotation(localPalm, localFingers));
            settings.FindProperty("reloadHandRotation").quaternionValue = reloadRotation;
            float seatT = Mathf.SmoothStep(0f, 1f, (0.66f - 0.44f) / 0.24f);
            var seatHand = new Vector3(0.045f, 0.14f - 0.045f * seatT, -0.035f);
            var seat = new Vector3(0f, 0.0714f - 0.049f + 0.01f, 0.0523f - 0.0334f);
            settings.FindProperty("clipGripOffset").vector3Value = Quaternion.Inverse(reloadRotation) * (seat - seatHand);
            var firingRoot = new GameObject("M1_ShotMotion").transform;
            firingRoot.SetParent(presentationRoot, false);
            foreach (var child in presentationRoot.Cast<Transform>().ToArray())
                if (child != firingRoot) child.SetParent(firingRoot, true);
            settings.FindProperty("firingTransform").objectReferenceValue = firingRoot;
            SupportGripSetup.Set(firingRoot.Find("Grip_Left"));
            settings.ApplyModifiedPropertiesWithoutUndo();
            ReplaceEmptyClip();
            data.ApplyModifiedPropertiesWithoutUndo();
            holder.SetLocalPositionAndRotation(hip, weaponMatrix.rotation);
            foreach (var child in previousChildren) Object.DestroyImmediate(child.gameObject);
            }
            catch
            {
                Object.DestroyImmediate(presentationRoot.gameObject);
                throw;
            }
        }

        static Transform PrepareReviewedVisual(GameObject visual)
        {
            // First person always draws LOD0.
            foreach (var group in visual.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                var lod0 = lods[0].renderers;
                for (int i = 1; i < lods.Length; i++)
                    foreach (var r in lods[i].renderers)
                        if (r != null && Array.IndexOf(lod0, r) < 0) r.gameObject.SetActive(false);
                group.SetLODs(new[] { new LOD(0.0001f, lod0) });
                group.fadeMode = LODFadeMode.None;
            }
            // Bolt cover axes are rotated in the source; move them (and the op rod) under an identity wrapper.
            var bolt = new GameObject("BoltMotion").transform;
            bolt.SetParent(visual.transform, false);
            bolt.localPosition = new Vector3(0f, 0.05977f, 0.03158f);
            foreach (var part in visual.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("M1_Cosmetic_Bolt_Cover_") || t.name.StartsWith("SM_M1_OpRod_")).ToArray())
                part.SetParent(bolt, true);
            return bolt;
        }

        static Matrix4x4 Matrix(JToken values)
        {
            var m = new Matrix4x4();
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) m[r,c] = (float)values[r*4+c];
            return m;
        }

        static Transform SetSocket(Transform holder, string name, Vector3 position)
        {
            var socket = holder.Find(name);
            if (socket == null) { socket = new GameObject(name).transform; socket.SetParent(holder, false); }
            socket.SetLocalPositionAndRotation(position, Quaternion.identity);
            return socket;
        }

        static void ReplaceEmptyClip()
        {
            const string path = "Assets/_Project/Prefabs/PF_WP_M1Garand_ClipEmpty.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon + "SM_WP_M1Garand_ClipEmpty.fbx"), root.transform);
                visual.name = "Model";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                if (root.TryGetComponent<BoxCollider>(out var collider))
                {
                    collider.center = new Vector3(0f, 0.02445f, 0f);
                    collider.size = new Vector3(0.02424f, 0.0489f, 0.01635f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
