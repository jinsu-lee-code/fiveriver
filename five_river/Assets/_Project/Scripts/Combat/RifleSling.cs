using System.Collections.Generic;
using UnityEngine;

namespace FiveRivers.Combat
{
    // 등에 멘 소총과 멜빵 (부족한 점 7번, 2026-11-26 시작 · 2026-11-27 마무리, 검수 캡처 sling/now_Ally_2_2right · 3back).
    // 1950 국군의 M1 개런드: 1¼인치 면 웨빙 멜빵(M1907 가죽 · M1 웨빙 공용 규격), 앞 고리(총열 밴드)와 뒤 고리(개머리) 사이.
    // 새벽에 등에 멘 총(총구는 오른 어깨 위, 개머리는 왼 허리 — AllyClipPlayer.SlungPose, 장전수 클립에서 구운 자리)은
    // 가슴 뼈 기준 30 cm 넘게 뒤에 떠 있었고, 총 메시(M1_Photo_Extras_L0~L2)의 멜빵은 총 아래 고정 고리라 등에 묻혔다.
    //  1. 몸 겉면: 메는 순간부터 0.75초 동안 네 번 몸 메시(옷 · 장비 포함)를 구워 앞면 · 뒷면 높이 지도(BodySurface)를 만들고
    //     칸마다 가장 바깥 값을 남긴다(첫 프레임은 자세가 바뀌는 중이고 숨으로 2~3 cm 흔들렸다). 가슴 뼈를 따라간다.
    //  2. 총: 등 쪽 면(멜빵 고리 선)이 뒷면에서 SlingSideClear 만큼 떨어지게 옮기고 등을 따라 눕힌다(SlingFit: 기울기 상한 MaxSlope).
    //     옮기는 양은 초당 ShiftRate 로만 따라가서 갑자기 붙거나 떨리지 않는다.
    //  3. 멜빵: 메고 있는 동안만 총 메시의 고정 고리(재질 Photo_Canvas, 둘째 부분)를 빼고, 같은 천 재질의 끈(폭 3.2 cm · 두께 3 mm)을
    //     앞 고리 → 오른 어깨 위 → 가슴을 대각선으로 → 왼 허리 → 뒤 고리로 그린다. 어깨 위 · 옆구리는 뼈에서 쏜 선이 몸 겉면을
    //     벗어나는 곳, 그 사이는 곡선. 곡선의 모든 점을 높이 지도 밖(Clearance)으로 밀어내 몸을 관통하지 않는다.
    // 뼈 추가 · FBX 재출력 없음. 표시 전용. 탄입대 · 짧은 끈 흔들림은 범위 밖. AllyCombatant 가 어깨걸이일 때 Drape, 아니면 Rest.
    public sealed class RifleSling : MonoBehaviour
    {
        // the strap: an M1 sling is 1 1/4 in. webbing
        public static float Width = 0.032f, Thickness = 0.003f, Clearance = 0.006f;
        // the rifle on the back: the sling side of the rifle this far off the back surface, laid along it at most this slope,
        // corrections followed at most this fast (m/s, slope/s)
        public static float SlingSideClear = 0.012f, MaxSlope = 0.45f, ShiftRate = 0.6f, SlopeRate = 1.5f, MostShift = 0.4f;
        public static float[] MeasureAt = { 0f, 0.25f, 0.5f, 0.75f };
        const int Samples = 44, RifleSamples = 12;

        // swivels on the rifle (its own frame, measured from the sling part of M1_Photo_Extras_L0)
        static readonly Vector3 FrontSwivel = new Vector3(0.001f, 0.008f, 0.557f), RearSwivel = new Vector3(0.002f, -0.110f, -0.277f);

        // where the strap runs, in the body frame (x to his right, y up, z forward; origins on the bone line, rays out to
        // the surface). Over the right shoulder, then down across the chest to the left side at the belt.
        struct Station { public int from; public Vector3 origin, ray; }
        // from: 0 = upper chest, 1 = right upper arm (shoulder joint), 2 = midway clavicle-shoulder
        static readonly Station[] Path =
        {
            new Station { from = 2, origin = new Vector3(0f, 0f, -0.01f), ray = new Vector3(0f, 0.55f, -0.83f) },   // behind the shoulder top
            new Station { from = 2, origin = Vector3.zero, ray = Vector3.up },                                       // on top
            new Station { from = 2, origin = new Vector3(0f, 0f, 0.01f), ray = new Vector3(0f, 0.6f, 0.8f) },        // front of the shoulder
            new Station { from = 0, origin = new Vector3(0.08f, 0.02f, 0f), ray = new Vector3(0.15f, 0.25f, 1f) },
            new Station { from = 0, origin = new Vector3(0.02f, -0.07f, 0f), ray = new Vector3(0f, 0f, 1f) },
            new Station { from = 0, origin = new Vector3(-0.05f, -0.16f, 0f), ray = new Vector3(-0.1f, 0f, 1f) },
            new Station { from = 0, origin = new Vector3(-0.09f, -0.23f, 0f), ray = new Vector3(-0.55f, 0f, 1f) },
            new Station { from = 0, origin = new Vector3(-0.10f, -0.27f, 0f), ray = new Vector3(-1f, 0f, 0.35f) },   // round his left side
        };

