using System;
using System.Collections.Generic;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    public enum BoatState
    {
        Afloat,
        Capsized,
    }

    /// <summary>
    /// One shared boat. Each crew member's blower pushes at their seat, not at the centre of mass, so
    /// off-centre thrust turns the boat: four players pushing at cross purposes is the whole game.
    /// </summary>
    public sealed class Boat : IBoatState
    {
        readonly SimConfig config;
        float recoverySecondsLeft;

        /// <param name="loadout">One blower per player: 1 to <see cref="CrewSectors.MaxCrew"/>. The crew size sets each player's air slice.</param>
        public Boat(int index, BoatPhysics body, SimConfig config, IReadOnlyList<BlowerDef> loadout)
        {
            if (loadout.Count < 1 || loadout.Count > CrewSectors.MaxCrew)
                throw new ArgumentException($"A boat needs 1 to {CrewSectors.MaxCrew} blowers, got {loadout.Count}.", nameof(loadout));

            Index = index;
            Body = body;
            this.config = config;
            Crew = new CrewMember[loadout.Count];
            for (var seat = 0; seat < Crew.Length; seat++)
            {
                // Everyone starts with their nozzle pointing straight out of the middle of their slice.
                var centre = CrewSectors.CentreDeg(seat, Crew.Length);
                Crew[seat] = new CrewMember(seat, config.SeatOffset(seat, Crew.Length), loadout[seat])
                {
                    LocalAimDeg = centre,
                    AimAngle = body.AngleRad + centre * Mathf.Deg2Rad,
                };
            }
        }

        public event Action<Boat> CapsizedEvent;
        public event Action<Boat> Recovered;

        public int Index { get; }
        public BoatPhysics Body { get; }
        public CrewMember[] Crew { get; }
        public TiltModel Tilt { get; } = new();
        public BoatState State { get; private set; }
        public int CapsizeCount { get; internal set; }

        /// <summary>While true (a race countdown, after the finish) the crew's controls are read but ignored.</summary>
        public bool ControlsLocked { get; set; }

        /// <summary>After a respawn the raft can lean but not capsize again for this long, so it gets a fair restart.</summary>
        public float CapsizeImmunitySeconds { get; set; }

        /// <summary>How fast the water under the raft moves (a current). Drag acts on the raft's speed through the water.</summary>
        public Vector2 WaterVelocity { get; set; }

        /// <summary>Counts jumps to a new place (reset, respawn), so views know to cut rather than slide there.</summary>
        public int TeleportCount { get; private set; }

        /// <summary>Checkpoints passed in the current race; set by the race rules.</summary>
        public int GatesPassed { get; internal set; }

        /// <summary>The tick the raft crossed the finish line, or 0 while it hasn't.</summary>
        public int FinishTick { get; internal set; }

        /// <summary>Speed along the hull's heading (negative when going backwards).</summary>
        public float ForwardSpeed => Body.LocalVector(Body.Velocity).x;

        /// <summary>0 when level, 1 at the capsize threshold.</summary>
        public float TiltDanger => Mathf.Clamp01(Tilt.Tilt.magnitude / config.capsizeAngle);

        // IBoatState: the transform is what Rigidbody2D interpolation smooths between physics ticks.
        Vector2 IBoatState.RenderPosition => Body.transform.position;
        float IBoatState.RenderAngleRad => Body.transform.eulerAngles.z * Mathf.Deg2Rad;
        Vector2 IBoatState.Velocity => Body.Velocity;
        Vector2 IBoatState.Tilt => Tilt.Tilt;
        bool IBoatState.Capsized => State == BoatState.Capsized;
        int IBoatState.CrewCount => Crew.Length;
        ICrewState IBoatState.CrewAt(int seat) => Crew[seat];

        /// <summary>Applies one tick of forces. Must run before the physics engine steps.</summary>
        public void Step(float dt)
        {
            Body.ApplyConfig(config);
            var afloat = State == BoatState.Afloat;
            var heelAccel = Vector2.zero;
            var bracedCount = 0;

            CapsizeImmunitySeconds = Mathf.Max(0f, CapsizeImmunitySeconds - dt);
            foreach (var crew in Crew)
            {
                crew.SeatOffset = config.SeatOffset(crew.Seat, Crew.Length);
                // Always read, so presses made while the controls are locked are used up rather than firing afterwards.
                var input = crew.Input?.ReadInput() ?? CrewInput.Idle;
                if (!afloat || ControlsLocked) input = CrewInput.Idle;
                crew.Braced = afloat && input.Brace;
                if (crew.Braced) bracedCount++;

                // Each player may only blow inside their own slice of the raft. Asking for a direction outside it turns
                // the nozzle to the nearest edge and keeps only the part of the push that still helps. The nozzle
                // sweeps there at a limited rate rather than snapping, so thrust changes direction smoothly.
                var share = 1f;
                if (input.HasAim)
                {
                    var requestedLocal = (input.AimAngle - Body.AngleRad) * Mathf.Rad2Deg;
                    share = CrewSectors.Clamp(requestedLocal, crew.Seat, Crew.Length, out var allowedLocal);
                    crew.LocalAimDeg = CrewSectors.TurnTowards(crew.LocalAimDeg, allowedLocal, config.nozzleTurnDegPerSec * dt, crew.Seat, Crew.Length);
                    crew.AimShare = share;
                }

                crew.AimAngle = Body.AngleRad + crew.LocalAimDeg * Mathf.Deg2Rad; // the slice turns with the raft
                heelAccel += StepBlower(crew, input, share, dt);
            }

            ApplyWaterDrag();
            StepTilt(heelAccel, bracedCount, dt);
        }

        /// <param name="share">How much of the requested push the player's slice allows (see <see cref="CrewSectors.Clamp"/>).</param>
        /// <returns>The heeling acceleration this crew member's thrust causes.</returns>
        Vector2 StepBlower(CrewMember crew, CrewInput input, float share, float dt)
        {
            var spec = crew.Blower;
            var thrust = spec.thrustNewtons * config.thrustMultiplier;

            // Braced crew stop blowing; burst-only blowers (bellows) have no continuous airflow to draw.
            var requested = crew.Braced || thrust <= 0f ? 0f : Mathf.Clamp01(input.Throttle) * share;
            var available = crew.Tank.Tick(requested, dt);
            crew.Spool = BlowerMath.Spool(crew.Spool, available, dt, spec.spoolUpMs / 1000f, spec.spoolDownMs / 1000f);
            crew.SecondsSinceBurst += dt;

            var airDirection = crew.AirDirection;
            var nozzle = Body.WorldPoint(crew.SeatOffset);
            var coneSign = BlowerMath.ConeSign(spec.special);
            var heel = Vector2.zero;

            var force = BlowerMath.ReactionForce(airDirection, thrust, crew.Spool, spec.special);
            if (crew.IsBlowing)
            {
                Body.AddForceAtLocalPoint(force, crew.SeatOffset);
                Body.PushBodiesInCone(nozzle, airDirection, spec, thrust * crew.Spool * config.coneCoupling, false, coneSign);
                heel = HeelPerUnit(force);
            }

            if (input.Burst && !crew.Braced && share > 0f && crew.Tank.TryBurst())
            {
                var burstImpulse = spec.burstImpulse * config.thrustMultiplier * share;
                var impulse = BlowerMath.ReactionForce(airDirection, burstImpulse, 1f, spec.special);
                Body.AddForceAtLocalPoint(impulse, crew.SeatOffset, impulse: true);
                Body.PushBodiesInCone(nozzle, airDirection, spec, burstImpulse * config.coneCoupling, true, coneSign);
                Tilt.Kick(HeelPerUnit(impulse)); // an impulse changes tilt velocity directly
                crew.SecondsSinceBurst = 0f;
                crew.BurstCount++;
            }

            return heel;
        }

        /// <summary>
        /// Horizontal thrust applied above the waterline heels the boat: sideways thrust rolls it, fore-aft thrust pitches it.
        /// Returns (roll, pitch) per unit of force, so a force gives an acceleration and an impulse gives a velocity change.
        /// </summary>
        Vector2 HeelPerUnit(Vector2 worldForce)
        {
            var local = Body.LocalVector(worldForce);
            return new Vector2(local.y, local.x) * (config.heelArm / config.tiltInertia);
        }

        void ApplyWaterDrag()
        {
            var localVelocity = Body.LocalVector(Body.Velocity - WaterVelocity); // a current carries the raft along with it
            Body.AddForce(Body.WorldVector(WaterDrag.LocalForce(localVelocity, config.forwardDrag, config.lateralDrag)));
            Body.AddTorque(WaterDrag.Torque(Body.AngularVelocityRad, config.angularDrag));
        }

        void StepTilt(Vector2 heelAccel, int bracedCount, float dt)
        {
            var impact = Body.ConsumeImpactImpulse();
            if (impact != Vector2.zero)
            {
                var local = Body.LocalVector(impact);
                Tilt.Kick(new Vector2(-local.y, -local.x) * config.impactKickScale);
            }

            if (State == BoatState.Capsized)
            {
                recoverySecondsLeft -= dt;
                if (recoverySecondsLeft <= 0f) Recover();
                return;
            }

            // Turning at speed heels the boat toward the outside of the turn.
            var turnHeel = new Vector2(-config.turnHeelCoupling * ForwardSpeed * Body.AngularVelocityRad, 0f);

            // Pitch is stiffer than roll. Scaling its damping by sqrt(stiffness ratio) keeps the same
            // damping ratio on both axes, so the stiffer pitch spring doesn't turn into a bouncing bow.
            var pitchMultiplier = config.pitchStiffnessMultiplier;
            var stiffness = new Vector2(config.tiltStiffness, config.tiltStiffness * pitchMultiplier);
            var rollDamping = config.tiltDamping * Mathf.Lerp(1f, config.braceDampingMultiplier, bracedCount / (float)Crew.Length);
            var damping = new Vector2(rollDamping, rollDamping * Mathf.Sqrt(pitchMultiplier));

            var capsizeAngle = CapsizeImmunitySeconds > 0f ? float.PositiveInfinity : config.capsizeAngle;
            if (!Tilt.Step(heelAccel + turnHeel, stiffness, damping, capsizeAngle, config.capsizeHoldSeconds, dt))
                return;

            State = BoatState.Capsized;
            CapsizeCount++;
            recoverySecondsLeft = config.recoverySeconds;
            CapsizedEvent?.Invoke(this);
        }

        void Recover()
        {
            Body.Stop();
            Tilt.Reset();
            foreach (var crew in Crew) crew.Spool = 0f;
            State = BoatState.Afloat;
            Recovered?.Invoke(this);
        }

        /// <summary>Back to a standing start: upright, still, full tanks.</summary>
        public void ResetTo(Vector2 position, float angleRad)
        {
            Body.Teleport(position, angleRad);
            TeleportCount++;
            Tilt.Reset();
            State = BoatState.Afloat;
            foreach (var crew in Crew)
            {
                crew.Equip(crew.Blower);
                crew.LocalAimDeg = CrewSectors.CentreDeg(crew.Seat, Crew.Length);
                crew.AimShare = 1f;
                crew.AimAngle = angleRad + crew.LocalAimDeg * Mathf.Deg2Rad;
            }
        }
    }
}
