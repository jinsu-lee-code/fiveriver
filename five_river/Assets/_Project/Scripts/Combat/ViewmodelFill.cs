using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FiveRivers.Combat
{
    // The moon fill on each first-person weapon (M1_MoonFill, Bazooka_MoonFill) is there to light the weapon and the
    // hands only. Its Culling Mask says so (layer 31), but this URP asset filters lights by rendering layer, so it lit
    // everything within its 1.5 m as well: with the bazooka up at the parapet the sandbags in front glowed white, and
    // went dark again at every weapon change (filmed 2026-11-17 evening). The light now has a rendering layer of its
    // own that only the first-person renderers carry; they keep the world layer too, so the moon still lights them.
    public static class ViewmodelFill
    {
        // 1 = world (default), 2 = the ammo crate's candle (AmmoCrateM1917Setup), 4 = first-person weapon fill
        public const uint Layer = 4u;

        // `inside`: any transform of the first-person weapon; `lightName`: its fill light somewhere under the same view model
        public static void Confine(Transform inside, string lightName)
        {
            if (inside == null) return;
            // the view model root: the topmost parent below the camera (the arms are a sibling of the weapon there)
            var root = inside;
            while (root.parent != null && root.parent.GetComponent<Camera>() == null) root = root.parent;
            Light fill = null;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.name == lightName) { fill = light; break; }
            if (fill == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask |= Layer;
            fill.GetUniversalAdditionalLightData().renderingLayers = Layer;
        }
    }
}
