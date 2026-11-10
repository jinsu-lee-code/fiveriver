using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.EditorTools
{
    // Quixel Megascans props (Fab, 2026-11-07, AI-use allowed by Quixel's 2026-11-05 terms) for the 1950 Dabudong map.
    // Visual-only prefabs: no colliders, so the runtime NavMesh (built from physics colliders) is unchanged.
    public static class Megascans20261107Setup
    {
        public const string Root = "Assets/_Project/Art/Megascans20261107/";
        static readonly string[] Props = { "FallenPineTree", "SandbagRound", "SandbagTorn", "RopeHemp", "ShellCrater" };
        // The crater scan is pale chalky sand; tint it to the map's red-brown granite soil (T2_Soil_A average / scan average).
        static readonly Color CraterTint = new Color(.47f, .41f, .35f, 1f); // darker than the soil so the rim never glows at night

        [MenuItem("FiveRivers/Art/Megascans 20261107/Create Materials and Prefabs")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            foreach (var name in Props)
            {
                string dir = Root + name + "/";
                ConfigureModel(dir + name + ".fbx");
                var mat = Material(dir, name, false);
                mat.SetColor("_BaseColor", name == "ShellCrater" ? CraterTint : name == "FallenPineTree" ? new Color(1.1f, 1.1f, 1.1f, 1f) : Color.white);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + ".fbx");
                if (model == null) throw new FileNotFoundException("Missing model", dir + name + ".fbx");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                PrefabUtility.SaveAsPrefabAsset(go, dir + "PF_MS_" + name + ".prefab");
                Object.DestroyImmediate(go);
            }
            // Pine needle litter: a 2 m alpha-clipped ground card.
            var litter = Material(Root + "PineLitter/", "PineLitter", true);
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(card.GetComponent<Collider>());
            card.name = "PineLitter";
            var mesh = card.GetComponent<MeshFilter>();
            var holder = new GameObject("PF_MS_PineLitter");
            card.transform.SetParent(holder.transform, false);
            card.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            card.transform.localScale = new Vector3(2f, 2f, 1f);
            card.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            var cr = card.GetComponent<MeshRenderer>(); cr.sharedMaterial = litter; cr.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(holder, Root + "PineLitter/PF_MS_PineLitter.prefab");
            Object.DestroyImmediate(holder);
            // Paddy mud as a terrain layer (2 m tiling, matching the scan).
            var layerPath = Root + "PaddyMud/TL_MS_PaddyMud.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            bool created = layer == null;
            if (created) layer = new TerrainLayer();
            layer.diffuseTexture = Texture(Root + "PaddyMud/PaddyMud_BaseColor.png", false);
            layer.normalMapTexture = Texture(Root + "PaddyMud/PaddyMud_Normal.png", true);
            layer.maskMapTexture = Texture(Root + "PaddyMud/PaddyMud_MetallicSmoothness.png", false, true);
            layer.tileSize = new Vector2(2f, 2f);
            if (created) AssetDatabase.CreateAsset(layer, layerPath);
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssets();
            Debug.Log("[Megascans 20261107] Materials, prefabs and paddy mud terrain layer ready.");
        }

        // Hand-picked spots off the trench floor and the hill assault lane: name, x, z, yaw, tilt-to-slope.
        // Placed where the player looks most: north from the trench toward the hill (enemy side),
        // around but not on the assault lane. Rear-area spots are left empty.
        // sink: metres pushed below the ground; flatY: vertical scale; scale: uniform scale; top: rest on the highest hit (parapet).
        static readonly (string prefab, float x, float z, float yaw, float slope, float sink, float flatY, float scale, bool top)[] Placements =
        {
            // Shell craters are not placed: the Megascans crater scan is only the raised rim, so on flat terrain it reads
            // as a ring sitting on the ground. Shelling marks, if wanted, should be dug into the terrain heightmap instead
            // (friendly artillery fell on the KPA approach in front of the line: Appleman, CMH 1961, ch. 19).
            ("SandbagTorn/PF_MS_SandbagTorn", 9.6f, 30.8f, 95f, 1f, 0f, 1f, 1f, false),       // beside CP16
            ("SandbagRound/PF_MS_SandbagRound", 8.0f, 21.0f, 0f, .8f, .05f, 1f, 1f, false),   // abandoned position on the slope facing the trench
            ("RopeHemp/PF_MS_RopeHemp", 5.8f, -2.9f, 40f, 0f, 0f, 1f, 1f, false),             // by the trench ammo crate, against the rear wall
            ("FallenPineTree/PF_MS_FallenPineTree", -25.0f, 27.0f, 115f, 1f, .05f, 1f, 1.3f, false), // shell-felled pines on the flanks of the view
            ("FallenPineTree/PF_MS_FallenPineTree", 18.5f, 30.0f, 250f, 1f, .05f, 1f, 1.3f, false),
        };

        [MenuItem("FiveRivers/Art/Megascans 20261107/Place in Dabudong")]
        public static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var map = GameObject.Find("/[Map]");
            if (map == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            var old = map.transform.Find("Megascans20261107");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var parent = new GameObject("Megascans20261107");
            Undo.RegisterCreatedObjectUndo(parent, "Place Megascans props");
            parent.transform.SetParent(map.transform, false);
            foreach (var p in Placements)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + p.prefab + ".prefab");
                if (prefab == null) throw new FileNotFoundException("Missing prefab", Root + p.prefab + ".prefab");
                // Lowest hit = the ground, not a tree or wall standing over it.
                var hits = Physics.RaycastAll(new Vector3(p.x, 200f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
                if (hits.Length == 0) throw new System.InvalidOperationException("No ground at " + p.x + ", " + p.z);
                var hit = hits[0];
                foreach (var h in hits) if (p.top ? h.point.y > hit.point.y : h.point.y < hit.point.y) hit = h;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                var up = Vector3.Slerp(Vector3.up, hit.normal, p.slope);
                go.transform.SetPositionAndRotation(hit.point - up * p.sink, Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, p.yaw, 0f));
                // Stretch each crater a little differently so repeats of the one scan do not read as identical rings.
                float stretch = p.prefab.Contains("ShellCrater") ? 1f + .25f * Mathf.Sin(p.yaw * 1.7f) : 1f;
                go.transform.localScale = new Vector3(p.scale * stretch, p.scale * p.flatY, p.scale / stretch);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("[Megascans 20261107] Placed " + Placements.Length + " props under [Map]/Megascans20261107 (scene left dirty for save).");
        }

        static void ConfigureModel(string path)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            if (mi == null) return;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.addCollider = false;
            // walk blockers use these meshes as colliders, and the runtime NavMesh build reads them: a player build needs them readable
            mi.isReadable = true;
            mi.SaveAndReimport();
        }

        static Texture2D Texture(string path, bool normal, bool linear = false)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) throw new FileNotFoundException("Missing texture", path);
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (linear && ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
            if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Material(string dir, string name, bool cutout)
        {
            string ext = File.Exists(dir + name + "_BaseColor.jpg") ? ".jpg" : ".png";
            string path = dir + "M_MS_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_MS_" + name };
            mat.SetTexture("_BaseMap", Texture(dir + name + "_BaseColor" + ext, false));
            mat.SetTexture("_BumpMap", Texture(dir + name + "_Normal" + ext, true));
            mat.SetTexture("_MetallicGlossMap", Texture(dir + name + "_MetallicSmoothness.png", false, true));
            mat.SetTexture("_OcclusionMap", Texture(dir + name + "_AO" + ext, false, true));
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.EnableKeyword("_OCCLUSIONMAP");
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", 0.4f); mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0f); mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            if (created) AssetDatabase.CreateAsset(mat, path);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
