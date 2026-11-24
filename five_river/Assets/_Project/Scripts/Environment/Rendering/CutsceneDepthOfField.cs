using FiveRivers.Narrative;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Environment
{
    // Depth of field only in the staged shots (2026-11-24): while a CutsceneRig plays, the far background softens a
    // little behind what the shot looks at (Gaussian, far side only, so nothing near the lens - the hands, the rifle,
    // the HUD is not part of the 3D image at all - can blur). Fades in and out with the shot; off in play.
    // The focus is the distance straight ahead of the camera that draws the shot (the topmost enabled camera).
    [RequireComponent(typeof(Volume))]
    public sealed class CutsceneDepthOfField : MonoBehaviour
    {
        [SerializeField] float fade = .5f;
        [SerializeField] float minStart = 8f;      // metres: never closer than this
        [SerializeField] float startFactor = 1.4f; // blur starts this far behind the focus
        [SerializeField] float endFactor = 4f;
        [SerializeField] float maxRadius = .9f;

        Volume volume;
        DepthOfField dof;
        float focus = 20f;

        void Awake()
        {
            volume = GetComponent<Volume>();
            volume.weight = 0f;
            var profile = volume.profile; // a runtime copy: the asset is not changed while playing
            if (!profile.TryGet(out dof)) dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianMaxRadius.Override(maxRadius);
            dof.highQualitySampling.Override(true);
        }

        void LateUpdate()
        {
            bool on = CutsceneRig.Running > 0;
            volume.weight = Mathf.MoveTowards(volume.weight, on ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(.01f, fade));
            if (volume.weight <= 0f) return;
            Camera top = null;
            foreach (var c in Camera.allCameras) if (c.enabled && (top == null || c.depth > top.depth)) top = c;
            if (top != null)
            {
                float d = Physics.Raycast(top.transform.position, top.transform.forward, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 60f;
                focus = Mathf.Lerp(focus, d, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f));
            }
            float start = Mathf.Max(minStart, focus * startFactor);
            dof.gaussianStart.Override(start);
            dof.gaussianEnd.Override(Mathf.Max(start + 10f, focus * endFactor));
        }
    }
}
