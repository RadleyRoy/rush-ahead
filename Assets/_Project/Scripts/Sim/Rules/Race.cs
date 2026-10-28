using System;
using UnityEngine;

namespace RushAhead.Sim
{
    public enum RacePhase
    {
        Countdown,
        Racing,
        Finished,
    }

    /// <summary>
    /// The rules of a co-op run down a course for one raft: a countdown with the controls locked; checkpoints that
    /// only count in order and going downstream; after every capsize, a respawn just past the last checkpoint with a
    /// moment's protection from capsizing again; and a finishing time counted in host ticks, never read from a clock,
    /// so every device shows the same result.
    /// </summary>
    public sealed class Race : IDisposable
    {
        public const float CountdownSeconds = 3f;
        public const float RespawnImmunitySeconds = 2f;

        readonly int tickRate;
        readonly int lane, lanes;
        Vector2 lastPosition;

        /// <summary>
        /// Puts the raft on the start line with full tanks and begins the countdown from <paramref name="nowTick"/>.
        /// In a Team Race each raft gets its own <see cref="Race"/> with the same start tick and its own lane.
        /// </summary>
        public Race(Course course, Boat boat, int nowTick, int tickRate, int lane = 0, int lanes = 1)
        {
            Course = course;
            Boat = boat;
            this.tickRate = tickRate;
            this.lane = lane;
            this.lanes = lanes;
            StartTick = nowTick + Mathf.RoundToInt(CountdownSeconds * tickRate);

            if (lane == 0) course.ResetBarrels(); // once per race, not once per raft
            var (start, heading) = course.RespawnPose(0, lane, lanes);
            boat.ResetTo(start, heading);
            boat.ControlsLocked = true;
            boat.ClearRun();
            lastPosition = boat.Body.Position;
            boat.Recovered += OnRecovered;
        }

        public event Action<Race> Finished;

        public Course Course { get; }
        public Boat Boat { get; }

        /// <summary>The tick the countdown ends and the clock starts.</summary>
        public int StartTick { get; }

        public RacePhase Phase { get; private set; }

        /// <summary>The run's time so far at <paramref name="tick"/>, or its finishing time once it has finished.</summary>
        public float SecondsAt(int tick) => RaceClock.Seconds(StartTick, Boat.FinishTick, tick, tickRate);

        /// <summary>Call once per simulation tick.</summary>
        public void Step(int tick)
        {
            if (Phase == RacePhase.Countdown)
            {
                if (tick < StartTick) return;
                Phase = RacePhase.Racing;
                Boat.ControlsLocked = false;
            }

            if (Phase != RacePhase.Racing) return;

            var position = Boat.Body.Position;
            var gates = Course.Gates;
            if (Boat.GatesPassed < gates.Count && gates[Boat.GatesPassed].CrossedBy(lastPosition, position))
            {
                Boat.GatesPassed++;
                if (Boat.GatesPassed == gates.Count)
                {
                    Boat.FinishTick = Mathf.Max(1, tick); // 0 means "not finished"
                    Phase = RacePhase.Finished;
                    Boat.ControlsLocked = true; // the time is in; the raft drifts on past the line
                    Finished?.Invoke(this);
                }
            }

            lastPosition = position;
        }

        void OnRecovered(Boat boat)
        {
            if (Phase != RacePhase.Racing) return; // before the start or after the finish it just rights itself where it is

            var (position, angle) = Course.RespawnPose(boat.GatesPassed, lane, lanes);
            boat.ResetTo(position, angle);
            boat.CapsizeImmunitySeconds = RespawnImmunitySeconds;
            lastPosition = position;
        }

        public void Dispose()
        {
            Boat.Recovered -= OnRecovered;
            Boat.ControlsLocked = false;
        }
    }

    /// <summary>The same race time from the same numbers on every device: the host's live race, or a client's snapshots.</summary>
    public static class RaceClock
    {
        /// <param name="finishTick">0 while still racing.</param>
        public static float Seconds(int startTick, int finishTick, double nowTick, int tickRate) =>
            (float)(Math.Max(0.0, (finishTick > 0 ? finishTick : nowTick) - startTick) / tickRate);

        /// <summary>Whole seconds left in the countdown (3, 2, 1), or 0 once it's over.</summary>
        public static int CountdownLeft(int startTick, double nowTick, int tickRate) =>
            nowTick >= startTick ? 0 : (int)Math.Ceiling((startTick - nowTick) / tickRate);
    }
}
