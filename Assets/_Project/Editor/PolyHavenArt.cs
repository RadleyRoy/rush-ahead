using System.IO;
using System.Linq;
using RushAhead.Render;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// The photographic art from Poly Haven (CC0, in <c>Assets/_Project/ThirdParty/PolyHaven</c>): import settings for
    /// its textures, models and sky, materials made from them, and the post-processing look. Run as part of
    /// <see cref="ArtBootstrap.CreatePalette"/>; if the files aren't there the palette keeps its flat colours.
    /// </summary>
    static class PolyHavenArt
    {
        const string Root = "Assets/_Project/ThirdParty/PolyHaven";
        const string Out = "Assets/_Project/Art/PolyHaven";

        public static bool Available => AssetDatabase.IsValidFolder(Root);

        public static void Apply(ArtPalette palette)
        {
            if (!Available) return;
            ContentBootstrap.EnsureFolder(Out);
            ConfigureImporters();

            // Surfaces built in code, textured by projection (Shaders/Triplanar.shader). The tint is an overlay: 0.5 is as photographed.
            palette.rock = Triplanar("Stone", "cliff_side", 2.5f, 0.12f, new Color(0.5f, 0.5f, 0.5f));
            palette.cliff = Triplanar("Cliff", "cliff_side", 6f, 0.1f, new Color(0.6f, 0.47f, 0.4f)); // the creek's warm earthy rock
            palette.grass = Triplanar("Ground", "aerial_grass_rock", 9f, 0.08f, new Color(0.6f, 0.62f, 0.54f));
            palette.wood = Triplanar("Bark", "bark_brown_02", 1.6f, 0.12f, new Color(0.5f, 0.5f, 0.5f), objectSpace: true);
            palette.leaves = Triplanar("Leaves", "forest_leaves_02", 1.4f, 0.15f, new Color(0.42f, 0.55f, 0.38f), objectSpace: true);
            palette.planks = Triplanar("Planks", "weathered_planks", 2.2f, 0.15f, new Color(0.6f, 0.55f, 0.5f), objectSpace: true);
            palette.metal = Triplanar("Metal", "rusty_metal_02", 1.2f, 0.35f, new Color(0.5f, 0.5f, 0.5f), objectSpace: true);
            palette.sand = Triplanar("Sand", "coast_sand_01", 4f, 0.1f, new Color(0.5f, 0.5f, 0.5f));

            // Scanned models, with their own UVs.
            palette.rockMeshes = new[] { FirstMesh("rock_07"), FirstMesh("rock_09"), FirstMesh("rock_face_01") };
            palette.rockMaterials = new[] { Model("rock_07", 0.12f), Model("rock_09", 0.12f), Model("rock_face_01", 0.12f) };
            palette.buoyMesh = FirstMesh("ocean_buoy");
            palette.buoyMaterial = Model("ocean_buoy", 0.4f);
            palette.crateMesh = FirstMesh("wooden_crate_02");
            palette.crateMaterial = Model("wooden_crate_02", 0.15f);
            palette.barrelMesh = FirstMesh("wooden_barrels_01", "barrel01");
            palette.barrelMaterial = Model("wooden_barrels_01", 0.2f, "wooden_barrels_01_barrel01");
            palette.fernMesh = FirstMesh("fern_02");
            palette.fernMaterial = Fern();

            palette.skyCubemap = AssetDatabase.LoadAssetAtPath<Cubemap>($"{Root}/sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr");
            palette.post = PostProfile();
            EditorUtility.SetDirty(palette);
        }

        /// <summary>The sky: the photographed one, as a skybox. Null without the files.</summary>
        public static Material Sky()
        {
            if (!Available) return null;
            ConfigureImporters();
            var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>($"{Root}/sky/kloofendal_48d_partly_cloudy_puresky_2k.hdr");
            if (cubemap == null) return null;
            var sky = LoadOrCreate($"{Out}/Sky.mat", "Skybox/Cubemap");
            sky.SetTexture("_Tex", cubemap);
            sky.SetFloat("_Exposure", 1.05f);
            sky.SetFloat("_Rotation", 40f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                var changed = false;
                void Set<T>(T current, T wanted, System.Action<T> apply)
                {
                    if (Equals(current, wanted)) return;
                    apply(wanted);
                    changed = true;
                }

                if (path.EndsWith(".hdr"))
                {
                    Set(importer.textureShape, TextureImporterShape.TextureCube, v => importer.textureShape = v);
                    Set(importer.maxTextureSize, 2048, v => importer.maxTextureSize = v);
                }
                else
                {
                    Set(importer.textureType, path.Contains("_nor_gl_") ? TextureImporterType.NormalMap : TextureImporterType.Default, v => importer.textureType = v);
                    Set(importer.maxTextureSize, 1024, v => importer.maxTextureSize = v);
                    var android = importer.GetPlatformTextureSettings("Android");
                    if (!android.overridden || android.format != TextureImporterFormat.ASTC_6x6)
                    {
                        android.overridden = true;
                        android.maxTextureSize = 1024;
                        android.format = TextureImporterFormat.ASTC_6x6;
                        importer.SetPlatformTextureSettings(android);
                        changed = true;
                    }
                }

                if (changed) importer.SaveAndReimport();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                if (importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation && !importer.importCameras) continue;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }
        }

        static Texture2D Texture(string folder, string file) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{folder}/{file}");

        internal static Material Triplanar(string name, string set, float metresPerTile, float smoothness, Color tint, bool objectSpace = false)
        {
            var material = LoadOrCreate($"{Out}/{name}.mat", "RushAhead/Triplanar");
            material.SetTexture("_BaseMap", Texture(set, $"{set}_diff_1k.jpg"));
            material.SetTexture("_BumpMap", Texture(set, $"{set}_nor_gl_1k.jpg"));
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Tile", metresPerTile);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_ObjectSpace", objectSpace ? 1f : 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP Lit with a model's own colour and normal maps.</summary>
        internal static Material Model(string folder, float smoothness, string prefix = null)
        {
            prefix ??= folder;
            var material = LoadOrCreate($"{Out}/{prefix}.mat", "Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", Texture(folder, $"{prefix}_diff_1k.jpg"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", Texture(folder, $"{prefix}_nor_gl_1k.jpg"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The fern: its colour with the alpha mask folded in, cut out and seen from both sides.</summary>
        static Material Fern()
        {
            var basePath = $"{Out}/fern_02_base.png";
            if (!File.Exists(basePath))
            {
                var colour = new Texture2D(2, 2);
                colour.LoadImage(File.ReadAllBytes($"{Root}/fern_02/fern_02_diff_1k.jpg"));
                var alpha = new Texture2D(2, 2);
                alpha.LoadImage(File.ReadAllBytes($"{Root}/fern_02/fern_02_alpha_1k.jpg"));
                var pixels = colour.GetPixels32();
                var mask = alpha.GetPixels32();
                for (var i = 0; i < pixels.Length && i < mask.Length; i++) pixels[i].a = mask[i].r;
                var combined = new Texture2D(colour.width, colour.height, TextureFormat.RGBA32, false);
                combined.SetPixels32(pixels);
                File.WriteAllBytes(basePath, combined.EncodeToPNG());
                Object.DestroyImmediate(colour);
                Object.DestroyImmediate(alpha);
                Object.DestroyImmediate(combined);
                AssetDatabase.ImportAsset(basePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(basePath);
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }

            var material = LoadOrCreate($"{Out}/Fern.mat", "Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", Texture("fern_02", "fern_02_nor_gl_1k.jpg"));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f); // leaves seen from both sides
            material.SetFloat("_Smoothness", 0.2f);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh FirstMesh(string folder, string nameHint = null)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath($"{Root}/{folder}/{folder}_1k.fbx").OfType<Mesh>().ToArray();
            return (nameHint != null ? meshes.FirstOrDefault(m => m.name.Contains(nameHint)) : null) ?? meshes.FirstOrDefault();
        }

        /// <summary>The look: filmic tone mapping, a little bloom on the sun's glints, a touch more contrast and colour, a soft vignette.</summary>
        static VolumeProfile PostProfile()
        {
            var path = $"{Out}/Post.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            T Component<T>() where T : VolumeComponent
            {
                if (profile.TryGet<T>(out var existing)) return existing;
                var added = profile.Add<T>(true);
                added.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(added, profile);
                return added;
            }

            Component<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom = Component<Bloom>();
            bloom.threshold.Override(1.25f);
            bloom.intensity.Override(0.2f);
            bloom.scatter.Override(0.6f);
            var colour = Component<ColorAdjustments>();
            colour.postExposure.Override(0.45f);
            colour.contrast.Override(10f);
            colour.saturation.Override(8f);
            var vignette = Component<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        internal static Material LoadOrCreate(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (material.shader != shader) material.shader = shader;
                return material;
            }

            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
