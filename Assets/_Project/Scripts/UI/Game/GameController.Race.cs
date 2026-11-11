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
        readonly List<int> outTicks = new();
        float[] bestSplits = System.Array.Empty<float>();
        (int venue, bool team) bestSplitsFor = (-1, false);
        float lastSplitShownAt = float.NegativeInfinity;
        float previousBest;
        string opened;
        (int venue, int start) coinedRun = (-1, -1);
        List<CoinAward> coins;
        int lastSplitGate;
        int shownWatched = -1;

        /// <summary>Where the room is in the round of play. The host decides; joined players follow.</summary>
        public RoomPhase Phase => Mode == NetMode.Joined ? client.Race.Phase : phase;

        /// <summary>A fresh race (on a freshly dealt layout) with a countdown while racing; otherwise the rafts go back to the start line.</summary>
        public void ResetBoat()
        {
            if (phase != RoomPhase.Racing)
            {
                Stage();
                return;
            }

            if (layoutRaced) DealNewLayout(); // a fresh attempt deals a fresh layout, like Race again
            StartRun();
        }

        /// <summary>Loads a venue (0 is the practice lagoon) for the next race, with the rafts waiting on its start line.</summary>
        public void ChooseVenue(int index)
        {
            if (!CanEditRaft || phase == RoomPhase.Racing) return;
            layoutSeed = Random.Range(1, int.MaxValue); // every visit to a course deals a fresh layout
            LoadVenue(index);
            phase = RoomPhase.Lobby;
            Stage();
            if (index > 0 && Session != SessionKind.None) PlayerPrefs.SetInt(LastVenueKey, index);
        }

        /// <summary>Everyone to the line: 3, 2, 1, go. In the practice lagoon it just lets the rafts loose, with no clock.</summary>
        public void StartRace()
        {
            if (!CanEditRaft || rafts.Count == 0) return;
            if (layoutRaced) DealNewLayout(); // "Race again": a new round, a new layout
            phase = RoomPhase.Racing;
            StartRun();
        }

        /// <summary>From the results (or a practice) back to choosing the next race, with the rafts on the start line.</summary>
        public void BackToLobby()
        {
            if (!CanEditRaft) return;
            if (layoutRaced) DealNewLayout(); // the lobby shows the next round's layout
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
                raft.Boat.Moored = course != null; // on a flowing river, waiting on the start line
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
            if (course != null) course.FlowBoost = 1f;
            for (var i = 0; i < rafts.Count; i++)
            {
                var raft = rafts[i];
                raft.Race?.Dispose();
                raft.Race = null;
                if (course != null)
                {
                    raft.Race = new Race(course, raft.Boat, now, config.tickRate, i, rafts.Count); // every raft starts on the same tick
                    layoutRaced = true;
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

        /// <summary>This round's weather: the host's choice, or for Random, dealt from the round's seed. Always clear in the lagoon.</summary>
        Weather RoundWeather => venue == 0 ? Weather.Clear : Mode == NetMode.Joined ? client.Race.Weather : WeatherDeal.For(weatherChoice, layoutSeed);

        /// <summary>The lobby's weather choice: a weather, or <see cref="WeatherDeal.Random"/>. Joined, the host's round's weather.</summary>
        public int WeatherChoice => Mode == NetMode.Joined ? (int)RoundWeather : weatherChoice;

        /// <summary>The weather on the course now.</summary>
        public Weather CurrentWeather => shownWeather;

        public void SetWeatherChoice(int choice)
        {
            if (!CanEditRaft || phase == RoomPhase.Racing) return;
            weatherChoice = Mathf.Clamp(choice, 0, WeatherDeal.Random);
            PlayerPrefs.SetInt(WeatherKey, weatherChoice);
            ApplyWeather(false);
            SendRace();
        }

        /// <summary>Puts this round's weather on the course: its look, and in the rain its gusts. Again whenever the venue or the weather changes.</summary>
        void ApplyWeather(bool newVenue)
        {
            if (weatherView == null) return;
            var weather = RoundWeather;
            if (course != null) course.Gusts = weather == Weather.Rain ? new Gusts(layoutSeed) : null;
            weatherView.Show(weather, course != null ? course.Def.scene : null, newVenue);
            if (courseView != null) courseView.SetNight(weather == Weather.Night);
            shownWeather = weather;
        }

        /// <summary>Old Snapjaw now: the host's, or a joined player's from the snapshots. Not up on any other course.</summary>
        (bool present, Vector2 position, float angleRad, BossMood mood, int chomps) BossState()
        {
            if (course?.Hazards?.Boss == null) return default;
            if (Mode == NetMode.Joined)
            {
                if (client.Boats.Count == 0) return default;
                var seen = client.Boats[0].Boss;
                return (seen.Present, seen.Position, seen.AngleRad, (BossMood)seen.Mood, seen.Chomps);
            }

            var boss = course.Hazards.Boss;
            return (boss.Present, boss.Position, boss.AngleRad, boss.Mood, boss.Chomps);
        }

        /// <summary>The wind where the camera's raft is, for the rain's lean.</summary>
        (float strength, Vector2 across, float warning) GustOnScreen()
        {
            if (course == null || course.Gusts == null) return (0f, Vector2.zero, 0f);
            var (strength, side, warning) = course.Hazards.Gust(ClockSeconds());
            var at = course.River.Project(ViewedBoat.RenderPosition);
            return (strength, new Vector2(-at.Tangent.y, at.Tangent.x) * side, warning);
        }

        /// <summary>Tells joined players what's on: the venue, the race start, the room's phase and the AI crewmates.</summary>
        void SendRace() => host?.SetRace(new RaceMessage
        {
            Course = (byte)venue,
            StartTick = (uint)Mathf.Max(0, StartTick),
            Phase = phase,
            AiSkill = RoomAiSkill,
            LayoutSeed = (uint)layoutSeed,
            Mode = raceMode,
            Weather = RoundWeather,
        });

        void StepRace(int tick)
        {
            foreach (var raft in rafts)
            {
                raft.Ai?.Update(config.TickDt); // decides the AI crew's controls for the coming ticks
                raft.Race?.Step(tick);
            }

            if (course == null || Mode == NetMode.Joined) return;
            if (raceMode == GameMode.Elimination && phase == RoomPhase.Racing) Knockouts(tick);
            course.ResetStrays();
            boatsOnCourse.Clear();
            foreach (var raft in rafts) boatsOnCourse.Add(raft.Boat);
            course.Hazards.Step(exhibitClock?.Invoke() ?? tick / (double)config.tickRate, boatsOnCourse); // where they'll be for the next physics step
        }

        readonly List<Boat> boatsOnCourse = new();
        readonly List<int> knockPassed = new();
        readonly List<bool> knockOuts = new();
        readonly List<int> knocked = new();

        /// <summary>Elimination, host: the last rafts through each knockout gate go out, and the river speeds up for each raft out.</summary>
        void Knockouts(int tick)
        {
            knockPassed.Clear();
            knockOuts.Clear();
            foreach (var raft in rafts)
            {
                if (raft.Race == null) return;
                if (raft.Boat.Out && !raft.Boat.Sunk) raft.Race.KnockOut(tick); // out of lives: it sinks out of the way too
                knockPassed.Add(raft.Boat.GatesPassed);
                knockOuts.Add(raft.Boat.Out);
            }

            KnockoutRule.Decide(knockPassed, knockOuts, course.Gates.Count, knocked);
            foreach (var i in knocked) rafts[i].Race.KnockOut(tick);
            var outs = 0;
            foreach (var raft in rafts)
                if (raft.Boat.Out) outs++;
            course.FlowBoost = KnockoutRule.FlowBoost(rafts.Count, outs);
        }

        /// <summary>The race clock in seconds: the host's tick, or the tick a joined player is drawing. Obstacles run on it.</summary>
        /// <summary>The race clock in seconds, as this device poses the obstacles.</summary>
        public double ObstacleClock => ClockSeconds();

        double ClockSeconds() =>
            PhotoStage != PhotoFinishStage.None ? photoClock // the obstacles posed as they were, for the photo finish
            : exhibitClock?.Invoke() ?? (Mode == NetMode.Joined ? PlaybackTick : runner.World.Tick) / config.tickRate;

        /// <summary>For the AI: where across the river to aim to keep clear of the wheels, geysers and crocodiles there.</summary>
        float SteerAcross(float from, float to, float across) =>
            course == null ? across : course.Hazards.SteerAcross(from, to, across, runner.World.Tick / (double)config.tickRate);

        /// <summary>For the AI: whether a swinging log or geyser is about to reach a raft at this point.</summary>
        bool BraceAhead(Vector2 position, float lookahead) =>
            course != null && course.Hazards.BraceNeeded(position, runner.World.Tick / (double)config.tickRate, lookahead);

        /// <summary>Host or solo: once everyone is home (or a Team Race has waited long enough), on to the results.</summary>
        void CheckForResults()
        {
            if (Mode == NetMode.Joined || phase != RoomPhase.Racing || course == null) return;
            finishTicks.Clear();
            outTicks.Clear();
            foreach (var raft in rafts)
            {
                finishTicks.Add(raft.Boat.FinishTick);
                outTicks.Add(raft.Boat.OutTick);
            }

            if (!ResultsRule.Due(finishTicks, outTicks, runner.World.Tick, config.tickRate, raceMode == GameMode.Elimination)) return;
            phase = RoomPhase.Results;
            SendRace();
        }

        /// <summary>Keeps the camera, the gates and the run's numbers in step with the raft on screen.</summary>
        void UpdateRun()
        {
            CheckForResults();
            if (course != null && exhibitClock == null)
                course.Hazards.RaceStart = Phase == RoomPhase.Lobby ? double.NaN : (Mode == NetMode.Joined ? clientRaceStart : StartTick) / (double)config.tickRate;
            if (Phase != RoomPhase.Racing)
            {
                watchedRaft = -1; // every race starts on your own raft
                seenOut.Clear();
            }
            var viewed = ViewedBoat;
            if (viewed == null) return;
            if (viewed.TeleportCount != shownTeleports || WatchedRaft != shownWatched)
            {
                shownTeleports = viewed.TeleportCount; // a respawn, a restart or a switch to another raft: cut there, don't swoop
                shownWatched = WatchedRaft;
                chase.SnapTo(viewed.RenderPosition, viewed.RenderAngleRad);
            }

            var boat = OwnBoat;
            if (boat == null) return;
            var elimination = Elimination;
            for (var raft = 0; raft < Rafts; raft++)
                if (ViewOf(raft) is { } view)
                {
                    view.Sinks = elimination;
                    view.Lantern = shownWeather == Weather.Night;
                    if (view.Effects != null) view.Effects.Close = raft == WatchedRaft;
                }
            if (courseView != null)
            {
                if (exhibitClock == null) // the gallery sets its own banners
                    courseView.ShowKnockouts(elimination && venue > 0 ? KnockoutRule.Gates(Rafts, courses[venue - 1].gates.Length) : null);
                courseView.ShowProgress(viewed.GatesPassed);
            }
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
                Lives = boat.Lives,
                Out = boat.Out,
                Fallen = boat.Fallen,
                Gates = def.gates.Length,
                Finished = boat.FinishTick > 0,
                Capsizes = boat.CapsizeCount,
                Best = Records.BestTime(def, team),
                BestSplits = bestSplits,
                Rafts = Rafts,
                MyRaft = MyRaft,
                Winner = -1,
                MyProgress = Progress(boat),
                Mode = RaceMode,
            };

            // Splits: the time at each checkpoint, against the best run's at the same one.
            tracker.Update(start, racing ? boat.GatesPassed : 0, info.Seconds, bestSplits);
            if (tracker.LastGate != lastSplitGate)
            {
                lastSplitGate = tracker.LastGate;
                if (lastSplitGate > 0) lastSplitShownAt = Time.unscaledTime;
            }

            info.BossCourse = def.boss;
            info.BossBehind = -1f;
            var (bossUp, bossAt, _, bossMood, _) = BossState();
            if (racing && bossUp && bossMood is BossMood.Chasing or BossMood.Lunging or BossMood.Surging)
            {
                var behind = course.River.Project(ViewedBoat.RenderPosition).Distance - course.River.Project(bossAt).Distance;
                if (behind > -2f)
                {
                    info.BossBehind = Mathf.Max(0f, behind - BossCroc.JawReach);
                    info.BossLunging = bossMood is BossMood.Lunging or BossMood.Surging;
                }
            }

            if (racing && course != null && course.Gusts != null && chase != null)
            {
                var (strength, side, warning) = course.Hazards.Gust(ClockSeconds());
                var at = course.River.Project(ViewedBoat.RenderPosition);
                var blowing = new Vector2(-at.Tangent.y, at.Tangent.x) * side;
                var forward = chase.Forward;
                info.GustStrength = strength;
                info.GustWarning = warning;
                info.GustScreenSide = Vector2.Dot(blowing, new Vector2(forward.y, -forward.x)) >= 0f ? 1 : -1;
            }

            info.Splits = tracker.Splits;
            info.SplitDeltas = tracker.Deltas;
            info.SplitGate = tracker.LastGate;
            info.SplitDelta = tracker.LastDelta;
            info.SplitAge = Time.unscaledTime - lastSplitShownAt;

            // Team Race: how the other raft is doing, and who crossed the line first (ties go to the lower tick; then Red).
            if (Rafts > 1)
            {
                var rival = Rafts == 2 ? RaftState(1 - MyRaft) : null;
                if (rival != null)
                {
                    info.RivalGatesPassed = rival.GatesPassed;
                    info.RivalFinished = rival.FinishTick > 0;
                    info.RivalSeconds = racing ? RaceClock.Seconds(start, rival.FinishTick, now, config.tickRate) : 0f;
                    info.RivalProgress = Progress(rival);
                    info.RivalLives = rival.Lives;
                    info.RivalOut = rival.Out;
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

            if (elimination) Standings(ref info, def, racing);

            if (info.Finished && racing && !elimination && recordedRun != (venue, start))
            {
                // Every device keeps its own best for each course, from the host's time.
                recordedRun = (venue, start);
                previousBest = info.Best;
                var starsBefore = TotalStars;
                info.NewBest = Records.Submit(def, team, info.Seconds, tracker.Splits, info.Stars);
                opened = OpenedBetween(starsBefore, TotalStars);
                if (info.NewBest) bestSplitsFor = (-1, false); // the next run compares against this one
            }
            else
            {
                info.NewBest = Run.NewBest && info.Finished && racing;
            }

            info.PreviousBest = info.Finished && racing ? previousBest : info.Best;
            info.Opened = info.Finished && racing ? opened : null;

            // Coins once the results are in, each device paying its own player (not one only watching).
            if (Phase == RoomPhase.Results && HasOwnSeat && coinedRun != (venue, start))
            {
                coinedRun = (venue, start);
                coins = CoinRules.For(info.Stars, info.TeamRace && info.Winner == info.MyRaft, elimination ? info.Place : 0, info.NewBest, def.boss && info.Finished);
                Records.AddCoins(CoinRules.Total(coins));
            }

            info.Coins = Phase == RoomPhase.Results && coinedRun == (venue, start) ? coins : null;
            Run = info;
        }

        readonly List<int> fieldFinish = new();
        readonly List<int> fieldOutTicks = new();
        readonly List<bool> fieldOuts = new();
        readonly List<float> fieldProgress = new();

        /// <summary>Elimination: places, who's out, the knockout gates, and the river's speed-up as every device sees it.</summary>
        void Standings(ref RaceInfo info, Content.CourseDef def, bool racing)
        {
            fieldFinish.Clear();
            fieldOutTicks.Clear();
            fieldOuts.Clear();
            fieldProgress.Clear();
            var outs = 0;
            for (var raft = 0; raft < Rafts; raft++)
            {
                var state = RaftState(raft);
                var gone = state != null && state.Out;
                fieldFinish.Add(state?.FinishTick ?? 0);
                fieldOutTicks.Add(gone ? Mathf.Max(1, OutTickOf(raft)) : 0);
                fieldOuts.Add(gone);
                fieldProgress.Add(Progress(state));
                if (gone) outs++;
            }

            info.Places = KnockoutRule.Places(fieldFinish, fieldOutTicks, fieldProgress);
            info.Place = System.Array.IndexOf(info.Places, MyRaft) + 1;
            info.FieldProgress = fieldProgress.ToArray();
            info.FieldOut = fieldOuts.ToArray();
            info.KnockoutGates = KnockoutRule.Gates(Rafts, def.gates.Length);
            foreach (var gate in info.KnockoutGates)
                if (gate > info.GatesPassed)
                {
                    info.NextKnockout = gate;
                    break;
                }

            info.Winner = racing ? KnockoutRule.Winner(fieldFinish, fieldOuts) : -1;
            if (course != null)
            {
                course.FlowBoost = racing ? KnockoutRule.FlowBoost(Rafts, outs) : 1f; // the host's own is set as it steps; the same number
                if (courseView != null) courseView.SetFlowBoost(course.FlowBoost);
            }
        }

        /// <summary>When a raft went out, for placing: the host knows the tick; joined players only the order they saw it in.</summary>
        int OutTickOf(int raft)
        {
            if (Mode != NetMode.Joined) return raft < rafts.Count ? rafts[raft].Boat.OutTick : 0;
            while (seenOut.Count <= raft) seenOut.Add(0);
            if (seenOut[raft] == 0) seenOut[raft] = Mathf.Max(1, (int)PlaybackTick);
            return seenOut[raft];
        }

        readonly List<int> seenOut = new();

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
