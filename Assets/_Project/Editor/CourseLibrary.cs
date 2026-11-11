using RushAhead.Content;
using RushAhead.Sim;
using UnityEditor;
using UnityEngine;

namespace RushAhead.EditorTools
{
    /// <summary>
    /// The shipped courses' starting layouts. Each is created once; after that the asset is the course, edited in the
    /// Inspector and by dragging its points in the Scene view, and running this again never overwrites it.
    /// Delete an asset to get its original layout back.
    /// </summary>
    public static class CourseLibrary
    {
        const string Folder = "Assets/_Project/Content/Courses";

        /// <summary>The courses in menu order. Their order is part of the wire format: only ever append.</summary>
        static readonly (string file, System.Action<CourseDef> layout)[] Layouts =
        {
            ("OtterCreek", OtterCreek), ("MirrorLake", MirrorLake), ("BeaverBend", BeaverBend),
            ("ReedMarsh", ReedMarsh), ("WhirlpoolReach", WhirlpoolReach), ("WhitewaterRun", WhitewaterRun),
            ("GlacierGate", GlacierGate), ("RedRockRun", RedRockRun), ("ThunderRapids", ThunderRapids),
            ("IcefallChute", IcefallChute), ("DynamiteGulch", DynamiteGulch), ("DevilsStaircase", DevilsStaircase),
            ("SnapjawSwamp", SnapjawSwamp),
        };

        /// <summary>The shipped courses as they stand, in menu order (null for any not made yet).</summary>
        public static CourseDef[] Shipped() =>
            System.Array.ConvertAll(Layouts, entry => AssetDatabase.LoadAssetAtPath<CourseDef>($"{Folder}/{entry.file}.asset"));

        public static CourseDef[] CreateDefaults()
        {
            ContentBootstrap.EnsureFolder(Folder);
            return System.Array.ConvertAll(Layouts, entry => Create(entry.file, entry.layout));
        }

        /// <summary>
        /// Puts every shipped course back to its layout here, overwriting edits made in the Inspector but keeping the
        /// assets themselves, so the scene and everything else pointing at them still does. For batchmode:
        /// <c>-executeMethod RushAhead.EditorTools.CourseLibrary.Rebuild</c>.
        /// </summary>
        public static void Rebuild()
        {
            foreach (var course in CreateDefaults())
            {
                var entry = System.Array.Find(Layouts, e => e.file == course.name);
                var fresh = ScriptableObject.CreateInstance<CourseDef>();
                entry.layout(fresh);
                EditorUtility.CopySerialized(fresh, course);
                course.name = entry.file;
                Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(course);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[RushAhead] Courses rebuilt from CourseLibrary.");
        }

        static CourseDef Create(string file, System.Action<CourseDef> layout)
        {
            var path = $"{Folder}/{file}.asset";
            var course = AssetDatabase.LoadAssetAtPath<CourseDef>(path);
            if (course != null) return course;

            course = ScriptableObject.CreateInstance<CourseDef>();
            layout(course);
            AssetDatabase.CreateAsset(course, path);
            return course;
        }

        /// <summary>
        /// The first course, about 350 m and the gentlest: a wide start pool, a left bend into a rock slalom, a right
        /// bend into a helpful current, a pool full of barrels, a narrow gorge where the water pushes back, then a long
        /// run to the finish. The fewest obstacles, a taste of most kinds.
        /// </summary>
        static void OtterCreek(CourseDef c)
        {
            c.displayName = "Otter Creek";
            c.blurb = "Weave the rocks, ride the current and shove through the barrels. New obstacles every round.";
            c.flow = 5.2f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(15f, 0f, 16f),
                new RiverPoint(31.5f, 4.5f, 14f),
                new RiverPoint(45f, 15f, 13f, -0.45f), // slalom: the line weaves round a rock at each of these three
                new RiverPoint(52.5f, 30f, 14f, 0.45f),
                new RiverPoint(54f, 46.5f, 14f, -0.45f),
                new RiverPoint(63f, 60f, 12f),
                new RiverPoint(78f, 66f, 11f),
                new RiverPoint(94.5f, 64.5f, 12f),
                new RiverPoint(109.5f, 55.5f, 18f), // the barrel pool
                new RiverPoint(120f, 42f, 9f), // the gorge
                new RiverPoint(129f, 30f, 12f),
                new RiverPoint(142.5f, 24f, 14f),
                new RiverPoint(159f, 22.5f, 16f),
                new RiverPoint(172.5f, 22.5f, 16f),
                new RiverPoint(188f, 20f, 15f),
                new RiverPoint(204f, 14f, 14f),
                new RiverPoint(220f, 10f, 14f, 0.3f),
                new RiverPoint(236f, 10f, 14f),
                new RiverPoint(252f, 14f, 14f, -0.3f),
                new RiverPoint(268f, 20f, 14f),
                new RiverPoint(284f, 22f, 15f),
                new RiverPoint(300f, 22f, 16f),
                new RiverPoint(316f, 22f, 16f),
            };

            Widen(c, 14, 17, 26f); // room for an island
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(3) - 6f, s.Near(6) + 4f, s.Near(9) + 12f, s.Near(13) + 4f, s.Near(18), s.Finish };
            c.islands = new[] { s.Island(3, 40f, 3.5f, 7.5f, 1f) };
            c.rocks = new[]
            {
                s.Rock(s.Near(3), 3.8f, 1.4f), s.Rock(s.Near(4), -3.8f, 1.5f), s.Rock(s.Near(5), 3.8f, 1.3f),
                s.Rock(s.Near(11) + 6f, 4.6f, 1.1f), // a boulder on the bend after the gorge
            };

            var pool = s.Near(9);
            c.barrels = new[]
            {
                s.Spot(pool - 8f, -2f), s.Spot(pool - 8f, 1.5f), s.Spot(pool - 3f, 0f), s.Spot(pool - 3f, -4f), s.Spot(pool - 3f, 4f),
                s.Spot(pool + 2f, 2f), s.Spot(pool + 2f, -2f), s.Spot(pool + 6f, 0f),
            };

            c.currents = new[]
            {
                new CurrentZone(s.Near(6) + 2f, s.Near(8) + 6f, 1.35f), // ride it
                new CurrentZone(s.Near(10) - 6f, s.Near(11) + 2f, -0.75f), // the gorge pushes back
            };

