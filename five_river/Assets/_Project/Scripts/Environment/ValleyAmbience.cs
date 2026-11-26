using UnityEngine;

namespace FiveRivers.Environment
{
    // Small life in the valley (2026-11-15): the poplars along the road lean and recover in the wind, each on its
    // own beat, and the still water in the stream bed carries a slow ripple. Sits on "ValleyRoad_Aug1950".
    // The stream was a flat mirror and the trees stood like posts; both read as placed props.
    public sealed class ValleyAmbience : MonoBehaviour
    {
        public static float Sway = 0.9f;                 // degrees a poplar leans at the top of a gust
        public static float GustEvery = 7f;              // seconds between gusts
        public static Vector2 Ripple = new Vector2(0.012f, 0.035f); // how fast the water's ripple drifts (uv per second)

        Transform[] trees;
        Quaternion[] rest;
        float[] phase;
        Material water;

        void Start()
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Poplar_")) list.Add(child);
                else if (child.name == "StreamWater")
                {
                    var r = child.GetComponent<Renderer>();
                    // 2026-11-25: the stream's water is the URP 3D sample's flowing surface (it moves by itself, no main
                    // texture to scroll; setting the offset of a material without one logs an error every frame)
                    if (r != null && r.sharedMaterial != null && HasMainTexture(r.sharedMaterial))
                        water = r.material; // an instance: the asset is not written to while playing
                }
            }
            trees = list.ToArray();
            rest = new Quaternion[trees.Length];
            phase = new float[trees.Length];
            for (int i = 0; i < trees.Length; i++)
            {
                rest[i] = trees[i].localRotation;
                // the gust runs down the valley: trees further along it lean a little later
                phase[i] = trees[i].position.z * 0.045f + (i * 0.618f % 1f) * 1.3f;
            }
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < trees.Length; i++)
            {
                if (trees[i] == null) continue;
                float gust = Mathf.Pow(Mathf.Max(0f, Mathf.Sin((t / GustEvery) * Mathf.PI * 2f - phase[i])), 2f);
                float lean = Sway * (0.25f * Mathf.Sin(t * 1.3f + phase[i] * 5f) + gust * (0.75f + 0.25f * Mathf.Sin(t * 2.9f + phase[i] * 3f)));
                trees[i].localRotation = Quaternion.Euler(lean * 0.35f, 0f, -lean) * rest[i]; // leaning about the foot of the trunk
            }
            if (water != null) water.mainTextureOffset = Ripple * t;
        }

        static bool HasMainTexture(Material m)
        {
            var shader = m.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
                if ((shader.GetPropertyFlags(i) & UnityEngine.Rendering.ShaderPropertyFlags.MainTexture) != 0 || shader.GetPropertyName(i) == "_MainTex") return true;
            return false;
        }

        void OnDestroy()
        {
            if (water != null) Destroy(water);
        }
    }
}
