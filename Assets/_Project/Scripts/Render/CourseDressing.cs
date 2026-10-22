using UnityEngine;

namespace RushAhead.Render
{
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
        public static void CliffBank(Transform parent, Vector2 from, Vector2 to, float depth, ArtPalette palette, System.Random random)
        {
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
                var height = 1.4f + (float)random.NextDouble() * 1.4f;
                var jut = (float)random.NextDouble() * 0.8f; // ragged water's edge
                var centre = from + direction * ((i + 0.5f) * length / blocks) + outward * (depth * 0.5f - jut);
                var size = new Vector3(depth, height, length / blocks + 0.05f);

                // yaw points local +Z along the bank and local +X outward, matching size's (depth, height, length).
                Shapes3D.Primitive(PrimitiveType.Cube, bank, SimSpace.ToWorld(centre, height * 0.5f - 0.6f), size, palette.cliff, yaw, "Cliff");
                Shapes3D.Primitive(PrimitiveType.Cube, bank, SimSpace.ToWorld(centre, height - 0.55f), new Vector3(depth + 0.3f, 0.22f, size.z + 0.02f),
                    palette.grass, yaw, "Grass");

                var top = height - 0.44f;
                if (random.NextDouble() < 0.55)
                    Tree(bank, centre + outward * (float)(random.NextDouble() * depth * 0.4f), top, palette, random);
                else if (random.NextDouble() < 0.5)
                    Bamboo(bank, centre - outward * (depth * 0.4f), top, palette, random);
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

        void Awake() => phase = Random.value * 10f;

        void LateUpdate()
        {
            if (Source == null) return;
            var bob = 0.04f * Mathf.Sin(Time.time * 2f + phase);
            transform.SetPositionAndRotation(SimSpace.ToWorld(Source.position, bob), Quaternion.Euler(0f, -Source.eulerAngles.z, 0f));
        }
    }
}
