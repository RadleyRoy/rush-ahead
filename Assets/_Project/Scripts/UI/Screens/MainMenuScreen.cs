using System.Collections.Generic;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// Home: the wordmark and your best times on the left over your raft circling on the water; on the right, who
    /// you're playing as and the ways to play. A banner says why the last game ended, when the player didn't choose it.
    /// </summary>
    public sealed class MainMenuScreen : GameScreen
    {
        readonly List<(StarRow stars, Label time, Label name)> bests = new();
        Label starTotal;
        Label playerName;
        Label playerDetail;
        Swatch playerFur;
        VisualElement banner;
        Label bannerText;

        public MainMenuScreen(GameUi ui) : base(ui)
        {
        }

        public override void Build()
        {
            Root.AddToClassList("row");
            Root.style.alignItems = Align.Stretch;

            // Left: the name of the game, then the records, at the bottom.
            var left = Ui.El("col grow");
            left.Add(Ui.Text("Rush Ahead", "wordmark"));
            left.Add(Ui.Text("created by Radley", "byline"));
            left.Add(Ui.Grow());

            var records = Ui.El("panel");
            records.style.alignSelf = Align.FlexStart;
            records.style.minWidth = 640;
            starTotal = Ui.Text("", "body sun");
            starTotal.style.marginLeft = 24;
            records.Add(Ui.El("row", Ui.Text("Your best times", "h3 muted"), Ui.Grow(), starTotal));
            records.Add(Ui.Spacer("spacer-s"));
            foreach (var course in Game.Courses)
            {
                var stars = new StarRow(40f);
                var time = Ui.Text("--", "h3");
                time.style.minWidth = 200;
                time.style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;
                var name = Ui.Text(course.displayName, "h3");
                name.style.flexGrow = 1;
                name.style.marginRight = 32;
                var row = Ui.El("row", name, stars, Ui.Spacer(), time);
                row.style.marginTop = 8;
                records.Add(row);
                bests.Add((stars, time, name));
            }

            left.Add(records);
            Root.Add(left);

            // Right: who you are, and how to play.
            var right = Ui.El("col");
            right.style.width = 760;
            right.style.justifyContent = Justify.Center;

            var me = Ui.El("panel row");
            playerFur = new Swatch(UnityEngine.Color.white, 72f);
            me.Add(playerFur);
            me.Add(Ui.Spacer());
            var who = Ui.El("col grow");
            playerName = Ui.Text("", "h2");
            playerDetail = Ui.Text("", "body muted");
            who.Add(playerName);
            who.Add(playerDetail);
            me.Add(who);
            me.Add(Btn("menu-animal", "Change", Flow.OpenAnimal, "btn btn--small"));
            right.Add(me);
            right.Add(Ui.Spacer("spacer-l"));

            right.Add(Btn("play-solo", "Play solo", Game.BeginSolo, "btn btn--primary"));
            right.Add(Btn("menu-host", "Host a game", Flow.OpenHost));
            right.Add(Btn("menu-join", "Join a game", Flow.OpenJoin));
            right.Add(Btn("menu-settings", "Settings", Flow.OpenSettings));
            Root.Add(right);

            // Why the last game ended, if the player didn't end it.
            bannerText = Ui.Text("", "body");
            bannerText.style.flexGrow = 1;
            banner = Ui.El("banner", bannerText, Btn("banner-ok", "OK", Flow.DismissBanner, "btn btn--small"));
            Root.Add(banner);
        }

        public override void OnShow()
        {
            var courses = Game.Courses;
            var total = Game.TotalStars;
            starTotal.text = $"{total} of {courses.Count * 3} stars";
            for (var i = 0; i < bests.Count && i < courses.Count; i++)
            {
                var open = Game.IsOpen(i + 1);
                var best = Records.BestTime(courses[i], false);
                bests[i].time.text = open ? Ui.Time(best) : $"{courses[i].starsToUnlock} stars to open";
                bests[i].stars.Count = Records.StarsOn(courses[i]);
                Ui.Show(bests[i].stars, open);
                Ui.SetClass(bests[i].name, "muted", !open);
                Ui.SetClass(bests[i].time, "muted", !open);
                Ui.SetClass(bests[i].time, "small", !open);
            }
        }

        public override void Refresh()
        {
            var me = Game.Local.Profile;
            Ui.Set(playerName, string.IsNullOrEmpty(me.Name) ? "Player" : me.Name);
            Ui.Set(playerDetail, $"{Game.Catalog.SpeciesOf(me).displayName} with a {Game.Catalog.BlowerOf(me).displayName}");
            playerFur.Colour = Game.Catalog.FurOf(me);

            Ui.Show(banner, Flow.Banner != null);
            if (Flow.Banner != null) Ui.Set(bannerText, Flow.Banner);
        }
    }
}
