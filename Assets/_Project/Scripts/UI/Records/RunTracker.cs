using System.Collections.Generic;

namespace RushAhead.UI
{
    /// <summary>
    /// Times the raft on screen through each checkpoint of a run (the last one is the finish) and compares each split
    /// with the same checkpoint in the best run, for the "-0.8" or "+1.2" shown as the raft passes.
    /// On a joined device the splits are as accurate as the snapshots (a thirtieth of a second); the finish is exact.
    /// </summary>
    public sealed class RunTracker
    {
        readonly List<float> splits = new();
        readonly List<float> deltas = new();
        int start = int.MinValue;

        /// <summary>The run's time at each checkpoint passed so far.</summary>
        public IReadOnlyList<float> Splits => splits;

        /// <summary>Each split against the best run's at the same checkpoint; NaN where there was nothing to compare.</summary>
        public IReadOnlyList<float> Deltas => deltas;

        /// <summary>The checkpoint passed most recently, counting from 1; 0 before the first.</summary>
        public int LastGate => splits.Count;

        /// <summary>The last split against the best run's at the same checkpoint (negative is faster); null with nothing to compare.</summary>
        public float? LastDelta { get; private set; }

        /// <summary>Call every frame with the run on screen. A new start tick means a new run.</summary>
        /// <param name="seconds">The run's clock now (or its finishing time once finished).</param>
        /// <param name="best">The best run's splits, or null.</param>
        public void Update(int startTick, int gatesPassed, float seconds, IReadOnlyList<float> best)
        {
            if (startTick != start)
            {
                start = startTick;
                splits.Clear();
                deltas.Clear();
                LastDelta = null;
            }

            while (splits.Count < gatesPassed)
            {
                var gate = splits.Count;
                splits.Add(seconds);
                LastDelta = best != null && gate < best.Count ? seconds - best[gate] : null;
                deltas.Add(LastDelta ?? float.NaN);
            }
        }
    }
}
