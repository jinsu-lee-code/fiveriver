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
    //   belt     what lies in the thin band round the loop becomes one mesh whose vertices are carried round the
    //            loop's centre line (the belt has no repeating link in its geometry, so it really goes round)
    //   wheels   what lies inside a wheel's disc becomes its own small mesh on a child that turns on the axle. The
    //            disc ends inside the tyre's flat side wall and short of the belt's band (in this mesh the belt is
    //            sunk up to 3 cm into the tyres where it lies on them): the ring outside it is round and stands still
    //   the rest stays where it is
    // The mesh is cut along those edges, not sorted triangle by triangle (2026-11-14, see Cut).
    // Wheel discs and the loop's centre line were measured on the mesh (source/vehicles/t34_running_gear) and are
    // read from Resources/Vehicles/T34_RunningGear.json. Each track is driven by the distance its own side of the
    // hull actually travels, so it works on the approach, in the entrance sequence and when the tank turns on the
    // spot (one side forwards, one back), and stands still when the tank does. Added by TankController.
    // The loop in the file follows the belt stretch by stretch (512 points), so the generated mesh's dips and bumps
    // stay where they are and only the links travel.
    public sealed class TankRunningGear : MonoBehaviour
    {
        public const string Resource = "Vehicles/T34_RunningGear";
        const int WheelStride = 4;      // z, y, radius of the disc that turns, radius the belt runs on (tank-body space)
        const int Table = 512;          // samples of the loop's centre line
        const float Teleport = 1.5f;    // a jump of more than this in one frame is a placement, not driving

        [Serializable]
        sealed class Side
        {
            public float length;
            public float[] loop;        // z, y pairs round the loop, tank-body space
            public float[] beltFill;    // texture coordinates of one texel in the belt's usual colour (for the core)
        }

        [Serializable]
        sealed class Data
        {
            public float[] wheels;
            public float beltHalf, beltInside, wheelInner, beltInner;
            public float coreIn, coreOut, coreX0, coreX1;   // the core inside the belt: out from the line, and sideways
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
            public Mesh belt, core;
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
        bool released;
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

            // The mesh is cut along the parts' edges (a triangle that lies across one is divided there), so every part
            // ends on a clean line: the belt's band on a line that runs with the loop, a wheel's disc on a circle.
            // Sorting whole triangles left saw-toothed edges that showed as soon as the parts moved.
            var cut = new Cut(mesh, toBody);
            int wheels = data.wheels.Length / WheelStride;
            var nearest = NearestGrid(track);

            // the belt first: where a wheel stands on it the two are fused, and the belt must stay whole
            var value = new float[cut.Count];
            for (int v = 0; v < value.Length; v++)
            {
                var p = cut.body[v];
                Project(track, nearest, p.z, p.y, out _, out _, out float outward);
                value[v] = Mathf.Min(outward + data.beltInside, Mathf.Min(data.beltHalf - outward, p.x * sign - data.beltInner));
            }
            var beltPart = new List<int>();
            var rest = new List<int>();
            cut.Split(new List<int>(mesh.triangles), value, beltPart, rest);

            // then each wheel's disc out of what is left (inside its circle, on the outer side of the track)
            var discs = new List<int>[wheels];
            for (int w = 0; w < wheels; w++)
            {
                float wz = data.wheels[w * WheelStride], wy = data.wheels[w * WheelStride + 1], turns = data.wheels[w * WheelStride + 2];
                value = new float[cut.Count];
                for (int v = 0; v < value.Length; v++)
                {
                    float dz = cut.body[v].z - wz, dy = cut.body[v].y - wy;
                    value[v] = turns - Mathf.Sqrt(dz * dz + dy * dy);
                }
                var inside = new List<int>();
                var outside = new List<int>();
                cut.Split(rest, value, inside, outside);
                value = new float[cut.Count];
                for (int v = 0; v < value.Length; v++) value[v] = cut.body[v].x * sign - data.wheelInner;
                discs[w] = new List<int>();
                cut.Split(inside, value, discs[w], outside);
                rest = outside;
            }

            var vertices = cut.position.ToArray();
            var normals = cut.normal.ToArray();
            var tangents = cut.tangent.ToArray();
            var uv = cut.uv.ToArray();
            var inBody = cut.body.ToArray();
            var bounds = mesh.bounds;
            filter.sharedMesh = Part(mesh.name + "_Rest", rest, vertices, normals, tangents, uv, Vector3.zero, out _);
            track.wheels = new Transform[wheels];
            track.wheelRadius = new float[wheels];
            for (int w = 0; w < wheels; w++)
            {
                var centre = toTrack.MultiplyPoint3x4(new Vector3(sign * 1.3f, data.wheels[w * WheelStride + 1], data.wheels[w * WheelStride]));
                var wheelMesh = Part(mesh.name + "_Wheel" + w, discs[w], vertices, normals, tangents, uv, centre, out _);
                track.wheels[w] = Child(track, "Wheel" + w, wheelMesh, centre).transform;
                track.wheelRadius[w] = data.wheels[w * WheelStride + 3];
            }

            // the belt: each vertex remembers where it sits on the loop
            // (its moving half, position + normal + tangent, is one vertex stream that the job fills and is copied
            // to the mesh in one go; the texture coordinates sit in a second stream and never change)
            var whole = Part(mesh.name + "_Belt", beltPart, vertices, normals, tangents, uv, Vector3.zero, out var used);
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
            track.core = Core(data, side, track, sign, toTrack);
            if (track.core != null) Child(track, "BeltCore", track.core, Vector3.zero);
            track.material = track.source.sharedMaterial;
            track.last = root.position;
            tracks.Add(track);
        }

        // A core inside the belt, all the way round the loop, in the belt's usual colour. Where a wheel stands on the
        // belt the mesh has no inner face (the two are fused), so once the belt runs those places are holes one can
        // see through; the core is what shows there. It is a box section just inside the belt's own faces, one flat
        // colour, and so does not need to move.
        static Mesh Core(Data data, Side side, Track track, float sign, Matrix4x4 toTrack)
        {
            if (side.beltFill == null || side.beltFill.Length < 2 || data.coreX1 <= data.coreX0 || data.coreOut <= data.coreIn) return null;
            const int Faces = 4, PerRow = Faces * 2;
            int rows = Table + 1;
            var positions = new Vector3[rows * PerRow];
            var normals = new Vector3[rows * PerRow];
            var tangents = new Vector4[rows * PerRow];
            var uv = new Vector2[rows * PerRow];
            var fill = new Vector2(side.beltFill[0], side.beltFill[1]);
            var across = toTrack.MultiplyVector(Vector3.right * sign).normalized;   // away from the hull
            // each face's two corners (sideways, out from the line) and which way it looks (across, out)
            float x0 = data.coreX0, x1 = data.coreX1, low = data.coreIn, high = data.coreOut;
            var corner = new[] { x0, high, x1, high, x0, low, x1, low, x0, low, x0, high, x1, low, x1, high };
            var looks = new[] { 0f, 1f, 0f, -1f, -1f, 0f, 1f, 0f };
            for (int k = 0; k < rows; k++)
            {
                float tz = track.tangentZ[k], ty = track.tangentY[k];
                // out from the line is (y, z) = (-tz, ty), as in the belt's job
                var outward = toTrack.MultiplyVector(new Vector3(0f, -tz, ty)).normalized;
                for (int c = 0; c < PerRow; c++)
                {
                    int v = k * PerRow + c, face = c / 2;
                    float x = corner[c * 2], offset = corner[c * 2 + 1];
                    positions[v] = toTrack.MultiplyPoint3x4(new Vector3(sign * x, track.loopY[k] - offset * tz, track.loopZ[k] + offset * ty));
                    normals[v] = across * looks[face * 2] + outward * looks[face * 2 + 1];
                    tangents[v] = face < 2 ? new Vector4(across.x, across.y, across.z, 1f) : new Vector4(outward.x, outward.y, outward.z, 1f);
                    uv[v] = fill;
                }
            }
            var indices = new int[Table * Faces * 6];
            for (int face = 0, i = 0; face < Faces; face++)
            {
                // which way round a triangle must go to face along the normal (the same all along a face)
                int first = face * 2;
                bool swap = Vector3.Dot(Vector3.Cross(positions[first + 1] - positions[first], positions[first + PerRow] - positions[first]), normals[first]) < 0f;
                for (int k = 0; k < Table; k++)
                {
                    int a = k * PerRow + first, b = a + 1, c = a + PerRow, d = c + 1;
                    indices[i++] = a;
                    indices[i++] = swap ? c : b;
                    indices[i++] = swap ? b : c;
                    indices[i++] = b;
                    indices[i++] = swap ? c : d;
                    indices[i++] = swap ? d : c;
                }
            }
            var mesh = new Mesh { name = track.root.name + "_Core" };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            return mesh;
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

        // The track's mesh while it is taken apart: its vertices (in the track's own space and in the tank body's),
        // to which Split adds the points where it divides a triangle.
        sealed class Cut
        {
            public readonly List<Vector3> position, normal, body;
            public readonly List<Vector4> tangent;
            public readonly List<Vector2> uv;
            readonly Dictionary<long, int> made = new Dictionary<long, int>();

            public int Count => position.Count;

            public Cut(Mesh mesh, Matrix4x4 toBody)
            {
                position = new List<Vector3>(mesh.vertices);
                normal = new List<Vector3>(mesh.normals);
                tangent = new List<Vector4>(mesh.tangents);
                uv = new List<Vector2>(mesh.uv);
                body = new List<Vector3>(position.Count);
                for (int i = 0; i < position.Count; i++) body.Add(toBody.MultiplyPoint3x4(position[i]));
            }

            // Sorts triangles (three indices each) by a value at their corners: `over` gets what lies where it is
            // above zero, `under` the rest. A triangle with corners on both sides is divided along the line where
            // the value passes zero. `value` covers every vertex there is when this is called.
            public void Split(List<int> triangles, float[] value, List<int> over, List<int> under)
            {
                made.Clear();
                for (int t = 0; t < triangles.Count; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    int above = (value[a] > 0f ? 1 : 0) + (value[b] > 0f ? 1 : 0) + (value[c] > 0f ? 1 : 0);
                    if (above == 3 || above == 0)
                    {
                        var all = above == 3 ? over : under;
                        all.Add(a);
                        all.Add(b);
                        all.Add(c);
                        continue;
                    }
                    // turned so that the corner that is alone on its side comes first (the order round the triangle stays)
                    bool alone = above == 1;
                    while (value[a] > 0f != alone)
                    {
                        int first = a;
                        a = b;
                        b = c;
                        c = first;
                    }
                    int p = Between(a, b, value), q = Between(c, a, value);
                    var corner = alone ? over : under;
                    var other = alone ? under : over;
                    corner.Add(a);
                    corner.Add(p);
                    corner.Add(q);
                    other.Add(p);
                    other.Add(b);
                    other.Add(c);
                    other.Add(p);
                    other.Add(c);
                    other.Add(q);
                }
            }

            // The point on the edge a-b where the value passes zero, as a new vertex (one per edge, so the two
            // triangles that share the edge stay joined).
            int Between(int a, int b, float[] value)
            {
                if (a > b)
                {
                    int swap = a;
                    a = b;
                    b = swap;
                }
                long key = ((long)a << 32) | (uint)b;
                if (made.TryGetValue(key, out int index)) return index;
                float span = value[a] - value[b];
                float t = Mathf.Abs(span) > 1e-12f ? Mathf.Clamp01(value[a] / span) : 0.5f;
                index = position.Count;
                position.Add(Vector3.Lerp(position[a], position[b], t));
                body.Add(Vector3.Lerp(body[a], body[b], t));
                normal.Add(Vector3.Lerp(normal[a], normal[b], t).normalized);
                var along = Vector3.Lerp(tangent[a], tangent[b], t).normalized;
                tangent.Add(new Vector4(along.x, along.y, along.z, tangent[a].w));
                uv.Add(Vector2.Lerp(uv[a], uv[b], t));
                made[key] = index;
                return index;
            }
        }

        // A mesh made of some triangles (three vertex indices each), its vertices moved by -origin.
        static Mesh Part(string name, List<int> triangles, Vector3[] vertices, Vector3[] normals, Vector4[] tangents,
            Vector2[] uv, Vector3 origin, out List<int> used)
        {
            var map = new Dictionary<int, int>();
            used = new List<int>();
            var indices = new int[triangles.Count];
            for (int i = 0; i < triangles.Count; i++)
            {
                int v = triangles[i];
                if (!map.TryGetValue(v, out int to))
                {
                    to = used.Count;
                    map[v] = to;
                    used.Add(v);
                }
                indices[i] = to;
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
            if (released) return;
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

        void OnDestroy() => Release();

        // Frees the jobs' native arrays and the split meshes, once. The tank body is saved inactive in the scene and
        // Unity skips OnDestroy on objects that were never active, so TankController calls this from its own
        // OnDestroy too: a session that ended before the tank section leaked them (2026-11-25).
        public void Release()
        {
            if (released) return;
            released = true;
            foreach (var track in tracks)
            {
                if (track.running) track.handle.Complete();
                track.job.Dispose();
                if (track.belt != null) Destroy(track.belt);
                if (track.core != null) Destroy(track.core);
                foreach (var wheel in track.wheels)
                    if (wheel != null) Destroy(wheel.GetComponent<MeshFilter>().sharedMesh);
                var rest = track.root != null ? track.root.GetComponent<MeshFilter>() : null;
                if (rest != null && rest.sharedMesh != null) Destroy(rest.sharedMesh);
            }
        }
    }
}
