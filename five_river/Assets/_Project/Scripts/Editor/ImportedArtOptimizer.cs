using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace FiveRivers.EditorTools
{
    public static class ImportedArtOptimizer
    {
        const string Scope = "Assets/_Project/Art/Weapons/M1Refined";
        const string ReportPath = "Temp/RefinedM1Optimization.json";

        [Serializable] public sealed class MeshRecord
        {
            public string path, name, excludedReason, geometryBefore, geometryAfter;
            public long localId, indicesBefore, indicesAfter, trianglesBefore, trianglesAfter, bytesBefore, bytesAfter;
            public int verticesBefore, verticesAfter, subMeshesBefore, subMeshesAfter;
            public bool readableBefore, readableAfter, changed, geometryPreserved;
            public Vector3 boundsCenterBefore, boundsSizeBefore, boundsCenterAfter, boundsSizeAfter;
        }
        [Serializable] public sealed class TextureRecord
        {
            public string path, sourceHashBefore, sourceHashAfter;
            public bool readableBefore, readableAfter, changed, sourceUnchanged;
            public int widthBefore, heightBefore, widthAfter, heightAfter, maxSizeBefore, maxSizeAfter;
            public long bytesBefore, bytesAfter;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion, scope, utc, note;
            public bool success;
            public int meshCount, meshesChanged, texturesChanged;
            public long meshBytesBefore, meshBytesAfter, textureBytesBefore, textureBytesAfter;
            public List<MeshRecord> meshes = new List<MeshRecord>();
            public List<TextureRecord> textures = new List<TextureRecord>();
        }

        [MenuItem("FiveRivers/Optimize/Refined M1 CPU Readability")]
        public static void OptimizeM1()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before optimizing imported art.");
            if (!AssetDatabase.IsValidFolder(Scope)) throw new DirectoryNotFoundException(Scope);
            var report = new Report { unityVersion = Application.unityVersion, scope = Scope, utc = DateTime.UtcNow.ToString("O"),
                note = "Only persistent CPU readability flags were changed. LOD, geometry, materials and texture resolution/compression settings were retained. Runtime memory figures are observations in this Editor, not a standalone-build benchmark." };
            Directory.CreateDirectory("Temp");
            var protectedMeshes = FindProtectedMeshes();
            var changedPaths = new HashSet<string>();
            try
            {
                foreach (var path in AssetDatabase.FindAssets("t:Mesh", new[] { Scope }).Select(AssetDatabase.GUIDToAssetPath).Distinct())
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string _, out long localId);
                    var record = new MeshRecord { path = path, name = mesh.name, localId = localId };
                    Snapshot(mesh, record, false);
                    bool native = path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
                    if (protectedMeshes.Contains(mesh)) record.excludedReason = "Used by a SkinnedMeshRenderer or MeshCollider";
                    else if (!native) record.excludedReason = "ModelImporter asset; existing importer settings retained";
                    else if (!mesh.isReadable) record.excludedReason = "Already non-readable";
                    else
                    {
                        record.geometryBefore = GeometryHash(path);
                        if (string.IsNullOrEmpty(record.geometryBefore)) record.excludedReason = "No auditable serialized vertex/index buffer";
                        else
                        {
                            var serialized = new SerializedObject(mesh);
                            var readable = serialized.FindProperty("m_IsReadable");
                            if (readable == null) record.excludedReason = "Persistent readability property unavailable";
                            else
                            {
                                readable.boolValue = false;
                                serialized.ApplyModifiedPropertiesWithoutUndo();
                                EditorUtility.SetDirty(mesh);
                                record.changed = true;
                                changedPaths.Add(path);
                            }
                        }
                    }
                    report.meshes.Add(record);
                }
                AssetDatabase.SaveAssets();
                // 실제 재임포트 뒤 플래그와 메시 버퍼를 다시 확인한다. UploadMeshData로 임시 상태만 바꾸지 않는다.
                foreach (var path in changedPaths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                foreach (var record in report.meshes)
                {
                    var mesh = AssetDatabase.LoadAllAssetsAtPath(record.path).OfType<Mesh>().First(m => LocalId(m) == record.localId);
                    Snapshot(mesh, record, true);
                    record.geometryAfter = record.changed ? GeometryHash(record.path) : record.geometryBefore;
                    record.geometryPreserved = record.verticesBefore == record.verticesAfter && record.indicesBefore == record.indicesAfter
                        && record.subMeshesBefore == record.subMeshesAfter && record.boundsCenterBefore == record.boundsCenterAfter
                        && record.boundsSizeBefore == record.boundsSizeAfter
                        && (!record.changed || record.geometryBefore == record.geometryAfter);
                    if (!record.geometryPreserved || (record.changed && record.readableAfter))
                        throw new InvalidOperationException("Mesh optimization verification failed: " + record.path);
                }

                foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { Scope }).Select(AssetDatabase.GUIDToAssetPath).Distinct())
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (importer == null || texture == null) continue;
                    var record = new TextureRecord { path = path, readableBefore = texture.isReadable, widthBefore = texture.width,
                        heightBefore = texture.height, maxSizeBefore = importer.maxTextureSize, bytesBefore = Profiler.GetRuntimeMemorySizeLong(texture),
                        sourceHashBefore = FileHash(path) };
                    if (importer.isReadable)
                    {
                        importer.isReadable = false;
                        importer.SaveAndReimport();
                        record.changed = true;
                    }
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    record.readableAfter = texture.isReadable;
                    record.widthAfter = texture.width; record.heightAfter = texture.height;
                    record.maxSizeAfter = importer.maxTextureSize;
                    record.bytesAfter = Profiler.GetRuntimeMemorySizeLong(texture);
                    record.sourceHashAfter = FileHash(path);
                    record.sourceUnchanged = record.sourceHashBefore == record.sourceHashAfter;
                    report.textures.Add(record);
                    if (!record.sourceUnchanged || record.widthBefore != record.widthAfter || record.heightBefore != record.heightAfter
                        || record.maxSizeBefore != record.maxSizeAfter || (record.changed && record.readableAfter))
                        throw new InvalidOperationException("Texture optimization verification failed: " + path);
                }
                report.success = true;
            }
            finally
            {
                report.meshCount = report.meshes.Count;
                report.meshesChanged = report.meshes.Count(r => r.changed);
                report.texturesChanged = report.textures.Count(r => r.changed);
                report.meshBytesBefore = report.meshes.Sum(r => r.bytesBefore);
                report.meshBytesAfter = report.meshes.Sum(r => r.bytesAfter);
                report.textureBytesBefore = report.textures.Sum(r => r.bytesBefore);
                report.textureBytesAfter = report.textures.Sum(r => r.bytesAfter);
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            }
            Debug.Log($"[Optimize M1] Meshes {report.meshesChanged}/{report.meshCount}, textures {report.texturesChanged}; report: {ReportPath}");
        }

        static HashSet<Mesh> FindProtectedMeshes()
        {
            var result = new HashSet<Mesh>();
            void Inspect(GameObject root)
            {
                foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (renderer.sharedMesh != null) result.Add(renderer.sharedMesh);
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                    if (collider.sharedMesh != null) result.Add(collider.sharedMesh);
            }
            foreach (var path in AssetDatabase.FindAssets("t:GameObject", new[] { Scope }).Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) Inspect(root);
            }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isLoaded) foreach (var root in scene.GetRootGameObjects()) Inspect(root);
            }
            return result;
        }

        static long LocalId(Mesh mesh)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string _, out long id);
            return id;
        }
        static void Snapshot(Mesh mesh, MeshRecord record, bool after)
        {
            long indices = 0, triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                long count = mesh.GetIndexCount(i); indices += count;
                if (mesh.GetTopology(i) == MeshTopology.Triangles) triangles += count / 3;
            }
            if (!after)
            {
                record.verticesBefore = mesh.vertexCount; record.indicesBefore = indices; record.trianglesBefore = triangles;
                record.subMeshesBefore = mesh.subMeshCount; record.readableBefore = mesh.isReadable;
                record.bytesBefore = Profiler.GetRuntimeMemorySizeLong(mesh);
                record.boundsCenterBefore = mesh.bounds.center; record.boundsSizeBefore = mesh.bounds.size;
            }
            else
            {
                record.verticesAfter = mesh.vertexCount; record.indicesAfter = indices; record.trianglesAfter = triangles;
                record.subMeshesAfter = mesh.subMeshCount; record.readableAfter = mesh.isReadable;
                record.bytesAfter = Profiler.GetRuntimeMemorySizeLong(mesh);
                record.boundsCenterAfter = mesh.bounds.center; record.boundsSizeAfter = mesh.bounds.size;
            }
        }
        static string GeometryHash(string path)
        {
            string serialized = File.ReadAllText(path);
            var matches = Regex.Matches(serialized, @"(?:m_IndexBuffer|_typelessdata):[ \t]*([0-9a-fA-F]+)");
            if (matches.Count < 2) return null;
            return Hash(Encoding.UTF8.GetBytes(string.Join("|", matches.Cast<Match>().Select(m => m.Groups[1].Value))));
        }
        static string FileHash(string path) => Hash(File.ReadAllBytes(path));
        static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