            // Obstacles: a fresh layout every round, dealt from these.
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Wheel), new ObstacleChance(ObstacleKind.Crocodile),
                new ObstacleChance(ObstacleKind.SwingingLog), new ObstacleChance(ObstacleKind.Geyser),
                new ObstacleChance(ObstacleKind.Bumper), new ObstacleChance(ObstacleKind.Rockslide),
                new ObstacleChance(ObstacleKind.Hippo), new ObstacleChance(ObstacleKind.Weir),
                new ObstacleChance(ObstacleKind.FallingTree), new ObstacleChance(ObstacleKind.Turtle),
                new ObstacleChance(ObstacleKind.Pelican), new ObstacleChance(ObstacleKind.GooseGang),
                new ObstacleChance(ObstacleKind.LeapingFrogs),
            };
            c.minObstacles = 6;
            c.maxObstacles = 9;
        }

        static RockSpot Rock(Vector2 at, float radius) => new(at.x, at.y, radius);

        /// <summary>Widens the river at these control points, for an island: only the width, so its line and length stay the same.</summary>
        static void Widen(CourseDef c, int from, int to, float width)
        {
            for (var i = from; i <= to; i++) c.river[i].width = Mathf.Max(c.river[i].width, width);
        }

        /// <summary>Lays a course's pieces out by distance down its river and distance across it (positive is the left bank).</summary>
        sealed class Survey
        {
            readonly CourseDef def;

            public Survey(CourseDef def)
            {
                this.def = def;
                River = new River(def.river);
            }

            public River River { get; }

            /// <summary>How far down the river a control point is.</summary>
            public float Near(int point) => River.Project(def.river[point].position).Distance;

            public float Finish => River.Length - 16f;

            public Vector2 Spot(float distance, float across)
            {
                var at = River.At(distance);
                return at.Centre + at.Left * across;
            }

            public RockSpot Rock(float distance, float across, float radius) => CourseLibrary.Rock(Spot(distance, across), radius);

            /// <summary>A log, turned <paramref name="toFlowDeg"/> from the way the water flows there (90 lies straight across).</summary>
            public LogSpot Log(float distance, float across, float toFlowDeg, float length)
            {
                var at = River.At(distance);
                return new LogSpot(Spot(distance, across), Mathf.Atan2(at.Tangent.y, at.Tangent.x) * Mathf.Rad2Deg + toFlowDeg, length);
            }

            public WhirlpoolSpot Whirlpool(float distance, float across, float radius, float spin, float pull) =>
                new(Spot(distance, across), radius, spin, pull);

            /// <summary>
            /// An island in the free water after checkpoint <paramref name="gate"/> (-1 for the start), clear of where rafts
            /// come back and their run-up, and of the next checkpoint: as long as fits up to <paramref name="length"/>, set
            /// across so the fast line on <paramref name="fastSide"/> (+1 left) is <paramref name="fastWidth"/> wide where
            /// the river is narrowest along it, and the safe line takes the rest.
            /// </summary>
            public IslandSpot Island(int gate, float length, float halfWidth, float fastWidth, float fastSide)
            {
                var start = (gate < 0 ? def.startDistance : def.gates[gate]) + 24f;
                var end = (gate + 1 < def.gates.Length ? def.gates[gate + 1] : Finish) - 10f;
                length = Mathf.Min(length, end - start);
                var nose = start + (end - start - length) * 0.5f;
                var half = float.MaxValue; // set by its widest stretch: the river narrows towards its ends as the island does
                for (var d = nose + length * 0.25f; d <= nose + length * 0.75f; d += 1f) half = Mathf.Min(half, River.At(d).HalfWidth);
                return new IslandSpot(nose, length, fastSide * (half - fastWidth - halfWidth), halfWidth, fastSide);
            }
        }

        /// <summary>Wide water with three islands to pick a way round: room for the obstacles that need it (cannons, the ferry).</summary>
        static void MirrorLake(CourseDef c)
        {
            c.displayName = "Mirror Lake";
            c.blurb = "Wide water round three islands, never calm for long. New obstacles every round.";
            c.scene = CourseScene.Lake;
            c.starsToUnlock = 1;
            c.flow = 4.4f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 18f),
                new RiverPoint(16f, 0f, 20f),
                new RiverPoint(32f, 5f, 24f),
                new RiverPoint(50f, 14f, 30f, 0.45f), // island A to the right of the line
                new RiverPoint(70f, 18f, 30f),
                new RiverPoint(90f, 14f, 30f, -0.45f), // island B to the left
                new RiverPoint(110f, 6f, 28f),
                new RiverPoint(128f, 2f, 28f, 0.45f), // island C to the right
                new RiverPoint(146f, 4f, 26f),
                new RiverPoint(162f, 10f, 22f),
                new RiverPoint(178f, 14f, 20f),
                new RiverPoint(194f, 14f, 18f),
                new RiverPoint(208f, 14f, 18f),
                new RiverPoint(224f, 12f, 20f),
                new RiverPoint(240f, 6f, 22f),
                new RiverPoint(256f, 2f, 22f, 0.4f),
                new RiverPoint(272f, 2f, 22f),
                new RiverPoint(288f, 6f, 20f, -0.4f),
                new RiverPoint(304f, 12f, 18f),
                new RiverPoint(320f, 14f, 18f),
                new RiverPoint(334f, 14f, 18f),
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(2) + 2f, s.Near(5) + 12f, s.Near(8), s.Near(11) + 4f, s.Near(15), s.Finish };
            c.islands = new[] { s.Island(0, 42f, 4.5f, 8f, -1f) };
            c.rocks = new[] { s.Rock(s.Near(7), -4f, 3.2f) }; // the first two rocky islets are a real island now
            var drift = s.Near(9);
            c.barrels = new[] { s.Spot(drift + 4f, -4f), s.Spot(drift + 4f, 3f), s.Spot(drift + 9f, 0f), s.Spot(drift + 9f, -6f), s.Spot(drift + 14f, 4f) };

            // Obstacles: a fresh layout every round, dealt from these.
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Geyser, 1.5f), new ObstacleChance(ObstacleKind.Crocodile, 1.2f),
                new ObstacleChance(ObstacleKind.Hippo, 1.2f), new ObstacleChance(ObstacleKind.Ferry),
                new ObstacleChance(ObstacleKind.Cannon), new ObstacleChance(ObstacleKind.Bumper),
                new ObstacleChance(ObstacleKind.Bear), new ObstacleChance(ObstacleKind.SwingingLog, 0.7f),
                new ObstacleChance(ObstacleKind.Turtle), new ObstacleChance(ObstacleKind.PiranhaShoal),
                new ObstacleChance(ObstacleKind.Pelican), new ObstacleChance(ObstacleKind.NetBoat),
                new ObstacleChance(ObstacleKind.Waterspout), // the paddle steamer's wide water is the island's now
                new ObstacleChance(ObstacleKind.LeapingFrogs), new ObstacleChance(ObstacleKind.GooseGang),
                new ObstacleChance(ObstacleKind.Kraken),
            };
            c.minObstacles = 7;
            c.maxObstacles = 10;
        }

        /// <summary>Switchbacks through beaver country, drifting logs and a log jam in the big pool.</summary>
        static void BeaverBend(CourseDef c)
        {
            c.displayName = "Beaver Bend";
            c.blurb = "Switchbacks through beaver country, logs adrift and a jam to bash through. New obstacles every round.";
            c.scene = CourseScene.Creek;
            c.starsToUnlock = 2;
            c.flow = 6f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 14f),
                new RiverPoint(16f, 0f, 14f),
                new RiverPoint(30f, 8f, 13f),
                new RiverPoint(38f, 22f, 12f),
                new RiverPoint(37f, 38f, 12f),
                new RiverPoint(44f, 53f, 12f),
                new RiverPoint(58f, 60f, 13f),
                new RiverPoint(74f, 56f, 12f),
                new RiverPoint(84f, 44f, 12f),
                new RiverPoint(98f, 37f, 14f),
                new RiverPoint(115f, 40f, 18f), // the log jam
                new RiverPoint(128f, 50f, 13f),
                new RiverPoint(136f, 64f, 12f),
                new RiverPoint(150f, 73f, 13f),
                new RiverPoint(166f, 74f, 14f),
                new RiverPoint(182f, 72f, 14f),
                new RiverPoint(196f, 72f, 14f),
                new RiverPoint(212f, 70f, 14f),
                new RiverPoint(228f, 64f, 13f),
                new RiverPoint(242f, 56f, 13f, 0.3f),
                new RiverPoint(256f, 52f, 13f),
                new RiverPoint(270f, 54f, 14f, -0.3f),
                new RiverPoint(286f, 60f, 14f),
                new RiverPoint(302f, 62f, 14f),
                new RiverPoint(316f, 62f, 14f),
            };

            Widen(c, 14, 17, 26f); // room for an island
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(3) - 6f, s.Near(7) + 4f, s.Near(10) + 12f, s.Near(13) + 2f, s.Near(17) + 4f, s.Near(21), s.Finish };
            c.islands = new[] { s.Island(3, 40f, 3.5f, 7.5f, -1f) };
            c.rocks = new[] { s.Rock(s.Near(6), -4.2f, 1.2f), s.Rock(s.Near(12), 3.8f, 1.1f) };
            var jam = s.Near(10);
            c.logs = new[]
            {
                s.Log(s.Near(3) + 2f, 2f, 90f, 3.5f), s.Log(s.Near(4) + 3f, -2.5f, 75f, 4f), s.Log(s.Near(7) + 1f, 0f, 60f, 3.5f),
                s.Log(jam - 4f, -4f, 20f, 4f), s.Log(jam - 2f, 3f, -35f, 4.5f), s.Log(jam + 2f, -1f, 80f, 4f), s.Log(jam + 5f, 5f, 10f, 3.5f),
                s.Log(jam + 7f, -5f, 50f, 4f),
            };
            c.currents = new[] { new CurrentZone(s.Near(4), s.Near(6), 1.05f), new CurrentZone(s.Near(11), s.Near(13), 0.9f) };

            // Obstacles: a fresh layout every round, dealt from these.
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Rockslide), new ObstacleChance(ObstacleKind.SwingingLog, 1.3f),
                new ObstacleChance(ObstacleKind.Wheel), new ObstacleChance(ObstacleKind.Sluice),
                new ObstacleChance(ObstacleKind.Sweeper), new ObstacleChance(ObstacleKind.Crocodile),
                new ObstacleChance(ObstacleKind.Bear), new ObstacleChance(ObstacleKind.Hippo),
                new ObstacleChance(ObstacleKind.Bumper), new ObstacleChance(ObstacleKind.Rapids, 0.6f),
                new ObstacleChance(ObstacleKind.Weir, 0.6f),
                new ObstacleChance(ObstacleKind.FallingTree), new ObstacleChance(ObstacleKind.BeaverDam, 1.3f),
                new ObstacleChance(ObstacleKind.Mudslide), new ObstacleChance(ObstacleKind.Turtle),
                new ObstacleChance(ObstacleKind.LogFlume), new ObstacleChance(ObstacleKind.MillRace),
                new ObstacleChance(ObstacleKind.Drawbridge),
            };
            c.minObstacles = 8;
            c.maxObstacles = 11;
        }

        /// <summary>Narrow channels through the reeds, where there's no steering round a hippo: brace or dodge.</summary>
        static void ReedMarsh(CourseDef c)
        {
            c.displayName = "Reed Marsh";
            c.blurb = "Narrow channels with nowhere to hide, and a whirlpool in the big pool. New obstacles every round.";
            c.scene = CourseScene.Lake;
            c.starsToUnlock = 4;
            c.flow = 5.2f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 14f),
                new RiverPoint(16f, 0f, 12f),
                new RiverPoint(30f, -6f, 10f),
                new RiverPoint(44f, -6f, 9f),
                new RiverPoint(57f, 2f, 9f),
                new RiverPoint(66f, 15f, 10f),
                new RiverPoint(79f, 25f, 11f),
                new RiverPoint(96f, 26f, 24f, 0.55f), // the pool: round the whirlpool's left side
                new RiverPoint(113f, 20f, 12f),
                new RiverPoint(125f, 10f, 9f),
                new RiverPoint(139f, 5f, 9f),
                new RiverPoint(153f, 9f, 10f),
                new RiverPoint(166f, 18f, 12f),
                new RiverPoint(180f, 21f, 14f),
                new RiverPoint(195f, 21f, 14f),
                new RiverPoint(210f, 19f, 12f),
                new RiverPoint(225f, 13f, 10f),
                new RiverPoint(240f, 9f, 10f),
                new RiverPoint(255f, 11f, 11f),
                new RiverPoint(270f, 17f, 12f),
                new RiverPoint(285f, 21f, 13f),
                new RiverPoint(300f, 21f, 14f),
                new RiverPoint(314f, 21f, 14f),
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(2) + 2f, s.Near(6), s.Near(8) + 6f, s.Near(11), s.Near(15) + 2f, s.Near(19), s.Finish };
            c.rocks = new[] { s.Rock(s.Near(12) + 2f, -4f, 1f) };
            var pool = s.Near(7);
            c.barrels = new[] { s.Spot(pool - 6f, 6f), s.Spot(pool + 5f, 7f), s.Spot(pool + 2f, -8f) };
            c.currents = new[] { new CurrentZone(s.Near(2), s.Near(5), 0.75f), new CurrentZone(s.Near(9), s.Near(11), -0.6f) };
            c.whirlpools = new[] { s.Whirlpool(pool, -3f, 6f, 1.95f, 0.9f) };

            // Obstacles: a fresh layout every round, dealt from these.
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Hippo, 1.3f), new ObstacleChance(ObstacleKind.Crocodile, 1.3f),
                new ObstacleChance(ObstacleKind.Geyser), new ObstacleChance(ObstacleKind.SwingingLog),
                new ObstacleChance(ObstacleKind.Weir, 0.7f), new ObstacleChance(ObstacleKind.Bear),
                new ObstacleChance(ObstacleKind.Rapids, 0.5f),
                new ObstacleChance(ObstacleKind.ElectricEels), new ObstacleChance(ObstacleKind.Pelican),
                new ObstacleChance(ObstacleKind.FallingTree), new ObstacleChance(ObstacleKind.BeaverDam),
                new ObstacleChance(ObstacleKind.LeapingFrogs), new ObstacleChance(ObstacleKind.GooseGang),
                new ObstacleChance(ObstacleKind.StormCloud),
            };
            c.minObstacles = 6;
            c.maxObstacles = 9;
        }

        /// <summary>Fast water past three whirlpools and a rock slalom, then a long run to the finish.</summary>
        static void WhirlpoolReach(CourseDef c)
        {
            c.displayName = "Whirlpool Reach";
            c.blurb = "Three whirlpools in fast water, then a long run of white water. New obstacles every round.";
            c.scene = CourseScene.Rapids;
            c.starsToUnlock = 6;
            c.flow = 7.2f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(18f, 0f, 16f),
                new RiverPoint(36f, -5f, 15f),
                new RiverPoint(54f, -3f, 20f, -0.5f), // whirlpool to the left: keep right
                new RiverPoint(72f, 6f, 16f),
                new RiverPoint(88f, 12f, 15f),
                new RiverPoint(106f, 9f, 20f, 0.5f), // whirlpool to the right: keep left
                new RiverPoint(124f, 0f, 16f),
                new RiverPoint(142f, -4f, 20f, -0.5f),
                new RiverPoint(160f, 0f, 15f),
                new RiverPoint(175f, 10f, 13f, -0.4f), // slalom
                new RiverPoint(187f, 23f, 12f, 0.4f),
                new RiverPoint(196f, 38f, 12f, -0.4f),
                new RiverPoint(204f, 53f, 13f),
                new RiverPoint(210f, 67f, 14f),
                new RiverPoint(214f, 80f, 14f),
                new RiverPoint(216f, 96f, 14f),
                new RiverPoint(222f, 111f, 15f),
                new RiverPoint(232f, 124f, 16f, 0.35f),
                new RiverPoint(244f, 135f, 16f),
                new RiverPoint(256f, 146f, 16f, -0.35f),
                new RiverPoint(266f, 160f, 15f),
                new RiverPoint(272f, 175f, 15f),
                new RiverPoint(274f, 190f, 16f),
                new RiverPoint(274f, 204f, 16f),
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(2), s.Near(5) + 2f, s.Near(7) + 4f, s.Near(9) + 2f, s.Near(14), s.Near(18), s.Finish };
            c.rocks = new[] { s.Rock(s.Near(10), 3.2f, 1.2f), s.Rock(s.Near(11), -3f, 1.1f), s.Rock(s.Near(12), 3f, 1.1f) };
            c.logs = new[] { s.Log(s.Near(6) + 8f, 5f, 30f, 3.5f), s.Log(s.Near(8) - 6f, -4f, 70f, 3f) };
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(2) + 6f, 1.5f), new CurrentZone(s.Near(9), s.Near(11), 1.8f),
                new CurrentZone(s.Near(12), s.Near(13) + 4f, -0.9f),
            };
            c.whirlpools = new[]
            {
                s.Whirlpool(s.Near(3), 3.5f, 5.5f, 2.1f, 1.05f), s.Whirlpool(s.Near(6), -3.5f, 6f, -2.4f, 1.2f),
                s.Whirlpool(s.Near(8), 3.5f, 5.5f, 2.7f, 1.35f),
            };

            // Obstacles: a fresh layout every round, dealt from these.
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Wheel), new ObstacleChance(ObstacleKind.Cannon),
                new ObstacleChance(ObstacleKind.Geyser), new ObstacleChance(ObstacleKind.Bear),
                new ObstacleChance(ObstacleKind.SwingingLog), new ObstacleChance(ObstacleKind.Rapids, 0.7f),
                new ObstacleChance(ObstacleKind.Rockslide), new ObstacleChance(ObstacleKind.Sluice),
                new ObstacleChance(ObstacleKind.Weir, 0.6f), new ObstacleChance(ObstacleKind.Sweeper),
                new ObstacleChance(ObstacleKind.Bumper),
                new ObstacleChance(ObstacleKind.Waterspout), new ObstacleChance(ObstacleKind.IceFloes),
                new ObstacleChance(ObstacleKind.Mudslide), new ObstacleChance(ObstacleKind.ElectricEels),
                new ObstacleChance(ObstacleKind.Kraken), new ObstacleChance(ObstacleKind.TidalBore),
                new ObstacleChance(ObstacleKind.ChainedMines), new ObstacleChance(ObstacleKind.WreckingBall),
            };
            c.minObstacles = 9;
            c.maxObstacles = 12;
        }

        /// <summary>The longest and hardest: every kind of obstacle, the most of them, in the fastest water.</summary>
        static void WhitewaterRun(CourseDef c)
        {
            c.displayName = "Whitewater Run";
            c.blurb = "The big one: the fastest water, and every kind of obstacle there is. New obstacles every round.";
            c.scene = CourseScene.Rapids;
            c.starsToUnlock = 8;
            c.flow = 8.4f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(18f, 0f, 15f),
                new RiverPoint(34f, 8f, 14f),
                new RiverPoint(46f, 22f, 12f, 0.4f), // slalom
                new RiverPoint(52f, 38f, 12f, -0.4f),
                new RiverPoint(62f, 52f, 13f, 0.4f),
                new RiverPoint(78f, 60f, 13f),
                new RiverPoint(96f, 60f, 14f),
                new RiverPoint(112f, 52f, 22f, -0.5f), // whirlpool to the left: keep right
                new RiverPoint(124f, 40f, 10f), // the gorge
                new RiverPoint(134f, 26f, 10f),
                new RiverPoint(148f, 18f, 13f),
                new RiverPoint(166f, 16f, 18f), // the log pool
                new RiverPoint(184f, 20f, 13f),
                new RiverPoint(198f, 30f, 12f),
                new RiverPoint(208f, 44f, 12f, 0.4f),
                new RiverPoint(222f, 54f, 13f),
                new RiverPoint(240f, 58f, 18f, 0.5f), // whirlpool to the right: keep left
                new RiverPoint(258f, 56f, 15f),
                new RiverPoint(274f, 54f, 16f),
                new RiverPoint(290f, 54f, 16f),
                new RiverPoint(306f, 52f, 15f),
                new RiverPoint(322f, 46f, 14f),
                new RiverPoint(337f, 38f, 14f, 0.35f),
                new RiverPoint(352f, 34f, 15f),
                new RiverPoint(368f, 36f, 15f, -0.35f),
                new RiverPoint(384f, 42f, 15f),
                new RiverPoint(400f, 46f, 16f),
                new RiverPoint(416f, 46f, 16f),
                new RiverPoint(430f, 46f, 16f),
            };

            Widen(c, 21, 23, 26f); // room for an island
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(3) - 6f, s.Near(6) + 2f, s.Near(9) + 4f, s.Near(12) + 8f, s.Near(16), s.Near(20), s.Near(24), s.Finish };
            c.islands = new[] { s.Island(5, 38f, 3.5f, 7.5f, 1f) };
            c.rocks = new[]
            {
                s.Rock(s.Near(3), -3f, 1.1f), s.Rock(s.Near(4), 3f, 1.1f), s.Rock(s.Near(5), -3.2f, 1.2f), s.Rock(s.Near(15), -3f, 1f),
            };
            var pool = s.Near(12);
            c.logs = new[]
            {
                s.Log(s.Near(2) + 4f, 2f, 70f, 3.5f), s.Log(pool - 5f, -3f, 40f, 4f), s.Log(pool - 1f, 4f, -60f, 4.5f), s.Log(pool + 3f, -5f, 85f, 4f),
                s.Log(pool + 6f, 2f, 15f, 3.5f),
            };
            c.barrels = new[] { s.Spot(pool, 0f), s.Spot(pool + 4f, -2f) };
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(3), 1.65f), new CurrentZone(s.Near(5), s.Near(7), 1.8f),
                new CurrentZone(s.Near(9) - 2f, s.Near(10) + 4f, -1.05f), new CurrentZone(s.Near(13), s.Near(16), 1.5f),
            };
            c.whirlpools = new[] { s.Whirlpool(s.Near(8), 4f, 6.5f, -2.7f, 1.35f), s.Whirlpool(s.Near(17), -4f, 6f, 2.4f, 1.2f) };

            // Obstacles: a fresh layout every round, dealt from these.
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Wheel), new ObstacleChance(ObstacleKind.Crocodile),
                new ObstacleChance(ObstacleKind.SwingingLog), new ObstacleChance(ObstacleKind.Geyser),
                new ObstacleChance(ObstacleKind.Rockslide), new ObstacleChance(ObstacleKind.Hippo),
                new ObstacleChance(ObstacleKind.Sluice), new ObstacleChance(ObstacleKind.Sweeper),
                new ObstacleChance(ObstacleKind.Cannon), new ObstacleChance(ObstacleKind.Rapids, 0.7f),
                new ObstacleChance(ObstacleKind.Weir, 0.7f), new ObstacleChance(ObstacleKind.Bumper),
                new ObstacleChance(ObstacleKind.Ferry), new ObstacleChance(ObstacleKind.Bear),
                new ObstacleChance(ObstacleKind.FallingTree), new ObstacleChance(ObstacleKind.Turtle),
                new ObstacleChance(ObstacleKind.PiranhaShoal), new ObstacleChance(ObstacleKind.Waterspout),
                new ObstacleChance(ObstacleKind.ElectricEels), new ObstacleChance(ObstacleKind.Mudslide),
                new ObstacleChance(ObstacleKind.IceFloes), new ObstacleChance(ObstacleKind.BeaverDam),
                new ObstacleChance(ObstacleKind.Pelican), new ObstacleChance(ObstacleKind.NetBoat),
                new ObstacleChance(ObstacleKind.GiantFan), new ObstacleChance(ObstacleKind.CargoCrane),
                new ObstacleChance(ObstacleKind.StormCloud), new ObstacleChance(ObstacleKind.Drawbridge),
                new ObstacleChance(ObstacleKind.JumpRamp), new ObstacleChance(ObstacleKind.ChainedMines),
            };
            c.minObstacles = 11;
            c.maxObstacles = 16;
        }

        // Phase 17: six harder courses in faster water, in two new places (glacier and canyon) and the rapids.

        /// <summary>Into the ice: a lake of floes, a gorge between ice walls, and the glacier shedding blocks.</summary>
        static void GlacierGate(CourseDef c)
        {
            c.displayName = "Glacier Gate";
            c.blurb = "Turquoise meltwater, ice floes and a glacier that drops blocks into the river. New obstacles every round.";
            c.scene = CourseScene.Glacier;
            c.starsToUnlock = 10;
            c.flow = 9f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(17f, 0f, 16f),
                new RiverPoint(33.8f, 2.4f, 16f),
                new RiverPoint(49.6f, 8.7f, 17f),
                new RiverPoint(63.7f, 18.2f, 18f),
                new RiverPoint(77.1f, 28.7f, 22f), // the floe lake
                new RiverPoint(91.5f, 37.7f, 22f),
                new RiverPoint(107.5f, 43.5f, 22f),
                new RiverPoint(124.4f, 44.7f, 18f),
                new RiverPoint(141.2f, 41.8f, 16f),
                new RiverPoint(157.5f, 37.1f, 15f),
                new RiverPoint(174.1f, 33.5f, 14f),
                new RiverPoint(191.1f, 34.1f, 12f),
                new RiverPoint(207.3f, 39.4f, 11f), // the ice gorge
                new RiverPoint(222.3f, 47.4f, 11f),
                new RiverPoint(237.3f, 55.4f, 12f),
                new RiverPoint(253.5f, 60.6f, 14f),
                new RiverPoint(270.5f, 61.2f, 15f),
                new RiverPoint(287.1f, 57.7f, 16f),
                new RiverPoint(303.3f, 52.4f, 16f),
                new RiverPoint(319.9f, 48.9f, 17f),
                new RiverPoint(336.9f, 49.5f, 18f),
                new RiverPoint(353.2f, 54.2f, 18f, -0.5f), // whirlpool to the left: keep right
                new RiverPoint(369f, 60.5f, 16f),
                new RiverPoint(385f, 66.3f, 15f),
                new RiverPoint(401.6f, 69.9f, 15f),
                new RiverPoint(418.6f, 70.5f, 16f),
                new RiverPoint(435.6f, 69.9f, 16f),
                new RiverPoint(452.6f, 69.3f, 16f),
                new RiverPoint(469.6f, 68.7f, 16f),
            };

            Widen(c, 5, 7, 26f); // room for an island
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(4), s.Near(8), s.Near(12) + 4f, s.Near(16), s.Near(20), s.Near(24), s.Finish };
            c.islands = new[] { s.Island(0, 40f, 3.5f, 7.5f, 1f) };
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(3), 1.5f), new CurrentZone(s.Near(12), s.Near(15), 1.8f),
                new CurrentZone(s.Near(18), s.Near(20), -0.9f),
            };
            c.whirlpools = new[] { s.Whirlpool(s.Near(22), 4.5f, 5.5f, 2.2f, 1.1f) };
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.CalvingGlacier, 1.4f), new ObstacleChance(ObstacleKind.IceFloes, 1.3f),
                new ObstacleChance(ObstacleKind.TidalBore), new ObstacleChance(ObstacleKind.Kraken),
                new ObstacleChance(ObstacleKind.StormCloud), new ObstacleChance(ObstacleKind.JumpRamp),
                new ObstacleChance(ObstacleKind.Drawbridge), new ObstacleChance(ObstacleKind.WreckingBall),
                new ObstacleChance(ObstacleKind.GooseGang), new ObstacleChance(ObstacleKind.Rockslide),
                new ObstacleChance(ObstacleKind.SwingingLog), new ObstacleChance(ObstacleKind.FallingTree),
                new ObstacleChance(ObstacleKind.Geyser), new ObstacleChance(ObstacleKind.Rapids, 0.7f),
                new ObstacleChance(ObstacleKind.Weir, 0.6f), new ObstacleChance(ObstacleKind.Bumper),
                new ObstacleChance(ObstacleKind.Bear), new ObstacleChance(ObstacleKind.Sluice),
                new ObstacleChance(ObstacleKind.Waterspout),
            };
            c.minObstacles = 11;
            c.maxObstacles = 14;
        }

        /// <summary>Red sandstone walls: a basin, a narrow cut and the mine works along the banks.</summary>
        static void RedRockRun(CourseDef c)
        {
            c.displayName = "Red Rock Run";
            c.blurb = "Down a red-rock canyon past the mine works: cranes, flumes and dynamite. New obstacles every round.";
            c.scene = CourseScene.Canyon;
            c.starsToUnlock = 12;
            c.flow = 9.5f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(17f, 0f, 16f),
                new RiverPoint(33.7f, -3f, 15f),
                new RiverPoint(49f, -10.4f, 14f),
                new RiverPoint(62.4f, -20.9f, 13f),
                new RiverPoint(75.8f, -31.3f, 12f),
                new RiverPoint(91.1f, -38.8f, 12f),
                new RiverPoint(107.9f, -41.2f, 13f),
                new RiverPoint(124.8f, -39.4f, 14f),
                new RiverPoint(141.7f, -37f, 15f),
                new RiverPoint(158.7f, -37.6f, 20f), // the basin
                new RiverPoint(175f, -42.3f, 20f),
                new RiverPoint(190.5f, -49.2f, 18f),
                new RiverPoint(206.5f, -55f, 14f),
                new RiverPoint(223.4f, -56.8f, 12f),
                new RiverPoint(240.2f, -53.8f, 11f), // the narrows
                new RiverPoint(256.3f, -48.6f, 11f),
                new RiverPoint(273f, -45.1f, 12f),
                new RiverPoint(289.9f, -46.2f, 13f),
                new RiverPoint(305.7f, -52.6f, 14f),
                new RiverPoint(320.1f, -61.6f, 15f),
                new RiverPoint(335.1f, -69.6f, 16f),
                new RiverPoint(351.4f, -74.3f, 16f),
                new RiverPoint(368.4f, -75.5f, 15f),
                new RiverPoint(385.4f, -75.5f, 14f),
                new RiverPoint(402.3f, -77.2f, 14f),
                new RiverPoint(418.5f, -82.5f, 15f),
                new RiverPoint(434f, -89.4f, 16f),
                new RiverPoint(449.8f, -95.8f, 16f),
                new RiverPoint(465.5f, -102.2f, 16f),
                new RiverPoint(481.3f, -108.5f, 16f),
            };

            Widen(c, 9, 11, 26f); // room for an island
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(4), s.Near(8), s.Near(12), s.Near(16) + 4f, s.Near(20), s.Near(24), s.Finish };
            c.islands = new[] { s.Island(1, 38f, 3.5f, 7.5f, -1f) };
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(3), 1.6f), new CurrentZone(s.Near(14), s.Near(17), 2f),
                new CurrentZone(s.Near(21), s.Near(23), -1f),
            };
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.DynamiteFloat, 1.4f), new ObstacleChance(ObstacleKind.Rockslide, 1.3f),
                new ObstacleChance(ObstacleKind.Mudslide), new ObstacleChance(ObstacleKind.CargoCrane),
                new ObstacleChance(ObstacleKind.WreckingBall), new ObstacleChance(ObstacleKind.LogFlume),
                new ObstacleChance(ObstacleKind.MillRace), new ObstacleChance(ObstacleKind.GiantFan),
                new ObstacleChance(ObstacleKind.JumpRamp), new ObstacleChance(ObstacleKind.ChainedMines),
                new ObstacleChance(ObstacleKind.PaddleSteamer), new ObstacleChance(ObstacleKind.Cannon),
                new ObstacleChance(ObstacleKind.Geyser), new ObstacleChance(ObstacleKind.Rapids, 0.7f),
                new ObstacleChance(ObstacleKind.Weir, 0.6f), new ObstacleChance(ObstacleKind.Sluice),
                new ObstacleChance(ObstacleKind.Bear), new ObstacleChance(ObstacleKind.Pelican),
                new ObstacleChance(ObstacleKind.Crocodile), new ObstacleChance(ObstacleKind.Bumper),
            };
            c.minObstacles = 12;
            c.maxObstacles = 16;
        }

        /// <summary>Fast white water: an S-bend run, two whirlpools and a storm overhead.</summary>
        static void ThunderRapids(CourseDef c)
        {
            c.displayName = "Thunder Rapids";
            c.blurb = "White water under a stormy sky: krakens, mines and lightning. New obstacles every round.";
            c.scene = CourseScene.Rapids;
            c.starsToUnlock = 14;
            c.flow = 10f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(17f, 0f, 16f),
                new RiverPoint(33.7f, 3f, 15f),
                new RiverPoint(49f, 10.4f, 14f),
                new RiverPoint(63.1f, 19.9f, 13f, 0.25f), // S-bends
                new RiverPoint(78.4f, 27.4f, 13f, -0.25f),
                new RiverPoint(95f, 30.9f, 13f, 0.25f),
                new RiverPoint(111.9f, 32.7f, 14f),
                new RiverPoint(128.6f, 36.2f, 18f),
                new RiverPoint(143.8f, 43.7f, 20f, -0.5f), // whirlpool to the left: keep right
                new RiverPoint(157.6f, 53.7f, 16f),
                new RiverPoint(172f, 62.7f, 13f),
                new RiverPoint(188.2f, 67.9f, 11f),
                new RiverPoint(205.2f, 68.5f, 11f),
                new RiverPoint(222.1f, 67.3f, 12f),
                new RiverPoint(239.1f, 68.5f, 14f),
                new RiverPoint(255.1f, 74.3f, 16f),
                new RiverPoint(269.5f, 83.3f, 18f, 0.5f), // whirlpool to the right: keep left
                new RiverPoint(283.9f, 92.3f, 16f),
                new RiverPoint(299.9f, 98.2f, 14f),
                new RiverPoint(316.8f, 99.3f, 14f),
                new RiverPoint(333.8f, 98.2f, 15f),
                new RiverPoint(350.8f, 98.7f, 16f),
                new RiverPoint(367.1f, 103.4f, 16f),
                new RiverPoint(382.4f, 110.9f, 15f),
                new RiverPoint(397.9f, 117.8f, 14f),
                new RiverPoint(414.4f, 121.9f, 15f),
                new RiverPoint(431.3f, 123.7f, 16f),
                new RiverPoint(448.2f, 125.5f, 16f),
                new RiverPoint(465.1f, 127.2f, 16f),
                new RiverPoint(482f, 129f, 16f),
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(3) - 4f, s.Near(7), s.Near(11), s.Near(15), s.Near(19), s.Near(23), s.Near(27), s.Finish };
            c.rocks = System.Array.Empty<RockSpot>(); // at this speed a rock in the water is a wall: the bends are the slalom
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(3), 1.8f), new CurrentZone(s.Near(11), s.Near(13), -1f),
                new CurrentZone(s.Near(20), s.Near(23), 2f),
            };
            c.whirlpools = new[] { s.Whirlpool(s.Near(9), 4.5f, 6f, -2.6f, 1.3f), s.Whirlpool(s.Near(17), -4.5f, 6f, 2.5f, 1.25f) };
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Kraken), new ObstacleChance(ObstacleKind.TidalBore),
                new ObstacleChance(ObstacleKind.ChainedMines), new ObstacleChance(ObstacleKind.WreckingBall),
                new ObstacleChance(ObstacleKind.StormCloud, 1.3f), new ObstacleChance(ObstacleKind.Drawbridge),
                new ObstacleChance(ObstacleKind.MillRace), new ObstacleChance(ObstacleKind.LeapingFrogs),
                new ObstacleChance(ObstacleKind.GooseGang), new ObstacleChance(ObstacleKind.LogFlume),
                new ObstacleChance(ObstacleKind.Wheel), new ObstacleChance(ObstacleKind.Crocodile),
                new ObstacleChance(ObstacleKind.SwingingLog), new ObstacleChance(ObstacleKind.Geyser),
                new ObstacleChance(ObstacleKind.Rockslide), new ObstacleChance(ObstacleKind.Hippo),
                new ObstacleChance(ObstacleKind.Sluice), new ObstacleChance(ObstacleKind.Sweeper),
                new ObstacleChance(ObstacleKind.Rapids, 0.8f), new ObstacleChance(ObstacleKind.Weir, 0.7f),
                new ObstacleChance(ObstacleKind.Bumper), new ObstacleChance(ObstacleKind.Bear),
                new ObstacleChance(ObstacleKind.FallingTree), new ObstacleChance(ObstacleKind.Waterspout),
                new ObstacleChance(ObstacleKind.ElectricEels), new ObstacleChance(ObstacleKind.Pelican),
            };
            c.minObstacles = 11;
            c.maxObstacles = 14;
        }

        /// <summary>Two ice chutes either side of a meltwater lake, and a gorge under the icefall.</summary>
        /// <summary>
        /// The boss course: a slow, winding swamp river (about 460 m) with two islands, crocodiles, hippos and worse in
        /// the water, plenty of power-up crates, and Old Snapjaw chasing the last raft all the way down.
        /// </summary>
        static void SnapjawSwamp(CourseDef c)
        {
            c.displayName = "Snapjaw Swamp";
            c.blurb = "Something huge lives in this swamp, and it's hungry. Keep ahead of Old Snapjaw. New obstacles every round.";
            c.scene = CourseScene.Swamp;
            c.starsToUnlock = 22;
            c.flow = 8.5f;
            c.boss = true;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(16f, 0f, 16f),
                new RiverPoint(32f, 2f, 16f),
                new RiverPoint(48f, 6f, 15f),
                new RiverPoint(63.5f, 11f, 15f),
                new RiverPoint(79f, 15f, 16f), // the first island
                new RiverPoint(95f, 17f, 16f),
                new RiverPoint(111f, 16f, 16f),
                new RiverPoint(127f, 12f, 15f),
                new RiverPoint(142f, 6f, 14f),
                new RiverPoint(157f, -1f, 14f),
                new RiverPoint(172f, -6f, 15f),
                new RiverPoint(188f, -8f, 16f),
                new RiverPoint(204f, -7f, 16f),
                new RiverPoint(220f, -3f, 15f),
                new RiverPoint(235.5f, 3f, 14f),
                new RiverPoint(250.5f, 10f, 15f),
                new RiverPoint(266f, 15f, 16f), // the second island
                new RiverPoint(282f, 17f, 16f),
                new RiverPoint(298f, 16f, 16f),
                new RiverPoint(314f, 12f, 15f),
                new RiverPoint(329.5f, 6f, 14f),
                new RiverPoint(345f, 1f, 14f),
                new RiverPoint(361f, -2f, 15f),
                new RiverPoint(377f, -3f, 16f),
                new RiverPoint(393f, -2f, 16f),
                new RiverPoint(409f, 0f, 16f),
                new RiverPoint(425f, 1f, 16f),
                new RiverPoint(441f, 1f, 16f),
                new RiverPoint(457f, 1f, 16f),
                new RiverPoint(473f, 1f, 16f),
            };

            Widen(c, 5, 8, 26f); // room for the islands, and to come out of them
            Widen(c, 17, 20, 26f);
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(4), s.Near(8), s.Near(12), s.Near(16), s.Near(20), s.Near(24), s.Finish };
            c.islands = new[] { s.Island(0, 40f, 3.5f, 7.5f, 1f), s.Island(3, 40f, 3.5f, 7.5f, -1f) };
            c.currents = new[] { new CurrentZone(s.Near(9), s.Near(11), -0.9f), new CurrentZone(s.Near(21), s.Near(23), 1.2f) };
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Crocodile, 1.6f), new ObstacleChance(ObstacleKind.Hippo, 1.4f),
                new ObstacleChance(ObstacleKind.Turtle, 1.2f), new ObstacleChance(ObstacleKind.PiranhaShoal, 1.2f),
                new ObstacleChance(ObstacleKind.GooseGang), new ObstacleChance(ObstacleKind.LeapingFrogs, 1.2f),
                new ObstacleChance(ObstacleKind.Kraken), new ObstacleChance(ObstacleKind.Pelican),
                new ObstacleChance(ObstacleKind.ElectricEels), new ObstacleChance(ObstacleKind.FallingTree),
                new ObstacleChance(ObstacleKind.Mudslide), new ObstacleChance(ObstacleKind.SwingingLog),
                new ObstacleChance(ObstacleKind.NetBoat), new ObstacleChance(ObstacleKind.Bumper),
            };
            c.minObstacles = 8;
            c.maxObstacles = 11;
            c.extraCrates = 3;
        }

        static void IcefallChute(CourseDef c)
        {
            c.displayName = "Icefall Chute";
            c.blurb = "Narrow ice chutes, a meltwater lake and a gorge under the icefall. New obstacles every round.";
            c.scene = CourseScene.Glacier;
            c.starsToUnlock = 16;
            c.flow = 10.5f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(17f, 0f, 16f),
                new RiverPoint(33.9f, -1.8f, 16f),
                new RiverPoint(50.1f, -7f, 15f),
                new RiverPoint(65.1f, -15f, 13f),
                new RiverPoint(80.1f, -23f, 11f), // the first chute
                new RiverPoint(96.3f, -28.2f, 11f),
                new RiverPoint(113.2f, -29.4f, 12f),
                new RiverPoint(130.1f, -27.7f, 14f),
                new RiverPoint(147f, -25.9f, 20f), // the meltwater lake
                new RiverPoint(164f, -26.5f, 22f),
                new RiverPoint(180.5f, -30.6f, 20f),
                new RiverPoint(195.8f, -38f, 16f),
                new RiverPoint(210.2f, -47f, 13f),
                new RiverPoint(225.5f, -54.5f, 11f),
                new RiverPoint(242.1f, -58f, 10f), // the icefall gorge
                new RiverPoint(259.1f, -57.4f, 10f),
                new RiverPoint(276f, -55.7f, 11f),
                new RiverPoint(293f, -56.3f, 13f),
                new RiverPoint(309.2f, -61.5f, 15f),
                new RiverPoint(323.6f, -70.5f, 16f),
                new RiverPoint(337.3f, -80.5f, 18f, -0.5f), // whirlpool to the left: keep right
                new RiverPoint(352.4f, -88.5f, 18f),
                new RiverPoint(368.9f, -92.6f, 16f),
                new RiverPoint(385.8f, -93.8f, 15f),
                new RiverPoint(402.8f, -95f, 14f),
                new RiverPoint(419.4f, -98.5f, 14f),
                new RiverPoint(434.9f, -105.4f, 15f),
                new RiverPoint(449.7f, -113.9f, 16f),
                new RiverPoint(464.4f, -122.4f, 16f),
                new RiverPoint(479.1f, -130.9f, 16f),
            };

            Widen(c, 9, 11, 26f); // room for an island
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(4), s.Near(8), s.Near(12), s.Near(16), s.Near(20), s.Near(24), s.Finish };
            c.islands = new[] { s.Island(1, 38f, 3.5f, 7.5f, 1f) };
            c.currents = new[]
            {
                new CurrentZone(s.Near(4), s.Near(7), 2f), new CurrentZone(s.Near(14), s.Near(17), 2.2f),
                new CurrentZone(s.Near(25), s.Near(27), -1.1f),
            };
            c.whirlpools = new[] { s.Whirlpool(s.Near(21), 4.5f, 5.5f, 2.4f, 1.2f) };
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.CalvingGlacier, 1.5f), new ObstacleChance(ObstacleKind.IceFloes, 1.4f),
                new ObstacleChance(ObstacleKind.TidalBore), new ObstacleChance(ObstacleKind.Kraken),
                new ObstacleChance(ObstacleKind.StormCloud), new ObstacleChance(ObstacleKind.JumpRamp),
                new ObstacleChance(ObstacleKind.Drawbridge), new ObstacleChance(ObstacleKind.WreckingBall),
                new ObstacleChance(ObstacleKind.ChainedMines), new ObstacleChance(ObstacleKind.GiantFan),
                new ObstacleChance(ObstacleKind.Rockslide), new ObstacleChance(ObstacleKind.SwingingLog),
                new ObstacleChance(ObstacleKind.FallingTree), new ObstacleChance(ObstacleKind.Geyser),
                new ObstacleChance(ObstacleKind.Rapids, 0.8f), new ObstacleChance(ObstacleKind.Weir, 0.7f),
                new ObstacleChance(ObstacleKind.Bumper), new ObstacleChance(ObstacleKind.Bear),
                new ObstacleChance(ObstacleKind.Sweeper), new ObstacleChance(ObstacleKind.Waterspout),
                new ObstacleChance(ObstacleKind.Cannon),
            };
            c.minObstacles = 11;
            c.maxObstacles = 15;
        }

        /// <summary>Switchbacks through a blasting quarry: dynamite floats, cranes and rockslides.</summary>
        static void DynamiteGulch(CourseDef c)
        {
            c.displayName = "Dynamite Gulch";
            c.blurb = "Switchbacks through a blasting quarry, with dynamite in the water. New obstacles every round.";
            c.scene = CourseScene.Canyon;
            c.starsToUnlock = 18;
            c.flow = 11f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(17f, 0f, 16f),
                new RiverPoint(33.6f, 3.5f, 15f),
                new RiverPoint(48.4f, 12f, 14f),
                new RiverPoint(60.6f, 23.8f, 13f),
                new RiverPoint(72.4f, 36.1f, 13f),
                new RiverPoint(86.5f, 45.6f, 14f),
                new RiverPoint(102.8f, 50.3f, 15f),
                new RiverPoint(119.8f, 50.3f, 16f),
                new RiverPoint(136.8f, 49.1f, 20f), // the quarry basin
                new RiverPoint(153.7f, 50.3f, 20f),
                new RiverPoint(169.7f, 56.1f, 16f),
                new RiverPoint(183.5f, 66.1f, 13f),
                new RiverPoint(196.1f, 77.4f, 12f), // the cut
                new RiverPoint(210.2f, 87f, 12f),
                new RiverPoint(226.4f, 92.2f, 13f),
                new RiverPoint(243.4f, 92.8f, 14f),
                new RiverPoint(260.3f, 91.6f, 15f),
                new RiverPoint(277.3f, 92.8f, 16f),
                new RiverPoint(293.2f, 98.6f, 17f),
                new RiverPoint(307.3f, 108.1f, 18f),
                new RiverPoint(321.1f, 118.1f, 16f),
                new RiverPoint(336.4f, 125.6f, 15f),
                new RiverPoint(353f, 129.1f, 14f),
                new RiverPoint(370f, 129.7f, 13f),
                new RiverPoint(387f, 130.3f, 13f),
                new RiverPoint(403.6f, 133.8f, 14f),
                new RiverPoint(419.1f, 140.7f, 15f),
                new RiverPoint(434.1f, 148.7f, 16f),
                new RiverPoint(449.2f, 156.7f, 16f),
                new RiverPoint(464.2f, 164.7f, 16f),
                new RiverPoint(479.2f, 172.7f, 16f),
            };

            Widen(c, 9, 11, 26f); // room for an island
            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(4), s.Near(8), s.Near(12), s.Near(16), s.Near(20), s.Near(24), s.Near(28), s.Finish };
            c.islands = new[] { s.Island(1, 38f, 3.5f, 7.5f, -1f) };
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(3), 1.8f), new CurrentZone(s.Near(12), s.Near(14), 2f),
                new CurrentZone(s.Near(22), s.Near(24), -1.2f),
            };
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.DynamiteFloat, 1.6f), new ObstacleChance(ObstacleKind.Rockslide, 1.3f),
                new ObstacleChance(ObstacleKind.CargoCrane, 1.2f), new ObstacleChance(ObstacleKind.Mudslide),
                new ObstacleChance(ObstacleKind.WreckingBall), new ObstacleChance(ObstacleKind.LogFlume),
                new ObstacleChance(ObstacleKind.MillRace), new ObstacleChance(ObstacleKind.GiantFan),
                new ObstacleChance(ObstacleKind.JumpRamp), new ObstacleChance(ObstacleKind.ChainedMines),
                new ObstacleChance(ObstacleKind.PaddleSteamer), new ObstacleChance(ObstacleKind.StormCloud),
                new ObstacleChance(ObstacleKind.Cannon), new ObstacleChance(ObstacleKind.Geyser),
                new ObstacleChance(ObstacleKind.Rapids, 0.8f), new ObstacleChance(ObstacleKind.Weir, 0.7f),
                new ObstacleChance(ObstacleKind.Sluice), new ObstacleChance(ObstacleKind.Bear),
                new ObstacleChance(ObstacleKind.Crocodile), new ObstacleChance(ObstacleKind.Bumper),
                new ObstacleChance(ObstacleKind.Wheel), new ObstacleChance(ObstacleKind.NetBoat),
            };
            c.minObstacles = 12;
            c.maxObstacles = 16;
        }

        /// <summary>The last and hardest: the fastest water, step after step of weirs and rapids, and every kind of obstacle.</summary>
        static void DevilsStaircase(CourseDef c)
        {
            c.displayName = "Devil's Staircase";
            c.blurb = "The fastest water of all, weir after weir, and every obstacle there is. New obstacles every round.";
            c.scene = CourseScene.Rapids;
            c.starsToUnlock = 20;
            c.flow = 12f;
            c.river = new[]
            {
                new RiverPoint(0f, 0f, 16f),
                new RiverPoint(17f, 0f, 16f),
                new RiverPoint(33.8f, -2.4f, 15f),
                new RiverPoint(49.6f, -8.7f, 14f),
                new RiverPoint(63.7f, -18.2f, 13f, 0.15f), // S-bends
                new RiverPoint(77.4f, -28.2f, 13f, -0.15f),
                new RiverPoint(92.7f, -35.7f, 13f, 0.15f),
                new RiverPoint(109.5f, -38.6f, 14f),
                new RiverPoint(126.5f, -38f, 16f),
                new RiverPoint(143.4f, -37.5f, 20f, 0.5f), // whirlpool to the right: keep left
                new RiverPoint(160.3f, -39.8f, 18f),
                new RiverPoint(175.8f, -46.7f, 14f),
                new RiverPoint(189.6f, -56.7f, 12f),
                new RiverPoint(203.3f, -66.7f, 11f),
                new RiverPoint(218.8f, -73.6f, 11f),
                new RiverPoint(235.7f, -76f, 12f),
                new RiverPoint(252.7f, -75.4f, 14f),
                new RiverPoint(269.7f, -75.4f, 18f, -0.5f), // whirlpool to the left: keep right
                new RiverPoint(286.2f, -79.5f, 16f),
                new RiverPoint(300.9f, -88f, 14f),
                new RiverPoint(314.3f, -98.5f, 13f),
                new RiverPoint(328.4f, -108f, 13f),
                new RiverPoint(344.4f, -113.8f, 14f),
                new RiverPoint(361.3f, -115.6f, 15f),
                new RiverPoint(378.2f, -116.2f, 16f),
                new RiverPoint(395.1f, -118.5f, 16f),
                new RiverPoint(411.1f, -124.4f, 15f),
                new RiverPoint(425.8f, -132.9f, 14f),
                new RiverPoint(440.5f, -141.4f, 15f),
                new RiverPoint(456.3f, -147.7f, 16f),
                new RiverPoint(472.4f, -153f, 16f),
                new RiverPoint(488.6f, -158.2f, 16f),
                new RiverPoint(504.8f, -163.5f, 16f),
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(3) - 4f, s.Near(7), s.Near(11), s.Near(15), s.Near(19), s.Near(23), s.Near(26), s.Near(29), s.Finish };
            c.rocks = System.Array.Empty<RockSpot>(); // at this speed a rock in the water is a wall: the bends are the slalom
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(3), 2f), new CurrentZone(s.Near(11), s.Near(13), -1.2f),
                new CurrentZone(s.Near(20), s.Near(23), 2.2f), new CurrentZone(s.Near(26), s.Near(28), -1f),
            };
            c.whirlpools = new[] { s.Whirlpool(s.Near(9), -4.5f, 6f, 2.7f, 1.35f), s.Whirlpool(s.Near(17), 4.5f, 6f, -2.7f, 1.35f) };
            c.menu = new[]
            {
                new ObstacleChance(ObstacleKind.Weir, 1.4f), new ObstacleChance(ObstacleKind.Rapids, 1.2f),
                new ObstacleChance(ObstacleKind.Kraken), new ObstacleChance(ObstacleKind.TidalBore),
                new ObstacleChance(ObstacleKind.JumpRamp), new ObstacleChance(ObstacleKind.GooseGang),
                new ObstacleChance(ObstacleKind.ChainedMines), new ObstacleChance(ObstacleKind.WreckingBall),
                new ObstacleChance(ObstacleKind.StormCloud), new ObstacleChance(ObstacleKind.Drawbridge),
                new ObstacleChance(ObstacleKind.MillRace), new ObstacleChance(ObstacleKind.CargoCrane),
                new ObstacleChance(ObstacleKind.LogFlume), new ObstacleChance(ObstacleKind.LeapingFrogs),
                new ObstacleChance(ObstacleKind.GiantFan), new ObstacleChance(ObstacleKind.Wheel),
                new ObstacleChance(ObstacleKind.Crocodile), new ObstacleChance(ObstacleKind.SwingingLog),
                new ObstacleChance(ObstacleKind.Geyser), new ObstacleChance(ObstacleKind.Rockslide),
                new ObstacleChance(ObstacleKind.Hippo), new ObstacleChance(ObstacleKind.Sluice),
                new ObstacleChance(ObstacleKind.Sweeper), new ObstacleChance(ObstacleKind.Cannon),
                new ObstacleChance(ObstacleKind.Bumper), new ObstacleChance(ObstacleKind.Bear),
                new ObstacleChance(ObstacleKind.FallingTree), new ObstacleChance(ObstacleKind.Turtle),
                new ObstacleChance(ObstacleKind.PiranhaShoal), new ObstacleChance(ObstacleKind.Waterspout),
                new ObstacleChance(ObstacleKind.ElectricEels), new ObstacleChance(ObstacleKind.Mudslide),
                new ObstacleChance(ObstacleKind.BeaverDam), new ObstacleChance(ObstacleKind.Pelican),
            };
            c.minObstacles = 11;
            c.maxObstacles = 15;
        }
    }
}
