// The poplar-lined road and the stream beside it (run in the editor: unity command script eval --code "<this file>").
//
// "A road lined with poplar trees ran north through the valley. A stream ran along the west side, closely
// paralleling the road" (the Bowling Alley north of Taegu, August 1950; Appleman, South to the Naktong, North
// to the Yalu). The map's tank road is that road: it runs north-south at x = 38..45 between two banks.
//
// In the drought of that August the stream is nearly dry: a stony bed with water standing in a few stretches.
// Changes the terrain data (heights, ground layers, grass) and the Dabudong scene (saves it).
float Edge(float a, float b, float x) { return UnityEngine.Mathf.SmoothStep(0f, 1f, UnityEngine.Mathf.InverseLerp(a, b, x)); }
float CentreX(float z) { return 35.7f + 0.45f * UnityEngine.Mathf.Sin(z * 0.11f) + 0.22f * UnityEngine.Mathf.Sin(z * 0.31f + 1f); }
const float Z0 = 34f, Z1 = 119.5f;                                  // the stream's southern end (it peters out) and the map edge
var terrain = UnityEngine.Terrain.activeTerrain; var data = terrain.terrainData; var origin = terrain.transform.position;
UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Dabudong 1950 valley road");
float Fade(float z) { return Edge(Z0, Z0 + 7f, z); }
// ---- 1. the bed is cut a little into the ground
bool already = UnityEngine.GameObject.Find("ValleyRoad_Aug1950") != null;   // run again: only the scene objects are rebuilt
int hr = data.heightmapResolution;
var heights = data.GetHeights(0, 0, hr, hr);
int cut = 0;
for (int y = 0; y < hr; y++)
    for (int x = 0; x < hr; x++)
    {
        float wx = origin.x + x / (hr - 1f) * data.size.x, wz = origin.z + y / (hr - 1f) * data.size.z;
        if (already || wz < Z0 || wz > Z1 || wx < 32f || wx > 40f) continue;
        float d = UnityEngine.Mathf.Abs(wx - CentreX(wz));
        float depth = 0.32f * (1f - Edge(0.35f, 1.35f, d)) * Fade(wz);
        if (depth <= 0.001f) continue;
        heights[y, x] -= depth / data.size.y; cut++;
    }
data.SetHeights(0, 0, heights);
// ---- 2. stones in the bed, mud on its edges
int an = data.alphamapWidth, ah = data.alphamapHeight, layers = data.alphamapLayers;
var alpha = data.GetAlphamaps(0, 0, an, ah);
const int Stones = 5, Mud = 3;
for (int y = 0; y < ah; y++)
    for (int x = 0; x < an; x++)
    {
        float wx = origin.x + (x + 0.5f) / an * data.size.x, wz = origin.z + (y + 0.5f) / ah * data.size.z;
        if (wz < Z0 || wz > Z1 || wx < 32f || wx > 40f) continue;
        float d = UnityEngine.Mathf.Abs(wx - CentreX(wz)) + (UnityEngine.Mathf.PerlinNoise(wx * 0.9f, wz * 0.9f) - 0.5f) * 0.35f;
        float stones = (1f - Edge(0.45f, 1.0f, d)) * Fade(wz);
        float mud = (1f - Edge(0.9f, 1.7f, d)) * Fade(wz) * 0.75f * (1f - stones);
        float keep = 1f - stones - mud;
        if (keep >= 0.999f) continue;
        for (int k = 0; k < layers; k++) alpha[y, x, k] *= keep;
        alpha[y, x, Stones] += stones; alpha[y, x, Mud] += mud;
    }
