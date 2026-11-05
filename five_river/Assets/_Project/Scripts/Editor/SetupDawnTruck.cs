using System;
using System.Collections.Generic;
using System.IO;
using FiveRivers.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Review outside Assets first. Import into an Editor folder only after truck visual approval.
    public static class SetupDawnTruck
    {
        const string Folder = "Assets/_Project/Art/DawnTruck20261102";
        const string PrefabPath = Folder + "/PF_DawnTruck.prefab";
        const string ModelPath = Folder + "/GMC_Truck_Open.fbx";

        [MenuItem("FiveRivers/Setup/Build Dawn Truck Prefab")]
        public static void BuildPrefab()
        {
            EnsureEditMode();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Missing reviewed truck: " + ModelPath);
            var truckMaterial = TruckMaterial();
            // 11-05 open stake-side cargo bed (Docs/Art/DawnTruck_20261105): the FBX "Wood" slot gets painted wood.
            var woodMaterial = MaterialAsset(Folder + "/M_DawnTruck_Wood.mat", Color.white, 0f, .12f);
            woodMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/T_DawnTruck_Wood.png"));
            var rampMaterial = MaterialAsset(Folder + "/M_DawnTruck_Ramp.mat", new Color(.55f, .55f, .48f), .22f, .15f);
            rampMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/T_GMC_Truck_BaseColor.jpg"));
            rampMaterial.SetTextureScale("_BaseMap", new Vector2(.16f, .02f));
            rampMaterial.SetTextureOffset("_BaseMap", new Vector2(.015f, .06f));
            var root = new GameObject("PF_DawnTruck");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "TruckVisual";
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = materials[i] != null && materials[i].name.StartsWith("Wood", StringComparison.Ordinal)
                            ? woodMaterial : truckMaterial;
                    renderer.sharedMaterials = materials;
                }
                var sequence = root.AddComponent<DawnTruckSequence>();
                sequence.approachDuration = .6f;
                sequence.stopSettle = .6f;
                sequence.departureInterval = .9f;
                sequence.wheelRadius = .48f;
                var sockets = Child(root.transform, "PassengerSockets");
                sequence.passengerSockets = new Transform[4];
                float[] z = { -2.5f, -1.65f, -.8f, .05f };
                for (int i = 0; i < z.Length; i++)
                {
                    var socket = Point(sockets, "Passenger_" + (i + 1), new Vector3(0f, 1.38f, z[i]));
                    socket.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    sequence.passengerSockets[i] = socket;
                }
                var exit = Child(root.transform, "ExitPath");
                sequence.exitPath = new[]
                {
                    Point(exit, "RearAisle", new Vector3(0f, 1.38f, -2.9f)),
                    Point(exit, "RampLip", new Vector3(0f, 1.38f, -3.3f)),
                    Point(exit, "RampFoot", new Vector3(0f, 0f, -6.9f)),
                };
                // These local points run around the truck's right side, outside its 1.5 m half-width.
                // Root can shorten/change the prefab join route for the final verified arrival orientation.
                var join = Child(root.transform, "JoinPath");
                sequence.joinPath = new[] { Point(join, "RearClear", new Vector3(2f, 0f, -7f)) };
                sequence.exitRamp = BuildRamp(root.transform, rampMaterial);
                sequence.exitRamp.SetActive(false);
                var wheels = new List<Transform>();
                foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Wheel_", StringComparison.Ordinal)
                        && (t.parent == null || !t.parent.name.StartsWith("Wheel_", StringComparison.Ordinal))) wheels.Add(t);
                sequence.wheelPivots = wheels.ToArray();
                var engine = root.AddComponent<AudioSource>();
                engine.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Supplemental/SFX/SFX_GMC_Engine_Loop.wav");
                engine.playOnAwake = false;
                engine.loop = true;
                engine.spatialBlend = 1f;
                engine.rolloffMode = AudioRolloffMode.Linear;
                engine.minDistance = 3f;
                engine.maxDistance = 55f;
                engine.volume = .45f;
                sequence.engineSource = engine;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[DawnTruck] Prefab ready: open stake-side bed, rear ramp slope 21 degrees; 4 original-student sockets. Scene route not changed.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // Intentionally no default scene-route menu: supply positions after checking the actual player view.
        public static void ConnectScene(Vector3 start, Vector3 stop, float yaw)
        {
            EnsureEditMode();
            var dawn = Object.FindFirstObjectByType<DawnDirector>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (dawn == null || prefab == null) throw new InvalidOperationException("DawnDirector or built truck prefab missing.");
            var route = dawn.transform.Find("DawnTruckRoute");
            if (route == null) route = Child(dawn.transform, "DawnTruckRoute");
            var begin = route.Find("ArrivalStart");
            if (begin == null) begin = Child(route, "ArrivalStart");
            var end = route.Find("ArrivalStop");
            if (end == null) end = Child(route, "ArrivalStop");
            Ground(ref start); Ground(ref stop);
            begin.SetPositionAndRotation(start, Quaternion.Euler(0f, yaw, 0f));
            end.SetPositionAndRotation(stop, Quaternion.Euler(0f, yaw, 0f));
            var serialized = new SerializedObject(dawn);
            serialized.FindProperty("truckPrefab").objectReferenceValue = prefab.GetComponent<DawnTruckSequence>();
            serialized.FindProperty("truckArrivalStart").objectReferenceValue = begin;
            serialized.FindProperty("truckArrivalStop").objectReferenceValue = end;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dawn);
            EditorSceneManager.MarkSceneDirty(dawn.gameObject.scene);
            // Caller visually verifies, then explicitly saves the scene.
        }

        static Material TruckMaterial()
        {
            var material = MaterialAsset(Folder + "/M_DawnTruck.mat", Color.white, .1f, .28f);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/T_GMC_Truck_BaseColor.jpg"));
            string normalPath = Folder + "/T_GMC_Truck_Normal.jpg";
            var importer = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normal != null) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MaterialAsset(string path, Color color, float metallic, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject BuildRamp(Transform parent, Material material)
        {
            var ramp = Child(parent, "RearRamp");
            Vector3 top = new Vector3(0f, 1.38f, -3.3f), foot = new Vector3(0f, 0f, -6.9f);
            var mesh = new Mesh { name = "DawnTruck_RearRamp" };
            float half = .45f, thickness = .06f;
            mesh.vertices = new[]
            {
                top + Vector3.left * half, top + Vector3.right * half,
                foot + Vector3.left * half, foot + Vector3.right * half,
                top + Vector3.left * half - Vector3.up * thickness, top + Vector3.right * half - Vector3.up * thickness,
                foot + Vector3.left * half - Vector3.up * thickness, foot + Vector3.right * half - Vector3.up * thickness
            };
            mesh.triangles = new[] { 0,1,2, 2,1,3, 4,6,5, 6,7,5, 0,4,1, 1,4,5, 2,3,6, 3,7,6, 0,2,4, 2,6,4, 1,5,3, 3,5,7 };
            mesh.uv = new[] { new Vector2(0,0),new Vector2(1,0),new Vector2(0,4),new Vector2(1,4),new Vector2(0,0),new Vector2(1,0),new Vector2(0,4),new Vector2(1,4) };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            const string meshPath = Folder + "/DawnTruck_RearRamp.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
            ramp.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ramp.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            ramp.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            for (int i = 1; i < 14; i++)
            {
                var slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slat.name = "GripSlat_" + i;
                slat.transform.SetParent(ramp, false);
                slat.transform.localPosition = Vector3.Lerp(top, foot, i / 14f) + Vector3.up * .009f;
                slat.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(1.38f, 3.6f) * Mathf.Rad2Deg, 0f, 0f);
                slat.transform.localScale = new Vector3(.86f, .018f, .035f);
                slat.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(slat.GetComponent<Collider>());
            }
            return ramp.gameObject;
        }

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
        static Transform Point(Transform parent, string name, Vector3 local)
        {
            var point = Child(parent, name); point.localPosition = local; return point;
        }
        static void Ground(ref Vector3 point)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null) point.y = terrain.SampleHeight(point) + terrain.transform.position.y + .008f;
        }
        static void EnsureEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before dawn truck setup.");
        }
    }
}


