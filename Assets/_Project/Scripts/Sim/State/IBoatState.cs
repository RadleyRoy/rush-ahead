using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Everything the views need to draw a raft, read-only. On the host it is the live <see cref="Boat"/>; on a client
    /// it is a raft rebuilt from network snapshots. Views depend on this, never on how the state was produced.
    /// </summary>
    public interface IBoatState
    {
        int Index { get; }

        /// <summary>Position for drawing: smoothed between physics ticks (host) or interpolated between snapshots (client).</summary>
        Vector2 RenderPosition { get; }

        float RenderAngleRad { get; }
        Vector2 Velocity { get; }

        /// <summary>Lean in radians: x roll toward port, y pitch toward the bow.</summary>
        Vector2 Tilt { get; }

        bool Capsized { get; }
        int CapsizeCount { get; }

        /// <summary>0 when level, 1 at the capsize threshold.</summary>
        float TiltDanger { get; }

        int CrewCount { get; }
        ICrewState CrewAt(int seat);
    }

    public interface ICrewState
    {
        int Seat { get; }
        BlowerDef Blower { get; }

        /// <summary>World-space direction the air leaves the nozzle, in radians.</summary>
        float AimAngle { get; }

        float Spool { get; }
        bool IsBlowing { get; }
        float AirFraction { get; }
        bool Braced { get; }

        /// <summary>Total bursts so far. Views spot a burst by this changing, which survives dropped snapshots.</summary>
        int BurstCount { get; }
    }
}
