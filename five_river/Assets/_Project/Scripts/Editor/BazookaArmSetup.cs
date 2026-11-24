using System;
using System.IO;
using System.Linq;
using FiveRivers.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FiveRivers.EditorTools
{
    // 바주카 1인칭 팔을 Tripo AI로 만들고 리깅한 국군 이등병(소매 걷은 차림, 손가락 뼈 포함)의 팔로 통째로 바꾼다
    // (2026-11-23, 사용자 지시 "트리포 ai에서 리깅하고 블랜더에서 맞춘뒤에 유니티를 넣는 방법" · "전신 리깅으로").
    // 팔은 source/characters/fp_arms_tripo_20261123/retarget_arms.py 가 게임 1인칭 뼈대의 기준 자세 · 같은 뼈 이름으로 만든다.
    // 소매 · 팔뚝 · 손이 한 모델에서 나오므로 이음새가 없다. 앞서 손만 이식하던 BazookaHandSetup의 결과는 걷어 낸다.
    // 여러 번 실행해도 결과가 같다. 편집 모드에서만.
    public static class BazookaArmSetup
    {
        const string Source = "../source/characters/fp_arms_tripo_20261123/work";
        const string Dir = "Assets/_Project/Art/Models/Characters/ROK_Arms/TripoArms";
        static readonly Vector3 LeftGripShift = new Vector3(-0.0302f, -0.0087f, 0.0058f), RightGripShift = new Vector3(0.0359f, 0.0113f, 0.0105f);
        // (2026-11-24, pass 2: refitted at armScale 1.9 - the 2026-11-23 values plus what ClaudeHandProbe.Measure then measured
        // from the closed fingers' loop to each grip's axis, with the loop 8.6 cm from the palm's bones: left 2.6 cm, right 1.2 cm)
        const float ArmScale = 1.9f;
        static readonly Vector3 HipViewPosition = new Vector3(0.20f, -0.16f, 0.24f), HipViewEuler = new Vector3(8f, -4f, 10f);
        // (2026-11-23 grip pose: moved out by 3.3 / 4.4 cm, so the palm's skin, not its bones, lies on the grip: the grips are
        // 6.6 / 7.4 cm across, the palm 3-4 cm thick to its bone, all measured in play by fitting the grip's cross-section)

        [Serializable] class ArmData { public string name; public string[] bones; public float[] vertices, normals, uv, boneWeight; public int[] triangles, boneIndex; }
        [Serializable] class ArmFile { public ArmData[] arms; }

        [MenuItem("FiveRivers/Setup/Bazooka Tripo Arms")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
            var file = Path.Combine(Source, "arms_unity.json");
            if (!File.Exists(file)) throw new FileNotFoundException("run source/characters/fp_arms_tripo_20261123/retarget_arms.py first", file);
            var data = JsonUtility.FromJson<ArmFile>(File.ReadAllText(file));
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Models/Characters/ROK_Arms", "TripoArms");
            var bazooka = Object.FindFirstObjectByType<Bazooka>(FindObjectsInactive.Include);
            if (bazooka == null) throw new InvalidOperationException("no Bazooka in the open scene (open Dabudong)");
            var arms = bazooka.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("SK_CH_ROK_Arm_")).ToArray();
            var material = BuildMaterial(data, arms);
            foreach (var arm in arms)
            {
                var d = data.arms.FirstOrDefault(a => a.name == arm.name);
                if (d == null) throw new InvalidOperationException("no arm data for " + arm.name);
                var bindposes = OriginalMesh(arm).bindposes;
                arm.sharedMesh = Save(Build(d, arm, bindposes), $"{Dir}/SM_{arm.name}_Tripo.asset");
                arm.sharedMaterial = material;
                arm.updateWhenOffscreen = true;
                arm.quality = SkinQuality.Bone4;
                EditorUtility.SetDirty(arm);
                // the hand-only graft of the earlier attempt
                var graft = arm.transform.parent.Find(arm.name.Replace("Arm_", "Hand_"));
                if (graft != null) Object.DestroyImmediate(graft.gameObject);
                Debug.Log($"[BazookaArmSetup] {arm.name}: {arm.sharedMesh.vertexCount} verts, {arm.sharedMesh.triangles.Length / 3} tris");
            }
            // wrist targets: measured in play with these arms (ClaudeHandProbe.Measure, the same at the hip and in the aim)
            // as the distance from the middle of the closed fingers' loop to the axis of the grip each hand holds
            var pres = bazooka.GetComponentInChildren<BazookaPresentation>(true) ?? Object.FindFirstObjectByType<BazookaPresentation>(FindObjectsInactive.Include);
            if (pres != null)
            {
                var so = new SerializedObject(pres);
                so.FindProperty("leftGripShift").vector3Value = LeftGripShift;
                so.FindProperty("rightGripShift").vector3Value = RightGripShift;
                // (2026-11-24, pass 2) The launcher is drawn about twice life size (its grips 6.6 / 7.4 cm across): at 1.25 the hands
                // were too small to close round them and, seen from the eye, 35 x 62 px (front hand, 1.05 m off, behind the sandbags;
                // the rear hand below the frame) against the M1 support hand's 302 x 347 px (0.47 m). The arms at 1.9 (the launcher's
                // scale), and the hip carry brought back, up and canted so the front hand shows below the tube as the M1 support
                // hand does under the handguard (65 x 142 px of bones; candidates measured in play with ClaudeHandPass2, 2026-11-24).
                so.FindProperty("armScale").floatValue = ArmScale;
                so.FindProperty("hipViewPosition").vector3Value = HipViewPosition;
                so.FindProperty("hipViewEuler").vector3Value = HipViewEuler;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // the fingers closed round the grips (the clips leave them open)
            var armsRoot = arms[0].transform.parent;
            if (!armsRoot.TryGetComponent<FirstPersonFingerCurl>(out var curl)) curl = armsRoot.gameObject.AddComponent<FirstPersonFingerCurl>();
            var all = bazooka.Model.GetComponentsInChildren<Transform>(true);
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
            void Fill(FirstPersonFingerCurl.Hand h, string side)
            {
                h.hand = Find($"mixamorig:{side}Hand"); h.grip = Find("Grip_" + side);
                Transform[] Chain(string f) => new[] { Find($"mixamorig:{side}Hand{f}1"), Find($"mixamorig:{side}Hand{f}2"), Find($"mixamorig:{side}Hand{f}3") };
                h.index = Chain("Index"); h.middle = Chain("Middle"); h.ring = Chain("Ring"); h.pinky = Chain("Pinky"); h.thumb = Chain("Thumb");
                // the launcher's surface near this grip, for the fingers to wrap (in the launcher's own frame)
                h.space = h.grip != null ? h.grip.parent : null;
                // (the right one reaches forward to the trigger and its guard: centred between the grip and the trigger)
                var middle = side == "Right" && h.space != null ? (h.grip.position + h.space.TransformPoint(new Vector3(0f, -0.11f, 0.176f))) * 0.5f : h.grip.position;
                h.field = h.space != null ? BakeField(h.space, middle, $"{Dir}/GripField_{side}.asset", side == "Right" ? 0.16f : 0.14f) : null;
            }
            Fill(curl.left, "Left"); Fill(curl.right, "Right");
            // where each grip's axis lies from the palm (with the wrists placed by the grip shifts above): the grip's
            // radius + the palm's thickness to its bones, measured in play 2026-11-23 (3.2 + 3.2 cm, 3.7 + 3.9 cm)
            curl.left.axisDepth = 0.064f; curl.right.axisDepth = 0.076f;
            // (pass 2) the palm's thickness grows with armScale 1.9 / 1.25, less the slimmer retarget: 3.2 + 4.7 cm, 3.7 + 5.6 cm
            curl.left.axisDepth = 0.079f; curl.right.axisDepth = 0.093f;
            curl.left.trigger = null;
            curl.right.trigger = curl.right.space != null ? TriggerPoint(curl.right.space) : null;
            // the thumb stood straight out past the grip at the first values (12, 26, 24)
            curl.fingerCurl = new Vector3(48f, 70f, 42f);
            curl.thumbCurl = new Vector3(30f, 42f, 34f);
            // the clips key another finger pose at their first and last frames: fold from the rest pose (FirstPersonFingerCurl)
            curl.fingersFromRest = true;
            EditorUtility.SetDirty(curl);
            // wrists and forearms within a human arm's range (after BazookaPresentation places the hands, before the fingers close)
            if (!armsRoot.TryGetComponent<ArmJointLimits>(out var limits)) limits = armsRoot.gameObject.AddComponent<ArmJointLimits>();
            if (pres != null)
            {
                var so = new SerializedObject(pres);
                Transform Ref(string f) => so.FindProperty(f).objectReferenceValue as Transform;
                void Arm(ArmJointLimits.Arm a, string s, string side)
                {
                    a.upper = Ref(s + "Upper"); a.forearm = Ref(s + "Forearm"); a.hand = Ref(s + "Wrist"); a.target = Ref(s + "Grip");
                    a.index1 = Find($"mixamorig:{side}HandIndex1"); a.middle1 = Find($"mixamorig:{side}HandMiddle1"); a.pinky1 = Find($"mixamorig:{side}HandPinky1");
                    a.left = side == "Left";
                }
                Arm(limits.left, "left", "Left"); Arm(limits.right, "right", "Right");
                limits.cameraFrame = pres.transform.parent;
                // (the scene keeps the values it was saved with: the pass-2 ones set here)
                limits.preferLeft = new Vector2(0f, 0f); limits.preferRight = new Vector2(0f, -10f); limits.preferWeight = 0.85f;
                limits.maxForearmTwist = 170f; limits.twistShare = 1f;
            }
            EditorUtility.SetDirty(limits);
            EditorSceneManager.MarkSceneDirty(bazooka.gameObject.scene);
            EditorSceneManager.SaveScene(bazooka.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        static Mesh OriginalMesh(SkinnedMeshRenderer arm)
        {
            foreach (var path in AssetDatabase.FindAssets("SK_CH_ROK_Arms t:Model").Select(AssetDatabase.GUIDToAssetPath))
            {
                var found = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault(m => m.name == arm.name);
                if (found != null) return found;
            }
            throw new InvalidOperationException("original arm mesh not found: " + arm.name);
        }

        static Mesh Build(ArmData a, SkinnedMeshRenderer arm, Matrix4x4[] bindposes)
        {
            int n = a.vertices.Length / 3;
            var index = a.bones.Select(b => Array.FindIndex(arm.bones, t => t.name == b)).ToArray();
            for (int i = 0; i < index.Length; i++) if (index[i] < 0) throw new InvalidOperationException("bone not on the arm: " + a.bones[i]);
            var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n]; var bw = new BoneWeight[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(a.vertices[i * 3], a.vertices[i * 3 + 1], a.vertices[i * 3 + 2]);
                nr[i] = new Vector3(a.normals[i * 3], a.normals[i * 3 + 1], a.normals[i * 3 + 2]);
                uv[i] = new Vector2(a.uv[i * 2], a.uv[i * 2 + 1]);
                bw[i] = new BoneWeight
                {
                    boneIndex0 = index[a.boneIndex[i * 4]], weight0 = a.boneWeight[i * 4],
                    boneIndex1 = index[a.boneIndex[i * 4 + 1]], weight1 = a.boneWeight[i * 4 + 1],
                    boneIndex2 = index[a.boneIndex[i * 4 + 2]], weight2 = a.boneWeight[i * 4 + 2],
                    boneIndex3 = index[a.boneIndex[i * 4 + 3]], weight3 = a.boneWeight[i * 4 + 3],
                };
            }
            var mesh = new Mesh { name = arm.name + "_Tripo", indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = v; mesh.normals = nr; mesh.uv = uv;
            mesh.SetTriangles(a.triangles, 0);
            mesh.boneWeights = bw;
            mesh.bindposes = bindposes;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // The surface of the launcher's own model (not the rocket, not the arms) within `range` of a point, in `space`:
        // every triangle there sampled about every `spacing` (its vertices alone left the flat faces of the low-poly
        // pistol grip empty, and the fingers found nothing to close on).
        static Vector3[] LauncherPoints(Transform space, Vector3 near, float range, float spacing = 0.006f)
        {
            var list = new System.Collections.Generic.List<Vector3>();
            var c = space.InverseTransformPoint(near);
            foreach (var mf in space.GetComponentsInChildren<MeshFilter>(true))
            {
                // (the old graybox grip and sight are switched off: only what is drawn)
                if (mf.sharedMesh == null || mf.name.Contains("Rocket") || !mf.gameObject.activeSelf) continue;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var v = mf.sharedMesh.vertices; var tri = mf.sharedMesh.triangles;
                var p = new Vector3[v.Length]; for (int i = 0; i < v.Length; i++) p[i] = m.MultiplyPoint3x4(v[i]);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = p[tri[i]], b = p[tri[i + 1]], d = p[tri[i + 2]];
                    var centre = (a + b + d) / 3f;
                    float reach = Mathf.Max((a - centre).magnitude, (b - centre).magnitude, (d - centre).magnitude);
                    if ((centre - c).magnitude > range + reach) continue;
                    int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max((a - b).magnitude, (b - d).magnitude, (d - a).magnitude) / spacing), 1, 60);
                    for (int x = 0; x <= n; x++) for (int y = 0; y <= n - x; y++)
                    {
                        var q = a + (b - a) * (x / (float)n) + (d - a) * (y / (float)n);
                        if ((q - c).sqrMagnitude < range * range) list.Add(q);
                    }
                }
            }
            return list.ToArray();
        }

        // The launcher's signed distance round a grip (the fingers close on it), baked from its own mesh: a cube of
        // `half` either side of the grip's wrist target, `cell` apart, inside negative (by the nearest face's side).
        static GripField BakeField(Transform space, Vector3 near, string path, float half = 0.14f, float cell = 0.006f, float limit = 0.04f)
        {
            var c = space.InverseTransformPoint(near);
            var tris = new System.Collections.Generic.List<(Vector3 a, Vector3 b, Vector3 d, Vector3 n, Vector3 centre, float reach)>();
            foreach (var mf in space.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name.Contains("Rocket") || !mf.gameObject.activeSelf) continue;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var v = mf.sharedMesh.vertices; var tri = mf.sharedMesh.triangles;
                var p = new Vector3[v.Length]; for (int i = 0; i < v.Length; i++) p[i] = m.MultiplyPoint3x4(v[i]);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = p[tri[i]], b = p[tri[i + 1]], d = p[tri[i + 2]];
                    var centre = (a + b + d) / 3f; float reach = Mathf.Max((a - centre).magnitude, (b - centre).magnitude, (d - centre).magnitude);
                    if (Mathf.Abs(centre.x - c.x) > half + limit + reach || Mathf.Abs(centre.y - c.y) > half + limit + reach || Mathf.Abs(centre.z - c.z) > half + limit + reach) continue;
                    var n = Vector3.Cross(b - a, d - a); if (n.sqrMagnitude < 1e-14f) continue;
                    tris.Add((a, b, d, n.normalized, centre, reach));
                }
            }
            int count = Mathf.CeilToInt(2f * half / cell) + 1;
            var origin = c - Vector3.one * half;
            var data = new byte[count * count * count * 2];
            for (int z = 0; z < count; z++)
                for (int y = 0; y < count; y++)
                    for (int x = 0; x < count; x++)
                    {
                        var q = origin + new Vector3(x, y, z) * cell;
                        float best = limit, sign = 1f;
                        foreach (var t in tris)
                        {
                            if ((q - t.centre).magnitude - t.reach > best) continue;
                            var cp = Closest(q, t.a, t.b, t.d); float dist = (q - cp).magnitude;
                            if (dist < best) { best = dist; sign = Vector3.Dot(q - cp, t.n) < 0f ? -1f : 1f; }
                        }
                        short s = (short)Mathf.Clamp(Mathf.RoundToInt(sign * best / limit * 32767f), -32767, 32767);
                        int i = ((z * count + y) * count + x) * 2; data[i] = (byte)(s & 0xff); data[i + 1] = (byte)((s >> 8) & 0xff);
                    }
            var field = AssetDatabase.LoadAssetAtPath<GripField>(path);
            if (field == null) { field = ScriptableObject.CreateInstance<GripField>(); AssetDatabase.CreateAsset(field, path); }
            field.origin = origin; field.cell = cell; field.nx = field.ny = field.nz = count; field.limit = limit; field.data = data;
            EditorUtility.SetDirty(field); AssetDatabase.SaveAssets();
            Debug.Log($"[BazookaArmSetup] grip field {path}: {count}^3 cells of {cell * 1000f:0} mm from {tris.Count} triangles");
            return field;
        }

        // closest point on triangle abc to p (Ericson, Real-Time Collision Detection 5.1.5)
        static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap); if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp); if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2; if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp); if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6; if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4; if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float den = 1f / (va + vb + vc); return a + ab * (vb * den) + ac * (vc * den);
        }

        // A point on the face of the trigger (front, where the finger pulls), from the model itself: the trigger is the
        // thin blade inside the guard, measured 2026-11-23 at about (0, -0.11, 0.17) in the launcher's frame.
        static Transform TriggerPoint(Transform space)
        {
            var t = space.Find("Trigger_Point");
            if (t == null) { t = new GameObject("Trigger_Point").transform; t.SetParent(space, false); }
            var pts = LauncherPoints(space, space.TransformPoint(new Vector3(0f, -0.108f, 0.170f)), 0.03f, 10f)   // the vertices alone (as measured)
                .Where(p => Mathf.Abs(p.x) < 0.006f && p.y > -0.120f && p.y < -0.096f && p.z > 0.158f && p.z < 0.184f).ToArray();
            var at = pts.Length >= 3 ? new Vector3(0f, pts.Average(p => p.y), pts.Max(p => p.z)) : new Vector3(0f, -0.110f, 0.175f);
            t.localPosition = at; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
            Debug.Log($"[BazookaArmSetup] trigger point {at.ToString("F3")} from {pts.Length} vertices");
            return t;
        }

        const float SkinMicroTiling = 28f, SkinMicroStrength = 0.7f;

        // (2026-11-24, 사용자 지시 "바주카 잡는손 매끄럽게 다듬어봐 지금 m1들었을때 손과 차이감이 너무 커서") The same soldier holds
        // both weapons: the bazooka arms take the M1 hands' skin and the M1 sleeves' cloth colour, and the M1 hands' clean skin.
        // Measured with source/unity_staging_20261124_hands/measure/measure_hands.py (median over each mesh's vertices' uv, linear):
        //   M1 support hand (SupportHand_M1, the hand most in view with the M1)  0.310 0.133 0.068  (sRGB 151 102 74)
        //   M1 sleeve (M1_FP_Cloth: cloth grain x (0.280, 0.254, 0.396))          0.131 0.167 0.050  (sRGB 101 114 63, olive)
        //   Tripo arms, skin                                                      0.456 0.153 0.091  (sRGB 181 109 85: 30 % brighter, redder)
        //   Tripo arms, sleeve                                                    0.246 0.168 0.048  (sRGB 133 111 60, khaki)
        // The same steps, rendered offline to check them: measure/color_match_preview2.py (Docs/Review/offline_*_after2_hands_20261124.png).
        //  - grime: a texel of skin more than 20 % darker than the skin round it (a box of 24 texels, weighted by the skin mask, the
        //    darkest texels left out) takes that skin's colour, keeping a quarter-power of its darkness (faint);
        //  - skin: each channel scaled to the M1 hand (the scan's grain kept);
        //  - sleeve: decided per triangle by the texture's own colour, not by the skin mask (wrong in places at the wrist: a bare-wrist
        //    triangle went green): on the sleeve's triangles (not mostly on a hand bone) under 20 % skin-coloured texels (linear R/G 2.3
        //    and over; skin's 1st percentile 2.52, sleeve's 95th 2.03) the whole triangle is cloth, over 60 % it is skin, between them
        //    texel by texel (R/G under 2.1 cloth, over 2.4 skin). Next to cloth, also cloth: the cuff's lip facing the hand (vertex
        //    normal . forearm axis over 0.6), its inside wall (normal toward the axis), a triangle more than 3 mm outside the bare
        //    wrist at the same height (the cuff's face where the scan's texture is skin-coloured), up to three rings; and the uv
        //    padding within 6 texels of cloth (filtering blended it in: skin-coloured flecks on the green cuff). Cloth keeps the
        //    scan's light and shade (its spread halved) in the M1 cloth's colour.
        //  - normal: the hand scan's detail (bake_hand_detail.py) kept within 12 degrees of the soldier's own normal (median 1.5,
        //    99th percentile 24.6, at most 61.9: deep scratches showed as black lines on the knuckles and finger tips); on the skin the
        //    normal is then halved toward flat (with the soldier's own normal alone the gaps between the fingers still read as black
        //    creases; the M1 support hand has no normal map, the right hand a smooth sculpt). Cloth folds kept.
        //  - skin smoothness to 0.28 (M1 hands 0.25 / 0.30; Tripo skin 0.36).
        // The black patches on the palms in the first offline renders were Blender's shadows of the hand on itself: these arms cast
        // none in the game (m_CastShadows 0).
        static readonly Vector3 SkinTarget = new Vector3(0.3095f, 0.1329f, 0.0676f), SleeveTarget = new Vector3(0.1312f, 0.1672f, 0.0495f);
        const int DirtRadius = 24; const float DirtBelow = 0.8f, DirtKeep = 0.25f;
        const float SkinRG = 2.3f, ClothRGLow = 2.1f, ClothRGHigh = 2.4f, SleeveContrast = 0.5f, SkinSmoothness = 0.28f;
        const float NormalMaxDegrees = 12f, SkinNormalFlat = 0.5f;
        const int PaddingTexels = 6;

        static float Lum(Vector3 c) => c.x * 0.2126f + c.y * 0.7152f + c.z * 0.0722f;
        static float Median(System.Collections.Generic.List<float> v) { if (v.Count == 0) return 0f; v.Sort(); return v[v.Count / 2]; }
        static Texture2D LoadRaw(string path) { var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); t.LoadImage(File.ReadAllBytes(path)); return t; }
        static int At(int w, int h, float u, float v) { int x = Mathf.Clamp((int)(Mathf.Repeat(u, 1f) * (w - 1)), 0, w - 1), y = Mathf.Clamp((int)(Mathf.Repeat(v, 1f) * (h - 1)), 0, h - 1); return y * w + x; }

        // box mean of a float field (running sums along rows, then columns)
        static float[] Box(float[] a, int w, int h, int r)
        {
            var tmp = new float[a.Length]; var o = new float[a.Length];
            for (int y = 0; y < h; y++)
            {
                double s = 0; int row = y * w;
                for (int x = -r; x <= r; x++) s += a[row + Mathf.Clamp(x, 0, w - 1)];
                for (int x = 0; x < w; x++) { tmp[row + x] = (float)s; s += a[row + Mathf.Min(x + r + 1, w - 1)] - a[row + Mathf.Max(x - r, 0)]; }
            }
            for (int x = 0; x < w; x++)
            {
                double s = 0;
                for (int y = -r; y <= r; y++) s += tmp[Mathf.Clamp(y, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++) { o[y * w + x] = (float)s; s += tmp[Mathf.Min(y + r + 1, h - 1) * w + x] - tmp[Mathf.Max(y - r, 0) * w + x]; }
            }
            return o;
        }

        // the texels a uv triangle covers (as the offline check rasterised them)
        static void Texels(Vector2 p0, Vector2 p1, Vector2 p2, int w, int h, System.Collections.Generic.List<int> into)
        {
            Vector2 v0 = p1 - p0, v1 = p2 - p0; float d = v0.x * v1.y - v0.y * v1.x; if (Mathf.Abs(d) < 1e-9f) return;
            int x0 = Mathf.FloorToInt(Mathf.Min(p0.x, Mathf.Min(p1.x, p2.x))), x1 = Mathf.CeilToInt(Mathf.Max(p0.x, Mathf.Max(p1.x, p2.x)));
            int y0 = Mathf.FloorToInt(Mathf.Min(p0.y, Mathf.Min(p1.y, p2.y))), y1 = Mathf.CeilToInt(Mathf.Max(p0.y, Mathf.Max(p1.y, p2.y)));
            if (x1 - x0 > w / 4 || y1 - y0 > h / 4) return;     // a triangle across a uv seam
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                {
                    float qx = x - p0.x, qy = y - p0.y, u = (qx * v1.y - qy * v1.x) / d, v = (v0.x * qy - v0.y * qx) / d;
                    if (u >= -0.02f && v >= -0.02f && u + v <= 1.04f) into.Add(y * w + x);
                }
        }

        // where the base colour is cloth (1 = all of it, between = blended), from the arms' triangles (see above)
        static float[] ClothWeight(ArmFile data, SkinnedMeshRenderer[] arms, float[] rg, int w, int h)
        {
            var region = new byte[w * h]; var covered = new bool[w * h];
            foreach (var a in data.arms)
            {
                var arm = arms.FirstOrDefault(r => r.name == a.name); if (arm == null) continue;
                var bind = OriginalMesh(arm).bindposes;
                string side = a.name.EndsWith("Left") ? "Left" : "Right";
                Vector3 Rest(string bone) { int i = Array.FindIndex(arm.bones, t => t.name == "mixamorig:" + side + bone); return i >= 0 ? (Vector3)bind[i].inverse.GetColumn(3) : Vector3.zero; }
                Vector3 fore = Rest("ForeArm"), hand = Rest("Hand"); var axis = (hand - fore).normalized; float len = Vector3.Distance(hand, fore);
                int n = a.uv.Length / 2;
                var handW = new float[n]; var na = new float[n]; var nr = new float[n]; var rad = new float[n]; var bin = new int[n]; var along = new float[n];
                for (int i = 0; i < n; i++)
                {
                    for (int k = 0; k < 4; k++) if (a.bones[a.boneIndex[i * 4 + k]].Contains("Hand")) handW[i] += a.boneWeight[i * 4 + k];
                    var p = new Vector3(a.vertices[i * 3], a.vertices[i * 3 + 1], a.vertices[i * 3 + 2]); var nm = new Vector3(a.normals[i * 3], a.normals[i * 3 + 1], a.normals[i * 3 + 2]);
                    var rel = p - fore; var radial = rel - Vector3.Dot(rel, axis) * axis;
                    na[i] = Vector3.Dot(nm, axis); rad[i] = radial.magnitude; nr[i] = rad[i] > 1e-6f ? Vector3.Dot(nm, radial) / rad[i] : 0f;
                    along[i] = Vector3.Dot(rel, axis) / len; bin[i] = Mathf.Clamp((int)(along[i] * 40f), 0, 59);
                }
                int tc = a.triangles.Length / 3;
                var kind = new byte[tc]; var skinny = new float[tc]; var isHand = new bool[tc];
                var start = new int[tc + 1]; var tex = new System.Collections.Generic.List<int>();
                for (int t = 0; t < tc; t++)
                {
                    int i0 = a.triangles[t * 3], i1 = a.triangles[t * 3 + 1], i2 = a.triangles[t * 3 + 2];
                    start[t] = tex.Count;
                    Vector2 P(int i) => new Vector2(a.uv[i * 2] * (w - 1), a.uv[i * 2 + 1] * (h - 1));
                    Texels(P(i0), P(i1), P(i2), w, h, tex);
                    isHand[t] = handW[i0] >= 0.5f || handW[i1] >= 0.5f || handW[i2] >= 0.5f;
                    int cnt = tex.Count - start[t], sk = 0; for (int j = start[t]; j < tex.Count; j++) if (rg[tex[j]] >= SkinRG) sk++;
                    skinny[t] = cnt > 0 ? sk / (float)cnt : 1f;
                    kind[t] = (byte)(cnt == 0 || isHand[t] || skinny[t] > 0.6f ? 0 : skinny[t] < 0.2f ? 2 : 1);
                    // (pass 2) the sleeve up the forearm and the arm is all cloth whatever its colour: the scan's orange-brown grime
                    // there read as skin and kept the skin's colour, streaks on the green sleeve. Bare skin begins 70 % of the way
                    // down the forearm (measured: skin-coloured vertices from 0.7 of elbow-to-wrist on, both arms).
                    if (cnt > 0 && !isHand[t] && (along[i0] + along[i1] + along[i2]) / 3f < 0.65f) kind[t] = 2;
                }
                start[tc] = tex.Count;
                for (int ring = 0; ring < 3; ring++)
                {
                    var clothV = new bool[n]; var skinV = new bool[n];
                    for (int t = 0; t < tc; t++) if (kind[t] == 2) for (int c = 0; c < 3; c++) clothV[a.triangles[t * 3 + c]] = true;
                    for (int t = 0; t < tc; t++)
                    {
                        int i0 = a.triangles[t * 3], i1 = a.triangles[t * 3 + 1], i2 = a.triangles[t * 3 + 2];
                        if (kind[t] == 0 && !isHand[t] && skinny[t] > 0.9f && !clothV[i0] && !clothV[i1] && !clothV[i2]) skinV[i0] = skinV[i1] = skinV[i2] = true;
                    }
                    var skinR = new float[60];
                    for (int b = 0; b < 60; b++)
                    {
                        var vals = new System.Collections.Generic.List<float>();
                        for (int i = 0; i < n; i++) if (skinV[i] && bin[i] == b) vals.Add(rad[i]);
                        skinR[b] = vals.Count >= 8 ? Median(vals) : float.PositiveInfinity;
                    }
                    int changed = 0;
                    for (int t = 0; t < tc; t++)
                    {
                        if (kind[t] == 2 || start[t + 1] == start[t]) continue;
                        int i0 = a.triangles[t * 3], i1 = a.triangles[t * 3 + 1], i2 = a.triangles[t * 3 + 2];
                        if (!clothV[i0] && !clothV[i1] && !clothV[i2]) continue;
                        float meanNa = (na[i0] + na[i1] + na[i2]) / 3f, meanNr = (nr[i0] + nr[i1] + nr[i2]) / 3f, meanR = (rad[i0] + rad[i1] + rad[i2]) / 3f;
                        bool outer = meanR > Mathf.Min(skinR[bin[i0]], Mathf.Min(skinR[bin[i1]], skinR[bin[i2]])) + 0.003f;
                        if (meanNa > 0.6f || meanNr < -0.2f || (isHand[t] && skinny[t] < 0.2f) || (!isHand[t] && outer)) { kind[t] = 2; changed++; }
                    }
                    if (changed == 0) break;
                }
                for (int t = 0; t < tc; t++)
                    for (int j = start[t]; j < start[t + 1]; j++) { int px = tex[j]; covered[px] = true; if (kind[t] > region[px]) region[px] = kind[t]; }
            }
            // uv padding next to cloth
            var full = new float[w * h]; for (int i = 0; i < full.Length; i++) full[i] = region[i] == 2 ? 1f : 0f;
            var near = Box(full, w, h, PaddingTexels);
            var weight = new float[w * h];
            for (int i = 0; i < weight.Length; i++)
            {
                if (region[i] == 2 || (!covered[i] && near[i] > 0f)) weight[i] = 1f;
                else if (region[i] == 1) weight[i] = Mathf.Clamp01((ClothRGHigh - rg[i]) / (ClothRGHigh - ClothRGLow));
            }
            return weight;
        }

        // the base colour, matched to the M1 hands and sleeves (see above)
        static void MatchedBaseColor(ArmFile data, SkinnedMeshRenderer[] arms, string source, string mask, string dest)
        {
            var tex = LoadRaw(source); var msk = LoadRaw(mask);
            int w = tex.width, h = tex.height;
            if (msk.width != w || msk.height != h) throw new InvalidOperationException("skin mask and base colour differ in size");
            var lin = new float[256]; for (int i = 0; i < 256; i++) lin[i] = Mathf.GammaToLinearSpace(i / 255f);
            var px = tex.GetPixels32(); var mp = msk.GetPixels32();
            Object.DestroyImmediate(msk);
            var sr = new System.Collections.Generic.List<float>(); var sg = new System.Collections.Generic.List<float>(); var sb = new System.Collections.Generic.List<float>();
            var cr = new System.Collections.Generic.List<float>(); var cg = new System.Collections.Generic.List<float>(); var cb = new System.Collections.Generic.List<float>();
            foreach (var a in data.arms)
                for (int i = 0; i < a.uv.Length / 2; i++)
                {
                    int at = At(w, h, a.uv[i * 2], a.uv[i * 2 + 1]); var c = px[at];
                    if (mp[at].r > 127) { sr.Add(lin[c.r]); sg.Add(lin[c.g]); sb.Add(lin[c.b]); } else { cr.Add(lin[c.r]); cg.Add(lin[c.g]); cb.Add(lin[c.b]); }
                }
            var skinSrc = new Vector3(Median(sr), Median(sg), Median(sb)); var sleeveSrc = new Vector3(Median(cr), Median(cg), Median(cb));
            var gain = new Vector3(SkinTarget.x / skinSrc.x, SkinTarget.y / skinSrc.y, SkinTarget.z / skinSrc.z);
            float skinLum = Lum(skinSrc), sleeveLum = Lum(sleeveSrc);
            var rg = new float[w * h];
            for (int i = 0; i < rg.Length; i++) rg[i] = lin[px[i].r] / Mathf.Max(lin[px[i].g], 1e-4f);
            var cloth = ClothWeight(data, arms, rg, w, h);
            rg = null;
            // grime: the skin round each texel, at half resolution (it is a 24-texel average)
            int hw = w / 2, hh = h / 2;
            var wr = new float[hw * hh]; var wg = new float[hw * hh]; var wb = new float[hw * hh]; var ww = new float[hw * hh];
            for (int y = 0; y < hh; y++) for (int x = 0; x < hw; x++)
            {
                int i = (y * 2) * w + x * 2, j = y * hw + x;
                var L = new Vector3(lin[px[i].r], lin[px[i].g], lin[px[i].b]);
                float m = mp[i].r / 255f, wt = Lum(L) > 0.5f * skinLum ? m : 0f;
                wr[j] = L.x * wt; wg[j] = L.y * wt; wb[j] = L.z * wt; ww[j] = wt;
            }
            int r2 = DirtRadius / 2;
            var br = Box(wr, hw, hh, r2); var bg = Box(wg, hw, hh, r2); var bb = Box(wb, hw, hh, r2); var bw = Box(ww, hw, hh, r2);
            wr = wg = wb = ww = null;
            for (int i = 0; i < px.Length; i++)
            {
                int x = i % w, y = i / w, j = Mathf.Min(y / 2, hh - 1) * hw + Mathf.Min(x / 2, hw - 1);
                var L = new Vector3(lin[px[i].r], lin[px[i].g], lin[px[i].b]); float m = mp[i].r / 255f, l = Lum(L);
                var clean = L;
                if (bw[j] > 1e-3f)
                {
                    var local = new Vector3(br[j], bg[j], bb[j]) / bw[j];
                    float r = l / Mathf.Max(Lum(local), 1e-6f);
                    float d = Mathf.Clamp01((DirtBelow - r) / 0.2f) * m;
                    if (d > 0f) clean = Vector3.Lerp(L, local * Mathf.Pow(Mathf.Clamp(r, 0.05f, 1f), DirtKeep), d);
                }
                var skin = Vector3.Scale(clean, gain);
                var sleeve = SleeveTarget * Mathf.Pow(l / Mathf.Max(sleeveLum, 1e-6f), SleeveContrast);
                var o = Vector3.Lerp(skin, sleeve, cloth[i]);
                px[i] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(o.x)) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(o.y)) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(o.z)) * 255f), px[i].a);
            }
            tex.SetPixels32(px); tex.Apply(false);
            File.WriteAllBytes(dest, tex.EncodeToPNG());
            Debug.Log($"[BazookaArmSetup] base colour matched to the M1 hands: skin {skinSrc.ToString("F3")} x {gain.ToString("F3")}, sleeve {sleeveSrc.ToString("F3")} -> {SleeveTarget.ToString("F3")}");
            Object.DestroyImmediate(tex);
        }

        // the soldier's normal with the hand scan's detail kept within NormalMaxDegrees, halved toward flat on the skin (see above)
        static void CappedNormal(string baseNormal, string detailNormal, string mask, string dest)
        {
            var nb = LoadRaw(baseNormal); var nd = LoadRaw(detailNormal); var msk = LoadRaw(mask);
            int w = nd.width, h = nd.height;
            var b = nb.GetPixels32(); var d = nd.GetPixels32(); var m = msk.GetPixels32();
            Vector3 Dec(Color32 c) => new Vector3(c.r / 255f * 2f - 1f, c.g / 255f * 2f - 1f, c.b / 255f * 2f - 1f).normalized;
            Vector3 BaseAt(float u, float v)
            {
                // bilinear in the (smaller) soldier's normal
                float fx = u * nb.width - 0.5f, fy = v * nb.height - 0.5f; int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, nb.width - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, nb.height - 1);
                int x1 = Mathf.Min(x0 + 1, nb.width - 1), y1 = Mathf.Min(y0 + 1, nb.height - 1); float tx = Mathf.Clamp01(fx - x0), ty = Mathf.Clamp01(fy - y0);
                var top = Vector3.Lerp(Dec(b[y0 * nb.width + x0]), Dec(b[y0 * nb.width + x1]), tx); var bot = Vector3.Lerp(Dec(b[y1 * nb.width + x0]), Dec(b[y1 * nb.width + x1]), tx);
                return Vector3.Lerp(top, bot, ty).normalized;
            }
            for (int i = 0; i < d.Length; i++)
            {
                int x = i % w, y = i / w;
                var A = BaseAt((x + 0.5f) / w, (y + 0.5f) / h); var B = Dec(d[i]);
                float ang = Vector3.Angle(A, B);
                var N = Vector3.Lerp(A, B, Mathf.Min(1f, NormalMaxDegrees / Mathf.Max(ang, 1e-3f))).normalized;
                float sk = m[(y * msk.height / h) * msk.width + x * msk.width / w].r / 255f;
                N = Vector3.Lerp(N, Vector3.forward, SkinNormalFlat * sk).normalized;
                d[i] = new Color32((byte)Mathf.RoundToInt((N.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((N.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((N.z * 0.5f + 0.5f) * 255f), 255);
            }
            nd.SetPixels32(d); nd.Apply(false);
            File.WriteAllBytes(dest, nd.EncodeToPNG());
            Object.DestroyImmediate(nb); Object.DestroyImmediate(nd); Object.DestroyImmediate(msk);
        }

        static Material BuildMaterial(ArmFile data, SkinnedMeshRenderer[] arms)
        {
            // the skin-detail versions (bake_hand_detail.py: the hand scan baked into the hands) when they exist
            string Pick(string file) { var detail = Path.Combine(Source, Path.GetFileNameWithoutExtension(file) + "_detail.png"); return File.Exists(detail) ? detail : Path.Combine(Source, file); }
            var basePath = $"{Dir}/T_CH_ROK_Arms_Tripo_BaseColor.png";
            var normalPath = $"{Dir}/T_CH_ROK_Arms_Tripo_Normal.png";
            var skinMask = Path.Combine(Source, "T_soldier_SkinMask.png");
            var detailNormal = Path.Combine(Source, "T_soldier_Normal_detail.png");
            if (File.Exists(skinMask)) MatchedBaseColor(data, arms, Pick("T_soldier_BaseColor.png"), skinMask, basePath);
            else File.Copy(Pick("T_soldier_BaseColor.png"), basePath, true);
            if (File.Exists(skinMask) && File.Exists(detailNormal)) CappedNormal(Path.Combine(Source, "T_soldier_Normal.png"), detailNormal, skinMask, normalPath);
            else File.Copy(Pick("T_soldier_Normal.png"), normalPath, true);
            var maskPath = $"{Dir}/T_CH_ROK_Arms_Tripo_MetallicSmoothness.png";
            // URP mask: R metallic, A smoothness (= 1 - roughness); on the skin scaled to SkinSmoothness at its median
            var metal = LoadRaw(Path.Combine(Source, "T_soldier_Metallic.png")); var rough = LoadRaw(Path.Combine(Source, "T_soldier_Roughness.png"));
            if (rough.width != metal.width || rough.height != metal.height) throw new InvalidOperationException("metallic and roughness maps differ in size");
            var mp = metal.GetPixels32(); var r = rough.GetPixels32();
            Texture2D skinTex = File.Exists(skinMask) ? LoadRaw(skinMask) : null; Color32[] sk = skinTex != null ? skinTex.GetPixels32() : null;
            float SkinAt(int i) { if (sk == null) return 0f; int x = i % metal.width, y = i / metal.width; return sk[(y * skinTex.height / metal.height) * skinTex.width + x * skinTex.width / metal.width].r / 255f; }
            float smoothScale = 1f;
            if (sk != null)
            {
                var vals = new System.Collections.Generic.List<float>();
                foreach (var a in data.arms) for (int i = 0; i < a.uv.Length / 2; i++)
                    { float u = a.uv[i * 2], v = a.uv[i * 2 + 1]; if (sk[At(skinTex.width, skinTex.height, u, v)].r > 127) vals.Add(1f - r[At(rough.width, rough.height, u, v)].r / 255f); }
                float median = Median(vals); if (median > 0.01f) smoothScale = SkinSmoothness / median;
                Debug.Log($"[BazookaArmSetup] skin smoothness {median:0.000} -> {SkinSmoothness:0.00}");
            }
            for (int i = 0; i < mp.Length; i++)
            {
                float smooth = (255 - r[i].r) / 255f * Mathf.Lerp(1f, smoothScale, SkinAt(i));
                mp[i] = new Color32(mp[i].r, 0, 0, (byte)Mathf.RoundToInt(Mathf.Clamp01(smooth) * 255f));
            }
            var mask = new Texture2D(metal.width, metal.height, TextureFormat.RGBA32, false, true); mask.SetPixels32(mp); mask.Apply();
            File.WriteAllBytes(maskPath, mask.EncodeToPNG());
            Object.DestroyImmediate(metal); Object.DestroyImmediate(rough); Object.DestroyImmediate(mask); if (skinTex != null) Object.DestroyImmediate(skinTex);
            AssetDatabase.Refresh();
            Import(basePath, true, false); Import(normalPath, false, true); Import(maskPath, false, false);

            var matPath = $"{Dir}/M_CH_ROK_Arms_Tripo.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath)); mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)); mat.SetFloat("_BumpScale", 1f); mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath)); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 1f); mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.SetFloat("_Cull", 2f);
            // skin micro detail (pores, fine cross wrinkles): a tiling normal over the skin only (mask from bake_hand_detail.py)
            if (File.Exists(skinMask) && File.Exists(Path.Combine(Source, "T_skin_micro_normal.png")))
            {
                var skinPath = $"{Dir}/T_CH_ROK_Arms_Tripo_SkinMask.png"; File.Copy(skinMask, skinPath, true);
                var microPath = $"{Dir}/T_CH_Skin_MicroNormal.png"; File.Copy(Path.Combine(Source, "T_skin_micro_normal.png"), microPath, true);
                AssetDatabase.Refresh();
                Import(skinPath, false, false); Import(microPath, false, true);
                var micro = (TextureImporter)AssetImporter.GetAtPath(microPath); micro.wrapMode = TextureWrapMode.Repeat; micro.maxTextureSize = 512; micro.SaveAndReimport();
                mat.SetTexture("_DetailMask", AssetDatabase.LoadAssetAtPath<Texture2D>(skinPath));
                mat.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(microPath));
                mat.SetFloat("_DetailNormalMapScale", SkinMicroStrength); mat.SetFloat("_DetailAlbedoMapScale", 1f);
                mat.SetTextureScale("_DetailAlbedoMap", new Vector2(SkinMicroTiling, SkinMicroTiling));
                mat.EnableKeyword("_DETAIL_MULX2");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void Import(string path, bool srgb, bool normal)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb; ti.maxTextureSize = 4096; ti.mipmapEnabled = true; ti.anisoLevel = 8;
            ti.alphaSource = !srgb && !normal ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        static Mesh Save(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); existing.name = Path.GetFileNameWithoutExtension(path);
            Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }
    }
}
