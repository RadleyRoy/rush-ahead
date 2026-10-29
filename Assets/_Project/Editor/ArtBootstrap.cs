using RushAhead.Render;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// Creates the placeholder art's materials and the <see cref="ArtPalette"/> that holds them. Colours are
    /// art direction rather than tuning, so running this again always re-applies them.
    /// </summary>
    static class ArtBootstrap
    {
        const string ArtFolder = "Assets/_Project/Art";
        const string MaterialFolder = ArtFolder + "/Materials";

        public static ArtPalette CreatePalette()
        {
            ContentBootstrap.EnsureFolder(MaterialFolder);
            var puff = PuffTexture();

            var palette = AssetDatabase.LoadAssetAtPath<ArtPalette>($"{ArtFolder}/ArtPalette.asset");
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<ArtPalette>();
                AssetDatabase.CreateAsset(palette, $"{ArtFolder}/ArtPalette.asset");
            }

            // A bright, saturated toy palette, after the reference game.
            palette.water = Water();
            palette.raftTube = Lit("RaftTube", new Color(1f, 0.72f, 0.56f), 0.45f);
            palette.raftBand = Lit("RaftBand", new Color(0.25f, 0.78f, 0.7f), 0.45f);
            palette.raftFloor = Lit("RaftFloor", new Color(0.55f, 0.85f, 0.45f), 0.2f);
            palette.rock = Lit("Rock", new Color(0.5f, 0.48f, 0.5f), 0.1f);
            palette.cliff = Lit("Cliff", new Color(0.86f, 0.42f, 0.32f), 0.05f);
            palette.grass = Lit("Grass", new Color(0.45f, 0.78f, 0.3f), 0.1f);
            palette.wood = Lit("Wood", new Color(0.55f, 0.36f, 0.2f), 0.1f);
            palette.leaves = Lit("Leaves", new Color(0.3f, 0.66f, 0.25f), 0.1f);
            palette.bamboo = Lit("Bamboo", new Color(0.5f, 0.82f, 0.35f), 0.25f);
            palette.sign = Lit("Sign", new Color(1f, 0.78f, 0.2f), 0.2f);
            palette.barrel = Lit("Barrel", new Color(0.88f, 0.22f, 0.25f), 0.35f);
            palette.barrelBand = Lit("BarrelBand", new Color(0.97f, 0.97f, 0.95f), 0.35f);
            palette.animal = Lit("Animal", Color.white, 0.2f); // tinted per animal at runtime
            palette.eyeWhite = Lit("EyeWhite", Color.white, 0.6f);
            palette.eyeDark = Lit("EyeDark", new Color(0.08f, 0.07f, 0.09f), 0.7f);
            palette.blowerTrim = Lit("BlowerTrim", new Color(0.18f, 0.18f, 0.22f), 0.4f);
            palette.air = Soft("Air", Color.white, puff);
            palette.foam = Soft("Foam", new Color(1f, 1f, 1f, 0.55f), puff);

            EditorUtility.SetDirty(palette);
            return palette;
        }

        public static Material Sky()
        {
            var sky = LoadOrCreateMaterial("Sky", "Skybox/Procedural");
            sky.SetColor("_SkyTint", new Color(0.55f, 0.72f, 1f));
            sky.SetColor("_GroundColor", new Color(0.62f, 0.72f, 0.78f));
            sky.SetFloat("_AtmosphereThickness", 0.75f);
            sky.SetFloat("_Exposure", 1.35f);
            sky.SetFloat("_SunSize", 0.03f);
            EditorUtility.SetDirty(sky);
            return sky;
        }

        /// <summary>The stylized water (Shaders/Water.shader): the reference's bright blue with drifting ripples and glints.</summary>
        static Material Water()
        {
            var material = LoadOrCreateMaterial("Water", "RushAhead/Water");
            material.SetColor("_BaseColor", new Color(0.13f, 0.66f, 0.95f));
            material.SetColor("_RippleColor", new Color(0.62f, 0.9f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Lit(string name, Color color, float smoothness)
        {
            var material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Soft transparent material for air clouds and foam, set up the way URP's own material inspector would.</summary>
        static Material Soft(string name, Color color, Texture2D texture)
        {
            var material = LoadOrCreateMaterial(name, "Universal Render Pipeline/Particles/Unlit");
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f);   // alpha blending
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (material.shader != shader) material.shader = shader;
                return material;
            }

            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A soft white blob: opaque centre fading to nothing at the edge.</summary>
        static Texture2D PuffTexture()
        {
            var path = $"{ArtFolder}/Puff.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Puff", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var centre = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = new Vector2(x - centre, y - centre).magnitude / centre;
                var alpha = Mathf.Clamp01(1f - distance);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * alpha * 255));
            }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }
    }
}
