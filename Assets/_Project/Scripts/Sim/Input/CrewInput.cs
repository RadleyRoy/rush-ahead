namespace RushAhead.Sim
{
    /// <summary>
    /// One crew member's intent for one tick. Identical whether it comes from a touch stick, a gamepad,
    /// the network or (later) the AI, so the simulation never knows or cares which.
    /// </summary>
    public struct CrewInput
    {
        /// <summary>World-space direction the air leaves the nozzle, in radians. Only read when <see cref="HasAim"/>.</summary>
        public float AimAngle;

        public bool HasAim;

        /// <summary>0..1 trigger pressure.</summary>
        public float Throttle;

        /// <summary>Edge-triggered: true for exactly one tick per press.</summary>
        public bool Burst;

        /// <summary>Stop blowing and steady the boat against capsizing.</summary>
        public bool Brace;

        public static CrewInput Idle => default;
    }

    public interface ICrewInputSource
    {
        /// <summary>Called once per simulation tick. Implementations must clear edge-triggered state (burst) when read.</summary>
        CrewInput ReadInput();
    }
}
