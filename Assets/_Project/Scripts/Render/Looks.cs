using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RushAhead.Render
{
    /// <summary>Switches for the expensive parts of the look, for the graphics setting.</summary>
    public static class Looks
    {
        /// <summary>Tone mapping, bloom, colour and vignette on the camera (the scene's volume holds the settings).</summary>
        public static void PostProcessing(Camera camera, bool on)
        {
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData data)) return;
            data.renderPostProcessing = on;
        }

        public static bool HasPostProcessing(Camera camera) =>
            camera != null && camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderPostProcessing;

        /// <summary>How much scenery to plant for the graphics setting: thinner on Low, all of it on High.</summary>
        public static float Detail => QualitySettings.GetQualityLevel() switch { <= 1 => 0.5f, 2 => 0.75f, _ => 1f };
    }
}
