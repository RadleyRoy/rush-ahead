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
    public sealed class Boat
    {
        readonly SimConfig config;
        float recoverySecondsLeft;

        public Boat(int index, BoatPhysics body, SimConfig config, IReadOnlyList<BlowerDef> loadout)
        {
            if (loadout.Count != SimConfig.SeatCount)
                throw new ArgumentException($"A boat needs {SimConfig.SeatCount} blowers, got {loadout.Count}.", nameof(loadout));

            Index = index;
            Body = body;
            this.config = config;
            Crew = new CrewMember[SimConfig.SeatCount];
            for (var seat = 0; seat < Crew.Length; seat++)
                Crew[seat] = new CrewMember(seat, config.seatOffsets[seat], loadout[seat]) { AimAngle = body.AngleRad + Mathf.PI };
        }

        public event Action<Boat> CapsizedEvent;
        public event Action<Boat> Recovered;

        public int Index { get; }
        public BoatPhysics Body { get; }
        public CrewMember[] Crew { get; }
        public TiltModel Tilt { get; } = new();
        public BoatState State { get; private set; }
        public int CapsizeCount { get; private set; }

        /// <summary>Speed along the hull's heading (negative when going backwards).</summary>
        public float ForwardSpeed => Body.LocalVector(Body.Velocity).x;

        /// <summary>0 when level, 1 at the capsize threshold.</summary>
        public float TiltDanger => Mathf.Clamp01(Tilt.Tilt.magnitude / config.capsizeAngle);

        /// <summary>Applies one tick of forces. Must run before the physics engine steps.</summary>
        public void Step(float dt)
        {
            Body.ApplyConfig(config);
            var afloat = State == BoatState.Afloat;
            var heelAccel = Vector2.zero;
            var bracedCount = 0;

            foreach (var crew in Crew)
            {
                crew.SeatOffset = config.seatOffsets[crew.Seat];
                var input = afloat && crew.Input != null ? crew.Input.ReadInput() : CrewInput.Idle;
                crew.Braced = afloat && input.Brace;
                if (crew.Braced) bracedCount++;
                if (input.HasAim) crew.AimAngle = input.AimAngle;

                heelAccel += StepBlower(crew, input, dt);
            }

            ApplyWaterDrag();
            StepTilt(heelAccel, bracedCount, dt);
        }

        /// <returns>The heeling acceleration this crew member's thrust causes.</returns>
        Vector2 StepBlower(CrewMember crew, CrewInput input, float dt)
        {
            var spec = crew.Blower;
            var thrust = spec.thrustNewtons * config.thrustMultiplier;

            // Braced crew stop blowing; burst-only blowers (bellows) have no continuous airflow to draw.
            var requested = crew.Braced || thrust <= 0f ? 0f : Mathf.Clamp01(input.Throttle);
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

            if (input.Burst && !crew.Braced && crew.Tank.TryBurst())
            {
                var burstImpulse = spec.burstImpulse * config.thrustMultiplier;
                var impulse = BlowerMath.ReactionForce(airDirection, burstImpulse, 1f, spec.special);
                Body.AddForceAtLocalPoint(impulse, crew.SeatOffset, impulse: true);
                Body.PushBodiesInCone(nozzle, airDirection, spec, burstImpulse * config.coneCoupling, true, coneSign);
                Tilt.Kick(HeelPerUnit(impulse)); // an impulse changes tilt velocity directly
                crew.SecondsSinceBurst = 0f;
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
            var localVelocity = Body.LocalVector(Body.Velocity);
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
            var damping = config.tiltDamping * Mathf.Lerp(1f, config.braceDampingMultiplier, bracedCount / (float)Crew.Length);

            if (!Tilt.Step(heelAccel + turnHeel, config.tiltStiffness, damping, config.capsizeAngle, config.capsizeHoldSeconds, dt))
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
            Tilt.Reset();
            State = BoatState.Afloat;
            foreach (var crew in Crew) crew.Equip(crew.Blower);
        }
    }
}
