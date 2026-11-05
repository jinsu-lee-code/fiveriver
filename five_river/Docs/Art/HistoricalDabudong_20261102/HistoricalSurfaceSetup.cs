using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class HistoricalSurfaceSetup
{
    const string Output = "C:/Users/mbc/Documents/ChatGPT/다섯개의강/HistoricalDabudong_20261102";
    const string Baseline = "Assets/_Project/Art/PhotoGround20261102/Frontline_PhotoPatch.asset";
    const string Derived = "Assets/_Project/Art/HistoricalDabudong20261102/Frontline_HistoricalSurface.asset";
    static Terrain Ground() => Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Single(t => t.terrainData.size.x == 135f);
    static string HeightHash(TerrainData data)
    {
        var values = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        var bytes = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }
    static string Placements() => Newtonsoft.Json.JsonConvert.SerializeObject(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(t => t.gameObject.scene.IsValid() && (t.gameObject.hideFlags & HideFlags.HideAndDontSave) == 0)
        .OrderBy(t => t.GetInstanceID()).Select(t => new { id = t.GetInstanceID(), p = new[] { t.position.x, t.position.y, t.position.z },
            r = new[] { t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w }, s = new[] { t.localScale.x, t.localScale.y, t.localScale.z } }));
    static void Assign(Object owner, TerrainData data)
    {
        Undo.RecordObject(owner, "Historical road surface only");
        var serialized = new SerializedObject(owner);
        var property = serialized.FindProperty("m_TerrainData");
        if (property == null) throw new Exception("Terrain data property missing: " + owner.GetType().Name);
        property.objectReferenceValue = data;
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        EditorUtility.SetDirty(owner);
    }
    public static string Main(string mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit mode required.");
        Directory.CreateDirectory(Output);
        if (mode.StartsWith("capture_")) { Capture(mode.Substring(8)); return mode; }
        if (mode == "persist")
        {
            var terrain = Ground();
            var persistedData = AssetDatabase.LoadAssetAtPath<TerrainData>(Derived);
            if (terrain.terrainData != persistedData || terrain.GetComponent<TerrainCollider>().terrainData != persistedData) throw new Exception("Reference mismatch before save.");
            var path = terrain.gameObject.scene.path;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(path);
            terrain = Ground();
            bool persisted = terrain.terrainData == persistedData && terrain.GetComponent<TerrainCollider>().terrainData == persistedData;
            if (!persisted) throw new Exception("Historical terrain reference did not persist on scene reopen.");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { persisted, renderData = AssetDatabase.GetAssetPath(terrain.terrainData),
                colliderData = AssetDatabase.GetAssetPath(terrain.GetComponent<TerrainCollider>().terrainData), layers = persistedData.terrainLayers.Length,
                heightHash = HeightHash(persistedData), dirty = terrain.gameObject.scene.isDirty }, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(Output + "/persistence.json", json);
            return json;
        }
        if (mode != "apply") throw new Exception("Unknown mode.");
        var ground = Ground();
        var baseline = AssetDatabase.LoadAssetAtPath<TerrainData>(Baseline);
        if (baseline == null || baseline.terrainLayers.Length != 6) throw new Exception("Reviewed photo baseline missing.");
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(Derived) != null) throw new Exception("Historical patch already exists; inspect instead of overwriting.");
        string beforeHash = HeightHash(ground.terrainData), beforePlacements = Placements();
        if (beforeHash != HeightHash(baseline)) throw new Exception("Photo baseline height differs from current map.");
        File.Copy(ground.gameObject.scene.path, Output + "/Dabudong_before_history.unity", false);
        var data = Object.Instantiate(baseline);
        data.name = "Frontline_HistoricalSurface";
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/HistoricalDabudong20261102"))
            AssetDatabase.CreateFolder("Assets/_Project/Art", "HistoricalDabudong20261102");
        AssetDatabase.CreateAsset(data, Derived);
        int width = data.alphamapWidth, height = data.alphamapHeight;
        var alpha = baseline.GetAlphamaps(0, 0, width, height);
        int soil = Array.FindIndex(data.terrainLayers, l => l.name == "TL_Claude_Soil");
        int mud = Array.FindIndex(data.terrainLayers, l => l.name == "TL_Claude_Mud");
        int gravel = Array.FindIndex(data.terrainLayers, l => l.name == "TL_Photo_RockyTrail");
        if (soil < 0 || mud < 0 || gravel < 0) throw new Exception("Expected surface layers missing.");
        var points = Object.FindFirstObjectByType<FiveRivers.Core.LevelPoints>();
        var start = points.tankStart.position;
        var end = points.tankHold.position;
        int changed = 0;
        float maxSumError = 0f;
        for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
        {
            float wx = ground.transform.position.x + (x + .5f) / width * data.size.x;
            float wz = ground.transform.position.z + (z + .5f) / height * data.size.z;
            float center = end.x + (wz - end.z) * (start.x - end.x) / (start.z - end.z);
            float distance = Mathf.Abs(wx - center);
            float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 2.8f, distance));
            float sideFade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.6f, 3.5f, distance));
            float endFade = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, 24, wz))
                * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(104, 112, wz)));
            // Reuse the existing mud-road mask. No new road footprint or track grooves.
            float blend = alpha[z, x, mud] * sideFade * endFade;
            if (blend <= .0001f) continue;
            float variation = .7f + .3f * Mathf.PerlinNoise(wx * .63f + 23, wz * .39f + 17);
            float coarse = Mathf.Lerp(.045f, .24f, edge) * variation;
            for (int k = 0; k < data.alphamapLayers; k++) alpha[z, x, k] *= 1f - blend;
            alpha[z, x, soil] += (1f - coarse - .18f) * blend;
            alpha[z, x, mud] += .18f * blend;
            alpha[z, x, gravel] += coarse * blend;
            float sum = 0; for (int k = 0; k < data.alphamapLayers; k++) sum += alpha[z, x, k];
            maxSumError = Mathf.Max(maxSumError, Mathf.Abs(1f - sum));
            changed++;
        }
        data.SetAlphamaps(0, 0, alpha);
        data.SetBaseMapDirty();
        if (HeightHash(data) != beforeHash) throw new Exception("Height preservation failed.");
        Assign(ground, data);
        Assign(ground.GetComponent<TerrainCollider>(), data);
        ground.Flush();
        bool placementsUnchanged = Placements() == beforePlacements;
        if (!placementsUnchanged) throw new Exception("Scene placement changed.");
        EditorSceneManager.MarkSceneDirty(ground.gameObject.scene);
        AssetDatabase.SaveAssets();
        var result = Newtonsoft.Json.JsonConvert.SerializeObject(new { source = "AWM P00716.048, 1950 near Tabu Dong; NARA 21988, 1950-09-02",
            method = "Historical visual reference with existing scanned PBR materials; not historical-photo 3D reconstruction",
            baseline = Baseline, derived = Derived, changedRoadPixels = changed, totalPixels = width * height, maxAlphaSumError = maxSumError,
            heightBefore = beforeHash, heightAfter = HeightHash(data), placementsUnchanged,
            roadFootprint = "Existing mud-road mask; bounded around unchanged tank start/hold centerline; z18..112",
            interpretation = "Finer soil center, sparse coarser edge. Blend weights are an artistic choice, not values measured from monochrome photographs.",
            noNewGeometryOrModels = true, noHistoricalImageUsedAsGameTexture = true, sceneSaved = false }, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(Output + "/application.json", result);
        return result;
    }
    static void Capture(string label)
    {
        bool detail = label.Contains("detail");
        var source = Camera.main;
        var go = new GameObject("Historical surface review") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        camera.CopyFrom(source); camera.enabled = false; camera.fieldOfView = detail ? 61f : 70f;
        camera.cullingMask &= ~((1 << 31) | (1 << 5)); camera.nearClipPlane = .03f; camera.aspect = 16f / 9f;
        var eye = detail ? new Vector3(42f, 1.8f, 44f) : new Vector3(28f, 1.8f, -6f);
        var target = detail ? new Vector3(43.2f, .05f, 57f) : new Vector3(42f, 1.2f, 35f);
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
        var additional = camera.GetUniversalAdditionalCameraData(); additional.renderPostProcessing = true;
        additional.volumeLayerMask = source.GetUniversalAdditionalCameraData().volumeLayerMask;
        var rt = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var old = RenderTexture.active; var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + label + ".png", texture.EncodeToPNG());
        }
        finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture); Object.DestroyImmediate(go); }
    }
}
