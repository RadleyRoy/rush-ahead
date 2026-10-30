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
        public const float LogRadius = 0.35f;
        const float BarrelMass = 8f;
        const float LogMassPerMetre = 7f;
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

        /// <summary>The moving obstacles. Null until <see cref="BuildPhysics"/>.</summary>
        public Hazards Hazards { get; private set; }

        /// <summary>
        /// Everything floating loose: the barrels in the course's order, then the logs. Snapshots identify them by
        /// position in this list, so every device builds it the same way.
        /// </summary>
        public IReadOnlyList<GameObject> Barrels => barrels;
        public IReadOnlyList<Rigidbody2D> BarrelBodies => barrelBodies;

        /// <summary>Whether the loose body at this index is a log (the logs follow the barrels).</summary>
        public bool IsLog(int index) => index >= Def.barrels.Length;

        public static Course Build(CourseDef def)
        {
            var course = new Course(def);
            course.BuildPhysics();
            return course;
        }

        /// <summary>How far past the water's edge a raft's middle can go before it's over (its tube hangs out first).</summary>
        public const float EdgeMargin = 0.6f;

        /// <summary>
        /// The course's bodies: rocks, barrels, logs and the obstacles. No banks: the river runs along high ground and
        /// its sides drop away, so a raft that drifts past the edge falls (see <see cref="IsOffCourse"/>). Only the ends
        /// are closed, by a line of boulders at the top and a log boom at the bottom, so a finished raft stops there.
        /// </summary>
        public void BuildPhysics()
        {
            Root = new GameObject($"Course: {Def.displayName}");
            EndWall(River.At(StartWallDistance), "Boulders");
            EndWall(River.At(River.Length - BoomBack), "Log boom");
            foreach (var rock in Def.rocks) PhysicsBodies.CreateRock(rock.position, rock.radius).transform.SetParent(Root.transform, true);
            Hazards = new Hazards(this, Root.transform);
            foreach (var spot in Def.barrels)
            {
                var barrel = PhysicsBodies.CreateBarrel(spot, BarrelRadius, BarrelMass);
                barrel.transform.SetParent(Root.transform, true);
                barrels.Add(barrel);
                barrelBodies.Add(barrel.GetComponent<Rigidbody2D>());
            }

            foreach (var log in Def.logs)
            {
                var body = PhysicsBodies.CreateLog(log.position, log.angleDeg * Mathf.Deg2Rad, log.length, LogRadius, LogMassPerMetre * log.length);
                body.transform.SetParent(Root.transform, true);
                barrels.Add(body);
                barrelBodies.Add(body.GetComponent<Rigidbody2D>());
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

        /// <summary>Where the boulders across the top of the river stop things, and how far up from its end the log boom is.</summary>
        public const float StartWallDistance = 0.3f;
        public const float BoomBack = 1f;

        void EndWall(RiverSample at, string name)
        {
            var reach = at.HalfWidth + EdgeMargin + 3f; // out past the edges, so nothing slips round the end
            var wall = PhysicsBodies.CreateWallLoop(new[] { at.Centre + at.Left * reach, at.Centre - at.Left * reach }, name);
            wall.transform.SetParent(Root.transform, false);
        }

        /// <summary>Whether a point is off the course: past either edge of the river (the ends are walled).</summary>
        public bool IsOffCourse(Vector2 point)
        {
            var at = River.Project(point);
            return Mathf.Abs(at.Lateral) > at.HalfWidth + EdgeMargin;
        }

        /// <summary>Puts back any barrel or log that has drifted over the edge, so the course never runs out of them.</summary>
        public void ResetStrays()
        {
            for (var i = 0; i < barrels.Count; i++)
            {
                if (!barrelBodies[i].simulated || !IsOffCourse(barrelBodies[i].position)) continue;
                if (!IsLog(i)) PhysicsBodies.Reset(barrels[i], Def.barrels[i]);
                else
                {
                    var log = Def.logs[i - Def.barrels.Length];
                    PhysicsBodies.Reset(barrels[i], log.position, log.angleDeg * Mathf.Deg2Rad);
                }
            }
        }

        /// <summary>
        /// How fast the water itself moves here. The river's flow runs downstream everywhere in the channel; currents
        /// add to it or hold it back; both are strongest mid-stream, easing off toward the edges (and currents toward
        /// their zone's ends). Whirlpools add a swirl and a pull toward their middle.
        /// </summary>
        public Vector2 WaterVelocityAt(Vector2 point)
        {
            var velocity = Whirlpools(point);
            if (Def.currents.Length == 0 && Def.flow <= 0f) return velocity;

            var at = River.Project(point);
            var across = at.HalfWidth > 0f ? at.Lateral / at.HalfWidth : 0f;
            var profile = Mathf.Clamp01(1f - across * across);
            velocity += at.Tangent * (Def.flow * Mathf.Lerp(0.5f, 1f, profile)) * (Mathf.Abs(across) <= 1f ? 1f : 0f);
            foreach (var zone in Def.currents)
            {
                var ramp = Mathf.Min((at.Distance - zone.from) / CurrentRampMetres, (zone.to - at.Distance) / CurrentRampMetres);
                if (ramp <= 0f) continue;
                velocity += at.Tangent * (zone.speed * Mathf.Min(1f, ramp) * profile);
            }

            return velocity;
        }

        /// <summary>
        /// A whirlpool's water goes round and in. Both are strongest halfway out and fade to nothing at the middle (so
        /// nothing spins in place for ever) and at the rim (so it starts gently).
        /// </summary>
        Vector2 Whirlpools(Vector2 point)
        {
            var velocity = Vector2.zero;
            foreach (var pool in Def.whirlpools)
            {
                var offset = point - pool.position;
                var distance = offset.magnitude;
                if (distance >= pool.radius || distance < 1e-3f) continue;
                var outward = offset / distance;
                var strength = Mathf.Sin(Mathf.PI * distance / pool.radius);
                velocity += (new Vector2(-outward.y, outward.x) * pool.spin - outward * pool.pull) * strength;
            }

            return velocity;
        }

        /// <summary>Puts the barrels and logs back where the course places them, at rest.</summary>
        public void ResetBarrels()
        {
            for (var i = 0; i < barrels.Count; i++)
            {
                if (!IsLog(i)) PhysicsBodies.Reset(barrels[i], Def.barrels[i]);
                else
                {
                    var log = Def.logs[i - Def.barrels.Length];
                    PhysicsBodies.Reset(barrels[i], log.position, log.angleDeg * Mathf.Deg2Rad);
                }
            }
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
