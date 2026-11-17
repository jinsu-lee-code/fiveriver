using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 2026-11-16: with the bazooka held at the hip the forearms showed bare skin. The arms are the same delivered
    // SK_CH_ROK_Arms as on the M1, whose sleeve covers the forearm; the texture itself has the sleeves rolled up.
    // For the bazooka's own material only, the forearm area of the texture (found from the mesh's bone weights,
    // forearm and upper arm against hand) is repainted in the uniform's own cloth colour, taken from the upper sleeve
    // of the same texture, with the baked shading kept, and a darker fold where the sleeve ends at the wrist.
    // The M1's material keeps the original texture. Re-running rebuilds the same texture.
    public static class BazookaSleeveSetup
    {
        const string Source = "Assets/_Project/Art/Models/Characters/ROK_Arms/Textures/T_CH_ROK_Arms_BaseColor.png";
        const string Output = "Assets/_Project/Art/Models/Characters/ROK_Arms/Textures/T_CH_ROK_Arms_Sleeved_BaseColor.png";
        const string Material = "Assets/_Project/Art/Models/Characters/ROK_Arms/M_CH_ROK_Bazooka_FP.mat";

        [MenuItem("FiveRivers/Setup/Bazooka Sleeved Arms")]
        public static string Apply()
        {
            var report = new StringBuilder();
            var arms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "VM_Bazooka")
                .GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null && r.sharedMesh.name.Contains("Arm")).ToArray();
            if (arms.Length == 0) throw new System.InvalidOperationException("bazooka arms not found");
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            src.LoadImage(File.ReadAllBytes(Source));
            int W = src.width, Hh = src.height;
            var px = src.GetPixels32();
            var mask = new byte[W * Hh];        // 1 sleeve, 2 cuff fold
            var upper = new bool[W * Hh];       // upper-arm cloth, the colour reference
            var handArea = new bool[W * Hh];    // hand and fingers: stays skin
            var covered = new bool[W * Hh];     // inside some UV triangle
            var armArea = new bool[W * Hh];     // inside a forearm / upper-arm triangle away from the hand
            var roll = new bool[W * Hh];        // cloth on forearm-weighted triangles: the rolled-up sleeve at the elbow

            foreach (var smr in arms)
            {
                var mesh = smr.sharedMesh;
                var names = smr.bones.Select(b => b != null ? b.name : "").ToArray();
                bool IsHand(string n) => n.Contains("Hand");                               // hand and fingers
                bool IsFore(string n) => n.Contains("ForeArm");
                bool IsUpper(string n) => (n.EndsWith("LeftArm") || n.EndsWith("RightArm") || n.Contains("Shoulder"));
                var bw = mesh.boneWeights;
                var uv = mesh.uv;
                var tris = mesh.triangles;
                float W3(BoneWeight w, System.Func<string, bool> f)
                {
                    float s = 0;
                    if (w.weight0 > 0 && f(names[w.boneIndex0])) s += w.weight0;
                    if (w.weight1 > 0 && f(names[w.boneIndex1])) s += w.weight1;
                    if (w.weight2 > 0 && f(names[w.boneIndex2])) s += w.weight2;
                    if (w.weight3 > 0 && f(names[w.boneIndex3])) s += w.weight3;
                    return s;
                }
                var hand = bw.Select(w => W3(w, IsHand)).ToArray();
                var arm = bw.Select(w => W3(w, n => IsFore(n) || IsUpper(n))).ToArray();
                var up = bw.Select(w => W3(w, IsUpper)).ToArray();
                // the rolled-up sleeve hangs on the upper-arm bone just above the elbow: vertices near an elbow joint
                // (the forearm bones' bind position), within 40 % of that arm's upper-arm length
                var bind = mesh.bindposes;
                var verts = mesh.vertices;
                var elbows = names.Select((n, i) => (n, i)).Where(p => IsFore(p.n)).Select(p => (Vector3)bind[p.i].inverse.GetColumn(3)).ToArray();
                var shoulders = names.Select((n, i) => (n, i)).Where(p => IsUpper(p.n) && !p.n.Contains("Shoulder")).Select(p => (Vector3)bind[p.i].inverse.GetColumn(3)).ToArray();
                var reach = elbows.Select(e => shoulders.Length > 0 ? .40f * shoulders.Min(s => Vector3.Distance(s, e)) : 0f).ToArray();
                report.AppendLine($"{smr.name}: elbows {elbows.Length}, roll reach {string.Join(" / ", reach.Select(r => r.ToString("0.000")))}");
                bool NearElbow(int vi) { for (int e = 0; e < elbows.Length; e++) if (Vector3.Distance(verts[vi], elbows[e]) < reach[e]) return true; return false; }
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    float handAvg = (hand[a] + hand[b] + hand[c]) / 3f, armAvg = (arm[a] + arm[b] + arm[c]) / 3f, upAvg = (up[a] + up[b] + up[c]) / 3f;
                    bool elbowTri = NearElbow(a) || NearElbow(b) || NearElbow(c);
                    Raster(uv[a], uv[b], uv[c], W, Hh, (i, l0, l1, l2) =>
                    {
                        float hw = hand[a] * l0 + hand[b] * l1 + hand[c] * l2;
                        if (upAvg > .6f) upper[i] = true;
                        if (elbowTri && hw < .30f) roll[i] = true;
                        covered[i] = true;
                        if (hw >= .45f) handArea[i] = true;
                        else if (armAvg >= .35f) armArea[i] = true;
                        if (armAvg >= .55f && hw < .30f) mask[i] = 1;
                        else if (armAvg >= .35f && hw >= .30f && hw < .45f) mask[i] = 2;
                    });
                }
            }

            bool Skin(Color32 c) => c.r > c.g + 12 && c.r > c.b + 22 && c.r > 90;
            // cloth reference: upper-arm pixels that are cloth, not skin
            double cr = 0, cg = 0, cb = 0; int cn = 0;
            double sl = 0; int sn = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (upper[i] && !Skin(px[i]) && px[i].a > 0 && (px[i].r + px[i].g + px[i].b) > 60) { cr += px[i].r; cg += px[i].g; cb += px[i].b; cn++; }
                if (mask[i] != 0 && Skin(px[i])) { sl += Lum(px[i]); sn++; }
            }
            if (cn == 0 || sn == 0) throw new System.InvalidOperationException($"no reference pixels cloth {cn} skin {sn}");
            var cloth = new Color((float)(cr / cn) / 255f, (float)(cg / cn) / 255f, (float)(cb / cn) / 255f);
            float skinMean = (float)(sl / sn);
            report.AppendLine($"cloth ref {cn} px colour {cloth} · forearm skin {sn} px mean lum {skinMean:0.0}");

            // the forearm islands are wider than the triangles that are clearly forearm: fill outward over connected
            // skin (the island and its padding) until the hand begins, so no pink rim is left along the seams
            // across the island (the hand stays skin), and up to 32 px of padding around it (mip bleed), never from the padding into another island
            var queue = new System.Collections.Generic.Queue<int>();
            var pad = new byte[W * Hh];
            for (int i = 0; i < mask.Length; i++) if (mask[i] != 0) queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % W, y = i / W;
                foreach (int n in new[] { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < Hh - 1 ? i + W : -1 })
                {
                    if (n < 0 || mask[n] != 0 || handArea[n] || !Skin(px[n])) continue;
                    if (covered[n] && !covered[i]) continue;                       // padding never leads into another island
                    if (!covered[n]) { if (pad[i] >= 32) continue; pad[n] = (byte)(pad[i] + 1); }
                    mask[n] = mask[i] == 2 ? (byte)2 : (byte)1;
                    queue.Enqueue(n);
                }
            }

            // the rolled-up sleeve at the elbow (cloth already in the texture, on forearm-weighted triangles): measured
            // before the forearm is repainted
            var rollIdx = new System.Collections.Generic.List<int>();
            double rl = 0;
            for (int i = 0; i < px.Length; i++)
                if (roll[i] && !handArea[i] && px[i].a > 0 && !Skin(px[i]) && Lum(px[i]) > 25f) { rollIdx.Add(i); rl += Lum(px[i]); }  // cloth, inside the sleeve mask or not (the mask only repaints skin)
            float rollMean = rollIdx.Count > 0 ? (float)(rl / rollIdx.Count) : 0f;
            {
                int all = 0, inMask = 0, skin = 0, hand = 0, dark = 0; double lAll = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    if (!roll[i]) continue;
                    all++; lAll += Lum(px[i]);
                    if (mask[i] != 0) inMask++; else if (handArea[i]) hand++; else if (Skin(px[i])) skin++; else if (Lum(px[i]) <= 25f) dark++;
                }
                report.AppendLine($"elbow area {all} px (mean lum {(all > 0 ? lAll / all : 0):0.0}): sleeve-mask {inMask}, hand {hand}, skin-coloured {skin}, dark {dark}");
            }

            var rnd = new System.Random(7);
            int changed = 0; double sleeveSum = 0; int sleeveN = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (mask[i] == 0 || !Skin(px[i])) continue;
                float shade = Mathf.Clamp(Lum(px[i]) / skinMean, .55f, 1.3f) * (1f + ((float)rnd.NextDouble() - .5f) * .05f);
                if (mask[i] == 2) shade *= .74f; // the fold where the sleeve is pushed up at the wrist
                var c = cloth * shade;
                px[i] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255), (byte)Mathf.Clamp(c.b * 255f, 0, 255), px[i].a);
                if (mask[i] == 1) { sleeveSum += Lum(px[i]); sleeveN++; }
                changed++;
            }
            // 2026-11-16 (user: "작은 것도 고치고 나서 이야기해"): that roll is pale khaki in the delivered texture and sits
            // nearest the camera, so it showed far brighter than the new forearm sleeve. Bring it to the same cloth: the
            // forearm sleeve's mean brightness (a touch under, it is the inside of the fold), its own folds kept as
            // relative shading, the colour pulled most of the way to the uniform cloth.
            float sleeveMean = sleeveN > 0 ? (float)(sleeveSum / sleeveN) : Lum(new Color32((byte)(cloth.r * 255), (byte)(cloth.g * 255), (byte)(cloth.b * 255), 255));
            float clothLum = .299f * cloth.r * 255f + .587f * cloth.g * 255f + .114f * cloth.b * 255f;
            // 2026-11-16 (second pass): fixing only the pale folds left the roll as a different, beige cloth on screen
            // (it sits nearest the camera and catches the first-person fill light). The whole roll is now the uniform's
            // khaki: every cloth pixel of the elbow area gets the cloth colour, at the forearm sleeve's brightness less
            // RollDarken (the fill light lifts it back), its own folds kept as relative shading. The change fades in over
            // RollFeather px from the edge of the area so there is no seam against the upper sleeve.
            const float RollDarken = .68f, RollFeather = 14f; // 2026-11-17: .87 still read 1.5x the sandbags behind it in the sprint view
            var inRoll = new bool[W * Hh];
            foreach (int i in rollIdx) inRoll[i] = true;
            var edge = new float[W * Hh];
            for (int i = 0; i < edge.Length; i++) edge[i] = float.MaxValue;
            var bfs = new System.Collections.Generic.Queue<int>();
            foreach (int i in rollIdx)
            {
                int x = i % W, y = i / W;
                bool border = x == 0 || y == 0 || x == W - 1 || y == Hh - 1 || !inRoll[i - 1] || !inRoll[i + 1] || !inRoll[i - W] || !inRoll[i + W];
                if (border) { edge[i] = 0f; bfs.Enqueue(i); }
            }
            while (bfs.Count > 0)
            {
                int i = bfs.Dequeue(), x = i % W, y = i / W;
                if (edge[i] >= RollFeather) continue;
                foreach (int n in new[] { x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < Hh - 1 ? i + W : -1 })
                    if (n >= 0 && inRoll[n] && edge[n] > edge[i] + 1f) { edge[n] = edge[i] + 1f; bfs.Enqueue(n); }
            }
            float target = sleeveMean * RollDarken;
            double before = 0, after = 0;
            foreach (int i in rollIdx)
            {
                var o = px[i];
                float l = Lum(o); before += l;
                float rel = Mathf.Clamp(l / Mathf.Max(1f, rollMean), .6f, 1.35f);       // the fold's own shading
                float nl = target * rel;
                var toCloth = cloth * (nl / Mathf.Max(1f, clothLum));
                var keep = new Color(o.r / 255f, o.g / 255f, o.b / 255f) * (nl / Mathf.Max(1f, l));
                var c = Color.Lerp(keep, toCloth, .9f);                                   // colour almost fully to the uniform
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Min(edge[i], RollFeather) / RollFeather);
                var o01 = new Color(o.r / 255f, o.g / 255f, o.b / 255f);
                c = Color.Lerp(o01, c, Mathf.Lerp(.35f, 1f, t));                         // soft edge, never fully left out
                px[i] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255), (byte)Mathf.Clamp(c.b * 255f, 0, 255), o.a);
                after += Lum(px[i]);
            }
            int rn = Mathf.Max(1, rollIdx.Count);
            report.AppendLine($"elbow roll {rollIdx.Count} px: mean lum {before / rn:0.0} → {after / rn:0.0} (forearm sleeve {sleeveMean:0.0}, target {target:0.0}, feather {RollFeather} px)");
            var outTex = new Texture2D(W, Hh, TextureFormat.RGBA32, false, false);
            outTex.SetPixels32(px); outTex.Apply();
            File.WriteAllBytes(Output, outTex.EncodeToPNG());
            Object.DestroyImmediate(outTex); Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(Source) is TextureImporter si && AssetImporter.GetAtPath(Output) is TextureImporter oi)
            {
                oi.sRGBTexture = si.sRGBTexture; oi.maxTextureSize = si.maxTextureSize; oi.mipmapEnabled = si.mipmapEnabled;
                oi.textureCompression = si.textureCompression; oi.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Output);
            var mat = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(Material);
            mat.SetTexture("_BaseMap", tex); mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            report.AppendLine($"pixels repainted {changed} ({100f * changed / px.Length:0.0}% of the texture) → {Output}, set on {Material}");
            File.WriteAllText("Temp/bazooka_sleeve.txt", report.ToString());
            return "sleeved";
        }

        static float Lum(Color32 c) => .299f * c.r + .587f * c.g + .114f * c.b;

        static void Raster(Vector2 a, Vector2 b, Vector2 c, int W, int H, System.Action<int, float, float, float> plot)
        {
            Vector2 A = new Vector2(a.x * W, a.y * H), B = new Vector2(b.x * W, b.y * H), C = new Vector2(c.x * W, c.y * H);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x)))), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y)))), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))));
            float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float l1 = ((p.x - A.x) * (C.y - A.y) - (C.x - A.x) * (p.y - A.y)) / area;
                    float l2 = ((B.x - A.x) * (p.y - A.y) - (p.x - A.x) * (B.y - A.y)) / area;
                    float l0 = 1f - l1 - l2;
                    if (l0 < -.01f || l1 < -.01f || l2 < -.01f) continue;
                    plot(y * W + x, l0, l1, l2);
                }
        }
    }
}
