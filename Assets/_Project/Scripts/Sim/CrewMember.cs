using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>One animal in one seat, holding one blower.</summary>
    public sealed class CrewMember : ICrewState
    {
        public CrewMember(int seat, Vector2 seatOffset, BlowerDef blower)
        {
            Seat = seat;
            SeatOffset = seatOffset;
            Equip(blower);
        }

        public int Seat { get; }
        public Vector2 SeatOffset { get; internal set; }
        public BlowerDef Blower { get; private set; }
        public AirTank Tank { get; } = new();

        /// <summary>Who drives this seat: a human, the network or the AI. Null means idle.</summary>
        public ICrewInputSource Input { get; set; }

        /// <summary>0..1 lagged throttle: the blower's motor winding up and down.</summary>
        public float Spool { get; internal set; }

        /// <summary>
        /// Nozzle direction relative to the raft, in degrees (0 = bow, +90 = port). Always inside the seat's slice
        /// (<see cref="CrewSectors"/>), and it turns with the raft.
        /// </summary>
        public float LocalAimDeg { get; internal set; }

        /// <summary>World-space direction the air leaves the nozzle, in radians. Derived from <see cref="LocalAimDeg"/> each tick.</summary>
        public float AimAngle { get; internal set; }

        /// <summary>Share of the last requested push the slice allowed: 1 inside it, falling to 0 when asking for the far side.</summary>
        public float AimShare { get; internal set; } = 1f;

        public bool Braced { get; internal set; }
        public float SecondsSinceBurst { get; internal set; } = float.MaxValue;
        public int BurstCount { get; internal set; }

        public Vector2 AirDirection => BlowerMath.Direction(AimAngle);
        public bool IsBlowing => Spool > 0.05f && Blower.thrustNewtons > 0f;
        public float AirFraction => Tank.Fraction;

        public void Equip(BlowerDef blower)
        {
            Blower = blower;
            Tank.Reset(blower);
            Spool = 0f;
        }
    }
}
