using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FiveRivers.Combat
{
    // M1 rear peep sight as the eye sees it (FM 23-5 sight picture, shooters' descriptions): the aperture is far
    // too close to focus on, so the plate reads as a soft, see-through dark ring (the "ghost ring" - the pupil is
    // wider than the 2.5 mm hole) around a small sharp hole with the front post and its ears in the middle, and
    // past the plate the world is almost normal. (A camera pressed to the sight makes the plate a black disc only
    // because its lens aperture is smaller than an eye's pupil.) The real aperture mesh is hidden
    // while aiming (at 3-4 cm it would be a sharp grey plate with a pin hole) and this ring is drawn in its place,
    // centred on the projected aperture so it follows sway and recoil.
    // Drawn on a screen-space overlay canvas so it lies over the out-of-focus receiver and sight ears too; the HUD
    // canvas stays on top. (Camera-source captures do not include overlay canvases; use ScreenCapture to check.)
    public sealed class PeepSightOverlay : MonoBehaviour
    {
        // radii as fractions of screen height
        public static float ClearRadius = 0.13f;  // sharp hole
        public static float SoftRadius = 0.21f;   // hole edge blurs into the plate
        public static float PlateRadius = 0.40f;  // plate (blurred) ...
        public static float PlateEdge = 0.56f;    // ... fading out to the surrounding view
        public static float PlateAlpha = 0.72f, OutsideAlpha = 0.18f;
        public static float RingAlpha = 1f;
        public static Color RingColor = new Color(0.035f, 0.035f, 0.04f, 1f);

        // While aiming the eye is focused out past the front post: everything within a hand's breadth of it (rear
        // sight knobs, receiver heel) goes soft, the front post at 0.7 m and the target stay sharp. Bokeh depth of
        // field (24 mm, f/4, focus 3 m: blur ~4% of the frame at 10 cm, slight at the post, none at 100 m), weighted by the aim blend.
        public static float FocusDistance = 3f, FocalLength = 24f, Aperture = 4f;
        Volume focus;
        VolumeProfile focusProfile;

        RawImage image;
        RectTransform rect, canvasRect;
        Texture2D texture;
        float shownClear = -1f, shownSoft = -1f;

        void Awake()
        {
            var go = new GameObject("PeepSightOverlay", typeof(Canvas));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; // after the world and the rifle
            canvas.sortingOrder = -100;                          // under the HUD
            canvasRect = (RectTransform)go.transform;
            var child = new GameObject("Ring", typeof(RectTransform), typeof(RawImage));
            child.transform.SetParent(go.transform, false);
            image = child.GetComponent<RawImage>();
            image.raycastTarget = false;
            rect = (RectTransform)child.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            image.enabled = false;

            focusProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var dof = focusProfile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(FocusDistance);
            dof.focalLength.Override(FocalLength);
            dof.aperture.Override(Aperture);
            var vgo = new GameObject("PeepSightFocus");
            vgo.transform.SetParent(transform, false);
            vgo.layer = gameObject.layer;
            focus = vgo.AddComponent<Volume>();
            focus.isGlobal = true;
            focus.priority = 50f;
            focus.sharedProfile = focusProfile;
            focus.weight = 0f;
        }

        void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            if (focusProfile != null) Destroy(focusProfile);
        }

        // strength 0..1 (aim blend), centre in viewport coordinates (0..1).
        public void Show(float strength, Vector2 centre)
        {
            if (focus != null) focus.weight = strength;
            if (strength <= 0.001f)
            {
                image.enabled = false;
                return;
            }
            if (shownClear != ClearRadius || shownSoft != SoftRadius) Build();
            var size = canvasRect.rect.size;
            rect.sizeDelta = Vector2.one * size.y * 4f; // texture spans 4 screen heights
            rect.anchoredPosition = Vector2.Scale(centre, size);
            var c = RingColor;
            c.a = RingAlpha * strength;
            image.color = c;
            image.enabled = true;
        }

        void Build()
        {
            shownClear = ClearRadius;
            shownSoft = SoftRadius;
            const int n = 512;
            if (texture == null) texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            // radius r (in screen heights) is r / 4 in uv units from the centre
            float r0 = ClearRadius / 4f, r1 = SoftRadius / 4f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n - .5f, dy = (y + .5f) / n - .5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float inner = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r0, r1, dist)) * PlateAlpha;
                    float outer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlateRadius / 4f, PlateEdge / 4f, dist));
                    float k = Mathf.Lerp(inner, OutsideAlpha, outer);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(k * 255));
                }
            texture.SetPixels32(px);
            texture.Apply();
            image.texture = texture;
        }
    }
}
