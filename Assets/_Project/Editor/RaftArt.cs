using System.IO;
using RushAhead.Render;
using UnityEditor;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// The raft's surfaces, drawn in code: rubber panels with lap seams for the tube and band (tinted by team), a woven
    /// fabric floor, and a twisted rope that runs round the outside of the tube.
    /// </summary>
    static class RaftArt
    {
        const string Out = "Assets/_Project/Art/Raft";

        public static void Apply(ArtPalette palette)
        {
            ContentBootstrap.EnsureFolder(Out);
            var rubber = Texture("rubber", 256, Rubber);
            Dress(palette.raftTube, rubber, new Vector2(8f, 1f), 0.55f);
            Dress(palette.raftBand, rubber, new Vector2(8f, 1f), 0.5f);
            Dress(palette.raftFloor, Texture("fabric", 256, Fabric), new Vector2(5f, 5f), 0.12f);
            palette.rope = PolyHavenArt.LoadOrCreate($"{Out}/Rope.mat", "Universal Render Pipeline/Lit");
            Dress(palette.rope, Texture("rope", 128, Rope), new Vector2(60f, 1f), 0.2f);
            palette.rope.SetColor("_BaseColor", new Color(0.86f, 0.78f, 0.6f));
            EditorUtility.SetDirty(palette);
        }

        static void Dress(Material material, Texture2D texture, Vector2 tiles, float smoothness)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiles);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
        }

        /// <summary>One rubber panel (u along the tube, v round it): a lap seam at each end, two seams along the top, faint mottling.</summary>
        static float Rubber(float u, float v)
        {
            var shade = 0.93f + 0.03f * (Mathf.PerlinNoise(u * 9f, v * 9f) - 0.5f);
            var across = Mathf.Min(u, 1f - u) * 256f;
            if (across < 1.5f) shade -= 0.25f; // the joint
            else if (across < 6f) shade += 0.04f; // the overlapping lap
            foreach (var seam in new[] { 0.14f, 0.36f })
            {
                var d = Mathf.Abs(v - seam) * 256f;
                if (d < 1.2f) shade -= 0.18f;
                else if (d < 3.5f) shade += 0.03f;
            }

            return shade;
        }

        /// <summary>A plain weave: threads over and under, a little uneven.</summary>
        static float Fabric(float u, float v)
        {
            const float threads = 48f;
            var x = u * threads;
            var y = v * threads;
            var over = ((int)x + (int)y) % 2 == 0;
            var along = over ? Mathf.Sin(Mathf.Repeat(x, 1f) * Mathf.PI) : Mathf.Sin(Mathf.Repeat(y, 1f) * Mathf.PI);
            return 0.72f + 0.2f * along + 0.06f * (Mathf.PerlinNoise(u * 20f, v * 20f) - 0.5f);
        }

        /// <summary>Twisted strands: bright diagonal bands with dark grooves between.</summary>
        static float Rope(float u, float v)
        {
            var twist = Mathf.Repeat(u * 3f + v * 1.5f, 1f);
            return 0.55f + 0.45f * Mathf.Sin(twist * Mathf.PI);
        }

        static Texture2D Texture(string name, int size, System.Func<float, float, float> shade)
        {
            var path = $"{Out}/{name}.png";
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var value = Mathf.Clamp01(shade(x / (float)size, y / (float)size));
                var c = (byte)(value * 255f);
                pixels[y * size + x] = new Color32(c, c, c, 255);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
