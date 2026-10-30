using System;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// How a place looks: what its cliffs are made of, how far down the gorge floor is, and what grows. Creek is red
    /// earth, grass, trees and bamboo; Lake is sandstone over a shallower drop, with reeds and lily pads; Rapids is grey
    /// rock over a deep gorge of pines. The height and plants also dress the practice lagoon's banks.
    /// </summary>
    public sealed class BankStyle
    {
        public Material Cliff;
        public Material CliffDeep; // the cliff's darker lower face
        public Material Ground;

        /// <summary>From the water down to the gorge floor, in metres.</summary>
        public float Drop = 24f;
        public float Height = 1.5f;
        public float HeightRange = 1.3f;

        /// <summary>Plants set back from the edge, and right on it.</summary>
        public Action<Transform, Vector2, float, ArtPalette, System.Random> Plant;
        public Action<Transform, Vector2, float, ArtPalette, System.Random> Fringe;
        public bool LilyPads;

        public static BankStyle For(CourseScene scene, ArtPalette palette)
        {
            var style = Make(scene, palette);
            style.CliffDeep = palette.Tinted(palette.cliff, Color.Lerp(style.Cliff.color, new Color(0.2f, 0.18f, 0.22f), 0.35f));
            return style;
        }

        static BankStyle Make(CourseScene scene, ArtPalette palette) => scene switch
        {
            CourseScene.Lake => new BankStyle
            {
                Cliff = palette.Tinted(palette.cliff, new Color(0.9f, 0.79f, 0.56f)),
                Ground = palette.Tinted(palette.grass, new Color(0.62f, 0.83f, 0.4f)),
                Height = 0.75f,
                HeightRange = 0.4f,
                Drop = 16f,
                Plant = CourseDressing.Tree,
                Fringe = CourseDressing.Reeds,
                LilyPads = true,
            },
            CourseScene.Rapids => new BankStyle
            {
                Cliff = palette.Tinted(palette.cliff, new Color(0.56f, 0.57f, 0.62f)),
                Ground = palette.Tinted(palette.grass, new Color(0.3f, 0.55f, 0.32f)),
                Height = 3.4f,
                HeightRange = 2.4f,
                Drop = 36f,
                Plant = CourseDressing.Pine,
                Fringe = CourseDressing.Pine,
            },
            _ => new BankStyle
            {
                Cliff = palette.cliff,
                Ground = palette.grass,
                Plant = CourseDressing.Tree,
                Fringe = CourseDressing.Bamboo,
            },
        };
    }

    /// <summary>
    /// Placeholder scenery in the style of the reference: bright water, clay cliffs with grass tops, round trees,
    /// bamboo, rocks and floating barrels. Purely visual; the matching physics comes from the course itself.
    /// </summary>
    public static class CourseDressing
    {
        public static void Water(Transform parent, Vector2 centre, Vector2 size, ArtPalette palette)
        {
            // Unity's plane primitive is 10 x 10 units.
            Shapes3D.Primitive(PrimitiveType.Plane, parent, SimSpace.ToWorld(centre), new Vector3(size.x / 10f, 1f, size.y / 10f), palette.water, name: "Water");
        }

        /// <summary>A cliff bank running from <paramref name="from"/> to <paramref name="to"/>, built outward on the right-hand side.</summary>
        public static void CliffBank(Transform parent, Vector2 from, Vector2 to, float depth, ArtPalette palette, System.Random random, BankStyle style = null)
        {
            style ??= BankStyle.For(CourseScene.Creek, palette);
            var along = to - from;
            var length = along.magnitude;
            var direction = along / length;
            var outward = new Vector2(direction.y, -direction.x);
            var yaw = Quaternion.LookRotation(SimSpace.Direction(direction));
            var bank = Shapes3D.Group(parent, "Bank");

            const float blockLength = 4f;
            var blocks = Mathf.CeilToInt(length / blockLength);
            for (var i = 0; i < blocks; i++)
            {
                var height = style.Height - 0.1f + (float)random.NextDouble() * (style.HeightRange + 0.1f);
                var jut = (float)random.NextDouble() * 0.8f; // ragged water's edge
                var centre = from + direction * ((i + 0.5f) * length / blocks) + outward * (depth * 0.5f - jut);
                var size = new Vector3(depth, height, length / blocks + 0.05f);

                // yaw points local +Z along the bank and local +X outward, matching size's (depth, height, length).
                Shapes3D.Primitive(PrimitiveType.Cube, bank, SimSpace.ToWorld(centre, height * 0.5f - 0.6f), size, style.Cliff, yaw, "Cliff");
                Shapes3D.Primitive(PrimitiveType.Cube, bank, SimSpace.ToWorld(centre, height - 0.55f), new Vector3(depth + 0.3f, 0.22f, size.z + 0.02f),
                    style.Ground, yaw, "Grass");

                var top = height - 0.44f;
                if (random.NextDouble() < 0.55)
                    style.Plant(bank, centre + outward * (float)(random.NextDouble() * depth * 0.4f), top, palette, random);
                else if (random.NextDouble() < 0.5)
                    style.Fringe(bank, centre - outward * (depth * 0.4f), top, palette, random);
            }
        }

        public static void Tree(Transform parent, Vector2 at, float groundHeight, ArtPalette palette, System.Random random)
        {
            var tree = Shapes3D.Group(parent, "Tree", SimSpace.ToWorld(at, groundHeight));
            var scale = 0.8f + (float)random.NextDouble() * 0.6f;
            Shapes3D.Primitive(PrimitiveType.Cylinder, tree, new Vector3(0f, 0.9f * scale, 0f), new Vector3(0.28f, 0.9f, 0.28f) * scale, palette.wood, name: "Trunk");
            Shapes3D.Primitive(PrimitiveType.Sphere, tree, new Vector3(0f, 2.2f * scale, 0f), new Vector3(2.2f, 1.6f, 2.2f) * scale, palette.leaves, name: "Canopy");
            Shapes3D.Primitive(PrimitiveType.Sphere, tree, new Vector3(0.5f, 2.8f, 0.2f) * scale, new Vector3(1.4f, 1.1f, 1.4f) * scale, palette.leaves, name: "Canopy");
        }

        public static void Bamboo(Transform parent, Vector2 at, float groundHeight, ArtPalette palette, System.Random random)
        {
            var clump = Shapes3D.Group(parent, "Bamboo", SimSpace.ToWorld(at, groundHeight));
            for (var i = 0; i < 3; i++)
            {
                var height = 2f + (float)random.NextDouble() * 1.6f;
                var offset = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 0.8f;
                Shapes3D.Primitive(PrimitiveType.Cylinder, clump, offset + Vector3.up * height * 0.5f, new Vector3(0.16f, height * 0.5f, 0.16f), palette.bamboo,
                    Quaternion.Euler((float)random.NextDouble() * 6f - 3f, 0f, (float)random.NextDouble() * 6f - 3f), "Stalk");
            }
        }

        /// <summary>A pine: a dark trunk and three stacked cones, taller than the round trees.</summary>
        public static void Pine(Transform parent, Vector2 at, float groundHeight, ArtPalette palette, System.Random random)
        {
            var pine = Shapes3D.Group(parent, "Pine", SimSpace.ToWorld(at, groundHeight));
            var scale = 0.8f + (float)random.NextDouble() * 0.7f;
            var needles = palette.Tinted(palette.leaves, new Color(0.16f, 0.42f, 0.27f));
            Shapes3D.Primitive(PrimitiveType.Cylinder, pine, new Vector3(0f, 0.6f * scale, 0f), new Vector3(0.26f, 0.6f, 0.26f) * scale, palette.wood, name: "Trunk");
            for (var tier = 0; tier < 3; tier++)
            {
                var radius = (1.5f - tier * 0.35f) * scale;
                Shapes3D.MeshObject("Needles", Shapes3D.Cone(radius, 1.9f * scale, 10), pine, new Vector3(0f, (0.9f + tier * 1.05f) * scale, 0f), needles);
            }
        }

        /// <summary>A clump of reeds with brown cattails, for lake shores.</summary>
        public static void Reeds(Transform parent, Vector2 at, float groundHeight, ArtPalette palette, System.Random random)
        {
            var clump = Shapes3D.Group(parent, "Reeds", SimSpace.ToWorld(at, groundHeight));
            var stem = palette.Tinted(palette.bamboo, new Color(0.55f, 0.72f, 0.3f));
            var tip = palette.Tinted(palette.wood, new Color(0.45f, 0.28f, 0.15f));
            for (var i = 0; i < 6; i++)
            {
                var height = 1.1f + (float)random.NextDouble() * 1f;
                var offset = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 1.1f;
                var lean = Quaternion.Euler((float)random.NextDouble() * 14f - 7f, 0f, (float)random.NextDouble() * 14f - 7f);
                Shapes3D.Primitive(PrimitiveType.Cylinder, clump, offset + Vector3.up * height * 0.5f, new Vector3(0.06f, height * 0.5f, 0.06f), stem, lean, "Stem");
                if (i % 2 == 0)
                    Shapes3D.Primitive(PrimitiveType.Capsule, clump, offset + lean * (Vector3.up * height), new Vector3(0.14f, 0.2f, 0.14f), tip, lean, "Cattail");
            }
        }

        /// <summary>A lily pad flat on the water, sometimes with a pink flower.</summary>
        public static void LilyPad(Transform parent, Vector2 at, ArtPalette palette, System.Random random)
        {
            var radius = 0.45f + (float)random.NextDouble() * 0.35f;
            var pad = Shapes3D.MeshObject("Lily pad", Shapes3D.Disc(radius, 14), parent, SimSpace.ToWorld(at, 0.03f),
                palette.Tinted(palette.leaves, new Color(0.3f, 0.66f, 0.3f)));
            if (random.NextDouble() < 0.3)
                Shapes3D.Primitive(PrimitiveType.Sphere, pad.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.16f, 0.28f),
                    palette.Tinted(palette.sign, new Color(1f, 0.6f, 0.78f)), name: "Flower");
        }

        /// <summary>A floating log that follows a 2D physics body, lying along the body's x axis.</summary>
        public static void Log(Transform parent, Transform physicsBody, float length, float radius, ArtPalette palette)
        {
            var log = Shapes3D.Group(parent, "Log");
            var bark = palette.Tinted(palette.wood, new Color(0.45f, 0.3f, 0.18f));
            var cut = palette.Tinted(palette.wood, new Color(0.86f, 0.7f, 0.46f));
            var lying = Quaternion.Euler(0f, 0f, 90f); // a cylinder's axis is its y: lay it along x
            Shapes3D.Primitive(PrimitiveType.Cylinder, log, new Vector3(0f, 0.12f, 0f), new Vector3(2f * radius, length * 0.5f - 0.04f, 2f * radius), bark, lying, "Bark");
            foreach (var end in new[] { -1f, 1f })
                Shapes3D.Primitive(PrimitiveType.Cylinder, log, new Vector3(end * (length * 0.5f - 0.03f), 0.12f, 0f),
                    new Vector3(2f * radius - 0.06f, 0.03f, 2f * radius - 0.06f), cut, lying, "End");
            Shapes3D.Primitive(PrimitiveType.Cube, log, new Vector3(0f, 0.02f, 0f), new Vector3(length + 0.5f, 0.01f, 2f * radius + 0.5f), palette.foam, name: "Foam");
            log.gameObject.AddComponent<FollowBody>().Source = physicsBody;
        }

        public static void Rock(Transform parent, Vector2 at, float radius, ArtPalette palette, System.Random random)
        {
            var rock = Shapes3D.Group(parent, "Rock", SimSpace.ToWorld(at));
            var spin = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            Shapes3D.Primitive(PrimitiveType.Sphere, rock, new Vector3(0f, 0.15f, 0f), new Vector3(2f * radius, 1.5f * radius, 2f * radius), palette.rock, spin, "Boulder");
            Shapes3D.Primitive(PrimitiveType.Sphere, rock, spin * new Vector3(radius * 0.7f, 0.05f, radius * 0.3f), Vector3.one * radius * 0.9f, palette.rock, spin, "Stone");
            Shapes3D.MeshObject("Foam", Shapes3D.Disc(radius + 0.5f), rock, new Vector3(0f, 0.02f, 0f), palette.foam);
        }

        /// <summary>A floating striped barrel that follows a 2D physics body.</summary>
        public static void Barrel(Transform parent, Transform physicsBody, float radius, ArtPalette palette)
        {
            var barrel = Shapes3D.Group(parent, "Barrel");
            Shapes3D.Primitive(PrimitiveType.Cylinder, barrel, new Vector3(0f, 0.25f, 0f), new Vector3(2f * radius, 0.45f, 2f * radius), palette.barrel, name: "Drum");
            Shapes3D.Primitive(PrimitiveType.Cylinder, barrel, new Vector3(0f, 0.3f, 0f), new Vector3(2f * radius + 0.04f, 0.07f, 2f * radius + 0.04f), palette.barrelBand, name: "Band");
            Shapes3D.MeshObject("Foam", Shapes3D.Disc(radius + 0.35f), barrel, new Vector3(0f, 0.02f, 0f), palette.foam);
            barrel.gameObject.AddComponent<FollowBody>().Source = physicsBody;
        }

        /// <summary>Two bamboo posts and a crossbar sign over a gap, like the reference's start and checkpoint gates.</summary>
        public static void Gate(Transform parent, Vector2 left, Vector2 right, ArtPalette palette)
        {
            var gate = Shapes3D.Group(parent, "Gate");
            const float height = 4.2f;
            foreach (var post in new[] { left, right })
                Shapes3D.Primitive(PrimitiveType.Cylinder, gate, SimSpace.ToWorld(post, height * 0.5f), new Vector3(0.35f, height * 0.5f, 0.35f), palette.bamboo, name: "Post");

            var middle = (left + right) * 0.5f;
            var span = right - left;
            var yaw = Quaternion.LookRotation(SimSpace.Direction(new Vector2(-span.y, span.x)));
            Shapes3D.Primitive(PrimitiveType.Cube, gate, SimSpace.ToWorld(middle, height - 0.3f), new Vector3(span.magnitude + 0.8f, 0.25f, 0.25f), palette.wood, yaw, "Crossbar");
            Shapes3D.Primitive(PrimitiveType.Cube, gate, SimSpace.ToWorld(middle, height + 0.35f), new Vector3(span.magnitude * 0.6f, 0.9f, 0.15f), palette.sign, yaw, "Sign");
        }
    }

    /// <summary>Keeps a visual on top of a 2D physics body, floating with a gentle bob.</summary>
    public sealed class FollowBody : MonoBehaviour
    {
        public Transform Source;
        float phase;

        void Awake() => phase = UnityEngine.Random.value * 10f;

        void LateUpdate()
        {
            if (Source == null) return;
            var bob = 0.04f * Mathf.Sin(Time.time * 2f + phase);
            transform.SetPositionAndRotation(SimSpace.ToWorld(Source.position, bob), Quaternion.Euler(0f, -Source.eulerAngles.z, 0f));
        }
    }
}