if (!already) data.SetAlphamaps(0, 0, alpha);
// ---- 3. no grass in the bed; reeds and tall grass stand along it, where the last of the water is
int dn = data.detailWidth;
var low = data.GetDetailLayer(0, 0, dn, dn, 0); var tall = data.GetDetailLayer(0, 0, dn, dn, 1);
for (int y = 0; y < dn; y++)
    for (int x = 0; x < dn; x++)
    {
        float wx = origin.x + (x + 0.5f) / dn * data.size.x, wz = origin.z + (y + 0.5f) / dn * data.size.z;
        if (wz < Z0 || wz > Z1 || wx < 32f || wx > 40f) continue;
        float d = UnityEngine.Mathf.Abs(wx - CentreX(wz));
        if (d < 1.25f) { low[y, x] = 0; tall[y, x] = 0; }
        else if (d < 2.3f && wx < 37.4f)
        {
            float n = UnityEngine.Mathf.PerlinNoise(wx * 0.5f + 40f, wz * 0.5f + 7f);
            tall[y, x] = UnityEngine.Mathf.Max(tall[y, x], UnityEngine.Mathf.RoundToInt(Edge(0.42f, 0.62f, n) * 170f * Fade(wz)));
        }
    }
if (!already) { data.SetDetailLayer(0, 0, 0, low); data.SetDetailLayer(0, 0, 1, tall); }
UnityEditor.EditorUtility.SetDirty(data);
// ---- 4. the scene objects
var old = UnityEngine.GameObject.Find("ValleyRoad_Aug1950");
if (old != null) UnityEngine.Object.DestroyImmediate(old);
UnityEngine.Transform parent = null;
foreach (var mf in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    if (mf.sharedMesh != null && mf.sharedMesh.name == "Korean_Red_Pine_LOD0") { parent = mf.transform.parent.parent.parent; break; }
var group = new UnityEngine.GameObject("ValleyRoad_Aug1950");
if (parent != null) group.transform.SetParent(parent, false);
group.transform.SetPositionAndRotation(UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
float Ground(float x, float z) { return terrain.SampleHeight(new UnityEngine.Vector3(x, 0f, z)) + origin.y; }
// water: a ribbon in the stretches that still hold some
var verts = new System.Collections.Generic.List<UnityEngine.Vector3>(); var uvs = new System.Collections.Generic.List<UnityEngine.Vector2>(); var tris = new System.Collections.Generic.List<int>();
bool open = false; int stretches = 0;
for (float z = Z0 + 6f; z <= Z1; z += 0.3f)
{
    // drought: only pools are left, a few metres long, with long dry stretches of stones between them
    float wet = UnityEngine.Mathf.PerlinNoise(z * 0.11f + 3f, 0.5f);
    float body = Edge(0.50f, 0.66f, wet);
    if (body < 0.03f) { open = false; continue; }
    // each bank wanders on its own, so the water has no straight edge
    float left = (0.10f + 0.34f * UnityEngine.Mathf.PerlinNoise(z * 0.55f, 9f)) * body;
    float right = (0.10f + 0.34f * UnityEngine.Mathf.PerlinNoise(z * 0.47f + 30f, 21f)) * body;
    float cx = CentreX(z) + (UnityEngine.Mathf.PerlinNoise(z * 0.3f, 55f) - 0.5f) * 0.3f; float yy = Ground(cx, z) + 0.035f;
    verts.Add(new UnityEngine.Vector3(cx - left, yy, z)); verts.Add(new UnityEngine.Vector3(cx + right, yy, z));
    uvs.Add(new UnityEngine.Vector2(0f, z * 0.5f)); uvs.Add(new UnityEngine.Vector2(1f, z * 0.5f));
    if (open) { int i = verts.Count - 4; tris.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 }); } else stretches++;
    open = true;
}
var dir = "Assets/_Project/Art/Maps/Dabudong1950/Stream/";
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_Project/Art/Maps/Dabudong1950/Stream")) UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Art/Maps/Dabudong1950", "Stream");
var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(dir + "SM_StreamWater.asset");
if (mesh == null) { mesh = new UnityEngine.Mesh { name = "SM_StreamWater" }; UnityEditor.AssetDatabase.CreateAsset(mesh, dir + "SM_StreamWater.asset"); }
mesh.Clear(); mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
UnityEditor.EditorUtility.SetDirty(mesh);
var water = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(dir + "M_StreamWater.mat");
if (water == null) { water = new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit")); UnityEditor.AssetDatabase.CreateAsset(water, dir + "M_StreamWater.mat"); }
// shallow, still, silty water: mostly a mirror of the sky, the bed just showing through
water.SetColor("_BaseColor", new UnityEngine.Color(0.075f, 0.075f, 0.06f, 0.66f));
water.SetFloat("_Smoothness", 0.50f); water.SetFloat("_Metallic", 0f);   // 2026-11-27: 0.90 ran to white at a low angle; the stream's look is kept by FiveRivers/Setup/Water Look 20261127 (BALANCE 4-3)
water.SetFloat("_Surface", 1f); water.SetFloat("_Blend", 0f); water.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); water.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
water.SetFloat("_ZWrite", 0f); water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); water.SetOverrideTag("RenderType", "Transparent");
water.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
UnityEditor.EditorUtility.SetDirty(water);
var wgo = new UnityEngine.GameObject("StreamWater"); wgo.transform.SetParent(group.transform, false);
wgo.AddComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
var wr = wgo.AddComponent<UnityEngine.MeshRenderer>(); wr.sharedMaterial = water; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
// poplars: a row on each side of the road, a tree every eight or nine metres, a few gone
var pa = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Maps/Dabudong1950/Poplar/PF_Poplar_A.prefab");
var pb = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Art/Maps/Dabudong1950/Poplar/PF_Poplar_B.prefab");
if (pa == null || pb == null) return "poplar prefabs missing";
// the pines that already stand in the valley keep their place: no poplar is planted against one
var standing = new System.Collections.Generic.List<UnityEngine.Vector3>();
foreach (var mf in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshFilter>(UnityEngine.FindObjectsSortMode.None))
    if (mf.sharedMesh != null && mf.sharedMesh.name == "Korean_Red_Pine_LOD0") standing.Add(mf.transform.parent.parent.position);
