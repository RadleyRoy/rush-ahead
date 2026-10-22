using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>The Crew panel: host or join a game, see who sits where, move seats, and pick an animal, fur and blower.</summary>
    public sealed partial class FeelTestHud
    {
        const float CrewPanelWidth = 390f;
        const float NameSettleSeconds = 0.6f; // send a typed name once the player pauses, not on every keystroke
        const int SwatchesPerRow = 6;

        string joinAddress = "192.168.";
        string joinCode = "";
        GUIStyle codeStyle;
        int dressSeat = -1; // the seat whose look is being edited; -1 means the player's own
        string nameDraft;
        float nameEditedAt = -1f;
        Vector2 crewScroll;
        GUIStyle header, small, centred;

        CrewCatalog Catalog => Game.Catalog;

        void UpdateCrewPanel()
        {
            if (nameEditedAt < 0f || Time.unscaledTime - nameEditedAt < NameSettleSeconds) return;
            nameEditedAt = -1f;
            var seat = MySeat(Game.Seats);
            if (seat < 0) return;
            var look = Game.Local.Profile;
            look.Name = Loadout.CleanName(nameDraft);
            Game.Dress(seat, look);
        }

        void DrawCrew(float width, float height)
        {
            header ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            centred ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

            panel = new Rect(width - CrewPanelWidth - 10f, 60f, CrewPanelWidth, height - 70f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            crewScroll = GUILayout.BeginScrollView(crewScroll);
            DrawPlayTogether();
            GUILayout.Space(10f);
            DrawCourses();
            GUILayout.Space(10f);
            DrawSeats();
            GUILayout.Space(10f);
            DrawDresser();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawPlayTogether()
        {
            GUILayout.Label("Play together", header);
            switch (Game.Mode)
            {
                case NetMode.Offline:
                    DrawOnline();
                    GUILayout.Space(8f);
                    GUILayout.Label("On the same Wi-Fi, or one phone's hotspot (no internet needed)", small);
                    GUI.enabled = !Game.GoingOnline;
                    if (GUILayout.Button("Host on Wi-Fi", GUILayout.Height(36f))) Later(() => Game.StartHosting());
                    GUI.enabled = true;

                    GUILayout.Label("Games nearby");
                    var rooms = Game.RoomsNearby;
                    if (Game.SearchError != null) GUILayout.Label(Game.SearchError, small);
                    else if (rooms.Count == 0) GUILayout.Label("Looking...", small);
                    foreach (var room in rooms)
                    {
                        var advert = room.Advert;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(advert.Compatible ? $"{room.Label}  {advert.Players}/{advert.Seats} aboard" : $"{room.Label}: needs the same version",
                            GUILayout.MinWidth(230f));
                        GUI.enabled = advert.Compatible;
                        if (GUILayout.Button(advert.Full ? "Watch" : "Join", GUILayout.Width(70f))) Later(() => Game.Join(room));
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.Label("Not listed? Join by address");
                    GUILayout.BeginHorizontal();
                    joinAddress = GUILayout.TextField(joinAddress, GUILayout.MinWidth(220f));
                    if (GUILayout.Button("Join", GUILayout.Width(70f))) Later(() => Game.Join(joinAddress));
                    GUILayout.EndHorizontal();
                    break;

                case NetMode.Hosting when Game.OnlineCode != null:
                    codeStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    GUILayout.Label("Online game. Friends join anywhere with this code:", small);
                    GUILayout.Label(Game.OnlineCode, codeStyle, GUILayout.Height(48f));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Copy code", GUILayout.Height(36f)))
                    {
                        GUIUtility.systemCopyBuffer = Game.OnlineCode;
                        Toast("Join code copied");
                    }

                    if (GUILayout.Button("Stop hosting", GUILayout.Height(36f))) Later(Game.LeaveNetwork);
                    GUILayout.EndHorizontal();
                    break;

                case NetMode.Hosting:
                    GUILayout.Label("Others find this raft under Games nearby, or join this device's address (top left).", small);
                    if (GUILayout.Button("Stop hosting", GUILayout.Height(36f))) Later(Game.LeaveNetwork);
                    break;

                case NetMode.Joined:
                    if (GUILayout.Button("Leave game", GUILayout.Height(36f))) Later(Game.LeaveNetwork);
                    break;
            }
        }

        /// <summary>Online play: host and get a code, or type a friend's code. Works over mobile data or any Wi-Fi.</summary>
        void DrawOnline()
        {
            GUILayout.Label("Online, anywhere (join with a code)", small);
            if (!Game.OnlineAvailable)
            {
                GUILayout.Label("Not set up in this build yet: it needs linking to Unity's online services.", small);
                return;
            }

            GUI.enabled = !Game.GoingOnline;
            if (GUILayout.Button(Game.GoingOnline ? "Connecting..." : "Host online", GUILayout.Height(36f))) Later(Game.StartHostingOnline);
            GUILayout.BeginHorizontal();
            joinCode = GUILayout.TextField(joinCode, 12, GUILayout.MinWidth(220f));
            if (GUILayout.Button("Join", GUILayout.Width(70f))) Later(() => Game.JoinOnline(joinCode));
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        void DrawSeats()
        {
            var seats = Game.Seats;
            GUILayout.Label("Raft", header);
            if (Game.CanEditRaft)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Players", GUILayout.Width(60f));
                for (var count = 1; count <= CrewSectors.MaxCrew; count++)
                {
                    var chosen = count;
                    if (GUILayout.Toggle(count == seats.Length, $" {count}", GUI.skin.button) && count != seats.Length) Later(() => Game.SetCrewCount(chosen));
                }

                GUILayout.EndHorizontal();
            }

            var dressed = DressedSeat(seats);
            for (var seat = 0; seat < seats.Length; seat++)
            {
                var s = seats[seat];
                var pick = seat;
                GUILayout.BeginHorizontal();

                var chip = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f), GUILayout.Height(16f));
                GUI.color = RaftView.PlayerColor(seat);
                GUI.DrawTexture(chip, circle);
                GUI.color = Color.white;

                var who = s.Kept ? $"kept for {s.Loadout.Name}, AI holding it" : s.Ai ? "AI crewmate" : !s.Human ? "spare"
                    : s.Yours ? $"{DisplayName(s, seat)} (you)" : s.Host ? $"{DisplayName(s, seat)} (host)" : DisplayName(s, seat);
                GUILayout.Label($"P{seat + 1}  {who}, {SliceName(seat, seats.Length)}\n" +
                                $"{Catalog.SpeciesOf(s.Loadout).displayName} with the {Catalog.BlowerOf(s.Loadout).displayName}", small, GUILayout.MinWidth(200f));

                if (Game.CanDress(seat) && GUILayout.Toggle(dressed == seat, "Dress", GUI.skin.button, GUILayout.Width(56f)) && dressed != seat)
                    Later(() => { dressSeat = pick; nameDraft = null; });
                if (!s.Human && GUILayout.Button("Sit here", GUILayout.Width(64f))) Later(() => Game.TakeSeat(pick));
                GUILayout.EndHorizontal();
            }

            if (seats.Length > 0 && MySeat(seats) < 0) GUILayout.Label("You're watching. Sit in a free seat to join in.", small);
            DrawAiSetting();
        }

        /// <summary>Who fills the seats no player has: AI crewmates at a skill, or (off) this device's other controls.</summary>
        void DrawAiSetting()
        {
            if (!Game.CanEditRaft) return;
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("AI crew", GUILayout.Width(60f));
            var profiles = Game.AiProfiles;
            for (var setting = -1; setting < 3; setting++)
            {
                var label = setting < 0 ? "Off" : profiles != null && setting < profiles.Count && profiles[setting] != null ? profiles[setting].displayName : $"{(Content.AiSkill)setting}";
                var chosen = setting;
                if (GUILayout.Toggle(Game.AiSetting == setting, label, GUI.skin.button) && Game.AiSetting != setting) Later(() => Game.SetAiSetting(chosen));
            }

            GUILayout.EndHorizontal();
            var blurb = Game.AiSetting < 0 ? "Spare seats are yours: drive them with the second stick, the keyboard or more pads."
                : profiles != null && Game.AiSetting < profiles.Count && profiles[Game.AiSetting] != null ? profiles[Game.AiSetting].blurb : "";
            GUILayout.Label(blurb, small);
        }

        void DrawDresser()
        {
            var seats = Game.Seats;
            var seat = DressedSeat(seats);
            if (seat < 0) return;

            var yours = seats[seat].Yours;
            var look = yours ? Game.Local.Profile : Game.Local.Spares[seat];
            GUILayout.Label(yours ? "Your animal" : $"P{seat + 1}'s animal (spare seat)", header);

            if (yours)
            {
                nameDraft ??= look.Name;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name", GUILayout.Width(60f));
                var typed = GUILayout.TextField(nameDraft, Loadout.MaxNameChars, GUILayout.MinWidth(220f));
                if (typed != nameDraft)
                {
                    nameDraft = typed;
                    nameEditedAt = Time.unscaledTime;
                }

                GUILayout.EndHorizontal();
            }

            var changed = false;
            look.Species = Stepper("Animal", look.Species, Catalog.species.Length, Catalog.SpeciesOf(look).displayName, ref changed);

            GUILayout.Label("Fur");
            for (var row = 0; row * SwatchesPerRow < Catalog.furColors.Length; row++)
            {
                GUILayout.BeginHorizontal();
                for (var i = row * SwatchesPerRow; i < Mathf.Min(Catalog.furColors.Length, (row + 1) * SwatchesPerRow); i++)
                {
                    var rect = GUILayoutUtility.GetRect(44f, 32f, GUILayout.Width(44f), GUILayout.Height(32f));
                    if (GUI.Button(rect, GUIContent.none) && look.Fur != i)
                    {
                        look.Fur = (byte)i;
                        changed = true;
                    }

                    var inset = 4f;
                    if (i == look.Fur)
                    {
                        GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), Texture2D.whiteTexture); // a white frame marks the choice
                        inset = 5f;
                    }

                    GUI.color = Catalog.furColors[i];
                    GUI.DrawTexture(new Rect(rect.x + inset, rect.y + inset, rect.width - 2f * inset, rect.height - 2f * inset), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                GUILayout.EndHorizontal();
            }

            look.Blower = Stepper("Blower", look.Blower, Catalog.blowers.Length, Catalog.BlowerOf(look).displayName, ref changed);
            GUILayout.Label(Catalog.BlowerOf(look).blurb, small);

            if (!changed) return;
            if (yours && nameDraft != null) look.Name = Loadout.CleanName(nameDraft);
            var chosen = look;
            Later(() => Game.Dress(seat, chosen));
        }

        byte Stepper(string label, byte value, int count, string shown, ref bool changed)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60f));
            var next = (int)value;
            if (GUILayout.Button("<", GUILayout.Width(44f), GUILayout.Height(30f))) next = (value + count - 1) % count;
            GUILayout.Label(shown, centred, GUILayout.MinWidth(150f), GUILayout.Height(30f));
            if (GUILayout.Button(">", GUILayout.Width(44f), GUILayout.Height(30f))) next = (value + 1) % count;
            GUILayout.EndHorizontal();

            changed |= next != value;
            return (byte)next;
        }

        /// <summary>The seat being dressed: the one the player picked, if they still may, otherwise their own.</summary>
        int DressedSeat(RosterSeat[] seats) => Game.CanDress(dressSeat) ? dressSeat : MySeat(seats);

        static int MySeat(RosterSeat[] seats)
        {
            for (var seat = 0; seat < seats.Length; seat++)
                if (seats[seat].Yours) return seat;
            return -1;
        }

        static string SliceName(int seat, int crewCount) => Mathf.RoundToInt(CrewSectors.CentreDeg(seat, crewCount)) switch
        {
            0 => "front",
            45 => "front-left",
            -45 => "front-right",
            90 => "left",
            -90 => "right",
            120 or 135 => "rear-left",
            -120 or -135 => "rear-right",
            _ => "all round",
        };
    }
}
