using UnityEngine;

namespace FiveRivers.Environment
{
    // The trench sinks below the field (2026-11-24, graphics pass 2). A height map of the trench rim (the highest
    // ground, parapet or sandbag within a metre and a half, baked by the menu FiveRivers/Setup/Trench Shade 20261124)
    // tells, for every point of the image, how far below the rim it lies. FrontlineVolumetricFog's composite darkens
    // what is down in the trench in proportion - the floor and the bottom of the walls the most, the open field and
    // the parapet top not at all - under the moon, a flare or the dawn alike, like the sky the trench cannot see.
    // Nothing within a metre and a half of the camera is touched (the hands, the rifle). Tried first: an Adaptive Probe
    // Volume with sky occlusion; it lost the ambient light on the open ground around the trench at dawn (see TASKS.md).
    [ExecuteAlways]
    public sealed class TrenchShade : MonoBehaviour
    {
        [SerializeField] Texture2D rimMap;
        [SerializeField] Vector4 bounds;          // min x, min z, size x, size z (world metres)
        [Range(0f, 0.8f)] [SerializeField] float strength = .35f;
        [SerializeField] float shadeStart = .45f; // metres below the rim where the shade begins
        [SerializeField] float shadeFull = 1.7f;  // and where it is full
        [SerializeField] float nearClear = 1.5f;  // nothing closer to the camera than this is shaded

        static readonly int MapId = Shader.PropertyToID("_TrenchRimMap");
        static readonly int BoundsId = Shader.PropertyToID("_TrenchRimBounds");
        static readonly int ParamsId = Shader.PropertyToID("_TrenchShadeParams");

        public void Set(Texture2D map, Vector4 worldBounds) { rimMap = map; bounds = worldBounds; Push(); }

        void OnEnable() => Push();
        void Update() => Push();
        void OnDisable() => Shader.SetGlobalVector(ParamsId, Vector4.zero);

        void Push()
        {
            if (rimMap == null) { Shader.SetGlobalVector(ParamsId, Vector4.zero); return; }
            Shader.SetGlobalTexture(MapId, rimMap);
            Shader.SetGlobalVector(BoundsId, new Vector4(bounds.x, bounds.y, 1f / Mathf.Max(.01f, bounds.z), 1f / Mathf.Max(.01f, bounds.w)));
            Shader.SetGlobalVector(ParamsId, new Vector4(strength, shadeStart, shadeFull, nearClear));
        }
    }
}
