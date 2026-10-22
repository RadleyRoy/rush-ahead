using RushAhead.Sim;

namespace RushAhead.Net
{
    /// <summary>
    /// A seat driven by a remote player. Holds their newest input (older or reordered packets are ignored) and turns
    /// the running burst count back into one burst per change, so lost packets can't lose or repeat a burst.
    /// </summary>
    public sealed class RemoteSeatInput : ICrewInputSource
    {
        InputMessage latest;
        bool received;
        byte appliedBursts;

        public void Receive(in InputMessage input)
        {
            if (received && !IsNewer(input.Sequence, latest.Sequence)) return;

            // The first packet sets the baseline rather than assuming the count starts at zero. Seats get re-dealt while
            // packets are in flight, so a fresh seat can first hear a count from before the re-deal; treating that as a
            // change would fire bursts nobody pressed.
            if (!received) appliedBursts = input.BurstCount;
            latest = input;
            received = true;
        }

        public CrewInput ReadInput()
        {
            if (!received) return CrewInput.Idle;

            var burst = latest.BurstCount != appliedBursts;
            appliedBursts = latest.BurstCount;
            return new CrewInput
            {
                AimAngle = latest.AimAngle,
                HasAim = latest.HasAim,
                Throttle = latest.Throttle,
                Brace = latest.Brace,
                Burst = burst,
            };
        }

        /// <summary>Sequence comparison that survives the 16-bit counter wrapping round.</summary>
        static bool IsNewer(ushort a, ushort b) => (short)(a - b) > 0;
    }
}
