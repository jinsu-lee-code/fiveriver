using FiveRivers.Core;
using FiveRivers.Data;
using UnityEngine;

namespace FiveRivers.Combat
{
    // Shell craters (2026-11-13, user: "전차 포 쐈을때 참호에 구멍이나게끔"). Where a tank round lands the ground is
    // blown out into a shallow bowl with a thrown-up lip, and the earth round it is darkened. The terrain asset is
    // never touched: on the first crater of a run the terrain gets a runtime copy of its data, and on a new run or a
    // checkpoint retry the original data is put back (so no crater outlives its run). Added by TankController.
    public sealed class ShellCraters : MonoBehaviour
    {
        FlowDirector director;
        TankConfig config;
        Terrain terrain;
        TerrainCollider terrainCollider;
        TerrainData original, copy;
        int scorchLayer = -1;

        public int Count { get; private set; }
        public Vector3 LastPoint { get; private set; }
        public int Breaches => breached.Count;

        // The trench's sandbag wall is laid in 2 m sections (Trench_Sandbag_Walls_*/<n>/Model). A round landing on or
        // beside one blows it down: the section goes and a slumped heap of its bags is left where it stood.
        readonly System.Collections.Generic.List<GameObject> breached = new System.Collections.Generic.List<GameObject>();
        readonly System.Collections.Generic.List<GameObject> rubble = new System.Collections.Generic.List<GameObject>();
        Renderer[] wallSections;

        public void Bind(FlowDirector flowDirector, TankConfig tankConfig, Terrain ground)
        {
            director = flowDirector;
            config = tankConfig;
            terrain = ground != null ? ground : Terrain.activeTerrain;
            if (terrain != null) terrainCollider = terrain.GetComponent<TerrainCollider>();
        }

        void Start()
        {
            if (director == null) return;
            director.Flow.SectionChanged += OnSectionChanged;
            director.Flow.RetryRequested += OnRetry;
        }

        void OnDestroy()
        {
            Restore();
            if (director == null || director.Flow == null) return;
            director.Flow.SectionChanged -= OnSectionChanged;
            director.Flow.RetryRequested -= OnRetry;
        }

        void OnSectionChanged(FlowTransition t)
        {
            if (t.To == FlowSection.Title || t.To == FlowSection.Tutorial) Restore();
        }

        void OnRetry(FlowCheckpoint checkpoint) => Restore();

        // Back to the terrain as authored.
        public void Restore()
        {
            foreach (var g in breached) if (g != null) g.SetActive(true);
            breached.Clear();
            foreach (var g in rubble) if (g != null) Destroy(g);
            rubble.Clear();
            if (copy == null || terrain == null) return;
            terrain.terrainData = original;
            if (terrainCollider != null) terrainCollider.terrainData = original;
            Destroy(copy);
            copy = null;
            Count = 0;
        }

