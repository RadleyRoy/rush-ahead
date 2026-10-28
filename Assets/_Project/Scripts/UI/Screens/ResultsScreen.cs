using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// After the race: who won (Team Race) or how it went, the time and its stars, how it compares with your best,
    /// each checkpoint's split, and what's next. The host chooses what's next; joiners wait for them.
    /// </summary>
    public sealed class ResultsScreen : GameScreen
    {
        Label headline;
        Label time;
        StarRow stars;
        Label versus;
        Label teams;
        Label splits;
        Label details;
        VisualElement hostButtons;
        VisualElement joinerRow;
        Label waiting;
        Button menu;

        public ResultsScreen(GameUi ui) : base(ui)
        {
        }

        public override void Build()
        {
            Root.AddToClassList("row");
            Root.style.alignItems = Align.Stretch;
            Root.Add(Ui.Grow()); // the raft circles on the left

            var panel = Ui.El("panel col");
            panel.style.width = 900;
            headline = Ui.Text("", "h1 sun");
            headline.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(headline);
            panel.Add(Ui.Spacer("spacer-s"));
            time = Ui.Text("", "wordmark");
            time.style.fontSize = 130;
            stars = new StarRow(70f);
            panel.Add(Ui.El("row", time, Ui.Grow(), stars));
            versus = Ui.Text("", "h3");
            panel.Add(versus);
            teams = Ui.Text("", "h3");
            panel.Add(teams);
            panel.Add(Ui.Spacer());
            splits = Ui.Text("", "body");
            panel.Add(splits);
            details = Ui.Text("", "small muted");
            panel.Add(details);
            panel.Add(Ui.Grow());

            hostButtons = Ui.El("row");
            hostButtons.Add(Btn("results-again", "Race again", Game.StartRace, "btn btn--primary"));
            hostButtons.Add(Ui.Spacer());
            hostButtons.Add(Ui.Grow());
            hostButtons.Add(Btn("results-lobby", "Lobby", Game.BackToLobby, "btn btn--small"));
            menu = Btn("results-menu", "Menu", () =>
            {
                if (Game.Session == SessionKind.Solo) Game.EndSession();
                else App.ConfirmLeave();
            }, "btn btn--small");
            hostButtons.Add(menu);
            panel.Add(hostButtons);

            waiting = Ui.Text("", "h3 muted");
            joinerRow = Ui.El("row", waiting, Ui.Grow(), Btn("results-leave", "Leave", () => App.ConfirmLeave(), "btn btn--small"));
            panel.Add(joinerRow);
            Root.Add(panel);
        }

        public override void Refresh()
        {
            var run = Game.Run;
            var canEdit = Game.CanEditRaft;
            Ui.Show(hostButtons, canEdit);
            Ui.Show(joinerRow, !canEdit);
            Ui.Set(waiting, $"Waiting for {HostName()} to choose...");
            if (!run.Active) return;

            var mine = GameController.TeamName(run.MyRaft);
            Ui.Set(headline, run.TeamRace
                ? run.Winner < 0 ? "Nobody made it home!" : run.Winner == run.MyRaft ? $"{mine} raft wins! That's you!" : $"{GameController.TeamName(run.Winner)} raft wins!"
                : !run.Finished ? "Out of time" : run.NewBest ? "New best!" : "Finished!");
            Ui.Set(time, run.Finished ? Ui.Time(run.Seconds) : "--");
            stars.Count = run.Stars;

            var previous = run.PreviousBest;
            Ui.Set(versus, !run.Finished ? ""
                : previous <= 0f ? "Your first time on this course."
                : run.NewBest ? $"{previous - run.Seconds:0.0} s faster than your old best, {Ui.Time(previous)}"
                : $"{run.Seconds - previous:0.0} s slower than your best, {Ui.Time(previous)}");
            Ui.SetClass(versus, "good", run.NewBest);

            Ui.Show(teams, run.TeamRace);
            if (run.TeamRace)
            {
                var theirs = GameController.TeamName(1 - run.MyRaft);
                var margin = run.Finished && run.RivalFinished ? $", {Mathf.Abs(run.Seconds - run.RivalSeconds):0.0} s apart" : "";
                Ui.Set(teams, $"{mine} {(run.Finished ? Ui.Time(run.Seconds) : "didn't finish")}   {theirs} {(run.RivalFinished ? Ui.Time(run.RivalSeconds) : "didn't finish")}{margin}");
            }

            Ui.Set(splits, Splits(run));
            var course = run.Course;
            Ui.Set(details, $"{run.Capsizes} capsize{(run.Capsizes == 1 ? "" : "s")}.   Stars at {Ui.Time(course.threeStarSeconds)}, {Ui.Time(course.twoStarSeconds)} and {Ui.Time(course.parSeconds)}.");
        }

        static string Splits(RaceInfo run)
        {
            if (run.Splits == null || run.Splits.Count == 0) return "";
            var text = new StringBuilder();
            for (var i = 0; i < run.Splits.Count; i++)
            {
                if (i > 0) text.Append("    ");
                text.Append(i == run.Gates - 1 ? "Finish " : $"CP{i + 1} ").Append(Ui.Time(run.Splits[i]));
                var delta = run.SplitDeltas != null && i < run.SplitDeltas.Count ? run.SplitDeltas[i] : float.NaN;
                if (!float.IsNaN(delta)) text.Append(" (").Append(Ui.Delta(delta)).Append(')');
            }

            return text.ToString();
        }

        string HostName()
        {
            foreach (var seat in Game.Seats)
                if (seat.Host && !string.IsNullOrEmpty(seat.Loadout.Name)) return seat.Loadout.Name;
            return "the host";
        }
    }
}
