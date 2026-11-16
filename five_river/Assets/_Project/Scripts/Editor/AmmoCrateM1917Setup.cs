using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-16 (user: "참호에 있는 탄약상자들이 너무 대충 만든 것 같은데, 열어 둔 상태에서 탄약상자에 탄약이 예쁘게 들어
    // 있는 모습으로 만들어봐"). The trench's ammunition boxes become M1917 packing boxes of .30 cal M2 ball in 8-round M1
    // clips packed in six-pocket cotton bandoleers (models: source/props/ammo_crate_m1917, background Blender):
    //   gameplay crates (PF_PR_AmmoCrate, F resupply): a closed box with an opened one on it; in the opened box the back
    //   bandoleer of the top layer has its flaps back and its clips half out toward the trench, the lid leans on the front. Inside the old 0.70 x 0.40 m
    //   footprint, no new colliders (the prefab's BoxCollider and the NavMesh stay as they were).
    //   the decorative pile in the trench: the same boxes, the old block hidden (its colliders kept).
    // Re-running rebuilds the same result.
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

            // ---- gameplay set: closed box, the opened one on it, lid leaning on the +X end, lantern spot at -X ----------
            var set = new GameObject(VisualName);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Place(closed, set.transform, "BoxBelow", new Vector3(0f, 0f, 0f), rot);
            float top = H + LidT + 0.004f;                                        // on the closed box's lid (wing nuts beside)
            var openRot = Quaternion.Euler(0f, yaw + 3.5f, 0f);
            var openBox = Place(open, set.transform, "BoxOpen", new Vector3(-0.012f, top, 0.006f), openRot);
            Pack(openBox.transform, bOpen, bClosed, fill, clip, yaw + 3.5f, tipPlusZ, report);
            // the lid stands on its short edge on the floor against the +X end, leaning 11 degrees onto it
            // the lid stands on its long edge on the floor, leaning back 14 degrees onto the lower box's front, stencil to the trench
            var lidGo = Place(lid, set.transform, "LidLeaning", Vector3.zero, Quaternion.identity);
            lidGo.transform.rotation = Quaternion.Euler(0f, 4f, 0f) * Quaternion.Euler(76f, 0f, 0f) * rot;
            Lean(lidGo.transform, new Vector3(0.01f, 0f, W / 2f + 0.003f));
            var spot = new GameObject("LanternSpot").transform;
            spot.SetParent(set.transform, false);
            spot.localPosition = new Vector3(-L / 2f - 0.11f, 0f, 0.05f);
            SaveSet(set, SetPrefab);

            // ---- decorative pile: two closed boxes, one more on top askew, an opened box beside on the floor --------------
            var pile = new GameObject("M1917_DecoPile");
            Place(closed, pile.transform, "Box0", new Vector3(-0.62f, 0f, 0f), rot);
            Place(closed, pile.transform, "Box1", new Vector3(-0.13f, 0f, 0.01f), Quaternion.Euler(0f, yaw - 2f, 0f));
            Place(closed, pile.transform, "Box2", new Vector3(-0.38f, top, -0.01f), Quaternion.Euler(0f, yaw + 7f, 0f));
            var deco = Place(open, pile.transform, "BoxOpen", new Vector3(0.42f, 0f, 0.03f), Quaternion.Euler(0f, yaw - 5f, 0f));
            Pack(deco.transform, bOpen, bClosed, fill, clip, yaw - 5f, tipPlusZ, report);
            var lid2 = Place(lid, pile.transform, "Lid", Vector3.zero, Quaternion.identity);
            lid2.transform.rotation = Quaternion.Euler(0f, -3f, 0f) * Quaternion.Euler(74f, 0f, 0f) * rot;
            Lean(lid2.transform, new Vector3(-0.13f, 0f, 0.01f + W / 2f + 0.003f));
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
                var lb = Bounds(lantern);
                var floorY = crate0.position.y;
                lantern.position += new Vector3(lspot.position.x - lb.center.x, floorY - lb.min.y, lspot.position.z - lb.center.z);
                report.AppendLine($"lantern on the floor beside the boxes at {lantern.position.ToString("F2")}");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            System.IO.File.WriteAllText("Temp/ammo_crate_setup.txt", report.ToString());
            return "crates";
        }

        // ---------------------------------------------------------------------------------------------------------------
        static void Textures()
        {
            void Set(string path, System.Action<TextureImporter> f)
            {
                if (AssetImporter.GetAtPath(path) is TextureImporter ti) { f(ti); ti.SaveAndReimport(); }
            }
            Set(TexDir + "/T_PR_M1917_Stencil.png", ti => { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.maxTextureSize = 2048; });
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
            // seasoned pine, unpainted, a little paler and warmer than the weathered plank scan
            d["Wood"] = M("Wood", m => { m.SetTexture("_BaseMap", T(WoodD)); m.SetColor("_BaseColor", new Color(.93f, .82f, .66f)); m.SetTexture("_BumpMap", T(WoodN)); m.SetFloat("_BumpScale", .8f); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Smoothness", .16f); m.SetFloat("_Metallic", 0f); });
            d["Iron"] = M("Iron", m => { m.SetTexture("_BaseMap", null); m.SetColor("_BaseColor", new Color(.17f, .17f, .16f)); m.SetFloat("_Metallic", .8f); m.SetFloat("_Smoothness", .32f); });
            d["Brass"] = M("Brass", m => { m.SetTexture("_BaseMap", null); m.SetColor("_BaseColor", new Color(.64f, .48f, .24f)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", .5f); });
            d["Cloth"] = M("Cloth", m => { m.SetTexture("_BaseMap", T(TexDir + "/T_PR_Bandoleer_BaseColor.png")); m.SetColor("_BaseColor", Color.white); m.SetTexture("_BumpMap", T(TexDir + "/T_PR_Bandoleer_Normal.png")); m.SetFloat("_BumpScale", .7f); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_Smoothness", .07f); m.SetFloat("_Metallic", 0f); });
            d["ClothDark"] = M("ClothDark", m => { m.SetTexture("_BaseMap", T(TexDir + "/T_PR_Bandoleer_BaseColor.png")); m.SetColor("_BaseColor", new Color(.5f, .5f, .5f)); m.SetFloat("_Smoothness", .05f); m.SetFloat("_Metallic", 0f); });
            d["Stencil"] = M("Stencil", m =>
            {
                m.SetTexture("_BaseMap", T(TexDir + "/T_PR_M1917_Stencil.png")); m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", .45f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Smoothness", .12f); m.SetFloat("_Metallic", 0f);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
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

        // bandoleers inside an opened box: packed layers, then a top layer of two closed ones with their pocket mouths
        // toward the front (the trench), flaps and snaps showing; an opened one lies on top of the back one, its flaps
        // unsnapped and laid forward and each clip half out of its pocket, lying over the near bandoleer, bullets forward
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
            float clearY = Bounds(near.transform).max.y + 0.0065f;   // over the near bandoleer and the laid-out flaps with their snaps

            var tipW = back.transform.TransformDirection(-modelFront); tipW.y = 0f; tipW.Normalize();   // out of its mouths
            var axis = Vector3.Cross(Vector3.up, tipW).normalized;
            var mesh = clip.GetComponentsInChildren<MeshFilter>(true).First().sharedMesh;
            var mb = mesh.bounds; var verts = mesh.vertices;
            float clipLen = mb.size.z;
            var baseBottom = new Vector3(mb.min.x, mb.center.y, tipPlusZ ? mb.min.z : mb.max.z);   // x ends up pointing up
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
                // lying flat (its two-round thickness up), bullets out of the mouth
                var zDir = tipPlusZ ? tipW : -tipW;
                var flat = Quaternion.LookRotation(zDir, axis);
                if ((flat * Vector3.right).y < 0f) flat = Quaternion.LookRotation(zDir, -axis);
                var mouth = s.position + tipW * 0.02f;                                          // the slot sits 2 cm inside
                float outLen = .046f + (float)rng.NextDouble() * .012f;                          // how much of it is out
                var baseAt = mouth - tipW * (clipLen - outLen); baseAt.y = s.position.y - 0.012f; // base end on the pocket floor
                float lift = 0f;
                for (lift = 1f; lift <= 40f; lift += .5f)
                {
                    var rm = Quaternion.AngleAxis(-lift, axis) * flat;
                    c.transform.rotation = rm * Quaternion.Inverse(rel);
                    c.transform.position += baseAt - mf.TransformPoint(baseBottom);
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
            report.AppendLine($"{box.name}: {k} clips half out of the open bandoleer, lying over the near one (lift {string.Join("/", lifts)} deg); top layer at {layerTop:0.000} m");
        }

        // a leaning lid: lowest point on the floor, centred on at.x, its back (top edge) touching the box face at at.z
        static void Lean(Transform t, Vector3 at)
        {
            var b = Bounds(t);
            var parent = t.parent;
            var worldAt = parent.TransformPoint(at);
            t.position += new Vector3(worldAt.x - b.center.x, parent.position.y - b.min.y, worldAt.z - b.min.z);
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
