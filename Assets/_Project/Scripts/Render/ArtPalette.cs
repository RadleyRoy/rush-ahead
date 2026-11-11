using System;
using System.Collections.Generic;
using System.Linq;
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
        [Tooltip("The rope round the raft's tube; none until the raft art is made.")] public Material rope;
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
        [Tooltip("See-through moving water with a solid shape (Shaders/Spray.shader): spouts, standing waves, jets.")] public Material spray;

        [Header("Photographic (Poly Haven, CC0; empty without the files)")]
        public Material planks;
        public Material metal;
        public Material sand;
        public Mesh[] rockMeshes = Array.Empty<Mesh>();
        public Material[] rockMaterials = Array.Empty<Material>();
        public Mesh buoyMesh;
        public Material buoyMaterial;
        public Mesh crateMesh;
        public Material crateMaterial;
        public Mesh barrelMesh;
        public Material barrelMaterial;
        public Mesh fernMesh;
        public Material fernMaterial;
        public Cubemap skyCubemap;
        public UnityEngine.Rendering.VolumeProfile post;

        [Header("Sculpted (baked in the editor; empty until then)")]
        [Tooltip("The crew animals, by AnimalShape.")] public Mesh[] animalMeshes = Array.Empty<Mesh>();
        [Tooltip("Velvet fur (Shaders/Fur.shader): tinted per animal from the mesh's colour regions.")] public Material fur;
        [Tooltip("The creatures, by Creature.")] public Mesh[] creatureMeshes = Array.Empty<Mesh>();

        [Header("Nature (Poly Haven, CC0; empty without the files)")]
        public Mesh[] shrubMeshes = Array.Empty<Mesh>();
        public Material[] shrubMaterials = Array.Empty<Material>();
        public Mesh[] boulderMeshes = Array.Empty<Mesh>();
        public Material[] boulderMaterials = Array.Empty<Material>();
        public Mesh cliffMesh;
        public Material cliffMaterial;
        [Tooltip("Tree foliage: leafy sprays and needle sprays on cut-out cards.")] public Material leafCards;
        public Material needleCards;
        public Material mud;
        public Material terrainMaterial;
        [Tooltip("Ground layers by role: grass, forest floor, scree, moss, mud, sand (see GorgeTerrain.Ground).")]
        public TerrainLayer[] terrainLayers = Array.Empty<TerrainLayer>();

        /// <summary>Sawn timber (bridges, gates, the ferry): planks if we have them, otherwise wood.</summary>
        public Material Planks => planks != null ? planks : wood;

        /// <summary>Metalwork (cannons, wheel hubs): rusty metal if we have it, otherwise the blower trim.</summary>
        public Material Metal => metal != null ? metal : blowerTrim;

        public bool HasRocks => rockMeshes != null && rockMeshes.Length > 0 && rockMeshes[0] != null;

        [Tooltip("Per rock mesh, as shares of its width when placed: how far its narrowest (x) and widest (y) sides reach " +
                 "from the middle seen from above at a 30% sunk waterline, and (z) the least it reaches in any direction.")]
        public Vector3[] rockReach = Array.Empty<Vector3>();

        [Tooltip("Per rock mesh: where its footprint's middle is, from the middle of its bounds, as a share of its width.")]
        public Vector2[] rockMiddle = Array.Empty<Vector2>();

        /// <summary>How a rock mesh reaches (see <see cref="rockReach"/>), or a plain round stone's when it hasn't been measured.</summary>
        public Vector3 RockReach(int i) => rockReach != null && i < rockReach.Length && rockReach[i].x > 0.05f ? rockReach[i] : new Vector3(0.45f, 0.5f, 0.4f);

        /// <summary>Whether there's what a real terrain needs: its material and every ground layer.</summary>
        public bool HasLand => terrainMaterial != null && terrainLayers != null && terrainLayers.Length >= 6 && terrainLayers.All(l => l != null);

        /// <summary>The terrain layer for a kind of ground: a photo layer, or one tinted from it for the glacier and the canyon.</summary>
        public TerrainLayer TerrainLayerFor(GorgeTerrain.Ground role) => role switch
        {
            GorgeTerrain.Ground.Snow => TintedLayer(GorgeTerrain.Ground.Sand, new Vector4(1.4f, 1.45f, 1.55f, 1f)),
            GorgeTerrain.Ground.Ice => TintedLayer(GorgeTerrain.Ground.Scree, new Vector4(0.95f, 1.25f, 1.45f, 1f)),
            GorgeTerrain.Ground.RedRock => TintedLayer(GorgeTerrain.Ground.Scree, new Vector4(1.4f, 0.72f, 0.5f, 1f)),
            GorgeTerrain.Ground.Dust => TintedLayer(GorgeTerrain.Ground.Sand, new Vector4(1.2f, 0.82f, 0.6f, 1f)),
            _ => terrainLayers[(int)role],
        };

        [NonSerialized] Dictionary<(GorgeTerrain.Ground, Vector4), TerrainLayer> tintedLayers;

        /// <summary>A copy of a photo layer with its colour scaled (the terrain shader's colour remap), made once and shared.</summary>
        TerrainLayer TintedLayer(GorgeTerrain.Ground from, Vector4 tint)
        {
            tintedLayers ??= new Dictionary<(GorgeTerrain.Ground, Vector4), TerrainLayer>();
            if (tintedLayers.TryGetValue((from, tint), out var layer) && layer != null) return layer;
            var source = terrainLayers[(int)from];
            layer = new TerrainLayer
            {
                name = $"{source.name} tinted",
                diffuseTexture = source.diffuseTexture,
                normalMapTexture = source.normalMapTexture,
                maskMapTexture = source.maskMapTexture,
                tileSize = source.tileSize,
                tileOffset = source.tileOffset,
                normalScale = source.normalScale,
                metallic = source.metallic,
                smoothness = source.smoothness,
                specular = source.specular,
                diffuseRemapMin = source.diffuseRemapMin,
                diffuseRemapMax = tint,
                maskMapRemapMin = source.maskMapRemapMin,
                maskMapRemapMax = source.maskMapRemapMax,
            };
            tintedLayers[(from, tint)] = layer;
            return layer;
        }

        [NonSerialized] Dictionary<(Material, Color), Material> tinted;
        [NonSerialized] Dictionary<(Color, Vector4), Material> sprays;

        /// <summary>
        /// Moving water with a body to it (a spout, a wave, a jet): this colour (alpha: how solid), with streaks running
        /// round the object's up axis at <paramref name="spin"/> turns a second and up it at <paramref name="rise"/> metres
        /// a second. Shared by everything asking for the same. Falls back to tinted foam without the shader.
        /// </summary>
        public Material Spray(Color color, float streaks = 0.5f, float spin = 0f, float rise = 0f, float rim = 0.8f)
        {
            if (spray == null) return Tinted(foam, color);
            sprays ??= new Dictionary<(Color, Vector4), Material>();
            var key = (color, new Vector4(streaks, spin, rise, rim));
            if (!sprays.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(spray) { name = $"Spray {ColorUtility.ToHtmlStringRGBA(color)}", color = color };
                material.SetFloat("_Streaks", streaks);
                material.SetFloat("_Spin", spin);
                material.SetFloat("_Rise", rise);
                material.SetFloat("_Rim", rim);
                sprays[key] = material;
            }

            return material;
        }
        [NonSerialized] Dictionary<(Color, Color, Color), Material> furs;

        /// <summary>The fur material in one animal's colours (shared by everyone asking for the same ones).</summary>
        public Material Fur(Color fur, Color pale, Color skin)
        {
            furs ??= new Dictionary<(Color, Color, Color), Material>();
            if (furs.TryGetValue((fur, pale, skin), out var material) && material != null) return material;
            material = new Material(this.fur) { name = $"Fur {ColorUtility.ToHtmlStringRGB(fur)}" };
            material.SetColor("_FurColor", fur);
            material.SetColor("_PaleColor", pale);
            material.SetColor("_SkinColor", skin);
            furs[(fur, pale, skin)] = material;
            return material;
        }

        /// <summary>A creature's hide: the fur material with a lower sheen and coarser grain, for scales and thick skin.</summary>
        public Material Hide(Color hide, Color pale, Color dark, Color skin)
        {
            hides ??= new Dictionary<(Color, Color, Color, Color), Material>();
            if (hides.TryGetValue((hide, pale, dark, skin), out var material) && material != null) return material;
            material = new Material(fur) { name = $"Hide {ColorUtility.ToHtmlStringRGB(hide)}" };
            material.SetColor("_FurColor", hide);
            material.SetColor("_PaleColor", pale);
            material.SetColor("_DarkColor", dark);
            material.SetColor("_SkinColor", skin);
            material.SetFloat("_Sheen", 0.25f);
            material.SetFloat("_Tufts", 28f);
            material.SetFloat("_Smoothness", 0.28f);
            hides[(hide, pale, dark, skin)] = material;
            return material;
        }

        [NonSerialized] Dictionary<(Color, Color, Color, Color), Material> hides;

        public bool HasCreature(Creature creature) => fur != null && creatureMeshes != null && (int)creature < creatureMeshes.Length && creatureMeshes[(int)creature] != null;

        public Mesh CreatureMesh(Creature creature) => creatureMeshes[(int)creature];

        /// <summary>Whether there's a sculpted mesh for this animal.</summary>
        public bool HasSculpted(int shape) => fur != null && animalMeshes != null && shape >= 0 && shape < animalMeshes.Length && animalMeshes[shape] != null;

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
