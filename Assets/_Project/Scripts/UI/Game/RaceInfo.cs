using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Net;

namespace RushAhead.UI
{
    /// <summary>A run as the HUD shows it: the same on the host and every joined device.</summary>
    public struct RaceInfo
    {
        public CourseDef Course;

        /// <summary>Choosing the race, racing, or looking at the results.</summary>
        public RoomPhase Phase;

        /// <summary>3, 2, 1 before the start; 0 once the clock is running.</summary>
        public int Countdown;

        /// <summary>Seconds since the start signal (negative during the countdown), for showing "GO!" briefly.</summary>
        public float SinceStart;

        /// <summary>The clock: time so far, or the finishing time.</summary>
        public float Seconds;

        public int GatesPassed;

        /// <summary>Checkpoints on the course, the finish included.</summary>
        public int Gates;
        public bool Finished;
        public int Capsizes;

        /// <summary>This device's best time on the course before this run, or 0 if it has none.</summary>
        public float Best;

        public bool NewBest;

        /// <summary>The best time before this run finished, to show the improvement (or the gap); 0 if there was none.</summary>
        public float PreviousBest;

        /// <summary>The best run's time at each checkpoint (the last is the finish); empty without one.</summary>
        public IReadOnlyList<float> BestSplits;

        /// <summary>This run's time at each checkpoint passed so far.</summary>
        public IReadOnlyList<float> Splits;

        /// <summary>Each split against the best run's at the same checkpoint; NaN where there was nothing to compare.</summary>
        public IReadOnlyList<float> SplitDeltas;

        /// <summary>The checkpoint passed most recently (from 1), its split against the best run's, and how long ago, in seconds.</summary>
        public int SplitGate;
        public float? SplitDelta;
        public float SplitAge;

        /// <summary>How far down the course this player's raft is, and in a Team Race the other raft, 0 to 1.</summary>
        public float MyProgress;
        public float RivalProgress;

        /// <summary>One raft (Co-op) or two (Team Race).</summary>
        public int Rafts;

        public int MyRaft;

        /// <summary>Team Race: the raft that finished first, or -1 while nobody has.</summary>
        public int Winner;

        public int RivalGatesPassed;
        public bool RivalFinished;
        public float RivalSeconds;

        public bool Active => Course != null;
        public bool TeamRace => Rafts > 1;
        public int Stars => Finished ? Course.Stars(Seconds) : 0;
    }
}