        Transform weapon, chest, rightArm, clavicle, root;
        Animator body;
        readonly List<Renderer> extras = new List<Renderer>();
        readonly List<Mesh> fullMeshes = new List<Mesh>();
        readonly List<Material[]> fullMaterials = new List<Material[]>();
        Vector3[] points;   // surface points of the stations, body frame
        Vector3[] normals;  // their outward rays, body frame
        float[] reach;      // the furthest surface seen along each station's ray (outermost of the measurements)
        BodySurface surface;
        int measured; float drapedAt;
        float shift, slope; // the rifle's correction as it is followed
        Material canvas;
        Vector2[] uvAlong;  // the original sling's texture, sampled along its length (left and right edge pairs)
        MeshFilter strapFilter; MeshRenderer strapRenderer; Mesh strap;
        bool draped;

        static readonly Dictionary<Mesh, Mesh> MetalOnly = new Dictionary<Mesh, Mesh>();

        public bool Draped => draped;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // review: strap points left inside the body after the push out, and the rifle's furthest gap off the back (m)
        public int LastInside { get; private set; }
        public float LastWorstGap { get; private set; }
#endif

        public static RifleSling For(Transform weapon, Animator body)
        {
            if (weapon == null || body == null) return null;
            var s = weapon.GetComponent<RifleSling>();
            if (s == null) { s = weapon.gameObject.AddComponent<RifleSling>(); s.Init(weapon, body); }
            return s.canvas != null ? s : null;
        }