        public void Make(Vector3 point)
        {
            if (terrain == null || config == null || config.craterRadius <= 0f) return;
            var data = Writable();
            var size = data.size;
            var origin = terrain.transform.position;
            float radius = config.craterRadius, rimOut = radius * 1.35f;

            // heights: a bowl of the given depth, a lip just outside it
            int res = data.heightmapResolution;
            float cell = size.x / (res - 1);
            int cx = Mathf.RoundToInt((point.x - origin.x) / size.x * (res - 1));
            int cz = Mathf.RoundToInt((point.z - origin.z) / size.z * (res - 1));
            int reach = Mathf.CeilToInt(rimOut / cell) + 1;
            int x0 = Mathf.Clamp(cx - reach, 0, res - 1), z0 = Mathf.Clamp(cz - reach, 0, res - 1);
            int x1 = Mathf.Clamp(cx + reach, 0, res - 1), z1 = Mathf.Clamp(cz + reach, 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            if (w < 2 || h < 2) return;
            var heights = data.GetHeights(x0, z0, w, h);
            float depth = config.craterDepth / size.y, rim = config.craterRim / size.y;
            // a torn, uneven edge rather than a drawn circle: the radius wanders with the direction
            float seed = Random.Range(0f, 100f);
            float Wobble(float dx, float dz)
            {
                float a = Mathf.Atan2(dz, dx);
                return 1f + 0.16f * (Mathf.PerlinNoise(seed + Mathf.Cos(a) * 1.3f, seed + Mathf.Sin(a) * 1.3f) - 0.5f) * 2f
                          + 0.06f * Mathf.Sin(a * 5f + seed);
            }
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float wx = origin.x + (x0 + x) * size.x / (res - 1), wz = origin.z + (z0 + z) * size.z / (res - 1);
                    float d = new Vector2(wx - point.x, wz - point.z).magnitude / (radius * Wobble(wx - point.x, wz - point.z));
                    float bowl = d < 1f ? -depth * (1f - d * d) : 0f;
                    float lip = d > 0.75f && d < 1.35f ? rim * Mathf.Sin(Mathf.InverseLerp(0.75f, 1.35f, d) * Mathf.PI) : 0f;
                    heights[z, x] = Mathf.Clamp01(heights[z, x] + bowl + lip);
                }
            data.SetHeightsDelayLOD(x0, z0, heights);
            data.SyncHeightmap();

            // paint: scorched earth inside, fading out over the lip
            if (scorchLayer >= 0)
            {
                int ares = data.alphamapResolution;
                int ax = Mathf.RoundToInt((point.x - origin.x) / size.x * ares), az = Mathf.RoundToInt((point.z - origin.z) / size.z * ares);
                int areach = Mathf.CeilToInt(rimOut * 1.2f / (size.x / ares)) + 1;
                int ax0 = Mathf.Clamp(ax - areach, 0, ares - 1), az0 = Mathf.Clamp(az - areach, 0, ares - 1);
                int aw = Mathf.Clamp(ax + areach, 0, ares - 1) - ax0 + 1, ah = Mathf.Clamp(az + areach, 0, ares - 1) - az0 + 1;
                var maps = data.GetAlphamaps(ax0, az0, aw, ah);
                int layers = maps.GetLength(2);
                for (int z = 0; z < ah; z++)
                    for (int x = 0; x < aw; x++)
                    {
                        float wx = origin.x + (ax0 + x + 0.5f) * size.x / ares, wz = origin.z + (az0 + z + 0.5f) * size.z / ares;
                        float d = new Vector2(wx - point.x, wz - point.z).magnitude / (rimOut * 1.2f * Wobble(wx - point.x, wz - point.z));
                        float k = 1f - Mathf.SmoothStep(0.55f, 1f, d);
                        if (k <= 0f) continue;
                        for (int l = 0; l < layers; l++) maps[z, x, l] *= 1f - k;
                        maps[z, x, scorchLayer] += k;
                    }
                data.SetAlphamaps(ax0, az0, maps);
            }
            Count++;
            LastPoint = point;
            Breach(point);
        }

        void Breach(Vector3 point)
        {
            if (config.craterBreach <= 0f) return;
            if (wallSections == null)
            {
                var list = new System.Collections.Generic.List<Renderer>();
                foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var t = r.transform;
                    for (int k = 0; k < 4 && t != null; k++, t = t.parent)
                        if (t.name.StartsWith("Trench_Sandbag_Walls")) { list.Add(r); break; }
                }
                wallSections = list.ToArray();
            }
            foreach (var r in wallSections)
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (Vector3.Distance(b.ClosestPoint(point), point) > config.craterBreach) continue;
                // the section's own object (Model's parent, e.g. Rear_22) goes; a squashed, tipped copy of its bags stays
                var section = r.transform.parent != null ? r.transform.parent.gameObject : r.gameObject;
                var heap = Instantiate(r.gameObject, r.transform.position, r.transform.rotation);
                heap.name = r.name + "_Breached";
                foreach (var c in heap.GetComponentsInChildren<Collider>()) Destroy(c);
                var s = r.transform.lossyScale;
                heap.transform.localScale = new Vector3(s.x * Random.Range(0.85f, 1.05f), s.y * 0.38f, s.z * 1.35f);
                var away = b.center - point; away.y = 0f;
                heap.transform.rotation = Quaternion.AngleAxis(Random.Range(-12f, 12f), Vector3.up) * Quaternion.AngleAxis(Random.Range(6f, 14f), Vector3.Cross(Vector3.up, away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward)) * r.transform.rotation;
                var ground = terrain.SampleHeight(b.center) + terrain.transform.position.y;
                // slumped onto the ground where the section stood, pushed a little away from the blast
                heap.transform.position += new Vector3(away.normalized.x, 0f, away.normalized.z) * 0.25f;
                heap.transform.position += Vector3.up * (ground - heap.GetComponent<Renderer>().bounds.min.y - 0.05f);
                rubble.Add(heap);
                section.SetActive(false);
                breached.Add(section);
            }
        }

        TerrainData Writable()
        {
            if (copy != null) return copy;
            original = terrain.terrainData;
            copy = Instantiate(original);
            copy.name = original.name + " (run)";
            terrain.terrainData = copy;
            if (terrainCollider != null) terrainCollider.terrainData = copy;
            scorchLayer = -1;
            var layers = copy.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] != null && layers[i].name.IndexOf("Burn", System.StringComparison.OrdinalIgnoreCase) >= 0) { scorchLayer = i; break; }
            return copy;
        }
    }
}
