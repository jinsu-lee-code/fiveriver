// Plants the Dabudong terrain for August 1950 (run in the editor: unity command script eval --code "<this file>").
//
// What the records say, and what is done about each:
//   - "the worst drought in thirty years, 37 C" (Dabudong memorial hall) but "the narrow, green valley" (Appleman,
//     ch. 19, of this very road)                                             -> summer green going dry: green with straw in it, not straw
//   - Korean hills of the early 1950s were mostly bare, the soil showing on
//     the slopes; firewood was 90% of the country's energy (National Archives
//     forest records)                                                        -> grass in broken patches, bare soil on the steep ground
//   - veterans: "not a whole tree or a whole rock was left"                   -> nothing is planted in the shelled trench line
//
// The terrain already held a grass layer, but its values (0..4) were instance counts while the terrain draws
// details in coverage mode (0..255): 1.6% cover, so nothing showed.
var terrain = UnityEngine.Terrain.activeTerrain;
var data = terrain.terrainData;
var dry = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/_Project/Art/Maps/Dabudong1950/T_Grass_DryTuft.png");
var tall = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/_Project/Art/Maps/Dabudong1950/T_Grass_SeedingTall.png");
if (dry == null || tall == null) return "grass textures missing";
// GLSL-style smoothstep: 0 below a, 1 above b (Mathf.SmoothStep is an interpolation, not this)
float Edge(float a, float b, float x) { return UnityEngine.Mathf.SmoothStep(0f, 1f, UnityEngine.Mathf.InverseLerp(a, b, x)); }
UnityEditor.Undo.RegisterCompleteObjectUndo(data, "Dabudong 1950 dry grass");

UnityEngine.DetailPrototype Make(UnityEngine.Texture2D tex, float w0, float w1, float h0, float h1, UnityEngine.Color healthy, UnityEngine.Color dryColor)
{
    return new UnityEngine.DetailPrototype
    {
        prototypeTexture = tex, usePrototypeMesh = false, renderMode = UnityEngine.DetailRenderMode.GrassBillboard,
        minWidth = w0, maxWidth = w1, minHeight = h0, maxHeight = h1, noiseSpread = 0.6f,
        healthyColor = healthy, dryColor = dryColor, density = 1f, targetCoverage = 1f, useDensityScaling = true,
    };
}
data.detailPrototypes = new[]
{
    // short summer grass burnt by the drought: ankle to knee high, so a kneeling man is not hidden by it
    Make(dry, 0.70f, 1.20f, 0.24f, 0.52f, new UnityEngine.Color(0.34f, 0.44f, 0.21f), new UnityEngine.Color(0.54f, 0.53f, 0.29f)),
    // seeding grass in a few thin stands
    Make(tall, 0.55f, 0.90f, 0.60f, 0.95f, new UnityEngine.Color(0.46f, 0.52f, 0.28f), new UnityEngine.Color(0.66f, 0.62f, 0.38f)),
};
data.SetDetailScatterMode(UnityEngine.DetailScatterMode.CoverageMode);
data.wavingGrassTint = new UnityEngine.Color(0.72f, 0.78f, 0.56f, 1f);
// the ground under it is parched too: the grass layer is pulled from green toward ochre
var grassLayer = data.terrainLayers[0];
grassLayer.diffuseRemapMax = new UnityEngine.Vector4(0.93f, 0.97f, 0.72f, 1f);
UnityEditor.EditorUtility.SetDirty(grassLayer);
data.wavingGrassAmount = 0.35f; data.wavingGrassSpeed = 0.35f; data.wavingGrassStrength = 0.4f;

