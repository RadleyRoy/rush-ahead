using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Render
{
    /// <summary>Small pieces the Phase 17 obstacle views share.</summary>
    static class Built
    {
        /// <summary>
        /// A stone block for something standing on the bank, from deep in the gorge up to <paramref name="top"/>, so a
        /// shack, mill or crane sits on solid ground at the river's lip rather than on air.
        /// </summary>
        public static void Footing(Transform parent, Vector2 at, Vector2 along, float width, float depth, ArtPalette palette, float top = 0.3f) =>
            Shapes3D.Primitive(PrimitiveType.Cube, parent, SimSpace.ToWorld(at, top - 6f), new Vector3(width, 12f, depth), palette.cliff,
                Quaternion.LookRotation(SimSpace.Direction(along)), "Footing");

        /// <summary>Stretches a cylinder (or, with <paramref name="cube"/>, a cube) from one point to another.</summary>
        public static void Span(Transform part, Vector3 from, Vector3 to, float thickness, bool cube = false)
        {
            var line = to - from;
            var length = Mathf.Max(0.01f, line.magnitude);
            part.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, line / length));
            part.localScale = new Vector3(thickness, cube ? length : length * 0.5f, thickness);
        }

        public static float Smooth(float t) => t * t * (3f - 2f * t);

        public static void Show(Component part, bool shown)
        {
            if (part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
        }

        /// <summary>See-through moving water drawn from both sides (for shapes open at the ends).</summary>
        public static Material TwoSided(Material spray)
        {
            var material = new Material(spray) { name = spray.name + " (both sides)" };
            material.SetFloat("_Cull", 0f);
            return material;
        }

        /// <summary>
        /// A breaking wave's body: its cross-section (a long back rising to a crest that curls over a steep face at -z)
        /// run from x = -1 to 1, closed at the ends. Scale it to the length, height and depth wanted.
        /// </summary>
        public static Mesh WaveMesh()
        {
            Vector2[] profile =
            {
                new(2.8f, -0.4f), new(2f, 0.15f), new(1.2f, 0.6f), new(0.5f, 1.05f), new(0f, 1.4f), new(-0.45f, 1.6f), new(-0.9f, 1.5f),
                new(-1.2f, 1.2f), new(-1.12f, 0.85f), new(-1.3f, 0.4f), new(-1.6f, -0.4f),
            };
            var n = profile.Length;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            foreach (var x in new[] { -1f, 1f })
                foreach (var p in profile)
                    vertices.Add(new Vector3(x, p.y, p.x));
            for (var k = 0; k < n; k++)
            {
                var next = (k + 1) % n;
                triangles.AddRange(new[] { k, next, n + next, k, n + next, n + k });
            }

            // The ends: a fan from inside the section.
            foreach (var x in new[] { -1f, 1f })
            {
                var centre = vertices.Count;
                vertices.Add(new Vector3(x, 0.45f, 0.3f));
                var first = vertices.Count;
                foreach (var p in profile) vertices.Add(new Vector3(x, p.y, p.x));
                for (var k = 0; k < n; k++)
                    if (x < 0f) triangles.AddRange(new[] { centre, first + k, first + (k + 1) % n });
                    else triangles.AddRange(new[] { centre, first + (k + 1) % n, first + k });
            }

            var mesh = new Mesh { name = "Wave" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            if (mesh.normals[5].y < 0f) // wound the wrong way round: turn every face over
            {
                for (var i = 0; i < triangles.Count; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Giant purple tentacles: bubbles where one will come down, then it rears up by the bank and slams across the water.</summary>
    sealed class KrakenView : ObstacleView
    {
        const int Segments = 22;
        const int BubbleCount = 9;
        readonly Kraken kraken;
        readonly Transform[][] arms;
        readonly Transform[][] suckers;
        readonly Transform[][] bubbles;
        readonly Transform[] shadows;
        readonly Transform[] splashes;

        public KrakenView(Kraken kraken, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Kraken"), palette)
        {
            this.kraken = kraken;
            var skin = Tint(palette.animal, new Color(0.46f, 0.15f, 0.3f));
            var tip = Tint(palette.animal, new Color(0.62f, 0.25f, 0.4f));
            var pale = Tint(palette.animal, new Color(0.96f, 0.74f, 0.72f));
            var froth = palette.Spray(new Color(0.9f, 0.97f, 1f, 0.85f), 0.3f);
            var shade = new Material(palette.foam) { name = "Kraken shadow", color = new Color(0.08f, 0.02f, 0.1f, 0.45f) };
            var splash = palette.Spray(new Color(1f, 1f, 1f, 0.8f), 0.5f, 0f, 1.5f);
            var n = kraken.Arms;
            arms = new Transform[n][];
            suckers = new Transform[n][];
            bubbles = new Transform[n][];
            shadows = new Transform[n];
            splashes = new Transform[n];
            for (var a = 0; a < n; a++)
            {
                arms[a] = new Transform[Segments];
                for (var i = 0; i < Segments; i++)
                    arms[a][i] = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, i >= Segments - 5 ? tip : skin, name: "Tentacle").transform;
                suckers[a] = new Transform[Segments / 2];
                for (var i = 0; i < suckers[a].Length; i++)
                    suckers[a][i] = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one * 0.2f, pale, name: "Sucker").transform;
                bubbles[a] = new Transform[BubbleCount];
                for (var i = 0; i < BubbleCount; i++)
                    bubbles[a][i] = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, froth, name: "Bubble").transform;
                shadows[a] = Part(PrimitiveType.Cube, Root, Vector3.zero, Vector3.one, shade, name: "Shadow").transform;
                splashes[a] = Part(PrimitiveType.Capsule, Root, Vector3.zero, Vector3.one, splash, name: "Splash").transform;
            }
        }

        static float Radius(float s) => Mathf.Lerp(0.78f, 0.2f, s);

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            for (var a = 0; a < arms.Length; a++)
            {
                var phase = kraken.ArmPhase(a, seconds);
                var (root, tip) = kraken.Line(a, seconds);
                var length = Vector2.Distance(root, tip);
                var across = SimSpace.Direction((tip - root).normalized);
                var facing = Quaternion.LookRotation(across);

                // Bubbles and a dark shape under the water along the line it'll come down on.
                var warn = phase >= Kraken.Bubbles && phase < Kraken.Slam ? Mathf.Clamp01((phase - Kraken.Bubbles) / (Kraken.Rise - Kraken.Bubbles)) : 0f;
                for (var i = 0; i < BubbleCount; i++)
                {
                    var bubble = bubbles[a][i];
                    Built.Show(bubble, warn > 0f);
                    if (warn <= 0f) continue;
                    var size = warn * (0.35f + 0.25f * Mathf.Abs(Mathf.Sin(t * 6f + i * 1.7f)));
                    bubble.position = SimSpace.ToWorld(Vector2.Lerp(root, tip, (i + 0.5f) / BubbleCount), 0.05f);
                    bubble.localScale = new Vector3(size, size * 0.5f, size);
                }

                Built.Show(shadows[a], warn > 0f);
                shadows[a].SetPositionAndRotation(SimSpace.ToWorld((root + tip) * 0.5f, 0.02f), facing);
                shadows[a].localScale = new Vector3(1.4f * warn, 0.02f, length);

                // The tentacle: up out of the water by the bank, rearing, then down across the river; lying there, then back under.
                var reach = 1f;
                var lean = 1f;
                var sink = 0f;
                if (phase < Kraken.Slam)
                {
                    var u = (phase - Kraken.Rise) / (Kraken.Slam - Kraken.Rise);
                    reach = Mathf.Clamp01(u / 0.3f);
                    lean = u < 0.75f ? Mathf.Lerp(0.1f, 0.35f, u / 0.75f) + 0.04f * Mathf.Sin(t * 5f) : Mathf.Lerp(0.35f, 1f, Mathf.Pow((u - 0.75f) / 0.25f, 2f));
                }
                else if (phase >= Kraken.Lift)
                {
                    var u = (phase - Kraken.Lift) / (1f - Kraken.Lift);
                    reach = 1f - u;
                    sink = u;
                }

                var visible = phase >= Kraken.Rise && reach > 0.02f;
                var curl = (1f - lean) * 70f * Mathf.Deg2Rad;
                var step = length * reach / (Segments - 1);
                var point = SimSpace.ToWorld(root, 0.25f - sink * 1.2f);
                for (var i = 0; i < Segments; i++)
                {
                    var s = i / (Segments - 1f);
                    var piece = arms[a][i];
                    Built.Show(piece, visible);
                    var sucker = i % 2 == 0 && i / 2 < suckers[a].Length ? suckers[a][i / 2] : null;
                    if (sucker != null) Built.Show(sucker, visible && lean < 0.95f);
                    if (!visible) continue;
                    var r = Radius(s) * Mathf.Max(0.35f, reach);
                    var angle = lean * Mathf.PI * 0.5f + curl * s;
                    var direction = Vector3.up * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                    var inner = across * Mathf.Cos(angle) - Vector3.up * Mathf.Sin(angle); // the side it curls toward: the suckers
                    piece.position = point + Vector3.up * (0.03f * Mathf.Sin(t * 7f + i));
                    piece.localScale = Vector3.one * (2f * r);
                    if (sucker != null)
                    {
                        sucker.position = point + inner * (r * 0.85f);
                        sucker.localScale = Vector3.one * (r * 0.55f);
                    }

                    point += direction * step;
                }

                // The slam's splash all along it.
                var slam = phase >= Kraken.Slam && phase < Kraken.Slam + 0.08f ? (phase - Kraken.Slam) / 0.08f : -1f;
                Built.Show(splashes[a], slam >= 0f);
                if (slam < 0f) continue;
                splashes[a].SetPositionAndRotation(SimSpace.ToWorld((root + tip) * 0.5f, 0.2f), facing * Quaternion.Euler(90f, 0f, 0f));
                splashes[a].localScale = new Vector3(1.2f + 1.5f * slam, length * 0.5f, 0.8f + 1.4f * slam);
            }
        }
    }

    /// <summary>A rolling wall of white water right across the river, swelling at the bottom of its stretch and coming up it.</summary>
    sealed class TidalBoreView : ObstacleView
    {
        readonly TidalBore bore;
        readonly Transform wave;

        public TidalBoreView(TidalBore bore, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Tidal bore"), palette)
        {
            this.bore = bore;
            var body = Built.TwoSided(palette.Spray(new Color(0.36f, 0.6f, 0.62f, 0.92f), 0.9f, 0f, 0f, 0.5f));
            var white = palette.Spray(new Color(1f, 1f, 1f, 0.95f), 0.6f, 0f, 1.5f);
            wave = Shapes3D.Group(Root, "Bore");
            Shapes3D.MeshObject("Water", Built.WaveMesh(), wave, Vector3.zero, body);
            Part(PrimitiveType.Capsule, wave, new Vector3(0f, 1.5f, -0.65f), new Vector3(0.6f, 1f, 0.75f), white, Quaternion.Euler(0f, 0f, 90f), "Crest");
            Part(PrimitiveType.Cube, wave, new Vector3(0f, 0.04f, -2.3f), new Vector3(2f, 0.04f, 1.6f), palette.foam, name: "Lip foam");
            Part(PrimitiveType.Cube, wave, new Vector3(0f, 0.04f, 4.2f), new Vector3(2f, 0.04f, 5f), palette.foam, name: "Churn");
        }

        public override void Pose(double seconds)
        {
            var (front, height) = bore.Wave(seconds);
            Built.Show(wave, height > 0.01f);
            if (height <= 0.01f) return;
            var at = bore.Course.River.At(front);
            wave.SetPositionAndRotation(SimSpace.ToWorld(at.Centre, 0.05f * Mathf.Sin((float)seconds * 9f)), Facing(at.Tangent));
            wave.localScale = new Vector3(at.HalfWidth - 0.1f, 1.5f * height, 1.2f);
        }
    }

    /// <summary>A plank ramp on log floats, striped yellow and black, with flags at its lip.</summary>
    sealed class JumpRampView : ObstacleView
    {
        readonly Transform[] flags = new Transform[2];

        public JumpRampView(JumpRamp ramp, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Jump ramp"), palette)
        {
            var at = ramp.At;
            var group = Shapes3D.Group(Root, "Ramp");
            group.SetPositionAndRotation(SimSpace.ToWorld(ramp.Foot + at.Tangent * (JumpRamp.Length * 0.5f)), Facing(at.Tangent));
            var slope = Mathf.Atan2(JumpRamp.Lip, JumpRamp.Length) * Mathf.Rad2Deg;
            var deck = Mathf.Sqrt(JumpRamp.Length * JumpRamp.Length + JumpRamp.Lip * JumpRamp.Lip);
            var width = JumpRamp.HalfWidth * 2f;
            var board = Shapes3D.Group(group, "Deck", new Vector3(0f, JumpRamp.Lip * 0.5f, 0f));
            board.localRotation = Quaternion.Euler(-slope, 0f, 0f);
            Part(PrimitiveType.Cube, board, new Vector3(0f, -0.1f, 0f), new Vector3(width, 0.2f, deck), palette.Planks, name: "Boards");
            var yellow = Tint(palette.sign, new Color(1f, 0.8f, 0.1f));
            var black = Tint(palette.sign, new Color(0.08f, 0.08f, 0.08f));
            for (var i = 0; i < 5; i++)
                Part(PrimitiveType.Cube, board, new Vector3(0f, 0.01f, (i - 2f) * deck * 0.19f), new Vector3(width * 0.98f, 0.02f, deck * 0.09f), i % 2 == 0 ? yellow : black, name: "Stripe");
            foreach (var x in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cube, board, new Vector3(x * (JumpRamp.HalfWidth - 0.08f), 0.12f, 0f), new Vector3(0.16f, 0.24f, deck), Tint(palette.animal, new Color(0.45f, 0.36f, 0.26f)), name: "Rail");
                Part(PrimitiveType.Cylinder, group, new Vector3(x * JumpRamp.HalfWidth * 0.7f, 0.05f, 0f), new Vector3(0.6f, JumpRamp.Length * 0.55f, 0.6f), Tint(palette.wood, new Color(0.45f, 0.33f, 0.22f)),
                    Quaternion.Euler(90f, 0f, 0f), "Float");
                Part(PrimitiveType.Cylinder, group, new Vector3(x * (JumpRamp.HalfWidth - 0.2f), JumpRamp.Lip * 0.5f, JumpRamp.Length * 0.5f - 0.1f), new Vector3(0.2f, JumpRamp.Lip * 0.5f, 0.2f),
                    palette.wood, name: "Post");
                var pole = Shapes3D.Group(group, "Flag", new Vector3(x * (JumpRamp.HalfWidth + 0.1f), JumpRamp.Lip, JumpRamp.Length * 0.5f));
                Part(PrimitiveType.Cylinder, pole, new Vector3(0f, 0.8f, 0f), new Vector3(0.06f, 0.8f, 0.06f), palette.Metal, name: "Pole");
                flags[x < 0f ? 0 : 1] = Part(PrimitiveType.Cube, pole, new Vector3(0f, 1.4f, -0.3f), new Vector3(0.03f, 0.4f, 0.6f), Tint(palette.sign, new Color(0.95f, 0.2f, 0.15f)), name: "Cloth").transform;
            }

            Part(PrimitiveType.Cube, group, new Vector3(0f, 0.03f, 0f), new Vector3(width + 1f, 0.04f, JumpRamp.Length + 1.2f), palette.foam, name: "Wash");
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            for (var i = 0; i < flags.Length; i++) flags[i].localRotation = Quaternion.Euler(0f, 18f * Mathf.Sin(t * 6f + i), 0f);
        }
    }

    /// <summary>Canada geese: brown bodies, black necks and white chin straps, necks stretched and wings out when they charge.</summary>
    sealed class GooseGangView : ObstacleView
    {
        readonly GooseGang gang;
        readonly Transform[] birds;
        readonly Transform[] necks;
        readonly Transform[][] wings;
        readonly Vector2[] last;
        readonly Vector2[] heading;
        readonly float[] speed;
        bool started;

        public GooseGangView(GooseGang gang, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Goose gang"), palette)
        {
            this.gang = gang;
            var body = Tint(palette.animal, new Color(0.44f, 0.37f, 0.3f));
            var breast = Tint(palette.animal, new Color(0.82f, 0.78f, 0.7f));
            var black = Tint(palette.animal, new Color(0.06f, 0.06f, 0.07f));
            var white = Tint(palette.eyeWhite, new Color(0.96f, 0.96f, 0.94f));
            var wing = Tint(palette.animal, new Color(0.34f, 0.29f, 0.24f));
            var n = gang.Geese.Count;
            birds = new Transform[n];
            necks = new Transform[n];
            wings = new Transform[n][];
            last = new Vector2[n];
            heading = new Vector2[n];
            speed = new float[n];
            for (var i = 0; i < n; i++)
            {
                var bird = birds[i] = Shapes3D.Group(Root, "Goose");
                Part(PrimitiveType.Sphere, bird, new Vector3(0f, 0.18f, 0f), new Vector3(0.55f, 0.42f, 0.85f), body, name: "Body");
                Part(PrimitiveType.Sphere, bird, new Vector3(0f, 0.16f, 0.2f), new Vector3(0.48f, 0.36f, 0.45f), breast, name: "Breast");
                Part(PrimitiveType.Sphere, bird, new Vector3(0f, 0.28f, -0.42f), new Vector3(0.25f, 0.16f, 0.25f), black, name: "Tail");
                var neck = necks[i] = Shapes3D.Group(bird, "Neck", new Vector3(0f, 0.3f, 0.32f));
                Part(PrimitiveType.Capsule, neck, new Vector3(0f, 0.28f, 0f), new Vector3(0.15f, 0.3f, 0.15f), black, name: "Neck");
                Part(PrimitiveType.Sphere, neck, new Vector3(0f, 0.6f, 0.06f), new Vector3(0.2f, 0.19f, 0.3f), black, name: "Head");
                Part(PrimitiveType.Sphere, neck, new Vector3(0f, 0.57f, 0.07f), new Vector3(0.22f, 0.1f, 0.18f), white, name: "Chin strap");
                Part(PrimitiveType.Cube, neck, new Vector3(0f, 0.58f, 0.25f), new Vector3(0.07f, 0.06f, 0.16f), black, name: "Beak");
                wings[i] = new Transform[2];
                for (var w = 0; w < 2; w++)
                {
                    var side = w == 0 ? -1f : 1f;
                    wings[i][w] = Shapes3D.Group(bird, "Wing", new Vector3(0.24f * side, 0.3f, 0f));
                    Part(PrimitiveType.Cube, wings[i][w], new Vector3(0.3f * side, 0f, -0.05f), new Vector3(0.62f, 0.05f, 0.5f), wing, name: "Feathers");
                }
            }

            // Their patch of reeds on the bank.
            var at = gang.At;
            var random = new System.Random(Mathf.RoundToInt(gang.Spot.distance * 7f));
            for (var i = -1; i <= 1; i++)
                CourseDressing.Reeds(Root, at.Centre + at.Left * (gang.Spot.side * (at.HalfWidth + 0.9f)) + at.Tangent * (i * 2.2f), 0f, palette, random);
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var dt = Mathf.Max(1e-3f, Time.deltaTime);
            for (var i = 0; i < birds.Length; i++)
            {
                Vector2 here = gang.Geese[i].transform.position;
                if (!started) last[i] = here;
                var moved = (here - last[i]) / dt;
                last[i] = here;
                speed[i] = Mathf.Lerp(speed[i], moved.magnitude, 1f - Mathf.Exp(-dt * 8f));
                if (moved.sqrMagnitude > 0.16f) heading[i] = Vector2.Lerp(heading[i], moved.normalized, 1f - Mathf.Exp(-dt * 10f));
                if (heading[i].sqrMagnitude < 1e-4f) heading[i] = -gang.Spot.side * gang.At.Left;
                var charging = Mathf.Clamp01((speed[i] - 1f) / 2f);
                birds[i].SetPositionAndRotation(SimSpace.ToWorld(here, 0.03f * Mathf.Sin(t * 3f + i)), Facing(heading[i]));
                necks[i].localRotation = Quaternion.Euler(Mathf.Lerp(-5f, 62f, charging) + 6f * Mathf.Sin(t * 4f + i), 0f, 0f);
                var flap = charging * (35f + 30f * Mathf.Sin(t * 20f + i));
                wings[i][0].localRotation = Quaternion.Euler(0f, 0f, -flap);
                wings[i][1].localRotation = Quaternion.Euler(0f, 0f, flap);
            }

            started = true;
        }
    }

    /// <summary>Rusty spiked mines bobbing on their chains, a red light blinking on each, with a flash when one goes off.</summary>
    sealed class ChainedMinesView : ObstacleView
    {
        readonly ChainedMines mines;
        readonly Transform[] balls;
        readonly MeshRenderer[] lights;
        readonly Transform[] chains;
        readonly Transform[] blasts;
        readonly Material lit;
        readonly Material dark;

        public ChainedMinesView(ChainedMines mines, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Chained mines"), palette)
        {
            this.mines = mines;
            var iron = Tint(palette.Metal, new Color(0.2f, 0.19f, 0.18f));
            var rust = Tint(palette.Metal, new Color(0.5f, 0.27f, 0.14f));
            lit = Tint(palette.sign, new Color(1f, 0.12f, 0.08f));
            dark = Tint(palette.sign, new Color(0.28f, 0.04f, 0.04f));
            var flash = palette.Spray(new Color(1f, 0.62f, 0.2f, 0.85f), 0.4f, 0f, 2f);
            var n = mines.Count;
            balls = new Transform[n];
            lights = new MeshRenderer[n];
            chains = new Transform[n];
            blasts = new Transform[n];
            for (var i = 0; i < n; i++)
            {
                var ball = balls[i] = Shapes3D.Group(Root, "Mine");
                Part(PrimitiveType.Sphere, ball, Vector3.zero, Vector3.one * (ChainedMines.MineRadius * 2f), iron, name: "Shell");
                Part(PrimitiveType.Cylinder, ball, new Vector3(0f, -0.1f, 0f), new Vector3(1.18f, 0.06f, 1.18f), rust, name: "Seam");
                for (var s = 0; s < 8; s++)
                {
                    var direction = Quaternion.Euler(s < 4 ? 20f : -35f, s * 90f + (s < 4 ? 0f : 45f), 0f) * Vector3.forward;
                    if (s >= 4) direction = (direction + Vector3.up * 0.9f).normalized;
                    Part(PrimitiveType.Cylinder, ball, direction * 0.6f, new Vector3(0.12f, 0.14f, 0.12f), rust, Quaternion.FromToRotation(Vector3.up, direction), "Spike");
                }

                lights[i] = Part(PrimitiveType.Sphere, ball, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.22f, dark, name: "Light").GetComponent<MeshRenderer>();
                Shapes3D.MeshObject("Ring", Shapes3D.Disc(0.95f, 24), ball, new Vector3(0f, -0.15f, 0f), palette.foam);
                chains[i] = Part(PrimitiveType.Cylinder, Root, Vector3.zero, Vector3.one, iron, name: "Chain").transform;
                blasts[i] = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, flash, name: "Blast").transform;
            }
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            for (var i = 0; i < balls.Length; i++)
            {
                var mine = mines.Mine(i, seconds);
                var at = SimSpace.ToWorld(mine, 0.1f + 0.08f * Mathf.Sin(t * 2.3f + i));
                balls[i].SetPositionAndRotation(at, Quaternion.Euler(0f, t * 25f + i * 40f, 6f * Mathf.Sin(t * 1.7f + i)));
                var blink = Mathf.Repeat(t * 2.4f + i * 0.31f, 1f) < 0.25f;
                lights[i].sharedMaterial = blink ? lit : dark;
                Built.Span(chains[i], at + Vector3.down * 0.4f, SimSpace.ToWorld(mines.Anchor(i), -1.6f), 0.08f);
                var age = (float)(seconds - mines.LastBlast(i));
                var blasting = age >= 0f && age < 0.5f;
                Built.Show(blasts[i], blasting);
                if (!blasting) continue;
                blasts[i].position = at;
                blasts[i].localScale = Vector3.one * (1.5f + 9f * age);
            }
        }
    }

    /// <summary>A yellow steel gantry over the river with a dark iron ball swinging on a chain beneath it.</summary>
    sealed class WreckingBallView : ObstacleView
    {
        readonly WreckingBall wrecker;
        readonly Transform chain;
        readonly Transform ball;
        readonly Transform splash;

        public WreckingBallView(WreckingBall wrecker, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Wrecking ball"), palette)
        {
            this.wrecker = wrecker;
            var at = wrecker.At;
            var yellow = Tint(palette.Metal, new Color(0.95f, 0.72f, 0.12f));
            var black = Tint(palette.Metal, new Color(0.08f, 0.08f, 0.08f));
            var iron = Tint(palette.Metal, new Color(0.17f, 0.17f, 0.18f));
            var top = WreckingBall.PivotHeight + 0.4f;
            foreach (var side in new[] { -1f, 1f })
            {
                var foot = at.Centre + at.Left * (side * (at.HalfWidth + 1.1f));
                Built.Footing(Root, foot, at.Tangent, 1.8f, 1.8f, palette);
                Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(foot, top * 0.5f), new Vector3(0.6f, top, 0.6f), yellow, name: "Post");
                for (var b = 0; b < 4; b++)
                    Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(foot, 1.2f + b * 1.8f), new Vector3(0.64f, 0.35f, 0.64f), black, name: "Stripe");
            }

            Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(at.Centre, top), new Vector3(0.7f, 0.7f, 2f * at.HalfWidth + 2.8f), yellow, Facing(at.Left), "Beam");
            Part(PrimitiveType.Cube, Root, SimSpace.ToWorld(wrecker.Pivot, top - 0.2f), new Vector3(0.5f, 0.5f, 1.6f), black, Facing(at.Tangent), "Pivot");
            chain = Part(PrimitiveType.Cylinder, Root, Vector3.zero, Vector3.one, iron, name: "Chain").transform;
            ball = Shapes3D.Group(Root, "Ball");
            Part(PrimitiveType.Sphere, ball, Vector3.zero, Vector3.one * (WreckingBall.Ball * 2f), iron, name: "Iron");
            Part(PrimitiveType.Cylinder, ball, new Vector3(0f, WreckingBall.Ball, 0f), new Vector3(0.4f, 0.2f, 0.4f), black, name: "Shackle");
            splash = Shapes3D.Group(Root, "Splash");
            Shapes3D.MeshObject("Foam", Shapes3D.Disc(1f, 24), splash, Vector3.zero, palette.foam);
            Part(PrimitiveType.Sphere, splash, new Vector3(0f, 0.2f, 0f), new Vector3(1.6f, 0.7f, 1.6f), palette.Spray(new Color(1f, 1f, 1f, 0.7f), 0.5f, 0f, 1.5f), name: "Spray");
        }

        public override void Pose(double seconds)
        {
            var (ground, height) = wrecker.Pose(seconds);
            var pivot = SimSpace.ToWorld(wrecker.Pivot, WreckingBall.PivotHeight);
            var middle = SimSpace.ToWorld(ground, height);
            Built.Span(chain, pivot, middle + (pivot - middle).normalized * WreckingBall.Ball, 0.14f);
            ball.SetPositionAndRotation(middle, Quaternion.FromToRotation(Vector3.up, (pivot - middle).normalized));
            var low = height < 1.6f;
            Built.Show(splash, low);
            if (!low) return;
            splash.position = SimSpace.ToWorld(ground, 0.04f);
            splash.localScale = Vector3.one * (1.6f + (1.6f - height));
        }
    }

    /// <summary>A heap of dark cloud with rain hanging under it, a glowing ring on the water where it'll strike, then a forked bolt.</summary>
    sealed class StormCloudView : ObstacleView
    {
        readonly StormCloud storm;
        readonly Transform cloud;
        readonly Transform ring;
        readonly Transform glow;
        readonly Material glowMaterial;
        readonly Transform[] bolt = new Transform[6];
        readonly Transform flash;
        readonly MeshRenderer ringRenderer;
        readonly Material ringBright;
        readonly Material ringDim;

        public StormCloudView(StormCloud storm, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Storm cloud"), palette)
        {
            this.storm = storm;
            var dark = palette.Spray(new Color(0.3f, 0.31f, 0.36f, 0.9f), 0.1f, 0.03f, 0.15f, 1f);
            var grey = palette.Spray(new Color(0.48f, 0.49f, 0.54f, 0.85f), 0.1f, -0.03f, 0.15f, 1f);
            cloud = Shapes3D.Group(Root, "Cloud");
            var random = new System.Random(Mathf.RoundToInt(storm.Spot.distance * 11f));
            for (var i = 0; i < 18; i++)
            {
                var size = 1.6f + (float)random.NextDouble() * 1.6f;
                var at = new Vector3(((float)random.NextDouble() * 2f - 1f) * 3.8f, (float)random.NextDouble() * 1.1f, ((float)random.NextDouble() * 2f - 1f) * 2.6f);
                Part(PrimitiveType.Sphere, cloud, at, new Vector3(size, size * 0.65f, size), at.y > 0.6f ? grey : dark, name: "Puff");
            }

            Part(PrimitiveType.Cylinder, cloud, new Vector3(0f, -0.25f, 0f), new Vector3(8.4f, 0.25f, 5.8f), dark, name: "Base");

            var rain = palette.Spray(new Color(0.6f, 0.68f, 0.78f, 0.3f), 1f, 0f, -4f, 0.3f);
            for (var i = 0; i < 5; i++)
                Part(PrimitiveType.Cylinder, cloud, new Vector3((i - 2f) * 1.3f, -StormCloud.Height * 0.5f, ((i * 7) % 5 - 2f) * 0.8f), new Vector3(0.9f, StormCloud.Height * 0.45f, 0.9f), rain, name: "Rain");

            ringBright = Tint(palette.sign, new Color(1f, 0.88f, 0.25f));
            ringDim = Tint(palette.sign, new Color(0.75f, 0.55f, 0.1f));
            ring = Shapes3D.MeshObject("Ring", Shapes3D.Torus(StormCloud.Strike, 0.1f, 40, 8), Root, Vector3.zero, ringBright).transform;
            ringRenderer = ring.GetComponent<MeshRenderer>();
            glowMaterial = new Material(palette.foam) { name = "Strike glow", color = new Color(1f, 0.85f, 0.3f, 0f) };
            glow = Shapes3D.MeshObject("Glow", Shapes3D.Disc(StormCloud.Strike, 28), Root, Vector3.zero, glowMaterial).transform;
            var white = Tint(palette.eyeWhite, new Color(1f, 0.98f, 0.75f));
            for (var i = 0; i < bolt.Length; i++) bolt[i] = Part(PrimitiveType.Cube, Root, Vector3.zero, Vector3.one, white, name: "Bolt").transform;
            flash = Part(PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one, palette.Spray(new Color(1f, 1f, 0.85f, 0.75f), 0.2f, 0f, 2f), name: "Flash").transform;
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var phase = storm.StrikePhase(seconds);
            var target = storm.Target(seconds);
            var over = SimSpace.ToWorld(storm.Cloud(seconds), StormCloud.Height + 0.3f * Mathf.Sin(t * 0.8f));
            cloud.SetPositionAndRotation(over, Quaternion.Euler(0f, t * 4f, 0f));

            var showing = phase >= StormCloud.Glow && phase < StormCloud.Flash;
            Built.Show(ring, showing);
            Built.Show(glow, showing);
            if (showing)
            {
                var grow = Mathf.Clamp01((phase - StormCloud.Glow) / (StormCloud.Bolt - StormCloud.Glow));
                ring.position = SimSpace.ToWorld(target, 0.06f);
                ring.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, grow);
                ringRenderer.sharedMaterial = grow > 0.5f && Mathf.Repeat(t * (4f + 10f * grow), 1f) < 0.5f ? ringDim : ringBright;
                glow.position = SimSpace.ToWorld(target, 0.04f);
                glow.localScale = ring.localScale;
                glowMaterial.color = new Color(1f, 0.85f, 0.3f, 0.35f * grow);
            }

            var striking = phase >= StormCloud.Bolt && phase < StormCloud.Flash;
            Built.Show(flash, striking);
            var top = SimSpace.ToWorld(target, StormCloud.Height - 1f);
            var bottom = SimSpace.ToWorld(target, 0f);
            var round = Mathf.FloorToInt(t * 23f); // the fork flickers
            var previous = top;
            for (var i = 0; i < bolt.Length; i++)
            {
                Built.Show(bolt[i], striking);
                if (!striking) continue;
                var s = (i + 1f) / bolt.Length;
                var next = Vector3.Lerp(top, bottom, s);
                if (i < bolt.Length - 1) next += new Vector3(Mathf.Sin(round * 3.1f + i * 1.9f), 0f, Mathf.Cos(round * 2.3f + i * 2.7f)) * 0.9f;
                Built.Span(bolt[i], previous, next, 0.2f, cube: true);
                previous = next;
            }

            if (!striking) return;
            flash.position = SimSpace.ToWorld(target, 0.5f);
            flash.localScale = Vector3.one * (StormCloud.Strike * 1.6f);
        }
    }

    /// <summary>A white and red paddle steamer with two tall funnels, turning paddles in boxes either side and a wake behind.</summary>
    sealed class PaddleSteamerView : ObstacleView
    {
        readonly PaddleSteamer steamer;
        readonly Transform boat;
        readonly Transform[] wheels = new Transform[2];
        readonly Transform wake;
        readonly Transform[] smoke = new Transform[4];

        public PaddleSteamerView(PaddleSteamer steamer, ArtPalette palette, Transform parent) : base(Shapes3D.Group(parent, "Paddle steamer"), palette)
        {
            this.steamer = steamer;
            var red = Tint(palette.animal, new Color(0.7f, 0.16f, 0.13f));
            var white = Tint(palette.animal, new Color(0.95f, 0.93f, 0.88f));
            var black = Tint(palette.Metal, new Color(0.1f, 0.1f, 0.11f));
            var glass = Tint(palette.Metal, new Color(0.2f, 0.3f, 0.38f));
            const float L = PaddleSteamer.Length;
            const float W = PaddleSteamer.Width;
            boat = Shapes3D.Group(Root, "Steamer");
            Part(PrimitiveType.Cube, boat, new Vector3(0f, 0.3f, -0.4f), new Vector3(W, 1.1f, L - 0.8f), red, name: "Hull");
            Part(PrimitiveType.Cube, boat, new Vector3(0f, 0.3f, L * 0.5f - 1.1f), new Vector3(W * 0.71f, 1.08f, W * 0.71f), red, Quaternion.Euler(0f, 45f, 0f), "Bow");
            Part(PrimitiveType.Cube, boat, new Vector3(0f, 0.9f, -0.3f), new Vector3(W + 0.1f, 0.12f, L - 0.6f), palette.Planks, name: "Deck");
            Part(PrimitiveType.Cube, boat, new Vector3(0f, 1.55f, -0.8f), new Vector3(W - 0.6f, 1.2f, L * 0.5f), white, name: "Saloon");
            Part(PrimitiveType.Cube, boat, new Vector3(0f, 1.65f, -0.8f), new Vector3(W - 0.55f, 0.3f, L * 0.5f - 0.2f), glass, name: "Windows");
            Part(PrimitiveType.Cube, boat, new Vector3(0f, 2.2f, -0.8f), new Vector3(W - 0.2f, 0.1f, L * 0.55f), white, name: "Upper deck");
            Part(PrimitiveType.Cube, boat, new Vector3(0f, 2.75f, 1.1f), new Vector3(1.5f, 1f, 1.2f), white, name: "Wheelhouse");
            foreach (var x in new[] { -0.6f, 0.6f })
            {
                Part(PrimitiveType.Cylinder, boat, new Vector3(x, 3.6f, -0.6f), new Vector3(0.55f, 1.4f, 0.55f), black, name: "Funnel");
                Part(PrimitiveType.Cylinder, boat, new Vector3(x, 4.55f, -0.6f), new Vector3(0.58f, 0.16f, 0.58f), red, name: "Band");
            }

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var x = side * (W * 0.5f + PaddleSteamer.Paddle * 0.5f);
                Part(PrimitiveType.Cube, boat, new Vector3(x, 1.9f, 0f), new Vector3(PaddleSteamer.Paddle + 0.1f, 0.14f, PaddleSteamer.PaddleAlong * 2f + 0.3f), white, name: "Box top");
                Part(PrimitiveType.Cube, boat, new Vector3(side * (W * 0.5f + PaddleSteamer.Paddle + 0.03f), 1.3f, 0f), new Vector3(0.06f, 1.2f, PaddleSteamer.PaddleAlong * 2f + 0.3f), red,
                    name: "Box side");
                var wheel = wheels[s] = Shapes3D.Group(boat, "Paddle wheel", new Vector3(x, 0.7f, 0f));
                for (var p = 0; p < 8; p++)
                    Part(PrimitiveType.Cube, wheel, Quaternion.Euler(p * 45f, 0f, 0f) * new Vector3(0f, 0.95f, 0f), new Vector3(PaddleSteamer.Paddle * 0.9f, 0.5f, 0.12f), palette.Planks,
                        Quaternion.Euler(p * 45f, 0f, 0f), "Paddle");
                Part(PrimitiveType.Cube, boat, new Vector3(x, 0.05f, 0f), new Vector3(PaddleSteamer.Paddle + 0.6f, 0.04f, 3f), palette.foam, name: "Churn");
            }

            wake = Part(PrimitiveType.Cube, Root, Vector3.zero, Vector3.one, palette.foam, name: "Wake").transform;
            var puff = palette.Spray(new Color(0.6f, 0.6f, 0.62f, 0.8f), 0.1f, 0.05f, 0.4f, 1f);
            for (var i = 0; i < smoke.Length; i++) smoke[i] = Part(PrimitiveType.Sphere, boat, Vector3.zero, Vector3.one, puff, name: "Smoke").transform;
        }

        public override void Pose(double seconds)
        {
            var t = (float)seconds;
            var (position, heading) = steamer.Pose(seconds);
            boat.SetPositionAndRotation(SimSpace.ToWorld(position, 0.04f * Mathf.Sin(t * 1.8f)), Facing(heading) * Quaternion.Euler(0.8f * Mathf.Sin(t * 1.3f), 0f, 1.2f * Mathf.Sin(t * 1.1f)));
            var turn = steamer.Offset(seconds) / 0.95f * Mathf.Rad2Deg;
            foreach (var wheel in wheels) wheel.localRotation = Quaternion.Euler(turn, 0f, 0f);

            var speed = steamer.Speed(seconds);
            var share = Mathf.Abs(speed) / Mathf.Max(0.1f, steamer.TopSpeed);
            Built.Show(wake, share > 0.1f);
            var back = -Mathf.Sign(speed) * heading;
            var length = PaddleSteamer.WakeLength * share;
            wake.SetPositionAndRotation(SimSpace.ToWorld(position + back * (PaddleSteamer.Length * 0.5f + length * 0.5f), 0.04f), Facing(heading));
            wake.localScale = new Vector3(PaddleSteamer.Width + 0.8f, 0.04f, Mathf.Max(0.1f, length));

            for (var i = 0; i < smoke.Length; i++)
            {
                var age = Mathf.Repeat(t * 0.5f + i / (float)smoke.Length, 1f);
                smoke[i].localPosition = new Vector3((i % 2 == 0 ? -0.6f : 0.6f) + age * 0.3f, 4.8f + age * 3f, -0.6f - age * 2f);
                smoke[i].localScale = Vector3.one * (0.5f + age * 1.4f);
            }
        }
    }
}
