using System.Collections.Generic;
using System.Linq;
using RushAhead.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// The Field Guide: every obstacle and everything else on the river, each with a photograph, what it does, how to
    /// beat it and the courses it turns up on. A grid of tiles on the left (ones you haven't met yet are dimmed), the page
    /// you picked on the right. Opened from the menu, the lobby, the pause menu, and "What hit me?" on the results, which
    /// opens it at the culprit (<see cref="GameUi.GuideFocus"/>).
    /// </summary>
    public sealed class FieldGuideScreen : GameScreen
    {
        readonly List<(GuideEntry entry, VisualElement tile, Label unmet)> tiles = new();
        Label count;
        ScrollView grid;
        VisualElement picture;
        Label title;
        Label counter;
        Label what;
        Label beat;
        Label where;
        Label unmetNote;
        GuideEntry showing;

        public FieldGuideScreen(GameUi ui) : base(ui)
        {
        }

        public override bool Opaque => true;

        public override void Build()
        {
            Root.AddToClassList("center");
            var panel = Ui.El("panel row guide");

            // Left: the heading and every entry as a tile.
            var left = Ui.El("col");
            left.style.width = 1030;
            count = Ui.Text("", "small muted");
            left.Add(Ui.El("row", Ui.Text("Field guide", "h1"), Ui.Grow(), count));
            left.Add(Ui.Spacer("spacer-s"));
            grid = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            grid.AddToClassList("scroll");
            grid.style.flexGrow = 1;
            Section("On the river", ObstacleGuide.Entries.Where(e => e.Kind == null));
            Section("Obstacles", ObstacleGuide.Entries.Where(e => e.Kind != null));
            left.Add(grid);
            panel.Add(left);
            panel.Add(Ui.Spacer());

            // Right: the page.
            var page = Ui.El("col grow");
            picture = Ui.El("guide-picture");
            page.Add(picture);
            page.Add(Ui.Spacer("spacer-s"));
            title = Ui.Text("", "h2");
            counter = Ui.Text("", "chip guide-counter");
            page.Add(Ui.El("row", title, Ui.Grow(), counter));
            unmetNote = Ui.Text("You haven't met this one on a river yet.", "small muted");
            page.Add(unmetNote);
            page.Add(Ui.Spacer("spacer-s"));
            var text = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            text.AddToClassList("scroll");
            text.style.flexGrow = 1;
            text.Add(Ui.Text("What it does", "h3 muted"));
            what = Ui.Text("", "body");
            text.Add(what);
            text.Add(Ui.Spacer("spacer-s"));
            text.Add(Ui.Text("How to beat it", "h3 muted"));
            beat = Ui.Text("", "body");
            text.Add(beat);
            text.Add(Ui.Spacer("spacer-s"));
            where = Ui.Text("", "small muted");
            text.Add(where);
            page.Add(text);
            page.Add(Ui.El("row", Ui.Grow(), Btn("guide-done", "Done", Flow.Close, "btn btn--primary")));
            panel.Add(page);
            Root.Add(panel);
        }

        void Section(string heading, IEnumerable<GuideEntry> entries)
        {
            var label = Ui.Text(heading, "h3 muted");
            label.style.marginTop = 8;
            label.style.marginBottom = 8;
            grid.Add(label);
            var wrap = Ui.El("row guide-grid");
            foreach (var entry in entries)
            {
                var shot = Ui.El("guide-thumb");
                shot.pickingMode = PickingMode.Ignore;
                GuidePictures.Paint(shot, entry);
                var unmet = Ui.Text("New", "guide-unmet");
                unmet.pickingMode = PickingMode.Ignore;
                shot.Add(unmet);
                var name = Ui.Text(entry.Name, "guide-name");
                name.pickingMode = PickingMode.Ignore;
                var tile = Ui.El("guide-tile", shot, name);
                tile.RegisterCallback<ClickEvent>(_ =>
                {
                    Ui.Click();
                    Show(entry);
                });
                App.Register($"guide-{entry.Id}", tile, () => Show(entry));
                wrap.Add(tile);
                tiles.Add((entry, tile, unmet));
            }

            grid.Add(wrap);
        }

        public override void OnShow()
        {
            var met = 0;
            foreach (var (entry, tile, unmet) in tiles)
            {
                var seen = Records.Seen(entry.Id);
                if (seen) met++;
                Ui.SetClass(tile, "guide-tile--unmet", !seen);
                Ui.Show(unmet, !seen);
            }

            count.text = $"{met} of {tiles.Count} met";
            var focus = ObstacleGuide.ById(App.GuideFocus) ?? showing ?? ObstacleGuide.Entries[0];
            App.GuideFocus = null;
            Show(focus);
            foreach (var (entry, tile, _) in tiles)
                if (entry == focus)
                    grid.schedule.Execute(() => grid.ScrollTo(tile));
        }

        void Show(GuideEntry entry)
        {
            showing = entry;
            foreach (var (e, tile, _) in tiles) Ui.SetClass(tile, "guide-tile--on", e == entry);
            GuidePictures.Paint(picture, entry);
            title.text = entry.Name.ToUpperInvariant();
            counter.text = entry.CounterWord.ToUpperInvariant();
            what.text = entry.What;
            beat.text = entry.Beat;
            Ui.Show(unmetNote, !Records.Seen(entry.Id));
            var on = Game.Courses.Where(c => ObstacleGuide.Appears(entry, c)).Select(c => c.displayName).ToList();
            where.text = on.Count == 0 ? "Not dealt on any course yet."
                : on.Count == Game.Courses.Count ? "Can turn up on every course."
                : $"Can turn up on {string.Join(", ", on)}.";
        }
    }
}
