using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>Venues: the practice lagoon (0) or a course.</summary>
    public sealed partial class GameController
    {
        /// <summary>Tears down the current venue and builds another. Joined, its barrels only show where the host says.</summary>
        void LoadVenue(int index)
        {
            UnloadVenue();
            venue = Mathf.Clamp(index, 0, courses.Length);
            if (venue == 0)
            {
                BuildLagoon();
            }
            else
            {
                course = Course.Build(courses[venue - 1]);
                courseView = CourseView.Build(course, palette);
                barrels.AddRange(course.Barrels);
                barrelBodies.AddRange(course.BarrelBodies);
                runner.World.WaterVelocity = course.WaterVelocityAt;
            }

            runner.World.Drifting.AddRange(barrelBodies);
            if (Mode == NetMode.Joined)
                foreach (var barrel in barrels) PhysicsBodies.Freeze(barrel);
            recordedRun = (-1, -1);
        }

        void UnloadVenue()
        {
            foreach (var raft in rafts)
            {
                raft.Race?.Dispose();
                raft.Race = null;
            }

            course?.Destroy();
            course = null;
            if (courseView != null) Destroy(courseView.gameObject);
            courseView = null;
            if (lagoon != null)
            {
                lagoon.SetActive(false); // out of the physics world now, not at the end of the frame
                Destroy(lagoon);
            }

            lagoon = null;
            barrels.Clear();
            barrelBodies.Clear();
            runner.World.Drifting.Clear();
            runner.World.WaterVelocity = null;
        }

        void BuildLagoon()
        {
            lagoon = new GameObject("Practice lagoon");
            var scenery = Shapes3D.Group(lagoon.transform, "Scenery");
            var random = new System.Random(7); // same scenery every run, and on every player's device
            var h = ArenaHalfSize;

            CourseDressing.Water(scenery, Vector2.zero, 2f * h + new Vector2(120f, 120f), palette);

            // Banks all round, walked counter-clockwise so each cliff builds outward, away from the water.
            var corners = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) };
            PhysicsBodies.CreateWallLoop(corners).transform.SetParent(lagoon.transform, true);
            for (var i = 0; i < corners.Length; i++)
                CourseDressing.CliffBank(scenery, corners[i], corners[(i + 1) % corners.Length], 7f, palette, random);

            // A slalom down the middle, then a gate between two rocks with 0.8 m to spare for the 3.2 m raft.
            foreach (var (position, radius) in new[]
                     {
                         (new Vector2(-4f, 6f), 1.5f), (new Vector2(4f, -6f), 1.5f), (new Vector2(12f, 6f), 1.5f), (new Vector2(20f, -6f), 1.5f),
                         (new Vector2(27f, 3.2f), 1.2f), (new Vector2(27f, -3.2f), 1.2f),
                     })
            {
                PhysicsBodies.CreateRock(position, radius).transform.SetParent(lagoon.transform, true);
                CourseDressing.Rock(scenery, position, radius, palette, random);
            }

            CourseDressing.Gate(scenery, new Vector2(27f, 3.2f), new Vector2(27f, -3.2f), palette);

            // Barrels are built in the same order on every device: snapshots identify them by position in this list.
            for (var i = 0; i < 8; i++)
            {
                var barrel = PhysicsBodies.CreateBarrel(BarrelSpot(i), BarrelRadius, 8f);
                barrel.transform.SetParent(lagoon.transform, true);
                CourseDressing.Barrel(scenery, barrel.transform, BarrelRadius, palette);
                barrels.Add(barrel);
                barrelBodies.Add(barrel.GetComponent<Rigidbody2D>());
            }
        }

        static Vector2 BarrelSpot(int i) => new(-15f + 1.8f * (i % 4), -2.7f + 1.8f * (i / 4) + 0.9f * (i % 2));
    }
}
