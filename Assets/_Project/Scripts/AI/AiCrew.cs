using System;
using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.AI
{
    /// <summary>
    /// The AI crewmates on one raft: every seat no human holds. Each tick the crew agrees on one push for the whole raft
    /// (from its current order), then each AI animal whose slice can help blows the opposite way, through its own
    /// personality: it reacts late, its aim wobbles, it manages its air well or badly, it may brace when the raft
    /// leans hard, it may think to burst when stuck, and it sometimes drifts off. If none of its seats faces the right
    /// way to push (the players hold the seats that do, and aren't pushing), it turns the raft round until one does. To the simulation, an AI seat is just another input source,
    /// so AI needs no special case anywhere else: not in the rules, the snapshot or the netcode.
    /// </summary>
    public sealed class AiCrew
    {
        const float LookaheadMetres = 7f;
        const float LaneShare = 0.85f; // how far toward its own lane a Team Race crew steers
        const float SpeedGain = 0.8f; // throttle per m/s of velocity error
        const float BraceOrderSeconds = 3f;
        const float PushHereSeconds = 10f;
        const float ArrivedMetres = 3f;
        const float StuckSpeed = 0.4f;
        const float StuckSeconds = 1.5f;
        const float BurstCooldown = 2f;
        const float TurnWhenShareBelow = 0.4f; // none of the AI can push the way it needs: turn the raft until one can
        const float SettledWithinDeg = 20f;
        const float SpinDeadband = 0.15f; // radians per second of spin the crew lets be before easing off
        const float MaxTurnRate = 0.6f; // radians per second: gentle, so there's air left to stop the spin

        readonly Boat boat;
        readonly AiProfile profile;
        readonly int tickRate;
        readonly System.Random random;
        readonly AiSeat[] seats;
        float orderSeconds;
        float stuckFor;
        float time;
        bool turning;

        public AiCrew(Boat boat, AiProfile profile, int tickRate, int seed = 1)
        {
            this.boat = boat;
            this.profile = profile;
            this.tickRate = tickRate;
            random = new System.Random(seed);
            seats = new AiSeat[boat.Crew.Length];
        }

        public AiProfile Profile => profile;

        /// <summary>The course's river, for following its racing line. Null in open water, where Go follows the players.</summary>
        public River River { get; set; }

        /// <summary>How fast the water moves at a point. The crew aims for its cruising speed through the water, so the flow carries it faster.</summary>
        public Func<Vector2, Vector2> Water { get; set; }

        /// <summary>Whether something bracing beats (a swinging log, a geyser) will reach a raft here within so many seconds.</summary>
        public Func<Vector2, float, bool> BraceAhead { get; set; }

        /// <summary>
        /// Where across the river to aim to keep clear of the obstacles between two distances down it (where the raft is
        /// and where it's aiming), given the line's choice.
        /// </summary>
        public Func<float, float, float, float> SteerAcross { get; set; }

        const float EdgeSense = 2.5f; // metres from the edge where the crew starts steering back in

        bool hazardSeen;
        bool hazardNoticed;

        /// <summary>In a Team Race, which side of the racing line this crew keeps to (0 left, 1 right), so two rafts race
        /// side by side rather than queueing nose to tail. One lane (Co-op) follows the line itself.</summary>
        public int Lane { get; set; }

        public int Lanes { get; set; } = 1;

        public CrewOrder Order { get; private set; }
        public Vector2 PushPoint { get; private set; }

        /// <summary>Where the crew is steering toward this tick, if anywhere: for gizmos and debugging.</summary>
        public Vector2? Target { get; private set; }

        public bool Drives(int seat) => seat >= 0 && seat < seats.Length && seats[seat] != null;

        /// <summary>
        /// Takes over exactly these seats (handing them their input source); any seat it drove before and isn't listed
        /// is let go, for whoever now sits there to wire up.
        /// </summary>
        public void Drive(IReadOnlyList<int> aiSeats)
        {
            var wanted = new bool[seats.Length];
            foreach (var seat in aiSeats)
                if (seat >= 0 && seat < seats.Length) wanted[seat] = true;

            for (var seat = 0; seat < seats.Length; seat++)
            {
                if (!wanted[seat])
                {
                    seats[seat] = null;
                    continue;
                }

                seats[seat] ??= new AiSeat(seat, Mathf.Max(1, Mathf.RoundToInt(profile.reactionSeconds * tickRate) + 1), (float)random.NextDouble() * 100f);
                boat.Crew[seat].Input = seats[seat];
            }
        }

        /// <summary>A new order for the whole AI crew. Brace and Push here wear off on their own and the crew carries on.</summary>
        public void Give(CrewOrder order, Vector2 point = default)
        {
            Order = order;
            PushPoint = point;
            orderSeconds = 0f;
        }

        /// <summary>Call once per tick. The inputs it decides are used by the simulation's following ticks, after each seat's reaction delay.</summary>
        public void Update(float dt)
        {
            time += dt;
            orderSeconds += dt;
            if ((Order == CrewOrder.Brace && orderSeconds > BraceOrderSeconds) ||
                (Order == CrewOrder.PushHere && (orderSeconds > PushHereSeconds || Vector2.Distance(boat.Body.Position, PushPoint) < ArrivedMetres)))
                Give(CrewOrder.Go);

            var desired = DesiredVelocity();
            var velocity = boat.Body.Velocity;
            stuckFor = desired.HasValue && velocity.magnitude < StuckSpeed ? stuckFor + dt : 0f;

            var error = desired.HasValue ? desired.Value - velocity : Vector2.zero;
            var throttle = desired.HasValue ? Mathf.Clamp01(error.magnitude * SpeedGain) : 0f;
            var airAngle = Mathf.Atan2(-error.y, -error.x); // blow away from where the raft should go
            var requestedLocal = (airAngle - boat.Body.AngleRad) * Mathf.Rad2Deg;
            var minShare = Mathf.Lerp(0.05f, 0.35f, profile.airDiscipline);
            var turnRate = TurnRateWanted(desired, velocity, airAngle);

            // A log about to swing through, or a geyser about to spout: duck. Each crew notices in time only so often;
            // the steady autopilot always does.
            var hazard = BraceAhead != null && BraceAhead(boat.Body.Position, profile.reactionSeconds + 0.6f);
            if (hazard && !hazardSeen)
                hazardNoticed = profile.reactionSeconds <= 0f || random.NextDouble() < Mathf.Lerp(0.45f, 1f, profile.braceInstinct);
            hazardSeen = hazard;

            foreach (var seat in seats)
            {
                if (seat == null) continue;
                var crew = boat.Crew[seat.Index];
                if (turnRate.HasValue)
                {
                    // Blow along the rim so the raft spins: every seat can, at about 70% of its push.
                    var arm = boat.Body.WorldVector(crew.SeatOffset);
                    var spin = Mathf.Sign(turnRate.Value - boat.Body.AngularVelocityRad);
                    var air = -spin * new Vector2(-arm.y, arm.x);
                    airAngle = Mathf.Atan2(air.y, air.x);
                    requestedLocal = (airAngle - boat.Body.AngleRad) * Mathf.Rad2Deg;
                    throttle = Mathf.Clamp(Mathf.Abs(turnRate.Value - boat.Body.AngularVelocityRad) * 0.6f, 0f, 0.6f);
                }

                var share = CrewSectors.Clamp(requestedLocal, seat.Index, boat.Crew.Length, out var allowedLocal);
                var seatThrottle = throttle;
                if (!turnRate.HasValue && Mathf.Abs(boat.Body.AngularVelocityRad) > SpinDeadband)
                {
                    // Like a twin-engine boat steered by its throttles: a seat whose push would add to the raft's spin
                    // eases off. Otherwise one blower pushing alone, off-centre, slowly twists the raft away.
                    var arm = boat.Body.WorldVector(crew.SeatOffset);
                    var push = -new Vector2(Mathf.Cos(boat.Body.AngleRad + allowedLocal * Mathf.Deg2Rad), Mathf.Sin(boat.Body.AngleRad + allowedLocal * Mathf.Deg2Rad));
                    var twist = arm.x * push.y - arm.y * push.x;
                    if (twist * boat.Body.AngularVelocityRad > 0f)
                        seatThrottle *= Mathf.Clamp01(1f - (Mathf.Abs(boat.Body.AngularVelocityRad) - SpinDeadband) * 3f);
                }

                // Now and then a crewmate drifts off for a couple of seconds and forgets to blow.
                if (seat.Daydreaming > 0f) seat.Daydreaming -= dt;
                else if (profile.daydream > 0f && random.NextDouble() < profile.daydream * dt) seat.Daydreaming = 1f + (float)random.NextDouble() * 2f;

                var input = new CrewInput
                {
                    AimAngle = airAngle + Wobble(seat) * Mathf.Deg2Rad,
                    HasAim = desired.HasValue,
                    Brace = Order == CrewOrder.Brace || Panicking(seat) || (hazard && hazardNoticed),
                };
                var blow = desired.HasValue && seat.Daydreaming <= 0f && share >= minShare && seatThrottle > 0.05f;
                input.Throttle = blow && !input.Brace ? seatThrottle : 0f;

                seat.SinceBurst += dt;
                if (stuckFor > StuckSeconds && share > 0.5f && seat.SinceBurst > BurstCooldown && crew.Blower.CanBurst && crew.AirFraction > 0.4f)
                {
                    seat.SinceBurst = 0f;
                    input.Burst = random.NextDouble() < profile.burstWhenStuck;
                }

                seat.Push(input);
            }
        }

        /// <summary>
        /// Null when some AI seat can push the way the raft needs to go (or the raft is already moving well). Otherwise
        /// the spin, in radians per second, that brings the nearest AI seat's slice round to face the right way.
        /// </summary>
        float? TurnRateWanted(Vector2? desired, Vector2 velocity, float airAngle)
        {
            if (!desired.HasValue || desired.Value.sqrMagnitude < 1e-4f)
            {
                turning = false;
                return null;
            }

            // Aim the AI seats' slices, together, at the way the air must go: then they push side by side, with no
            // leftover twist. (Stopping as soon as one seat can just about push leaves it shoving alone, off-centre,
            // which spins the raft straight back.)
            var best = 0f;
            var nearest = float.MaxValue;
            var together = Vector2.zero;
            foreach (var seat in seats)
            {
                if (seat == null) continue;
                var centre = boat.Body.AngleRad * Mathf.Rad2Deg + CrewSectors.CentreDeg(seat.Index, boat.Crew.Length);
                var turn = Mathf.DeltaAngle(centre, airAngle * Mathf.Rad2Deg);
                if (Mathf.Abs(turn) < Mathf.Abs(nearest)) nearest = turn;
                together += new Vector2(Mathf.Cos(centre * Mathf.Deg2Rad), Mathf.Sin(centre * Mathf.Deg2Rad));
                best = Mathf.Max(best, CrewSectors.Clamp((airAngle - boat.Body.AngleRad) * Mathf.Rad2Deg, seat.Index, boat.Crew.Length, out _));
            }

            if (nearest == float.MaxValue)
            {
                turning = false;
                return null;
            }

            // Seats spread evenly round the raft have no "together" direction; then the nearest one will do.
            var clustered = together.magnitude > 0.3f;
            var misalignment = clustered ? Mathf.DeltaAngle(Mathf.Atan2(together.y, together.x) * Mathf.Rad2Deg, airAngle * Mathf.Rad2Deg) : nearest;
            var makingWay = Vector2.Dot(velocity, desired.Value.normalized) > 1f;
            if (!turning && best < TurnWhenShareBelow && !makingWay) turning = true;
            else if (turning && Mathf.Abs(misalignment) < SettledWithinDeg && Mathf.Abs(boat.Body.AngularVelocityRad) < 0.3f) turning = false;
            if (!turning) return null;

            return Mathf.Clamp(misalignment * Mathf.Deg2Rad, -MaxTurnRate, MaxTurnRate);
        }

        /// <summary>Where the order says the raft should be going, and how fast; null to leave it be.</summary>
        Vector2? DesiredVelocity()
        {
            Target = null;
            switch (Order)
            {
                case CrewOrder.Hold:
                case CrewOrder.Brace:
                    return null;

                case CrewOrder.PushHere:
                    Target = PushPoint;
                    return Toward(PushPoint);

                default:
                    if (River != null)
                    {
                        var at = River.Project(boat.Body.Position);
                        var ahead = River.At(at.Distance + LookaheadMetres);
                        // Keep to this crew's side of the line, wide enough apart that two rafts (3.2 m each) pass without
                        // trading paint, and out of each other's exhaust; the clamp keeps clear of the banks.
                        var across = Mathf.Clamp(ahead.LineOffset * ahead.HalfWidth + LaneShare * Course.LaneOffset(ahead, Lane, Lanes),
                            -0.72f * ahead.HalfWidth, 0.72f * ahead.HalfWidth);
                        if (SteerAcross != null) across = SteerAcross(at.Distance, ahead.Distance, across);
                        Target = ahead.Centre + ahead.Left * across;
                        var desired = Toward(Target.Value);

                        // Near the edge there's nothing to stop a fall: steer back in, harder the closer it gets.
                        var room = at.HalfWidth - Mathf.Abs(at.Lateral);
                        if (desired.HasValue && room < EdgeSense)
                            desired += -Mathf.Sign(at.Lateral) * new Vector2(-at.Tangent.y, at.Tangent.x) * ((EdgeSense - room) * 1.2f);
                        return desired;
                    }

                    return FollowThePlayers();
            }
        }

        Vector2? Toward(Vector2 point)
        {
            var to = point - boat.Body.Position;
            var water = Water?.Invoke(boat.Body.Position) ?? Vector2.zero;
            return to.sqrMagnitude > 1e-4f ? to.normalized * profile.cruiseSpeed + water : water;
        }

        /// <summary>In open water there's no line to follow, so help: push the way the players are pushing, or rest.</summary>
        Vector2? FollowThePlayers()
        {
            var push = Vector2.zero;
            foreach (var crew in boat.Crew)
                if (!Drives(crew.Seat) && crew.IsBlowing) push -= crew.AirDirection * crew.Spool;

            if (push.magnitude < 0.2f) return null;
            Target = boat.Body.Position + push.normalized * LookaheadMetres;
            return push.normalized * profile.cruiseSpeed;
        }

        /// <summary>The aim wanders smoothly rather than jittering: a hand drifting, not a twitch.</summary>
        float Wobble(AiSeat seat) => profile.aimWobbleDeg * (Mathf.PerlinNoise(seat.Noise, time * 0.6f) * 2f - 1f);

        /// <summary>When the raft leans hard, each crewmate decides once (by its instinct) whether to brace until it settles.</summary>
        bool Panicking(AiSeat seat)
        {
            var danger = boat.TiltDanger;
            if (!seat.Leaning && danger > profile.panicLean)
            {
                seat.Leaning = true;
                seat.Bracing = random.NextDouble() < profile.braceInstinct;
            }
            else if (seat.Leaning && danger < profile.panicLean * 0.6f)
            {
                seat.Leaning = seat.Bracing = false;
            }

            return seat.Bracing;
        }

        /// <summary>One AI animal's controls, played back after its reaction time.</summary>
        sealed class AiSeat : ICrewInputSource
        {
            readonly CrewInput[] delay;
            int next;

            public AiSeat(int index, int delayTicks, float noise)
            {
                Index = index;
                Noise = noise;
                delay = new CrewInput[delayTicks];
            }

            public int Index { get; }
            public float Noise { get; }
            public bool Leaning;
            public bool Bracing;
            public float Daydreaming;
            public float SinceBurst = float.MaxValue;

            /// <summary>Queues a decision; it comes out of <see cref="ReadInput"/> the reaction time later.</summary>
            public void Push(CrewInput input)
            {
                delay[next] = input;
                next = (next + 1) % delay.Length;
            }

            public CrewInput ReadInput()
            {
                // With a delay of N ticks the oldest queued decision is the one at 'next'; with N = 1 it's the newest.
                var input = delay[next];
                delay[next].Burst = false; // a burst plays once, even if this slot is read again before it's refilled
                return input;
            }
        }
    }
}