        void Init(Transform w, Animator a)
        {
            weapon = w; body = a; root = a.transform;
            chest = a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest);
            rightArm = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            clavicle = a.GetBoneTransform(HumanBodyBones.RightShoulder) ?? rightArm;
            foreach (var r in weapon.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.name.StartsWith("M1_Photo_Extras")) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.subMeshCount < 2 || r.sharedMaterials.Length < 2) continue;
                extras.Add(r); fullMeshes.Add(mf.sharedMesh); fullMaterials.Add(r.sharedMaterials);
                if (canvas == null && mf.sharedMesh.isReadable) { canvas = r.sharedMaterials[1]; SampleTexture(mf.sharedMesh); }
            }
            if (chest == null || rightArm == null) canvas = null;
        }

        // the original strap's texture along its length: the vertex of its outer face nearest each point of its middle line
        void SampleTexture(Mesh mesh)
        {
            var v = mesh.vertices; var uv = mesh.uv; var tri = mesh.GetTriangles(1);
            var used = new HashSet<int>(tri);
            uvAlong = new Vector2[2 * 16];
            for (int k = 0; k < 16; k++)
            {
                float z = Mathf.Lerp(RearSwivel.z + 0.04f, FrontSwivel.z - 0.04f, k / 15f);
                for (int side = 0; side < 2; side++)
                {
                    var want = new Vector3(side == 0 ? 0.03f : 0.058f, -0.2f, z);
                    float best = float.MaxValue; var u = Vector2.zero;
                    foreach (int i in used)
                    {
                        float d = (v[i] - want).sqrMagnitude;
                        if (d < best) { best = d; u = uv.Length > i ? uv[i] : Vector2.zero; }
                    }
                    uvAlong[2 * k + side] = u;
                }
            }
        }

        // Called each frame the rifle is on his back, after the rifle, the arms and the hold fade are done.
        // settled: the hold fade into the slung pose is over (the first fit then lands at once; before it, and later, it is followed)
        public void Drape(bool settled = true)
        {
            if (canvas == null) return;
            if (!draped)
            {
                if (!SetFrame()) return;
                surface = null; reach = new float[Path.Length]; measured = 0; shift = slope = 0f; fitted = false;
                drapedAt = Time.time;
                if (!Measure()) return;
                for (int i = 0; i < extras.Count; i++)
                {
                    var mf = extras[i].GetComponent<MeshFilter>();
                    mf.sharedMesh = MetalOnlyOf(fullMeshes[i]);
                    extras[i].sharedMaterials = new[] { fullMaterials[i][0] };
                }
                EnsureStrap();
                strapRenderer.enabled = true;
                draped = true;
            }
            else if (measured < MeasureAt.Length && Time.time - drapedAt >= MeasureAt[measured]) Measure();
            FitRifle(settled);
            BuildStrap();
        }

        // Back to the rifle's own sling (in his hands, or the scene restarting).
        public void Rest()
        {
            if (!draped) return;
            draped = false;
            for (int i = 0; i < extras.Count; i++)
            {
                if (extras[i] == null) continue;
                extras[i].GetComponent<MeshFilter>().sharedMesh = fullMeshes[i];
                extras[i].sharedMaterials = fullMaterials[i];
            }
            if (strapRenderer != null) strapRenderer.enabled = false;
        }

        void OnDisable() => Rest();

        void OnDestroy()
        {
            if (strap != null) Destroy(strap);
            if (strapRenderer != null) Destroy(strapRenderer.gameObject);
        }

        static Mesh MetalOnlyOf(Mesh full)
        {
            if (MetalOnly.TryGetValue(full, out var m) && m != null) return m;
            m = Instantiate(full); m.name = full.name + "_NoSling";
            m.subMeshCount = 1;
            MetalOnly[full] = m;
            return m;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => MetalOnly.Clear();

        // ---- the body frame: the chest bone's position and turn without its scale (the rigs import in centimetres), with
        // axes x to his right, y world-up, z forward as they were when he slung it
        Vector3 frameRight, frameUp, frameForward;
        Vector3 ToChest(Vector3 world) => Quaternion.Inverse(chest.rotation) * (world - chest.position);
        Vector3 ToFrame(Vector3 world) { var c = ToChest(world); return new Vector3(Vector3.Dot(c, frameRight), Vector3.Dot(c, frameUp), Vector3.Dot(c, frameForward)); }
        Vector3 FrameToChest(Vector3 f) => frameRight * f.x + frameUp * f.y + frameForward * f.z;
        Vector3 FromFrame(Vector3 f) => chest.position + chest.rotation * FrameToChest(f);
        Vector3 FrameDirToWorld(Vector3 f) => chest.rotation * FrameToChest(f);

        bool SetFrame()
        {
            var left = body.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            if (left == null) return false;
            var r = rightArm.position - left.position; r.y = 0f;
            if (r.sqrMagnitude < 1e-4f) return false;
            r.Normalize();
            var f = Vector3.Cross(r, Vector3.up);
            var inv = Quaternion.Inverse(chest.rotation);
            frameRight = inv * r; frameUp = inv * Vector3.up; frameForward = inv * f;
            return true;
        }

        Vector3 OriginPoint(Station s)
        {
            if (s.from == 2) return ToFrame(Vector3.Lerp(clavicle.position, rightArm.position, 0.6f)) + s.origin;
            if (s.from == 1) return ToFrame(rightArm.position) + s.origin;
            return s.origin;
        }

        // One more look at his outer surface (clothes and webbing): the height maps and the station rays keep the outermost.
        readonly List<Vector3> buffer = new List<Vector3>();
        bool Measure()
        {
            measured++;
            var verts = new List<Vector3>();
            var baked = new Mesh();
            foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || smr.transform.IsChildOf(weapon) || smr.sharedMesh == null) continue;
                smr.BakeMesh(baked, true);
                baked.GetVertices(buffer);
                // baked "with scale", then the renderer's full matrix takes it to the world (measured 2026-11-26: the body
                // renderer is scaled about 180x under a 0.0078 parent; placing it without the scale missed the body entirely)
                var toWorld = smr.transform.localToWorldMatrix;
                foreach (var v in buffer) verts.Add(ToFrame(toWorld.MultiplyPoint3x4(v)));
            }
            Destroy(baked);
            if (verts.Count == 0) return false;

            // the trunk between the arms, from below the belt to the shoulder joints (the arms hang outside x = ±0.8 shoulder)
            var shoulder = ToFrame(rightArm.position);
            float half = Mathf.Max(0.1f, Mathf.Abs(shoulder.x) * 0.8f);
            var look = new BodySurface(-half, half, -0.55f, shoulder.y, 0.04f);
            look.AddAll(verts);
            look.Fill();
            if (surface == null) surface = look; else surface.Merge(look);

            if (points == null) { points = new Vector3[Path.Length]; normals = new Vector3[Path.Length]; }
            for (int s = 0; s < Path.Length; s++)
            {
                var o = OriginPoint(Path[s]);
                var d = Path[s].ray.normalized;
                float far = 0f;
                foreach (var v in verts)
                {
                    var to = v - o; float along = Vector3.Dot(to, d);
                    if (along <= 0f || along > 0.35f) continue;
                    if ((to - d * along).sqrMagnitude > 0.022f * 0.022f) continue;
                    if (along > far) far = along;
                }
                if (far <= 0f) far = 0.1f;
                reach[s] = Mathf.Max(reach[s], far);
                points[s] = o + d * (reach[s] + Clearance + Thickness * 0.5f);
                normals[s] = d;
            }
            return true;
        }

        // The rifle laid against his back: the sling side (the line through the two swivels) SlingSideClear off the back surface.
        readonly float[] along = new float[RifleSamples], gaps = new float[RifleSamples];
        bool fitted;
        void FitRifle(bool settled)
        {
            if (surface == null) return;
            var rear = weapon.TransformPoint(RearSwivel); var front = weapon.TransformPoint(FrontSwivel);
            var line = front - rear; float length = line.magnitude;
            if (length < 0.1f) return;
            for (int i = 0; i < RifleSamples; i++)
            {
                float t = Mathf.Lerp(-0.3f, 1.05f, i / (RifleSamples - 1f)); // from the butt, past the rear swivel, to the muzzle
                var p = rear + line * t;
                along[i] = t * length;
                gaps[i] = surface.BackGapBehind(ToFrame(p));
            }
            if (!SlingFit.Solve(along, gaps, SlingSideClear, MaxSlope, out float wantShift, out float wantSlope)) return;
            wantShift = Mathf.Clamp(wantShift, -MostShift, MostShift);
            float dt = Time.deltaTime;
            // the first fit once he has it on his back lands at once (no sliding in from 30 cm off the back); the rest is followed
            if (settled && !fitted) { shift = wantShift; slope = wantSlope; fitted = true; }
            else { shift = Mathf.MoveTowards(shift, wantShift, ShiftRate * dt); slope = Mathf.MoveTowards(slope, wantSlope, SlopeRate * dt); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastWorstGap = SlingFit.WorstGap(along, gaps, shift, slope);
#endif
            var back = FrameDirToWorld(Vector3.back).normalized;
            var dir = line / length;
            var across = back - dir * Vector3.Dot(back, dir);
            if (across.sqrMagnitude < 1e-6f) return;
            across.Normalize();
            var turned = (dir + across * slope).normalized;
            var turn = Quaternion.FromToRotation(dir, turned);
            // turned about the rear swivel, then moved straight back (or in) by the shift
            weapon.SetPositionAndRotation(rear + turn * (weapon.position - rear) + back * shift, turn * weapon.rotation);
        }

        void EnsureStrap()
        {
            if (strapRenderer != null) return;
            var go = new GameObject("RifleSling_Strap");
            go.transform.SetParent(root, false);
            strapFilter = go.AddComponent<MeshFilter>();
            strapRenderer = go.AddComponent<MeshRenderer>();
            strapRenderer.sharedMaterial = canvas;
            strap = new Mesh { name = "RifleSling_Strap" };
            strap.MarkDynamic();
            strapFilter.sharedMesh = strap;
        }

        readonly List<Vector3> ctrl = new List<Vector3>(), ctrlN = new List<Vector3>();
        Vector3[] vbuf; Vector3[] nbuf; Vector2[] ubuf; int[] tbuf; float[] seg;

        void BuildStrap()
        {
            // control points in world space
            ctrl.Clear(); ctrlN.Clear();
            var toRoot = root.worldToLocalMatrix;
            var front = weapon.TransformPoint(FrontSwivel); var rear = weapon.TransformPoint(RearSwivel);
            var away = -weapon.up; // the sling side of the rifle faces his back
            ctrl.Add(front); ctrlN.Add(-away);
            for (int s = 0; s < points.Length; s++)
            {
                ctrl.Add(FromFrame(points[s]));
                ctrlN.Add(FrameDirToWorld(normals[s]));
            }
            ctrl.Add(rear); ctrlN.Add(-away);

            int rings = Samples, ringVerts = 8; // 4 faces, 2 verts each (hard edges)
            int nv = rings * ringVerts;
            if (vbuf == null || vbuf.Length != nv)
            {
                vbuf = new Vector3[nv]; nbuf = new Vector3[nv]; ubuf = new Vector2[nv];
                tbuf = new int[(rings - 1) * 4 * 6];
                int t = 0;
                for (int i = 0; i < rings - 1; i++)
                    for (int face = 0; face < 4; face++)
                    {
                        int a = i * ringVerts + face * 2, b = a + 1, c = a + ringVerts, d = b + ringVerts;
                        tbuf[t++] = a; tbuf[t++] = c; tbuf[t++] = b;
                        tbuf[t++] = b; tbuf[t++] = c; tbuf[t++] = d;
                    }
            }
            // even spacing by the length of the polyline through the control points
            if (seg == null || seg.Length != ctrl.Count) seg = new float[ctrl.Count];
            float total = 0f; seg[0] = 0f;
            for (int i = 1; i < ctrl.Count; i++) { total += Vector3.Distance(ctrl[i - 1], ctrl[i]); seg[i] = total; }
            Vector3 prevB = Vector3.zero;
            float clear = Clearance + Thickness * 0.5f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int inside = 0;
#endif
            for (int i = 0; i < rings; i++)
            {
                float at = total * i / (rings - 1);
                int k = 1; while (k < ctrl.Count - 1 && seg[k] < at) k++;
                float u = seg[k] > seg[k - 1] ? (at - seg[k - 1]) / (seg[k] - seg[k - 1]) : 0f;
                var p = CatmullRom(ctrl, k - 1, u);
                // the curve between the stations cuts corners: bring it out over the surface it crosses (never through him)
                if (surface != null)
                {
                    var f = surface.PushOut(ToFrame(p), clear);
                    p = FromFrame(f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (surface.Inside(f, 0.002f)) inside++;
#endif
                }
                var tangent = (CatmullRom(ctrl, k - 1, Mathf.Min(1f, u + 0.02f)) - CatmullRom(ctrl, k - 1, Mathf.Max(0f, u - 0.02f)));
                if (tangent.sqrMagnitude < 1e-10f) tangent = ctrl[k] - ctrl[k - 1];
                tangent.Normalize();
                var n = Vector3.Slerp(ctrlN[k - 1], ctrlN[k], u);
                n = (n - tangent * Vector3.Dot(n, tangent)).normalized;
                var b = Vector3.Cross(tangent, n);
                if (i > 0 && Vector3.Dot(b, prevB) < 0f) { b = -b; n = -n; }
                prevB = b;
                float w = Width * 0.5f, h = Thickness * 0.5f;
                Vector3 c0 = p + b * w + n * h, c1 = p - b * w + n * h, c2 = p - b * w - n * h, c3 = p + b * w - n * h;
                int o = i * ringVerts;
                vbuf[o + 0] = c0; vbuf[o + 1] = c1; nbuf[o + 0] = nbuf[o + 1] = n;     // outer face
                vbuf[o + 2] = c1; vbuf[o + 3] = c2; nbuf[o + 2] = nbuf[o + 3] = -b;    // edge
                vbuf[o + 4] = c2; vbuf[o + 5] = c3; nbuf[o + 4] = nbuf[o + 5] = -n;    // inner face
                vbuf[o + 6] = c3; vbuf[o + 7] = c0; nbuf[o + 6] = nbuf[o + 7] = b;     // edge
                float s = (float)i / (rings - 1) * 15f; int s0 = Mathf.Min(14, (int)s); float sf = s - s0;
                var uvL = Vector2.Lerp(uvAlong[2 * s0], uvAlong[2 * s0 + 2], sf);
                var uvR = Vector2.Lerp(uvAlong[2 * s0 + 1], uvAlong[2 * s0 + 3], sf);
                for (int q = 0; q < ringVerts; q++) ubuf[o + q] = (q == 0 || q == 3 || q == 4 || q == 7) ? uvL : uvR;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastInside = inside;
#endif
            // built in world metres, stored in his root's space (scaled 0.78 on the prefab): the strap keeps its real width
            for (int q = 0; q < vbuf.Length; q++) { vbuf[q] = toRoot.MultiplyPoint3x4(vbuf[q]); nbuf[q] = toRoot.MultiplyVector(nbuf[q]).normalized; }
            strap.Clear();
            strap.vertices = vbuf; strap.normals = nbuf; strap.uv = ubuf; strap.triangles = tbuf;
            strap.RecalculateBounds();
            strap.RecalculateTangents();
        }

        static Vector3 CatmullRom(List<Vector3> p, int i, float t)
        {
            var p0 = p[Mathf.Max(0, i - 1)]; var p1 = p[i]; var p2 = p[Mathf.Min(p.Count - 1, i + 1)]; var p3 = p[Mathf.Min(p.Count - 1, i + 2)];
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
