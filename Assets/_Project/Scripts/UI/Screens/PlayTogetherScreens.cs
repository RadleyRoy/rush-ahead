using System.Collections.Generic;
using RushAhead.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>Hosting: on this Wi-Fi (or a phone's hotspot, no internet needed), or online with a code to share.</summary>
    public sealed class HostScreen : GameScreen
    {
        Button wifi;
        Button online;
        Label onlineNote;
        VisualElement busy;
        Label error;
        bool attempted;

        public HostScreen(GameUi ui) : base(ui)
        {
        }

        public override void Build()
        {
            Root.Add(Ui.Text("Host a game", "h1"));
            Root.Add(Ui.Spacer("spacer-l"));

            var cards = Ui.El("row");
            cards.style.alignItems = Align.Stretch;
            wifi = Btn("host-wifi", "Host on Wi-Fi", () =>
            {
                attempted = true;
                Game.StartHosting();
            }, "btn btn--primary");
            cards.Add(Card("Wi-Fi or hotspot", "Friends on the same Wi-Fi, or on your phone's hotspot, find your game nearby. No internet needed.", wifi, null));

            online = Btn("host-online", "Host online", () =>
            {
                attempted = true;
                Game.StartHostingOnline();
            }, "btn btn--primary");
            onlineNote = Ui.Text("", "small muted");
            cards.Add(Ui.Spacer("spacer-l"));
            cards.Add(Card("Online", "Friends anywhere join with a six-letter code you share.", online, onlineNote));
            Root.Add(cards);

            busy = Ui.El("row", Ui.Text("Setting up your online game...", "body"), Ui.Spacer(),
                Btn("host-cancel", "Cancel", Game.CancelGoingOnline, "btn btn--small"));
            busy.style.marginTop = 32;
            Root.Add(busy);
            error = Ui.Text("", "body bad");
            error.style.marginTop = 24;
            Root.Add(error);

            Root.Add(Ui.Grow());
            Root.Add(Ui.El("row", Btn("host-back", "Back", () => App.Back(), "btn btn--small")));
        }

        static VisualElement Card(string title, string body, Button action, Label note)
        {
            var card = Ui.El("panel col");
            card.style.width = 760;
            card.Add(Ui.Text(title, "h2"));
            card.Add(Ui.Spacer("spacer-s"));
            var text = Ui.Text(body, "body muted");
            text.style.flexGrow = 1;
            card.Add(text);
            card.Add(Ui.Spacer());
            card.Add(action);
            if (note != null) card.Add(note);
            return card;
        }

        public override void OnShow() => attempted = false;

        public override void Refresh()
        {
            var going = Game.GoingOnline;
            Ui.Enable(wifi, !going);
            Ui.Enable(online, !going && Game.OnlineAvailable);
            Ui.Show(onlineNote, !Game.OnlineAvailable);
            Ui.Set(onlineNote, "Online play isn't set up in this build.");
            Ui.Show(busy, going);
            var message = attempted && !going ? Game.LastError : null;
            Ui.Show(error, message != null);
            if (message != null) Ui.Set(error, message);
        }
    }

    /// <summary>Joining: games heard on this Wi-Fi, a code for an online game, or a host's address.</summary>
    public sealed class JoinScreen : GameScreen
    {
        readonly List<string> shownRooms = new();
        VisualElement rooms;
        Label roomsNote;
        TextField code;
        Button joinCode;
        TextField address;
        VisualElement addressRow;
        VisualElement busy;
        Label error;
        bool attempted;

        public JoinScreen(GameUi ui) : base(ui)
        {
        }

        public override void Build()
        {
            Root.Add(Ui.Text("Join a game", "h1"));
            Root.Add(Ui.Spacer());

            var columns = Ui.El("row grow");
            columns.style.alignItems = Align.Stretch;

            var nearby = Ui.El("panel col grow");
            nearby.Add(Ui.Text("Games nearby", "h2"));
            nearby.Add(Ui.Spacer("spacer-s"));
            roomsNote = Ui.Text("", "body muted");
            nearby.Add(roomsNote);
            var scroll = new ScrollView();
            scroll.AddToClassList("scroll");
            rooms = scroll.contentContainer;
            nearby.Add(scroll);
            columns.Add(nearby);
            columns.Add(Ui.Spacer("spacer-l"));

            var byCode = Ui.El("panel col");
            byCode.style.width = 700;
            byCode.Add(Ui.Text("Join with a code", "h2"));
            byCode.Add(Ui.Spacer("spacer-s"));
            code = new TextField { maxLength = 6, isDelayed = false };
            code.AddToClassList("field");
            code.RegisterValueChangedCallback(e =>
            {
                var clean = e.newValue.ToUpperInvariant();
                if (clean != e.newValue) code.SetValueWithoutNotify(clean);
            });
            byCode.Add(code);
            joinCode = Btn("join-code", "Join", () =>
            {
                attempted = true;
                Game.JoinOnline(code.value);
            }, "btn btn--primary");
            byCode.Add(joinCode);
            byCode.Add(Ui.Grow());

            addressRow = Ui.El("col");
            address = new TextField { maxLength = 64 };
            address.AddToClassList("field");
            addressRow.Add(Ui.Text("Host's address", "small muted"));
            addressRow.Add(address);
            addressRow.Add(Btn("join-address", "Join by address", () =>
            {
                attempted = true;
                Game.Join(address.value);
            }, "btn btn--small"));
            Ui.Show(addressRow, false);
            byCode.Add(Btn("join-more", "Join by address instead", () => Ui.Show(addressRow, true), "btn btn--small"));
            byCode.Add(addressRow);
            columns.Add(byCode);
            Root.Add(columns);

            busy = Ui.El("row", Ui.Text("Finding the online game...", "body"), Ui.Spacer(),
                Btn("join-cancel", "Cancel", Game.CancelGoingOnline, "btn btn--small"));
            Root.Add(busy);
            error = Ui.Text("", "body bad");
            Root.Add(error);
            Root.Add(Ui.Spacer());
            Root.Add(Ui.El("row", Btn("join-back", "Back", () => App.Back(), "btn btn--small")));
        }

        public override void OnShow()
        {
            attempted = false;
            Game.SetSearching(true);
        }

        public override void OnHide() => Game.SetSearching(false);

        public override void Refresh()
        {
            Game.SetSearching(true);
            var going = Game.GoingOnline;
            Ui.Enable(joinCode, !going && Game.OnlineAvailable && code.value.Trim().Length >= 4);
            Ui.Show(busy, going);

            var message = Flow.JoinError ?? (attempted && !going ? Game.LastError : null);
            Ui.Show(error, message != null);
            if (message != null) Ui.Set(error, message);

            var found = Game.RoomsNearby;
            Ui.Set(roomsNote, Game.SearchError ?? (found.Count == 0 ? "Looking for games on this Wi-Fi..." : ""));
            if (SameRooms(found)) return;
            shownRooms.Clear();
            rooms.Clear();
            foreach (var room in found)
            {
                shownRooms.Add(Describe(room));
                rooms.Add(RoomRow(room));
            }
        }

        bool SameRooms(IReadOnlyList<DiscoveredRoom> found)
        {
            if (found.Count != shownRooms.Count) return false;
            for (var i = 0; i < found.Count; i++)
                if (Describe(found[i]) != shownRooms[i]) return false;
            return true;
        }

        static string Describe(DiscoveredRoom room) =>
            $"{room.Address}|{room.Advert.GamePort}|{room.Label}|{room.Advert.Players}|{room.Advert.Seats}|{room.Advert.Phase}|{room.Advert.Compatible}";

        VisualElement RoomRow(DiscoveredRoom room)
        {
            var advert = room.Advert;
            var info = Ui.El("col grow",
                Ui.Text(room.Label, "h3"),
                Ui.Text(!advert.Compatible ? "Needs the same version of Rush Ahead"
                    : $"{advert.Players} of {advert.Seats} aboard, {PhaseText(advert.Phase)}", "small muted"));
            var join = Ui.Btn(advert.Full ? "Watch" : "Join", () =>
            {
                attempted = true;
                Game.Join(room);
            }, "btn btn--small btn--primary");
            Ui.Enable(join, advert.Compatible);
            return Ui.El("card row", info, join);
        }

        static string PhaseText(RoomPhase phase) => phase switch
        {
            RoomPhase.Racing => "racing",
            RoomPhase.Results => "looking at the results",
            _ => "in the lobby",
        };
    }

    /// <summary>Waiting to hear from the host. Gives up after a while rather than leaving the player staring.</summary>
    public sealed class ConnectingScreen : GameScreen
    {
        const float GiveUpSeconds = 12f;
        float waited;
        Label status;

        public ConnectingScreen(GameUi ui) : base(ui)
        {
        }

        public override void Build()
        {
            Root.AddToClassList("center");
            var panel = Ui.El("panel col center");
            panel.style.width = 980;
            panel.Add(Ui.Text("Joining the game...", "h1"));
            panel.Add(Ui.Spacer());
            status = Ui.Text("", "body muted");
            panel.Add(status);
            panel.Add(Ui.Spacer("spacer-l"));
            panel.Add(Btn("connecting-cancel", "Cancel", () => Game.EndSession(), "btn btn--small"));
            Root.Add(panel);
        }

        public override void OnShow() => waited = 0f;

        public override void Refresh()
        {
            waited += Time.unscaledDeltaTime;
            Ui.Set(status, Game.NetStatus);
            if (waited > GiveUpSeconds) Game.EndSession("Couldn't connect to that game. Check you're on the same Wi-Fi, or try the code again.");
        }
    }
}
