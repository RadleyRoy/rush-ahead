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
        };

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
        /// The first course, about 220 m: a wide start pool, a left bend into a rock slalom, a right bend into a
        /// helpful current, a pool full of barrels, a narrow gorge where the water pushes back, then the finish.
        /// </summary>
        static void OtterCreek(CourseDef c)
        {
            c.displayName = "Otter Creek";
            c.blurb = "Weave the rocks, ride the current, shove through the barrels and fight the gorge.";
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
            };

            // Lay everything else out by distance down the river and distance across it, so it lands where it means to.
            var river = new River(c.river);
            Vector2 Spot(float distance, float across)
            {
                var at = river.At(distance);
                return at.Centre + at.Left * across;
            }

            float DistanceNear(int point) => river.Project(c.river[point].position).Distance;

            c.startDistance = 8f;
            var finish = river.Length - 16f;
            c.gates = new[] { DistanceNear(3) - 6f, DistanceNear(6) + 4f, DistanceNear(9) + 12f, finish };

            c.rocks = new[]
            {
                Rock(Spot(DistanceNear(3), 3.8f), 1.4f),
                Rock(Spot(DistanceNear(4), -3.8f), 1.5f),
                Rock(Spot(DistanceNear(5), 3.8f), 1.3f),
                Rock(Spot(DistanceNear(11) + 6f, 4.6f), 1.1f), // a boulder on the bend after the gorge
            };

            var pool = DistanceNear(9);
            c.barrels = new[]
            {
                Spot(pool - 8f, -2f), Spot(pool - 8f, 1.5f), Spot(pool - 3f, 0f), Spot(pool - 3f, -4f), Spot(pool - 3f, 4f),
                Spot(pool + 2f, 2f), Spot(pool + 2f, -2f), Spot(pool + 6f, 0f),
            };

            c.currents = new[]
            {
                new CurrentZone(DistanceNear(6) + 2f, DistanceNear(8) + 6f, 0.9f), // ride it
                new CurrentZone(DistanceNear(10) - 6f, DistanceNear(11) + 2f, -0.5f), // the gorge pushes back
            };

            // The obstacles, gently: a paddle wheel on the left of the first bend (keep right), a log swinging under a
            // bridge on the current (brace as it comes), and a crocodile on the last straight.
            c.flow = 1.2f;
            var s = new Survey(c);
            c.wheels = new[] { s.Wheel(DistanceNear(2), 4.2f, 2.8f, 3, 50f) };
            c.swingers = new[] { new SwingSpot(DistanceNear(7) + 4f, 5f, 0f, 3.5f) };
            c.crocodiles = new[] { new CrocSpot(DistanceNear(12) + 2f, -1f, 0.35f, 7f, 2f) };

            // Measured by the AI crews with the river flowing and the obstacles in: Ace 58.2 s, Keen 70.3 s, Clumsy 99.6 s
            // (two lives lost to the swinging log); steady 60.5 s, flat out 59.6 s. Three stars just above Ace, two just under
            // Keen, par about Keen + 16%.
            c.parSeconds = 82f;
            c.twoStarSeconds = 68f;
            c.threeStarSeconds = 59f;
        }

        static RockSpot Rock(Vector2 at, float radius) => new(at.x, at.y, radius);

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
            /// A paddle wheel, turning the way that makes the arms meeting a raft coming downstream sweep it toward the
            /// middle of the river rather than over the edge (anticlockwise on the left, clockwise on the right).
            /// </summary>
            public WheelSpot Wheel(float distance, float across, float radius, int arms, float degreesPerSecond) =>
                new(Spot(distance, across), radius, arms, Mathf.Abs(degreesPerSecond) * (across >= 0f ? 1f : -1f));

            public GeyserSpot Geyser(float distance, float across, float radius, float period, float offset) =>
                new(Spot(distance, across), radius, period, offset);
        }

        /// <summary>Wide, calm water with three islands to pick a way round, and a drift of barrels near the end.</summary>
        static void MirrorLake(CourseDef c)
        {
            c.displayName = "Mirror Lake";
            c.blurb = "Wide, calm water. Pick your way round the islands, and mind the drifting barrels.";
            c.scene = CourseScene.Lake;
            c.starsToUnlock = 1;
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
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(2) + 2f, s.Near(5) + 12f, s.Near(8), s.Finish };
            c.rocks = new[] { s.Rock(s.Near(3), -4f, 3.5f), s.Rock(s.Near(5), 4.5f, 4f), s.Rock(s.Near(7), -4f, 3.2f) };
            var drift = s.Near(9);
            c.barrels = new[] { s.Spot(drift + 4f, -4f), s.Spot(drift + 4f, 3f), s.Spot(drift + 9f, 0f), s.Spot(drift + 9f, -6f), s.Spot(drift + 14f, 4f) };
            c.flow = 0.8f;
            c.geysers = new[]
            {
                s.Geyser(s.Near(4), 5f, 2f, 5f, 0f), s.Geyser(s.Near(4) + 6f, -6f, 2f, 5f, 2.5f), s.Geyser(s.Near(6) + 6f, 0f, 2f, 4.5f, 1f),
            };
            c.crocodiles = new[] { new CrocSpot(s.Near(6) + 14f, 1f, 0.3f, 7f, 0f), new CrocSpot(s.Near(10) + 2f, -1f, 0.4f, 6.5f, 3f) };
            // Measured by the AI crews: Ace 66.4 s, Keen 71.7 s, Clumsy 114.6 s; steady 68.4 s, flat out 62.8 s. Set as on Otter
            // Creek, keeping two stars a few seconds clear of three where Keen runs nearly as fast as Ace.
            c.parSeconds = 83f;
            c.twoStarSeconds = 71f;
            c.threeStarSeconds = 67f;
        }

        /// <summary>Switchbacks through beaver country, logs drifting across the bends, and a log jam in the big pool.</summary>
        static void BeaverBend(CourseDef c)
        {
            c.displayName = "Beaver Bend";
            c.blurb = "Switchbacks through beaver country. Shove the drifting logs aside, and bash through the jam.";
            c.scene = CourseScene.Creek;
            c.starsToUnlock = 3;
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
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(3) - 6f, s.Near(7) + 4f, s.Near(10) + 12f, s.Near(13) + 2f, s.Finish };
            c.rocks = new[] { s.Rock(s.Near(6), -4.2f, 1.2f), s.Rock(s.Near(12), 3.8f, 1.1f) };
            var jam = s.Near(10);
            c.logs = new[]
            {
                s.Log(s.Near(3) + 2f, 2f, 90f, 3.5f), s.Log(s.Near(4) + 3f, -2.5f, 75f, 4f), s.Log(s.Near(7) + 1f, 0f, 60f, 3.5f),
                s.Log(jam - 4f, -4f, 20f, 4f), s.Log(jam - 2f, 3f, -35f, 4.5f), s.Log(jam + 2f, -1f, 80f, 4f), s.Log(jam + 5f, 5f, 10f, 3.5f),
                s.Log(jam + 7f, -5f, 50f, 4f),
            };
            c.currents = new[] { new CurrentZone(s.Near(4), s.Near(6), 0.7f), new CurrentZone(s.Near(11), s.Near(13), 0.6f) };
            c.flow = 1.2f;
            c.swingers = new[] { new SwingSpot(s.Near(5) + 2f, 5f, 1f, 3.5f), new SwingSpot(s.Near(14) + 4f, 4.5f, 0f, 3.5f) };
            c.wheels = new[] { s.Wheel(s.Near(8) + 6f, -3f, 2.6f, 3, 60f) };
            c.crocodiles = new[] { new CrocSpot(s.Near(12), -1f, 0.35f, 6f, 1f) };
            // Measured by the AI crews: Ace 69.8 s, Keen 86.2 s (a life lost to the second log), Clumsy 125.7 s; steady 71.6 s,
            // flat out 66.6 s. Set as on Otter Creek.
            c.parSeconds = 100f;
            c.twoStarSeconds = 84f;
            c.threeStarSeconds = 71f;
        }

        /// <summary>Narrow channels through the reeds, lily pads along the edges, and a whirlpool in the big pool.</summary>
        static void ReedMarsh(CourseDef c)
        {
            c.displayName = "Reed Marsh";
            c.blurb = "Narrow channels through the reeds, and a whirlpool waiting in the big pool. Keep to the edge.";
            c.scene = CourseScene.Lake;
            c.starsToUnlock = 5;
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
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(2) + 2f, s.Near(6), s.Near(8) + 6f, s.Near(11), s.Finish };
            c.rocks = new[] { s.Rock(s.Near(12) + 2f, -4f, 1f) };
            var pool = s.Near(7);
            c.barrels = new[] { s.Spot(pool - 6f, 6f), s.Spot(pool + 5f, 7f), s.Spot(pool + 2f, -8f) };
            c.currents = new[] { new CurrentZone(s.Near(2), s.Near(5), 0.5f), new CurrentZone(s.Near(9), s.Near(11), -0.4f) };
            c.whirlpools = new[] { s.Whirlpool(pool, -3f, 6f, 1.3f, 0.6f) };
            c.flow = 0.8f;
            c.crocodiles = new[] { new CrocSpot(s.Near(5) + 3f, 1f, 0.25f, 6.5f, 0f), new CrocSpot(s.Near(10) + 2f, -1f, 0.35f, 6f, 2f) };
            c.geysers = new[] { s.Geyser(pool + 4f, 7f, 1.8f, 5f, 0f), s.Geyser(s.Near(12) + 4f, 0f, 1.6f, 4.5f, 1.5f) };
            // Measured by the AI crews: Ace 64.5 s, Keen 105.8 s (knocked off the edge once by the first crocodile), Clumsy
            // 114.5 s; steady 70.2 s, flat out 62 s. Set as on Otter Creek.
            c.parSeconds = 123f;
            c.twoStarSeconds = 103f;
            c.threeStarSeconds = 65f;
        }

        /// <summary>Three whirlpools in wide pools with white water between them, a rock slalom and a climb at the end.</summary>
        static void WhirlpoolReach(CourseDef c)
        {
            c.displayName = "Whirlpool Reach";
            c.blurb = "Three whirlpools that grab and spin, white water between them, and a climb at the end.";
            c.scene = CourseScene.Rapids;
            c.starsToUnlock = 8;
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
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(2), s.Near(5) + 2f, s.Near(7) + 4f, s.Near(9) + 2f, s.Finish };
            c.rocks = new[] { s.Rock(s.Near(10), 3.2f, 1.2f), s.Rock(s.Near(11), -3f, 1.1f), s.Rock(s.Near(12), 3f, 1.1f) };
            c.logs = new[] { s.Log(s.Near(6) + 8f, 5f, 30f, 3.5f), s.Log(s.Near(8) - 6f, -4f, 70f, 3f) };
            c.currents = new[]
            {
                new CurrentZone(s.Near(1) + 4f, s.Near(2) + 6f, 1f), new CurrentZone(s.Near(9), s.Near(11), 1.2f),
                new CurrentZone(s.Near(12), s.Near(13) + 4f, -0.6f),
            };
            c.whirlpools = new[]
            {
                s.Whirlpool(s.Near(3), 3.5f, 5.5f, 1.4f, 0.7f), s.Whirlpool(s.Near(6), -3.5f, 6f, -1.6f, 0.8f),
                s.Whirlpool(s.Near(8), 3.5f, 5.5f, 1.8f, 0.9f),
            };
            c.flow = 1.8f;
            c.wheels = new[] { s.Wheel(s.Near(1) + 8f, 4f, 3f, 3, 70f), s.Wheel(s.Near(9) - 5f, -4f, 2.8f, 3, 60f) };
            c.geysers = new[] { s.Geyser(s.Near(4), 0f, 2f, 5f, 0f), s.Geyser(s.Near(11), 3f, 1.5f, 5f, 2f) };
            c.swingers = new[] { new SwingSpot(s.Near(13) + 5f, 4.5f, 0f, 3.5f) };
            // Measured by the AI crews: Ace 71 s, Keen 74.9 s, Clumsy 89.9 s; steady 70.5 s, flat out 73.4 s. Set as on Otter
            // Creek, keeping two stars a few seconds clear of three where Keen runs nearly as fast as Ace.
            c.parSeconds = 87f;
            c.twoStarSeconds = 76f;
            c.threeStarSeconds = 72f;
        }

        /// <summary>The longest: a rock slalom in fast water, a whirlpool, a gorge that pushes back, a pool of logs, another whirlpool.</summary>
        static void WhitewaterRun(CourseDef c)
        {
            c.displayName = "Whitewater Run";
            c.blurb = "The big one: a rock slalom, two whirlpools, a gorge that pushes back and a pool full of logs.";
            c.scene = CourseScene.Rapids;
            c.starsToUnlock = 11;
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
            };

            var s = new Survey(c);
            c.startDistance = 8f;
            c.gates = new[] { s.Near(3) - 6f, s.Near(6) + 2f, s.Near(9) + 4f, s.Near(12) + 8f, s.Near(16), s.Finish };
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
                new CurrentZone(s.Near(1) + 4f, s.Near(3), 1.1f), new CurrentZone(s.Near(5), s.Near(7), 1.2f),
                new CurrentZone(s.Near(9) - 2f, s.Near(10) + 4f, -0.7f), new CurrentZone(s.Near(13), s.Near(16), 1f),
            };
            c.whirlpools = new[] { s.Whirlpool(s.Near(8), 4f, 6.5f, -1.8f, 0.9f), s.Whirlpool(s.Near(17), -4f, 6f, 1.6f, 0.8f) };
            c.flow = 1.8f;
            c.wheels = new[] { s.Wheel(s.Near(1) + 6f, -3.5f, 2.8f, 3, 60f), s.Wheel(s.Near(14), 3f, 2.6f, 3, 70f) };
            c.swingers = new[] { new SwingSpot(s.Near(7), 5f, 0f, 3.5f), new SwingSpot(s.Near(18) + 4f, 4.5f, 1.5f, 3.5f) };
            c.geysers = new[] { s.Geyser(s.Near(11) + 2f, 0f, 1.8f, 5f, 0f), s.Geyser(s.Near(17) + 8f, 3f, 1.6f, 4.5f, 2f) };
            // Measured by the AI crews: Ace 85.4 s, Keen 92.9 s, Clumsy 116.6 s; steady 83.6 s, flat out 91.6 s. Set as on
            // Otter Creek, keeping two stars a few seconds clear of three.
            c.parSeconds = 108f;
            c.twoStarSeconds = 91f;
            c.threeStarSeconds = 86f;
        }
    }
}
