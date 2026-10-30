using System.Collections.Generic;
using RushAhead.Net;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Runs: the room's round of play (lobby, racing, results), starting a race, stepping it, and the numbers on screen.
    /// The host (or the solo player) moves the room from phase to phase; joined players follow its Race messages.
    /// </summary>
    public sealed partial class GameController
    {
        RoomPhase phase = RoomPhase.Lobby;
        readonly RunTracker tracker = new();
        readonly List<int> finishTicks = new();
        float[] bestSplits = System.Array.Empty<float>();
        (int venue, bool team) bestSplitsFor = (-1, false);
        float lastSplitShownAt = float.NegativeInfinity;
        float previousBest;
        string opened;
        int lastSplitGate;

        /// <summary>Where the room is in the round of play. The host decides; joined players follow.</summary>
        public RoomPhase Phase => Mode == NetMode.Joined ? client.Race.Phase : phase;

        /// <summary>A fresh race with a countdown while racing; otherwise the rafts go back to the start line.</summary>
        public void ResetBoat()
        {
            if (phase == RoomPhase.Racing) StartRun();
            else Stage();
        }

        /// <summary>Loads a venue (0 is the practice lagoon) for the next race, with the rafts waiting on its start line.</summary>
        public void ChooseVenue(int index)
        {
            if (!CanEditRaft || phase == RoomPhase.Racing) return;
            LoadVenue(index);
            phase = RoomPhase.Lobby;
            Stage();
            if (index > 0 && Session != SessionKind.None) PlayerPrefs.SetInt(LastVenueKey, index);
        }

        /// <summary>Everyone to the line: 3, 2, 1, go. In the practice lagoon it just lets the rafts loose, with no clock.</summary>
        public void StartRace()
        {
            if (!CanEditRaft || rafts.Count == 0) return;
            phase = RoomPhase.Racing;
            StartRun();
        }

        /// <summary>From the results (or a practice) back to choosing the next race, with the rafts on the start line.</summary>
        public void BackToLobby()
        {
            if (!CanEditRaft) return;
            phase = RoomPhase.Lobby;
            Stage();
        }

        /// <summary>The rafts wait on the start line with their controls locked, and the barrels go back where they began.</summary>
        void Stage()
        {
            if (!CanEditRaft) return;
            for (var i = 0; i < rafts.Count; i++)
            {
                var raft = rafts[i];
                raft.Race?.Dispose();
                raft.Race = null;
                var (position, heading) = SpawnPose(i);
                raft.Boat.ResetTo(position, heading);
                raft.Boat.ClearRun();
                raft.Boat.ControlsLocked = true;
                PointAiAtCourse(i);
                raft.Ai?.Give(CrewOrder.Go);
            }

            ResetBarrels();
            WireSeats();
            SendRace();
        }

        void StartRun()
        {
            if (!CanEditRaft || rafts.Count == 0) return;
            var now = runner.World.Tick;
            for (var i = 0; i < rafts.Count; i++)
            {
                var raft = rafts[i];
                raft.Race?.Dispose();
                raft.Race = null;
                if (course != null)
                {
                    raft.Race = new Race(course, raft.Boat, now, config.tickRate, i, rafts.Count); // every raft starts on the same tick
                }
                else
                {
                    var (position, heading) = SpawnPose(i);
                    raft.Boat.ResetTo(position, heading);
                    raft.Boat.ClearRun();
                    raft.Boat.ControlsLocked = phase != RoomPhase.Racing;
                }

                PointAiAtCourse(i);
                raft.Ai?.Give(CrewOrder.Go);
            }

            if (course == null) ResetBarrels();
            WireSeats();
            SendRace();
        }

        void ResetBarrels()
        {
            if (course != null) course.ResetBarrels();
            else
                for (var i = 0; i < barrels.Count; i++) PhysicsBodies.Reset(barrels[i], BarrelSpot(i));
        }

        int StartTick => rafts.Count > 0 && rafts[0].Race != null ? rafts[0].Race.StartTick : 0;

        /// <summary>The AI crewmates' skill as the room plays it: in a Team Race the spares are always AI, at least Clumsy.</summary>
        byte RoomAiSkill => Local.AiSetting >= 0 ? (byte)Local.AiSetting : raftCount > 1 ? (byte)0 : RaceMessage.AiOff;

        /// <summary>Tells joined players what's on: the venue, the race start, the room's phase and the AI crewmates.</summary>
        void SendRace() => host?.SetRace(new RaceMessage
        {
            Course = (byte)venue,
            StartTick = (uint)Mathf.Max(0, StartTick),
            Phase = phase,
            AiSkill = RoomAiSkill,
        });

        void StepRace(int tick)
        {
            foreach (var raft in rafts)
            {
                raft.Ai?.Update(config.TickDt); // decides the AI crew's controls for the coming ticks
                raft.Race?.Step(tick);
            }
        }

        /// <summary>Host or solo: once everyone is home (or a Team Race has waited long enough), on to the results.</summary>
        void CheckForResults()
        {
            if (Mode == NetMode.Joined || phase != RoomPhase.Racing || course == null) return;
            finishTicks.Clear();
            foreach (var raft in rafts) finishTicks.Add(raft.Boat.FinishTick);
            if (!ResultsRule.Due(finishTicks, runner.World.Tick, config.tickRate)) return;
            phase = RoomPhase.Results;
            SendRace();
        }

        /// <summary>Keeps the camera, the gates and the run's numbers in step with the raft on screen.</summary>
        void UpdateRun()
        {
            CheckForResults();
            var boat = ViewedBoat;
            if (boat == null) return;
            if (boat.TeleportCount != shownTeleports)
            {
                shownTeleports = boat.TeleportCount; // a respawn or restart: cut the camera there, don't swoop
                chase.SnapTo(boat.RenderPosition, boat.RenderAngleRad);
            }

            if (courseView != null) courseView.ShowProgress(boat.GatesPassed);
            if (venue == 0)
            {
                Run = default;
                return;
            }

            var def = courses[venue - 1];
            var team = Rafts > 1;
            if (bestSplitsFor != (venue, team))
            {
                bestSplitsFor = (venue, team);
                bestSplits = Records.BestSplits(def, team);
            }

            var start = Mode == NetMode.Joined ? clientRaceStart : StartTick;
            var now = Mode == NetMode.Joined ? PlaybackTick : runner.World.Tick;
            var racing = Phase != RoomPhase.Lobby;
            var info = new RaceInfo
            {
                Course = def,
                Phase = Phase,
                Countdown = racing ? RaceClock.CountdownLeft(start, now, config.tickRate) : 0,
                SinceStart = racing ? (float)((now - start) / config.tickRate) : float.NegativeInfinity,
                Seconds = racing ? RaceClock.Seconds(start, boat.FinishTick, now, config.tickRate) : 0f,
                GatesPassed = boat.GatesPassed,
                Gates = def.gates.Length,
                Finished = boat.FinishTick > 0,
                Capsizes = boat.CapsizeCount,
                Best = Records.BestTime(def, team),
                BestSplits = bestSplits,
                Rafts = Rafts,
                MyRaft = MyRaft,
                Winner = -1,
                MyProgress = Progress(boat),
            };

            // Splits: the time at each checkpoint, against the best run's at the same one.
            tracker.Update(start, racing ? boat.GatesPassed : 0, info.Seconds, bestSplits);
            if (tracker.LastGate != lastSplitGate)
            {
                lastSplitGate = tracker.LastGate;
                if (lastSplitGate > 0) lastSplitShownAt = Time.unscaledTime;
            }

            info.Splits = tracker.Splits;
            info.SplitDeltas = tracker.Deltas;
            info.SplitGate = tracker.LastGate;
            info.SplitDelta = tracker.LastDelta;
            info.SplitAge = Time.unscaledTime - lastSplitShownAt;

            // Team Race: how the other raft is doing, and who crossed the line first (ties go to the lower tick; then Red).
            if (Rafts > 1)
            {
                var rival = RaftState(1 - MyRaft);
                if (rival != null)
                {
                    info.RivalGatesPassed = rival.GatesPassed;
                    info.RivalFinished = rival.FinishTick > 0;
                    info.RivalSeconds = racing ? RaceClock.Seconds(start, rival.FinishTick, now, config.tickRate) : 0f;
                    info.RivalProgress = Progress(rival);
                }

                var best = int.MaxValue;
                for (var raft = 0; raft < Rafts; raft++)
                {
                    var state = RaftState(raft);
                    if (state == null || state.FinishTick <= 0 || state.FinishTick >= best) continue;
                    best = state.FinishTick;
                    info.Winner = raft;
                }
            }

            if (info.Finished && racing && recordedRun != (venue, start))
            {
                // Every device keeps its own best for each course, from the host's time.
                recordedRun = (venue, start);
                previousBest = info.Best;
                var starsBefore = TotalStars;
                info.NewBest = Records.Submit(def, team, info.Seconds, tracker.Splits);
                opened = OpenedBetween(starsBefore, TotalStars);
                if (info.NewBest) bestSplitsFor = (-1, false); // the next run compares against this one
            }
            else
            {
                info.NewBest = Run.NewBest && info.Finished && racing;
            }

            info.PreviousBest = info.Finished && racing ? previousBest : info.Best;
            info.Opened = info.Finished && racing ? opened : null;
            Run = info;
        }

        /// <summary>Every star this device has won, best of Co-op and Team Race on each course: they open the later courses.</summary>
        public int TotalStars => Records.TotalStars(courses);

        /// <summary>
        /// Whether a venue can be chosen: the lagoon and the first course always; a later course once this device has
        /// its stars. The developer tools open everything. In a game together, the host's stars decide.
        /// </summary>
        public bool IsOpen(int index)
        {
            if (index <= 0 || index > courses.Length) return index == 0;
            if (Ui != null && Ui.DeveloperTools) return true;
            return TotalStars >= courses[index - 1].starsToUnlock;
        }

        string OpenedBetween(int before, int after)
        {
            if (Ui != null && Ui.DeveloperTools) return null;
            var names = new List<string>();
            foreach (var def in courses)
                if (def.starsToUnlock > before && def.starsToUnlock <= after) names.Add(def.displayName);
            return names.Count > 0 ? string.Join(" and ", names) : null;
        }

        /// <summary>The course a new game starts on: the last one chosen, if it's open, or the first.</summary>
        int PreferredVenue()
        {
            var last = PlayerPrefs.GetInt(LastVenueKey, 1);
            return last >= 1 && last <= courses.Length && IsOpen(last) ? last : Mathf.Min(1, courses.Length);
        }

        const string LastVenueKey = "rushahead.venue.v1";

        /// <summary>How far down the course a raft is, 0 at the start to 1 at the finish.</summary>
        float Progress(IBoatState boat)
        {
            if (course == null || boat == null) return 0f;
            if (boat.FinishTick > 0) return 1f;
            var finish = courses[venue - 1].FinishDistance;
            return finish > 0f ? Mathf.Clamp01(course.River.Project(boat.RenderPosition).Distance / finish) : 0f;
        }
    }
}
