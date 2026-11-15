using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    static class BazookaBoreSetup
    {
        const string Folder = "Assets/_Project/Art/Models/Weapons/Bazooka/";
        const string SourcePath = Folder + "SM_WP_Bazooka.fbx";
        const string BodyPath = Folder + "SM_WP_Bazooka_FPBody.asset";
        const string BorePath = Folder + "SM_WP_Bazooka_FPBore.asset";
        const float BoreY = .004f;

        internal static void OpenRear(Transform launcher, Material material)
        {
            var body = launcher.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(filter =>
            {
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                return path == BodyPath || path == SourcePath;
            });
            if (body == null) return;
            var source = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<Mesh>().Single();
            var vertices = source.vertices;
            var opened = Object.Instantiate(source);
            opened.name = "SM_WP_Bazooka_FPBody";
            int removed = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var sourceIndices = source.GetTriangles(s);
                var remaining = new List<int>(sourceIndices.Length);
                for (int i = 0; i < sourceIndices.Length; i += 3)
                {
                    int a = sourceIndices[i], b = sourceIndices[i + 1], c = sourceIndices[i + 2];
                    if (vertices[a].z < -.480f && vertices[b].z < -.480f && vertices[c].z < -.480f
                        && (InsideOpening(vertices[a]) || InsideOpening(vertices[b]) || InsideOpening(vertices[c])))
                    {
                        removed++;
                        continue;
                    }
                    remaining.Add(a);
                    remaining.Add(b);
                    remaining.Add(c);
                }
                opened.SetTriangles(remaining, s, calculateBounds: false);
            }
            if (removed < 100)
            {
                Object.DestroyImmediate(opened);
                throw new InvalidOperationException($"바주카 후방 덮개를 찾지 못했습니다. 제거한 삼각형 {removed}개.");
            }
            body.sharedMesh = SaveMesh(opened, BodyPath);

            var bore = SetupUtil.Child(body.transform, "Rear_Bore");
            SetupUtil.Ensure<MeshFilter>(bore).sharedMesh = BoreMesh();
            SetupUtil.Ensure<MeshRenderer>(bore).sharedMaterial = material;
        }

        static bool InsideOpening(Vector3 vertex)
        {
            const float radius = .056f;
            float y = vertex.y - BoreY;
            return vertex.x * vertex.x + y * y < radius * radius;
        }

        static Mesh BoreMesh()
        {
            const int sides = 40;
            const float rear = -.582f, inside = -.484f;
            const float innerRadius = .055f, outerRadius = .084f;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                var nearA = At(innerRadius, rear, i);
                var nearB = At(innerRadius, rear, i + 1);
                var farA = At(innerRadius, inside, i);
                var farB = At(innerRadius, inside, i + 1);
                Quad(nearA, nearB, At(outerRadius, rear + .0004f, i + 1), At(outerRadius, rear + .0004f, i));
                Quad(nearA, farA, farB, nearB);
                int cap = vertices.Count;
                vertices.AddRange(new[] { new Vector3(0f, BoreY, inside), farB, farA });
                indices.AddRange(new[] { cap, cap + 1, cap + 2 });
            }

            Vector3 At(float radius, float z, int i)
            {
                float angle = i * 2f * Mathf.PI / sides;
                return new Vector3(radius * Mathf.Cos(angle), BoreY + radius * Mathf.Sin(angle), z);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            var mesh = new Mesh { name = "SM_WP_Bazooka_FPBore" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, BorePath);
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
