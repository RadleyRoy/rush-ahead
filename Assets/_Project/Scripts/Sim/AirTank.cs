using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Holding the trigger drains the tank and blocks recharging, so nobody can blow forever:
    /// the crew has to take turns, which is what forces coordination.
    /// </summary>
    public sealed class AirTank
    {
        BlowerDef spec;
        float secondsSinceFiring;

        public float Air { get; private set; }

        public float Fraction => spec == null || spec.HasInfiniteAir || spec.airCapacity <= 0f ? 1f : Air / spec.airCapacity;

        public bool IsEmpty => spec != null && !spec.HasInfiniteAir && Air <= 0f;

        public void Reset(BlowerDef blower)
        {
            spec = blower;
            Air = blower.airCapacity;
            secondsSinceFiring = float.MaxValue;
        }

        /// <summary>Advances one tick and returns the throttle actually available (0 when empty).</summary>
        public float Tick(float requestedThrottle, float dt)
        {
            // Holding the trigger counts as firing even on an empty tank, so recharging requires letting go.
            secondsSinceFiring = requestedThrottle > 0f ? 0f : secondsSinceFiring + dt;
            if (spec.HasInfiniteAir) return requestedThrottle;

            var available = Air > 0f ? requestedThrottle : 0f;
            if (available > 0f)
                Air = Mathf.Max(0f, Air - spec.drainPerSec * available * dt);
            else if (secondsSinceFiring >= spec.rechargeDelayMs / 1000f)
                Air = Mathf.Min(spec.airCapacity, Air + spec.rechargePerSec * dt);

            return available;
        }

        public bool TryBurst()
        {
            if (!spec.CanBurst) return false;
            if (!spec.HasInfiniteAir)
            {
                if (Air < spec.burstCost) return false;
                Air -= spec.burstCost;
            }

            secondsSinceFiring = 0f;
            return true;
        }
    }
}
