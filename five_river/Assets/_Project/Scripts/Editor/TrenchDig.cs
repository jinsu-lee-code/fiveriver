using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FiveRivers.Core;
using UnityEditor;
using UnityEngine;

namespace FiveRivers.EditorTools
{
    // 참호를 실제로 파인 참호로 (2026-11-16, 사용자 지시 "참호 지형부터"). 조사 메뉴는 Temp/trench_probe.txt 에 쓴다.
    public static class TrenchDig
    {
        const string ProbePath = "Temp/trench_probe.txt";

        [MenuItem("FiveRivers/Dev/Trench Probe")]
        public static void Probe()
        {
            var sb = new StringBuilder();
            var terrain = Terrain.activeTerrain;
            var data = terrain.terrainData;
            sb.AppendLine($"terrain {terrain.name} pos {terrain.transform.position} size {data.size} heightRes {data.heightmapResolution} alphaRes {data.alphamapResolution} layers {string.Join(",", data.terrainLayers.Select(l => l.name))} holesRes {data.holesResolution}");
            var points = UnityEngine.Object.FindAnyObjectByType<LevelPoints>(FindObjectsInactive.Include);
            void P(string n, Transform t) { if (t != null) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "point {0} {1} ground {2:0.00} parent {3}", n, t.position, Ground(terrain, t.position), t.parent != null ? t.parent.name : "-")); }
            P("playerSpawn", points.playerSpawn); P("tankCheckpoint", points.tankCheckpoint); P("moundTop", points.moundTop);
            P("parkSpot", points.parkSpot); P("blanketSpot", points.blanketSpot); P("target", points.target);
            P("reinforcementsStart", points.reinforcementsStart); P("reinforcementsEnd", points.reinforcementsEnd);
            if (points.allySpots != null) for (int i = 0; i < points.allySpots.Length; i++) P("ally" + i, points.allySpots[i]);
            if (points.ammoCrates != null) for (int i = 0; i < points.ammoCrates.Length; i++) P("ammo" + i, points.ammoCrates[i]);

