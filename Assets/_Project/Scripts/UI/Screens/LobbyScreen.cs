using System.Text;
using RushAhead.Net;
using RushAhead.Render;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// Before each race: the course and how it's raced on the left (the host's to choose; joiners see their choices),
    /// the rafts and who sits where on the right, and Start. The same screen for a solo game, a host and a joiner.
    /// </summary>
    public sealed class LobbyScreen : GameScreen
    {
        Label title;
        Label roomInfo;
        Label code;
        Button copyCode;
        Label players;
        Label courseName;
        Label courseBlurb;
        StarRow courseStars;
        Label courseBest;
        Label courseTimes;
        Button changeCourse;
        Segmented mode;
        Segmented crew;
        Segmented ai;
        Label aiBlurb;
        Label chooser;
        VisualElement seats;
        string seatsShown;
        Button start;
        Label waiting;
        VisualElement picker;

        public LobbyScreen(GameUi ui) : base(ui)
        {
        }

        public override void Build()
        {
            // Header: what kind of game, how to join it, and who's here.
            var header = Ui.El("row");
            title = Ui.Text("", "h1");
            header.Add(title);
            header.Add(Ui.Spacer("spacer-l"));
            roomInfo = Ui.Text("", "chip");
            header.Add(roomInfo);
            code = Ui.Text("", "h1 sun");
            code.style.marginLeft = 20;
            header.Add(code);
            copyCode = Btn("lobby-copy", "Copy", () =>
            {
                GUIUtility.systemCopyBuffer = Game.OnlineCode;
                App.Toast("Code copied. Send it to your friends.");
            }, "btn btn--small");
            copyCode.style.marginLeft = 20;
            header.Add(copyCode);
            header.Add(Ui.Grow());
            players = Ui.Text("", "h3 muted");
            header.Add(players);
            Root.Add(header);
            Root.Add(Ui.Spacer());

            var middle = Ui.El("row grow");
            middle.style.alignItems = Align.Stretch;

            // Left: the course and how it's raced.
            var left = Ui.El("panel col");
            left.style.width = 700;
            var leftScroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            leftScroll.AddToClassList("scroll");
            left.Add(leftScroll);
            var settings = leftScroll.contentContainer;
            var course = Ui.El("card");
            courseName = Ui.Text("", "h2 ellipsis");
            courseBlurb = Ui.Text("", "small muted ellipsis");
            courseStars = new StarRow(40f);
            courseBest = Ui.Text("", "h3");
            courseTimes = Ui.Text("", "small muted");
            changeCourse = Btn("lobby-course", "Change", OpenPicker, "btn btn--small");
            changeCourse.style.marginRight = 0;
            courseName.style.flexGrow = 1;
            courseName.style.flexShrink = 1;
            course.Add(Ui.El("row", courseName, changeCourse));
            course.Add(courseBlurb);
            course.Add(Ui.Spacer("spacer-s"));
            course.Add(Ui.El("row", courseStars, Ui.Spacer(), courseBest));
            course.Add(courseTimes);
            settings.Add(course);

            settings.Add(Ui.Text("Mode", "small muted"));
            mode = new Segmented(App, "lobby-mode", new[] { "Co-op", "Team Race" }, i => Game.SetTeamRace(i == 1));
            settings.Add(mode);
            settings.Add(Ui.Spacer("spacer-s"));
            settings.Add(Ui.Text("Animals per raft", "small muted"));
            crew = new Segmented(App, "lobby-crew", new[] { "1", "2", "3", "4" }, i => Game.SetCrewCount(i + 1));
            settings.Add(crew);
            settings.Add(Ui.Spacer("spacer-s"));
            settings.Add(Ui.Text("AI crewmates", "small muted"));
            ai = new Segmented(App, "lobby-ai", new[] { "Off", "Clumsy", "Keen", "Ace" }, i => Game.SetAiSetting(i - 1));
            settings.Add(ai);
            aiBlurb = Ui.Text("", "small muted");
            settings.Add(aiBlurb);
            chooser = Ui.Text("", "body sun");
            settings.Add(chooser);
            middle.Add(left);
            middle.Add(Ui.Spacer());

            // Right: the rafts and who sits where.
            var right = Ui.El("panel col grow");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("scroll");
            seats = scroll.contentContainer;
            right.Add(scroll);
            middle.Add(right);
            Root.Add(middle);
            Root.Add(Ui.Spacer());

            // Bottom: leave, dress, start.
            var bottom = Ui.El("row");
            bottom.Add(Btn("lobby-leave", "Leave", Leave, "btn btn--small"));
            bottom.Add(Btn("lobby-animal", "Your animal", () =>
            {
                App.DressingSeat = -1;
                Flow.OpenAnimal();
            }, "btn btn--small"));
            bottom.Add(Ui.Grow());
            waiting = Ui.Text("", "h3 muted");
            bottom.Add(waiting);
            start = Btn("lobby-start", "Start", Game.StartRace, "btn btn--primary");
            start.style.width = 420;
            start.style.marginBottom = 0;
            bottom.Add(start);
            Root.Add(bottom);

            picker = Ui.El("layer scrim center");
            Ui.Show(picker, false);
            Root.Add(picker);
        }

        void Leave()
        {
            if (Game.Session == SessionKind.Solo) Game.EndSession();
            else App.ConfirmLeave();
        }

        public override void OnShow()
        {
            seatsShown = null;
            Ui.Show(picker, false);
        }

        public override void Refresh()
        {
            var session = Game.Session;
            var canEdit = Game.CanEditRaft;
            var host = HostName();

            Ui.Set(title, session switch
            {
                SessionKind.Solo => "Solo",
                SessionKind.Joined => $"{host}'s game",
                _ => "Your game",
            });
            var onlineCode = Game.OnlineCode;
            Ui.Set(roomInfo, session switch
            {
                SessionKind.Solo => "You and your AI crew",
                SessionKind.Hosting when onlineCode != null => "Join code",
                SessionKind.Hosting => $"On Wi-Fi at {Game.HostingAddress}",
                _ => onlineCode != null ? "Online" : "On Wi-Fi",
            });
            var showCode = session == SessionKind.Hosting && onlineCode != null;
            Ui.Show(code, showCode);
            Ui.Show(copyCode, showCode);
            if (showCode) Ui.Set(code, onlineCode);
            var aboard = 0;
            foreach (var seat in Game.Seats)
                if (seat.Human) aboard++;
            Ui.Set(players, session == SessionKind.Solo ? "" : aboard == 1 ? "1 player" : $"{aboard} players");

            // The course.
            var venue = Game.Venue;
            var team = Game.TeamRace;
            if (venue == 0)
            {
                Ui.Set(courseName, "Practice lagoon");
                Ui.Set(courseBlurb, "Free play round the lagoon: no clock, no finish.");
                Ui.Show(courseStars, false);
                Ui.Set(courseBest, "");
                Ui.Set(courseTimes, "");
            }
            else
            {
                var def = Game.Courses[venue - 1];
                Ui.Set(courseName, def.displayName);
                Ui.Set(courseBlurb, $"{Facts(def)}. {def.blurb}");
                Ui.Show(courseStars, true);
                var best = Records.BestTime(def, team);
                courseStars.Count = Records.BestStars(def, team);
                Ui.Set(courseBest, best > 0f ? $"Best {Ui.Time(best)}" : "No time yet");
                Ui.Set(courseTimes, $"Stars at {Ui.Time(def.threeStarSeconds)}, {Ui.Time(def.twoStarSeconds)} and {Ui.Time(def.parSeconds)}");
            }

            Ui.Show(changeCourse, canEdit);
            mode.Selected = team ? 1 : 0;
            crew.Selected = Game.CrewCount - 1;
            ai.Selected = Game.AiSetting + 1;
            Ui.Enable(mode, canEdit);
            Ui.Enable(crew, canEdit);
            Ui.Enable(ai, canEdit);
            ai.Enable(0, !team); // in a Team Race the spare seats always have AI crewmates
            var skill = Game.AiSetting;
            Ui.Set(aiBlurb, skill >= 0 && skill < Game.AiProfiles.Count ? Game.AiProfiles[skill].blurb
                : team ? "" : "Spare seats are yours: steer them with a second stick or keyboard.");
            Ui.Set(chooser, canEdit ? "" : $"{host} chooses the race.");

            Ui.Show(start, canEdit);
            Ui.Show(waiting, !canEdit);
            Ui.Set(waiting, $"Waiting for {host} to start...");

            RefreshSeats();
        }

        string HostName()
        {
            foreach (var seat in Game.Seats)
                if (seat.Host && !string.IsNullOrEmpty(seat.Loadout.Name)) return seat.Loadout.Name;
            return "The host";
        }

        void RefreshSeats()
        {
            var all = Game.Seats;
            var rafts = Mathf.Max(1, Game.Rafts);
            var crewSize = Mathf.Max(1, Game.CrewCount);
            var signature = new StringBuilder().Append(rafts).Append('/').Append(crewSize);
            for (var i = 0; i < all.Length; i++)
            {
                var s = all[i];
                signature.Append('|').Append(s.Loadout).Append(s.Human).Append(s.Host).Append(s.Yours).Append(s.Ai).Append(s.Kept).Append(Game.CanDress(i));
            }

            var text = signature.ToString();
            if (text == seatsShown) return;
            seatsShown = text;
            seats.Clear();

            for (var raft = 0; raft < rafts; raft++)
            {
                var heading = rafts > 1 ? $"{GameController.TeamName(raft)} raft" : "Your raft";
                var label = Ui.Text(heading + (rafts > 1 && raft == Game.MyRaft ? " (yours)" : ""), "h3" + (rafts > 1 ? raft == 0 ? " team-red" : " team-blue" : ""));
                label.style.marginTop = raft > 0 ? 18 : 0;
                label.style.marginBottom = 10;
                seats.Add(label);
                for (var seat = raft * crewSize; seat < (raft + 1) * crewSize && seat < all.Length; seat++)
                    seats.Add(SeatRow(seat, all[seat], seat - raft * crewSize));
            }
        }

        VisualElement SeatRow(int index, RosterSeat seat, int seatInRaft)
        {
            var look = seat.Loadout;
            var name = string.IsNullOrEmpty(look.Name) ? $"P{seatInRaft + 1}" : look.Name;
            var detail = $"{Game.Catalog.SpeciesOf(look).displayName}, {Game.Catalog.BlowerOf(look).displayName}";
            var info = Ui.El("col grow", Ui.Text(name, "h3 ellipsis"), Ui.Text(detail, "small muted ellipsis"));
            info.style.overflow = Overflow.Hidden;

            var row = Ui.El("card seat row");
            var chip = new Swatch(RaftView.PlayerColor(seatInRaft), 34f);
            chip.style.marginRight = 20;
            row.Add(chip);
            row.Add(info);
            var tag = seat.Yours ? "You" : seat.Kept ? "Away" : seat.Human ? seat.Host ? "Host" : "Player" : seat.Ai ? "AI" : "Spare";
            row.Add(Ui.Text(tag, "chip"));
            if (!seat.Human && !seat.Yours)
                row.Add(Ui.Btn("Sit here", () => Game.TakeSeat(index), "btn btn--small"));
            if (Game.CanDress(index) && !seat.Yours)
                row.Add(Ui.Btn("Dress", () =>
                {
                    App.DressingSeat = index;
                    Flow.OpenAnimal();
                }, "btn btn--small"));
            return row;
        }

        void OpenPicker()
        {
            picker.Clear();
            var panel = Ui.El("panel col");
            panel.style.width = 1240;
            panel.style.maxHeight = new Length(96, LengthUnit.Percent);
            var total = Game.TotalStars;
            var heading = Ui.El("row", Ui.Text("Choose a course", "h1"), Ui.Grow(), Ui.Text($"{total} of {Game.Courses.Count * 3} stars", "h3 sun"));
            heading.style.flexShrink = 0; // the list scrolls; the heading and Close keep their size
            heading.style.marginBottom = 12;
            panel.Add(heading);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("scroll");
            for (var i = 0; i < Game.Courses.Count; i++)
            {
                var def = Game.Courses[i];
                scroll.Add(VenueCard(i + 1, def.displayName, def.blurb, def));
            }

            scroll.Add(VenueCard(0, "Practice lagoon", "Free play round the lagoon: no clock, no finish.", null));
            panel.Add(scroll);
            var footer = Ui.El("row", Ui.Btn("Close", () => Ui.Show(picker, false), "btn btn--small"));
            footer.style.flexShrink = 0;
            footer.style.marginTop = 12;
            panel.Add(footer);
            picker.Add(panel);
            Ui.Show(picker, true);
        }

        VisualElement VenueCard(int venue, string name, string blurb, Content.CourseDef def)
        {
            var open = Game.IsOpen(venue);
            var card = Ui.El("card row" + (venue == Game.Venue ? " card--selected" : ""));
            if (!open) card.style.opacity = 0.55f;
            var info = Ui.El("col grow", Ui.Text(name, "h2"));
            info.style.flexShrink = 1;
            if (def != null) info.Add(Ui.Text(Facts(def), "small sun"));
            info.Add(Ui.Text(blurb, "small muted"));
            card.Add(info);
            if (def != null && open)
            {
                var stars = new StarRow(36f) { Count = Records.BestStars(def, Game.TeamRace) };
                var best = Records.BestTime(def, Game.TeamRace);
                card.Add(Ui.El("col", stars, Ui.Text(best > 0f ? Ui.Time(best) : "--", "h3")));
                card.Add(Ui.Spacer());
            }

            if (!open)
            {
                var needs = Ui.Text($"{def.starsToUnlock} stars to open", "body");
                needs.style.marginLeft = 24;
                card.Add(needs);
                return card;
            }

            card.Add(Btn($"venue-{venue}", "Choose", () =>
            {
                Game.ChooseVenue(venue);
                Ui.Show(picker, false);
            }, "btn btn--small btn--primary"));
            return card;
        }

        /// <summary>Where it is, how long, and what's in the water: "Rapids, 290 m: currents, whirlpools, logs".</summary>
        static string Facts(Content.CourseDef def)
        {
            var hazards = new System.Collections.Generic.List<string>();
            if (def.currents.Length > 0) hazards.Add("currents");
            if (def.whirlpools.Length > 0) hazards.Add("whirlpools");
            if (def.logs.Length > 0) hazards.Add("logs");
            if (def.barrels.Length > 0) hazards.Add("barrels");
            if (def.rocks.Length > 0) hazards.Add("rocks");
            var facts = $"{def.scene}, {def.FinishDistance:0} m";
            return hazards.Count > 0 ? $"{facts}: {string.Join(", ", hazards)}" : facts;
        }
    }
}
