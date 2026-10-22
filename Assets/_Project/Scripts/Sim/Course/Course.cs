using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>A line across the river that counts only when crossed going downstream, between the banks.</summary>
    public readonly struct Gate
    {
        public readonly Vector2 Centre;
        public readonly Vector2 Downstream;
        public readonly float HalfWidth;
        public readonly float Distance;

        public Gate(RiverSample at)
        {
            Centre = at.Centre;
            Downstream = at.Tangent;
            HalfWidth = at.HalfWidth;
            Distance = at.Distance;
        }

        public Vector2 Across => new(-Downstream.y, Downstream.x);
        public Vector2 LeftEnd => Centre + Across * HalfWidth;
        public Vector2 RightEnd => Centre - Across * HalfWidth;

        /// <summary>Moving from <paramref name="from"/> to <paramref name="to"/> crossed the line downstream, inside the posts.</summary>
        public bool CrossedBy(Vector2 from, Vector2 to)
        {
            var before = Vector2.Dot(from - Centre, Downstream);
            var after = Vector2.Dot(to - Centre, Downstream);
            if (before >= 0f || after < 0f) return false;

            var crossing = Vector2.Lerp(from, to, before / (before - after));
            return Mathf.Abs(Vector2.Dot(crossing - Centre, Across)) <= HalfWidth + 0.5f;
        }
    }

    /// <summary>
    /// A <see cref="CourseDef"/> made real: its river geometry, gates and currents, and (once <see cref="BuildPhysics"/>
    /// runs) its banks, rocks and barrels as 2D physics bodies. The look is built separately, by the course view.
    /// </summary>
    public sealed class Course
    {
        public const float BarrelRadius = 0.4f;
        const float BarrelMass = 8f;
        const float CurrentRampMetres = 6f; // currents ease in and out rather than starting at a line
        const float RespawnPastGate = 3f;

        readonly List<Gate> gates = new();
        readonly List<GameObject> barrels = new();
        readonly List<Rigidbody2D> barrelBodies = new();

        public Course(CourseDef def)
        {
            Def = def;
            River = new River(def.river);
            foreach (var distance in def.gates) gates.Add(new Gate(River.At(distance)));

            var start = River.At(def.startDistance);
            SpawnPosition = start.Centre;
            SpawnAngleRad = Mathf.Atan2(start.Tangent.y, start.Tangent.x);
        }

        public CourseDef Def { get; }
        public River River { get; }

        /// <summary>Checkpoints in order; the last is the finish line.</summary>
        public IReadOnlyList<Gate> Gates => gates;

        public Vector2 SpawnPosition { get; }
        public float SpawnAngleRad { get; }

        /// <summary>Everything physical, so the course can be removed in one go. Null until <see cref="BuildPhysics"/>.</summary>
        public GameObject Root { get; private set; }

        /// <summary>Loose barrels, in the course's order: snapshots identify them by position in this list.</summary>
        public IReadOnlyList<GameObject> Barrels => barrels;
        public IReadOnlyList<Rigidbody2D> BarrelBodies => barrelBodies;

        public static Course Build(CourseDef def)
        {
            var course = new Course(def);
            course.BuildPhysics();
            return course;
        }

        public void BuildPhysics()
        {
            Root = new GameObject($"Course: {Def.displayName}");
            PhysicsBodies.CreateWallLoop(River.Outline(), "Banks").transform.SetParent(Root.transform, false);
            foreach (var rock in Def.rocks) PhysicsBodies.CreateRock(rock.position, rock.radius).transform.SetParent(Root.transform, true);
            foreach (var spot in Def.barrels)
            {
                var barrel = PhysicsBodies.CreateBarrel(spot, BarrelRadius, BarrelMass);
                barrel.transform.SetParent(Root.transform, true);
                barrels.Add(barrel);
                barrelBodies.Add(barrel.GetComponent<Rigidbody2D>());
            }
        }

        /// <summary>
        /// Where a raft starts, or comes back after capsizing (just past the last checkpoint it passed), facing
        /// downstream. With two rafts each has its own lane, either side of the middle, so they never land on each other.
        /// </summary>
        public (Vector2 position, float angleRad) RespawnPose(int gatesPassed, int lane = 0, int lanes = 1)
        {
            var at = gatesPassed <= 0 ? River.At(Def.startDistance)
                : River.At(gates[Mathf.Min(gatesPassed, gates.Count) - 1].Distance + RespawnPastGate);
            return (at.Centre + at.Left * LaneOffset(at, lane, lanes), Mathf.Atan2(at.Tangent.y, at.Tangent.x));
        }

        /// <summary>Across the river from the middle: lane 0 to the left, lane 1 to the right, clear of each other and the banks.</summary>
        public static float LaneOffset(RiverSample at, int lane, int lanes)
        {
            if (lanes <= 1) return 0f;
            var offset = Mathf.Clamp(at.HalfWidth * 0.42f, 2f, Mathf.Max(2f, at.HalfWidth - 1.9f));
            return lane == 0 ? offset : -offset;
        }

        /// <summary>How fast the water itself moves here. Strongest mid-stream, easing off toward the banks and the zone's ends.</summary>
        public Vector2 WaterVelocityAt(Vector2 point)
        {
            if (Def.currents.Length == 0) return Vector2.zero;

            var at = River.Project(point);
            var across = at.HalfWidth > 0f ? at.Lateral / at.HalfWidth : 0f;
            var profile = Mathf.Clamp01(1f - across * across);
            var velocity = Vector2.zero;
            foreach (var zone in Def.currents)
            {
                var ramp = Mathf.Min((at.Distance - zone.from) / CurrentRampMetres, (zone.to - at.Distance) / CurrentRampMetres);
                if (ramp <= 0f) continue;
                velocity += at.Tangent * (zone.speed * Mathf.Min(1f, ramp) * profile);
            }

            return velocity;
        }

        /// <summary>Puts the barrels back where the course places them, at rest.</summary>
        public void ResetBarrels()
        {
            for (var i = 0; i < barrels.Count; i++) PhysicsBodies.Reset(barrels[i], Def.barrels[i]);
        }

        public void Destroy()
        {
            if (Root == null) return;
            Root.SetActive(false); // out of the physics world now, not at the end of the frame
            if (Application.isPlaying) Object.Destroy(Root);
            else Object.DestroyImmediate(Root);
            Root = null;
        }
    }
}
