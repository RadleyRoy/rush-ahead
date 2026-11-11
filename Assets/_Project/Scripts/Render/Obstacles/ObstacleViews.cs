using System.Collections.Generic;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>
    /// How one of the newer obstacles looks, posed every frame from the race clock (so it's exactly where the host's
    /// physics has it, on every device). <see cref="For"/> builds the right one for each kind.
    /// </summary>
    public abstract class ObstacleView
    {
        protected ObstacleView(Transform root, ArtPalette palette)
        {
            Root = root;
            Palette = palette;
        }

        protected Transform Root { get; }
        protected ArtPalette Palette { get; }

        public abstract void Pose(double seconds);

        public static ObstacleView For(Obstacle obstacle, ArtPalette palette, Transform parent) => obstacle switch
        {
            Rockslide o => new RockslideView(o, palette, parent),
            Hippo o => new HippoView(o, palette, parent),
            Sluice o => new SluiceView(o, palette, parent),
            Sweeper o => new SweeperView(o, palette, parent),
            Cannon o => new CannonView(o, palette, parent),
            Rapids o => new RapidsView(o, palette, parent),
            Weir o => new WeirView(o, palette, parent),
            Bumper o => new BumperView(o, palette, parent),
            Ferry o => new FerryView(o, palette, parent),
            Bear o => new BearView(o, palette, parent),
            FallingTree o => new FallingTreeView(o, palette, parent),
            Turtle o => new TurtleView(o, palette, parent),
            PiranhaShoal o => new PiranhaShoalView(o, palette, parent),
            Waterspout o => new WaterspoutView(o, palette, parent),
            ElectricEels o => new ElectricEelsView(o, palette, parent),
            Mudslide o => new MudslideView(o, palette, parent),
            IceFloes o => new IceFloesView(o, palette, parent),
            BeaverDam o => new BeaverDamView(o, palette, parent),
            PelicanDive o => new PelicanView(o, palette, parent),
            NetBoat o => new NetBoatView(o, palette, parent),
            Kraken o => new KrakenView(o, palette, parent),
            TidalBore o => new TidalBoreView(o, palette, parent),
            JumpRamp o => new JumpRampView(o, palette, parent),
            GooseGang o => new GooseGangView(o, palette, parent),
            ChainedMines o => new ChainedMinesView(o, palette, parent),
            WreckingBall o => new WreckingBallView(o, palette, parent),
            StormCloud o => new StormCloudView(o, palette, parent),
            PaddleSteamer o => new PaddleSteamerView(o, palette, parent),
            MillRace o => new MillRaceView(o, palette, parent),
            Drawbridge o => new DrawbridgeView(o, palette, parent),
            CargoCrane o => new CargoCraneView(o, palette, parent),
            LogFlume o => new LogFlumeView(o, palette, parent),
            CalvingGlacier o => new CalvingGlacierView(o, palette, parent),
            DynamiteFloat o => new DynamiteFloatView(o, palette, parent),
            LeapingFrogs o => new LeapingFrogsView(o, palette, parent),
            GiantFan o => new GiantFanView(o, palette, parent),
            _ => null,
        };

        protected Material Tint(Material source, Color colour) => Palette.Tinted(source, colour);

        protected static GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material material, Quaternion? turn = null, string name = null) =>
            Shapes3D.Primitive(type, parent, at, size, material, turn, name);

        /// <summary>A world rotation facing a direction on the water.</summary>
        protected static Quaternion Facing(Vector2 direction) => Quaternion.LookRotation(SimSpace.Direction(direction));

        /// <summary>
        /// A bluff on one bank (<paramref name="side"/> +1 the left), built across the river's line: rising from just under
        /// the water at the bank to <paramref name="height"/> at <paramref name="peakOut"/> metres out, then dropping away into
        /// the gorge; <paramref name="halfAlong"/> either side of the spot, its ends sloping down. Lumpy with noise.
        /// </summary>
        protected static Mesh Bluff(RiverSample at, float side, float halfAlong, float height, float peakOut, System.Random random)
        {
            var bank = at.HalfWidth;
            var outs = new[] { bank - 0.5f, bank + 0.2f, bank + peakOut * 0.35f, bank + peakOut * 0.7f, bank + peakOut, bank + peakOut + 2.5f, bank + peakOut + 5f, bank + peakOut + 6f };
            var rise = new[] { -0.8f, 0.05f, 0.45f, 0.82f, 1f, 0.92f, 0.5f, -4f };
            const int rows = 9;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var a = 0; a < rows; a++)
            {
                var v = a / (rows - 1f) * 2f - 1f; // -1 upstream end, +1 downstream end
                var along = v * (halfAlong + 3f);
                var shoulder = Mathf.Pow(Mathf.Max(0f, 1f - v * v), 0.45f);
                for (var o = 0; o < outs.Length; o++)
                {
                    var h = rise[o] > 0f ? height * rise[o] * shoulder : rise[o] * height;
                    if (o > 0 && o < outs.Length - 1 && a > 0 && a < rows - 1) h += ((float)random.NextDouble() - 0.5f) * 0.5f;
                    if (rise[o] > 0f && shoulder < 0.2f) h = Mathf.Min(h, -0.3f);
                    vertices.Add(SimSpace.ToWorld(at.Centre + at.Left * (side * outs[o]) + at.Tangent * along, h));
                }
            }

            var n = outs.Length;
            for (var a = 0; a < rows - 1; a++)
            for (var o = 0; o < n - 1; o++)
            {
                var i = a * n + o;
                if (side > 0f) triangles.AddRange(new[] { i, i + 1, i + n, i + 1, i + n + 1, i + n });
                else triangles.AddRange(new[] { i, i + n, i + 1, i + 1, i + n, i + n + 1 });
            }

            var mesh = new Mesh { name = "Bluff" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A surface of revolution about the local up axis: a ring of <paramref name="radii"/>[i] at
        /// <paramref name="heights"/>[i], each ring's middle shifted sideways by <paramref name="sway"/>[i] (x) so a
        /// column can snake. Open at both ends.
        /// </summary>
        protected static Mesh Lathe(float[] radii, float[] heights, float[] sway = null, int segments = 28)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var ring = 0; ring < radii.Length; ring++)
            {
                var shift = sway != null ? sway[ring] : 0f;
                for (var s = 0; s <= segments; s++)
                {
                    var a = s * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(shift + Mathf.Cos(a) * radii[ring], heights[ring], Mathf.Sin(a) * radii[ring]));
                }
            }

            var row = segments + 1;
            for (var ring = 0; ring < radii.Length - 1; ring++)
            for (var s = 0; s < segments; s++)
            {
                var i = ring * row + s;
                triangles.AddRange(new[] { i, i + row, i + 1, i + 1, i + row, i + row + 1 });
            }

            var mesh = new Mesh { name = "Lathe" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>A pile of rocks on one side with a boulder that teeters, rolls down, tumbles across and drops away.</summary>
    sealed class RockslideView : ObstacleView
    {
        readonly Rockslide slide;
        readonly Transform boulder;
        readonly Vector3 axis;

        public RockslideView(Rockslide slide, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Rockslide"), palette)
        {
            this.slide = slide;
            var at = slide.At;
            var side = slide.Spot.side;
            var radius = slide.Spot.radius;
            var random = new System.Random(Mathf.RoundToInt(slide.Spot.distance * 10f));
            var stone = palette.cliff;

            // The crag: a rock mass standing out of the gorge beside the river, a chute sloping from its edge down to the
            // water where the boulders come down, flat on top where the pile waits.
            Shapes3D.MeshObject("Crag", Crag(at, side, radius, random), Root, Vector3.zero, stone);
            if (palette.HasRocks)
                for (var i = 0; i < 7; i++)
                {
                    // Rough stones on the top and down the crag's flanks (never in the chute).
                    var pick = random.Next(palette.rockMeshes.Length);
                    var along = (i % 2 == 0 ? 1f : -1f) * (radius + 1.4f + (float)random.NextDouble() * 2.2f);
                    var out_ = at.HalfWidth + 1.2f + (float)random.NextDouble() * (Rockslide.CragEdge + 4f);
                    var size = 0.9f + (float)random.NextDouble() * 1.3f;
                    var height = Mathf.Lerp(0f, Rockslide.CragHeight, Mathf.InverseLerp(at.HalfWidth + 0.3f, at.HalfWidth + Rockslide.CragEdge, out_));
                    Shapes3D.Model("Stone", palette.rockMeshes[pick], palette.rockMaterials[pick], Root,
                        SimSpace.ToWorld(at.Centre + at.Left * (side * out_) + at.Tangent * along, height - 0.25f), size, (float)random.NextDouble() * 360f);
                }

            boulder = Boulder(Root, "Boulder", palette, radius);
            // The pile the next ones come from, out behind the edge.
            for (var i = 0; i < 3; i++)
            {
                var spot = at.Centre + at.Left * (side * (at.HalfWidth + Rockslide.CragEdge + Rockslide.PileOut + 1.6f + i * 0.9f)) +
                           at.Tangent * ((i - 1) * radius * 1.7f);
                var waiting = Boulder(Root, "Waiting boulder", palette, radius * (0.85f + 0.1f * i));
                waiting.SetPositionAndRotation(SimSpace.ToWorld(spot, Rockslide.CragHeight + radius * 0.9f), Quaternion.Euler(i * 70f, i * 130f, 0f));
            }

            axis = SimSpace.Direction(at.Tangent) * side; // rolling across the river turns it about the river's line
        }

        static Transform Boulder(Transform parent, string name, ArtPalette palette, float radius)
        {
            var boulder = Shapes3D.Group(parent, name);
            if (palette.HasRocks)
            {
                // Centred on the group, so it spins about its middle as it rolls.
                // The roundest scan, sized so it reaches the boulder's radius every way it rolls.
                var pick = CourseDressing.RoundestRock(palette);
                var rock = Shapes3D.Model("Rock", palette.rockMeshes[pick], palette.rockMaterials[pick], boulder, Vector3.zero,
                    radius * 1.02f / palette.RockReach(pick).z);
                rock.transform.localPosition -= Vector3.up * (palette.rockMeshes[pick].bounds.size.y * rock.transform.localScale.y * 0.5f); // its middle on the pivot
            }
            else Part(PrimitiveType.Sphere, boulder, Vector3.zero, Vector3.one * (radius * 2f), palette.Tinted(palette.rock, new Color(0.47f, 0.44f, 0.4f)), name: "Rock");

            return boulder;
        }

        /// <summary>
        /// The crag's rock, built across the river's line: from the bank out, a chute rising from the water to the edge
        /// (as wide as the boulders, with shoulders either side), a flat top, and a face dropping into the gorge.
        /// </summary>
        static Mesh Crag(RiverSample at, float side, float radius, System.Random random)
        {
            var bank = at.HalfWidth;
            // The section across, as (metres out from the middle, height): bank, chute, edge, top, back, deep.
            var outs = new[] { bank - 0.4f, bank + 0.3f, bank + Rockslide.CragEdge, bank + Rockslide.CragEdge + 2.5f, bank + 11f, bank + 12f, bank + 10f };
            var heights = new[] { -1.5f, 0f, Rockslide.CragHeight, Rockslide.CragHeight + 0.2f, Rockslide.CragHeight + 0.6f, -6f, -40f };
            // Along the river: the chute in the middle, shoulders standing higher either side, the ends falling away.
            var alongs = new[] { -radius - 5f, -radius - 2.5f, -radius - 0.6f, -radius + 0.1f, radius - 0.1f, radius + 0.6f, radius + 2.5f, radius + 5f };
            var lift = new[] { -3.5f, 0.8f, 1.3f, 0f, 0f, 1.3f, 0.8f, -3.5f };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var a = 0; a < alongs.Length; a++)
            for (var o = 0; o < outs.Length; o++)
            {
                var h = heights[o];
                if (o >= 1 && o <= 4) h += lift[a] * (o == 1 ? 0.5f : 1f) + (a is > 0 and < 7 && (a < 3 || a > 4) ? ((float)random.NextDouble() - 0.5f) * 0.6f : 0f);
                if (o == 0) h = Mathf.Min(h, lift[a] * 0.3f - 1.2f);
                var outward = outs[o] + (a is 0 or 7 ? -0.5f : 0f);
                vertices.Add(SimSpace.ToWorld(at.Centre + at.Left * (side * outward) + at.Tangent * alongs[a], h));
            }

            var n = outs.Length;
            for (var a = 0; a < alongs.Length - 1; a++)
            for (var o = 0; o < n - 1; o++)
            {
                var i = a * n + o;
                // Clockwise seen from above (Unity's front faces), whichever bank it's on.
                if (side > 0f) triangles.AddRange(new[] { i, i + 1, i + n, i + 1, i + n + 1, i + n });
                else triangles.AddRange(new[] { i, i + n, i + 1, i + 1, i + n, i + n + 1 });
            }

            var mesh = new Mesh { name = "Crag" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        bool wasCrossing;

        public override void Pose(double seconds)
        {
            var (position, height, rolled, crossing, teeter) = slide.Boulder(seconds);
            var wobble = teeter * Mathf.Sin((float)seconds * 22f) * 0.15f;
            boulder.position = SimSpace.ToWorld(position, slide.Spot.radius + height) + SimSpace.Direction(slide.At.Left) * wobble;
            boulder.rotation = Quaternion.AngleAxis(rolled / slide.Spot.radius * Mathf.Rad2Deg, axis);
            boulder.gameObject.SetActive(height > -30f);
            if (crossing && !wasCrossing) Splashes.Splash(SimSpace.ToWorld(position, 0.2f), 2.4f, Palette); // into the water it comes
            wasCrossing = crossing;
        }
    }

    /// <summary>Rising bubbles, then a hippo's broad back and head heaving up out of the water.</summary>
    sealed class HippoView : ObstacleView
    {
        readonly Hippo hippo;
        readonly Transform body;
        readonly Transform bubbles;

        public HippoView(Hippo hippo, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Hippo", SimSpace.ToWorld(hippo.Position)), palette)
        {
            this.hippo = hippo;
            Root.rotation = Facing(new Vector2(Mathf.Cos(hippo.Heading * Mathf.Deg2Rad), Mathf.Sin(hippo.Heading * Mathf.Deg2Rad)));
            var skin = Tint(palette.animal, new Color(0.45f, 0.38f, 0.42f));
            var pink = Tint(palette.animal, new Color(0.85f, 0.55f, 0.55f));
            body = Shapes3D.Group(Root, "Body");
            if (palette.HasCreature(Creature.Hippo))
            {
                Shapes3D.MeshObject("Hippo", palette.CreatureMesh(Creature.Hippo), body, Vector3.zero,
                    palette.Hide(new Color(0.42f, 0.35f, 0.38f), new Color(0.78f, 0.55f, 0.55f), new Color(0.12f, 0.09f, 0.09f), new Color(0.8f, 0.5f, 0.5f)));
                foreach (var x in new[] { -1f, 1f })
                {
                    Part(PrimitiveType.Sphere, body, new Vector3(0.45f * x, 0.8f, 1.3f), Vector3.one * 0.2f, palette.eyeWhite, name: "Eye");
                    Part(PrimitiveType.Sphere, body, new Vector3(0.46f * x, 0.82f, 1.39f), Vector3.one * 0.1f, palette.eyeDark, name: "Pupil");
                }
            }
            else BlockHippo(skin, pink, palette);

            bubbles = Shapes3D.Group(Root, "Bubbles");

            for (var i = 0; i < 10; i++)
            {
                var angle = i * 2.4f;
                var r = 0.3f + (i % 4) * 0.35f;
                Part(PrimitiveType.Sphere, bubbles, new Vector3(Mathf.Cos(angle) * r, 0.05f, Mathf.Sin(angle) * r), Vector3.one * (0.25f + (i % 3) * 0.1f), palette.foam, name: "Bubble");
            }

            Shapes3D.MeshObject("Ring", Shapes3D.Disc(Hippo.Radius + 0.4f, 24), Root, new Vector3(0f, 0.02f, 0f), palette.foam);
        }

        /// <summary>The old block hippo, for when there's no sculpted one.</summary>
        void BlockHippo(Material skin, Material pink, ArtPalette palette)
        {
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0f, -0.3f), new Vector3(2.4f, 1.5f, 3.2f), skin, name: "Back");
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.25f, 1.4f), new Vector3(1.6f, 1.1f, 1.5f), skin, name: "Head");
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.05f, 2.05f), new Vector3(1.5f, 0.8f, 0.9f), pink, name: "Snout");
            foreach (var x in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, body, new Vector3(0.45f * x, 0.75f, 1.2f), Vector3.one * 0.32f, palette.eyeWhite, name: "Eye");
                Part(PrimitiveType.Sphere, body, new Vector3(0.47f * x, 0.78f, 1.33f), Vector3.one * 0.15f, palette.eyeDark, name: "Pupil");
                Part(PrimitiveType.Sphere, body, new Vector3(0.6f * x, 0.85f, 0.85f), new Vector3(0.25f, 0.3f, 0.12f), skin, name: "Ear");
                Part(PrimitiveType.Sphere, body, new Vector3(0.3f * x, 0.4f, 2.4f), Vector3.one * 0.14f, palette.eyeDark, name: "Nostril");
            }
        }

        public override void Pose(double seconds)
        {
            var (rise, bubbling) = hippo.Pose(seconds);
            body.localPosition = new Vector3(0f, Mathf.Lerp(-2.2f, 0.25f, rise), 0f);
            body.gameObject.SetActive(rise > 0.01f);
            bubbles.gameObject.SetActive(bubbling > 0f);
            bubbles.localPosition = new Vector3(0f, 0.05f + 0.1f * bubbling * Mathf.Abs(Mathf.Sin((float)seconds * 20f)), 0f);
            bubbles.localScale = Vector3.one * (0.6f + 0.6f * bubbling);
        }
    }

    /// <summary>Two timber boards across the river on a gantry, sliding to move the gap between them.</summary>
    sealed class SluiceView : ObstacleView
    {
        readonly Sluice sluice;
        readonly Transform left;
        readonly Transform right;

        public SluiceView(Sluice sluice, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Sluice gate"), palette)
        {
            this.sluice = sluice;
            var at = sluice.At;
            var timber = palette.Planks;
            var dark = Tint(palette.Planks, new Color(0.32f, 0.3f, 0.28f));
            var across = Quaternion.LookRotation(SimSpace.Direction(at.Tangent)); // local x runs across the river
            left = Board(timber, dark, across, "Left board");
            right = Board(timber, dark, across, "Right board");

            // The gantry: posts on both lips and a beam high over the river that the boards hang from.
            foreach (var side in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, Root, SimSpace.ToWorld(at.Centre + at.Left * (side * (at.HalfWidth + 0.7f)), 2.4f), new Vector3(0.5f, 2.4f, 0.5f), dark, name: "Post");
            Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(at.Centre, 4.8f), new Vector3(2f * at.HalfWidth + 2f, 0.4f, 0.5f), dark, across, "Beam");
        }

        Transform Board(Material timber, Material dark, Quaternion across, string name)
        {
            var board = Shapes3D.Group(Root, name);
            board.rotation = across;
            Part(PrimitiveType.Cube, board, new Vector3(0f, 0.55f, 0f), new Vector3(sluice.BoardLength, 1.3f, Sluice.Thickness), timber, name: "Planks");
            for (var plank = 0; plank < 3; plank++)
                Part(PrimitiveType.Cube, board, new Vector3(0f, 0.15f + plank * 0.42f, -Sluice.Thickness * 0.5f - 0.01f), new Vector3(sluice.BoardLength, 0.05f, 0.02f), dark, name: "Seam");
            Part(PrimitiveType.Cube, board, new Vector3(0f, 0.12f, 0f), new Vector3(sluice.BoardLength, 0.3f, Sluice.Thickness + 0.5f), Palette.foam, name: "Foam");
            return board;
        }

        public override void Pose(double seconds)
        {
            var (l, r) = sluice.Boards(seconds);
            left.position = SimSpace.ToWorld(l);
            right.position = SimSpace.ToWorld(r);
        }
    }

    /// <summary>A post in the river with a long log spinning flat on top of the water.</summary>
    sealed class SweeperView : ObstacleView
    {
        readonly Sweeper sweeper;
        readonly Transform arm;

        public SweeperView(Sweeper sweeper, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Sweeper", SimSpace.ToWorld(sweeper.Pivot)), palette)
        {
            this.sweeper = sweeper;
            var bark = Tint(palette.wood, new Color(0.4f, 0.27f, 0.16f));
            Part(PrimitiveType.Cylinder, Root, new Vector3(0f, 0.9f, 0f), new Vector3(Sweeper.PostRadius * 2f, 0.9f, Sweeper.PostRadius * 2f), Tint(palette.rock, new Color(0.4f, 0.4f, 0.42f)), name: "Post");
            Shapes3D.MeshObject("Foam", Shapes3D.Disc(1.2f, 20), Root, new Vector3(0f, 0.02f, 0f), palette.foam);
            arm = Shapes3D.Group(Root, "Arm");
            Part(PrimitiveType.Cylinder, arm, new Vector3(0f, 0.35f, 0f), new Vector3(Sweeper.LogWidth, sweeper.Spot.length * 0.5f, Sweeper.LogWidth), bark, Quaternion.Euler(0f, 0f, 90f), "Log");
            Part(PrimitiveType.Cube, arm, new Vector3(0f, 0.03f, 0f), new Vector3(sweeper.Spot.length, 0.02f, Sweeper.LogWidth + 0.5f), palette.foam, name: "Wash");
        }

        public override void Pose(double seconds) => arm.localRotation = Quaternion.Euler(0f, -sweeper.AngleDeg(seconds), 0f);
    }

    /// <summary>A cannon on a stand on the lip, and its jet: a long white column of water reaching across the river.</summary>
    sealed class CannonView : ObstacleView
    {
        readonly Cannon cannon;
        readonly Transform barrel;
        readonly Transform jet;

        public CannonView(Cannon cannon, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Water cannon", SimSpace.ToWorld(cannon.Muzzle)), palette)
        {
            this.cannon = cannon;
            var metal = palette.Metal;
            var brass = Tint(palette.sign, new Color(0.8f, 0.6f, 0.25f));
            Part(PrimitiveType.Cube, Root, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 1f, 1.2f), palette.Planks, name: "Stand");
            barrel = Shapes3D.Group(Root, "Barrel", new Vector3(0f, 1.3f, 0f));
            Part(PrimitiveType.Cylinder, barrel, new Vector3(0f, 0f, 0.4f), new Vector3(0.55f, 0.7f, 0.55f), metal, Quaternion.Euler(90f, 0f, 0f), "Tube");
            Part(PrimitiveType.Cylinder, barrel, new Vector3(0f, 0f, 1.15f), new Vector3(0.38f, 0.08f, 0.38f), brass, Quaternion.Euler(90f, 0f, 0f), "Muzzle");
            jet = Shapes3D.Group(Root, "Jet", new Vector3(0f, 1.1f, 0f));
            Part(PrimitiveType.Cylinder, jet, new Vector3(0f, 0f, cannon.Reach * 0.5f), new Vector3(Cannon.JetRadius * 1.4f, cannon.Reach * 0.5f, Cannon.JetRadius * 1.4f),
                palette.Spray(new Color(0.85f, 0.95f, 1f, 0.8f), 0.8f, 0f, -1.5f), Quaternion.Euler(90f, 0f, 0f), "Water");
            Part(PrimitiveType.Sphere, jet, new Vector3(0f, -0.9f, cannon.Reach), new Vector3(2.4f, 0.8f, 2.4f), palette.Spray(new Color(1f, 1f, 1f, 0.75f), 0.5f, 0f, 1f),
                name: "Splash");
            // The jet's track on the water: a churned white stripe where it lands, so you can see where it's pointing.
            track = Shapes3D.Group(Root, "Track", new Vector3(0f, -SimSpace.ToWorld(cannon.Muzzle).y + 0.04f, 0f));
            Part(PrimitiveType.Cube, track, new Vector3(0f, 0f, cannon.Reach * 0.55f), new Vector3(Cannon.JetRadius * 2.2f, 0.02f, cannon.Reach * 0.9f),
                Tint(palette.foam, new Color(1f, 1f, 1f, 0.45f)), name: "Stripe");
            // A tank of water behind the gun, with a pipe to it.
            var tank = Tint(palette.Metal, new Color(0.35f, 0.42f, 0.5f));
            Part(PrimitiveType.Cylinder, Root, new Vector3(0f, 1.3f, -1.6f), new Vector3(1.5f, 1.3f, 1.5f), tank, name: "Tank");
            Part(PrimitiveType.Cylinder, Root, new Vector3(0f, 1.25f, -0.75f), new Vector3(0.22f, 0.5f, 0.22f), metal, Quaternion.Euler(90f, 0f, 0f), "Pipe");
        }

        readonly Transform track;

        public override void Pose(double seconds)
        {
            var aim = Facing(cannon.Jet(seconds));
            barrel.rotation = aim;
            jet.rotation = aim * Quaternion.Euler(4f, 0f, 0f); // the water droops as it goes
            track.rotation = aim;
            var flicker = 1f + 0.08f * Mathf.Sin((float)seconds * 37f);
            jet.localScale = new Vector3(flicker, flicker, 1f);
        }
    }

    /// <summary>White water: foam patches and standing waves across the river that heave up and down.</summary>
    sealed class RapidsView : ObstacleView
    {
        readonly List<(Transform wave, float phase)> waves = new();

        public RapidsView(Rapids rapids, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Rapids"), palette)
        {
            var river = rapids.Course.River;
            var random = new System.Random(Mathf.RoundToInt(rapids.Spot.from * 7f));
            var white = palette.Spray(new Color(1f, 1f, 1f, 0.9f), 0.5f, 0f, 0.6f, 0.5f);
            var boil = palette.Spray(new Color(0.62f, 0.85f, 0.92f, 0.75f), 0.6f, 0f, 0.4f, 0.6f);
            var froth = Tint(palette.foam, new Color(1f, 1f, 1f, 0.8f));
            var row = 0;
            for (var distance = rapids.Spot.from; distance < rapids.Spot.to; distance += 1.5f, row++)
            {
                var at = river.At(distance);
                var turn = Quaternion.LookRotation(SimSpace.Direction(at.Tangent));
                var count = 2 + row % 2;
                for (var k = 0; k < count; k++)
                {
                    // A standing wave: a short hump of green water with a white crest curling over, staggered row to row.
                    var slot = (k + 0.5f + (row % 2) * 0.5f) / (count + 0.5f) * 2f - 1f;
                    var across = (slot + ((float)random.NextDouble() - 0.5f) * 0.25f) * at.HalfWidth * 0.85f;
                    var width = at.HalfWidth * (0.3f + (float)random.NextDouble() * 0.25f);
                    var wave = Shapes3D.Group(Root, "Standing wave", SimSpace.ToWorld(at.Centre + at.Left * across, 0f));
                    wave.rotation = turn * Quaternion.Euler(0f, ((float)random.NextDouble() - 0.5f) * 30f, 0f);
                    Part(PrimitiveType.Capsule, wave, Vector3.zero, new Vector3(0.6f, width * 0.5f, 1.4f), boil, Quaternion.Euler(0f, 0f, 90f), "Hump");
                    Part(PrimitiveType.Capsule, wave, new Vector3(0f, 0.17f, 0.3f), new Vector3(0.26f, width * 0.42f, 0.5f), white, Quaternion.Euler(0f, 0f, 90f), "Crest");
                    waves.Add((wave, (float)random.NextDouble() * 6f));
                }

                Shapes3D.MeshObject("Foam", Shapes3D.Disc(1.2f + (float)random.NextDouble() * 1.4f, 14), Root,
                    SimSpace.ToWorld(at.Centre + at.Left * (((float)random.NextDouble() - 0.5f) * 1.6f * at.HalfWidth), 0.03f), froth);
            }
        }

        public override void Pose(double seconds)
        {
            foreach (var (wave, phase) in waves)
            {
                var p = wave.localPosition;
                wave.localPosition = new Vector3(p.x, -0.05f + 0.16f * Mathf.Sin((float)seconds * 5f + phase), p.z);
            }
        }
    }

    /// <summary>A line of stones just under the water right across the river, white water pouring over it.</summary>
    sealed class WeirView : ObstacleView
    {
        readonly Transform spray;

        public WeirView(Weir weir, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Weir"), palette)
        {
            var at = weir.At;
            var stone = Tint(palette.rock, new Color(0.52f, 0.5f, 0.46f));
            var turn = Quaternion.LookRotation(SimSpace.Direction(at.Tangent));
            for (var across = -at.HalfWidth; across <= at.HalfWidth + 0.01f; across += 1.1f)
                Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(at.Centre + at.Left * across, -0.05f), new Vector3(1.05f, 0.25f, 0.9f), stone,
                    turn * Quaternion.Euler(0f, across * 7f, 0f), "Stone");
            var white = palette.Spray(new Color(1f, 1f, 1f, 0.88f), 0.6f, 0f, 0.8f, 0.5f);
            // The lip the water pours over (a smooth glassy band), and the churning white water below it.
            Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(at.Centre - at.Tangent * 0.6f, 0.04f), new Vector3(2f * at.HalfWidth, 0.06f, 1.2f),
                palette.Spray(new Color(0.55f, 0.85f, 0.85f, 0.55f), 0.4f, 0f, 1.2f, 0.3f), turn, "Glassy lip");
            Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(at.Centre + at.Tangent * 1.6f, 0.06f), new Vector3(2f * at.HalfWidth, 0.14f, 3f), white, turn, "White water");
            spray = Shapes3D.Group(Root, "Spray", SimSpace.ToWorld(at.Centre + at.Tangent * 1.3f, 0.35f));
            for (var i = -3; i <= 3; i++)
                Part(PrimitiveType.Sphere, spray, SimSpace.Direction(at.Left) * (i * at.HalfWidth * 0.29f), new Vector3(2.2f, 0.9f, 1.8f), white, name: "Puff");
        }

        public override void Pose(double seconds) => spray.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin((float)seconds * 9f));
    }

    /// <summary>A big red-and-white buoy bobbing on its mooring.</summary>
    sealed class BumperView : ObstacleView
    {
        readonly Transform buoy;
        readonly float phase;

        public BumperView(Bumper bumper, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Bumper", SimSpace.ToWorld(bumper.Spot.position)), palette)
        {
            var r = bumper.Spot.radius;
            buoy = Shapes3D.Group(Root, "Buoy");
            if (palette.buoyMesh != null)
                Shapes3D.Model("Buoy", palette.buoyMesh, palette.buoyMaterial, buoy, Vector3.zero, 2f * r, Yaw(bumper), 0.3f, Quaternion.Euler(-90f, 0f, 0f));
            else
            {
                var red = Tint(palette.sign, new Color(0.9f, 0.18f, 0.15f));
                Part(PrimitiveType.Sphere, buoy, new Vector3(0f, 0.25f, 0f), new Vector3(2f * r, 1.3f * r, 2f * r), red, name: "Float");
                Part(PrimitiveType.Cylinder, buoy, new Vector3(0f, 0.3f, 0f), new Vector3(2.04f * r, 0.12f, 2.04f * r), palette.eyeWhite, name: "Band");
                Part(PrimitiveType.Cylinder, buoy, new Vector3(0f, 0.75f * r + 0.5f, 0f), new Vector3(0.18f, 0.5f, 0.18f), palette.blowerTrim, name: "Mast");
                Part(PrimitiveType.Sphere, buoy, new Vector3(0f, 0.75f * r + 1.05f, 0f), Vector3.one * 0.35f, palette.sign, name: "Light");
            }

            Shapes3D.MeshObject("Foam", Shapes3D.Disc(r + 0.45f, 20), Root, new Vector3(0f, 0.02f, 0f), palette.foam);
            phase = bumper.Spot.position.x * 0.7f;
        }

        static float Yaw(Bumper bumper) => bumper.Spot.position.x * 37f % 360f;

        public override void Pose(double seconds)
        {
            var t = (float)seconds * 2.2f + phase;
            buoy.localPosition = new Vector3(0f, 0.08f * Mathf.Sin(t), 0f);
            buoy.localRotation = Quaternion.Euler(6f * Mathf.Sin(t * 0.7f), 0f, 6f * Mathf.Cos(t * 0.9f));
        }
    }

    /// <summary>A timber ferry with rails, hauled across the river on a cable strung between two posts.</summary>
    sealed class FerryView : ObstacleView
    {
        readonly Ferry ferry;
        readonly Transform deck;

        public FerryView(Ferry ferry, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Ferry"), palette)
        {
            this.ferry = ferry;
            var at = ferry.At;
            var timber = palette.Planks;
            var dark = Tint(palette.Planks, new Color(0.32f, 0.3f, 0.28f));
            var along = Quaternion.LookRotation(SimSpace.Direction(at.Tangent));
            deck = Shapes3D.Group(Root, "Deck");
            deck.rotation = along;
            Part(PrimitiveType.Cube, deck, new Vector3(0f, 0.25f, 0f), new Vector3(Ferry.Width, 0.4f, Ferry.Length), timber, name: "Deck");
            foreach (var z in new[] { -1f, 1f })
                Part(PrimitiveType.Cube, deck, new Vector3(0f, 0.75f, z * (Ferry.Length * 0.5f - 0.1f)), new Vector3(Ferry.Width, 0.12f, 0.12f), dark, name: "Rail");
            if (palette.crateMesh != null) Shapes3D.Model("Crate", palette.crateMesh, palette.crateMaterial, deck, new Vector3(0.6f, 0.45f, 0f), 0.9f, 20f);
            else Part(PrimitiveType.Cube, deck, new Vector3(0.6f, 0.8f, 0f), new Vector3(0.9f, 0.7f, 0.9f), Tint(palette.wood, new Color(0.7f, 0.52f, 0.3f)), name: "Crate");
            Part(PrimitiveType.Cylinder, deck, new Vector3(-1f, 0.85f, 0.3f), new Vector3(0.6f, 0.4f, 0.6f), palette.barrel, name: "Barrel");
            Part(PrimitiveType.Cube, deck, new Vector3(0f, 0.08f, 0f), new Vector3(Ferry.Width + 0.6f, 0.04f, Ferry.Length + 0.6f), palette.foam, name: "Wash");

            foreach (var side in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, Root, SimSpace.ToWorld(at.Centre + at.Left * (side * (at.HalfWidth + 0.8f)), 1.5f), new Vector3(0.35f, 1.5f, 0.35f), dark, name: "Cable post");
            Part(PrimitiveType.Cylinder, Root, SimSpace.ToWorld(at.Centre, 2.6f), new Vector3(0.05f, at.HalfWidth + 0.8f, 0.05f), palette.blowerTrim,
                Quaternion.LookRotation(SimSpace.Direction(at.Tangent)) * Quaternion.Euler(0f, 0f, 90f), "Cable");
        }

        public override void Pose(double seconds) => deck.position = SimSpace.ToWorld(ferry.Position(seconds));
    }

    /// <summary>A big brown bear on the lip that rears up on its hind legs and swipes a paw over the water.</summary>
    sealed class BearView : ObstacleView
    {
        readonly Bear bear;
        readonly Transform body;
        readonly Transform arm;

        public BearView(Bear bear, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Bear"), palette)
        {
            this.bear = bear;
            var at = bear.At;
            var side = bear.Spot.side;
            Root.SetPositionAndRotation(SimSpace.ToWorld(at.Centre + at.Left * (side * (at.HalfWidth + 1.4f)), 0.1f),
                Quaternion.LookRotation(SimSpace.Direction(-side * at.Left))); // facing the water
            var fur = Tint(palette.animal, new Color(0.38f, 0.24f, 0.14f));
            var muzzle = Tint(palette.animal, new Color(0.6f, 0.45f, 0.3f));
            body = Shapes3D.Group(Root, "Body");
            if (palette.HasCreature(Creature.BearBody) && palette.HasCreature(Creature.BearArm))
            {
                var hide = palette.Fur(new Color(0.36f, 0.23f, 0.13f), new Color(0.6f, 0.45f, 0.3f), new Color(0.3f, 0.25f, 0.22f));
                Shapes3D.MeshObject("Bear", palette.CreatureMesh(Creature.BearBody), body, Vector3.zero, hide);
                foreach (var x in new[] { -1f, 1f })
                {
                    Part(PrimitiveType.Sphere, body, new Vector3(0.2f * x, 2.48f, 0.64f), Vector3.one * 0.11f, palette.eyeWhite, name: "Eye");
                    Part(PrimitiveType.Sphere, body, new Vector3(0.2f * x, 2.49f, 0.69f), Vector3.one * 0.065f, palette.eyeDark, name: "Pupil");
                }

                arm = Shapes3D.Group(body, "Arm", new Vector3(-0.7f * side, 1.8f, 0.2f));
                Shapes3D.MeshObject("Arm", palette.CreatureMesh(Creature.BearArm), arm, Vector3.zero, hide);
                return;
            }

            Part(PrimitiveType.Capsule, body, new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 1.1f, 1.3f), fur, name: "Torso");
            var head = Shapes3D.Group(body, "Head", new Vector3(0f, 2.35f, 0.25f));
            Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.95f, 0.85f, 0.9f), fur, name: "Skull");
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.12f, 0.45f), new Vector3(0.45f, 0.35f, 0.45f), muzzle, name: "Muzzle");
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.02f, 0.68f), Vector3.one * 0.14f, palette.eyeDark, name: "Nose");
            foreach (var x in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, head, new Vector3(0.38f * x, 0.38f, -0.05f), new Vector3(0.3f, 0.3f, 0.15f), fur, name: "Ear");
                Part(PrimitiveType.Sphere, head, new Vector3(0.2f * x, 0.12f, 0.4f), Vector3.one * 0.1f, palette.eyeDark, name: "Eye");
                Part(PrimitiveType.Capsule, body, new Vector3(0.45f * x, 0.4f, 0.1f), new Vector3(0.45f, 0.45f, 0.45f), fur, name: "Leg");
            }

            arm = Shapes3D.Group(body, "Arm", new Vector3(-0.7f * side, 1.8f, 0.2f));
            Part(PrimitiveType.Capsule, arm, new Vector3(0f, 0f, 0.7f), new Vector3(0.38f, 0.75f, 0.38f), fur, Quaternion.Euler(90f, 0f, 0f), "Forearm");
            Part(PrimitiveType.Sphere, arm, new Vector3(0f, 0f, 1.45f), new Vector3(0.5f, 0.3f, 0.5f), muzzle, name: "Paw");
        }

        public override void Pose(double seconds)
        {
            var (rear, swipe) = bear.Pose(seconds);
            body.localRotation = Quaternion.Euler(-12f * rear + 25f * swipe, 0f, 0f);
            body.localPosition = new Vector3(0f, 0.4f * rear, 0f);
            // The paw goes up as it rears, then sweeps down and across the water.
            var reach = swipe > 0f ? Mathf.Lerp(-70f, 40f, swipe) : Mathf.Lerp(20f, -70f, rear);
            arm.localRotation = Quaternion.Euler(reach, 30f * swipe * bear.Spot.side, 0f);
            // The swipe stretches the arm out to the paw's reach over the water (Bear.Reach past the lip), not fatter.
            var stretch = 1f + 1.45f * swipe;
            arm.localScale = new Vector3(1f + 0.35f * swipe, 1f + 0.35f * swipe, stretch);
        }
    }
}
