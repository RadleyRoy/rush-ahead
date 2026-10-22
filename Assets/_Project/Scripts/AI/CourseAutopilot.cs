using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.AI
{
    /// <summary>
    /// A competent, unhurried crew that drives a raft down a course's racing line. It decides one push for the whole
    /// raft each tick (toward a point a few metres down the line, at cruising speed), then every animal whose slice
    /// can help blows the opposite way; the others save their air. Each one blows until its tank runs dry, then rests
    /// until it's mostly full, so the crew naturally takes turns. Used to prove courses can be finished, and the
    /// starting point for Phase 6's AI crewmates, which add personality and mistakes on top.
    /// </summary>
    public sealed class CourseAutopilot
    {
        const float LookaheadMetres = 7f;
        const float SpeedGain = 0.8f; // throttle per m/s of velocity error
        const float MinUsefulShare = 0.35f; // below this, blowing wastes more air than it helps
        const float RestUntilAir = 0.6f;

        readonly Boat boat;
        readonly River river;
        readonly Seat[] seats;

        public CourseAutopilot(Boat boat, River river)
        {
            this.boat = boat;
            this.river = river;
            seats = new Seat[boat.Crew.Length];
            for (var i = 0; i < seats.Length; i++) boat.Crew[i].Input = seats[i] = new Seat();
        }

        /// <summary>The speed it aims for, in metres per second. Faster means more time blowing and less resting.</summary>
        public float CruiseSpeed { get; set; } = 3.2f;

        /// <summary>Where the crew is steering toward this tick, for debugging and gizmos.</summary>
        public Vector2 Target { get; private set; }

        /// <summary>Call once per tick before the simulation steps.</summary>
        public void Update()
        {
            var position = boat.Body.Position;
            var at = river.Project(position);
            Target = river.At(at.Distance + LookaheadMetres).RacingPoint;

            var toTarget = Target - position;
            var desired = toTarget.sqrMagnitude > 1e-4f ? toTarget.normalized * CruiseSpeed : Vector2.zero;
            var error = desired - boat.Body.Velocity;
            var throttle = Mathf.Clamp01(error.magnitude * SpeedGain);
            var airAngle = Mathf.Atan2(-error.y, -error.x); // blow away from where the raft should go

            var requestedLocal = (airAngle - boat.Body.AngleRad) * Mathf.Rad2Deg;
            for (var i = 0; i < seats.Length; i++)
            {
                var crew = boat.Crew[i];
                var share = CrewSectors.Clamp(requestedLocal, crew.Seat, boat.Crew.Length, out _);
                var seat = seats[i];

                if (crew.Tank.IsEmpty) seat.Resting = true;
                else if (seat.Resting && crew.AirFraction >= RestUntilAir) seat.Resting = false;

                var blow = !seat.Resting && share >= MinUsefulShare && throttle > 0.05f;
                seat.Input = new CrewInput { AimAngle = airAngle, HasAim = true, Throttle = blow ? throttle : 0f };
            }
        }

        sealed class Seat : ICrewInputSource
        {
            public CrewInput Input;
            public bool Resting;
            public CrewInput ReadInput() => Input;
        }
    }
}
