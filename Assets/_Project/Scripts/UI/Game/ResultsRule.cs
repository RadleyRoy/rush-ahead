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
        public static bool Due(IReadOnlyList<int> finishTicks, int nowTick, int tickRate) => Due(finishTicks, null, nowTick, tickRate);

        /// <param name="outTicks">Each raft's tick it ran out of lives, or 0. Out counts as done; only finishers start the Team Race wait.</param>
        public static bool Due(IReadOnlyList<int> finishTicks, IReadOnlyList<int> outTicks, int nowTick, int tickRate)
        {
            var lastDone = 0;
            var allDone = finishTicks.Count > 0;
            for (var i = 0; i < finishTicks.Count; i++)
            {
                var done = finishTicks[i] > 0 ? finishTicks[i] : outTicks != null && i < outTicks.Count ? outTicks[i] : 0;
                if (done <= 0) allDone = false;
                else if (done > lastDone) lastDone = done;
            }

            if (allDone && nowTick - lastDone >= AfterLastFinishSeconds * tickRate) return true;
            return TeamRaceWaitOver(finishTicks, nowTick, tickRate);
        }

        /// <summary>A Team Race doesn't wait for ever: half a minute after the first raft home, the results come.</summary>
        static bool TeamRaceWaitOver(IReadOnlyList<int> finishTicks, int nowTick, int tickRate)
        {
            var first = int.MaxValue;
            foreach (var tick in finishTicks)
                if (tick > 0 && tick < first) first = tick;
            return first != int.MaxValue && finishTicks.Count > 1 && nowTick - first >= TeamRaceGraceSeconds * tickRate;
        }
    }
}
