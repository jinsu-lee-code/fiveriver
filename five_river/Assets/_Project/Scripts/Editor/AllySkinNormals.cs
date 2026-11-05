using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // Smooth only the delivered arm-skin slot. Positions, weights, UV seams and cloth
    // normals stay intact; the source FBX and its mesh are never overwritten.
    public static class AllySkinNormals
    {
        const string Folder = "Assets/_Project/Art/SurfaceDetail20261030";

        public static void Apply(GameObject actor)
        {
            Directory.CreateDirectory(Folder);
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var source = renderer.sharedMesh;
                if (source == null) continue;
                var included = new HashSet<int>();
                var excluded = new HashSet<int>();
                var triangles = new List<int>();
                for (int slot = 0; slot < source.subMeshCount; slot++)
                {
                    bool skin = slot < renderer.sharedMaterials.Length && renderer.sharedMaterials[slot] != null
                        && renderer.sharedMaterials[slot].name.IndexOf("Arm_Skin_NoNails", StringComparison.OrdinalIgnoreCase) >= 0;
                    var indices = source.GetTriangles(slot);
                    foreach (int index in indices) (skin ? included : excluded).Add(index);
                    if (skin) triangles.AddRange(indices);
                }
                if (included.Count == 0) continue;
                var vertices = source.vertices;
                var sums = new Dictionary<Vector3Int, Vector3>();
                Vector3Int Key(int index)
                {
                    var p = vertices[index] * 100000f;
                    return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                }
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    foreach (int index in new[] { a, b, c })
                    {
                        var key = Key(index);
                        sums.TryGetValue(key, out var sum);
                        sums[key] = sum + face;
                    }
                }
                var mesh = Object.Instantiate(source);
                var normals = mesh.normals;
                if (normals.Length != vertices.Length) { Object.DestroyImmediate(mesh); continue; }
                var tangents = mesh.tangents;
                foreach (int index in included)
                    if (!excluded.Contains(index) && sums[Key(index)].sqrMagnitude > 1e-20f)
                    {
                        var sum = sums[Key(index)];
                        // Vector3.normalized itself drops tiny vectors to zero at 1e-5.
                        // Mesh-space triangle areas are smaller than that on this delivery.
                        normals[index] = sum / Mathf.Sqrt(sum.sqrMagnitude);
                    }
                mesh.normals = normals;
                mesh.RecalculateTangents();
                if (tangents.Length == vertices.Length)
                {
                    var updated = mesh.tangents;
                    for (int i = 0; i < updated.Length; i++)
                        if (!included.Contains(i) || excluded.Contains(i)) updated[i] = tangents[i];
                    mesh.tangents = updated;
                }
                mesh.name = "ROK_" + actor.name + "_SmoothArmSkin";
                string path = Folder + "/" + mesh.name + ".asset";
                var previous = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (previous == null) AssetDatabase.CreateAsset(mesh, path);
                else { EditorUtility.CopySerialized(mesh, previous); Object.DestroyImmediate(mesh); mesh = previous; }
                renderer.sharedMesh = mesh;
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                Debug.Log("[Allies] Smoothed arm-skin normals only: " + actor.name + " vertices=" + included.Count);
            }
        }
    }
}
