using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Sim;

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

        /// <summary>Lives left (each capsize or fall costs one), out of lives, and gone over the edge right now.</summary>
        public int Lives;
        public bool Out;
        public bool Fallen;
        public int RivalLives;
        public bool RivalOut;

        /// <summary>Checkpoints on the course, the finish included.</summary>
        public int Gates;
        public bool Finished;
        public int Capsizes;

        /// <summary>This device's best time on the course before this run, or 0 if it has none.</summary>
        public float Best;

        public bool NewBest;

        /// <summary>The best time before this run finished, to show the improvement (or the gap); 0 if there was none.</summary>
        public float PreviousBest;

        /// <summary>Courses this run's stars opened, or null.</summary>
        public string Opened;

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

        /// <summary>Co-op, Team Race or Elimination.</summary>
        public GameMode Mode;

        /// <summary>Elimination: every raft, best placed first (raft numbers), and this player's place from 1.</summary>
        public int[] Places;
        public int Place;

        /// <summary>Elimination: each raft's progress (0 to 1), whether it's out, and the knockout gates as checkpoint counts.</summary>
        public float[] FieldProgress;
        public bool[] FieldOut;
        public int[] KnockoutGates;

        /// <summary>Elimination: the next knockout gate ahead of this player's raft (a checkpoint count), or 0.</summary>
        public int NextKnockout;

        /// <summary>The boss course: how far behind the raft on screen Old Snapjaw is (-1 when it's not up, or not after it), and whether it's lunging.</summary>
        public bool BossCourse;
        public float BossBehind;
        public bool BossLunging;

        /// <summary>The results: the coins this run earned this player, and why. Null before the results.</summary>
        public IReadOnlyList<CoinAward> Coins;

        /// <summary>The wind in the rain: how near the next gust is (0 to 1), how hard one's blowing (0 to 1), and which way on the screen (-1 left, +1 right).</summary>
        public float GustWarning;
        public float GustStrength;
        public int GustScreenSide;

        public bool Active => Course != null;
        public bool Elimination => Mode == GameMode.Elimination;

        /// <summary>Two rafts, Red against Blue.</summary>
        public bool TeamRace => Rafts > 1 && !Elimination;

        /// <summary>Knocked out of an Elimination at a gate, with lives to spare.</summary>
        public bool KnockedOut => Elimination && Out && Lives > 0;
        /// <summary>A star for every life left at the finish (Elimination is about places, not stars).</summary>
        public int Stars => Elimination ? 0 : CourseDef.Stars(Finished, Lives);
    }
}
