using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// A client's raft, rebuilt every frame from the host's snapshots. Position follows a Hermite curve using the sent
    /// velocities (a raft under thrust moves in curves, and the velocity is on the wire anyway); angles take the short
    /// way round; flags and burst counts step rather than blend. Past the newest snapshot it extrapolates briefly, then holds.
    /// </summary>
    public sealed class InterpolatedBoat : IBoatState
    {
        const float MaxExtrapolationSeconds = 0.15f;

        readonly SimConfig config;
        readonly IReadOnlyList<BlowerDef> blowers;
        readonly InterpolatedCrew[] crew = new InterpolatedCrew[CrewSectors.MaxCrew];

        public InterpolatedBoat(SimConfig config, IReadOnlyList<BlowerDef> blowers)
        {
            this.config = config;
            this.blowers = blowers;
            for (var i = 0; i < crew.Length; i++) crew[i] = new InterpolatedCrew { Seat = i, Blower = blowers[0] };
        }

        public bool HasData { get; private set; }
        public int Index => 0;
        public Vector2 RenderPosition { get; private set; }
        public float RenderAngleRad { get; private set; }
        public Vector2 Velocity { get; private set; }
        public Vector2 Tilt { get; private set; }
        public bool Capsized { get; private set; }
        public int CapsizeCount { get; private set; }
        public int TeleportCount { get; private set; }
        public int GatesPassed { get; private set; }
        public int FinishTick { get; private set; }
        public float TiltDanger => Mathf.Clamp01(Tilt.magnitude / config.capsizeAngle);
        public int CrewCount { get; private set; }
        public ICrewState CrewAt(int seat) => crew[Mathf.Clamp(seat, 0, crew.Length - 1)];

        /// <summary>Loose bodies such as barrels, interpolated the same way.</summary>
        public List<PropSnapshot> Props { get; } = new();

        public void Update(SnapshotTimeline timeline)
        {
            // Right after joining, playback starts behind the first snapshot received; wait the few frames until it
            // reaches it rather than showing a pose that's slightly ahead of where the raft should be.
            if (!timeline.Ready) return;
            if (!timeline.Sample(out var from, out var to, out var t, out var overshoot) || from.Boats.Count == 0) return;
            var a = from.Boats[0];
            var b = to.Boats.Count > 0 ? to.Boats[0] : a;

            var span = (to.Tick - from.Tick) / timeline.TickRate;
            var position = span > 0f ? Hermite(a.Position, a.Velocity * span, b.Position, b.Velocity * span, t) : a.Position;
            var angle = a.AngleRad + Mathf.DeltaAngle(a.AngleRad * Mathf.Rad2Deg, b.AngleRad * Mathf.Rad2Deg) * Mathf.Deg2Rad * t;
            if (a.TeleportCount != b.TeleportCount)
            {
                // The raft jumped (a respawn or reset) between these snapshots: cut to where it landed, don't sweep across the map.
                a = b;
                position = b.Position;
                angle = b.AngleRad;
            }
            if (overshoot > 0f)
            {
                var ahead = Mathf.Min(overshoot, MaxExtrapolationSeconds);
                position += b.Velocity * ahead;
                angle += b.AngularVelocity * ahead;
            }

            RenderPosition = position;
            RenderAngleRad = angle;
            Velocity = Vector2.Lerp(a.Velocity, b.Velocity, t);
            Tilt = Vector2.Lerp(a.Tilt, b.Tilt, t);
            Capsized = a.Capsized;
            CapsizeCount = a.CapsizeCount;
            TeleportCount = a.TeleportCount;
            GatesPassed = a.GatesPassed;
            FinishTick = (int)a.FinishTick;

            CrewCount = Mathf.Min(a.Crew.Count, b.Crew.Count, crew.Length);
            for (var i = 0; i < CrewCount; i++)
            {
                var ca = a.Crew[i];
                var cb = b.Crew[i];
                var localAim = ca.LocalAimDeg + Mathf.DeltaAngle(ca.LocalAimDeg, cb.LocalAimDeg) * t;
                var c = crew[i];
                c.AimAngle = angle + localAim * Mathf.Deg2Rad;
                c.Spool = Mathf.Lerp(ca.Spool, cb.Spool, t);
                c.AirFraction = Mathf.Lerp(ca.Air, cb.Air, t);
                c.Braced = ca.Braced;
                c.BurstCount = ca.BurstCount;
                c.Blower = ca.BlowerIndex < blowers.Count ? blowers[ca.BlowerIndex] : blowers[0];
            }

            var props = Mathf.Min(from.Props.Count, to.Props.Count);
            Props.Clear();
            for (var i = 0; i < props; i++)
            {
                var pa = from.Props[i];
                var pb = to.Props[i];
                Props.Add(new PropSnapshot
                {
                    Position = Vector2.Lerp(pa.Position, pb.Position, t),
                    AngleRad = pa.AngleRad + Mathf.DeltaAngle(pa.AngleRad * Mathf.Rad2Deg, pb.AngleRad * Mathf.Rad2Deg) * Mathf.Deg2Rad * t,
                });
            }

            HasData = true;
        }

        static Vector2 Hermite(Vector2 p0, Vector2 m0, Vector2 p1, Vector2 m1, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0 + (t3 - 2f * t2 + t) * m0 + (-2f * t3 + 3f * t2) * p1 + (t3 - t2) * m1;
        }

        sealed class InterpolatedCrew : ICrewState
        {
            public int Seat { get; set; }
            public BlowerDef Blower { get; set; }
            public float AimAngle { get; set; }
            public float Spool { get; set; }
            public bool IsBlowing => Spool > 0.05f && Blower.thrustNewtons > 0f;
            public float AirFraction { get; set; }
            public bool Braced { get; set; }
            public int BurstCount { get; set; }
        }
    }
}
