using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RushAhead.Render
{
    /// <summary>
    /// The photo finish's frozen frame in old-photo sepia: colour drained to brown, warm and a little bright, with a dark
    /// vignette. A global post-processing volume over everything else, faded in by <see cref="Weight"/>.
    /// </summary>
    public sealed class PhotoFinishLook : MonoBehaviour
    {
        Volume volume;

        public static PhotoFinishLook Create()
        {
            var go = new GameObject("Photo finish look");
            var look = go.AddComponent<PhotoFinishLook>();
            look.volume = go.AddComponent<Volume>();
            look.volume.isGlobal = true;
            look.volume.priority = 1000f;
            look.volume.weight = 0f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(0.2f);
            colour.contrast.Override(12f);

            // Sepia as a channel mix (URP drains saturation after the colour filter, which would leave plain grey).
            var sepia = profile.Add<ChannelMixer>(true);
            sepia.redOutRedIn.Override(39.3f);
            sepia.redOutGreenIn.Override(76.9f);
            sepia.redOutBlueIn.Override(18.9f);
            sepia.greenOutRedIn.Override(34.9f);
            sepia.greenOutGreenIn.Override(68.6f);
            sepia.greenOutBlueIn.Override(16.8f);
            sepia.blueOutRedIn.Override(27.2f);
            sepia.blueOutGreenIn.Override(53.4f);
            sepia.blueOutBlueIn.Override(13.1f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.48f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.18f, 0.1f, 0.04f));
            look.volume.sharedProfile = profile;
            return look;
        }

        /// <summary>0 for the game as it is, 1 for the full old photo.</summary>
        public float Weight
        {
            get => volume.weight;
            set => volume.weight = Mathf.Clamp01(value);
        }

        void OnDestroy()
        {
            if (volume != null && volume.sharedProfile != null) Destroy(volume.sharedProfile);
        }
    }
}
