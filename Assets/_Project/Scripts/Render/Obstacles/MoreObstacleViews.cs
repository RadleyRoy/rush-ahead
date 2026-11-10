using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// A tall pine on the lip, its roots in a mound of earth. Once a race it shakes (dropping needles), topples across the
    /// river with its root plate tearing up, and lies there for the rest of the race with the water piling white
    /// against it. A pine, not a round-crowned tree: its tip is thin, so the gap past it reads as open water.
    /// </summary>
    sealed class FallingTreeView : ObstacleView
    {
        readonly FallingTree tree;
        readonly Transform pivot;
        readonly Transform foam;
        readonly Transform roots;
        readonly Quaternion upright;
        readonly Vector3 foot;
        float lastTilt;

        public FallingTreeView(FallingTree tree, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Falling tree"), palette)
        {
            this.tree = tree;
            var into = -tree.Spot.side * tree.At.Left; // the way it falls: into the river
            var bark = Tint(palette.wood, new Color(0.36f, 0.25f, 0.16f));
            var earth = Tint(palette.wood, new Color(0.3f, 0.22f, 0.15f));
            var length = tree.Length;
            foot = SimSpace.ToWorld(tree.Foot, 0.3f);
            upright = Facing(into);
            pivot = Shapes3D.Group(Root, "Pivot");
            pivot.SetPositionAndRotation(foot, upright);
            if (Flora.Available(palette))
            {
                Flora.Tree(Flora.Kind.Conifer, pivot, Vector3.zero, length + 0.4f, 37f * tree.Spot.distance, Mathf.RoundToInt(tree.Spot.distance), palette);
            }
            else
            {
                Part(PrimitiveType.Cylinder, pivot, new Vector3(0f, length * 0.5f, 0f), new Vector3(FallingTree.TrunkWidth, length * 0.5f, FallingTree.TrunkWidth), bark, name: "Trunk");
                for (var i = 0; i < 7; i++)
                {
                    var size = Mathf.Lerp(2.4f, 0.8f, i / 6f);
                    Part(PrimitiveType.Sphere, pivot, new Vector3(0f, length * (0.3f + 0.1f * i), 0f), new Vector3(size, size * 0.6f, size), palette.leaves, name: "Bough");
                }
            }

            // The root plate tips up with the trunk; the mound it stood in stays on the bank, with a dark hole once it's gone.
            roots = Shapes3D.Group(pivot, "Root plate");
            Part(PrimitiveType.Cylinder, roots, new Vector3(0f, -0.15f, 0f), new Vector3(1.9f, 0.18f, 1.9f), earth, name: "Plate");
            for (var i = 0; i < 6; i++)
            {
                var angle = i * 60f + 15f;
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Part(PrimitiveType.Cylinder, roots, direction * 0.75f + Vector3.down * 0.25f, new Vector3(0.16f, 0.45f, 0.16f), bark,
                    Quaternion.LookRotation(Vector3.down, direction) * Quaternion.Euler(-60f, 0f, 0f), "Root");
            }

            var outward = SimSpace.Direction(-into);
            Part(PrimitiveType.Sphere, Root, foot + outward * 0.9f + Vector3.down * 0.5f, new Vector3(3.6f, 1.4f, 3.2f), earth, name: "Mound");
            Part(PrimitiveType.Cylinder, Root, foot + Vector3.down * 0.02f, new Vector3(1.7f, 0.03f, 1.7f), Tint(palette.wood, new Color(0.12f, 0.09f, 0.06f)), name: "Hole");
            foam = Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(tree.Foot + into * (length * 0.5f), 0.03f), new Vector3(FallingTree.TrunkWidth + 1.4f, 0.04f, length),
                palette.foam, upright, "Wash").transform;
        }

        public override void Pose(double seconds)
        {
            var (tilt, shake) = tree.Pose(seconds);
            var wobble = shake * shake * 6f * Mathf.Sin((float)seconds * 27f);
            pivot.SetPositionAndRotation(foot, upright * Quaternion.Euler(tilt * 90f + wobble, 0f, 0f));
            foam.gameObject.SetActive(tilt >= 1f);
            if (tilt >= 1f && lastTilt is > 0f and < 1f) // it hit the water just now
                for (var i = 1; i <= 3; i++)
                    Splashes.Splash(SimSpace.ToWorld(tree.Foot - tree.Spot.side * tree.At.Left * (tree.Length * i / 3.5f), 0.1f), 2.2f, Palette);
            lastTilt = tilt;
        }
    }

    /// <summary>A big snapping turtle paddling across, its head darting out now and then.</summary>
    sealed class TurtleView : ObstacleView
    {
        readonly Turtle turtle;
        readonly Transform head;
        readonly Transform[] flippers = new Transform[4];

        public TurtleView(Turtle turtle, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Turtle"), palette)
        {
            this.turtle = turtle;
            const float r = Turtle.Radius;
            if (palette.HasCreature(Creature.TurtleShell) && palette.HasCreature(Creature.TurtleHead) && palette.HasCreature(Creature.TurtleFlipper))
            {
                var hide = palette.Hide(new Color(0.3f, 0.31f, 0.19f), new Color(0.47f, 0.48f, 0.34f), new Color(0.19f, 0.2f, 0.13f), new Color(0.6f, 0.58f, 0.45f));
                Shapes3D.MeshObject("Shell", palette.CreatureMesh(Creature.TurtleShell), Root, new Vector3(0f, 0.05f, 0f), hide);
                head = Shapes3D.Group(Root, "Head", new Vector3(0f, 0.1f, Turtle.Radius + 0.2f));
                Shapes3D.MeshObject("Head", palette.CreatureMesh(Creature.TurtleHead), head, Vector3.zero, hide);
                foreach (var x in new[] { -1f, 1f })
                    Part(PrimitiveType.Sphere, head, new Vector3(0.21f * x, 0.17f, 0.28f), Vector3.one * 0.08f, palette.eyeDark, name: "Eye");
                var corner = new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) };
                for (var i = 0; i < 4; i++)
                {
                    flippers[i] = Shapes3D.Group(Root, "Flipper", new Vector3(corner[i].x * 0.9f * Turtle.Radius, -0.05f, corner[i].y * 0.6f * Turtle.Radius));
                    var fin = Shapes3D.MeshObject("Fin", palette.CreatureMesh(Creature.TurtleFlipper), flippers[i], Vector3.zero, hide);
                    fin.transform.localRotation = Quaternion.Euler(0f, corner[i].x < 0f ? 180f : 0f, 0f);
                }

                Shapes3D.MeshObject("Wake", Shapes3D.Disc(Turtle.Radius + 0.5f, 24), Root, new Vector3(0f, 0.02f, 0f), palette.foam);
                return;
            }

            var shell = Tint(palette.animal, new Color(0.3f, 0.32f, 0.2f));
            var plate = Tint(palette.animal, new Color(0.22f, 0.23f, 0.15f));
            var skin = Tint(palette.animal, new Color(0.42f, 0.45f, 0.32f));
            Part(PrimitiveType.Sphere, Root, new Vector3(0f, 0.05f, 0f), new Vector3(2f * r, 0.95f, 2.2f * r), shell, name: "Shell");
            for (var i = -2; i <= 2; i++)
                Part(PrimitiveType.Sphere, Root, new Vector3(0f, 0.42f, i * 0.55f), new Vector3(0.7f, 0.3f, 0.55f), plate, name: "Ridge");
            foreach (var x in new[] { -1f, 1f })
                for (var i = -1; i <= 1; i++)
                    Part(PrimitiveType.Sphere, Root, new Vector3(x * 0.75f, 0.3f, i * 0.75f), new Vector3(0.65f, 0.25f, 0.65f), plate, name: "Plate");

            head = Shapes3D.Group(Root, "Head", new Vector3(0f, 0.1f, r + 0.2f));
            Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.8f, 0.6f, 1f), skin, name: "Skull");
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.05f, 0.45f), new Vector3(0.5f, 0.32f, 0.42f), plate, name: "Beak");
            foreach (var x in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, head, new Vector3(0.27f * x, 0.17f, 0.2f), Vector3.one * 0.15f, palette.eyeDark, name: "Eye");

            var corners = new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) };
            for (var i = 0; i < 4; i++)
            {
                flippers[i] = Shapes3D.Group(Root, "Flipper", new Vector3(corners[i].x * 0.9f * r, -0.05f, corners[i].y * 0.6f * r));
                Part(PrimitiveType.Sphere, flippers[i], new Vector3(corners[i].x * 0.35f, 0f, 0f), new Vector3(1f, 0.16f, 0.55f), skin, name: "Fin");
            }

            Shapes3D.MeshObject("Wake", Shapes3D.Disc(r + 0.5f, 24), Root, new Vector3(0f, 0.02f, 0f), palette.foam);
        }

        public override void Pose(double seconds)
        {
            var at = turtle.At;
            var moving = turtle.Across(seconds + 0.05) - turtle.Across(seconds - 0.05);
            var swim = Mathf.Clamp(moving / 0.08f, -1f, 1f);
            var facing = at.Left * swim + at.Tangent * (1f - Mathf.Abs(swim)); // turns round downstream at each side
            var t = (float)seconds;
            Root.SetPositionAndRotation(SimSpace.ToWorld(turtle.Position(seconds), 0.04f * Mathf.Sin(t * 1.7f)), Facing(facing));
            for (var i = 0; i < 4; i++)
                flippers[i].localRotation = Quaternion.Euler(0f, 30f * Mathf.Sin(t * 4f + i * 1.6f) * (i % 2 == 0 ? 1f : -1f), 0f);
            var snap = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.3f)), 6f);
            head.localPosition = new Vector3(0f, 0.1f, Turtle.Radius + 0.2f + 0.35f * snap);
        }
    }

    /// <summary>A frothing patch of water drifting across, with piranhas leaping out of it.</summary>
    sealed class PiranhaShoalView : ObstacleView
    {
        readonly PiranhaShoal shoal;
        readonly Transform churn;
        readonly List<(Transform fish, Vector2 spot, float phase)> fish = new();

        public PiranhaShoalView(PiranhaShoal shoal, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Piranha shoal"), palette)
        {
            this.shoal = shoal;
            churn = Shapes3D.Group(Root, "Churn");
            Shapes3D.MeshObject("Froth", Shapes3D.Disc(PiranhaShoal.Radius, 28), churn, new Vector3(0f, 0.03f, 0f),
                Tint(palette.foam, new Color(0.9f, 0.42f, 0.36f, 0.5f)));
            var finSkin = Tint(palette.animal, new Color(0.22f, 0.22f, 0.24f));
            for (var i = 0; i < 7; i++)
            {
                // Fins cutting the water, circling (they turn with the churn).
                var angle = i * Mathf.PI * 2f / 7f;
                var r = PiranhaShoal.Radius * (i % 2 == 0 ? 0.7f : 0.45f);
                var fin = Shapes3D.Group(churn, "Fin", new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
                fin.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
                Part(PrimitiveType.Cube, fin, new Vector3(0f, 0.18f, 0f), new Vector3(0.05f, 0.36f, 0.32f), finSkin, Quaternion.Euler(-30f, 0f, 0f), "Dorsal");
                Shapes3D.MeshObject("Wake", Shapes3D.Disc(0.28f, 10), fin, new Vector3(0f, 0.04f, -0.2f), palette.foam);
            }
            var random = new System.Random(Mathf.RoundToInt(shoal.Spot.distance * 11f));
            for (var i = 0; i < 9; i++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var r = (float)random.NextDouble() * PiranhaShoal.Radius * 0.8f;
                Shapes3D.MeshObject("Bubbles", Shapes3D.Disc(0.4f + (float)random.NextDouble() * 0.5f, 12), churn,
                    new Vector3(Mathf.Cos(angle) * r, 0.05f, Mathf.Sin(angle) * r), palette.foam);
            }

            var scales = Tint(palette.animal, new Color(0.62f, 0.6f, 0.58f));
            var belly = Tint(palette.animal, new Color(0.85f, 0.25f, 0.18f));
            for (var i = 0; i < 12; i++)
            {
                var one = Shapes3D.Group(Root, "Piranha");
                Part(PrimitiveType.Sphere, one, Vector3.zero, new Vector3(0.18f, 0.34f, 0.5f), scales, name: "Body");
                Part(PrimitiveType.Sphere, one, new Vector3(0f, -0.07f, 0.06f), new Vector3(0.15f, 0.18f, 0.34f), belly, name: "Belly");
                Part(PrimitiveType.Cube, one, new Vector3(0f, 0f, -0.3f), new Vector3(0.03f, 0.22f, 0.14f), scales, name: "Tail");
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var r = Mathf.Sqrt((float)random.NextDouble()) * PiranhaShoal.Radius * 0.85f;
                fish.Add((one, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, (float)random.NextDouble() * 10f));
            }
        }

        public override void Pose(double seconds)
        {
            var centre = shoal.Position(seconds);
            churn.position = SimSpace.ToWorld(centre);
            churn.localRotation = Quaternion.Euler(0f, (float)seconds * 75f, 0f);
            foreach (var (one, spot, phase) in fish)
            {
                var t = (float)seconds * 3.2f + phase;
                var hop = Mathf.Sin(t);
                var heading = new Vector2(Mathf.Cos(phase), Mathf.Sin(phase));
                var at = centre + spot + heading * (0.5f * Mathf.Cos(t));
                one.gameObject.SetActive(hop > -0.2f);
                one.SetPositionAndRotation(SimSpace.ToWorld(at, Mathf.Max(0f, hop) * 0.8f - 0.1f),
                    Facing(heading) * Quaternion.Euler(-Mathf.Cos(t) * 55f, 0f, 0f)); // nose up as it leaps, down as it dives back
            }
        }
    }

    /// <summary>A whirling column of spray from the river to a dark cloud, with foam spiralling round its foot.</summary>
    sealed class WaterspoutView : ObstacleView
    {
        readonly Waterspout spout;
        readonly Transform column;
        readonly Transform swirl;
        readonly Transform cloud;

        public WaterspoutView(Waterspout spout, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Waterspout"), palette)
        {
            this.spout = spout;
            const float core = Waterspout.Core;
            var heights = new[] { 0f, 1.2f, 3f, 5.5f, 8f, 10.5f, 12.5f, 14.2f };
            var radii = new[] { core * 1.25f, core * 0.85f, core * 0.7f, core * 0.75f, core * 0.95f, core * 1.35f, core * 2f, core * 3.2f };
            var sway = new[] { 0f, 0.15f, 0.45f, 0.7f, 0.55f, 0.15f, -0.3f, -0.5f };
            column = Shapes3D.Group(Root, "Column");
            Shapes3D.MeshObject("Funnel", Lathe(radii, heights, sway), column, Vector3.zero, palette.Spray(new Color(0.55f, 0.6f, 0.66f, 0.85f), 0.9f, 0f, 2.5f, 1f));
            var veil = new float[radii.Length];
            for (var i = 0; i < radii.Length; i++) veil[i] = radii[i] * 1.45f + 0.2f;
            Shapes3D.MeshObject("Veil", Lathe(veil, heights, sway), column, Vector3.zero, palette.Spray(new Color(0.88f, 0.9f, 0.94f, 0.3f), 0.7f, -0.3f, 1.5f, 1.2f));

            // The storm cloud it hangs from: dark, low and lumpy.
            cloud = Shapes3D.Group(Root, "Cloud", new Vector3(0f, 15.2f, 0f));
            var storm = Tint(palette.eyeWhite, new Color(0.33f, 0.35f, 0.4f));
            var random = new System.Random(Mathf.RoundToInt(spout.Spot.distance * 3f));
            for (var i = 0; i < 9; i++)
            {
                var angle = i * 40f * Mathf.Deg2Rad;
                var r = i == 0 ? 0f : 2.2f + (float)random.NextDouble() * 2.6f;
                var size = 3.4f + (float)random.NextDouble() * 2.4f;
                Part(PrimitiveType.Sphere, cloud, new Vector3(Mathf.Cos(angle) * r, (float)random.NextDouble() * 0.8f, Mathf.Sin(angle) * r),
                    new Vector3(size, size * 0.55f, size), storm, name: "Billow");
            }

            // Spray boiling up round its foot, and streaks of foam spiralling in.
            swirl = Shapes3D.Group(Root, "Swirl");
            Shapes3D.MeshObject("Spray", Lathe(new[] { core * 3f, core * 2.4f, core * 1.3f }, new[] { 0.05f, 1f, 2.3f }), swirl, Vector3.zero,
                palette.Spray(new Color(0.95f, 0.97f, 1f, 0.6f), 0.6f, 0f, 1f));
            Shapes3D.MeshObject("Foot", Shapes3D.Disc(core * 2.6f, 24), swirl, new Vector3(0f, 0.04f, 0f), palette.foam);
            for (var i = 0; i < 10; i++)
            {
                var angle = i * Mathf.PI / 5f;
                var r = 2.2f + (i % 3) * 0.9f;
                Part(PrimitiveType.Cube, swirl, new Vector3(Mathf.Cos(angle) * r, 0.03f, Mathf.Sin(angle) * r), new Vector3(0.35f, 0.03f, 2f + r * 0.4f), palette.foam,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 25f, 0f), "Streak");
            }
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            Root.position = SimSpace.ToWorld(spout.Position(seconds));
            column.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 4f, t * 260f, Mathf.Cos(t * 0.9f) * 4f);
            swirl.localRotation = Quaternion.Euler(0f, t * 150f, 0f);
            cloud.localRotation = Quaternion.Euler(0f, t * 12f, 0f);
        }
    }

    /// <summary>Eels lying in a band right across the river; the water over them flickers blue, then crackles with sparks.</summary>
    sealed class ElectricEelsView : ObstacleView
    {
        readonly ElectricEels eels;
        readonly Material glow;
        readonly Transform glowStrip;
        readonly List<Transform> sparks = new();
        readonly List<(Transform eel, float phase)> bodies = new();

        public ElectricEelsView(ElectricEels eels, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Electric eels"), palette)
        {
            this.eels = eels;
            var at = eels.At;
            var along = Quaternion.LookRotation(SimSpace.Direction(at.Tangent)); // local x runs across the river
            var skin = Tint(palette.animal, new Color(0.18f, 0.2f, 0.16f));
            var spots = Tint(palette.sign, new Color(0.95f, 0.8f, 0.2f));
            var random = new System.Random(Mathf.RoundToInt(eels.Spot.distance * 9f));
            for (var i = 0; i < 5; i++)
            {
                var across = Mathf.Lerp(-0.8f, 0.8f, i / 4f) * at.HalfWidth;
                var lengthways = ((float)random.NextDouble() - 0.5f) * ElectricEels.Depth * 0.6f;
                var eel = Shapes3D.Group(Root, "Eel");
                eel.SetPositionAndRotation(SimSpace.ToWorld(at.Centre + at.Left * across + at.Tangent * lengthways, 0f), Facing(at.Left * (i % 2 == 0 ? 1f : -1f)));
                for (var s = 0; s < 7; s++)
                {
                    // Backs and heads just breaking the surface, glowing spots along them.
                    var size = s == 0 ? 0.5f : 0.46f - s * 0.04f;
                    Part(PrimitiveType.Sphere, eel, new Vector3(0f, 0f, 0.9f - s * 0.5f), new Vector3(size, size * 0.55f, 0.68f), skin, name: "Segment");
                    Part(PrimitiveType.Sphere, eel, new Vector3(0f, size * 0.24f, 0.9f - s * 0.5f), Vector3.one * 0.13f, spots, name: "Spot");
                }

                foreach (var x in new[] { -1f, 1f })
                    Part(PrimitiveType.Sphere, eel, new Vector3(0.14f * x, 0.12f, 1.05f), Vector3.one * 0.09f, palette.eyeDark, name: "Eye");

                bodies.Add((eel, (float)random.NextDouble() * 6f));
            }

            glow = new Material(palette.foam) { name = "Eel glow", color = new Color(0.45f, 0.85f, 1f, 0f) };
            glowStrip = Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(at.Centre, 0.05f), new Vector3(2f * at.HalfWidth, 0.02f, ElectricEels.Depth), glow, along, "Glow").transform;
            // A warning sign on each bank: a yellow diamond with a black bolt.
            var yellow = Tint(palette.sign, new Color(1f, 0.82f, 0.1f));
            var black = Tint(palette.eyeDark, new Color(0.05f, 0.05f, 0.05f));
            foreach (var side in new[] { -1f, 1f })
            {
                var post = Shapes3D.Group(Root, "Warning sign");
                post.SetPositionAndRotation(SimSpace.ToWorld(at.Centre + at.Left * (side * (at.HalfWidth + 0.7f)) - at.Tangent * (ElectricEels.Depth * 0.5f + 1f)),
                    Facing(-at.Tangent));
                Part(PrimitiveType.Cylinder, post, new Vector3(0f, 0.9f, 0f), new Vector3(0.1f, 0.9f, 0.1f), palette.Planks, name: "Post");
                Part(PrimitiveType.Cube, post, new Vector3(0f, 2.05f, -0.06f), new Vector3(0.85f, 0.85f, 0.06f), yellow, Quaternion.Euler(0f, 0f, 45f), "Diamond");
                Part(PrimitiveType.Cube, post, new Vector3(0.06f, 2.2f, -0.1f), new Vector3(0.12f, 0.42f, 0.03f), black, Quaternion.Euler(0f, 0f, -25f), "Bolt");
                Part(PrimitiveType.Cube, post, new Vector3(-0.06f, 1.9f, -0.1f), new Vector3(0.12f, 0.42f, 0.03f), black, Quaternion.Euler(0f, 0f, -25f), "Bolt");
                Part(PrimitiveType.Cube, post, new Vector3(0f, 2.05f, -0.1f), new Vector3(0.3f, 0.09f, 0.03f), black, Quaternion.Euler(0f, 0f, 20f), "Bolt");
            }

            var spark = Tint(palette.eyeWhite, new Color(0.8f, 0.95f, 1f));
            for (var i = 0; i < 12; i++)
            {
                var across = ((float)random.NextDouble() * 2f - 1f) * at.HalfWidth;
                var lengthways = ((float)random.NextDouble() - 0.5f) * ElectricEels.Depth;
                sparks.Add(Part(PrimitiveType.Cylinder, Root, SimSpace.ToWorld(at.Centre + at.Left * across + at.Tangent * lengthways, 0.4f), new Vector3(0.05f, 0.45f, 0.05f), spark,
                    name: "Spark").transform);
            }
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var warning = eels.Warning(seconds, out var pulsing);
            // A faint hum always marks the band; it flickers harder as a pulse comes, and blazes during one.
            var hum = 0.16f + 0.05f * Mathf.Sin(t * 7f);
            var flicker = pulsing ? 0.8f : warning > 0f && Mathf.Sin(t * 45f) > 0.6f - warning ? 0.3f + 0.3f * warning : hum;
            glow.color = new Color(0.45f, 0.85f, 1f, flicker);
            glowStrip.gameObject.SetActive(true);
            for (var i = 0; i < sparks.Count; i++)
            {
                sparks[i].gameObject.SetActive(pulsing && Mathf.Sin(t * 60f + i * 2.1f) > 0f);
                sparks[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 50f + i) * 50f, i * 37f, Mathf.Cos(t * 47f + i) * 50f);
            }

            foreach (var (eel, phase) in bodies)
            {
                var p = eel.position;
                eel.position = new Vector3(p.x, (pulsing ? 0.12f : 0f) + 0.03f * Mathf.Sin(t * 1.7f + phase), p.z);
                var e = eel.eulerAngles;
                eel.rotation = Quaternion.Euler(e.x, e.y, (pulsing ? 25f : 6f) * Mathf.Sin(t * (pulsing ? 30f : 2f) + phase));
            }
        }
    }

    /// <summary>A raw earth slope above one side: it trembles and trickles, then a brown slurry pours out across the river.</summary>
    sealed class MudslideView : ObstacleView
    {
        readonly Mudslide slide;
        readonly Transform slope;
        readonly Transform pour;
        readonly Transform sheet;
        readonly List<(Transform lump, float along, float out_)> lumps = new();
        readonly Vector3 slopeAt;
        readonly Vector2 edge;
        readonly Vector2 into;

        public MudslideView(Mudslide slide, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Mudslide"), palette)
        {
            this.slide = slide;
            var at = slide.At;
            into = -slide.Spot.side * at.Left;
            edge = at.Centre - into * at.HalfWidth;
            var mud = Tint(palette.rock, new Color(0.38f, 0.27f, 0.17f));
            var wet = palette.mud != null ? palette.mud : Tint(palette.rock, new Color(0.27f, 0.19f, 0.11f));
            var facing = Facing(into);
            var random0 = new System.Random(Mathf.RoundToInt(slide.Spot.distance * 3f));
            slope = Shapes3D.Group(Root, "Hillside");
            slopeAt = Vector3.zero;
            Shapes3D.MeshObject("Bluff", Bluff(at, slide.Spot.side, Mudslide.Along + 1.5f, 5f, 5f, random0), slope, Vector3.zero,
                palette.mud != null ? Tint(palette.mud, new Color(0.74f, 0.6f, 0.46f)) : mud);
            // The scar down its face where the mud comes away: from near the top to the water.
            pour = Shapes3D.Group(Root, "Pour");
            pour.SetPositionAndRotation(SimSpace.ToWorld(edge - into * 2.4f, 2.45f), facing * Quaternion.Euler(44f, 0f, 0f));
            Part(PrimitiveType.Cube, pour, Vector3.zero, new Vector3(2f * Mudslide.Along, 0.3f, 7f), wet, name: "Chute");
            sheet = Shapes3D.Group(Root, "Sheet");
            sheet.SetPositionAndRotation(SimSpace.ToWorld(edge, 0.06f), facing);
            Part(PrimitiveType.Cube, sheet, new Vector3(0f, 0f, 0.5f), new Vector3(2f * Mudslide.Along, 0.14f, 1f), wet, name: "Slurry");
            var random = new System.Random(Mathf.RoundToInt(slide.Spot.distance * 5f));
            for (var i = 0; i < 9; i++)
            {
                var size = 0.4f + (float)random.NextDouble() * 0.5f;
                lumps.Add((Part(PrimitiveType.Sphere, Root, Vector3.zero, new Vector3(size, size * 0.6f, size), mud, name: "Lump").transform,
                    ((float)random.NextDouble() * 2f - 1f) * Mudslide.Along * 0.9f, (float)random.NextDouble()));
            }
        }

        public override void Pose(double seconds)
        {
            var flow = slide.Flow(seconds, out var rumble);
            var t = (float)seconds;
            slope.localPosition = slopeAt + new Vector3(Mathf.Sin(t * 41f), 0f, Mathf.Cos(t * 37f)) * (0.06f * rumble);
            pour.gameObject.SetActive(flow > 0f || rumble > 0.4f);
            pour.localScale = new Vector3(flow > 0f ? 1f : 0.25f, 1f, 1f);
            var reach = slide.Reach * flow;
            sheet.gameObject.SetActive(flow > 0f);
            sheet.localScale = new Vector3(1f, 1f, Mathf.Max(0.01f, reach));
            var along = slide.At.Tangent;
            foreach (var (lump, a, o) in lumps)
            {
                var out_ = Mathf.Repeat(o + t * 0.5f, 1f) * reach;
                lump.gameObject.SetActive(flow > 0f);
                lump.position = SimSpace.ToWorld(edge + into * out_ + along * a, 0.12f);
            }
        }
    }

    /// <summary>Flat slabs of ice riding the current, rising at the top of their run and sinking at the bottom.</summary>
    sealed class IceFloesView : ObstacleView
    {
        readonly IceFloes floes;
        readonly Transform[] slabs = new Transform[IceFloes.Count];

        public IceFloesView(IceFloes floes, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Ice floes"), palette)
        {
            this.floes = floes;
            var ice = Tint(palette.eyeWhite, new Color(0.84f, 0.92f, 0.97f));
            var blue = Tint(palette.eyeWhite, new Color(0.6f, 0.77f, 0.9f));
            for (var i = 0; i < IceFloes.Count; i++)
            {
                slabs[i] = Shapes3D.Group(Root, $"Floe {i}");
                Part(PrimitiveType.Cube, slabs[i], new Vector3(0f, 0f, 0f), new Vector3(IceFloes.Width, 0.5f, IceFloes.Length), blue, name: "Slab");
                Part(PrimitiveType.Cube, slabs[i], new Vector3(0f, 0.2f, 0f), new Vector3(IceFloes.Width - 0.2f, 0.25f, IceFloes.Length - 0.25f), ice, name: "Top");
                Part(PrimitiveType.Cube, slabs[i], new Vector3(0.35f * (i == 0 ? 1f : -1f), 0.4f, 0.4f), new Vector3(1.1f, 0.3f, 1f), ice, Quaternion.Euler(4f, 28f, 3f), "Ridge");
                Part(PrimitiveType.Cube, slabs[i], new Vector3(0f, -0.15f, 0f), new Vector3(IceFloes.Width + 0.45f, 0.08f, IceFloes.Length + 0.45f), palette.foam, name: "Foam");
            }
        }

        public override void Pose(double seconds)
        {
            for (var i = 0; i < IceFloes.Count; i++)
            {
                var (position, angle, up) = floes.Floe(i, seconds);
                var heading = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var bob = 0.04f * Mathf.Sin((float)seconds * 2f + i * 2f);
                slabs[i].gameObject.SetActive(up > 0.01f);
                slabs[i].SetPositionAndRotation(SimSpace.ToWorld(position, Mathf.Lerp(-0.8f, 0.1f, up) + bob), Facing(heading));
            }
        }
    }

    /// <summary>A long mound of sticks and mud across the river with plugs the beavers lift out of one gap at a time.</summary>
    sealed class BeaverDamView : ObstacleView
    {
        readonly BeaverDam dam;
        readonly Transform[] plugs;

        public BeaverDamView(BeaverDam dam, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Beaver dam"), palette)
        {
            this.dam = dam;
            var at = dam.At;
            var across = Quaternion.LookRotation(SimSpace.Direction(at.Tangent)); // local x runs across the river
            var sticks = Tint(palette.wood, new Color(0.45f, 0.32f, 0.2f));
            var mud = Tint(palette.rock, new Color(0.34f, 0.26f, 0.18f));
            var random = new System.Random(Mathf.RoundToInt(at.Distance * 3f));
            var half = at.HalfWidth;
            var w = dam.GapWidth;

            var edges = new List<float> { -half - 1.5f };
            foreach (var gap in dam.Gaps)
            {
                edges.Add(gap - w * 0.5f);
                edges.Add(gap + w * 0.5f);
            }

            edges.Add(half + 1.5f);
            for (var i = 0; i < edges.Count; i += 2)
                Mound(Root, edges[i], edges[i + 1], 0.75f, at, across, mud, sticks, random);

            // Wing walls of sticks along both lips, just upstream, that keep a raft sliding along the dam from slipping over the edge.
            foreach (var side in new[] { -1f, 1f })
            {
                var wing = at.Centre + at.Left * (side * (half + 0.5f)) - at.Tangent * (BeaverDam.WingLength * 0.5f);
                var along = Quaternion.LookRotation(SimSpace.Direction(at.Tangent));
                Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(wing, 0.25f), new Vector3(1.3f, 0.75f, BeaverDam.WingLength), mud, along, "Wing");
                for (var k = 0; k < 12; k++)
                {
                    var place = wing + at.Tangent * (((float)random.NextDouble() - 0.5f) * BeaverDam.WingLength) + at.Left * (((float)random.NextDouble() - 0.5f) * 1f);
                    Part(PrimitiveType.Cylinder, Root, SimSpace.ToWorld(place, 0.55f + (float)random.NextDouble() * 0.3f), new Vector3(0.1f, 0.5f + (float)random.NextDouble() * 0.5f, 0.1f),
                        sticks, along * Quaternion.Euler(90f + (float)random.NextDouble() * 30f - 15f, (float)random.NextDouble() * 50f - 25f, 0f), "Stick");
                }
            }

            plugs = new Transform[dam.Gaps.Count];
            for (var i = 0; i < plugs.Length; i++)
            {
                plugs[i] = Shapes3D.Group(Root, $"Plug {i}");
                Mound(plugs[i], dam.Gaps[i] - w * 0.5f + 0.05f, dam.Gaps[i] + w * 0.5f - 0.05f, 0.6f, at, across, mud, sticks, random);
            }

            // A beaver or two sitting on the dam, keeping an eye on things.
            var fur = Tint(palette.animal, new Color(0.4f, 0.27f, 0.16f));
            var tail = Tint(palette.animal, new Color(0.2f, 0.15f, 0.12f));
            for (var i = 0; i + 1 < edges.Count && i < 4; i += 2)
            {
                var spot = (edges[i] + edges[i + 1]) * 0.5f;
                if (i == 0) spot = edges[1] - 0.8f;
                var beaver = Shapes3D.Group(Root, "Beaver");
                beaver.SetPositionAndRotation(SimSpace.ToWorld(at.Centre + at.Left * spot, 0.8f), Quaternion.LookRotation(SimSpace.Direction(-at.Tangent)));
                Part(PrimitiveType.Capsule, beaver, new Vector3(0f, 0.35f, 0f), new Vector3(0.55f, 0.4f, 0.5f), fur, name: "Body");
                Part(PrimitiveType.Sphere, beaver, new Vector3(0f, 0.8f, 0.12f), new Vector3(0.4f, 0.36f, 0.4f), fur, name: "Head");
                Part(PrimitiveType.Cube, beaver, new Vector3(0f, 0.68f, 0.33f), new Vector3(0.12f, 0.1f, 0.05f), palette.eyeWhite, name: "Teeth");
                Part(PrimitiveType.Cube, beaver, new Vector3(0f, 0.05f, -0.5f), new Vector3(0.35f, 0.08f, 0.6f), tail, Quaternion.Euler(-15f, 0f, 0f), "Tail");
            }
        }

        static void Mound(Transform parent, float from, float to, float height, RiverSample at, Quaternion across, Material mud, Material sticks, System.Random random)
        {
            var width = to - from;
            var middle = at.Centre + at.Left * ((from + to) * 0.5f);
            Shapes3D.Primitive(PrimitiveType.Cube, parent, SimSpace.ToWorld(middle, height * 0.35f), new Vector3(width, height, BeaverDam.Thickness + 0.5f), mud, across, "Mud");
            for (var k = 0; k < Mathf.CeilToInt(width * 2.2f); k++)
            {
                var place = at.Centre + at.Left * (from + (float)random.NextDouble() * width) + at.Tangent * (((float)random.NextDouble() - 0.5f) * 0.9f);
                Shapes3D.Primitive(PrimitiveType.Cylinder, parent, SimSpace.ToWorld(place, height * (0.55f + (float)random.NextDouble() * 0.4f)),
                    new Vector3(0.1f, 0.5f + (float)random.NextDouble() * 0.5f, 0.1f), sticks,
                    across * Quaternion.Euler((float)random.NextDouble() * 50f - 25f, (float)random.NextDouble() * 60f - 30f, 90f + (float)random.NextDouble() * 30f - 15f), "Stick");
            }
        }

        public override void Pose(double seconds)
        {
            var open = dam.OpenGap(seconds, out var closing);
            for (var i = 0; i < plugs.Length; i++)
            {
                // The open gap's plug drops out of sight as its turn starts and comes back up as it ends.
                var down = i != open ? 0f : closing < 0.08f ? closing / 0.08f : closing > 0.9f ? 1f - (closing - 0.9f) / 0.1f : 1f;
                plugs[i].localPosition = Vector3.down * (1.3f * down);
                plugs[i].gameObject.SetActive(down < 0.99f);
            }
        }
    }

    /// <summary>A pelican circling high over the river: its shadow grows on the water, then it folds its wings and plunges.</summary>
    sealed class PelicanView : ObstacleView
    {
        readonly PelicanDive pelican;
        readonly Transform bird;
        readonly Transform[] wings = new Transform[2];
        readonly Transform shadow;
        readonly Transform splash;
        readonly Material dark;

        public PelicanView(PelicanDive pelican, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Pelican"), palette)
        {
            this.pelican = pelican;
            var white = Tint(palette.eyeWhite, new Color(0.95f, 0.95f, 0.92f));
            var grey = Tint(palette.eyeWhite, new Color(0.45f, 0.45f, 0.47f));
            var beak = Tint(palette.sign, new Color(0.95f, 0.65f, 0.22f));
            bird = Shapes3D.Group(Root, "Bird");
            Part(PrimitiveType.Capsule, bird, Vector3.zero, new Vector3(0.65f, 0.6f, 0.65f), white, Quaternion.Euler(90f, 0f, 0f), "Body");
            Part(PrimitiveType.Sphere, bird, new Vector3(0f, 0.3f, 0.75f), new Vector3(0.38f, 0.38f, 0.42f), white, name: "Head");
            Part(PrimitiveType.Cube, bird, new Vector3(0f, 0.24f, 1.3f), new Vector3(0.15f, 0.1f, 0.95f), beak, name: "Beak");
            Part(PrimitiveType.Sphere, bird, new Vector3(0f, 0.1f, 1.2f), new Vector3(0.17f, 0.22f, 0.65f), beak, name: "Pouch");
            foreach (var x in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, bird, new Vector3(0.14f * x, 0.4f, 0.88f), Vector3.one * 0.08f, palette.eyeDark, name: "Eye");
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                wings[i] = Shapes3D.Group(bird, "Wing", new Vector3(0.28f * side, 0.1f, 0.05f));
                Part(PrimitiveType.Cube, wings[i], new Vector3(1.05f * side, 0f, 0f), new Vector3(2.1f, 0.06f, 0.65f), white, name: "Feathers");
                Part(PrimitiveType.Cube, wings[i], new Vector3(2.0f * side, 0f, -0.05f), new Vector3(0.5f, 0.05f, 0.55f), grey, name: "Tip");
            }

            dark = new Material(palette.foam) { name = "Pelican shadow", color = new Color(0f, 0f, 0f, 0f) };
            shadow = Shapes3D.MeshObject("Shadow", Shapes3D.Disc(PelicanDive.Splash, 28), Root, Vector3.zero, dark).transform;
            Shapes3D.MeshObject("Target", Shapes3D.Torus(PelicanDive.Splash, 0.09f, 40, 8), shadow, new Vector3(0f, 0.03f, 0f),
                Tint(palette.sign, new Color(1f, 0.25f, 0.15f)));
            splash = Shapes3D.Group(Root, "Splash");
            Shapes3D.MeshObject("Ring", Shapes3D.Disc(PelicanDive.Splash, 24), splash, new Vector3(0f, 0.05f, 0f), palette.foam);
            Part(PrimitiveType.Sphere, splash, new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 1.2f, 1.6f), palette.Spray(new Color(1f, 1f, 1f, 0.75f), 0.5f, 0f, 1.5f), name: "Plume");
        }

        public override void Pose(double seconds)
        {
            var at = pelican.At;
            var t = (float)seconds;
            var phase = pelican.Circuit(seconds);
            var (target, shade, plunge) = pelican.Pose(seconds);
            var round = new Vector2(Mathf.Cos(t * 0.9f), Mathf.Sin(t * 0.9f));
            var high = SimSpace.ToWorld(at.Centre + round * (at.HalfWidth * 0.9f), 10f);
            var water = SimSpace.ToWorld(target, 0.35f);
            var rising = phase >= PelicanDive.Done && phase < PelicanDive.Done + 0.15f ? (phase - PelicanDive.Done) / 0.15f : -1f;

            Vector3 position;
            Quaternion facing;
            var folded = 0f;
            if (plunge > 0f)
            {
                position = Vector3.Lerp(high, water, plunge * plunge);
                facing = Quaternion.LookRotation((water - high).normalized);
                folded = 1f;
            }
            else if (rising >= 0f)
            {
                position = Vector3.Lerp(water, high, Smooth(rising));
                facing = Quaternion.LookRotation((high - water).normalized + Vector3.up * 0.3f);
            }
            else
            {
                position = high;
                facing = Quaternion.LookRotation(SimSpace.Direction(new Vector2(-round.y, round.x))) * Quaternion.Euler(0f, 0f, -15f); // banking round the circle
            }

            bird.SetPositionAndRotation(position, facing);
            var flap = folded > 0f ? 0f : 28f * Mathf.Sin(t * (rising >= 0f ? 9f : 4f));
            for (var i = 0; i < 2; i++)
            {
                var side = i == 0 ? -1f : 1f;
                wings[i].localRotation = Quaternion.Euler(0f, folded * 55f * side, side * (flap + folded * 60f));
            }

            shadow.gameObject.SetActive(shade > 0f);
            shadow.position = SimSpace.ToWorld(target, 0.04f);
            shadow.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, shade);
            dark.color = new Color(0f, 0f, 0f, 0.45f * shade);
            var splashing = phase >= PelicanDive.Dive && phase < PelicanDive.Dive + 0.07f ? (phase - PelicanDive.Dive) / 0.07f : -1f;
            splash.gameObject.SetActive(splashing >= 0f);
            splash.position = SimSpace.ToWorld(target);
            splash.localScale = Vector3.one * (0.5f + splashing);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }

    /// <summary>A little fishing boat crossing to and fro, a line of corks and a net streaming out behind it.</summary>
    sealed class NetBoatView : ObstacleView
    {
        readonly NetBoat boat;
        readonly Transform hull;
        readonly Transform net;
        readonly Transform[] corks = new Transform[10];

        public NetBoatView(NetBoat boat, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Net boat"), palette)
        {
            this.boat = boat;
            var paint = Tint(palette.Planks, new Color(0.72f, 0.3f, 0.2f));
            var trim = Tint(palette.Planks, new Color(0.9f, 0.86f, 0.72f));
            var coat = Tint(palette.animal, new Color(0.95f, 0.75f, 0.15f));
            hull = Shapes3D.Group(Root, "Boat");
            Part(PrimitiveType.Cube, hull, new Vector3(0f, 0.3f, -0.15f), new Vector3(NetBoat.BoatWidth, 0.6f, NetBoat.BoatLength - 0.3f), paint, name: "Hull");
            Part(PrimitiveType.Cube, hull, new Vector3(0f, 0.3f, NetBoat.BoatLength * 0.5f - 0.45f), new Vector3(NetBoat.BoatWidth * 0.7f, 0.58f, NetBoat.BoatWidth * 0.7f), paint,
                Quaternion.Euler(0f, 45f, 0f), "Bow");
            Part(PrimitiveType.Cube, hull, new Vector3(0f, 0.62f, -0.15f), new Vector3(NetBoat.BoatWidth + 0.06f, 0.06f, NetBoat.BoatLength - 0.3f), trim, name: "Gunwale");
            Part(PrimitiveType.Cube, hull, new Vector3(0f, 0.95f, -0.55f), new Vector3(0.85f, 0.6f, 0.75f), trim, name: "Cabin");
            Part(PrimitiveType.Capsule, hull, new Vector3(0f, 1f, 0.35f), new Vector3(0.4f, 0.4f, 0.4f), coat, name: "Fisher");
            Part(PrimitiveType.Sphere, hull, new Vector3(0f, 1.5f, 0.35f), Vector3.one * 0.3f, Tint(palette.animal, new Color(0.85f, 0.65f, 0.5f)), name: "Head");
            Part(PrimitiveType.Cylinder, hull, new Vector3(0f, 1.66f, 0.35f), new Vector3(0.42f, 0.04f, 0.42f), coat, name: "Hat");
            Part(PrimitiveType.Cube, hull, new Vector3(0f, 0.05f, 0f), new Vector3(NetBoat.BoatWidth + 0.5f, 0.04f, NetBoat.BoatLength + 0.4f), palette.foam, name: "Wash");

            net = Part(PrimitiveType.Cube, Root, Vector3.zero, Vector3.one, Palette.Tinted(palette.foam, new Color(0.25f, 0.3f, 0.3f, 0.45f)), name: "Net").transform;
            var cork = Tint(palette.sign, new Color(0.95f, 0.72f, 0.3f));
            for (var i = 0; i < corks.Length; i++)
                corks[i] = Part(PrimitiveType.Sphere, Root, Vector3.zero, new Vector3(0.22f, 0.16f, 0.22f), cork, name: "Cork").transform;
        }

        public override void Pose(double seconds)
        {
            var at = boat.At;
            var t = (float)seconds;
            var swim = Mathf.Clamp(boat.Speed(seconds) / 1.2f, -1f, 1f);
            var heading = at.Left * swim + at.Tangent * (1f - Mathf.Abs(swim)) * 0.5f;
            hull.SetPositionAndRotation(SimSpace.ToWorld(boat.Position(seconds), 0.04f * Mathf.Sin(t * 2.4f)),
                Facing(heading) * Quaternion.Euler(2f * Mathf.Sin(t * 1.9f), 0f, 3f * Mathf.Sin(t * 1.5f)));
            var (near, far) = boat.Net(seconds);
            var length = Mathf.Abs(far - near);
            net.gameObject.SetActive(length > 0.2f);
            net.SetPositionAndRotation(SimSpace.ToWorld(at.Centre + at.Left * ((near + far) * 0.5f), 0.02f), Quaternion.LookRotation(SimSpace.Direction(at.Tangent)));
            net.localScale = new Vector3(length, 0.02f, NetBoat.NetDepth);
            for (var i = 0; i < corks.Length; i++)
            {
                var across = Mathf.Lerp(near, far, i / (corks.Length - 1f));
                corks[i].position = SimSpace.ToWorld(at.Centre + at.Left * across - at.Tangent * (NetBoat.NetDepth * 0.5f), 0.06f + 0.03f * Mathf.Sin(t * 3f + i));
            }
        }
    }
}
