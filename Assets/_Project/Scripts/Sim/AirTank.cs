using RushAhead.Content;
using UnityEngine;

namespace RushAhead.Sim
{
    /// <summary>
    /// Air for bursts. Steady blowing is free, so a player can hold the trigger for as long as they like; each burst
    /// spends a puff from the tank, which refills on its own a moment after the last one, blowing or not.
    /// </summary>
    public sealed class AirTank
    {
        BlowerDef spec;
        float secondsSinceBurst;

        public float Air { get; private set; }

        public float Fraction => spec == null || spec.airCapacity <= 0f ? 1f : Air / spec.airCapacity;

        public void Reset(BlowerDef blower)
        {
            spec = blower;
            Air = blower.airCapacity;
            secondsSinceBurst = float.MaxValue;
        }

        /// <summary>Advances one tick: refills once the last burst is <see cref="BlowerDef.rechargeDelayMs"/> behind.</summary>
        public void Tick(float dt)
        {
            secondsSinceBurst += dt;
            if (secondsSinceBurst >= spec.rechargeDelayMs / 1000f)
                Air = Mathf.Min(spec.airCapacity, Air + spec.rechargePerSec * dt);
        }

        public bool TryBurst()
        {
            if (!spec.CanBurst || Air < spec.burstCost) return false;
            Air -= spec.burstCost;
            secondsSinceBurst = 0f;
            return true;
        }
    }
}
