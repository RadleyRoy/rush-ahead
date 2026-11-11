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

        /// <summary>Gone over the edge of the course: dropping out of sight before coming back at a checkpoint.</summary>
        Fallen,
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

        /// <summary>
        /// Held where it is, as if tied up: the water doesn't carry it and it stops dead each tick (a bump still nudges it).
        /// For waiting on the start line, and for a raft that's out of lives, now that the river flows.
        /// </summary>
        public bool Moored { get; set; }

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

        /// <summary>Lives left in the current race: each capsize or fall costs one. Set by the race rules.</summary>
        public int Lives { get; internal set; } = 3;

        /// <summary>Out of lives: the race is over for this raft.</summary>
        public bool Out { get; internal set; }

        /// <summary>The tick it ran out of lives, or 0.</summary>
        public int OutTick { get; internal set; }

        public const float FallSeconds = 1.4f;

        /// <summary>The water's velocity a moment ahead along the raft's path (set by the world each tick), for following bends.</summary>
        public Vector2 WaterAhead { get; set; }

        /// <summary>How far ahead along its path, in seconds, the world samples the water for <see cref="WaterAhead"/>.</summary>
        public const float BendLookSeconds = 0.25f;

        const float BendCarryOver = 5f; // the current carries a raft round a bend at up to this much over its own speed
        const float MaxImpactKick = 1.6f; // a hard ram at full speed; more is the physics squeezing a pinned raft, not a hit

        /// <summary>The share of the crew bracing on the last tick (ducking a swinging log needs half).</summary>
        public float BracedFraction { get; private set; }

        /// <summary>Seconds left of an electric eel's jolt: until then the crew can't blow (bracing still works).</summary>
        public float StunnedSeconds { get; set; }

        /// <summary>
        /// A knock from outside: a swinging log or a geyser. Pushes the raft and heels it over by <paramref name="heel"/>
        /// times what that push alone would, so it can capsize; a braced crew takes much less of it.
        /// </summary>
        /// <param name="spin">How hard it also sets the raft spinning (it lands off-centre): 0 to 1.</param>
        public void Shove(Vector2 impulse, float heel, float spin = 0f)
        {
            if (State != BoatState.Afloat) return;
            var soften = 1f - 0.75f * BracedFraction;
            var offCentre = spin * 0.9f * Body.LocalVector(new Vector2(-impulse.y, impulse.x).normalized); // hit to one side of the middle
            Body.AddForceAtLocalPoint(impulse * soften, offCentre, impulse: true);
            Tilt.Kick(HeelPerUnit(impulse) * (heel * soften));
            Shoved?.Invoke(this);
        }

        /// <summary>
        /// A steady push or drag from the water (mud, a net, nibbling fish), one tick's worth as an impulse: bracing
        /// doesn't soften it and it doesn't heel the raft.
        /// </summary>
        public void Push(Vector2 impulse)
        {
            if (State != BoatState.Afloat) return;
            Body.AddForceAtLocalPoint(impulse, Vector2.zero, impulse: true);
        }

        /// <summary>A swinging log or geyser caught the raft.</summary>
        public event Action<Boat> Shoved;

        /// <summary>
        /// Over the edge: the controls go dead while the raft drops out of sight, then it recovers like after a capsize
        /// (the race rules put it back at the last checkpoint). Nothing happens unless it's afloat.
        /// </summary>
        public void Fall()
        {
            if (State != BoatState.Afloat) return;
            State = BoatState.Fallen;
            recoverySecondsLeft = FallSeconds;
            Tilt.Reset();
        }

        /// <summary>
        /// Knocked straight over (the boss croc's bite): a capsize like any other, costing a life, unless a shield
        /// bubble pops instead. Whether it went over.
        /// </summary>
        public bool KnockOver()
        {
            if (State != BoatState.Afloat) return false;
            if (Shielded)
            {
                Shielded = false;
                ShieldPops++;
                Tilt.Reset();
                CapsizeImmunitySeconds = 1f;
                return false;
            }

            State = BoatState.Capsized;
            CapsizeCount++;
            recoverySecondsLeft = config.recoverySeconds;
            CapsizedEvent?.Invoke(this);
            return true;
        }

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
        bool IBoatState.Fallen => State == BoatState.Fallen;
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
            StunnedSeconds = Mathf.Max(0f, StunnedSeconds - dt);
            foreach (var crew in Crew)
            {
                crew.SeatOffset = config.SeatOffset(crew.Seat, Crew.Length);
                // Always read, so presses made while the controls are locked are used up rather than firing afterwards.
                var input = crew.Input?.ReadInput() ?? CrewInput.Idle;
                if (!afloat || ControlsLocked) input = CrewInput.Idle;
                if (input.UseItem) ItemRequested = true;
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

            if (Moored) Body.Stop();
            else ApplyWaterDrag();
            BracedFraction = Crew.Length > 0 ? bracedCount / (float)Crew.Length : 0f;
            StepTilt(heelAccel, bracedCount, dt);
        }

        /// <param name="share">How much of the requested push the player's slice allows (see <see cref="CrewSectors.Clamp"/>).</param>
        /// <returns>The heeling acceleration this crew member's thrust causes.</returns>
        Vector2 StepBlower(CrewMember crew, CrewInput input, float share, float dt)
        {
            var spec = crew.Blower;
            var thrust = spec.thrustNewtons * config.thrustMultiplier;

            // Braced and stunned crew stop blowing; burst-only blowers (bellows) have no continuous airflow to draw.
            var stunned = StunnedSeconds > 0f;
            var requested = crew.Braced || stunned || thrust <= 0f ? 0f : Mathf.Clamp01(input.Throttle) * share;
            crew.Tank.Tick(dt);
            crew.Spool = BlowerMath.Spool(crew.Spool, requested, dt, spec.spoolUpMs / 1000f, spec.spoolDownMs / 1000f);
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
                heel = HeelPerUnit(force) / Mathf.Max(1f, config.thrustMultiplier); // more push, not more lean
            }

            if (input.Burst && !crew.Braced && !stunned && share > 0f && crew.Tank.TryBurst())
            {
                var burstImpulse = spec.burstImpulse * config.thrustMultiplier * share;
                var impulse = BlowerMath.ReactionForce(airDirection, burstImpulse, 1f, spec.special);
                Body.AddForceAtLocalPoint(impulse, crew.SeatOffset, impulse: true);
                Body.PushBodiesInCone(nozzle, airDirection, spec, burstImpulse * config.coneCoupling, true, coneSign);
                Tilt.Kick(HeelPerUnit(impulse) / Mathf.Max(1f, config.thrustMultiplier)); // an impulse changes tilt velocity directly
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

            // The river carries a raft: slower than the water the way it flows, it's drawn up toward the water's speed
            // (never beyond it, and never held back by it when it's faster).
            var water = WaterVelocity;
            var flow = water.magnitude;
            if (flow < 0.05f) return;
            var along = water / flow;
            var behind = Mathf.Clamp(flow - Vector2.Dot(Body.Velocity, along), 0f, flow);
            Body.AddForce(along * (behind * config.waterCarry * Body.Mass));

            // Round a bend the current carries a raft with it: its path turns as the water's does, at up to a little over
            // the water's own speed. Any speed beyond that is the crew's own to steer round the bend.
            var velocity = Body.Velocity;
            var travelled = velocity.magnitude * BendLookSeconds;
            if (travelled < 0.3f || WaterAhead.sqrMagnitude < 0.25f) return;
            var turn = Mathf.Clamp(Vector2.SignedAngle(water, WaterAhead) * Mathf.Deg2Rad / travelled, -0.08f, 0.08f); // radians per metre
            var carried = Mathf.Min(velocity.magnitude, flow + BendCarryOver);
            Body.AddForce(new Vector2(-velocity.y, velocity.x).normalized * (turn * carried * carried * Body.Mass * config.bendCarry));
        }

        void StepTilt(Vector2 heelAccel, int bracedCount, float dt)
        {
            var impact = Body.ConsumeImpactImpulse();
            if (impact != Vector2.zero)
            {
                var local = Body.LocalVector(impact);
                Tilt.Kick(Vector2.ClampMagnitude(new Vector2(-local.y, -local.x) * config.impactKickScale, MaxImpactKick));
            }

            if (State is BoatState.Capsized or BoatState.Fallen)
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

            if (Shielded)
            {
                Shielded = false; // the bubble pops and rights the raft
                ShieldPops++;
                Tilt.Reset();
                CapsizeImmunitySeconds = 1f;
                return;
            }

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
            StunnedSeconds = 0f;
            State = BoatState.Afloat;
            foreach (var crew in Crew)
            {
                crew.Equip(crew.Blower);
                crew.LocalAimDeg = CrewSectors.CentreDeg(crew.Seat, Crew.Length);
                crew.AimShare = 1f;
                crew.AimAngle = angleRad + crew.LocalAimDeg * Mathf.Deg2Rad;
            }
        }

        /// <summary>Knocked out of an Elimination: it sinks out of the race and out of everyone's way.</summary>
        public bool Sunk
        {
            get => !Body.Solid;
            set => Body.Solid = !value;
        }

        /// <summary>The power-up the raft carries, if any.</summary>
        public PowerUp Item { get; set; }

        /// <summary>A crewmate pressed to use the item (the AI sets it directly); the power-ups use it up.</summary>
        public bool ItemRequested { get; set; }

        /// <summary>Seconds of turbo left.</summary>
        public float TurboSeconds { get; set; }

        /// <summary>A shield bubble is up: it saves the raft from its next fall or capsize.</summary>
        public bool Shielded { get; set; }

        /// <summary>Running counts, for the views to play a pop, a horn, a pickup: they survive dropped snapshots.</summary>
        public int ShieldPops { get; set; }
        public int HornCount { get; set; }
        public int Pickups { get; set; }
        public int ItemsUsed { get; set; }

        bool IBoatState.Turbo => TurboSeconds > 0f;

        /// <summary>Wipes the last run's progress: no checkpoints, no finish, no capsizes counted, every life back.</summary>
        public void ClearRun(int lives = 3)
        {
            Sunk = false;
            Item = PowerUp.None;
            ItemRequested = false;
            TurboSeconds = 0f;
            Shielded = false;
            GatesPassed = 0;
            FinishTick = 0;
            CapsizeCount = 0;
            CapsizeImmunitySeconds = 0f;
            Lives = lives;
            Out = false;
            OutTick = 0;
        }
    }
}
