using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Render;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// Pick your animal: a name, a species, a fur colour and a blower, with the animal in the booth on the left
    /// showing each choice as it's made. Blowers show their character as bars, since they play differently. The host
    /// also dresses the AI crewmates here, from their seats in the lobby.
    /// </summary>
    public sealed class YourAnimalScreen : GameScreen
    {
        readonly List<Button> species = new();
        readonly List<VisualElement> furs = new();
        readonly List<VisualElement> blowers = new();
        Label heading;
        TextField nameField;
        VisualElement nameRow;
        DressingRoom booth;
        int seat = -1;

        public YourAnimalScreen(GameUi ui) : base(ui)
        {
        }

        public override void Build()
        {
            Root.AddToClassList("row");
            Root.style.alignItems = Align.Stretch;
            Root.Add(Ui.Grow()); // the booth shows on the left

            var panel = Ui.El("panel col");
            panel.style.width = 1080;
            heading = Ui.Text("Your animal", "h1");
            panel.Add(heading);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("scroll");
            var body = scroll.contentContainer;

            nameRow = Ui.El("col");
            nameRow.Add(Ui.Text("Name", "small muted"));
            nameField = new TextField { maxLength = Loadout.MaxNameChars, isDelayed = true };
            nameField.AddToClassList("field");
            nameField.RegisterValueChangedCallback(e => Change(look => look.Name = Loadout.CleanName(e.newValue)));
            nameRow.Add(nameField);
            body.Add(nameRow);

            body.Add(Ui.Text("Animal", "small muted"));
            var speciesRow = Ui.El("row");
            speciesRow.style.flexWrap = Wrap.Wrap;
            var catalog = Game.Catalog;
            for (var i = 0; i < catalog.species.Length; i++)
            {
                var index = (byte)i;
                var button = Btn($"animal-species-{i}", catalog.species[i].displayName, () => Change(look =>
                {
                    look.Species = index;
                    look.Fur = (byte)Mathf.Clamp(catalog.species[index].defaultFur, 0, catalog.furColors.Length - 1);
                }), "btn btn--small");
                button.style.marginBottom = 12;
                species.Add(button);
                speciesRow.Add(button);
            }

            body.Add(speciesRow);
            body.Add(Ui.Spacer("spacer-s"));
            body.Add(Ui.Text("Fur", "small muted"));
            var furRow = Ui.El("row");
            furRow.style.flexWrap = Wrap.Wrap;
            for (var i = 0; i < catalog.furColors.Length; i++)
            {
                var index = (byte)i;
                var swatch = new Swatch(catalog.furColors[i], 96f);
                swatch.style.marginRight = 16;
                swatch.style.marginBottom = 16;
                swatch.style.borderTopWidth = swatch.style.borderBottomWidth = swatch.style.borderLeftWidth = swatch.style.borderRightWidth = 6;
                swatch.name = $"animal-fur-{i}";
                System.Action pick = () => Change(look => look.Fur = index);
                swatch.AddManipulator(new Clickable(pick));
                App.Register(swatch.name, swatch, pick);
                furs.Add(swatch);
                furRow.Add(swatch);
            }

            body.Add(furRow);
            body.Add(Ui.Text("Blower", "small muted"));
            for (var i = 0; i < catalog.blowers.Length; i++) body.Add(BlowerCard(catalog.blowers[i], (byte)i));
            panel.Add(scroll);

            panel.Add(Ui.Spacer("spacer-s"));
            panel.Add(Ui.El("row", Ui.Grow(), Btn("animal-done", "Done", Flow.Close, "btn btn--primary")));
            Root.Add(panel);
        }

        VisualElement BlowerCard(BlowerDef blower, byte index)
        {
            var stats = new BlowerStats(blower);
            var card = Ui.El("card");
            card.name = $"animal-blower-{index}";
            var tag = BlowerStats.Tag(blower);
            var title = Ui.El("row", Ui.Text(blower.displayName, "h3"));
            if (tag != null) title.Add(Ui.Text(tag, "chip sun"));
            card.Add(title);
            card.Add(Ui.Text(blower.blurb, "small muted"));
            var bars = Ui.El("row");
            bars.style.marginTop = 10;
            bars.Add(new StatBar("Power", stats.Power));
            bars.Add(new StatBar("Quick start", stats.QuickStart));
            bars.Add(new StatBar("Reach", stats.Reach));
            bars.Add(new StatBar("Burst", stats.Burst));
            card.Add(bars);
            System.Action pick = () => Change(look => look.Blower = index);
            card.AddManipulator(new Clickable(pick));
            App.Register(card.name, card, pick);
            blowers.Add(card);
            return card;
        }

        void Change(System.Action<LoadoutBox> edit)
        {
            var box = new LoadoutBox { Look = Current() };
            edit(box);
            if (seat < 0) Game.DressMe(box.Look);
            else Game.Dress(seat, box.Look);
        }

        /// <summary>A loadout the pickers can change in place.</summary>
        sealed class LoadoutBox
        {
            public Loadout Look;

            public byte Species { set => Look.Species = value; }
            public byte Fur { set => Look.Fur = value; }
            public byte Blower { set => Look.Blower = value; }
            public string Name { set => Look.Name = value; }
        }

        Loadout Current() => seat >= 0 && seat < Game.Seats.Length ? Game.Seats[seat].Loadout : Game.Local.Profile;

        public override void OnShow()
        {
            seat = App.DressingSeat;
            if (seat >= 0 && (seat >= Game.Seats.Length || !Game.CanDress(seat))) seat = -1;
            booth ??= App.Booth;
            booth.gameObject.SetActive(true);
            Ui.Show(nameRow, seat < 0);
            nameField.SetValueWithoutNotify(Current().Name ?? "");
            Ui.Set(heading, seat < 0 ? "Your animal" : $"Dressing P{seat % Mathf.Max(1, Game.CrewCount) + 1} (AI)");
        }

        public override void OnHide()
        {
            App.DressingSeat = -1;
            if (booth != null) booth.gameObject.SetActive(false);
        }

        public override void Refresh()
        {
            var look = Current();
            var crew = Mathf.Max(1, Game.CrewCount);
            var colourSeat = seat >= 0 ? seat : MySeat();
            booth.Show(look, RaftView.PlayerColor(colourSeat >= 0 ? colourSeat % crew : 0));
            for (var i = 0; i < species.Count; i++) Ui.SetClass(species[i], "btn--on", i == look.Species);
            for (var i = 0; i < furs.Count; i++)
            {
                var on = i == look.Fur;
                var edge = on ? new Color(1f, 0.79f, 0.24f) : new Color(1f, 1f, 1f, 0.15f);
                furs[i].style.borderTopColor = furs[i].style.borderBottomColor = furs[i].style.borderLeftColor = furs[i].style.borderRightColor = edge;
            }

            for (var i = 0; i < blowers.Count; i++) Ui.SetClass(blowers[i], "card--selected", i == look.Blower);
        }

        int MySeat()
        {
            for (var i = 0; i < Game.Seats.Length; i++)
                if (Game.Seats[i].Yours) return i;
            return -1;
        }
    }
}
