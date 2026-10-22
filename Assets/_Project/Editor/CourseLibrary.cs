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
        public static CourseDef[] CreateDefaults()
        {
            ContentBootstrap.EnsureFolder(Folder);
            return new[] { Create("OtterCreek", OtterCreek) };
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

            // Set from the course-solvability test: a steady autopilot crew finishes in about 118 s (2 stars); pushing flat
            // out it runs dry and takes 130 s. Three stars wants a crew that takes turns well.
            c.parSeconds = 150f;
            c.twoStarSeconds = 120f;
            c.threeStarSeconds = 100f;
        }

        static RockSpot Rock(Vector2 at, float radius) => new(at.x, at.y, radius);
    }
}
