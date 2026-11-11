using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Render;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// Pick your animal: a name, a species, a fur colour and a blower, with the animal in the booth on the left
    /// showing each choice as it's made. Blowers show their character as bars, since they play differently. The shop's
    /// tabs sell hats, raft skins and blower paints for coins. The host also dresses the AI crewmates here, from their
    /// seats in the lobby.
    /// </summary>
    public sealed class YourAnimalScreen : GameScreen
    {
        static readonly string[] TabNames = { "Animal", "Hats", "Raft", "Paint" };

        readonly List<Button> species = new();
        readonly List<VisualElement> furs = new();
        readonly List<VisualElement> blowers = new();
        readonly List<(CosmeticKind kind, int index, VisualElement card, Label status)> shopCards = new();
        readonly VisualElement[] pages = new VisualElement[TabNames.Length];
        Segmented tabs;
        int tab;
        Label coins;
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
            coins = Ui.Text("", "h3 sun");
            coins.name = "animal-coins";
            var top = Ui.El("row", heading, Ui.Grow(), coins);
            top.style.alignItems = Align.Center;
            panel.Add(top);
            tabs = new Segmented(App, "animal-tab", TabNames, ShowTab);
            tabs.style.marginBottom = 16;
            panel.Add(tabs);

            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("scroll");
            var body = pages[0] = Ui.El("col");
            scroll.contentContainer.Add(body);
            for (var kind = 0; kind < 3; kind++) scroll.contentContainer.Add(pages[kind + 1] = ShopPage((CosmeticKind)kind));

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

        void ShowTab(int index)
        {
            tab = index;
            tabs.Selected = index;
            for (var i = 0; i < pages.Length; i++) Ui.Show(pages[i], i == index);
        }

        /// <summary>One of the shop's tabs: every hat, skin or paint, with its price, or whether it's owned or worn.</summary>
        VisualElement ShopPage(CosmeticKind kind)
        {
            var page = Ui.El("col");
            var help = Ui.Text(kind switch
            {
                CosmeticKind.Hat => "Something for your animal's head. Earn coins from your race results: stars, wins and new bests.",
                CosmeticKind.Skin => "A pattern for the raft's tube. A raft wears its captain's: the first player aboard. Team colours stay underneath.",
                _ => "A finish for your blower.",
            }, "small muted");
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.marginBottom = 14;
            page.Add(help);
            var grid = Ui.El("row");
            grid.style.flexWrap = Wrap.Wrap;
            var items = Cosmetics.Of(kind);
            for (var i = 0; i < items.Count; i++)
            {
                var index = i;
                var card = Ui.El("card");
                card.name = $"shop-{kind.ToString().ToLowerInvariant()}-{i}";
                card.style.width = 312;
                card.style.marginRight = 14;
                card.style.marginBottom = 14;
                var title = Ui.El("row");
                title.style.alignItems = Align.Center;
                if (kind != CosmeticKind.Hat)
                {
                    var swatch = new Swatch(kind == CosmeticKind.Paint ? BlowerPaints.Colour(i, new Color(0.85f, 0.85f, 0.85f)) : SkinSwatch(i), 40f);
                    swatch.style.marginRight = 14;
                    title.Add(swatch);
                }

                var name = Ui.Text(items[i].Name, "h3");
                name.style.flexShrink = 1;
                title.Add(name);
                card.Add(title);
                var status = Ui.Text("", "small muted");
                card.Add(status);
                System.Action pick = () => Pick(kind, index);
                card.AddManipulator(new Clickable(pick));
                App.Register(card.name, card, pick);
                shopCards.Add((kind, i, card, status));
                grid.Add(card);
            }

            page.Add(grid);
            return page;
        }

        static Color SkinSwatch(int skin) => Cosmetics.Skins[skin].Id switch
        {
            "stripes" => new Color(0.95f, 0.95f, 0.95f),
            "dots" => new Color(0.98f, 0.9f, 0.9f),
            "checker" => new Color(0.3f, 0.3f, 0.32f),
            "stars" => new Color(1f, 0.95f, 0.55f),
            "camo" => new Color(0.38f, 0.42f, 0.22f),
            "shark" => new Color(0.35f, 0.05f, 0.08f),
            "flames" => new Color(1f, 0.5f, 0.1f),
            "gold" => new Color(1f, 0.78f, 0.28f),
            _ => new Color(1f, 0.72f, 0.56f),
        };

        /// <summary>Wears it if it's owned; otherwise offers to buy it (or says how far off it is).</summary>
        void Pick(CosmeticKind kind, int index)
        {
            if (Records.Owns(kind, index))
            {
                Change(box => box.Look = box.Look.Wearing(kind, (byte)index));
                return;
            }

            var item = Cosmetics.Of(kind)[index];
            if (Records.Coins < item.Price)
            {
                App.Confirm($"{item.Name}: {item.Price} coins", $"You have {Records.Coins}. Race for more: every star, win and new best earns coins.", "OK", () => { });
                return;
            }

            App.Confirm($"Buy the {item.Name}?", $"{item.Price} coins. You have {Records.Coins}.", "Buy", () =>
            {
                if (!Records.Buy(kind, index)) return;
                var audio = GameAudio.Instance;
                audio?.Play(audio.Bank.Jingle, 0.7f);
                Change(box => box.Look = box.Look.Wearing(kind, (byte)index));
            });
        }

        VisualElement BlowerCard(BlowerDef blower, byte index)
        {
            var stats = new BlowerStats(blower);
            var card = Ui.El("card");
            card.name = $"animal-blower-{index}";
            var tag = BlowerStats.Tag(blower);
            var title = Ui.El("row", Ui.Text(blower.displayName, "h3"));
            if (tag != null) title.Add(Ui.Text(tag, "chip sun"));
            var words = Ui.El("col grow");
            words.Add(title);
            words.Add(Ui.Text(blower.blurb, "small muted"));
            var bars = Ui.El("row");
            bars.style.marginTop = 10;
            bars.Add(new StatBar("Power", stats.Power));
            bars.Add(new StatBar("Quick start", stats.QuickStart));
            bars.Add(new StatBar("Reach", stats.Reach));
            bars.Add(new StatBar("Burst", stats.Burst));
            words.Add(bars);
            var icon = new BlowerIcon(BlowerModels.KindOf(blower), blower.color, 104f);
            icon.style.marginRight = 22;
            var row = Ui.El("row", icon, words);
            row.style.alignItems = Align.Center;
            card.Add(row);
            System.Action pick = () => Change(look => look.Blower = index);
            card.AddManipulator(new Clickable(pick));
            App.Register(card.name, card, pick);
            blowers.Add(card);
            return card;
        }

        void Change(System.Action<LoadoutBox> edit)
        {
            GameAudio.Instance?.Play(GameAudio.Instance.Bank.Click, 0.5f);
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
            ShowTab(tab);
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
                var edge = on ? new Color(1f, 0.416f, 0.169f) : new Color(1f, 1f, 1f, 0.15f);
                furs[i].style.borderTopColor = furs[i].style.borderBottomColor = furs[i].style.borderLeftColor = furs[i].style.borderRightColor = edge;
            }

            for (var i = 0; i < blowers.Count; i++) Ui.SetClass(blowers[i], "card--selected", i == look.Blower);
            Ui.Set(coins, $"{Records.Coins} COINS");
            foreach (var (kind, index, card, status) in shopCards)
            {
                var worn = look.Worn(kind) == index;
                var owned = Records.Owns(kind, index);
                Ui.SetClass(card, "card--selected", worn);
                Ui.Set(status, worn ? "WEARING" : owned ? "OWNED" : $"{Cosmetics.Of(kind)[index].Price} COINS");
                Ui.SetClass(status, "good", worn);
            }
        }

        int MySeat()
        {
            for (var i = 0; i < Game.Seats.Length; i++)
                if (Game.Seats[i].Yours) return i;
            return -1;
        }
    }
}
