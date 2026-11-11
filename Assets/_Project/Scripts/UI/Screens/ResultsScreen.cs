using System.Collections.Generic;
using System.Linq;
using RushAhead.Content;
using RushAhead.Render;
using RushAhead.Sim;
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
        Label opened;
        Label coinsEarned;
        Label coinsWhy;
        Label coinsTotal;
        VisualElement coinRow;
        int shownCoins;
        float coinTick;
        Label teams;
        VisualElement splits;
        readonly List<(VisualElement cell, Label name, Label time, Label delta)> splitCells = new();
        Label details;
        VisualElement culpritRow;
        Label culprits;
        VisualElement hostButtons;
        VisualElement joinerRow;
        Label waiting;
        Button menu;
        Button again;

        VisualElement photo;
        Image photoImage;
        Label photoLabel;
        float shownFor;
        bool jingled;

        public ResultsScreen(GameUi ui) : base(ui)
        {
        }

        public override void OnShow()
        {
            shownFor = 0f;
            jingled = false;
            stars.Count = 0;
            shownCoins = 0;
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
            opened = Ui.Text("", "h3 sun");
            panel.Add(opened);
            coinsEarned = Ui.Text("", "h2 sun");
            coinsEarned.name = "results-coins";
            coinsWhy = Ui.Text("", "small muted");
            coinsWhy.style.marginLeft = 24;
            coinsWhy.style.whiteSpace = WhiteSpace.Normal;
            coinsWhy.style.flexShrink = 1;
            coinsTotal = Ui.Text("", "small muted");
            coinRow = Ui.El("row", coinsEarned, coinsWhy, Ui.Grow(), coinsTotal);
            coinRow.style.alignItems = Align.Center;
            panel.Add(coinRow);
            teams = Ui.Text("", "h3");
            panel.Add(teams);
            panel.Add(Ui.Spacer());
            splits = Ui.El("row");
            splits.style.flexWrap = Wrap.Wrap;
            splits.style.alignItems = Align.FlexStart;
            splits.style.flexShrink = 0; // a second row of cells pushes the lines below down instead of sitting on them
            panel.Add(splits);
            details = Ui.Text("", "small muted");
            panel.Add(details);
            panel.Add(Ui.Spacer("spacer-s"));
            culprits = Ui.Text("", "body");
            culprits.style.flexShrink = 1;
            culpritRow = Ui.El("row", culprits, Ui.Grow(), Btn("results-culprit", "What hit me?", () =>
            {
                var last = App.Culprits.Count > 0 ? App.Culprits[App.Culprits.Count - 1] : null;
                App.OpenGuide(last);
            }, "btn btn--small"));
            panel.Add(culpritRow);
            panel.Add(Ui.Grow());

            hostButtons = Ui.El("row");
            again = Btn("results-again", "Race again", Game.StartRace, "btn btn--primary");
            hostButtons.Add(again);
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

            // The photo finish's picture, pinned up like a print, over the left of the screen.
            photoImage = new Image { scaleMode = ScaleMode.ScaleAndCrop };
            photoImage.style.width = 520;
            photoImage.style.height = 292;
            photoLabel = Ui.Text("", "small");
            photoLabel.style.color = new Color(0.2f, 0.15f, 0.1f);
            photoLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            photoLabel.style.marginTop = 6;
            photo = Ui.El("col", photoImage, photoLabel);
            photo.name = "results-photo";
            photo.pickingMode = PickingMode.Ignore;
            photo.style.position = Position.Absolute;
            photo.style.left = 70;
            photo.style.bottom = 80;
            photo.style.backgroundColor = new Color(0.96f, 0.94f, 0.88f);
            photo.style.paddingTop = photo.style.paddingLeft = photo.style.paddingRight = 12;
            photo.style.paddingBottom = 8;
            photo.style.rotate = new Rotate(-3f);
            Ui.Show(photo, false);
            Root.Add(photo);
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
            Ui.Set(headline, run.Elimination
                ? run.Winner == run.MyRaft ? "You win the Elimination!" : run.Winner >= 0 ? $"{GameController.TeamName(run.Winner)} raft wins! You came {Ordinal(run.Place)}."
                    : "Every raft was knocked out!"
                : run.TeamRace
                ? run.Winner >= 0 ? run.Winner == run.MyRaft ? $"{mine} raft wins! That's you!" : $"{GameController.TeamName(run.Winner)} raft wins!"
                    : run.Out && run.RivalOut ? "Both rafts ran out of lives!" : "Nobody made it home!"
                : run.Out ? "Out of lives!" : !run.Finished ? "Out of time"
                : run.BossCourse ? run.NewBest ? "Escaped, with a new best!" : "You escaped Old Snapjaw!"
                : run.NewBest ? "New best!" : "Finished!");
            var againText = !run.TeamRace && !run.Elimination && run.Out ? "TRY AGAIN" : "RACE AGAIN";
            if (again.text != againText) again.text = againText;
            Ui.Set(time, run.Elimination ? Ordinal(run.Place).ToUpperInvariant() : run.Finished ? Ui.Time(run.Seconds) : "--");
            // The stars arrive one at a time, each with a ding a little higher than the last; then any fanfare.
            shownFor += Time.unscaledDeltaTime;
            var due = Mathf.Min(run.Stars, Mathf.FloorToInt((shownFor - 0.5f) / 0.35f) + 1);
            while (stars.Count < due)
            {
                stars.Count++;
                var audio = GameAudio.Instance;
                audio?.Play(audio.Bank.Star, 0.8f, 1f + 0.12f * (stars.Count - 1));
            }

            if (!jingled && shownFor > 0.6f + run.Stars * 0.35f && (run.NewBest || run.Opened != null))
            {
                jingled = true;
                var audio = GameAudio.Instance;
                audio?.Play(audio.Bank.Jingle, 0.8f);
            }

            var previous = run.PreviousBest;
            Ui.Set(versus, !run.Finished || run.Elimination ? ""
                : previous <= 0f ? "Your first time on this course."
                : run.NewBest ? $"{previous - run.Seconds:0.0} s faster than your old best, {Ui.Time(previous)}"
                : $"{run.Seconds - previous:0.0} s slower than your best, {Ui.Time(previous)}");
            Ui.SetClass(versus, "good", run.NewBest);
            Ui.Show(opened, run.Opened != null);
            if (run.Opened != null) Ui.Set(opened, $"Your stars opened {run.Opened}!");
            ShowCoins(run);

            Ui.Show(teams, run.TeamRace || run.Elimination);
            if (run.Elimination && run.Places != null)
            {
                var line = new System.Text.StringBuilder();
                for (var i = 0; i < run.Places.Length; i++)
                {
                    var raft = run.Places[i];
                    if (i > 0) line.Append("   ");
                    line.Append(Ordinal(i + 1)).Append(' ').Append(GameController.TeamName(raft)).Append(raft == run.MyRaft ? " (you)" : "");
                }

                Ui.Set(teams, line.ToString());
            }
            else if (run.TeamRace)
            {
                var theirs = GameController.TeamName(1 - run.MyRaft);
                var margin = run.Finished && run.RivalFinished ? $", {Mathf.Abs(run.Seconds - run.RivalSeconds):0.0} s apart" : "";
                var me = run.Finished ? Ui.Time(run.Seconds) : run.Out ? "out of lives" : "didn't finish";
                var them = run.RivalFinished ? Ui.Time(run.RivalSeconds) : run.RivalOut ? "out of lives" : "didn't finish";
                Ui.Set(teams, $"{mine} {me}   {theirs} {them}{margin}");
            }

            ShowSplits(run);
            var picture = Game.PhotoPicture;
            Ui.Show(photo, picture != null);
            if (picture != null && photoImage.image != picture)
            {
                photoImage.image = picture;
                Ui.Set(photoLabel, $"PHOTO FINISH   {Game.PhotoCaption}");
            }

            Ui.Show(culpritRow, App.Culprits.Count > 0);
            if (App.Culprits.Count > 0)
                Ui.Set(culprits, "Caught out by " + string.Join(", ", App.Culprits.Select(id => ObstacleGuide.ById(id)?.Name.ToLowerInvariant() ?? id)) + ".");
            Ui.Set(details, run.Elimination ? $"{run.Lives} of {Race.MaxLives} lives left, {run.Capsizes} capsize{(run.Capsizes == 1 ? "" : "s")}.   The last raft through each knockout gate is out."
                : $"{run.Lives} of {Race.MaxLives} lives left, {run.Capsizes} capsize{(run.Capsizes == 1 ? "" : "s")}.   A star for every life you finish with.");
        }

        static string Ordinal(int place) => CoinRules.Ordinal(place);

        /// <summary>"+35 COINS", counting up once the stars are in, with what earned them and the new total.</summary>
        void ShowCoins(RaceInfo run)
        {
            var total = CoinRules.Total(run.Coins);
            Ui.Show(coinRow, run.Coins != null && total > 0);
            if (run.Coins == null || total <= 0) return;
            if (shownFor > 0.9f + run.Stars * 0.35f && shownCoins < total)
            {
                coinTick += Time.unscaledDeltaTime;
                var step = Mathf.Max(1, Mathf.CeilToInt(total / 30f)); // about half a second, whatever the total
                while (coinTick >= 1f / 60f && shownCoins < total)
                {
                    coinTick -= 1f / 60f;
                    shownCoins = Mathf.Min(total, shownCoins + step);
                    var audio = GameAudio.Instance;
                    if (shownCoins % (step * 4) == 0 || shownCoins == total) audio?.Play(audio.Bank.Click, 0.35f, 1.5f + 0.5f * shownCoins / total);
                }
            }

            Ui.Set(coinsEarned, $"+{shownCoins} COINS");
            Ui.Set(coinsWhy, string.Join("   ", run.Coins.Select(award => $"{award.Why} +{award.Coins}")));
            Ui.Set(coinsTotal, $"{Records.Coins} coins");
        }

        /// <summary>A cell per checkpoint: its name, the time there, and how that compares with your best (green ahead, red behind).</summary>
        void ShowSplits(RaceInfo run)
        {
            var count = run.Splits?.Count ?? 0;
            splits.EnableInClassList("splits--compact", count > 6); // six checkpoints and the finish still fit on one row
            while (splitCells.Count < count)
            {
                var name = Ui.Text("", "split-name");
                var at = Ui.Text("", "split-time");
                var delta = Ui.Text("", "split-delta");
                var cell = Ui.El("split", name, at, delta);
                splits.Add(cell);
                splitCells.Add((cell, name, at, delta));
            }

            for (var i = 0; i < splitCells.Count; i++)
            {
                var (cell, name, at, delta) = splitCells[i];
                Ui.Show(cell, i < count);
                if (i >= count) continue;
                Ui.Set(name, i == run.Gates - 1 ? "FINISH" : $"CP{i + 1}");
                Ui.Set(at, Ui.Time(run.Splits[i]));
                var change = run.SplitDeltas != null && i < run.SplitDeltas.Count ? run.SplitDeltas[i] : float.NaN;
                Ui.Set(delta, float.IsNaN(change) ? "" : Ui.Delta(change));
                Ui.SetClass(delta, "good", change <= 0f);
                Ui.SetClass(delta, "bad", change > 0f);
            }
        }

        string HostName()
        {
            foreach (var seat in Game.Seats)
                if (seat.Host && !string.IsNullOrEmpty(seat.Loadout.Name)) return seat.Loadout.Name;
            return "the host";
        }
    }
}