var rng = new System.Random(1950);
float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
int trees = 0, skipped = 0;
foreach (var row in new[] { 0, 1 })
{
    // the west row stands between the road and the cameras of the tank's entrance, which run along the stream: it
    // is kept thin so a trunk crosses the lens now and then instead of hiding the tank
    for (float z = 46f + row * 4f; z <= 114f; z += row == 0 ? R(15f, 18f) : R(7.8f, 9.2f))
    {
        if (rng.NextDouble() < 0.14) { skipped++; continue; }                     // a gap in the row
        // west row on the stream bank between the water and the road; east row clear of the tank's right track
        float tankX = 39f + 6f * (z - 23f) / 82f;
        float x = row == 0 ? UnityEngine.Mathf.Min(CentreX(z) + 1.75f, tankX - 2.6f) : tankX + 3.3f + R(-0.3f, 0.3f);
        var p = new UnityEngine.Vector3(x + R(-0.25f, 0.25f), 0f, z); p.y = Ground(p.x, p.z) - 0.06f;
        bool crowded = UnityEngine.Physics.CheckSphere(p + UnityEngine.Vector3.up * 1.5f, 0.9f, ~0, UnityEngine.QueryTriggerInteraction.Ignore);
        foreach (var q in standing) if ((q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) < 3.5f * 3.5f) crowded = true;
        if (crowded) { skipped++; continue; }
        var t = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(rng.NextDouble() < 0.6 ? pa : pb, group.transform);
        t.name = "Poplar_" + (row == 0 ? "W" : "E") + "_" + trees;
        t.transform.SetPositionAndRotation(p, UnityEngine.Quaternion.Euler(0f, R(0f, 360f), 0f));
        float s = R(0.88f, 1.12f); t.transform.localScale = new UnityEngine.Vector3(s, s * R(0.95f, 1.08f), s);
        trees++;
    }
}
// wind in the poplars and a slow ripple on the water (FiveRivers.Environment.ValleyAmbience)
var ripple = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(dir + "T_StreamRipple_N.png");
if (ripple != null) { water.SetTexture("_BumpMap", ripple); water.SetFloat("_BumpScale", 0.45f); water.EnableKeyword("_NORMALMAP"); water.SetTextureScale("_BaseMap", new UnityEngine.Vector2(1f, 0.35f)); UnityEditor.EditorUtility.SetDirty(water); }
group.AddComponent<FiveRivers.Environment.ValleyAmbience>();
UnityEditor.AssetDatabase.SaveAssets();
var scene = group.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "height samples cut " + cut + ", water stretches " + stretches + " (" + tris.Count / 3 + " tris), poplars " + trees + " (skipped " + skipped + "), scene saved " + saved;
