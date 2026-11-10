using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 박용팔 skin fix authored in Blender (tools/park_skin_fix, 2026-11-10): smoothed shirt normals and
    // shoulder/arm weights, applied by Unity vertex index to a copy of his current mesh. The source mesh and FBX are
    // never overwritten; running it again rebuilds the same asset.
    public static class ParkSkinFix
    {
        const string Actor = "[Map]/Generated/People/ParkYongpal";
        const string Folder = "Assets/_Project/Art/SurfaceDetail20261110";
        const string AssetPath = Folder + "/ROK_ParkYongpal_SkinFix.asset";
        const string SourceMesh = "ROK_ParkYongpal_SmoothArmSkin";

        static string DataDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "tools", "park_skin_fix"));

        [MenuItem("FiveRivers/Characters/Apply Park Skin Fix (Blender)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[ParkSkinFix] Edit mode only."); return; }
            var actor = GameObject.Find(Actor);
            var renderer = actor != null ? actor.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
            if (renderer == null) { Debug.LogError("[ParkSkinFix] " + Actor + " skinned mesh not found."); return; }
            var source = renderer.sharedMesh;
            // re-running starts again from the delivered mesh the Blender data was made from
            if (source.name != SourceMesh)
                source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/SurfaceDetail20261030/" + SourceMesh + ".asset");
            if (source == null) { Debug.LogError("[ParkSkinFix] source mesh " + SourceMesh + " missing."); return; }

            var weights = ReadWeights(Path.Combine(DataDir, "weights_fixed.bin"), source.vertexCount);
            var normals = ReadNormals(Path.Combine(DataDir, "normals_fixed.bin"), source.vertexCount);
            if (weights == null || normals == null) return;
            if (source.bindposes.Length != renderer.bones.Length) { Debug.LogError("[ParkSkinFix] bone count mismatch."); return; }

            var mesh = Object.Instantiate(source);
            var oldTangents = mesh.tangents;
            mesh.boneWeights = weights;
            mesh.normals = normals;
            // tangents follow the new shirt normals (sub-mesh 0); every other slot keeps its delivered tangents
            mesh.RecalculateTangents();
            if (oldTangents.Length == mesh.vertexCount)
            {
                var shirt = new System.Collections.Generic.HashSet<int>(source.GetTriangles(0));
                var updated = mesh.tangents;
                for (int i = 0; i < updated.Length; i++) if (!shirt.Contains(i)) updated[i] = oldTangents[i];
                mesh.tangents = updated;
            }
            mesh.name = "ROK_ParkYongpal_SkinFix";
            Directory.CreateDirectory(Folder);
            var previous = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
            if (previous == null) AssetDatabase.CreateAsset(mesh, AssetPath);
            else { EditorUtility.CopySerialized(mesh, previous); Object.DestroyImmediate(mesh); mesh = previous; }
            AssetDatabase.SaveAssets();
            renderer.sharedMesh = mesh;
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(actor.scene);
            EditorSceneManager.SaveScene(actor.scene);
            Debug.Log("[ParkSkinFix] applied: " + AssetPath + " vertices=" + mesh.vertexCount);
        }

        static BoneWeight[] ReadWeights(string path, int count)
        {
            if (!File.Exists(path)) { Debug.LogError("[ParkSkinFix] missing " + path); return null; }
            using var r = new BinaryReader(File.OpenRead(path));
            int n = r.ReadInt32();
            if (n != count) { Debug.LogError($"[ParkSkinFix] weights for {n} vertices, mesh has {count}."); return null; }
            var index = new int[n * 4];
            for (int i = 0; i < index.Length; i++) index[i] = r.ReadInt32();
            var result = new BoneWeight[n];
            for (int v = 0; v < n; v++)
            {
                float w0 = r.ReadSingle(), w1 = r.ReadSingle(), w2 = r.ReadSingle(), w3 = r.ReadSingle();
                result[v] = new BoneWeight
                {
                    boneIndex0 = index[v * 4], boneIndex1 = index[v * 4 + 1], boneIndex2 = index[v * 4 + 2], boneIndex3 = index[v * 4 + 3],
                    weight0 = w0, weight1 = w1, weight2 = w2, weight3 = w3,
                };
            }
            return result;
        }

        static Vector3[] ReadNormals(string path, int count)
        {
            if (!File.Exists(path)) { Debug.LogError("[ParkSkinFix] missing " + path); return null; }
            using var r = new BinaryReader(File.OpenRead(path));
            int n = r.ReadInt32();
            if (n != count) { Debug.LogError($"[ParkSkinFix] normals for {n} vertices, mesh has {count}."); return null; }
            var result = new Vector3[n];
            for (int v = 0; v < n; v++) result[v] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            return result;
        }
    }
}
