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
        public const float GeyserWarningSeconds = 1.5f;
        public const float GeyserSpoutSeconds = 0.8f;
        public const float CrocLength = 2.6f;
        public const float CrocRadius = 0.45f;
        public const float ArmWidth = 0.45f;
        const float RaftRadius = 1.6f;
        const float LogHalfWidth = 0.35f;
        const float SwingLow = 0.75f; // within this share of its swing from the middle, the log hangs low enough to hit
        const float LogShove = 150f;
        const float LogHeel = 6f;
        const float GeyserShove = 90f;
        const float GeyserHeel = 5f; // a big lurch unbraced; braced, the crew rides it out

        readonly Course course;
        readonly List<(float distance, float across)> wheelSpots = new();
        readonly List<(float distance, float across)> geyserSpots = new();
        readonly List<Rigidbody2D> wheels = new();
        readonly List<Rigidbody2D> crocs = new();
        readonly Dictionary<(Boat, int), double> lastHit = new();

        public Hazards(Course course, Transform parent)
        {
            this.course = course;
            var def = course.Def;
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
                    box.size = new Vector2(wheel.radius, ArmWidth);
                    box.offset = new Vector2(wheel.radius * 0.5f, 0f);
                }

                wheels.Add(go.GetComponent<Rigidbody2D>());
                var at = course.River.Project(wheel.position);
                wheelSpots.Add((at.Distance, at.Lateral));
            }

            foreach (var geyser in def.geysers)
            {
                var at = course.River.Project(geyser.position);
                geyserSpots.Add((at.Distance, at.Lateral));
            }

            for (var i = 0; i < def.crocodiles.Length; i++)
            {
                var (position, heading, _) = Croc(i, 0);
                var go = Kinematic($"Crocodile {i}", position, parent);
                go.transform.rotation = Quaternion.Euler(0f, 0f, heading * Mathf.Rad2Deg);
                var capsule = go.AddComponent<CapsuleCollider2D>();
                capsule.direction = CapsuleDirection2D.Horizontal;
                capsule.size = new Vector2(CrocLength, 2f * CrocRadius);
                capsule.sharedMaterial = new PhysicsMaterial2D("Crocodile") { bounciness = 0.4f, friction = 0.3f };
                crocs.Add(go.GetComponent<Rigidbody2D>());
            }
        }

        public CourseDef Def => course.Def;

        /// <summary>A wheel's turn, in degrees, at a time on the race clock.</summary>
        public float WheelAngleDeg(int i, double seconds) => (float)(Def.wheels[i].degreesPerSecond * seconds % 360.0);

        /// <summary>
        /// Where a crocodile is: waiting at the edge for half its period, lunging across (a fifth of its period: quick,
        /// but not so quick it throws a raft clean over the far edge), lingering, then swimming back. <c>lunging</c> is
        /// true while it's out in the river (jaws open).
        /// </summary>
        public (Vector2 position, float headingRad, bool lunging) Croc(int i, double seconds)
        {
            var croc = Def.crocodiles[i];
            var at = course.River.At(croc.distance);
            var phase = Phase(seconds, croc.period, croc.offset);
            var outward = phase < 0.5f ? 0f : phase < 0.7f ? Smooth((phase - 0.5f) / 0.2f) : phase < 0.8f ? 1f : 1f - Smooth((phase - 0.8f) / 0.2f);
            var waiting = croc.side * (at.HalfWidth + 0.4f);
            var far = croc.side * (at.HalfWidth - croc.reach * 2f * at.HalfWidth);
            var position = at.Centre + at.Left * Mathf.Lerp(waiting, far, outward);
            var facing = phase < 0.8f ? -croc.side * at.Left : croc.side * at.Left; // into the river, then home
            return (position, Mathf.Atan2(facing.y, facing.x), phase >= 0.5f && phase < 0.8f);
        }

        /// <summary>
        /// A swinging log: the middle of its footprint on the water, which way the river runs there, how far across it
        /// is, the way it's swinging (+1 toward the left bank), and whether it hangs low enough to hit.
        /// </summary>
        public (Vector2 centre, Vector2 along, float across, float swing, bool low) Swing(int i, double seconds)
        {
            var swinger = Def.swingers[i];
            var at = course.River.At(swinger.distance);
            var amplitude = Mathf.Max(0.5f, at.HalfWidth - 0.6f);
            var angle = 2f * Mathf.PI * Phase(seconds, swinger.period, swinger.offset);
            var across = amplitude * Mathf.Sin(angle);
            return (at.Centre + at.Left * across, at.Tangent, across, Mathf.Sign(Mathf.Cos(angle)), Mathf.Abs(across) < amplitude * SwingLow);
        }

        /// <summary>A geyser: 0 when quiet, rising to 1 as it bubbles toward a spout; <paramref name="spouting"/> while it spouts.</summary>
        public float GeyserWarning(int i, double seconds, out bool spouting)
        {
            var geyser = Def.geysers[i];
            var t = (float)Mod(seconds + geyser.offset, geyser.period);
            spouting = t >= geyser.period - GeyserSpoutSeconds;
            var warnFrom = geyser.period - GeyserSpoutSeconds - GeyserWarningSeconds;
            return spouting ? 1f : t >= warnFrom ? (t - warnFrom) / GeyserWarningSeconds : 0f;
        }

        /// <summary>Host, every tick before physics: moves the bodies to where the clock says, and knocks rafts caught by a log or a geyser.</summary>
        public void Step(double seconds, IReadOnlyList<Boat> boats)
        {
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

                for (var i = 0; i < Def.swingers.Length; i++)
                {
                    var (centre, along, _, swing, low) = Swing(i, seconds);
                    var offset = raft - centre;
                    var lengthways = Mathf.Abs(Vector2.Dot(offset, along));
                    var sideways = Mathf.Abs(Vector2.Dot(offset, new Vector2(-along.y, along.x)));
                    if (!low || lengthways > Def.swingers[i].logLength * 0.5f + RaftRadius || sideways > LogHalfWidth + RaftRadius) continue;
                    if (boat.BracedFraction >= 0.5f || !Ready(boat, i, seconds)) continue; // ducked under it
                    boat.Shove(new Vector2(-along.y, along.x) * (swing * LogShove), LogHeel);
                }

                for (var i = 0; i < Def.geysers.Length; i++)
                {
                    GeyserWarning(i, seconds, out var spouting);
                    var geyser = Def.geysers[i];
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
            for (var i = 0; i < Def.swingers.Length; i++)
            {
                var start = Swing(i, seconds);
                if (Mathf.Abs(Vector2.Dot(position - start.centre, start.along)) > Def.swingers[i].logLength * 0.5f + RaftRadius + 2.5f) continue;
                for (var t = 0f; t <= lookahead; t += 0.1f)
                {
                    var (centre, along, _, _, low) = Swing(i, seconds + t);
                    if (low && Mathf.Abs(Vector2.Dot(position - centre, new Vector2(-along.y, along.x))) < LogHalfWidth + RaftRadius + 0.5f) return true;
                }
            }

            for (var i = 0; i < Def.geysers.Length; i++)
            {
                var geyser = Def.geysers[i];
                if (Vector2.Distance(position, geyser.position) > geyser.radius + RaftRadius + 1f) continue;
                var warning = GeyserWarning(i, seconds + lookahead, out var spouting);
                if (spouting || warning > 0f || GeyserWarning(i, seconds, out _) > 0f) return true;
            }

            return false;
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

            for (var i = 0; i < wheelSpots.Count; i++)
            {
                var (d, lateral) = wheelSpots[i];
                var radius = Def.wheels[i].radius;
                if (Near(d, radius + RaftRadius + 1f)) across = Clear(across, lateral, radius + RaftRadius + 0.5f, limit);
            }

            for (var i = 0; i < geyserSpots.Count; i++)
            {
                var (d, lateral) = geyserSpots[i];
                var radius = Def.geysers[i].radius;
                if (!Near(d, radius + RaftRadius + 1f)) continue;
                if (GeyserWarning(i, seconds + 1.0, out var soon) > 0f || soon || GeyserWarning(i, seconds, out _) > 0f)
                    across = Clear(across, lateral, radius + RaftRadius + 0.3f, limit);
            }

            for (var i = 0; i < Def.crocodiles.Length; i++)
            {
                var croc = Def.crocodiles[i];
                if (!Near(croc.distance, CrocRadius + RaftRadius + 1f)) continue;
                var phase = Phase(seconds + 1.0, croc.period, croc.offset);
                if (phase < 0.4f || phase > 0.9f) continue;
                var here = course.River.At(croc.distance);
                var body = croc.side * (here.HalfWidth - croc.reach * 2f * here.HalfWidth); // its middle at full stretch
                var safe = body - croc.side * (CrocLength * 0.5f + RaftRadius + 0.6f); // clear of its snout
                if ((across - safe) * croc.side > 0f) across = safe;
            }

            return Mathf.Clamp(across, -limit, limit);
        }

        /// <summary>Moves <paramref name="across"/> out of a band round <paramref name="centre"/>, to the nearer side that has room.</summary>
        static float Clear(float across, float centre, float reach, float limit)
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
        bool Ready(Boat boat, int hazard, double seconds)
        {
            if (lastHit.TryGetValue((boat, hazard), out var when) && seconds - when < 1.0) return false;
            lastHit[(boat, hazard)] = seconds;
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