            // which splat layer is the trench floor, and where is it painted
            int floor = Array.FindIndex(data.terrainLayers, l => l.name.Contains("TrenchOnly"));
            sb.AppendLine("floor layer index " + floor);
            if (floor >= 0)
            {
                int res = data.alphamapResolution;
                var maps = data.GetAlphamaps(0, 0, res, res);
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue; int n = 0;
                var rows = new System.Collections.Generic.SortedDictionary<int, (float a, float b)>();
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                    {
                        if (maps[z, x, floor] < 0.5f) continue;
                        float wx = terrain.transform.position.x + (x + .5f) / res * data.size.x, wz = terrain.transform.position.z + (z + .5f) / res * data.size.z;
                        minX = Mathf.Min(minX, wx); maxX = Mathf.Max(maxX, wx); minZ = Mathf.Min(minZ, wz); maxZ = Mathf.Max(maxZ, wz); n++;
                        int key = Mathf.RoundToInt(wx / 4f) * 4;
                        rows[key] = rows.TryGetValue(key, out var r) ? (Mathf.Min(r.a, wz), Mathf.Max(r.b, wz)) : (wz, wz);
                    }
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "floor painted cells {0} x {1:0.0}..{2:0.0} z {3:0.0}..{4:0.0}", n, minX, maxX, minZ, maxZ));
                foreach (var kv in rows) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  floor x {0}: z {1:0.0}..{2:0.0}", kv.Key, kv.Value.a, kv.Value.b));
            }

            // terrain height sections across the trench (north-south) every 6 m of x
            for (int x = -30; x <= 30; x += 6)
            {
                sb.Append($"section x {x}:");
                for (float z = -8f; z <= 6f; z += 1f) sb.Append(string.Format(CultureInfo.InvariantCulture, " {0:0.00}", Ground(terrain, new Vector3(x, 0, z))));
                sb.AppendLine("   (z -8..6)");
            }

            // everything with a renderer near the trench: name, bounds
            var box = new Bounds(new Vector3(0, 0.5f, -1.5f), new Vector3(80, 6, 16));
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => !(r is ParticleSystemRenderer) && box.Contains(r.bounds.center) && r.bounds.size.magnitude < 40f && r.GetComponentInParent<Terrain>() == null)
                .OrderBy(r => r.bounds.center.x))
            {
                var t = r.transform;
                string path = t.name; for (var p = t.parent; p != null && path.Length < 110; p = p.parent) path = p.name + "/" + path;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "obj {0} c ({1:0.0},{2:0.00},{3:0.0}) min.y {4:0.00} max.y {5:0.00} size ({6:0.0},{7:0.0},{8:0.0}) col {9} static {10}",
                    path, r.bounds.center.x, r.bounds.center.y, r.bounds.center.z, r.bounds.min.y, r.bounds.max.y, r.bounds.size.x, r.bounds.size.y, r.bounds.size.z,
                    r.GetComponentInParent<Collider>() != null, r.gameObject.isStatic));
            }
            foreach (var s in UnityEngine.Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                sb.AppendLine($"navmesh surface {s.name} collect {s.collectObjects} geometry {s.useGeometry} layers {s.layerMask.value} data {(s.navMeshData != null ? AssetDatabase.GetAssetPath(s.navMeshData) : "-")}");
            File.WriteAllText(ProbePath, sb.ToString());
        }

        static float Ground(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;

        // Holders that only sit at the origin (no body of their own) are not part of the trench.
        static readonly string[] Holders = { "[Systems]", "[Enemies]", "[DawnGrade]", "[Audio]", "Moon_Bounce", "Global Volume" };
        static bool IsHolder(Transform t) => Holders.Contains(t.name) || t.name.StartsWith("ShellCraters") || t.GetComponent<Light>() is Light l && l.type == LightType.Directional;

        [MenuItem("FiveRivers/Dev/Trench Dig - put holders back")]
        public static void PutHoldersBack()
        {
            var sb = new StringBuilder();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsHolder(t) || Mathf.Abs(t.position.y + Depth) > 0.02f || t.GetComponentInChildren<Renderer>(true) != null) continue;
                t.position += Vector3.up * Depth;
                sb.AppendLine("back " + t.name);
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Temp/trench_dig_back.txt", sb.ToString());
        }

        // ---- dig ------------------------------------------------------------------------------------------------
        // The "trench" was two rows of sandbags standing on a flat yard 6.6 m wide. The floor between the rows is
        // lowered by Depth, and the rows and everything standing between them go down with it: from outside one
        // course of bags shows above the ground, inside an earth wall carries the bags. Eye height against the top
        // of the bags is unchanged, so the view over the parapet is as before. Both ends ramp up to the ground.
        public const float Depth = 0.7f;
        const float WestEnd = -18.5f, EastEnd = 20.5f, Ramp = 3f;   // full depth between the ends, ramps beyond
        const float FloorHalf = 3.0f, LipHalf = 3.35f;                // |z|: full depth inside, ground level at the lip
        const float WallZ = 3.6f;
        const string Marker = "TrenchDig_Applied";

        static float DepthAt(float x, float z)
        {
            float fx = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WestEnd - Ramp, WestEnd, x)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EastEnd + Ramp, EastEnd, x));
            float fz = Mathf.InverseLerp(LipHalf, FloorHalf, Mathf.Abs(z));
            return Depth * fx * fz;
        }

        static bool InStrip(Vector3 p, float margin) => Mathf.Abs(p.z) <= LipHalf + margin && p.x >= WestEnd - Ramp - margin && p.x <= EastEnd + Ramp + margin;

        // ---- fix: the terrain could not go below its own base ------------------------------------------------
        // The terrain sits at y -0.65 and a height sample cannot go under its base, so the dig stopped at -0.65
        // instead of ground - 0.7 (-0.73 on the flat, down to -1.07 in the old dips by the spawn and Ally_1). Every
        // object the dig lowered went down the full 0.7 and so stood 8-38 cm under the floor. This lowers the
        // terrain object by BaseDrop and raises every sample by the same amount (nothing outside the trench moves),
        // then rebuilds the trench from the heights before the dig (a copy of the committed TerrainData, placed at
        // PreDigCopy before running) minus DepthAt - exactly what the dig moved the objects by.
        const float BaseDrop = 1.0f;
        public const string PreDigCopy = "Assets/_Project/Art/MapPolish20261105/_PreDig_TerrainData.asset";
        const string FixMarker = "TrenchDig_FloorFixed";

        [MenuItem("FiveRivers/Setup/Trench Dig - Fix Clamped Floor")]
        public static string FixClampedFloor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            if (map.transform.Find(Marker) == null) throw new InvalidOperationException("참호 파기가 아직 적용되지 않았습니다.");
            if (map.transform.Find(FixMarker) != null) return "already fixed";
            var before = AssetDatabase.LoadAssetAtPath<TerrainData>(PreDigCopy);
            if (before == null) throw new InvalidOperationException("파기 전 지형 복사본이 없습니다: " + PreDigCopy);
            var terrain = Terrain.activeTerrain;
            var data = terrain.terrainData;
            if (before.heightmapResolution != data.heightmapResolution || before.size != data.size)
                throw new InvalidOperationException("복사본 해상도 · 크기가 다릅니다.");
            int res = data.heightmapResolution;
            float oldBase = terrain.transform.position.y, newBase = oldBase - BaseDrop, sizeY = data.size.y;
            var src = before.GetHeights(0, 0, res, res);
            var dst = new float[res, res];
            float lowest = float.MaxValue;
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    float wx = terrain.transform.position.x + i / (float)(res - 1) * data.size.x;
                    float wz = terrain.transform.position.z + j / (float)(res - 1) * data.size.z;
                    float world = oldBase + src[j, i] * sizeY - DepthAt(wx, wz);
                    lowest = Mathf.Min(lowest, world);
                    dst[j, i] = Mathf.Clamp01((world - newBase) / sizeY);
                }
            if (lowest < newBase) throw new InvalidOperationException("BaseDrop 이 모자랍니다.");
            Undo.RegisterCompleteObjectUndo(data, "Trench Dig Fix");
            Undo.RecordObject(terrain.transform, "Trench Dig Fix");
            // trees are stored in normalized terrain space: keep their world height
            var trees = data.treeInstances;
            for (int k = 0; k < trees.Length; k++) trees[k].position.y = Mathf.Clamp01((oldBase + trees[k].position.y * sizeY - newBase) / sizeY);
            terrain.transform.position += Vector3.down * BaseDrop;
            data.SetHeights(0, 0, dst);
            if (trees.Length > 0) data.SetTreeInstances(trees, false);
            EditorUtility.SetDirty(data);
            var marker = new GameObject(FixMarker);
            marker.transform.SetParent(map.transform, false);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(map.scene);
            AssetDatabase.SaveAssets();
            string report = string.Format(CultureInfo.InvariantCulture, "terrain base {0:0.00} -> {1:0.00}, lowest point {2:0.00}, trees {3}", oldBase, newBase, lowest, trees.Length);
            File.WriteAllText("Temp/trench_fix.txt", report);
            return report;
        }

        // ---- rebuild to FM 5-15 (Field Fortifications) -------------------------------------------------------
        // Sources (2026-11-16, user: "참호는 관련 자료 읽고 그대로 다시 만들어"): FM 5-15 Field Fortifications 1944
        // (the edition the Aug 1949 manual replaced): one-man foxhole fig. 17 (2 ft wide, 4-5 ft deep "depending upon
        // the height of the man", 1 ft elbow rest, spoil as an all-around parapet 3 ft thick and 1/2 ft high), special
        // trench fig. 39 (2 ft wide or less, 3-5 ft deep), standard trench fig. 40 (parapet 1 ft high, 3-5 ft wide,
        // 1 ft berm, fire positions staggered along the wall), sandbag revetment para 24 (interior slopes of parapets,
        // 1 on 4 batter), breastwork fig. 16 (earth face 1 on 1.5).
        // The squad here kneels to fire (A_ROK_Fire_Crouch) and the player looks over the parapet standing, so the depth
        // is the 3 ft of a kneeling firer's hole. The floor is wider than 2 ft so that two men and the ammunition pass.
        public const float RebuildDepth = 0.90f;        // 3 ft
        const float FloorFront = 0.10f, FloorRear = -1.70f;   // z of the floor edges (enemy is +z)
        const float WallRun = 0.22f;                    // 0.9 m wall at 1 on 4 batter
        const float Berm = 0.30f;                       // 1 ft elbow rest at ground level
        const float ParapetHigh = 0.30f, ParapetCrest = 0.50f, ParapetRise = 0.25f;    // 1 ft high
        const float ParadosHigh = 0.25f;
        const float NicheDepth = 0.90f;                 // ammunition niches cut into the rear wall
        static readonly (float x, float half)[] Niches = { (0f, 1.4f), (5f, 1.0f), (18.5f, 2.4f), (-18f, 1.2f) };
        const string RebuildMarker = "TrenchRebuild_FM515";

        // read by TrenchDressing (2026-11-16): the cross-section the dressing is laid on
        public const float TrenchWest = WestEnd, TrenchEast = EastEnd;
        public static float FrontFloorZ => FloorFront;
        public static float WallRunZ => WallRun;
        public static float BermWidth => Berm;
        public static float ParapetHeight => ParapetHigh;
        public static float ParadosHeight => ParadosHigh;
        public static float RearFloorZ(float x) => RearEdge(x);
        public static float EndFade(float x) => Fx(x);
        public static (float x, float half)[] AmmoNiches => Niches;

        static float Fx(float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(WestEnd - Ramp, WestEnd, x)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EastEnd + Ramp, EastEnd, x));

        static float RearEdge(float x)
        {
            float w = 0f;
            foreach (var n in Niches) w = Mathf.Max(w, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(n.half + 0.3f, n.half, Mathf.Abs(x - n.x))));
            return FloorRear - NicheDepth * w;
        }

        // height relative to the ground before any digging
        public static float RebuildRel(float x, float z)
        {
            float fx = Fx(x);
            if (fx <= 0f) return 0f;
            float zr = RearEdge(x);
            float d;
            if (z <= FloorFront && z >= zr) d = -RebuildDepth;
            else if (z > FloorFront && z < FloorFront + WallRun) d = -RebuildDepth * (1f - Mathf.SmoothStep(0f, 1f, (z - FloorFront) / WallRun));
            else if (z < zr && z > zr - WallRun) d = -RebuildDepth * Mathf.SmoothStep(0f, 1f, (z - (zr - WallRun)) / WallRun);
            else d = 0f;
            // front parapet (spoil): rise, flat crest, 1 on 1.5 face toward the enemy
            float p0 = FloorFront + WallRun + Berm, p1 = p0 + ParapetRise, p2 = p1 + ParapetCrest, p3 = p2 + ParapetHigh * 1.5f;
            float p = 0f;
            if (z > p0 && z < p3) p = ParapetHigh * (z < p1 ? Mathf.SmoothStep(0f, 1f, (z - p0) / ParapetRise) : z < p2 ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (z - p2) / (p3 - p2)));
            // parados behind: berm, rise, crest, face
            float q0 = FloorRear - WallRun - Berm, q1 = q0 - ParapetRise, q2 = q1 - ParapetCrest, q3 = q2 - ParadosHigh * 1.5f;
            if (zr > FloorRear - 0.01f && z < q0 && z > q3) p = ParadosHigh * (z > q1 ? Mathf.SmoothStep(0f, 1f, (q0 - z) / ParapetRise) : z > q2 ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (q2 - z) / (q2 - q3)));
            return fx * (d + p);
        }

        [MenuItem("FiveRivers/Setup/Trench Rebuild (FM 5-15)")]
        public static string Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            if (map.transform.Find(FixMarker) == null) throw new InvalidOperationException("먼저 Trench Dig - Fix Clamped Floor 를 적용하세요.");
            if (map.transform.Find(RebuildMarker) != null) return "already rebuilt";
            var before = AssetDatabase.LoadAssetAtPath<TerrainData>(PreDigCopy);
            if (before == null) throw new InvalidOperationException("파기 전 지형 복사본이 없습니다: " + PreDigCopy);
            var terrain = Terrain.activeTerrain;
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            float preBase = origin.y + BaseDrop;   // the copy's samples are relative to the terrain's base before the fix
            int res = data.heightmapResolution;
            var src = before.GetHeights(0, 0, res, res);
            float PreGround(float x, float z)
            {
                float u = Mathf.Clamp01((x - origin.x) / data.size.x) * (res - 1), v = Mathf.Clamp01((z - origin.z) / data.size.z) * (res - 1);
                int i = Mathf.Min((int)u, res - 2), j = Mathf.Min((int)v, res - 2);
                float fu = u - i, fv = v - j;
                float h = Mathf.Lerp(Mathf.Lerp(src[j, i], src[j, i + 1], fu), Mathf.Lerp(src[j + 1, i], src[j + 1, i + 1], fu), fv);
                return preBase + h * data.size.y;
            }
            var report = new StringBuilder();

            // 1. terrain from the ground before the dig
            Undo.RegisterCompleteObjectUndo(data, "Trench Rebuild");
            var dst = data.GetHeights(0, 0, res, res);
            int changed = 0;
            for (int j = 0; j < res; j++)
                for (int i = 0; i < res; i++)
                {
                    float wx = origin.x + i / (float)(res - 1) * data.size.x, wz = origin.z + j / (float)(res - 1) * data.size.z;
                    if (wz < -6f || wz > 6f || wx < WestEnd - Ramp - 1f || wx > EastEnd + Ramp + 1f) continue;
                    float world = preBase + src[j, i] * data.size.y + RebuildRel(wx, wz);
                    dst[j, i] = Mathf.Clamp01((world - origin.y) / data.size.y);
                    changed++;
                }
            data.SetHeights(0, 0, dst);
            EditorUtility.SetDirty(data);
            report.AppendLine($"terrain samples rebuilt {changed}");
            float NewFloor(Vector3 p) => PreGround(p.x, p.z) + RebuildRel(p.x, p.z);
            float OldFloor(Vector3 p) => PreGround(p.x, p.z) - DepthAt(p.x, p.z);

            void Shift(Transform t, Vector3 d, string why)
            {
                if (t == null) return;
                Undo.RecordObject(t, "Trench Rebuild");
                t.position += d;
                if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1} by ({2:0.00},{3:0.00},{4:0.00}) to {5}", why, t.name, d.x, d.y, d.z, t.position));
            }
            Bounds? BoundsOf(Transform t)
            {
                var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (rs.Length == 0) return null;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
            }

            // 2. sandbag rows: the front row revets the inside of the parapet at the lip, one course showing (1 ft);
            //    the rear row revets the parados behind the rear wall (or behind a niche)
            foreach (var t in map.GetComponentsInChildren<Transform>(true).Where(t => t.parent != null && t.parent.name.StartsWith("Trench_Sandbag_Walls")).ToArray())
            {
                var b = BoundsOf(t); if (b == null) continue;
                bool front = t.position.z > 0f;
                float halfDepth = b.Value.extents.z;
                float z = front ? FloorFront + WallRun + Berm + halfDepth - 0.02f : RearEdge(t.position.x) - WallRun - Berm - halfDepth + 0.02f; // behind the 1 ft elbow rest / rear berm
                float g = PreGround(t.position.x, z);
                float top = g + (front ? 0.33f : 0.22f) * Fx(t.position.x) + 0.02f;
                Shift(t, new Vector3(0f, top - b.Value.max.y, z - t.position.z), front ? "front row" : "rear row");
            }

            // 3. people and gameplay points keep their height above the floor
            var points = map.transform.Find("Points");
            if (points != null) foreach (Transform t in points) { if (Fx(t.position.x) > 0f && Mathf.Abs(t.position.z) < 3.4f) Shift(t, Vector3.up * (NewFloor(t.position) - OldFloor(t.position)), "point"); }
            var people = map.transform.Find("Generated/People");
            if (people != null) foreach (Transform t in people) { if (Fx(t.position.x) > 0f && Mathf.Abs(t.position.z) < 3.4f) Shift(t, Vector3.up * (NewFloor(t.position) - OldFloor(t.position)), "person"); }
            var player = GameObject.Find("/[Player]");
            if (player != null) Shift(player.transform, Vector3.up * (NewFloor(player.transform.position) - OldFloor(player.transform.position)), "player");

            // 4. props stand on the new floor
            foreach (var path in new[] { "Generated/Props/GameplayAmmoCrate_0", "Generated/Props/GameplayAmmoCrate_1", "Dabudong_Imported/M01_Greybox/Zones/AMMO_01",
                "Dabudong_Imported/M01_Greybox/Zones/PROP_MANSU", "Dabudong_Imported/M01_Environment/Trench_Visuals/Ammo_Crates", "Dabudong_Imported/M01_Environment/Trench_Visuals/Blanket_Body" })
            {
                var t = map.transform.Find(path); if (t == null) { report.AppendLine("missing " + path); continue; }
                var b = BoundsOf(t); if (b == null) continue;
                Shift(t, Vector3.up * (NewFloor(b.Value.center) - b.Value.min.y), "prop");
            }
            var lantern = map.transform.Find("Frontline_Art_20261102/Supply_bay_lantern");
            if (lantern != null)
            {
                var crate = map.transform.Find("Generated/Props/GameplayAmmoCrate_0");
                var spot = crate != null ? crate.Find("M1917_Visual/LanternSpot") : null;   // AmmoCrateM1917Setup: lantern on the floor beside the boxes
                var cb = crate != null ? BoundsOf(crate) : null; var lb = BoundsOf(lantern);
                if (spot != null && lb != null) Shift(lantern, new Vector3(spot.position.x - lb.Value.center.x, crate.position.y - lb.Value.min.y, spot.position.z - lb.Value.center.z), "lantern beside crate");
                else if (cb != null && lb != null) Shift(lantern, Vector3.up * (cb.Value.max.y - lb.Value.min.y), "lantern on crate");
            }
            var cases = map.transform.Find("Frontline_Art_20261102/Ground_Debris/Spent_cartridge_cases");
            if (cases != null) foreach (Transform c in cases)
                {
                    var b = BoundsOf(c); if (b == null) continue;
                    var to = b.Value.center; to.z = Mathf.Clamp(to.z - 1.75f, FloorRear + 0.15f, FloorFront - 0.1f);
                    Shift(c, new Vector3(0f, NewFloor(to) - b.Value.min.y, to.z - b.Value.center.z), "cartridges");
                }

            // 5. the wooden revetment boards go to the new wall faces, top at the ground
            var boards = map.transform.Find("Dabudong_Imported/M01_Environment/Trench_Visuals/Wood_Revetment");
            if (boards != null) foreach (Transform c in boards.Cast<Transform>().ToArray())
                {
                    var b = BoundsOf(c); if (b == null) continue;
                    if (b.Value.min.z < 0f && b.Value.max.z > 0f) { report.AppendLine("board spans both walls, left as is: " + c.name + " " + b.Value); continue; }
                    float cx = b.Value.center.x;
                    bool front = b.Value.center.z > 0f;
                    float z = front ? FloorFront + 0.06f + b.Value.extents.z : RearEdge(cx) - 0.06f - b.Value.extents.z;
                    float top = PreGround(cx, z) + 0.02f;
                    Shift(c, new Vector3(0f, top - b.Value.max.y, z - b.Value.center.z), front ? "board front" : "board rear");
                }

            // 6. report the greybox floor zone collider, which was laid at the old floor
            var zone = map.transform.Find("Dabudong_Imported/M01_Greybox/Zones/A_Floor");
            if (zone != null) foreach (var c in zone.GetComponentsInChildren<Collider>(true)) report.AppendLine($"A_Floor collider trigger {c.isTrigger} enabled {c.enabled} layer {LayerMask.LayerToName(c.gameObject.layer)} bounds {c.bounds}");

            var marker = new GameObject(RebuildMarker);
            marker.transform.SetParent(map.transform, false);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(map.scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/trench_rebuild.txt", report.ToString());
            return "rebuilt";
        }

        // Follow-ups to the rebuild; every step sets an absolute place, so running it again changes nothing.
        [MenuItem("FiveRivers/Setup/Trench Rebuild - Fixups")]
        public static string RebuildFixups()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            if (map.transform.Find(RebuildMarker) == null) throw new InvalidOperationException("먼저 Trench Rebuild (FM 5-15) 를 적용하세요.");
            var terrain = Terrain.activeTerrain;
            float Floor(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
            var report = new StringBuilder();
            Bounds? BoundsOf(Transform t)
            {
                var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                if (rs.Length == 0) return null;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
            }
            void Move(Transform t, Vector3 d, string why)
            {
                if (d.sqrMagnitude < 1e-6f) return;
                Undo.RecordObject(t, "Trench Fixups");
                t.position += d;
                if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1} by ({2:0.00},{3:0.00},{4:0.00})", why, t.name, d.x, d.y, d.z));
            }

            // 1. the NavMesh is baked from the colliders under [Map] when the scene starts: the people standing in the
            //    trench were baked in as obstacles and, on a floor 1.8 m wide, cut the trench into pieces
            var people = map.transform.Find("Generated/People");
            if (people != null)
            {
                var mod = people.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
                if (mod == null) mod = Undo.AddComponent<Unity.AI.Navigation.NavMeshModifier>(people.gameObject);
                mod.ignoreFromBuild = true; mod.applyToChildren = true;
                EditorUtility.SetDirty(mod);
                report.AppendLine("People: NavMeshModifier ignoreFromBuild");
            }

            // 2. rear sandbag sections that reach over an ammunition niche go behind the niche
            foreach (var t in map.GetComponentsInChildren<Transform>(true).Where(t => t.parent != null && t.parent.name.StartsWith("Trench_Sandbag_Walls") && t.position.z < 0f).ToArray())
            {
                var b = BoundsOf(t); if (b == null) continue;
                float zr = Mathf.Min(RearEdge(b.Value.min.x), RearEdge(b.Value.center.x), RearEdge(b.Value.max.x));
                float z = zr - WallRun - Berm - b.Value.extents.z + 0.02f;
                Move(t, new Vector3(0f, 0f, z - b.Value.center.z), "rear row");
            }

            // 3. loose things that ended up in a wall or a parapet go onto the floor
            var rope = map.transform.Find("Megascans20261107/PF_MS_RopeHemp");
            if (rope != null) { var b = BoundsOf(rope); if (b != null) { var to = new Vector3(b.Value.center.x, 0f, RearEdge(b.Value.center.x) + 0.35f); Move(rope, new Vector3(0f, Floor(to) - b.Value.min.y, to.z - b.Value.center.z), "rope"); } }
            var cases = map.transform.Find("Frontline_Art_20261102/Ground_Debris/Spent_cartridge_cases");
            if (cases != null) { var b = BoundsOf(cases); if (b != null) { var to = new Vector3(b.Value.center.x, 0f, (FloorFront + FloorRear) * 0.5f + 0.1f); Move(cases, new Vector3(0f, Floor(to) - b.Value.min.y - 0.01f, to.z - b.Value.center.z), "cartridges"); } }

            // 4. the wooden revetment is one mesh built along the old 6 m-wide walls: it now stands in the open
            var boards = map.transform.Find("Dabudong_Imported/M01_Environment/Trench_Visuals/Wood_Revetment");
            if (boards != null && boards.gameObject.activeSelf) { Undo.RecordObject(boards.gameObject, "Trench Fixups"); boards.gameObject.SetActive(false); report.AppendLine("Wood_Revetment off (one mesh along the old walls)"); }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(map.scene);
            File.WriteAllText("Temp/trench_fixups.txt", report.ToString());
            return "fixups done";
        }

        [MenuItem("FiveRivers/Setup/Trench Dig (0.7 m)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 모드를 종료한 뒤 실행하세요.");
            var map = GameObject.Find("/[Map]");
            if (map == null) throw new InvalidOperationException("[Map] 이 없습니다.");
            if (map.transform.Find(Marker) != null) throw new InvalidOperationException("이미 적용됐습니다 (" + Marker + ").");
            var report = new StringBuilder();
            var terrain = Terrain.activeTerrain;
            var data = terrain.terrainData;
            var origin = terrain.transform.position;

            // 1. terrain
            int res = data.heightmapResolution;
            int x0 = Mathf.FloorToInt((WestEnd - Ramp - 1f - origin.x) / data.size.x * (res - 1)), x1 = Mathf.CeilToInt((EastEnd + Ramp + 1f - origin.x) / data.size.x * (res - 1));
            int z0 = Mathf.FloorToInt((-LipHalf - 1f - origin.z) / data.size.z * (res - 1)), z1 = Mathf.CeilToInt((LipHalf + 1f - origin.z) / data.size.z * (res - 1));
            Undo.RegisterCompleteObjectUndo(data, "Trench Dig");
            var heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            int changed = 0;
            for (int j = 0; j <= z1 - z0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    float wx = origin.x + (x0 + i) / (float)(res - 1) * data.size.x, wz = origin.z + (z0 + j) / (float)(res - 1) * data.size.z;
                    float d = DepthAt(wx, wz);
                    if (d <= 0f) continue;
                    heights[j, i] -= d / data.size.y;
                    changed++;
                }
            data.SetHeights(x0, z0, heights);
            EditorUtility.SetDirty(data);
            report.AppendLine($"terrain samples lowered {changed}");

            // 2. the sandbag rows go down with the floor at their place along the trench
            int walls = 0;
            foreach (var t in map.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null || !t.parent.name.StartsWith("Trench_Sandbag_Walls")) continue;
                float d = DepthAt(t.position.x, 0f);
                if (d <= 0f) continue;
                Undo.RecordObject(t, "Trench Dig");
                t.position += Vector3.down * d;
                walls++;
            }
            report.AppendLine($"wall sections lowered {walls}");

            // 3. everything standing on the floor
            int moved = 0;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) Visit(root.transform);
            void Visit(Transform t)
            {
                if (t.GetComponent<Terrain>() != null || t.name.StartsWith("Trench_Sandbag_Walls")) return;
                if (IsHolder(t) && t.GetComponentInChildren<Renderer>(true) == null) { foreach (Transform child in t) Visit(child); return; }
                var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                bool whole;
                if (renderers.Length > 0)
                {
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    whole = InStrip(t.position, 0f) && InStrip(b.min, 0.6f) && InStrip(b.max, 0.6f);
                }
                else whole = t.childCount == 0 && InStrip(t.position, 0f);
                if (whole)
                {
                    float d = DepthAt(t.position.x, t.position.z);
                    float above = t.position.y - (-0.03f);
                    if (d > 0f && above > -1.2f && above < 2.6f)
                    {
                        Undo.RecordObject(t, "Trench Dig");
                        t.position += Vector3.down * d;
                        if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                        moved++;
                        string path = t.name; for (var p = t.parent; p != null && path.Length < 90; p = p.parent) path = p.name + "/" + path;
                        report.AppendLine(string.Format(CultureInfo.InvariantCulture, "moved {0} by {1:0.00} to y {2:0.00}", path, d, t.position.y));
                    }
                    return;
                }
                foreach (Transform child in t) Visit(child);
            }
            report.AppendLine($"objects lowered {moved}");

            var marker = new GameObject(Marker);
            marker.transform.SetParent(map.transform, false);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(map.scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/trench_dig.txt", report.ToString());
        }
    }
}
