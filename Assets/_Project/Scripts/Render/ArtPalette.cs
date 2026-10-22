using System;
using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// Every material the placeholder art uses. Living in an asset that the scene references is what keeps
    /// these shaders, and the transparent variants in particular, in player builds.
    /// Created and kept up to date by the editor's ContentBootstrap.
    /// </summary>
    [CreateAssetMenu(menuName = "Rush Ahead/Art Palette", fileName = "ArtPalette")]
    public sealed class ArtPalette : ScriptableObject
    {
        [Header("Opaque")]
        public Material water;
        public Material raftTube;
        public Material raftBand;
        public Material raftFloor;
        public Material rock;
        public Material cliff;
        public Material grass;
        public Material wood;
        public Material leaves;
        public Material bamboo;
        public Material sign;
        public Material barrel;
        public Material barrelBand;
        public Material animal;
        public Material eyeWhite;
        public Material eyeDark;
        public Material blowerTrim;

        [Header("Transparent")]
        public Material air;
        public Material foam;

        [NonSerialized] Dictionary<(Material, Color), Material> tinted;

        /// <summary>A colour variant of an opaque material, shared by everyone asking for the same colour.</summary>
        public Material Tinted(Material source, Color color)
        {
            tinted ??= new Dictionary<(Material, Color), Material>();
            if (!tinted.TryGetValue((source, color), out var material) || material == null)
                tinted[(source, color)] = material = new Material(source) { name = $"{source.name} {ColorUtility.ToHtmlStringRGB(color)}", color = color };
            return material;
        }
    }
}
