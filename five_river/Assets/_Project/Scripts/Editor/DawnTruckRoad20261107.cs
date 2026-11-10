using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.EditorTools
{
    // The dawn GMC is parked at the east end of the trench facing east, its cargo bed toward the trench. Its wheel
    // ruts now show how it got there: up from the rear edge of the map (south, behind the line) along x = 34, east
    // of the trench spoil mound and clear of the pine and boulder at x 24-25, a right turn to the east, a stop,
    // then backing west into the parked spot so the bed faces the trench for unloading. Two ribbons (the drive in
    // and the reverse) hug the terrain (a vertex every 0.5 m along, 7 across, 2 cm up) with the scanned Mud Tire
    // Tracks surface tinted to the map soil, both edges faded by texture alpha (the far end lies in the fog at
    // the map edge). No terrain paint or height change.
    public static class DawnTruckRoad20261107
    {
        const string Folder = "Assets/_Project/Art/Megascans20261107/MudTireTracks/";
        const float Width = 2.6f, Step = 0.5f, Lift = 0.02f, TileLength = 2f;
        const int Across = 7;
        static readonly Vector2 Start = new Vector2(34f, -58f), TurnCentre = new Vector2(39f, -4f), Pulled = new Vector2(43f, 1.2f);
        static readonly Vector2 Parked = new Vector2(31.5f, 3f); // under the parked truck's front wheels
        const float TurnRadius = 5f;

        [MenuItem("FiveRivers/Map/Dawn Truck Road Ruts 20261107")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Stop Play.");
            var map = GameObject.Find("/[Map]");
            var terrain = Terrain.activeTerrain;
            if (map == null || terrain == null) throw new System.InvalidOperationException("Open the Dabudong scene.");
            var old = map.transform.Find("DawnTruckRoad20261107");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var material = Material();
            var root = new GameObject("DawnTruckRoad20261107");
            Undo.RegisterCreatedObjectUndo(root, "Dawn truck road");
            root.transform.SetParent(map.transform, false);
            Build(root.transform, "DriveIn", Ribbon(terrain, DriveIn()), material);
            Build(root.transform, "Reverse", Ribbon(terrain, Reverse()), material);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(map.scene);
            Debug.Log("[Dawn truck road] Drive-in " + DriveIn().Count + " points, reverse " + Reverse().Count + " points.");
        }

        static void Build(Transform parent, string name, Mesh mesh, Material material)
        {
            string meshPath = Folder + "SM_DawnTruckRoad_" + name + ".asset";
            mesh.name = "SM_DawnTruckRoad_" + name;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing != null) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
        }

        // drive in: straight north, quarter turn right to the east, run on a few metres and stop
        public static List<Vector2> DriveIn()
        {
            var pts = new List<Vector2>();
            var turnIn = new Vector2(TurnCentre.x - TurnRadius, TurnCentre.y);
            for (float z = Start.y; z < turnIn.y; z += Step) pts.Add(new Vector2(Start.x, z));
            int arc = Mathf.CeilToInt(TurnRadius * Mathf.PI * .5f / Step);
            for (int i = 0; i <= arc; i++)
            {
                float a = Mathf.Lerp(Mathf.PI, Mathf.PI * .5f, i / (float)arc);
                pts.Add(TurnCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * TurnRadius);
            }
            var end = pts[pts.Count - 1];
            for (float d = Step; d <= Pulled.x - end.x; d += Step) pts.Add(new Vector2(end.x + d, Mathf.Lerp(end.y, Pulled.y, d / (Pulled.x - end.x))));
            return pts;
        }

        // backing in: from where it stopped, a gentle curve west into the parked spot
        public static List<Vector2> Reverse()
        {
            var pts = new List<Vector2>();
            int n = Mathf.CeilToInt((Pulled - Parked).magnitude / Step);
            for (int i = 0; i <= n; i++)
            {
                float s = i / (float)n;
                var p = Vector2.Lerp(Pulled, Parked, s);
                p.y = Mathf.Lerp(Pulled.y, Parked.y, Mathf.SmoothStep(0f, 1f, s));
                pts.Add(p);
            }
            return pts;
        }

        public static List<Vector2> Path() { var all = DriveIn(); all.AddRange(Reverse()); return all; }

        static Mesh Ribbon(Terrain t, List<Vector2> pts)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            float run = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) run += (pts[i] - pts[i - 1]).magnitude;
                var dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                var side = new Vector2(dir.y, -dir.x);
                for (int j = 0; j < Across; j++)
                {
                    float v = j / (float)(Across - 1);
                    var p = pts[i] + side * (v - .5f) * Width;
                    var w = new Vector3(p.x, 0f, p.y);
                    w.y = t.SampleHeight(w) + t.transform.position.y + Lift;
                    verts.Add(w);
                    uvs.Add(new Vector2(run / TileLength, v));
                }
                if (i == 0) continue;
                int a0 = (i - 1) * Across, b0 = i * Across;
                for (int j = 0; j < Across - 1; j++)
                {
                    tris.Add(a0 + j); tris.Add(b0 + j); tris.Add(a0 + j + 1);
                    tris.Add(a0 + j + 1); tris.Add(b0 + j); tris.Add(b0 + j + 1);
                }
            }
            var mesh = new Mesh { name = "SM_DawnTruckRoad" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            // winding check: the ribbon must face up
            if (mesh.normals.Length > 0 && mesh.normals[Across / 2].y < 0f)
            {
                for (int k = 0; k < tris.Count; k += 3) { int s = tris[k + 1]; tris[k + 1] = tris[k + 2]; tris[k + 2] = s; }
                mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateTangents();
            }
            return mesh;
        }

        static Material Material()
        {
            string basePath = Folder + "T_MudTireTracks_BaseAlpha.png", normalPath = Folder + "T_MudTireTracks_Normal.png";
            var bi = (TextureImporter)AssetImporter.GetAtPath(basePath);
            if (bi != null && (!bi.alphaIsTransparency || bi.wrapModeV != TextureWrapMode.Clamp))
            { bi.alphaIsTransparency = true; bi.wrapModeU = TextureWrapMode.Repeat; bi.wrapModeV = TextureWrapMode.Clamp; bi.SaveAndReimport(); }
            var ni = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (ni != null && (ni.textureType != TextureImporterType.NormalMap || ni.wrapModeV != TextureWrapMode.Clamp))
            { ni.textureType = TextureImporterType.NormalMap; ni.wrapModeU = TextureWrapMode.Repeat; ni.wrapModeV = TextureWrapMode.Clamp; ni.SaveAndReimport(); }
            string matPath = Folder + "M_MS_MudTireTracks.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_BumpScale", 1.2f);
            m.SetColor("_BaseColor", new Color(1.0f, .9f, .75f, 1f)); // scanned mud -> the map's red-brown granite soil, a little darker where pressed
            m.SetFloat("_Smoothness", .12f);
            m.SetFloat("_Metallic", 0f);
            // transparent, alpha blended, no depth write: lies on the terrain without z-fighting
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent - 50; // before other transparents (smoke, effects)
            m.SetFloat("_ReceiveShadows", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
