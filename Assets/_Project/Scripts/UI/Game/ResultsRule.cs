using System.Collections.Generic;

namespace RushAhead.UI
{
    /// <summary>
    /// When a race is over enough to show the results: a moment after the last raft crosses the line, so its crew sees
    /// themselves finish; and in a Team Race, not forever after the winner, so a raft that never finishes can't hold
    /// the room.
    /// </summary>
    public static class ResultsRule
    {
        public const float AfterLastFinishSeconds = 2.5f;
        public const float TeamRaceGraceSeconds = 30f;

        /// <param name="finishTicks">Each raft's finishing tick, or 0 while it's still racing.</param>
        public static bool Due(IReadOnlyList<int> finishTicks, int nowTick, int tickRate)
        {
            var first = int.MaxValue;
            var last = 0;
            var everyone = finishTicks.Count > 0;
            foreach (var tick in finishTicks)
            {
                if (tick <= 0)
                {
                    everyone = false;
                    continue;
                }

                if (tick < first) first = tick;
                if (tick > last) last = tick;
            }

            if (first == int.MaxValue) return false;
            if (everyone && nowTick - last >= AfterLastFinishSeconds * tickRate) return true;
            return finishTicks.Count > 1 && nowTick - first >= TeamRaceGraceSeconds * tickRate;
        }
    }
}
