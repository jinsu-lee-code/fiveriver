using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.EditorTools
{
    // 2026-11-25 (사용자 지시 "이느낌으로 아군 진지 다시 바꾸어봐" + 1950 한국전 산비탈 진지 사진. 결정: 참호 모양 · 길찾기 ·
    // 아군 자리는 그대로, 꾸미기 먼저): the squad's position dressed as the men in the photograph had theirs —
    //  - cut young pines laid over the parapet and the sandbags as camouflage (not in front of a firing place)
    //  - pine litter, broken twigs, a log or two and a thrown-down poncho on the floor and the berms
    //  - dry grass on the berms and at the foot of the walls, loose stones
    //  - young pines, shrubs and a few red pines standing behind the position and at its ends
    // The sandbags themselves and the dug-out earth on the elbow rest are TrenchDressing (run that first).
    // Nothing has a collider (the NavMesh, cover and bullets are as before). Re-running rebuilds the same result.
    public static class PositionDressing20261125
    {
        const string RootName = "PositionDressing_20261125";
        // ---- boughs before the firing places (2026-11-27, BALANCE.md 4-5)
        public const float FiringClear = 1.5f, FiringLow = 2.6f;   // metres along the trench from a firing place: nothing / only flat boughs
        public const float TankFiringPlace = 17f;                   // where the player stands for the tank (LevelPoints.tankCheckpoint), not in TrenchDressing.FiringPlaces
        static readonly float[] ClearPlaces = TrenchDressing.FiringPlaces.Concat(new[] { TankFiringPlace }).ToArray();
        static readonly int[] LowBoughs = { 1, 2, 4, 5 };           // PD_Bough_0 stands up to a metre and 3 is the bushiest
        public static readonly Color Needles = new Color(.17f, .25f, .10f);   // was (.10, .16, .055): it read as black, by day too
        const string Art = "Assets/_Project/Art/";
        const string Props = Art + "MapPolish20261105/PositionDressing20261125/SM_PD_PositionProps.fbx";
        const string Folder = "Assets/_Project/Art/MapPolish20261105/PositionDressing20261125";

        [MenuItem("FiveRivers/Setup/Position Dressing 20261125")]
        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            if (map == null || map.transform.Find("TrenchDressing_20261116") == null) throw new System.InvalidOperationException("먼저 Trench Dressing 을 적용하세요.");
            var terrain = Terrain.activeTerrain;
            var td = terrain.terrainData; var tp = terrain.transform.position;
            float H(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + tp.y;
            Vector3 N(float x, float z) => td.GetInterpolatedNormal((x - tp.x) / td.size.x, (z - tp.z) / td.size.z);
            var report = new StringBuilder();

            var old = map.transform.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Position Dressing");
            root.transform.SetParent(map.transform, false);
            Transform Group(string n) { var g = new GameObject(n); g.transform.SetParent(root.transform, false); return g.transform; }
            var rng = new System.Random(1950_1125);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            GameObject Load(string p) { var g = AssetDatabase.LoadAssetAtPath<GameObject>(Art + p); if (g == null) throw new System.InvalidOperationException("없음: " + p); return g; }
            // (the delivered ENV_young_pine / ENV_shrub carry no texture — white frost on a thorn bush in the 1st look —
            // so the standing trees are the Korean red pine scan made small and the summer shrub scan)
            var shrub = Load("Maps/DabudongRefined/Source/Art/EnvironmentCharacters20261023/Summer_Shrub/Summer_Shrub.prefab");
            // 2026-11-25: the re-made Korean red pines (RedPineSetup20261125) instead of the scan whose crown read as a bonsai
            var pines = RedPineSetup20261125.Trees.Select(t => Load("Maps/Dabudong_Env/RedPine20261125/" + t + ".fbx")).ToArray();
            var grassDry = Load("Maps/Dabudong_Env/Models/ENV_grass_clump_dry.fbx");
            var grass = Load("Maps/Dabudong_Env/Models/ENV_grass_clump.fbx");
            var tuft = Load("Maps/Dabudong_Env/Models/ENV_grass_tuft.fbx");
            var pebble = Load("Maps/Dabudong_Env/Models/ENV_pebble.fbx");
            var litter = Load("Megascans20261107/PineLitter/PF_MS_PineLitter.prefab");
            var log = Load("Megascans20261107/FallenPineTree/PF_MS_FallenPineTree.prefab");
            var meshes = AssetDatabase.LoadAllAssetsAtPath(Props).OfType<Mesh>().ToDictionary(m => m.name);
            if (!meshes.ContainsKey("PD_Twig_0") || !meshes.ContainsKey("PD_Poncho_A")) throw new System.InvalidOperationException("소품 FBX가 없습니다: " + Props);
            var twigMat = AssetDatabase.LoadAssetAtPath<Material>(Art + "Megascans20261107/FallenPineTree/M_MS_FallenPineTree.mat");
            var ponchoMat = PonchoMaterial();
            // cut pine boughs (PD_Bough_*, needles as thin crossed blades): bark, fresh needles, needles going yellow
            var boughMats = new[] { twigMat, Plain("M_PD_PineNeedles", Needles, .28f, true), Plain("M_PD_PineNeedlesDry", new Color(.30f, .27f, .11f), .2f, true) };
            var grassMats = new Dictionary<GameObject, Material>
            {
                { grass, GrassMat("M_PD_GrassClump", Color.white, .30f, .45f) },
                { grassDry, GrassMat("M_PD_GrassClumpDry", new Color(1.02f, .98f, .9f), .55f, .45f) },
                { tuft, GrassMat("M_PD_GrassTuft", Color.white, .40f, .62f) },
            };
            var stoneMat = Tex("M_PD_Stone", "MapPolish20261105/dirt_floor_diff_2k.jpg", new Color(.95f, .9f, .82f));

            int count = 0;
            GameObject Put(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot, float scale, GameObject lod1 = null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = Vector3.one * scale;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                if (lod1 != null && go.GetComponentInChildren<LODGroup>() == null)
                {
                    var far = (GameObject)PrefabUtility.InstantiatePrefab(lod1, go.transform);
                    far.transform.localPosition = Vector3.zero; far.transform.localRotation = Quaternion.identity; far.transform.localScale = Vector3.one;
                    foreach (var c in far.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    var near = go.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(far.transform)).ToArray();
                    var group = go.AddComponent<LODGroup>();
                    group.SetLODs(new[] { new LOD(.25f, near), new LOD(.03f, far.GetComponentsInChildren<Renderer>(true)) });
                    group.RecalculateBounds();
                }
                count++;
                return go;
            }
            GameObject PutMesh(string mesh, Material mat, Transform parent, Vector3 pos, Quaternion rot, float scale)
            {
                var go = new GameObject(mesh); go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = Vector3.one * scale;
                go.AddComponent<MeshFilter>().sharedMesh = meshes[mesh];
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On;
                count++;
                return go;
            }
            GameObject PutBough(int i, Transform parent, Vector3 pos, Quaternion rot, float scale)
            {
                var go = new GameObject("PD_Bough_" + i); go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = Vector3.one * scale;
                Renderer Part(string lod)
                {
                    var p = new GameObject(lod); p.transform.SetParent(go.transform, false);
                    p.AddComponent<MeshFilter>().sharedMesh = meshes[$"PD_Bough_{i}_{lod}"];
                    var r = p.AddComponent<MeshRenderer>(); r.sharedMaterials = boughMats; r.shadowCastingMode = ShadowCastingMode.On;
                    return r;
                }
                var group = go.AddComponent<LODGroup>();
                group.SetLODs(new[] { new LOD(.12f, new[] { Part("LOD0") }), new LOD(.015f, new[] { Part("LOD1") }) });
                group.RecalculateBounds();
                count++;
                return go;
            }
            Quaternion OnGround(float x, float z, float yaw) => Quaternion.FromToRotation(Vector3.up, N(x, z)) * Quaternion.Euler(0f, yaw, 0f);
            bool NearFiring(float x, float m) => TrenchDressing.FiringPlaces.Any(f => Mathf.Abs(f - x) < m);

            float west = TrenchDig.TrenchWest, east = TrenchDig.TrenchEast;
            float frontFloor = TrenchDig.FrontFloorZ, wall = TrenchDig.WallRunZ;
            float RearFloor(float x) => TrenchDig.RearFloorZ(x);
            // the parapet's back (enemy side of the crest) and the parados's top
            float parapetZ0 = frontFloor + wall + .75f, parapetZ1 = frontFloor + wall + 1.7f;

            // 1. camouflage: cut pine boughs piled on the parapet, butts back, tips hanging toward the trench (a bough lies
            // along its +x: yaw 90 points it at -z). Two or three to a pile; not in front of a firing place.
            var gCamo = Group("Camouflage");
            int camo = 0, piles = 0, cleared = 0, laid = 0;
            for (float x = west + .8f; x <= east - .6f; x += R(1.2f, 1.9f))
            {
                if (NearFiring(x, 1.15f)) continue;
                int n = rng.NextDouble() < .45 ? 3 : 2;
                bool any = false;
                for (int k = 0; k < n; k++)
                {
                    float bx = x + R(-.35f, .35f), z = R(parapetZ0 + .15f, parapetZ0 + .55f);
                    float yaw = R(-35f, 35f);
                    var rot = Quaternion.Euler(0f, 90f + yaw, 0f) * Quaternion.Euler(R(-12f, 12f), 0f, R(-18f, -4f));   // tip a little down
                    int kind = rng.Next(6); float scale = R(.85f, 1.2f); float y = H(bx, z) + .02f + k * .05f;
                    // 2026-11-27 (아트 검수 "흉장 위 솔가지가 세로 병솔처럼 서서 전차 구간 사격 자리 시야를 가림"): in front of a firing
                    // place — the tank section's too, which was not in the list — nothing within FiringClear of it, and out to
                    // FiringLow only boughs laid flat on the enemy's side of the crest, tips down the slope, the low kinds.
                    // (The same random numbers are drawn as before, so everything placed after this stands where it stood.)
                    float clear = ClearPlaces.Min(p => Mathf.Abs(p - bx));
                    if (clear < FiringClear) { cleared++; continue; }
                    if (clear < FiringLow)
                    {
                        kind = LowBoughs[kind % LowBoughs.Length]; scale = Mathf.Min(scale, .95f);
                        z = parapetZ0 + .95f + (z - parapetZ0 - .15f); y = H(bx, z) + .02f;
                        rot = OnGround(bx, z, -90f + yaw) * Quaternion.Euler(0f, 0f, -8f);
                        laid++;
                    }
                    PutBough(kind, gCamo, new Vector3(bx, y, z), rot, scale);
                    camo++; any = true;
                }
                if (any) piles++;
            }
            // boughs against the parados (butt behind, leaning up over the trench edge) and over the niche sandbags
            foreach (var x in new[] { -16.2f, -11.8f, -9.5f, -2.6f, 0.6f, 5.4f, 7.8f, 13.6f, 18.0f })
            {
                float z = RearFloor(x) - wall - R(.55f, .85f);
                var rot = Quaternion.Euler(0f, -90f + R(-30f, 30f), 0f) * Quaternion.Euler(R(-10f, 10f), 0f, R(10f, 28f));     // +x toward +z, tip up
                PutBough(rng.Next(6), gCamo, new Vector3(x, H(x, z) + .05f, z), rot, R(.85f, 1.15f));
                camo++;
            }
            report.AppendLine($"camouflage boughs {camo} ({piles} piles on the parapet; before the firing places {cleared} left out, {laid} laid flat beyond the crest)");

            // 2. litter, twigs, stones on the floor and the berms
            var gLitter = Group("Litter");
            int litters = 0, twigs = 0, stones = 0;
            for (int i = 0; i < 46; i++)
            {
                float x = R(west + .3f, east - .3f);
                // floor (most) or the berms
                float z = i % 3 == 0 ? R(parapetZ0 - .1f, parapetZ1) : i % 3 == 1 ? R(RearFloor(x) + .1f, frontFloor - .05f) : R(RearFloor(x) - 2.2f, RearFloor(x) - .6f);
                Put(litter, gLitter, new Vector3(x, H(x, z) + .012f, z), OnGround(x, z, R(0f, 360f)), R(.45f, .9f));
                litters++;
            }
            for (int i = 0; i < 44; i++)
            {
                float x = R(west + .3f, east - .3f);
                float z = i % 2 == 0 ? R(RearFloor(x) + .05f, frontFloor) : (i % 4 == 1 ? R(parapetZ0 - .2f, parapetZ1) : R(RearFloor(x) - 2f, RearFloor(x) - .5f));
                PutMesh("PD_Twig_" + (i % 6), twigMat, gLitter, new Vector3(x, H(x, z) - .004f, z), OnGround(x, z, R(0f, 360f)), R(.7f, 1.15f));
                twigs++;
            }
            for (int i = 0; i < 60; i++)
            {
                float x = R(west + .2f, east - .2f);
                float z = i % 3 == 0 ? R(RearFloor(x) + .05f, frontFloor + .05f) : i % 3 == 1 ? R(parapetZ0 - .3f, parapetZ1 + .4f) : R(RearFloor(x) - 2.4f, RearFloor(x) - .3f);
                float s = R(.05f, .16f);
                var st = Put(pebble, gLitter, new Vector3(x, H(x, z) - s * .12f, z), Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f)), s);
                foreach (var r in st.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = stoneMat;
                stones++;
            }
            report.AppendLine($"litter {litters}, twigs {twigs}, stones {stones}");

            // 3. logs and ponchos (on the berms, never on the floor where the men walk)
            var gThings = Group("Things");
            foreach (var (x, onParados) in new[] { (-10.8f, true), (7.2f, true), (-17.6f, false), (3.6f, false) })
            {
                float z = onParados ? RearFloor(x) - R(1.4f, 2.0f) : parapetZ1 + R(.1f, .5f);
                Put(log, gThings, new Vector3(x, H(x, z) + .02f, z), OnGround(x, z, R(-25f, 25f) + (onParados ? 0f : 90f)), R(.8f, 1.05f));
            }
            PutMesh("PD_Poncho_A", ponchoMat, gThings, new Vector3(3.4f, H(3.4f, parapetZ0 + .25f) + .01f, parapetZ0 + .25f), OnGround(3.4f, parapetZ0 + .25f, 12f), 1f);
            { float x = -10.2f, z = RearFloor(x) - .9f; PutMesh("PD_Poncho_B", ponchoMat, gThings, new Vector3(x, H(x, z) + .01f, z), OnGround(x, z, -20f), 1f); }

            // 4. grass on the berms and at the foot of the walls
            var gGrass = Group("Grass");
            int grasses = 0;
            for (int i = 0; i < 90; i++)
            {
                float x = R(west - 1f, east + 1f);
                float z;
                switch (i % 5)
                {
                    case 0: z = R(parapetZ0 + .2f, parapetZ1 + 1.2f); break;                  // parapet back
                    case 1: z = R(RearFloor(x) - 3f, RearFloor(x) - .7f); break;              // parados and behind
                    case 2: z = frontFloor - R(.02f, .12f); break;                            // foot of the front wall
                    case 3: z = RearFloor(x) + R(.02f, .12f); break;                          // foot of the rear wall
                    default: z = R(parapetZ1, parapetZ1 + 3f); break;                         // in front, thin
                }
                var pick = i % 3 == 0 ? grass : i % 3 == 1 ? grassDry : tuft;
                float s = i % 5 >= 2 && i % 5 <= 3 ? R(.7f, 1.05f) : R(.9f, 1.6f);   // (re-made clumps are ~0.4 m)
                var gr = Put(pick, gGrass, new Vector3(x, H(x, z) - .02f, z), OnGround(x, z, R(0f, 360f)), s);
                foreach (var r in gr.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = grassMats[pick]; r.shadowCastingMode = ShadowCastingMode.Off; }
                grasses++;
            }
            report.AppendLine($"grass {grasses}");

            // 5. trees and shrubs behind the position and at its ends (nothing in the field of fire)
            var gTrees = Group("Trees");
            int trees = 0;
            var roads = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name.Contains("Road") || r.name.Contains("Truck")).Select(r => r.bounds).ToArray();
            bool OnRoad(Vector3 p) => roads.Any(b => { var e = b; e.Expand(new Vector3(2.5f, 50f, 2.5f)); return e.Contains(p); });
            for (int i = 0; i < 28; i++)
            {
                float x, z;
                if (i < 20) { x = R(west - 1f, east - 2f); z = RearFloor(x) - R(3.2f, 9f); }
                else { bool w = i % 2 == 0; x = w ? R(west - 5f, west - 1.5f) : R(east + 2.5f, east + 5f); z = R(-5f, 1.5f); }
                var p = new Vector3(x, H(x, z) - .05f, z);
                if (OnRoad(p)) continue;
                int kind = i % 4;
                if (kind == 0 && i < 20) RedPineSetup20261125.TuneLods(Put(pines[i % 3], gTrees, p, Quaternion.Euler(0f, R(0f, 360f), 0f), R(.8f, 1.1f)));
                else if (kind == 1) Put(shrub, gTrees, p, Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)), R(.8f, 1.3f));
                else RedPineSetup20261125.TuneLods(Put(pines[(i + 1) % 3], gTrees, p, Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)), R(.28f, .5f)));   // young pines (2.5 - 4.5 m)
                trees++;
            }
            report.AppendLine($"trees and shrubs {trees}, total placed {count}");

            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            // what was just placed is drawn by distance like the rest of the map (2026-11-25, "거리별로 … 달라지게")
            report.Append(DistanceDetail20261125.Apply(false));
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            File.WriteAllText("Temp/position_dressing.txt", report.ToString());
            Debug.Log("[PositionDressing20261125]\n" + report);
            return report.ToString();
        }

        // 2026-11-25 (사용자 지시 "풀은가져오지말고 느낌만 빼와서 기존에 있는거에 이식해"): the delivered grass re-made in place
        // (source/environment/grass_20261125/make_grass.py: thin tapering blades on alpha cards, seed heads) and drawn with
        // FiveRivers/WildGrass — wind in the tips, the root in shade, light through the blade against the sun.
        static Material GrassMat(string name, Color tint, float dry, float height)
        {
            string path = Folder + "/" + name + ".mat";
            var shader = Shader.Find("FiveRivers/WildGrass");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            const string tex = Art + "Maps/Dabudong_Env/Textures/T_grass_wild_0_BaseColor.png";
            if (AssetImporter.GetAtPath(tex) is TextureImporter ti && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp || !ti.mipMapsPreserveCoverage))
            {
                ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = .45f;
                ti.SaveAndReimport();
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_DryVariation", dry);
            m.SetFloat("_Height", height);
            m.SetFloat("_RootDarken", .3f);   // .55 read as a black brush at the foot of the clump
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Plain(string name, Color c, float smooth, bool twoSided)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Tex(string name, string tex, Color c)
        {
            var m = Plain(name, c, .05f, false);
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + tex);
            m.SetTexture("_BaseMap", t); m.SetTexture("_MainTex", t);
            EditorUtility.SetDirty(m);
            return m;
        }

        // an olive-drab shelter half: the hessian scan's weave, dyed and dirty
        static Material PonchoMaterial()
        {
            string path = Folder + "/M_PD_Poncho.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            var b = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "MapPolish20261105/TrenchDressing/Sandbags20261125/T_TD_SandbagBurlap_BaseColor.png");
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Maps/ClaudePolish20261030/Scan/hessian_230_nor_gl_2k.jpg");
            m.SetTexture("_BaseMap", b); m.SetTexture("_MainTex", b);
            var c = new Color(.62f, .66f, .46f);
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", .08f); m.SetFloat("_Metallic", 0f);
            if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", .5f); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Cull", 0f);   // the sheet is seen from both sides
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
