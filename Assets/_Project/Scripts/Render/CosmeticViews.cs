using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// The shop's hats, built from shapes on an animal's head (<see cref="AnimalSculptures.Head"/>), in the order of
    /// <see cref="Cosmetics.Hats"/>. Ears poke through, as they would.
    /// </summary>
    public static class HatModels
    {
        static readonly Color Gold = new(1f, 0.78f, 0.28f);

        /// <summary>Builds hat <paramref name="hat"/> under the head; null for none.</summary>
        public static Transform Build(int hat, Transform head, AnimalShape shape, ArtPalette palette)
        {
            if (hat <= 0 || hat >= Cosmetics.Hats.Length) return null;
            var root = Shapes3D.Group(head, "Hat " + Cosmetics.Hats[hat].Id, new Vector3(0f, 0f, -0.01f));
            root.localRotation = Quaternion.Euler(-8f, 0f, 0f); // pushed back a little, jauntily
            if (shape is AnimalShape.Bear or AnimalShape.Pig) root.localScale = Vector3.one * 1.08f;
            Material Paint(Color c) => palette.Tinted(palette.animal, c);
            var gold = Shiny(palette, Gold, 0.8f);
            var cylinder = PrimitiveType.Cylinder;
            var sphere = PrimitiveType.Sphere;
            switch (Cosmetics.Hats[hat].Id)
            {
                case "cap":
                {
                    var red = Paint(new Color(0.85f, 0.15f, 0.12f));
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.17f, 0f), new Vector3(0.46f, 0.28f, 0.48f), red, name: "Crown");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.18f, 0.2f), new Vector3(0.32f, 0.012f, 0.28f), red, name: "Peak");
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.31f, 0f), Vector3.one * 0.05f, Paint(Color.white), name: "Button");
                    break;
                }
                case "beanie":
                {
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.16f, -0.01f), new Vector3(0.49f, 0.36f, 0.49f), Paint(new Color(0.1f, 0.6f, 0.62f)), name: "Knit");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.12f, -0.01f), new Vector3(0.51f, 0.04f, 0.51f), Paint(new Color(0.06f, 0.4f, 0.44f)), name: "Turn-up");
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.35f, -0.02f), Vector3.one * 0.12f, Paint(Color.white), name: "Pompom");
                    break;
                }
                case "party":
                {
                    var cone = Shapes3D.MeshObject("Cone", Shapes3D.Cone(0.15f, 0.36f, 16), root, new Vector3(0.02f, 0.2f, 0f), Paint(new Color(1f, 0.35f, 0.65f)));
                    cone.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
                    Shapes3D.Primitive(sphere, cone.transform, new Vector3(0f, 0.36f, 0f), Vector3.one * 0.075f, Paint(new Color(1f, 0.9f, 0.2f)), name: "Pompom");
                    for (var i = 0; i < 3; i++)
                        Shapes3D.Primitive(sphere, cone.transform, new Vector3(0f, 0.08f + i * 0.09f, 0.15f - 0.042f * (i + 1) * 1.1f), Vector3.one * 0.035f,
                            Paint(i % 2 == 0 ? new Color(0.3f, 0.8f, 1f) : new Color(1f, 0.9f, 0.2f)), name: "Spot");
                    break;
                }
                case "chef":
                {
                    var white = Paint(new Color(0.97f, 0.97f, 0.95f));
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.26f, 0f), new Vector3(0.38f, 0.1f, 0.38f), white, name: "Band");
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.43f, 0f), new Vector3(0.46f, 0.26f, 0.46f), white, name: "Puff");
                    for (var i = 0; i < 4; i++)
                    {
                        var a = i * Mathf.PI * 0.5f + 0.4f;
                        Shapes3D.Primitive(sphere, root, new Vector3(Mathf.Cos(a) * 0.14f, 0.41f, Mathf.Sin(a) * 0.14f), Vector3.one * 0.22f, white, name: "Puff");
                    }

                    break;
                }
                case "flowers":
                {
                    Color[] petals = { new(1f, 0.55f, 0.75f), new(1f, 0.95f, 0.4f), new(1f, 1f, 1f), new(0.7f, 0.5f, 1f) };
                    var leaf = Paint(new Color(0.3f, 0.65f, 0.25f));
                    for (var i = 0; i < 10; i++)
                    {
                        var a = i * Mathf.PI * 2f / 10f;
                        var at = new Vector3(Mathf.Cos(a) * 0.235f, 0.15f, Mathf.Sin(a) * 0.235f);
                        Shapes3D.Primitive(sphere, root, at, Vector3.one * 0.085f, Paint(petals[i % petals.Length]), name: "Flower");
                        Shapes3D.Primitive(sphere, root, at * 1.12f + Vector3.up * 0.01f, Vector3.one * 0.035f, Paint(new Color(1f, 0.8f, 0.1f)), name: "Middle");
                        var b = a + Mathf.PI / 10f;
                        Shapes3D.Primitive(sphere, root, new Vector3(Mathf.Cos(b) * 0.24f, 0.13f, Mathf.Sin(b) * 0.24f), new Vector3(0.07f, 0.03f, 0.045f), leaf,
                            Quaternion.Euler(0f, -b * Mathf.Rad2Deg, 0f), "Leaf");
                    }

                    break;
                }
                case "headphones":
                {
                    var dark = Paint(new Color(0.12f, 0.12f, 0.14f));
                    var accent = Paint(new Color(1f, 0.42f, 0.17f));
                    for (var i = 0; i <= 8; i++)
                    {
                        var a = Mathf.PI * i / 8f;
                        Shapes3D.Primitive(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * 0.27f, 0.02f + Mathf.Sin(a) * 0.26f, -0.02f), new Vector3(0.075f, 0.035f, 0.06f), dark,
                            Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f), "Band");
                    }

                    foreach (var side in new[] { -1f, 1f })
                    {
                        Shapes3D.Primitive(cylinder, root, new Vector3(0.27f * side, -0.03f, -0.02f), new Vector3(0.17f, 0.04f, 0.17f), dark, Quaternion.Euler(0f, 0f, 90f), "Cup");
                        Shapes3D.Primitive(cylinder, root, new Vector3(0.31f * side, -0.03f, -0.02f), new Vector3(0.12f, 0.012f, 0.12f), accent, Quaternion.Euler(0f, 0f, 90f), "Cap");
                    }

                    break;
                }
                case "cowboy":
                {
                    var leather = Paint(new Color(0.55f, 0.36f, 0.2f));
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.19f, 0f), new Vector3(0.8f, 0.045f, 0.72f), leather, name: "Brim");
                    foreach (var side in new[] { -1f, 1f }) // its sides curl up
                        Shapes3D.Primitive(sphere, root, new Vector3(0.3f * side, 0.23f, 0f), new Vector3(0.2f, 0.04f, 0.5f), leather, Quaternion.Euler(0f, 0f, 30f * side), "Curl");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.29f, 0f), new Vector3(0.34f, 0.1f, 0.32f), leather, name: "Crown");
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.38f, 0f), new Vector3(0.34f, 0.1f, 0.3f), leather, name: "Top");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.22f, 0f), new Vector3(0.35f, 0.025f, 0.33f), Paint(new Color(0.2f, 0.12f, 0.08f)), name: "Band");
                    break;
                }
                case "propeller":
                {
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.16f, 0f), new Vector3(0.47f, 0.32f, 0.47f), Paint(new Color(0.2f, 0.45f, 0.95f)), name: "Dome");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.17f, 0f), new Vector3(0.48f, 0.03f, 0.48f), Paint(new Color(1f, 0.85f, 0.2f)), name: "Stripe");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.34f, 0f), new Vector3(0.025f, 0.04f, 0.025f), Paint(new Color(0.3f, 0.3f, 0.32f)), name: "Stick");
                    var spinner = Shapes3D.Group(root, "Propeller", new Vector3(0f, 0.385f, 0f));
                    Shapes3D.Primitive(PrimitiveType.Cube, spinner, new Vector3(0.09f, 0f, 0f), new Vector3(0.17f, 0.01f, 0.05f), Paint(new Color(0.9f, 0.15f, 0.15f)), Quaternion.Euler(12f, 0f, 0f), "Blade");
                    Shapes3D.Primitive(PrimitiveType.Cube, spinner, new Vector3(-0.09f, 0f, 0f), new Vector3(0.17f, 0.01f, 0.05f), Paint(new Color(1f, 0.85f, 0.2f)), Quaternion.Euler(-12f, 0f, 0f), "Blade");
                    spinner.gameObject.AddComponent<Spinner>();
                    break;
                }
                case "pirate":
                {
                    var black = Paint(new Color(0.1f, 0.09f, 0.1f));
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.19f, 0f), new Vector3(0.42f, 0.22f, 0.38f), black, name: "Crown");
                    Shapes3D.Primitive(PrimitiveType.Capsule, root, new Vector3(0f, 0.27f, -0.01f), new Vector3(0.2f, 0.34f, 0.12f), black, Quaternion.Euler(0f, 0f, 90f), "Bicorne");
                    Shapes3D.Primitive(PrimitiveType.Capsule, root, new Vector3(0f, 0.27f, 0.052f), new Vector3(0.205f, 0.345f, 0.01f), gold, Quaternion.Euler(0f, 0f, 90f), "Trim");
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.3f, 0.065f), new Vector3(0.08f, 0.075f, 0.02f), Paint(Color.white), name: "Skull");
                    break;
                }
                case "viking":
                {
                    var steel = Shiny(palette, new Color(0.7f, 0.72f, 0.75f), 0.75f);
                    var bone = Paint(new Color(0.95f, 0.9f, 0.78f));
                    Shapes3D.Primitive(sphere, root, new Vector3(0f, 0.12f, 0f), new Vector3(0.53f, 0.4f, 0.53f), steel, name: "Helmet");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.08f, 0f), new Vector3(0.54f, 0.035f, 0.54f), gold, name: "Rim");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.2f, 0f), new Vector3(0.04f, 0.13f, 0.54f), gold, Quaternion.Euler(90f, 0f, 0f), "Ridge");
                    foreach (var side in new[] { -1f, 1f })
                    {
                        Shapes3D.Primitive(PrimitiveType.Capsule, root, new Vector3(0.27f * side, 0.24f, 0f), new Vector3(0.09f, 0.14f, 0.09f), bone, Quaternion.Euler(0f, 0f, -48f * side), "Horn");
                        Shapes3D.Primitive(PrimitiveType.Capsule, root, new Vector3(0.36f * side, 0.38f, 0f), new Vector3(0.055f, 0.08f, 0.055f), bone, Quaternion.Euler(0f, 0f, -15f * side), "Tip");
                    }

                    break;
                }
                case "tophat":
                {
                    var black = Paint(new Color(0.08f, 0.08f, 0.1f));
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.2f, 0f), new Vector3(0.52f, 0.012f, 0.52f), black, name: "Brim");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.37f, 0f), new Vector3(0.33f, 0.17f, 0.33f), black, name: "Body");
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.25f, 0f), new Vector3(0.34f, 0.035f, 0.34f), Paint(new Color(0.75f, 0.1f, 0.15f)), name: "Band");
                    break;
                }
                case "crown":
                {
                    Shapes3D.Primitive(cylinder, root, new Vector3(0f, 0.24f, 0f), new Vector3(0.37f, 0.06f, 0.37f), gold, name: "Band");
                    for (var i = 0; i < 6; i++)
                    {
                        var a = i * Mathf.PI * 2f / 6f;
                        var at = new Vector3(Mathf.Cos(a) * 0.165f, 0.29f, Mathf.Sin(a) * 0.165f);
                        Shapes3D.MeshObject("Point", Shapes3D.Cone(0.05f, 0.12f, 6), root, at, gold);
                        Shapes3D.Primitive(sphere, root, at + Vector3.up * 0.125f, Vector3.one * 0.035f, gold, name: "Ball");
                    }

                    Color[] gems = { new(0.95f, 0.1f, 0.2f), new(0.15f, 0.45f, 1f), new(0.1f, 0.85f, 0.4f) };
                    for (var i = 0; i < 3; i++)
                    {
                        var a = Mathf.PI * 0.5f + (i - 1) * 0.7f;
                        Shapes3D.Primitive(sphere, root, new Vector3(Mathf.Cos(a) * 0.185f, 0.24f, Mathf.Sin(a) * 0.185f), Vector3.one * 0.05f, palette.Glow(gems[i]), name: "Gem");
                    }

                    break;
                }
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        static readonly Dictionary<(Color, float), Material> shiny = new();

        /// <summary>Polished metal in a colour: gold, steel, chrome.</summary>
        internal static Material Shiny(ArtPalette palette, Color colour, float smoothness)
        {
            if (shiny.TryGetValue((colour, smoothness), out var material) && material != null) return material;
            material = new Material(palette.animal) { name = $"Shiny {ColorUtility.ToHtmlStringRGB(colour)}", color = colour };
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", smoothness);
            return shiny[(colour, smoothness)] = material;
        }

        /// <summary>Turns a propeller round and round.</summary>
        sealed class Spinner : MonoBehaviour
        {
            void Update() => transform.Rotate(0f, 900f * Time.deltaTime, 0f, Space.Self);
        }
    }

    /// <summary>
    /// The raft skins: patterns drawn in code over the rubber panels of the tube, in the order of
    /// <see cref="Cosmetics.Skins"/>, each over the tube's own colour (so team rafts stay red, blue, green or yellow).
    /// One texture per skin and colour, made the first time it's worn.
    /// </summary>
    public static class RaftSkins
    {
        const int Size = 256;
        const float PanelMetres = 1.02f; // u, along the tube: one of the eight rubber panels
        const float RoundMetres = 1.44f; // v, round the tube (0 the outside, 0.25 the top, 0.5 the inside, 0.75 underneath)
        static readonly Color Gold = new(1f, 0.78f, 0.28f);
        static readonly Dictionary<(int, Color), Material> made = new();

        /// <summary>The tube's material for <paramref name="skin"/> over <paramref name="tube"/> (the material's own colour if null).</summary>
        public static Material For(ArtPalette palette, int skin, Color? tube)
        {
            var plain = tube.HasValue ? palette.Tinted(palette.raftTube, tube.Value) : palette.raftTube;
            if (skin <= 0 || skin >= Cosmetics.Skins.Length) return plain;
            var colour = tube ?? palette.raftTube.color;
            if (made.TryGetValue((skin, colour), out var material) && material != null) return material;

            var id = Cosmetics.Skins[skin].Id;
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                var c = Pattern(id, u, v, colour) * RubberShade(u, v);
                c.a = 1f;
                pixels[y * Size + x] = c;
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Skin " + id, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            material = new Material(plain) { name = $"Raft tube {id} {ColorUtility.ToHtmlStringRGB(colour)}", color = Color.white };
            material.SetTexture("_BaseMap", texture);
            if (id == "gold")
            {
                material.SetFloat("_Metallic", 0.55f);
                material.SetFloat("_Smoothness", 0.8f);
            }

            return made[(skin, colour)] = material;
        }

        /// <summary>One rubber panel (u along the tube, v round it): a lap seam at each end, two seams along the top, faint mottling.</summary>
        public static float RubberShade(float u, float v)
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

        static Color Pattern(string id, float u, float v, Color base_)
        {
            var white = Color.Lerp(base_, Color.white, 0.88f);
            switch (id)
            {
                case "stripes":
                    return Mathf.Repeat(u * 2f, 1f) < 0.32f ? white : base_;
                case "dots":
                {
                    // Round dots on a grid that fits the panel exactly, every other row shifted by half.
                    var row = v * 6f;
                    var col = u * 4f + (Mathf.FloorToInt(row) % 2 == 0 ? 0f : 0.5f);
                    var dx = (Mathf.Repeat(col, 1f) - 0.5f) * PanelMetres / 4f;
                    var dy = (Mathf.Repeat(row, 1f) - 0.5f) * RoundMetres / 6f;
                    return dx * dx + dy * dy < 0.075f * 0.075f ? white : base_;
                }
                case "checker":
                    return (Mathf.FloorToInt(u * 4f) + Mathf.FloorToInt(v * 6f)) % 2 == 0 ? white : Color.Lerp(base_, Color.black, 0.15f);
                case "stars":
                {
                    var row = v * 3f;
                    var col = u * 2f + (Mathf.FloorToInt(row) % 2 == 0 ? 0f : 0.5f);
                    var dx = (Mathf.Repeat(col, 1f) - 0.5f) * PanelMetres / 2f;
                    var dy = (Mathf.Repeat(row, 1f) - 0.5f) * RoundMetres / 3f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var a = Mathf.Atan2(dy, dx) - Mathf.PI * 0.5f;
                    var edge = 0.15f * Mathf.Lerp(0.42f, 1f, Mathf.Pow(0.5f + 0.5f * Mathf.Cos(5f * a), 3f));
                    return r < edge ? Color.Lerp(white, new Color(1f, 0.95f, 0.55f), 0.5f) : base_;
                }
                case "camo":
                {
                    var n = Tileable(u, v, 3f) + 0.5f * Tileable(u, v, 7f);
                    if (n < 0.62f) return Color.Lerp(base_, Color.black, 0.45f);
                    if (n < 0.82f) return Color.Lerp(base_, new Color(0.35f, 0.4f, 0.2f), 0.55f);
                    return base_;
                }
                case "shark":
                {
                    // A grinning mouth along the outside of the tube: a dark band with white teeth top and bottom.
                    var s = v > 0.5f ? v - 1f : v; // -0.5 to 0.5, 0 the outside
                    if (Mathf.Abs(s) > 0.13f) return base_;
                    var t = Mathf.Repeat(u * 4f + (s < 0f ? 0.5f : 0f), 1f);
                    var depth = (0.13f - Mathf.Abs(s)) / 0.1f; // 0 at the lips, 1 in the middle of the mouth
                    if (Mathf.Abs(t - 0.5f) * 2f < 1f - depth) return new Color(0.97f, 0.96f, 0.92f);
                    return new Color(0.35f, 0.05f, 0.08f);
                }
                case "flames":
                {
                    // Flames licking up the outside from underneath: yellow at the roots, orange, then red at the tips.
                    var h = Mathf.Repeat(v - 0.75f, 1f) / 0.5f; // 0 underneath, 1 on top, more on the inside
                    if (h > 1f) return base_;
                    var lick = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 3f) * (0.6f + 0.4f * Mathf.Sin(u * Mathf.PI * 2f * 7f + 1.3f));
                    var reach = 0.38f + 0.42f * lick;
                    if (h > reach) return base_;
                    var k = h / reach;
                    return k < 0.45f ? Color.Lerp(new Color(1f, 0.92f, 0.3f), new Color(1f, 0.55f, 0.1f), k / 0.45f)
                        : Color.Lerp(new Color(1f, 0.55f, 0.1f), new Color(0.9f, 0.12f, 0.05f), (k - 0.45f) / 0.55f);
                }
                case "gold":
                {
                    var sparkle = Mathf.PerlinNoise(u * 60f, v * 60f) > 0.78f ? 0.25f : 0f;
                    var s = v > 0.5f ? v - 1f : v;
                    var band = Mathf.Abs(s - 0.1f) < 0.05f || Mathf.Abs(s + 0.1f) < 0.05f || Mathf.Abs(v - 0.25f) < 0.04f;
                    var gold = Color.Lerp(Gold, Color.white, sparkle);
                    return band ? gold : Color.Lerp(base_, gold, 0.35f + sparkle);
                }
                default:
                    return base_;
            }
        }

        /// <summary>Perlin noise that wraps at the panel's ends and round the tube, so the pattern has no seams.</summary>
        static float Tileable(float u, float v, float scale)
        {
            float N(float a, float b) => Mathf.PerlinNoise(a * scale + 11.3f, b * scale + 4.7f);
            var top = Mathf.Lerp(N(u, v), N(u - 1f, v), u);
            var bottom = Mathf.Lerp(N(u, v - 1f), N(u - 1f, v - 1f), u);
            return Mathf.Lerp(top, bottom, v);
        }
    }

    /// <summary>The blower paints, in the order of <see cref="Cosmetics.Paints"/>: plain colours, metals, camo and rainbow.</summary>
    public static class BlowerPaints
    {
        static Material camo;
        static Material rainbow;

        /// <summary>The paint's main colour (the blower's own for the factory finish), for its body and for icons.</summary>
        public static Color Colour(int paint, Color factory) => Id(paint) switch
        {
            "red" => new Color(0.86f, 0.13f, 0.1f),
            "blue" => new Color(0.1f, 0.38f, 0.88f),
            "lime" => new Color(0.58f, 0.9f, 0.16f),
            "bubblegum" => new Color(1f, 0.52f, 0.76f),
            "midnight" => new Color(0.09f, 0.09f, 0.24f),
            "camo" => new Color(0.38f, 0.42f, 0.22f),
            "chrome" => new Color(0.86f, 0.88f, 0.92f),
            "gold" => new Color(1f, 0.78f, 0.28f),
            "rainbow" => new Color(1f, 0.4f, 0.75f),
            _ => factory,
        };

        static string Id(int paint) => paint > 0 && paint < Cosmetics.Paints.Length ? Cosmetics.Paints[paint].Id : "factory";

        /// <summary>The body's material, and a lighter one for its trim pieces.</summary>
        public static (Material body, Material light) Materials(int paint, Color factory, ArtPalette palette)
        {
            var colour = Colour(paint, factory);
            var light = palette.Tinted(palette.animal, Color.Lerp(colour, Color.white, 0.5f));
            switch (Id(paint))
            {
                case "chrome":
                    return (HatModels.Shiny(palette, colour, 0.92f), HatModels.Shiny(palette, Color.Lerp(colour, Color.white, 0.4f), 0.9f));
                case "gold":
                    return (HatModels.Shiny(palette, colour, 0.82f), HatModels.Shiny(palette, Color.Lerp(colour, Color.white, 0.3f), 0.8f));
                case "camo":
                    camo ??= Textured(palette, "Camo paint", Camo());
                    return (camo, palette.Tinted(palette.animal, new Color(0.28f, 0.3f, 0.18f)));
                case "rainbow":
                    rainbow ??= Textured(palette, "Rainbow paint", Rainbow());
                    return (rainbow, palette.Tinted(palette.animal, Color.white));
                case "midnight":
                    return (HatModels.Shiny(palette, colour, 0.85f), light);
                default:
                    return (palette.Tinted(palette.animal, colour), light);
            }
        }

        static Material Textured(ArtPalette palette, string name, Texture2D texture)
        {
            var material = new Material(palette.animal) { name = name, color = Color.white };
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.4f);
            return material;
        }

        static Texture2D Camo()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var n = Mathf.PerlinNoise(x * 0.09f + 3f, y * 0.09f + 7f) + 0.5f * Mathf.PerlinNoise(x * 0.21f, y * 0.21f + 2f);
                pixels[y * size + x] = n < 0.6f ? new Color(0.2f, 0.24f, 0.12f) : n < 0.85f ? new Color(0.38f, 0.42f, 0.22f) : new Color(0.55f, 0.48f, 0.3f);
            }

            return Finish("Camo", size, pixels);
        }

        static Texture2D Rainbow()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = Color.HSVToRGB(Mathf.Repeat((x + y) / (float)size, 1f), 0.7f, 1f);
            return Finish("Rainbow", size, pixels);
        }

        static Texture2D Finish(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
