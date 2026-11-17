using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.EditorTools
{
    // 2026-11-16 (user: "참호에 있는 탄약상자들이 너무 대충 만든 것 같은데, 열어 둔 상태에서 탄약상자에 탄약이 예쁘게 들어
    // 있는 모습으로 만들어봐"). The trench's ammunition boxes become M1917 packing boxes of .30 cal M2 ball in 8-round M1
    // clips packed in six-pocket cotton bandoleers (models: source/props/ammo_crate_m1917, background Blender):
    //   gameplay crates (PF_PR_AmmoCrate, F resupply): one opened box standing on the floor; the back bandoleer of its
    //   top layer has its flaps back and its clips half out toward the trench, the lid leans on the box's end, a
    //   bandoleer lies in front. Inside the old 0.70 x 0.40 m footprint, no new colliders (the prefab's BoxCollider
    //   and the NavMesh stay as they were).
    //   the decorative pile in the trench: closed boxes lying on their backs, an opened one beside; the old block
    //   hidden (its colliders kept).
    // Re-running rebuilds the same result.
    //
    // 2026-11-17 (user: "미국하고 우리나라 원본 기록 찾아서 그대로 만들어놔"): remade against TM 9-1990 Small-Arms
    // Ammunition (War Department, 1942, with C2 1943): par. 23 boxes dark brown (chocolate) with yellow stencils,
    // par. 24 and fig. 12.6 the markings, par. 28 six wing nuts and the metal liner, par. 30 the olive-drab six-pocket
    // bandoleer with webbing strap, safety pin, reference card and one carton with an 8-round clip in each pocket,
    // fig. 13 the opening sequence. The clips go into the pockets bullets first. The supply bay's light is a candle
    // (no record of a lamp in a 1950 ROK trench was found; see source/props/ammo_crate_m1917/README.md).
    public static class AmmoCrateM1917Setup
    {
        const string Dir = "Assets/_Project/Art/Models/Props/AmmoCrateM1917";
        const string TexDir = Dir + "/Textures";
        const string WoodD = "Assets/_Project/Art/Maps/ClaudePolish20261030/Scan/brown_planks_04_diff_2k.jpg";
        const string WoodN = "Assets/_Project/Art/Maps/ClaudePolish20261030/Scan/brown_planks_04_nor_gl_2k.jpg";
        const string ClipFbx = "Assets/_Project/Art/Models/Weapons/M1Garand/SM_WP_M1Garand_Clip.fbx";
        const string CratePrefab = "Assets/_Project/Prefabs/PF_PR_AmmoCrate.prefab";
        const string SetPrefab = Dir + "/PF_PR_M1917_OpenStack.prefab";
        const string DecoPrefab = Dir + "/PF_PR_M1917_DecoPile.prefab";
        const string VisualName = "M1917_Visual";
        const string DecoPath = "Dabudong_Imported/M01_Environment/Trench_Visuals/Ammo_Crates";
        const string GreyboxPath = "Dabudong_Imported/M01_Greybox/Zones/AMMO_01";
        const string LanternPath = "Frontline_Art_20261102/Supply_bay_lantern";
        // box (M1917: 18-7/16 x 9-7/16 x 14-13/16 in) and bandoleer sizes, as built in make_crates.py
        const float L = 0.468f, W = 0.240f, H = 0.376f, LidT = 0.016f, PT = 0.028f;
        static Vector3 modelFront = Vector3.forward;   // the stencilled long side, in the models' own frame

        [MenuItem("FiveRivers/Setup/Ammo Crates M1917 (Open, Bandoleers)")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("편집 모드에서 실행");
            var report = new StringBuilder();
            Textures();
            var mats = Materials();
            foreach (var path in new[] { "SM_PR_M1917_CrateOpen", "SM_PR_M1917_CrateClosed", "SM_PR_M1917_Lid", "SM_PR_Bandoleer_Open", "SM_PR_Bandoleer_Closed", "SM_PR_Bandoleer_Fill" })
                Model($"{Dir}/{path}.fbx", mats);

            var open = Load("SM_PR_M1917_CrateOpen"); var closed = Load("SM_PR_M1917_CrateClosed"); var lid = Load("SM_PR_M1917_Lid");
            var bOpen = Load("SM_PR_Bandoleer_Open"); var bClosed = Load("SM_PR_Bandoleer_Closed"); var fill = Load("SM_PR_Bandoleer_Fill");
            var clip = AssetDatabase.LoadAssetAtPath<GameObject>(ClipFbx);

            // which way the boxes face once imported: the long side carries the stencil (the box's front)
            float yaw = FrontYaw(open, mats["Stencil"], report);
            bool tipPlusZ = ClipTipAlongPlusZ(clip, report);

            // ---- gameplay set (2026-11-17: the two-high stack read as a tower): one opened box standing on the floor,
            //      its lid stood on its short edge leaning on the +X end, a bandoleer dropped in front, lantern spot at -X
            var set = new GameObject(VisualName);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            const float boxX = -0.05f;
            var openRot = Quaternion.Euler(0f, yaw + 3.5f, 0f);
            var openBox = Place(open, set.transform, "BoxOpen", new Vector3(boxX, 0f, 0f), openRot);
            Pack(openBox.transform, bOpen, bClosed, fill, clip, yaw + 3.5f, tipPlusZ, report);
            var lidGo = Place(lid, set.transform, "LidLeaning", Vector3.zero, Quaternion.identity);
            LeanOnEnd(lidGo.transform, rot, boxX + L / 2f, 3f);
            // no bandoleer on the floor in front: lying flat it read as a wooden board (user, 2026-11-17)
            var spot = new GameObject("LanternSpot").transform;
            spot.SetParent(set.transform, false);
            spot.localPosition = new Vector3(boxX - L / 2f - 0.12f, 0f, 0.04f);
            SaveSet(set, SetPrefab);

            // ---- decorative pile: closed boxes standing upright as they are carried and stacked (lid and wing nuts up,
            //      markings readable on the side and ends), two side by side and a third on them, an opened box beside ----
            var pile = new GameObject("M1917_DecoPile");
            var box0 = Place(closed, pile.transform, "Box0", new Vector3(-0.68f, 0f, 0.02f), rot);
            Place(closed, pile.transform, "Box1", new Vector3(-0.19f, 0f, 0.035f), Quaternion.Euler(0f, yaw - 2.5f, 0f));
            Place(closed, pile.transform, "Box2", new Vector3(-0.43f, Bounds(box0.transform).max.y, 0.02f), Quaternion.Euler(0f, yaw + 4f, 0f));
            var deco = Place(open, pile.transform, "BoxOpen", new Vector3(0.44f, 0f, 0.03f), Quaternion.Euler(0f, yaw - 5f, 0f));
            Pack(deco.transform, bOpen, bClosed, fill, clip, yaw - 5f, tipPlusZ, report);
            var lid2 = Place(lid, pile.transform, "Lid", Vector3.zero, Quaternion.identity);
            LeanOnEnd(lid2.transform, rot, 0.44f + L / 2f, -4f);
            SaveSet(pile, DecoPrefab);

            // ---- the gameplay crate prefab shows the set; its BoxCollider, AmmoCrate and position are untouched ---------
            var root = PrefabUtility.LoadPrefabContents(CratePrefab);
            try
            {
                foreach (Transform c in root.transform.Cast<Transform>().ToArray())
                {
                    if (c.name == VisualName) Object.DestroyImmediate(c.gameObject);
                    else if (c.name == "Model" || c.name == "Band") c.gameObject.SetActive(false);
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SetPrefab), root.transform);
                inst.name = VisualName;
                inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity;
                PrefabUtility.SaveAsPrefabAsset(root, CratePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            report.AppendLine("PF_PR_AmmoCrate: Model and Band off, " + VisualName + " added");

            // ---- scene: decorative pile, greybox stand-in, lantern ----------------------------------------------------------
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DabudongSetup.ScenePath) scene = EditorSceneManager.OpenScene(DabudongSetup.ScenePath);
            var map = GameObject.Find("/[Map]").transform;
            var decoT = map.Find(DecoPath);
            if (decoT != null)
            {
                foreach (Transform c in decoT.Cast<Transform>().ToArray()) if (c.name == "M1917_DecoPile") Object.DestroyImmediate(c.gameObject);
                var b = Bounds(decoT);
                int hidden = 0;
                foreach (var r in decoT.GetComponentsInChildren<Renderer>(true)) { if (r.enabled) hidden++; r.enabled = false; }
                var pi = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DecoPrefab), decoT);
                pi.name = "M1917_DecoPile";
                // stand on the trench floor (the old block was sunk into it), measured at the pile's footprint
                float floor = b.min.y;
                var terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    floor = float.MinValue;
                    foreach (var dx in new[] { -0.8f, -0.3f, 0.3f, 0.8f })
                        floor = Mathf.Max(floor, terrain.SampleHeight(new Vector3(b.center.x + dx, 0f, b.center.z)) + terrain.transform.position.y);
                }
                pi.transform.position = new Vector3(b.center.x, floor, b.center.z);
                pi.transform.rotation = Quaternion.identity;
                report.AppendLine($"decorative pile at {pi.transform.position.ToString("F2")} (old block {b.size.ToString("F2")}, {hidden} renderers hidden, colliders kept)");
            }
            else report.AppendLine("missing " + DecoPath);
            var grey = map.Find(GreyboxPath);
            if (grey != null) foreach (var r in grey.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; report.AppendLine("greybox AMMO_01 renderer hidden"); }
            var lantern = map.Find(LanternPath);
            var crate0 = map.Find("Generated/Props/GameplayAmmoCrate_0");
            var lspot = crate0 != null ? crate0.Find(VisualName + "/LanternSpot") : null;
            if (lantern != null && lspot != null)
            {
                Candle(lantern, new Vector3(lspot.position.x, crate0.position.y, lspot.position.z), report);
                report.AppendLine($"candle on the floor beside the boxes at {lantern.position.ToString("F2")}");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            System.IO.File.WriteAllText("Temp/ammo_crate_setup.txt", report.ToString());
            return "crates";
        }

        // The supply bay's light (2026-11-17, second pass). The hurricane lantern had no source: no record of a lamp in a
        // 1950 ROK front-line trench was found. The one photograph found of a light in a Korean front-line dugout
        // (Samichon Valley, 1952, koreanwaronline.com/history/Ron/Frame/small-15.htm) shows a candle, so the light is a
        // candle stub (source/props/candle/make_candle.py); the upturned tin under it is our own choice. The scene object
        // keeps its name and its Light; its root is put at the flame and the model stands on the floor below it.
        const string CandleFbx = "Assets/_Project/Art/Models/Props/Candle/SM_PR_CandleOnTin.fbx";
        const float FlameHeight = 0.1315f;
        static void Candle(Transform lantern, Vector3 floorPoint, StringBuilder report)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material M(string name, System.Action<Material> f)
            {
                string path = $"Assets/_Project/Art/Models/Props/Candle/M_PR_Candle_{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit; f(m); EditorUtility.SetDirty(m); return m;
            }
            void Emit(Material m, Color c) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            var mats = new Dictionary<string, Material>
            {
                // a ration tin gone dull, a little of the flame on it
                ["Steel"] = M("Steel", m => { m.SetColor("_BaseColor", new Color(.33f, .34f, .31f)); m.SetFloat("_Metallic", .6f); m.SetFloat("_Smoothness", .38f); Emit(m, new Color(.020f, .011f, .004f)); }),
                // tallow-white wax, the flame glowing a little through it
                ["Wax"] = M("Wax", m => { m.SetColor("_BaseColor", new Color(.80f, .76f, .64f)); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", .46f); Emit(m, new Color(.085f, .045f, .014f)); }),
                ["Wick"] = M("Wick", m => { m.SetColor("_BaseColor", new Color(.05f, .045f, .04f)); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", .1f); }),
                ["Flame"] = M("Flame", m => { m.SetColor("_BaseColor", new Color(1f, .70f, .30f)); Emit(m, new Color(6.0f, 2.6f, .55f)); }),
            };
            if (AssetImporter.GetAtPath(CandleFbx) is ModelImporter mi)
            {
                mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
                mi.importNormals = ModelImporterNormals.Calculate; mi.normalSmoothingAngle = 50f;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var kv in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
                mi.SaveAndReimport();
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CandleFbx);
            if (asset == null) { report.AppendLine("missing " + CandleFbx); return; }
            for (int i = lantern.childCount - 1; i >= 0; i--) Object.DestroyImmediate(lantern.GetChild(i).gameObject);
            foreach (var r in lantern.GetComponents<Renderer>()) r.enabled = false;
            lantern.SetPositionAndRotation(floorPoint + Vector3.up * FlameHeight, Quaternion.identity);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, lantern);
            go.name = "Candle_Model";
            go.transform.position = floorPoint;
            go.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            // The bay light sits in the flame, a few centimetres from the wax and the tin: it lit them to white.
            // Rendering layers: the bay light keeps the world (bit 0), the candle model is on bit 1 only, which the moon
            // and its bounce also light; a weak light of its own gives the wax and the tin the flame's warmth.
            const uint World = 1u, Self = 2u;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.renderingLayerMask = Self; }
            var bay = lantern.GetComponent<Light>();
            if (bay != null) { bay.GetUniversalAdditionalLightData().renderingLayers = World; EditorUtility.SetDirty(bay); }
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { var d = l.GetUniversalAdditionalLightData(); d.renderingLayers |= Self; EditorUtility.SetDirty(d); }
            var selfGo = new GameObject("Candle_SelfLight");
            selfGo.transform.SetParent(lantern, false);
            selfGo.transform.localPosition = new Vector3(0f, .012f, 0f);
            var self = selfGo.AddComponent<Light>();
            self.type = LightType.Point; self.shadows = LightShadows.None; self.range = .30f; self.intensity = .004f;
            self.color = new Color(1f, .62f, .30f);
            self.GetUniversalAdditionalLightData().renderingLayers = Self;
            var b = Bounds(go.transform);
            report.AppendLine($"candle model {b.size.ToString("F3")} base at {b.min.y:0.000} (floor {floorPoint.y:0.000})");
        }

        // ---------------------------------------------------------------------------------------------------------------
        static void Textures()
        {
            void Set(string path, System.Action<TextureImporter> f)
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter ti) { f(ti); ti.SaveAndReimport(); }
            }
            Set(TexDir + "/T_PR_M1917_Stencil.png", ti => { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.maxTextureSize = 4096; ti.anisoLevel = 12; ti.mipMapsPreserveCoverage = false; ti.mipMapBias = -.5f; ti.filterMode = FilterMode.Trilinear; });
            Set(TexDir + "/T_PR_Bandoleer_BaseColor.png", ti => { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; });
            Set(TexDir + "/T_PR_Bandoleer_Normal.png", ti => { ti.textureType = TextureImporterType.NormalMap; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; });
            if (AssetImporter.GetAtPath(WoodN) is TextureImporter wn && wn.textureType != TextureImporterType.NormalMap) { wn.textureType = TextureImporterType.NormalMap; wn.SaveAndReimport(); }
        }

        static Dictionary<string, Material> Materials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material M(string name, System.Action<Material> f)
            {
                string path = $"{Dir}/M_AC_{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                f(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            Texture2D T(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            var d = new Dictionary<string, Material>();
            // TM 9-1990 par. 23 (C2): "painted or stained dark brown (chocolate)" -- the plank scan stained down to it
            d["Wood"] = M("Wood", m => { m.SetTexture("_BaseMap", T(WoodD)); m.SetColor("_BaseColor", new Color(.86f, .60f, .43f)); m.SetTexture("_BumpMap", T(WoodN)); m.SetFloat("_BumpScale", .8f); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Smoothness", .16f); m.SetFloat("_Metallic", 0f); });
            // the cut ends of the fingers at the box's corners take the stain darker
            d["EndGrain"] = M("EndGrain", m => { m.SetTexture("_BaseMap", T(WoodD)); m.SetColor("_BaseColor", new Color(.62f, .42f, .30f)); m.SetFloat("_Smoothness", .10f); m.SetFloat("_Metallic", 0f); });
            // the liner (terne or zinc sheet, dulled) and the chipboard of the cartons and the reference card
            // 2026-11-17: at metallic .30 / smoothness .22 the sheet mirrored the night sky and turned blue up close. A
            // dulled, oxidised terne sheet: barely metallic, rough, a warm grey, and no sky-probe reflection (the candle
            // and the moon still light it and give it a soft highlight).
            d["Zinc"] = M("Zinc", m =>
            {
                m.SetTexture("_BaseMap", null); m.SetColor("_BaseColor", new Color(.215f, .185f, .140f));
                m.SetFloat("_Metallic", .08f); m.SetFloat("_Smoothness", .14f); m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;
                m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            });
            d["Card"] = M("Card", m => { m.SetTexture("_BaseMap", null); m.SetColor("_BaseColor", new Color(.60f, .50f, .35f)); m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", .12f); m.SetFloat("_Cull", 0f); });
            d["Iron"] = M("Iron", m => { m.SetTexture("_BaseMap", null); m.SetColor("_BaseColor", new Color(.17f, .17f, .16f)); m.SetFloat("_Metallic", .8f); m.SetFloat("_Smoothness", .32f); });
            d["Brass"] = M("Brass", m => { m.SetTexture("_BaseMap", null); m.SetColor("_BaseColor", new Color(.64f, .48f, .24f)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", .5f); });
            d["Cloth"] = M("Cloth", m => { m.SetTexture("_BaseMap", T(TexDir + "/T_PR_Bandoleer_BaseColor.png")); m.SetColor("_BaseColor", Color.white); m.SetTexture("_BumpMap", T(TexDir + "/T_PR_Bandoleer_Normal.png")); m.SetFloat("_BumpScale", 1.15f); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Smoothness", .07f); m.SetFloat("_Metallic", 0f); });
            // two more dye lots of the same cotton (2026-11-17): a sun-faded pocket and a darker, greener one
            void Lot(string name, Color tint) => d[name] = M(name, m => { m.CopyPropertiesFromMaterial(d["Cloth"]); m.SetColor("_BaseColor", tint); m.EnableKeyword("_NORMALMAP"); });
            Lot("ClothB", new Color(1.12f, 1.07f, .96f));
            Lot("ClothC", new Color(.84f, .88f, .78f));
            d["ClothDark"] = M("ClothDark", m => { m.SetTexture("_BaseMap", T(TexDir + "/T_PR_Bandoleer_BaseColor.png")); m.SetColor("_BaseColor", new Color(.5f, .5f, .5f)); m.SetFloat("_Smoothness", .05f); m.SetFloat("_Metallic", 0f); });
            d["Stencil"] = M("Stencil", m =>
            {
                m.SetTexture("_BaseMap", T(TexDir + "/T_PR_M1917_Stencil.png")); m.SetColor("_BaseColor", Color.white);
                // 2026-11-17: alpha-tested, the thin letters dropped out below the cut-off a few metres away (whole side
                // blank from standing height). Painted lettering is a blended decal: it thins with distance instead.
                m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f); m.SetOverrideTag("RenderType", "Transparent");
                m.SetShaderPassEnabled("DepthOnly", false); m.SetShaderPassEnabled("ShadowCaster", false);
                m.SetFloat("_Smoothness", .12f); m.SetFloat("_Metallic", 0f);
                m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            });
            return d;
        }

        static void Model(string path, Dictionary<string, Material> mats)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            mi.importAnimation = false; mi.importCameras = false; mi.importLights = false;
            mi.importNormals = ModelImporterNormals.Calculate; mi.normalSmoothingAngle = 35f;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();
        }

        static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{name}.fbx");

        static GameObject Place(GameObject asset, Transform parent, string name, Vector3 localPos, Quaternion localRot)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.localPosition = localPos; go.transform.localRotation = localRot;
            return go;
        }

        // yaw that turns the stencilled long side of the box toward +Z with the length along X
        static float FrontYaw(GameObject open, Material stencil, StringBuilder report)
        {
            var mf = open.GetComponentsInChildren<MeshFilter>(true).First();
            var mr = mf.GetComponent<MeshRenderer>();
            int sub = System.Array.FindIndex(mr.sharedMaterials, m => m == stencil);
            var mesh = mf.sharedMesh; var v = mesh.vertices; var tr = mf.transform;
            Vector3 acc = Vector3.zero;
            var tris = mesh.GetTriangles(Mathf.Max(0, sub));
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = tr.TransformPoint(v[tris[i]]), b = tr.TransformPoint(v[tris[i + 1]]), c = tr.TransformPoint(v[tris[i + 2]]);
                var n = Vector3.Cross(b - a, c - a); // area-weighted
                if (Mathf.Abs(n.normalized.y) < .5f) acc += n;
            }
            acc.y = 0f;
            // the side stencil dominates; snap to the nearest axis
            var f = Mathf.Abs(acc.x) > Mathf.Abs(acc.z) ? new Vector3(Mathf.Sign(acc.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(acc.z));
            float yaw = Vector3.SignedAngle(f, Vector3.forward, Vector3.up);
            modelFront = f;
            report.AppendLine($"box front (stencil side) faces {f} in the model -> yaw {yaw:0}");
            return yaw;
        }

        // the bullet end of the clip: the tips taper, so that end's cross-section is narrower
        static bool ClipTipAlongPlusZ(GameObject clip, StringBuilder report)
        {
            var mesh = clip.GetComponentsInChildren<MeshFilter>(true).First().sharedMesh;
            var b = mesh.bounds; float slice = b.size.z * .06f;
            float SpreadAt(bool plus)
            {
                float zEdge = plus ? b.max.z : b.min.z; float m = 0f;
                foreach (var p in mesh.vertices) if (Mathf.Abs(p.z - zEdge) < slice) m = Mathf.Max(m, Mathf.Abs(p.x - b.center.x));
                return m;
            }
            float plus = SpreadAt(true), minus = SpreadAt(false);
            report.AppendLine($"clip: spread at +z {plus:0.0000} / -z {minus:0.0000} -> bullets toward {(plus < minus ? "+z" : "-z")}");
            return plus < minus;
        }

        // bandoleers inside an opened box: packed layers, then a top layer of two full ones with their open pocket
        // mouths toward the front (the trench), the cartons and the steel backs of the clips showing; one being emptied
        // lies on top of the back one, each clip half drawn from its pocket. A clip goes into its pocket bullets first
        // (TM 9-1990 fig. 13 C; the bandoleer photographs), so what comes out is the clip's back and the case heads.
        static void Pack(Transform box, GameObject bOpen, GameObject bClosed, GameObject fill, GameObject clip, float yaw, bool tipPlusZ, StringBuilder report)
        {
            float layerTop = H - 0.013f - PT;                 // the top layer lies just under the rim
            var f = Place(fill, box, "Packed", Vector3.zero, Quaternion.identity);
            var fb = Bounds(f.transform);
            float fillTop = fb.max.y - box.position.y;
            f.transform.localScale = new Vector3(1f, layerTop / Mathf.Max(.05f, fillTop), 1f);
            // the bandoleer models have their pocket mouths toward -modelFront; half a turn sends them to the front
            var turn = Quaternion.AngleAxis(180f, Vector3.up);
            var near = Place(bClosed, box, "BandoleerFront", modelFront * 0.0515f + Vector3.up * (layerTop + PT / 2f), turn);
            var under = Place(bClosed, box, "BandoleerBack", -modelFront * 0.0515f + Vector3.up * (layerTop + PT / 2f), Quaternion.AngleAxis(1.5f, Vector3.up) * turn);
            // the opened one rests on the back one (cloth gives 3 mm), mouths just past the gap so its flaps lie on the near one
            float rest = Bounds(under.transform).max.y - box.position.y - 0.003f;
            var back = Place(bOpen, box, "BandoleerOpen", -modelFront * 0.038f + Vector3.up * (rest + PT / 2f), Quaternion.AngleAxis(-1.2f, Vector3.up) * turn);
            float clearY = Bounds(near.transform).max.y + 0.003f;    // over the near bandoleer and its folded strap

            var tipW = back.transform.TransformDirection(-modelFront); tipW.y = 0f; tipW.Normalize();   // out of its mouths
            var axis = Vector3.Cross(Vector3.up, tipW).normalized;
            var mesh = clip.GetComponentsInChildren<MeshFilter>(true).First().sharedMesh;
            var mb = mesh.bounds; var verts = mesh.vertices;
            float clipLen = mb.size.z;
            var tipBottom = new Vector3(mb.min.x, mb.center.y, tipPlusZ ? mb.max.z : mb.min.z);    // the bullet end, inside the pocket; x ends up pointing up
            var slots = back.GetComponentsInChildren<Transform>(true).Where(t => t.name.EndsWith("_ClipSlot")).OrderBy(t => t.name).ToArray();
            var rng = new System.Random(48);
            var lifts = new List<string>();
            int k = 0;
            foreach (var s in slots)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(clip, back.transform);
                c.name = "Clip" + k;
                var mf = c.GetComponentInChildren<MeshFilter>(true).transform;
                var rel = Quaternion.Inverse(c.transform.rotation) * mf.rotation;
                // lying flat (its two-round thickness up), bullets into the pocket, the clip's back out of the mouth
                var zDir = tipPlusZ ? -tipW : tipW;
                var flat = Quaternion.LookRotation(zDir, axis);
                if ((flat * Vector3.right).y < 0f) flat = Quaternion.LookRotation(zDir, -axis);
                var mouth = s.position + tipW * 0.02f;                                          // the slot sits 2 cm inside
                float outLen = .046f + (float)rng.NextDouble() * .012f;                          // how much of it is out
                var baseAt = mouth - tipW * (clipLen - outLen); baseAt.y = s.position.y - 0.012f; // bullet tips on the pocket floor
                float lift = 0f;
                for (lift = 1f; lift <= 40f; lift += .5f)
                {
                    var rm = Quaternion.AngleAxis(-lift, axis) * flat;
                    c.transform.rotation = rm * Quaternion.Inverse(rel);
                    c.transform.position += baseAt - mf.TransformPoint(tipBottom);
                    float minY = float.MaxValue;
                    foreach (var v in verts)
                    {
                        var p = mf.TransformPoint(v);
                        if (Vector3.Dot(p - mouth, tipW) > 0.012f) minY = Mathf.Min(minY, p.y);
                    }
                    if (minY >= clearY) break;
                }
                lifts.Add(lift.ToString("0.0"));
                k++;
            }
            report.AppendLine($"{box.name}: {k} clips half drawn (bullets inside) from the open bandoleer, over the near one (lift {string.Join("/", lifts)} deg); top layer at {layerTop:0.000} m");
        }

        // a leaning lid: lowest point on the floor, centred on at.x, its back (top edge) touching the box face at at.z
        static void Lean(Transform t, Vector3 at)
        {
            var b = Bounds(t);
            var parent = t.parent;
            var worldAt = parent.TransformPoint(at);
            t.position += new Vector3(worldAt.x - b.center.x, parent.position.y - b.min.y, worldAt.z - b.min.z);
        }

        // a lid stood on its short edge on the floor, leaning 24 degrees onto the +X end of an opened box
        static void LeanOnEnd(Transform t, Quaternion rot, float boxEndX, float twist)
        {
            t.localRotation = Quaternion.Euler(0f, twist, 0f) * Quaternion.Euler(0f, 0f, -66f) * rot;
            var b = Bounds(t);
            var parent = t.parent;
            t.position += new Vector3(parent.position.x + boxEndX + 0.004f - b.min.x, parent.position.y - b.min.y, parent.position.z - b.center.z);
        }

        static void SaveSet(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
