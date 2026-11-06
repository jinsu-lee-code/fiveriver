using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Repairs only the hollow rear of the nine old scanned field-cover visuals.
    // Gameplay roots, BoxColliders, terrain, LOD thresholds and combat markers are retained.
    public static class CoverSandbagRepair20261106
    {
        const string Source = "Assets/_Project/Art/MapRepair20261106/ScannedSackStack_20261106.fbx";
        const string Output = "Assets/_Project/Art/MapRepair20261106/FittedScanStack";
        const string MaterialPath = "Assets/_Project/Art/Materials/M_PR_Sandbag.mat";

        [MenuItem("FiveRivers/Repair/Main Trench Full Course Variation 20261106")]
        public static void ApplyMainOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play.");
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            Directory.CreateDirectory("Temp/Expanded_20261106");
            string backup = "Temp/Expanded_20261106/Dabudong_before_full_course_variation.unity";
            if (!File.Exists(backup)) File.Copy(EditorSceneManager.GetActiveScene().path, backup);
            int changed = VaryMainTrench();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            File.WriteAllText("Temp/Expanded_20261106/main_trench_full_variation.txt", "Changed " + changed + " main trench visuals only. Original UV/material, collision, ground height and exact mesh bounds retained. CP field covers untouched. Unique deterministic scan orientation and course-load patterns.");
            Debug.Log("[Main trench full-course variation] " + changed + " modules; scene left dirty for operator save.");
        }

        static int VaryMainTrench()
        {
            var root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Trench_Sandbag_Walls_20261105")?.gameObject;
            if (root == null) throw new InvalidOperationException("Missing original main trench.");
            const string originalPath = "Assets/_Project/Art/Models/Props/Sandbag/SM_PR_SandbagWall.fbx";
            var baseline = AssetDatabase.LoadAllAssetsAtPath(originalPath).OfType<Mesh>().Single(m => m.name == "SandbagWall");
            var originals = baseline.vertices;
            var oldBounds = baseline.bounds;
            var originalNormals = baseline.normals;
            int changed = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true).OrderBy(f => f.transform.parent.name))
            {
                if (filter.sharedMesh == null || !(filter.sharedMesh.name == "SandbagWall" || filter.sharedMesh.name.StartsWith("TrenchSack_LoadVar") || filter.sharedMesh.name.StartsWith("TrenchSack_FullVar"))) continue;
                string protectTransform = EditorJsonUtility.ToJson(filter.transform);
                var renderer = filter.GetComponent<Renderer>();
                var protectMaterials = renderer.sharedMaterials.ToArray();
                var up = filter.transform.InverseTransformDirection(Vector3.up);
                int vertical = Mathf.Abs(up.x) > Mathf.Abs(up.y) ? (Mathf.Abs(up.x) > Mathf.Abs(up.z) ? 0 : 2) : (Mathf.Abs(up.y) > Mathf.Abs(up.z) ? 1 : 2);
                float verticalSign = up[vertical] >= 0f ? 1f : -1f;
                int width = Enumerable.Range(0,3).Where(a => a != vertical).OrderByDescending(a => oldBounds.size[a]).First();
                int depth = Enumerable.Range(0,3).Single(a => a != vertical && a != width);
                uint hash = unchecked((uint)(changed + 27) * 747796405u + 2891336453u);
                hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
                hash = (hash >> 22) ^ hash;
                float phase = (hash & 0xffffffu) / 16777216f * Mathf.PI * 2f;
                float widthSign = (hash & 1u) == 0 ? 1f : -1f;
                float depthSign = (hash & 2u) == 0 ? 1f : -1f;
                Func<Vector3, Vector3> preliminary = p =>
                {
                    // Reorient the intact scan around its own center: no object movement or UV reprojecting.
                    p[width] = oldBounds.center[width] + (p[width] - oldBounds.center[width]) * widthSign;
                    p[depth] = oldBounds.center[depth] + (p[depth] - oldBounds.center[depth]) * depthSign;
                    float q = (p[vertical] - oldBounds.min[vertical]) / oldBounds.size[vertical];
                    if (verticalSign < 0f) q = 1f - q;
                    float u = (p[width] - oldBounds.min[width]) / oldBounds.size[width];
                    float joinAnchor = Mathf.Sin(Mathf.PI * u);
                    // Different length distribution in each course breaks the recurring diagonal gathered sack.
                    float shift = (.022f*Mathf.Sin(phase+q*8f)+.010f*Mathf.Cos(phase*1.3f+q*12.6f))*joinAnchor
                        + .009f*Mathf.Cos(phase+q*9f)*Mathf.Sin(2f*Mathf.PI*u);
                    p[width] = Mathf.LerpUnclamped(oldBounds.min[width],oldBounds.max[width],u+shift);
                    float bearing = Mathf.SmoothStep(0f,1f,(q-.05f)/.10f);
                    float lower = 1f-Mathf.SmoothStep(0f,1f,(q-.48f)/.12f);
                    q += .014f*Mathf.Sin(3f*Mathf.PI*q)*Mathf.Sin(phase+u*7.1f)*joinAnchor*bearing*lower;
                    if (q > .56f)
                    {
                        float load = .82f + .17f * Mathf.Sin(phase + u * 8.7f);
                        q = .56f + (q - .56f) * load;
                    }
                    if (verticalSign < 0f) q = 1f - q;
                    p[vertical] = Mathf.LerpUnclamped(oldBounds.min[vertical],oldBounds.max[vertical],q);
                    return p;
                };
                float maxTop = originals.Select(preliminary).Max(p => verticalSign > 0f ? (p[vertical]-oldBounds.min[vertical])/oldBounds.size[vertical] : (oldBounds.max[vertical]-p[vertical])/oldBounds.size[vertical]);
                Func<Vector3, Vector3> deform = p =>
                {
                    var result=preliminary(p);
                    float q=verticalSign>0f?(result[vertical]-oldBounds.min[vertical])/oldBounds.size[vertical]:(oldBounds.max[vertical]-result[vertical])/oldBounds.size[vertical];
                    if(q>.56f)q=.56f+(q-.56f)*.44f/(maxTop-.56f);
                    if(verticalSign<0f)q=1f-q;
                    result[vertical]=Mathf.LerpUnclamped(oldBounds.min[vertical],oldBounds.max[vertical],q);
                    return result;
                };
                var mesh=Object.Instantiate(baseline);
                mesh.name="TrenchSack_FullVar_"+changed;
                mesh.vertices=originals.Select(deform).ToArray();
                var normals=new Vector3[originalNormals.Length];
                for(int i=0;i<normals.Length;i++)
                {
                    var p=originals[i];var jacobian=Matrix4x4.identity;const float step=.0001f;
                    for(int axis=0;axis<3;axis++)
                    {
                        var delta=Vector3.zero;delta[axis]=step;
                        var derivative=(deform(p+delta)-deform(p-delta))/(2f*step);
                        jacobian.SetColumn(axis,new Vector4(derivative.x,derivative.y,derivative.z,0f));
                    }
                    normals[i]=jacobian.inverse.transpose.MultiplyVector(originalNormals[i]).normalized;
                }
                mesh.normals=normals;
                if(widthSign*depthSign<0f)
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var triangles=mesh.GetTriangles(sub);
                    for(int i=0;i<triangles.Length;i+=3){int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}
                    mesh.SetTriangles(triangles,sub,false);
                }
                mesh.RecalculateTangents();mesh.RecalculateBounds();
                if((mesh.bounds.size-oldBounds.size).sqrMagnitude>.000001f||(mesh.bounds.center-oldBounds.center).sqrMagnitude>.000001f)
                    throw new InvalidOperationException("Main trench visual envelope changed.");
                string path=Output+"/MainTrenchFull_"+changed.ToString("D2")+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
                else{EditorUtility.CopySerialized(mesh,saved);saved.vertices=mesh.vertices;saved.normals=mesh.normals;saved.tangents=mesh.tangents;saved.triangles=mesh.triangles;saved.RecalculateBounds();saved.UploadMeshData(false);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);}
                AssetDatabase.SaveAssetIfDirty(saved);
                Undo.RecordObject(filter,"Vary visible sack courses");filter.sharedMesh=saved;EditorUtility.SetDirty(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                if(protectTransform!=EditorJsonUtility.ToJson(filter.transform)||!protectMaterials.SequenceEqual(renderer.sharedMaterials))
                    throw new InvalidOperationException("Protected trench placement or fabric changed.");
                changed++;
            }
            if(changed!=46)throw new InvalidOperationException("Unexpected main trench count: "+changed);
            return changed;
        }
        [MenuItem("FiveRivers/Repair/Closed Field Sandbag Visuals 20261106")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before repairing cover visuals.");
            var map = GameObject.Find("/[Map]/Dabudong_Imported");
            if (map == null) throw new InvalidOperationException("Expected current Dabudong_Imported map.");
            var terrain = map.GetComponentInChildren<Terrain>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (terrain == null || material == null) throw new InvalidOperationException("Missing existing terrain or sandbag material.");
            var importer = AssetImporter.GetAtPath(Source) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing unique repaired sandbag source.");
            if (!importer.isReadable || importer.importAnimation)
            {
                importer.isReadable = true;
                importer.importAnimation = false;
                importer.SaveAndReimport();
            }
            var source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
            source.hideFlags = HideFlags.HideAndDontSave;
            source.transform.position = Vector3.zero; // Preserve the FBX importer axis rotation and scale.
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            Directory.CreateDirectory("Temp/Expanded_20261106");
            if (!File.Exists("Temp/Expanded_20261106/Dabudong_before_cover_repair.unity"))
                File.Copy(map.scene.path, "Temp/Expanded_20261106/Dabudong_before_cover_repair.unity");
            int groups = 0, meshes = 0;
            var report = new List<string>();
            try
            {
                var sources = source.GetComponentsInChildren<MeshFilter>().OrderBy(f => f.name).ToArray();
                if (sources.Length != 3) throw new InvalidOperationException("Expected exactly three repaired LODs.");
                var sourcePoints = sources[0].sharedMesh.vertices.Select(sources[0].transform.TransformPoint).ToArray();
                var sourceBounds = new Bounds(sourcePoints[0], Vector3.zero);
                foreach (var v in sourcePoints) sourceBounds.Encapsulate(v);
                if (sourceBounds.size.y < sourceBounds.size.z)
                    throw new InvalidOperationException("Sandbag source is not upright: " + sourceBounds.size);
                foreach (var lod in map.GetComponentsInChildren<LODGroup>(true))
                {
                    var levels = lod.GetLODs();
                    if (levels.Length == 0 || !levels[0].renderers.Any(r => r != null && r.name.StartsWith("Sandbag_Wall", StringComparison.Ordinal))) continue;
                    var owner = lod.transform;
                    while (owner != null && !owner.name.Contains("_Sandbags_")) owner = owner.parent;
                    if (owner == null) continue;
                    string cp = owner.name.Split('_')[0];
                    var cover = map.transform.Find("M01_Greybox/Cover/" + cp)?.GetComponent<BoxCollider>();
                    if (cover == null) throw new InvalidOperationException("Missing original cover: " + cp);
                    if (lod.GetComponentsInChildren<Collider>(true).Length != 0)
                        throw new InvalidOperationException("Visual unexpectedly owns a collider: " + cp);
                    string colliderBefore = EditorJsonUtility.ToJson(cover);
                    string rootBefore = EditorJsonUtility.ToJson(owner);
                    var size = cover.size;
                    var scale = new Vector3(size.x / sourceBounds.size.x, size.y / sourceBounds.size.y, size.z / sourceBounds.size.z);
                    for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
                    foreach (var renderer in levels[levelIndex].renderers)
                    {
                        if (renderer == null) continue;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null) continue;
                        var input = sources[Mathf.Min(levelIndex, sources.Length - 1)];
                        var mesh = Object.Instantiate(input.sharedMesh);
                        mesh.name = cp + "_ClosedSandbags_Courses_LOD" + levelIndex + "_20261106";
                        var vertices = mesh.vertices;
                        var normals = mesh.normals;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            var p = input.transform.TransformPoint(vertices[i]);
                            var local = cover.center + Vector3.Scale(p - sourceBounds.center, scale);
                            var world = cover.transform.TransformPoint(local);
                            float t = (p.y - sourceBounds.min.y) / sourceBounds.size.y;
                            float blend = 1f - Mathf.SmoothStep(0f, 1f, t / .28f);
                            var bottom = cover.transform.TransformPoint(new Vector3(local.x, cover.center.y - size.y * .5f, local.z));
                            world.y += (terrain.SampleHeight(world) + terrain.transform.position.y - .015f - bottom.y) * blend;
                            vertices[i] = filter.transform.InverseTransformPoint(world);
                            if (normals.Length == vertices.Length)
                            {
                                var n = input.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]);
                                n = new Vector3(n.x / scale.x, n.y / scale.y, n.z / scale.z);
                                n = cover.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(n);
                                normals[i] = filter.transform.localToWorldMatrix.transpose.MultiplyVector(n).normalized;
                            }
                        }
                        mesh.vertices = vertices;
                        if (normals.Length == vertices.Length) mesh.normals = normals; else mesh.RecalculateNormals();
                        mesh.RecalculateTangents();
                        mesh.RecalculateBounds();
                        string path = Output + "/" + cp + "_LOD" + levelIndex + ".asset";
                        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                        if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                        else
                        {
                            EditorUtility.CopySerialized(mesh, saved);
                            saved.vertices = mesh.vertices;
                            saved.normals = mesh.normals;
                            saved.tangents = mesh.tangents;
                            saved.uv = mesh.uv;
                            saved.triangles = mesh.triangles;
                            saved.RecalculateBounds();
                            saved.UploadMeshData(false);
                            Object.DestroyImmediate(mesh);
                            EditorUtility.SetDirty(saved);
                        }
                        AssetDatabase.SaveAssetIfDirty(saved);
                        Undo.RecordObject(filter, "Repair hollow sandbag visual");
                        Undo.RecordObject(renderer, "Apply matching original sandbag UV material");
                        filter.sharedMesh = saved;
                                                renderer.sharedMaterials = new[] { material };
                        EditorUtility.SetDirty(filter);
                        EditorUtility.SetDirty(renderer);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                        meshes++;
                        report.Add(cp + " LOD" + levelIndex + " triangles=" + saved.triangles.Length / 3 + " source=" + Source);
                    }
                    lod.RecalculateBounds();
                    EditorUtility.SetDirty(lod);
                    if (colliderBefore != EditorJsonUtility.ToJson(cover) || rootBefore != EditorJsonUtility.ToJson(owner))
                        throw new InvalidOperationException("Protected cover changed: " + cp);
                    groups++;
                }
                if (groups != 9 || meshes != 27) throw new InvalidOperationException("Unexpected affected cover count: " + groups + "/" + meshes);
                int mainModules = VaryMainTrench();
                report.Add("Main trench: " + mainModules + " module visuals retain original worn-cloth materials, grounding/bearing courses and bounds; unique full-course scan orientation/load variants.");
                EditorSceneManager.MarkSceneDirty(map.scene);
                report.Insert(0, "Repaired " + groups + " cover visuals / " + meshes + " LOD meshes. Collider components, placement roots and LOD thresholds unchanged. Scene remains dirty for the operator to save.");
                File.WriteAllLines("Temp/Expanded_20261106/cover_repair.txt", report);
                Debug.Log("[Cover repair] " + report[0]);
            }
            finally { Object.DestroyImmediate(source); }
        }
    }
}






