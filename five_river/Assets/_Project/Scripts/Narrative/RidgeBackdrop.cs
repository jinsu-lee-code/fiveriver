using UnityEngine;

namespace FiveRivers.Narrative
{
    // 먼 산 띠(2026-11-18)에 밤 → 새벽 진행도를 알린다. 하늘 재질의 _DawnBlend(DawnDirector가 올린다)를
    // 그대로 전역 값으로 옮기므로 하늘과 산이 같은 박자로 밝아진다. 표시 전용.
    [ExecuteAlways]
    public sealed class RidgeBackdrop : MonoBehaviour
    {
        static readonly int DawnBlendId = Shader.PropertyToID("_DawnBlend");
        static readonly int RidgeDawnId = Shader.PropertyToID("_RidgeDawn");
        float last = -1f;

        void OnEnable() { last = -1f; Push(); }
        void OnDisable() { Shader.SetGlobalFloat(RidgeDawnId, 0f); last = -1f; }
        void LateUpdate() => Push();

        void Push()
        {
            var sky = RenderSettings.skybox;
            float dawn = sky != null && sky.HasProperty(DawnBlendId) ? sky.GetFloat(DawnBlendId) : 0f;
            if (dawn == last) return;
            last = dawn;
            Shader.SetGlobalFloat(RidgeDawnId, dawn);
        }
    }
}
