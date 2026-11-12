using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace FiveRivers.Combat
{
    // Turning wheels and a running track belt for the T-34 (2026-11-12). The model's TrackL / TrackR are each one
    // fused mesh (wheels, sprocket, idler and belt together), so this splits them when the scene loads:
    //   wheels   every triangle inside a wheel's disc becomes its own small mesh on a child that turns on the axle
    //            (the disc stops just short of the tyre's rim: the outer ring is round, so it can stand still)
    //   belt     every triangle in the thin band round the loop becomes one mesh whose vertices are carried round
    //            the loop's centre line (the belt has no repeating link in its geometry, so it really goes round)
    //   the rest stays where it is
    // Wheel discs and the loop's centre line were measured on the mesh (source/vehicles/t34_running_gear) and are
    // read from Resources/Vehicles/T34_RunningGear.json. Each track is driven by the distance its own side of the
    // hull actually travels, so it works on the approach, in the entrance sequence and when the tank turns on the
    // spot (one side forwards, one back), and stands still when the tank does. Added by TankController.
    public sealed class TankRunningGear : MonoBehaviour
    {
        public const string Resource = "Vehicles/T34_RunningGear";
        const int WheelStride = 4;      // z, y, radius of the disc that turns, radius the belt runs on (tank-body space)
        const int Table = 512;          // samples of the loop's centre line
        const float SeamOverlap = 0.012f; // metres either side of a wheel disc's edge
        const float Teleport = 1.5f;    // a jump of more than this in one frame is a placement, not driving

        [Serializable]
        sealed class Side
        {
            public float length;
            public float[] loop;        // z, y pairs round the loop, tank-body space
        }

        [Serializable]
        sealed class Data
        {
            public float[] wheels;
            public float beltHalf, beltInside, wheelInner, beltInner;
            public Side TrackL, TrackR;
        }

        sealed class Track
        {
            public Transform root;
            public Renderer source;
            public Material material;
            public readonly List<Renderer> parts = new List<Renderer>();
            public Transform[] wheels;
            public float[] wheelRadius;
            public Vector3 axle;                    // the hull's sideways axis in the track's own space
            public Renderer beltRenderer;
            public Mesh belt;
            public float length;
            public float[] loopZ, loopY, tangentZ, tangentY;   // Table + 1 samples, by distance round the loop
            public BeltJob job;                     // the same table and every belt vertex's place on the loop
            public JobHandle handle;
            public bool running;
            public Vector3 axisZ, axisY;            // hull forward and up in the track's space, with its scale
            public Vector3 unitZ, unitY, unitX;
            public float distance, drawn = float.NaN;
            public Vector3 last;
        }

        readonly List<Track> tracks = new List<Track>();
        Transform body;

        // milliseconds the last belt update took (all tracks that moved), for the debug window
        public static float LastUpdateMs { get; private set; }
        public int TrackCount => tracks.Count;
        public float Distance(int track) => track < tracks.Count ? tracks[track].distance : 0f;

        public void Build(Transform tankBody)
        {
            body = tankBody;
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return;
            var data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null || data.wheels == null) return;
            Add("Model/TrackL", data, data.TrackL, -1f);
            Add("Model/TrackR", data, data.TrackR, 1f);
        }

        void Add(string path, Data data, Side side, float sign)
        {
            var root = body.Find(path);
            var filter = root != null ? root.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || side == null || side.loop == null || side.loop.Length < 8) return;
            if (!mesh.isReadable)
            {
                Debug.LogWarning("[TankRunningGear] " + mesh.name + " is not readable (Read/Write on the model import): the tracks stay still.");
                return;
            }
            var track = new Track { root = root, source = root.GetComponent<Renderer>() };
            var toBody = body.worldToLocalMatrix * root.localToWorldMatrix;
            var toTrack = toBody.inverse;
            track.axisZ = toTrack.MultiplyVector(Vector3.forward);
            track.axisY = toTrack.MultiplyVector(Vector3.up);
            track.unitZ = track.axisZ.normalized;
            track.unitY = track.axisY.normalized;
            track.unitX = toTrack.MultiplyVector(Vector3.right).normalized;
            track.axle = track.unitX;
            BuildTable(track, side);

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var uv = mesh.uv;
            var triangles = mesh.triangles;
            var inBody = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) inBody[i] = toBody.MultiplyPoint3x4(vertices[i]);

            // which part each triangle belongs to: 0 the rest, 1.. a wheel, wheels + 1 the belt
            int wheels = data.wheels.Length / WheelStride;
            var nearest = NearestGrid(track);
            var groups = new List<int>[wheels + 2];
            for (int g = 0; g < groups.Length; g++) groups[g] = new List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var c = (inBody[triangles[t]] + inBody[triangles[t + 1]] + inBody[triangles[t + 2]]) / 3f;
                float x = c.x * sign;
                int group = 0;
                // the belt first: where a wheel stands on it the two are fused, and the belt must stay whole
                if (x > data.beltInner)
                {
                    Project(track, nearest, c.z, c.y, out _, out float along, out float outward);
                    if (outward < data.beltHalf && outward > -data.beltInside && Mathf.Abs(along) < data.beltHalf) group = wheels + 1;
                }
                if (group == 0 && x > data.wheelInner)
                    for (int w = 0; w < wheels; w++)
                    {
                        float dz = c.z - data.wheels[w * WheelStride], dy = c.y - data.wheels[w * WheelStride + 1];
                        float r = data.wheels[w * WheelStride + 2], d = Mathf.Sqrt(dz * dz + dy * dy);
                        if (d >= r + SeamOverlap) continue;
                        group = w + 1;
                        // triangles on the seam are drawn by both sides of it, so no gap opens as the wheel turns
                        if (d > r - SeamOverlap) groups[0].Add(t);
                        break;
                    }
                groups[group].Add(t);
            }

            var bounds = mesh.bounds;
            filter.sharedMesh = Part(mesh.name + "_Rest", groups[0], triangles, vertices, normals, tangents, uv, Vector3.zero, out _);
            track.wheels = new Transform[wheels];
            track.wheelRadius = new float[wheels];
            for (int w = 0; w < wheels; w++)
            {
                var centre = toTrack.MultiplyPoint3x4(new Vector3(sign * 1.3f, data.wheels[w * WheelStride + 1], data.wheels[w * WheelStride]));
                var wheelMesh = Part(mesh.name + "_Wheel" + w, groups[w + 1], triangles, vertices, normals, tangents, uv, centre, out _);
                track.wheels[w] = Child(track, "Wheel" + w, wheelMesh, centre).transform;
                track.wheelRadius[w] = data.wheels[w * WheelStride + 3];
            }

            // the belt: each vertex remembers where it sits on the loop
            // (its moving half, position + normal + tangent, is one vertex stream that the job fills and is copied
            // to the mesh in one go; the texture coordinates sit in a second stream and never change)
            var whole = Part(mesh.name + "_Belt", groups[wheels + 1], triangles, vertices, normals, tangents, uv, Vector3.zero, out var used);
            int count = used.Count;
            track.belt = new Mesh { name = whole.name, indexFormat = whole.indexFormat };
            track.belt.MarkDynamic();
            track.belt.SetVertexBufferParams(count,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1));
            track.belt.SetVertexBufferData(whole.uv, 0, 0, count, 1);
            track.belt.SetIndices(whole.triangles, MeshTopology.Triangles, 0, false);
            track.belt.bounds = bounds;
            Destroy(whole);
            var job = new BeltJob
            {
                loopZ = new NativeArray<float>(track.loopZ, Allocator.Persistent),
                loopY = new NativeArray<float>(track.loopY, Allocator.Persistent),
                tangentZ = new NativeArray<float>(track.tangentZ, Allocator.Persistent),
                tangentY = new NativeArray<float>(track.tangentY, Allocator.Persistent),
                place = new NativeArray<Vector3>(count, Allocator.Persistent),
                lateral = new NativeArray<Vector3>(count, Allocator.Persistent),
                normalIn = new NativeArray<Vector3>(count, Allocator.Persistent),
                tangentIn = new NativeArray<Vector4>(count, Allocator.Persistent),
                moved = new NativeArray<BeltVertex>(count, Allocator.Persistent),
                length = track.length,
                axisZ = track.axisZ, axisY = track.axisY, unitZ = track.unitZ, unitY = track.unitY, unitX = track.unitX,
            };
            for (int i = 0; i < count; i++)
            {
                int v = used[i];
                var p = inBody[v];
                Project(track, nearest, p.z, p.y, out float s, out float along, out float outward);
                Sample(track, s, out _, out _, out float tz, out float ty);
                job.place[i] = new Vector3(s, along, outward);
                job.lateral[i] = toTrack.MultiplyPoint3x4(new Vector3(p.x, 0f, 0f));
                var n = toBody.MultiplyVector(normals[v]).normalized;
                job.normalIn[i] = new Vector3(n.z * tz + n.y * ty, n.z * ty - n.y * tz, n.x);
                var tangent = toBody.MultiplyVector(tangents[v]).normalized;
                job.tangentIn[i] = new Vector4(tangent.z * tz + tangent.y * ty, tangent.z * ty - tangent.y * tz, tangent.x, tangents[v].w);
            }
            track.job = job;
            track.beltRenderer = Child(track, "Belt", track.belt, Vector3.zero);
            track.material = track.source.sharedMaterial;
            track.last = root.position;
            tracks.Add(track);
        }

        // The loop sampled evenly by distance, with its direction at each sample.
        static void BuildTable(Track track, Side side)
        {
            int n = side.loop.Length / 2;
            var cumulative = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float dz = side.loop[j * 2] - side.loop[i * 2], dy = side.loop[j * 2 + 1] - side.loop[i * 2 + 1];
                cumulative[i + 1] = cumulative[i] + Mathf.Sqrt(dz * dz + dy * dy);
            }
            track.length = cumulative[n];
            track.loopZ = new float[Table + 1];
            track.loopY = new float[Table + 1];
            track.tangentZ = new float[Table + 1];
            track.tangentY = new float[Table + 1];
            int segment = 0;
            for (int k = 0; k < Table; k++)
            {
                float s = track.length * k / Table;
                while (segment < n - 1 && cumulative[segment + 1] < s) segment++;
                float u = (s - cumulative[segment]) / Mathf.Max(1e-6f, cumulative[segment + 1] - cumulative[segment]);
                int a = segment, b = (segment + 1) % n;
                track.loopZ[k] = Mathf.Lerp(side.loop[a * 2], side.loop[b * 2], u);
                track.loopY[k] = Mathf.Lerp(side.loop[a * 2 + 1], side.loop[b * 2 + 1], u);
            }
            track.loopZ[Table] = track.loopZ[0];
            track.loopY[Table] = track.loopY[0];
            for (int k = 0; k <= Table; k++)
            {
                int before = (k + Table - 1) % Table, after = (k + 1) % Table;
                float dz = track.loopZ[after] - track.loopZ[before], dy = track.loopY[after] - track.loopY[before];
                float length = Mathf.Max(1e-6f, Mathf.Sqrt(dz * dz + dy * dy));
                track.tangentZ[k] = dz / length;
                track.tangentY[k] = dy / length;
            }
        }

        // Centre line and direction at a distance round the loop (0 .. length).
        static void Sample(Track track, float s, out float z, out float y, out float tz, out float ty)
        {
            float f = s / track.length * Table;
            int i = (int)f;
            if (i >= Table) i = Table - 1;
            float u = f - i;
            z = track.loopZ[i] + (track.loopZ[i + 1] - track.loopZ[i]) * u;
            y = track.loopY[i] + (track.loopY[i + 1] - track.loopY[i]) * u;
            tz = track.tangentZ[i] + (track.tangentZ[i + 1] - track.tangentZ[i]) * u;
            ty = track.tangentY[i] + (track.tangentY[i + 1] - track.tangentY[i]) * u;
        }

        const float GridZ0 = -2.9f, GridY0 = -0.3f, GridCell = 0.05f;
        const int GridW = 116, GridH = 36;

        // For each cell of a coarse side-view grid: the loop sample nearest its centre (so projecting a point only
        // has to look at a few samples around it).
        static int[] NearestGrid(Track track)
        {
            var grid = new int[GridW * GridH];
            for (int gy = 0; gy < GridH; gy++)
                for (int gz = 0; gz < GridW; gz++)
                {
                    float z = GridZ0 + (gz + 0.5f) * GridCell, y = GridY0 + (gy + 0.5f) * GridCell;
                    float best = float.MaxValue;
                    for (int k = 0; k < Table; k++)
                    {
                        float dz = track.loopZ[k] - z, dy = track.loopY[k] - y, d = dz * dz + dy * dy;
                        if (d < best) { best = d; grid[gy * GridW + gz] = k; }
                    }
                }
            return grid;
        }

        // Where a point (side view) sits on the loop: distance round it, and its offset along and out from the
        // centre line there. Sample(s) + along * tangent + outward * normal gives the point back.
        static void Project(Track track, int[] grid, float z, float y, out float s, out float along, out float outward)
        {
            int gz = Mathf.Clamp((int)((z - GridZ0) / GridCell), 0, GridW - 1), gy = Mathf.Clamp((int)((y - GridY0) / GridCell), 0, GridH - 1);
            int centre = grid[gy * GridW + gz];
            float best = float.MaxValue;
            s = 0f;
            for (int o = -6; o <= 6; o++)
            {
                int k = (centre + o + Table) % Table;
                float az = track.loopZ[k], ay = track.loopY[k];
                float bz = track.loopZ[k + 1] - az, by = track.loopY[k + 1] - ay;
                float u = Mathf.Clamp01(((z - az) * bz + (y - ay) * by) / Mathf.Max(1e-9f, bz * bz + by * by));
                float ez = z - az - bz * u, ey = y - ay - by * u, d = ez * ez + ey * ey;
                if (d < best) { best = d; s = (k + u) * track.length / Table; }
            }
            Sample(track, s, out float pz, out float py, out float tz, out float ty);
            along = (z - pz) * tz + (y - py) * ty;
            outward = (z - pz) * ty - (y - py) * tz;
        }

        // A mesh made of some of the source's triangles, its vertices moved by -origin.
        static Mesh Part(string name, List<int> picked, int[] triangles, Vector3[] vertices, Vector3[] normals, Vector4[] tangents,
            Vector2[] uv, Vector3 origin, out List<int> used)
        {
            var map = new Dictionary<int, int>();
            used = new List<int>();
            var indices = new int[picked.Count * 3];
            for (int i = 0; i < picked.Count; i++)
                for (int c = 0; c < 3; c++)
                {
                    int v = triangles[picked[i] + c];
                    if (!map.TryGetValue(v, out int to))
                    {
                        to = used.Count;
                        map[v] = to;
                        used.Add(v);
                    }
                    indices[i * 3 + c] = to;
                }
            int count = used.Count;
            var p = new Vector3[count];
            var n = new Vector3[count];
            var t = new Vector4[count];
            var u = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int v = used[i];
                p[i] = vertices[v] - origin;
                n[i] = normals[v];
                t[i] = tangents[v];
                u[i] = uv[v];
            }
            var mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(p);
            mesh.SetNormals(n);
            mesh.SetTangents(t);
            mesh.SetUVs(0, u);
            mesh.SetTriangles(indices, 0);
            return mesh;
        }

        static Renderer Child(Track track, string name, Mesh mesh, Vector3 localPosition)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            child.layer = track.root.gameObject.layer;
            child.transform.SetParent(track.root, false);
            child.transform.localPosition = localPosition;
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = track.source.sharedMaterial;
            renderer.shadowCastingMode = track.source.shadowCastingMode;
            renderer.receiveShadows = track.source.receiveShadows;
            track.parts.Add(renderer);
            return renderer;
        }

        void OnEnable()
        {
            foreach (var track in tracks) track.last = track.root.position;
        }

        void LateUpdate()
        {
            bool timed = false;
            float started = Time.realtimeSinceStartup;
            foreach (var track in tracks)
            {
                // the burnt material (and anything else put on the track) goes onto its parts too
                var material = track.source.sharedMaterial;
                if (material != track.material)
                {
                    track.material = material;
                    foreach (var part in track.parts) part.sharedMaterial = material;
                }

                var now = track.root.position;
                float moved = Vector3.Dot(now - track.last, body.forward);
                track.last = now;
                if (Mathf.Abs(moved) > Teleport) moved = 0f;
                if (moved != 0f)
                {
                    track.distance += moved;
                    for (int w = 0; w < track.wheels.Length; w++)
                        track.wheels[w].localRotation = Quaternion.AngleAxis(track.distance / track.wheelRadius[w] * Mathf.Rad2Deg, track.axle);
                }
                // the belt only while it is moving and on screen
                if (track.distance == track.drawn || !(track.beltRenderer.isVisible || float.IsNaN(track.drawn))) continue;
                timed = true;
                track.drawn = track.distance;
                // driving forwards, the top run goes forwards and the bottom run back: against the loop's own direction
                float shift = -track.distance % track.length;
                if (shift < 0f) shift += track.length;
                track.job.shift = shift;
                track.handle = track.job.Schedule(track.job.place.Length, 2048);
                track.running = true;
            }
            if (!timed) return;
            foreach (var track in tracks)
            {
                if (!track.running) continue;
                track.running = false;
                track.handle.Complete();
                track.belt.SetVertexBufferData(track.job.moved, 0, 0, track.job.moved.Length, 0,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }
            LastUpdateMs = (Time.realtimeSinceStartup - started) * 1000f;
        }

        struct BeltVertex
        {
            public Vector3 position, normal;
            public Vector4 tangent;
        }

        // Carries every belt vertex to its place on the loop, `shift` metres further round.
        [BurstCompile]
        struct BeltJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> loopZ, loopY, tangentZ, tangentY;
            [ReadOnly] public NativeArray<Vector3> place;      // distance round the loop, offset along it, offset out from it
            [ReadOnly] public NativeArray<Vector3> lateral;    // the part of the position that does not move (sideways)
            [ReadOnly] public NativeArray<Vector3> normalIn;   // normal in the loop's frame (along, out, sideways)
            [ReadOnly] public NativeArray<Vector4> tangentIn;  // tangent in the loop's frame, w kept
            [WriteOnly] public NativeArray<BeltVertex> moved;
            public float length, shift;
            public Vector3 axisZ, axisY, unitZ, unitY, unitX;

            public void Execute(int v)
            {
                var at = place[v];
                float s = at.x + shift;
                if (s >= length) s -= length;
                float f = s * (Table / length);
                int i = (int)f;
                if (i >= Table) i = Table - 1;
                float u = f - i;
                float tz = tangentZ[i] + (tangentZ[i + 1] - tangentZ[i]) * u, ty = tangentY[i] + (tangentY[i + 1] - tangentY[i]) * u;
                float z = loopZ[i] + (loopZ[i + 1] - loopZ[i]) * u + at.y * tz + at.z * ty;
                float y = loopY[i] + (loopY[i + 1] - loopY[i]) * u + at.y * ty - at.z * tz;
                var n = normalIn[v];
                var t = tangentIn[v];
                var direction = unitX * t.z + unitZ * (t.x * tz + t.y * ty) + unitY * (t.x * ty - t.y * tz);
                moved[v] = new BeltVertex
                {
                    position = lateral[v] + axisZ * z + axisY * y,
                    normal = unitX * n.z + unitZ * (n.x * tz + n.y * ty) + unitY * (n.x * ty - n.y * tz),
                    tangent = new Vector4(direction.x, direction.y, direction.z, t.w),
                };
            }

            public void Dispose()
            {
                loopZ.Dispose();
                loopY.Dispose();
                tangentZ.Dispose();
                tangentY.Dispose();
                place.Dispose();
                lateral.Dispose();
                normalIn.Dispose();
                tangentIn.Dispose();
                moved.Dispose();
            }
        }

        void OnDestroy()
        {
            foreach (var track in tracks)
            {
                if (track.running) track.handle.Complete();
                track.job.Dispose();
                if (track.belt != null) Destroy(track.belt);
                foreach (var wheel in track.wheels)
                    if (wheel != null) Destroy(wheel.GetComponent<MeshFilter>().sharedMesh);
                var rest = track.root != null ? track.root.GetComponent<MeshFilter>() : null;
                if (rest != null && rest.sharedMesh != null) Destroy(rest.sharedMesh);
            }
        }
    }
}