int n = data.detailWidth, an = data.alphamapWidth;
var alpha = data.GetAlphamaps(0, 0, an, data.alphamapHeight);
var a0 = new int[n, n]; var a1 = new int[n, n];
long cells = 0, planted = 0, plantedTall = 0, bared = 0;
bool changedAlpha = false;
for (int y = 0; y < n; y++)
    for (int x = 0; x < n; x++)
    {
        float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
        int ax = UnityEngine.Mathf.Min(an - 1, (int)(u * an)), ay = UnityEngine.Mathf.Min(data.alphamapHeight - 1, (int)(v * data.alphamapHeight));
        float grass = alpha[ay, ax, 0];
        float slope = data.GetSteepness(u, v);
        float wx = u * data.size.x, wz = v * data.size.z;
        // broken patches a few metres across, and smaller gaps inside them
        float patch = UnityEngine.Mathf.PerlinNoise(wx * 0.085f + 13.7f, wz * 0.085f + 4.1f) * 0.65f + UnityEngine.Mathf.PerlinNoise(wx * 0.29f + 71f, wz * 0.29f + 33f) * 0.35f;
        float gaps = UnityEngine.Mathf.PerlinNoise(wx * 0.9f + 5f, wz * 0.9f + 19f);
        float cover = Edge(0.30f, 0.50f, patch) * (0.55f + 0.45f * Edge(0.2f, 0.5f, gaps));
        cover *= UnityEngine.Mathf.Pow(grass, 1.6f);                                   // only where the ground itself is grass
        cover *= 1f - Edge(24f, 38f, slope);                   // the steep ground is bare
        if (grass > 0.5f) cells++;
        int c = UnityEngine.Mathf.RoundToInt(cover * 255f);
        if (c > 12) { a0[y, x] = c; planted++; }
        float stand = Edge(0.70f, 0.80f, UnityEngine.Mathf.PerlinNoise(wx * 0.06f + 201f, wz * 0.06f + 57f)) * Edge(0.45f, 0.7f, gaps);
        int t = UnityEngine.Mathf.RoundToInt(stand * UnityEngine.Mathf.Pow(grass, 1.6f) * (1f - Edge(18f, 30f, slope)) * 110f);
        if (t > 14) { a1[y, x] = t; plantedTall++; }
    }
// bare soil on the steep slopes: the grass layer gives way to the soil layer there, in an uneven edge
for (int ay = 0; ay < data.alphamapHeight; ay++)
    for (int ax = 0; ax < an; ax++)
    {
        float u = (ax + 0.5f) / an, v = (ay + 0.5f) / data.alphamapHeight;
        float slope = data.GetSteepness(u, v);
        float wx = u * data.size.x, wz = v * data.size.z;
        float wear = Edge(20f, 36f, slope + (UnityEngine.Mathf.PerlinNoise(wx * 0.21f + 9f, wz * 0.21f + 77f) - 0.5f) * 14f) * 0.75f;
        // and worn patches on the flatter ground
        wear = UnityEngine.Mathf.Max(wear, Edge(0.66f, 0.80f, UnityEngine.Mathf.PerlinNoise(wx * 0.11f + 301f, wz * 0.11f + 133f)) * 0.55f);
        float g = alpha[ay, ax, 0];
        if (wear <= 0.01f || g <= 0.01f) continue;
        float move = g * wear;
        alpha[ay, ax, 0] = g - move; alpha[ay, ax, 1] += move; bared++; changedAlpha = true;
    }
if (changedAlpha) data.SetAlphamaps(0, 0, alpha);
// the detail maps follow the ground as it now is: no grass where the soil took over
for (int y = 0; y < n; y++)
    for (int x = 0; x < n; x++)
    {
        int ax = UnityEngine.Mathf.Min(an - 1, (int)((x + 0.5f) / n * an)), ay = UnityEngine.Mathf.Min(data.alphamapHeight - 1, (int)((y + 0.5f) / n * data.alphamapHeight));
        float g = alpha[ay, ax, 0];
        if (g < 0.45f) { a0[y, x] = 0; a1[y, x] = 0; }
        // nothing grows on the trampled floor of the trench or on its sandbag walls
        float tx = data.size.x * (x + 0.5f) / n + terrain.transform.position.x, tz = data.size.z * (y + 0.5f) / n + terrain.transform.position.z;
        if (tx > -24f && tx < 27f && tz > -6.5f && tz < 5.5f) { a0[y, x] = 0; a1[y, x] = 0; }
    }
data.SetDetailLayer(0, 0, 0, a0);
data.SetDetailLayer(0, 0, 1, a1);
UnityEditor.EditorUtility.SetDirty(data); // the scene itself is not touched: everything lives in the terrain data and the layer
UnityEditor.AssetDatabase.SaveAssets();
return "grass cells " + cells + ", dry grass in " + planted + ", tall in " + plantedTall + ", alphamap texels moved to soil " + bared;
