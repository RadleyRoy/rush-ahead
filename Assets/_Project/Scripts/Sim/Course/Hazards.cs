using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// A course's moving obstacles. All of them run on the race clock (the host's world tick, in seconds), so every
    /// device can place them exactly from the clock alone and nothing about them is sent over the network: paddle
    /// wheels turn, crocodiles lunge on a timetable, logs swing under bridges, geysers spout.
    /// The host steps their physics (<see cref="Step"/>). Wheels and crocodiles are kinematic bodies that shove whatever
    /// they meet; a swinging log knocks a raft over unless at least half the crew ducks (braces); a geyser throws a raft
    /// off balance, much less if it's braced.
    /// </summary>
    public sealed class Hazards
    {
        public const float GeyserWarningSeconds = 0.9f;
        public const float GeyserSpoutSeconds = 0.6f;
        public const float CrocLength = 2.6f;
        public const float CrocRadius = 0.45f;
        public const float ArmWidth = 0.45f;
        const float RaftRadius = 1.6f;
        const float LogHalfWidth = 0.35f;
        public const float LogClearance = 0.45f; // a swinging log's middle above the water at the bottom of its swing
        public const float SwingDegrees = 55f; // how far out either way it swings on its ropes
        public const float LogRadius = 0.3f;
        const float TubeTop = 0.75f; // the raft's tube, above the water
        const float CrewTop = 1.5f; // the crew's heads
        const float CrewReach = 1.15f; // how far out from the raft's middle the crew stand
        const float LogShove = 150f;
        const float LogHeel = 6f;
        const float GeyserShove = 90f;
        const float GeyserHeel = 7f; // over it goes, unbraced; braced, the crew rides it out
        const float CrocShove = 95f;
        const float CrocHeel = 3f; // a lurch, not a capsize: the danger is the shove toward the far edge

        readonly Course course;
        readonly List<(float distance, float across)> wheelSpots = new();
        readonly List<(float distance, float across)> geyserSpots = new();
        readonly List<Rigidbody2D> wheels = new();
        readonly List<Rigidbody2D> crocs = new();
        readonly Dictionary<(Boat, object, int), double> lastHit = new();
        readonly List<Obstacle> obstacles = new();
        readonly List<float> obstacleDistances = new();
        readonly List<(float distance, int kind, int index)> steering = new();
        static readonly System.Comparison<(float distance, int kind, int index)> FarthestFirst = (a, b) => b.distance.CompareTo(a.distance);

        /// <summary>The order to steer round things in: behind the raft first (they matter least), then ahead from the farthest.</summary>
        const float CrocEarly = 8f; // metres past the look-ahead at which a raft starts moving over for a crocodile

        static float SteerRank(float distance, float from) => distance >= from - 2f ? distance : float.MaxValue;
        readonly List<BoostLane> boosts = new();
        readonly List<Marker> markers = new();
        double lastSeconds = double.NaN;
        static readonly PhysicsMaterial2D Paddle = new("Paddle") { bounciness = 0.5f, friction = 0.2f };

        /// <summary>
        /// The race clock's time at the start (the GO) of the race being run, or NaN between races. What happens once a
        /// race (a tree falling) is timed from it, so every device agrees from the clock alone.
        /// </summary>
        public double RaceStart { get; set; } = double.NaN;

        public Hazards(Course course, Transform parent)
        {
            this.course = course;
            var def = course.Layout;
            PowerUps = new PowerUps(course, def.crates, def.Seed);
            if (course.Def.boss) Boss = new BossCroc(course);
            for (var i = 0; i < def.wheels.Length; i++)
            {
                var wheel = def.wheels[i];
                var go = Kinematic($"Wheel {i}", wheel.position, parent);
                go.AddComponent<CircleCollider2D>().radius = 0.5f; // the hub
                for (var arm = 0; arm < Mathf.Max(2, wheel.arms); arm++)
                {
                    var child = new GameObject($"Arm {arm}");
                    child.transform.SetParent(go.transform, false);
                    child.transform.localRotation = Quaternion.Euler(0f, 0f, arm * 360f / Mathf.Max(2, wheel.arms));
                    var box = child.AddComponent<BoxCollider2D>();
                    box.sharedMaterial = Paddle;
                    box.size = new Vector2(wheel.radius, ArmWidth);
                    box.offset = new Vector2(wheel.radius * 0.5f, 0f);
                }

                wheels.Add(go.GetComponent<Rigidbody2D>());
                var at = course.River.Project(wheel.position);
                wheelSpots.Add((at.Distance, at.Lateral));
                Mark(ObstacleKind.Wheel, at.Distance, at.Lateral);
            }

            foreach (var geyser in def.geysers)
            {
                var at = course.River.Project(geyser.position);
                geyserSpots.Add((at.Distance, at.Lateral));
                Mark(ObstacleKind.Geyser, at.Distance, at.Lateral);
            }

            for (var i = 0; i < def.crocodiles.Length; i++)
            {
                var (position, heading, _) = Croc(i, 0);
                var go = Kinematic($"Crocodile {i}", position, parent);
                go.transform.rotation = Quaternion.Euler(0f, 0f, heading * Mathf.Rad2Deg);
                var capsule = go.AddComponent<CapsuleCollider2D>();
                capsule.direction = CapsuleDirection2D.Horizontal;
                capsule.size = new Vector2(CrocLength, 2f * CrocRadius);
                capsule.isTrigger = true; // its bite does the work: a body switching solid with a raft inside it flings the raft clean off
                crocs.Add(go.GetComponent<Rigidbody2D>());
                Mark(ObstacleKind.Crocodile, def.crocodiles[i].distance, 0f);
            }

            foreach (var swing in def.swingers) Mark(ObstacleKind.SwingingLog, swing.distance, 0f);

            foreach (var spot in def.rockslides) Add(new Rockslide(course, parent, spot), ObstacleKind.Rockslide, spot.distance, Bank(spot.distance, spot.side));
            foreach (var spot in def.hippos) Add(new Hippo(course, parent, spot), ObstacleKind.Hippo, spot.distance, spot.across);
            foreach (var spot in def.sluices) Add(new Sluice(course, parent, spot), ObstacleKind.Sluice, spot.distance);
            foreach (var spot in def.sweepers) Add(new Sweeper(course, parent, spot), ObstacleKind.Sweeper, spot.distance, spot.across);
            foreach (var spot in def.cannons) Add(new Cannon(course, spot), ObstacleKind.Cannon, spot.distance, Bank(spot.distance, spot.side));
            foreach (var spot in def.rapids) Add(new Rapids(course, spot), ObstacleKind.Rapids, spot.from);
            foreach (var spot in def.weirs) Add(new Weir(course, spot), ObstacleKind.Weir, spot.distance);
            foreach (var spot in def.bumpers) Add(new Bumper(course, parent, spot), ObstacleKind.Bumper, course.River.Project(spot.position).Distance, course.River.Project(spot.position).Lateral);
            foreach (var spot in def.ferries) Add(new Ferry(course, parent, spot), ObstacleKind.Ferry, spot.distance);
            foreach (var spot in def.bears) Add(new Bear(course, spot), ObstacleKind.Bear, spot.distance, Bank(spot.distance, spot.side));
            foreach (var spot in def.trees) Add(new FallingTree(course, parent, spot), ObstacleKind.FallingTree, spot.distance, Bank(spot.distance, spot.side));
            foreach (var spot in def.turtles) Add(new Turtle(course, parent, spot), ObstacleKind.Turtle, spot.distance);
            foreach (var spot in def.shoals) Add(new PiranhaShoal(course, spot), ObstacleKind.PiranhaShoal, spot.distance);
            foreach (var spot in def.waterspouts) Add(new Waterspout(course, spot), ObstacleKind.Waterspout, spot.distance);
            foreach (var spot in def.eels) Add(new ElectricEels(course, spot), ObstacleKind.ElectricEels, spot.distance);
            foreach (var spot in def.mudslides) Add(new Mudslide(course, spot), ObstacleKind.Mudslide, spot.distance, Bank(spot.distance, spot.side));
            foreach (var spot in def.floes) Add(new IceFloes(course, parent, spot), ObstacleKind.IceFloes, spot.distance);
            foreach (var spot in def.dams) Add(new BeaverDam(course, parent, spot), ObstacleKind.BeaverDam, spot.distance);
            foreach (var spot in def.pelicans) Add(new PelicanDive(course, spot), ObstacleKind.Pelican, spot.distance, 0.5f * Bank(spot.distance, spot.side));
            foreach (var spot in def.netBoats) Add(new NetBoat(course, parent, spot), ObstacleKind.NetBoat, spot.distance);
            foreach (var spot in def.extras)
                if (NewObstacles.Create(course, parent, spot) is { } obstacle)
                    Add(obstacle, spot.kind, spot.distance, spot.side != 0f ? Bank(spot.distance, spot.side) : spot.across);
            foreach (var spot in def.boosts)
            {
                boosts.Add(new BoostLane(course, spot));
                markers.Add(new Marker("BoostLane", null, spot.distance, spot.across, null));
            }

            foreach (var pool in course.Def.whirlpools)
            {
                var at = course.River.Project(pool.position);
                markers.Add(new Marker("Whirlpool", null, at.Distance - pool.radius * 0.5f, at.Lateral, null));
            }
        }

        /// <summary>The bank on one side (+1 the left), as metres across from the middle.</summary>
        float Bank(float distance, float side) => side * course.River.At(distance).HalfWidth;

        void Add(Obstacle obstacle, ObstacleKind kind, float distance, float across = 0f)
        {
            obstacle.Owner = this;
            obstacles.Add(obstacle);
            obstacleDistances.Add(distance);
            Mark(kind, distance, across, obstacle);
        }

        void Mark(ObstacleKind kind, float distance, float across, Obstacle obstacle = null) =>
            markers.Add(new Marker(kind.ToString(), kind, distance, across, obstacle));

        /// <summary>Where something on this round's river is and what it is, for the warnings and the Field Guide.</summary>
        public readonly struct Marker
        {
            /// <summary>Its Field Guide entry (<see cref="ObstacleGuide.ById"/>): the obstacle's kind, or a feature such as "Whirlpool".</summary>
            public readonly string Id;
            /// <summary>The obstacle's kind, or null for a course feature.</summary>
            public readonly ObstacleKind? Kind;
            /// <summary>Metres down the river.</summary>
            public readonly float Distance;
            /// <summary>Metres across from the middle (positive the left), where it matters.</summary>
            public readonly float Across;
            /// <summary>The obstacle itself (null for the first four, which live in here).</summary>
            public readonly Obstacle Obstacle;

            public Marker(string id, ObstacleKind? kind, float distance, float across, Obstacle obstacle)
            {
                Id = id;
                Kind = kind;
                Distance = distance;
                Across = across;
                Obstacle = obstacle;
            }
        }

        /// <summary>The wind now (see <see cref="Gusts.At"/>): none without rain, or before the race starts.</summary>
        public (float strength, int side, float warning) Gust(double seconds) =>
            course.Gusts == null || double.IsNaN(RaceStart) ? (0f, 0, 0f) : course.Gusts.At(seconds - RaceStart);

        /// <summary>For AI crews: which way the wind blows or is about to here, and how hard (zero for none).</summary>
        public Vector2 WindComing(Vector2 position, double seconds) =>
            course.Gusts == null || double.IsNaN(RaceStart) ? Vector2.zero : course.Gusts.Coming(course.River, position, seconds - RaceStart);

        /// <summary>Whether the river between two distances down it is free of obstacles (for an AI crew deciding to turbo).</summary>
        public bool ClearBetween(float from, float to)
        {
            foreach (var marker in Markers)
            {
                if (marker.Distance > to) break;
                if (marker.Kind != null && marker.Distance > from - 6f) return false;
            }

            return true;
        }

        /// <summary>Every obstacle this round, nearest the start first.</summary>
        public IReadOnlyList<Marker> Markers
        {
            get
            {
                if (!sorted)
                {
                    markers.Sort((a, b) => a.Distance.CompareTo(b.Distance));
                    sorted = true;
                }

                return markers;
            }
        }

        bool sorted;

        /// <summary>The newer obstacles (rockslides, hippos, sluice gates and the rest), in the order the course lists them by type.</summary>
        public IReadOnlyList<Obstacle> Obstacles => obstacles;

        /// <summary>This round's boost lanes.</summary>
        public IReadOnlyList<BoostLane> Boosts => boosts;

        /// <summary>This round's power-up crates, and what rafts do with what they find in them.</summary>
        public PowerUps PowerUps { get; private set; }

        /// <summary>An air horn: every creature near takes fright.</summary>
        public void Scare(Vector2 at, float radius, double seconds)
        {
            foreach (var obstacle in obstacles) obstacle.Scare(at, radius, seconds);
            Boss?.Scare(at, radius);
        }

        /// <summary>Old Snapjaw, on the boss course; null elsewhere.</summary>
        public BossCroc Boss { get; }

        public CourseDef Def => course.Def;

        /// <summary>Every obstacle's freely moving bodies (geese), in a fixed order (see <see cref="Obstacle.LooseBodies"/>).</summary>
        public IEnumerable<Rigidbody2D> LooseBodies
        {
            get
            {
                foreach (var obstacle in obstacles)
                    foreach (var body in obstacle.LooseBodies)
                        yield return body;
            }
        }

        /// <summary>Puts every obstacle's loose bodies back where they start.</summary>
        public void ResetLoose()
        {
            foreach (var obstacle in obstacles) obstacle.ResetLoose();
            Boss?.Reset();
        }

        /// <summary>Every obstacle's walls across the river (see <see cref="Obstacle.Barriers"/>).</summary>
        public IEnumerable<Collider2D> Barriers
        {
            get
            {
                foreach (var obstacle in obstacles)
                    foreach (var barrier in obstacle.Barriers)
                        yield return barrier;
            }
        }

        /// <summary>Where this round's obstacles are.</summary>
        public CourseLayout Layout => course.Layout;

        /// <summary>A wheel's turn, in degrees, at a time on the race clock.</summary>
        public float WheelAngleDeg(int i, double seconds) => (float)(Layout.wheels[i].degreesPerSecond * seconds % 360.0);

        /// <summary>
        /// Where a crocodile is: waiting at the edge for half its period, lunging across fast (an eighth of its period),
        /// lingering with its jaws open, then swimming back. <c>lunging</c> is true while it's out in the river.
        /// </summary>
        public (Vector2 position, float headingRad, bool lunging) Croc(int i, double seconds)
        {
            var croc = Layout.crocodiles[i];
            var at = course.River.At(croc.distance);
            var phase = Phase(seconds, croc.period, croc.offset);
            var outward = phase < 0.5f ? 0f : phase < 0.62f ? Smooth((phase - 0.5f) / 0.12f) : phase < 0.75f ? 1f : 1f - Smooth((phase - 0.75f) / 0.25f);
            var waiting = croc.side * (at.HalfWidth + 0.4f);
            var far = croc.side * (at.HalfWidth - croc.reach * 2f * at.HalfWidth);
            var position = at.Centre + at.Left * Mathf.Lerp(waiting, far, outward);
            var facing = phase < 0.75f ? -croc.side * at.Left : croc.side * at.Left; // into the river, then home
            return (position, Mathf.Atan2(facing.y, facing.x), phase >= 0.5f && phase < 0.75f);
        }

        /// <summary>
        /// A swinging log: the middle of its footprint on the water, which way the river runs there, how far across it
        /// is, the way it's swinging (+1 toward the left bank), and whether it hangs low enough to hit.
        /// </summary>
        public (Vector2 centre, Vector2 along, float across, float swing, bool low) Swing(int i, double seconds)
        {
            var swinger = Layout.swingers[i];
            var at = course.River.At(swinger.distance);
            var amplitude = SwingAmplitude(i);
            var angle = 2f * Mathf.PI * Phase(seconds, swinger.period, swinger.offset);
            var across = amplitude * Mathf.Sin(angle);
            return (at.Centre + at.Left * across, at.Tangent, across, Mathf.Sign(Mathf.Cos(angle)), LogHeight(i, across) - LogRadius < CrewTop);
        }

        /// <summary>How far either way a swinging log's middle swings across the river.</summary>
        float SwingAmplitude(int i) => Mathf.Max(0.5f, course.River.At(Layout.swingers[i].distance).HalfWidth - 0.6f);

        /// <summary>The ropes a swinging log hangs from: long enough to swing it out to <see cref="SwingDegrees"/> either way.</summary>
        public float LogRope(int i) => SwingAmplitude(i) / Mathf.Sin(SwingDegrees * Mathf.Deg2Rad);

        /// <summary>How high a swinging log's middle hangs over the water when it's <paramref name="across"/> out from the bottom of its swing (the view draws it there).</summary>
        public float LogHeight(int i, float across)
        {
            var rope = LogRope(i);
            return LogClearance + rope - Mathf.Sqrt(Mathf.Max(0f, rope * rope - across * across));
        }

        /// <summary>A geyser: 0 when quiet, rising to 1 as it bubbles toward a spout; <paramref name="spouting"/> while it spouts.</summary>
        public float GeyserWarning(int i, double seconds, out bool spouting)
        {
            var geyser = Layout.geysers[i];
            var t = (float)Mod(seconds + geyser.offset, geyser.period);
            spouting = t >= geyser.period - GeyserSpoutSeconds;
            var warnFrom = geyser.period - GeyserSpoutSeconds - GeyserWarningSeconds;
            return spouting ? 1f : t >= warnFrom ? (t - warnFrom) / GeyserWarningSeconds : 0f;
        }

        /// <summary>Host, every tick before physics: moves the bodies to where the clock says, and knocks rafts caught by a log or a geyser.</summary>
        public void Step(double seconds, IReadOnlyList<Boat> boats)
        {
            var dt = double.IsNaN(lastSeconds) ? 1f / 60f : Mathf.Clamp((float)(seconds - lastSeconds), 0f, 0.1f);
            lastSeconds = seconds;
            foreach (var obstacle in obstacles) obstacle.Step(seconds, dt, boats);
            foreach (var boost in boosts) boost.Step(dt, boats, course.WaterVelocityAt);
            PowerUps.Step(seconds, dt, boats);
            Boss?.Step(seconds, dt, boats, RaceStart);
            if (course.Gusts != null && !double.IsNaN(RaceStart))
                foreach (var boat in boats)
                    if (boat.State == BoatState.Afloat && !boat.Moored)
                        boat.Push(course.Gusts.Push(course.River, boat.Body.Position, seconds - RaceStart, boat.BracedFraction) * dt);

            for (var i = 0; i < wheels.Count; i++) wheels[i].MoveRotation(WheelAngleDeg(i, seconds));
            for (var i = 0; i < crocs.Count; i++)
            {
                var (position, heading, _) = Croc(i, seconds);
                crocs[i].MovePosition(position);
                crocs[i].MoveRotation(heading * Mathf.Rad2Deg);
            }

            foreach (var boat in boats)
            {
                if (boat.State != BoatState.Afloat) continue;
                var raft = boat.Body.Position;

                for (var i = 0; i < Layout.swingers.Length; i++)
                {
                    // It hits what it passes through: the tube while it hangs that low, the crew while it's below their heads.
                    var (centre, along, across, swing, _) = Swing(i, seconds);
                    var offset = raft - centre;
                    var lengthways = Mathf.Abs(Vector2.Dot(offset, along));
                    var sideways = Mathf.Abs(Vector2.Dot(offset, new Vector2(-along.y, along.x)));
                    var underside = LogHeight(i, across) - LogRadius;
                    var reach = underside < TubeTop ? RaftRadius : underside < CrewTop ? CrewReach : -1f;
                    if (reach < 0f || lengthways > Layout.swingers[i].logLength * 0.5f + reach || sideways > LogHalfWidth + reach) continue;
                    if (boat.BracedFraction >= 0.5f || !Ready(boat, i, seconds)) continue; // ducked under it
                    boat.Shove(new Vector2(-along.y, along.x) * (swing * LogShove), LogHeel);
                }

                // A lunging crocodile bites whatever its jaws reach: a hard shove and a heavy heel.
                for (var i = 0; i < Layout.crocodiles.Length; i++)
                {
                    var (head, heading, lunging) = Croc(i, seconds);
                    if (!lunging) continue;
                    var snout = head + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * (CrocLength * 0.5f);
                    if (Vector2.Distance(snout, raft) > RaftRadius + CrocRadius + 0.3f || !Ready(boat, 2000 + i, seconds)) continue;
                    var away = raft - head;
                    boat.Shove((away.sqrMagnitude > 1e-4f ? away.normalized : Vector2.up) * CrocShove, CrocHeel);
                }

                for (var i = 0; i < Layout.geysers.Length; i++)
                {
                    GeyserWarning(i, seconds, out var spouting);
                    var geyser = Layout.geysers[i];
                    var offset = raft - geyser.position;
                    if (!spouting || offset.magnitude > geyser.radius + RaftRadius * 0.5f || !Ready(boat, 1000 + i, seconds)) continue;
                    var outward = offset.sqrMagnitude > 1e-4f ? offset.normalized : Vector2.up;
                    boat.Shove(outward * GeyserShove, GeyserHeel);
                }
            }
        }

        /// <summary>
        /// For AI crews: whether something that bracing beats is about to reach a raft here: a log swinging low through
        /// it, or a geyser about to spout under it, within <paramref name="lookahead"/> seconds.
        /// </summary>
        public bool BraceNeeded(Vector2 position, double seconds, float lookahead)
        {
            for (var i = 0; i < Layout.swingers.Length; i++)
            {
                var start = Swing(i, seconds);
                if (Mathf.Abs(Vector2.Dot(position - start.centre, start.along)) > Layout.swingers[i].logLength * 0.5f + RaftRadius + 2.5f) continue;
                for (var t = 0f; t <= lookahead; t += 0.1f)
                {
                    var (centre, along, _, _, low) = Swing(i, seconds + t);
                    if (low && Mathf.Abs(Vector2.Dot(position - centre, new Vector2(-along.y, along.x))) < LogHalfWidth + RaftRadius + 0.5f) return true;
                }
            }

            for (var i = 0; i < Layout.geysers.Length; i++)
            {
                var geyser = Layout.geysers[i];
                if (Vector2.Distance(position, geyser.position) > geyser.radius + RaftRadius + 1f) continue;
                var warning = GeyserWarning(i, seconds + lookahead, out var spouting);
                if (spouting || warning > 0f || GeyserWarning(i, seconds, out _) > 0f) return true;
            }

            // A crocodile about to lunge at a raft that couldn't get past its reach in time: braced, the bite is a lurch.
            if (Layout.crocodiles.Length > 0)
            {
                var here = course.River.Project(position);
                for (var i = 0; i < Layout.crocodiles.Length; i++)
                {
                    var croc = Layout.crocodiles[i];
                    var ahead = croc.distance - here.Distance;
                    if (ahead < -(RaftRadius + 1f) || ahead > RaftRadius + 4f) continue;
                    var at = course.River.At(croc.distance);
                    var snout = croc.side * (at.HalfWidth - croc.reach * 2f * at.HalfWidth) - croc.side * (CrocLength * 0.5f);
                    if ((here.Lateral - snout) * croc.side < -(RaftRadius + CrocRadius + 0.3f)) continue; // beyond its jaws
                    for (var t = 0f; t <= lookahead; t += 0.1f)
                        if (Croc(i, seconds + t).lunging) return true;
                }
            }

            foreach (var obstacle in obstacles)
                if (obstacle.BraceNeeded(position, seconds, lookahead)) return true;
            return false;
        }

        /// <summary>
        /// For the hit-zone overlay: what a raft's rim (or, for a log at head height, its crew) must touch to be hit:
        /// each low-hanging log, a lunging crocodile's jaws, a spouting geyser, and every newer obstacle's own.
        /// </summary>
        public void Zones(double seconds, List<Vector2> lines)
        {
            for (var i = 0; i < Layout.swingers.Length; i++)
            {
                var (centre, along, _, _, low) = Swing(i, seconds);
                if (low) Obstacle.Box(lines, centre, along, Layout.swingers[i].logLength * 0.5f, LogHalfWidth);
            }

            for (var i = 0; i < Layout.crocodiles.Length; i++)
            {
                var (head, heading, lunging) = Croc(i, seconds);
                if (lunging) Obstacle.Circle(lines, head + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * (CrocLength * 0.5f), CrocRadius + 0.3f);
            }

            for (var i = 0; i < Layout.geysers.Length; i++)
                if (GeyserWarning(i, seconds, out _) > 0f) Obstacle.Circle(lines, Layout.geysers[i].position, Layout.geysers[i].radius - RaftRadius * 0.5f);

            foreach (var obstacle in obstacles) obstacle.Zones(seconds, lines);
            foreach (var boost in boosts) Obstacle.Box(lines, boost.Middle, boost.At.Tangent, BoostLane.Length * 0.5f, BoostLane.HalfWidth);
        }

        /// <summary>
        /// For AI crews: where across the river to aim, to keep clear of what can be seen coming between where the raft
        /// is (<paramref name="from"/> metres down the river) and where it's aiming (<paramref name="to"/>): round a
        /// paddle wheel, away from a geyser that's bubbling, and over to the far side from a crocodile about to lunge.
        /// Logs aren't dodged (they swing right across): those are for bracing.
        /// </summary>
        public float SteerAcross(float from, float to, float across, double seconds)
        {
            var at = course.River.At(to);
            var limit = Mathf.Max(0f, at.HalfWidth - RaftRadius - 0.6f);
            bool Near(float distance, float reach) => distance + reach >= from && distance - reach <= to;

            // A boost lane first, so anything to keep clear of afterwards wins.
            foreach (var boost in boosts) across = boost.SteerAcross(from, across, limit);

            // Then everything else: what the raft is past first, then what's ahead from the farthest to the nearest, so the
            // nearest has the final say. A dodge for something further on must never steer it into what's about to hit it.
            steering.Clear();
            for (var i = 0; i < wheelSpots.Count; i++) steering.Add((SteerRank(wheelSpots[i].distance, from), 0, i));
            for (var i = 0; i < geyserSpots.Count; i++) steering.Add((SteerRank(geyserSpots[i].distance, from), 1, i));
            for (var i = 0; i < Layout.crocodiles.Length; i++) steering.Add((SteerRank(Layout.crocodiles[i].distance, from), 2, i));
            for (var i = 0; i < obstacles.Count; i++) steering.Add((SteerRank(obstacleDistances[i], from), 3, i));
            steering.Sort(FarthestFirst);
            foreach (var (_, kind, i) in steering)
            {
                switch (kind)
                {
                    case 0:
                    {
                        var (d, lateral) = wheelSpots[i];
                        var radius = Layout.wheels[i].radius;
                        if (Near(d, radius + RaftRadius + 1.5f)) across = Clear(across, lateral, radius + RaftRadius + 1.2f, limit);
                        break;
                    }
                    case 1:
                    {
                        var (d, lateral) = geyserSpots[i];
                        var radius = Layout.geysers[i].radius;
                        if (!Near(d, radius + RaftRadius + 1f)) break;
                        if (GeyserWarning(i, seconds + 1.0, out var soon) > 0f || soon || GeyserWarning(i, seconds, out _) > 0f)
                            across = Clear(across, lateral, radius + RaftRadius + 0.3f, limit);
                        break;
                    }
                    case 2:
                    {
                        // Passing a crocodile: keep to the far side of where its jaws get to, whatever it's doing.
                        // It lunges a third of the time, too often to time at this speed, and getting across takes a
                        // raft longer than the lunge gives warning, so start a few metres early.
                        var croc = Layout.crocodiles[i];
                        if (croc.distance + CrocRadius + RaftRadius + 1f < from || croc.distance - CrocEarly > to) break;
                        var here = course.River.At(croc.distance);
                        var body = croc.side * (here.HalfWidth - croc.reach * 2f * here.HalfWidth); // its middle at full stretch
                        var safe = body - croc.side * (CrocLength * 0.5f + RaftRadius + CrocRadius + 0.8f); // past its bite
                        if ((across - safe) * croc.side > 0f) across = safe;
                        break;
                    }
                    default:
                        across = obstacles[i].SteerAcross(from, to, across, seconds, limit);
                        break;
                }
            }

            return Mathf.Clamp(across, -limit, limit);
        }

        /// <summary>Moves <paramref name="across"/> out of a band round <paramref name="centre"/>, to the nearer side that has room.</summary>
        internal static float Clear(float across, float centre, float reach, float limit)
        {
            if (Mathf.Abs(across - centre) >= reach) return across;
            var left = centre + reach;
            var right = centre - reach;
            var preferLeft = across >= centre;
            if (preferLeft && left <= limit) return left;
            if (!preferLeft && right >= -limit) return right;
            return left <= limit ? left : right >= -limit ? right : across;
        }

        /// <summary>One knock per raft per pass: not again for a second.</summary>
        bool Ready(Boat boat, int hazard, double seconds) => Ready(boat, this, hazard, seconds, 1.0);

        /// <summary>One knock per raft per obstacle part: not again for <paramref name="gap"/> seconds.</summary>
        internal bool Ready(Boat boat, object who, int part, double seconds, double gap)
        {
            if (lastHit.TryGetValue((boat, who, part), out var when) && seconds - when < gap && seconds >= when) return false;
            lastHit[(boat, who, part)] = seconds;
            return true;
        }

        static GameObject Kinematic(string name, Vector2 position, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.SetParent(parent, true);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            return go;
        }

        static double Mod(double x, double m) => (x % m + m) % m;

        static float Phase(double seconds, float period, float offset) => (float)(Mod(seconds + offset, period) / period);

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
