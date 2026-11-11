using System.Collections.Generic;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// One obstacle on a course: a rockslide, a hippo, a sluice gate and so on. Where it is and what it's doing follow
    /// from the race clock alone, so every device poses it identically with nothing sent over the network; only the
    /// host steps its physics (<see cref="Step"/>). The four first obstacles (wheels, crocodiles, swinging logs and
    /// geysers) live in <see cref="Hazards"/> itself; everything newer is one of these.
    /// </summary>
    public abstract class Obstacle
    {
        protected const float RaftRadius = 1.6f;

        /// <summary>How fast a raft comes down on an obstacle, for guessing when it'll get there.</summary>
        protected const float ApproachSpeed = 12f;

        /// <summary>For barriers a raft runs into or is pressed against (see <see cref="BoatPhysics.Cushioned"/>).</summary>
        protected static PhysicsMaterial2D Slick => BoatPhysics.Cushioned;

        protected Obstacle(Course course) => Course = course;

        public Course Course { get; }
        protected River River => Course.River;
        internal Hazards Owner { get; set; }

        /// <summary>Host, every tick before physics: moves its bodies to where the clock says and knocks the rafts it catches.</summary>
        public abstract void Step(double seconds, float dt, IReadOnlyList<Boat> boats);

        /// <summary>Its walls across the river (gates, dams, fallen trees, ice), which loose barrels and logs slip under rather than jam against.</summary>
        public virtual IEnumerable<Collider2D> Barriers => System.Array.Empty<Collider2D>();

        /// <summary>For AI crews: whether bracing would save a raft here from something about to hit it within <paramref name="lookahead"/> seconds.</summary>
        public virtual bool BraceNeeded(Vector2 position, double seconds, float lookahead) => false;

        /// <summary>
        /// For AI crews: where across the river to aim to keep clear of it, given the raft is <paramref name="from"/>
        /// metres down the river and aiming at <paramref name="to"/>, never further out than <paramref name="limit"/>.
        /// </summary>
        public virtual float SteerAcross(float from, float to, float across, double seconds, float limit) => across;

        /// <summary>
        /// For the hit-zone overlay: the outline a raft's rim has to touch for this obstacle's scripted knock to land, as
        /// pairs of points on the water (each pair one line). Its solid parts are traced from their colliders.
        /// </summary>
        public virtual void Zones(double seconds, List<Vector2> lines)
        {
        }

        /// <summary>A circle as lines (a small dot if it's smaller than that).</summary>
        public static void Circle(List<Vector2> lines, Vector2 centre, float radius)
        {
            radius = Mathf.Max(radius, 0.05f);
            const int segments = 28;
            for (var i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                lines.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                lines.Add(centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius);
            }
        }

        /// <summary>A rectangle as lines: <paramref name="halfAlong"/> either way along <paramref name="axis"/>, <paramref name="halfAcross"/> either way across it.</summary>
        public static void Box(List<Vector2> lines, Vector2 centre, Vector2 axis, float halfAlong, float halfAcross)
        {
            var side = new Vector2(-axis.y, axis.x);
            halfAlong = Mathf.Max(halfAlong, 0.05f);
            halfAcross = Mathf.Max(halfAcross, 0.05f);
            var corners = new[]
            {
                centre + axis * halfAlong + side * halfAcross, centre - axis * halfAlong + side * halfAcross,
                centre - axis * halfAlong - side * halfAcross, centre + axis * halfAlong - side * halfAcross,
            };
            for (var i = 0; i < 4; i++)
            {
                lines.Add(corners[i]);
                lines.Add(corners[(i + 1) % 4]);
            }
        }

        /// <summary>One knock per raft per pass of each part: not again for <paramref name="gap"/> seconds.</summary>
        protected bool Ready(Boat boat, double seconds, int part = 0, double gap = 1.0) => Owner.Ready(boat, this, part, seconds, gap);

        protected static bool Afloat(Boat boat) => boat.State == BoatState.Afloat;

        /// <summary>0 to 1 through a repeating timetable.</summary>
        protected static float Phase(double seconds, float period, float offset) =>
            (float)((((seconds + offset) % period) + period) % period / period);

        protected static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>Whether something at <paramref name="distance"/> (give or take <paramref name="reach"/>) lies between two distances down the river.</summary>
        protected static bool Between(float distance, float reach, float from, float to) => distance + reach >= from && distance - reach <= to;

        protected static Rigidbody2D Kinematic(string name, Vector2 position, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.SetParent(parent, true);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            return body;
        }

        /// <summary>Moves <paramref name="across"/> out of a band round <paramref name="centre"/>, to the nearer side that has room.</summary>
        protected static float Clear(float across, float centre, float reach, float limit) => Hazards.Clear(across, centre, reach, limit);

        /// <summary>
        /// Water piled up against a barrier runs along it to the way through: a raft held against <paramref name="wall"/>
        /// (lying across the river at <paramref name="at"/>) is carried along it toward <paramref name="gap"/> (metres across,
        /// positive the left) at a walking pace or so, unless it's already lined up with the gap. Keeps a raft from sitting
        /// pinned in dead water, and from sliding the wrong way: fast water pressing a raft that lies at an angle into a
        /// wall drives it sideways like a keel, whichever way it happens to point.
        /// </summary>
        protected static void AlongTheWall(Boat boat, RiverSample at, float wall, float gap, float gapWidth, float dt)
        {
            const float Slide = 3f; // metres a second along the wall
            const float Grip = 6f; // how quickly the water brings a raft up to that, per second
            const float MaxPull = 1000f; // newtons: more than fast water pressing a raft side-on into a wall
            if (!Afloat(boat)) return;
            var offset = boat.Body.Position - at.Centre;
            var upstream = -Vector2.Dot(offset, at.Tangent);
            if (upstream < 0f || upstream > wall * 0.5f + RaftRadius + 1.2f) return;
            var across = Vector2.Dot(offset, at.Left) - gap;
            if (Mathf.Abs(across) < Mathf.Max(0.3f, gapWidth * 0.5f - RaftRadius)) return;
            var toward = -Mathf.Sign(across);
            var short_ = toward * Slide - Vector2.Dot(boat.Body.Velocity, at.Left);
            if (short_ * toward <= 0f) return; // already going that way at least that fast
            var pull = Mathf.Clamp(short_ * Grip * boat.Body.Mass, -MaxPull, MaxPull);
            boat.Push(at.Left * (pull * dt));
        }

        /// <summary>
        /// Bodies the host simulates for this obstacle that move freely (geese): sent to joined players with the barrels
        /// and logs, after them, in this order.
        /// </summary>
        public virtual IReadOnlyList<Rigidbody2D> LooseBodies => System.Array.Empty<Rigidbody2D>();

        /// <summary>Puts its loose bodies back where they start (a new race).</summary>
        public virtual void ResetLoose()
        {
        }

        /// <summary>
        /// A number from 0 to 1 that depends only on <paramref name="a"/> and <paramref name="b"/>, the same on every
        /// device: where the n-th tentacle or crate comes down, say.
        /// </summary>
        protected static float Hash(int a, int b)
        {
            unchecked
            {
                var h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ 0x27D4EB2Fu;
                h ^= h >> 15;
                h *= 0x2C1B3C6Du;
                h ^= h >> 12;
                h *= 0x297A2D39u;
                h ^= h >> 15;
                return (h >> 8) / 16777216f;
            }
        }

        /// <summary>Which round of a repeating timetable the clock is in (counting from the race clock's zero).</summary>
        protected static int Round(double seconds, float period, float offset) => (int)System.Math.Floor((seconds + offset) / period);

        /// <summary>The direction from <paramref name="from"/> to a raft, or <paramref name="fallback"/> if it's right on it.</summary>
        protected static Vector2 Away(Vector2 from, Vector2 to, Vector2 fallback)
        {
            var offset = to - from;
            return offset.sqrMagnitude > 1e-4f ? offset.normalized : fallback;
        }

        /// <summary>The point on the line from <paramref name="a"/> to <paramref name="b"/> nearest <paramref name="point"/>.</summary>
        protected static Vector2 Nearest(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return a + ab * t;
        }

        /// <summary>A direction turned a quarter left.</summary>
        public static Vector2 Perpendicular(Vector2 direction) => new(-direction.y, direction.x);

        /// <summary>The water-plane angle, in degrees, of a direction.</summary>
        protected static float Degrees(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
}
