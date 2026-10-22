using System;
using System.Collections.Generic;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Feel-test overlay: status, touch sticks, name tags, controls help, the Crew panel (play together, seats, pick
    /// your animal) and a live tuning panel. IMGUI keeps it free of scene setup, which suits a prototype; Phase 8 brings
    /// real menus. Slider changes write straight into the <see cref="SimConfig"/> asset; in the editor they persist,
    /// and on a phone "Copy values" puts the tuned config on the clipboard as JSON.
    /// </summary>
    public sealed partial class FeelTestHud : MonoBehaviour
    {
        const float PanelWidth = 340f;

        enum Panel
        {
            None,
            Crew,
            Tune,
        }

        public FeelTestBootstrap Game;

        // Anything that changes the game or the panels' contents waits for Update: changing it mid-OnGUI would change
        // the controls between IMGUI's layout and repaint passes.
        readonly List<Action> later = new();
        Panel open;
        bool help = true;
        int shownVenue;
        Vector2 scroll;
        Rect crewButton, tuneButton, resetButton, helpButton, panel;
        Texture2D circle;
        GUIStyle tagStyle;
        string toast;
        float toastUntil;

        static float Scale => Mathf.Max(1f, Screen.height / 720f);

        /// <summary>Whether a touch starting at this screen position (y-up) is on an on-screen control.</summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / Scale;
            return crewButton.Contains(gui) || tuneButton.Contains(gui) || resetButton.Contains(gui) || helpButton.Contains(gui) ||
                   (open != Panel.None && panel.Contains(gui)) || resultsCard.Contains(gui) || orderBar.Contains(gui) || pickingPoint;
        }

        void Update()
        {
            foreach (var action in later) action();
            later.Clear();
            if (Game.Venue != shownVenue)
            {
                shownVenue = Game.Venue;
                if (shownVenue > 0) help = false; // racing: keep the view clear; the Help button still brings it back
            }

            Game.SetSearching(open == Panel.Crew); // only listen for rooms while someone's looking
            UpdateCrewPanel();
            UpdateOrders();
        }

        void Later(Action action) => later.Add(action);

        public void OpenCrewPanel() => open = Panel.Crew;

        void OnGUI()
        {
            if (Game == null || Game.ViewedBoat == null) return;
            if (circle == null) circle = CircleTexture(64);

            var scale = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            DrawNameTags(scale);
            DrawSticks(scale);
            DrawStatus();
            DrawRace(width, height);
            DrawOrders(width, height);

            tuneButton = new Rect(width - 90f, 10f, 80f, 40f);
            resetButton = new Rect(width - 180f, 10f, 80f, 40f);
            helpButton = new Rect(width - 270f, 10f, 80f, 40f);
            crewButton = new Rect(width - 360f, 10f, 80f, 40f);
            if (GUI.Button(crewButton, open == Panel.Crew ? "Close" : "Crew")) Later(() => open = open == Panel.Crew ? Panel.None : Panel.Crew);
            if (GUI.Button(tuneButton, open == Panel.Tune ? "Close" : "Tune")) Later(() => open = open == Panel.Tune ? Panel.None : Panel.Tune);
            GUI.enabled = Game.CanEditRaft; // only the host can reset the shared raft
            if (GUI.Button(resetButton, Game.Run.Active ? "Restart" : "Reset")) Later(Game.ResetBoat);
            GUI.enabled = true;
            if (GUI.Button(helpButton, help ? "Hide help" : "Help")) Later(() => help = !help);

            if (help && open == Panel.None && !Game.Run.Finished && !Game.HasAiCrew) DrawHelp(height);
            if (open == Panel.Crew) DrawCrew(width, height);
            if (open == Panel.Tune) DrawTuning(width, height);

            if (Time.unscaledTime < toastUntil)
                GUI.Box(new Rect(width * 0.5f - 150f, height - 60f, 300f, 34f), toast);
        }

        void DrawStatus()
        {
            var boat = Game.ViewedBoat;
            GUILayout.BeginArea(new Rect(10f, 10f, 300f, 130f), GUI.skin.box);
            GUILayout.Label($"Speed {boat.Velocity.magnitude:0.0} m/s   Capsizes {boat.CapsizeCount}");

            GUILayout.Label(boat.Capsized ? "CAPSIZED!  recovering..." : "Lean");
            var bar = GUILayoutUtility.GetRect(240f, 14f);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(Color.green, Color.red, boat.TiltDanger);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * boat.TiltDanger, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (Game.Router.DirectNozzleAim) GUILayout.Label("Direct nozzle aim (expert)");
            if (Game.NetStatus.Length > 0) GUILayout.Label(Game.NetStatus);
            GUILayout.EndArea();
        }

        /// <summary>When playing together, each animal carries its player's name in their colour, so everyone knows who's who.</summary>
        void DrawNameTags(float scale)
        {
            var view = Game.RaftView;
            var camera = Camera.main;
            if ((Game.Mode == NetMode.Offline && !Game.HasAiCrew) || view == null || camera == null) return;

            tagStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15 };
            var seats = Game.Seats;
            for (var seat = 0; seat < seats.Length && seat < Game.ViewedBoat.CrewCount; seat++)
            {
                if (!seats[seat].Human && !seats[seat].Ai && !seats[seat].Kept) continue;
                var screen = camera.WorldToScreenPoint(view.TagPosition(seat));
                if (screen.z <= 0f) continue;

                var at = new Vector2(screen.x, Screen.height - screen.y) / scale;
                var text = seats[seat].Yours ? "You" : DisplayName(seats[seat], seat);
                var rect = new Rect(at.x - 80f, at.y - 12f, 160f, 24f);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, tagStyle);
                GUI.color = RaftView.PlayerColor(seat);
                GUI.Label(rect, text, tagStyle);
            }

            GUI.color = Color.white;
        }

        static string DisplayName(RosterSeat seat, int index)
        {
            var name = string.IsNullOrEmpty(seat.Loadout.Name) ? $"P{index + 1}" : seat.Loadout.Name;
            if (seat.Kept) return $"{name} (away)";
            return seat.Ai ? "AI" : name;
        }

        void DrawSticks(float scale)
        {
            foreach (var stick in Game.Router.Sticks)
            {
                if (!stick.Active) continue;
                var radius = (Screen.dpi > 0f ? Screen.dpi * 0.45f : Screen.height * 0.12f) / scale;
                var origin = new Vector2(stick.Origin.x, Screen.height - stick.Origin.y) / scale;
                var knob = new Vector2(stick.Position.x, Screen.height - stick.Position.y) / scale;

                GUI.color = new Color(1f, 1f, 1f, 0.18f);
                GUI.DrawTexture(new Rect(origin.x - radius, origin.y - radius, 2f * radius, 2f * radius), circle);
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                GUI.DrawTexture(new Rect(knob.x - radius * 0.35f, knob.y - radius * 0.35f, radius * 0.7f, radius * 0.7f), circle);
            }

            GUI.color = Color.white;
        }

        void DrawHelp(float height)
        {
            const string text =
                "Push the stick where you want the raft to go (up = away from the camera); your animal blows the other way.\n" +
                "Each player can only blow inside their coloured slice around the raft. Teamwork is the only way forward.\n" +
                "Touch: drag on the left or right half to drive the two main seats. Quick tap = burst.\n" +
                "Keyboard: WASD (+L Shift burst, L Ctrl brace) and Arrows (+R Shift, R Ctrl) = main seats;\n" +
                "   IJKL (+U, O) and Numpad 8456 (+0, .) = the rest. Gamepads: one pad = both sticks; more = one seat each.\n" +
                "Crew: play together over Wi-Fi, choose seats, set the AI crewmates, and pick your animal, colour and blower.";
            GUI.Box(new Rect(10f, height - 130f, 640f, 120f), text, new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true });
        }

        void DrawTuning(float width, float height)
        {
            var c = Game.Config;
            panel = new Rect(width - PanelWidth - 10f, 60f, PanelWidth, height - 70f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            if (!Game.CanEditRaft)
            {
                GUILayout.Label("Joined: the raft is tuned on the host's device.");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            GUI.changed = false;
            GUILayout.Label("Water");
            c.forwardDrag = Slider("Forward drag", c.forwardDrag, 0.5f, 20f);
            c.lateralDrag = Slider($"Lateral drag (keel, {c.lateralDrag / Mathf.Max(0.01f, c.forwardDrag):0.0}x)", c.lateralDrag, 2f, 120f);
            c.angularDrag = Slider("Spin drag", c.angularDrag, 1f, 60f);

            GUILayout.Label("Boat");
            c.mass = Slider("Mass", c.mass, 20f, 150f, "0");
            c.inertiaMultiplier = Slider("Spin inertia x", c.inertiaMultiplier, 0.5f, 3f);
            c.thrustMultiplier = Slider("All thrust x", c.thrustMultiplier, 0.25f, 3f);
            c.nozzleTurnDegPerSec = Slider("Blower swing (deg/s)", c.nozzleTurnDegPerSec, 90f, 1440f, "0");
            c.coneCoupling = Slider("Air pushes others", c.coneCoupling, 0f, 2f);

            GUILayout.Label("Lean and capsize");
            c.tiltStiffness = Slider("Righting stiffness", c.tiltStiffness, 1f, 20f);
            c.pitchStiffnessMultiplier = Slider("Fore-aft stiffness x", c.pitchStiffnessMultiplier, 1f, 6f);
            c.tiltDamping = Slider("Lean damping", c.tiltDamping, 0.2f, 8f);
            c.heelArm = Slider("Thrust heels boat", c.heelArm, 0f, 1.5f);
            c.turnHeelCoupling = Slider("Turns heel boat", c.turnHeelCoupling, 0f, 0.6f);
            c.impactKickScale = Slider("Crash rocks boat", c.impactKickScale, 0f, 0.015f, "0.0000");
            c.capsizeAngle = Slider("Capsize angle (rad)", c.capsizeAngle, 0.2f, 1.2f);

#if UNITY_EDITOR
            if (GUI.changed) UnityEditor.EditorUtility.SetDirty(c);
#endif

            GUILayout.Space(8f);
            Game.Router.DirectNozzleAim = GUILayout.Toggle(Game.Router.DirectNozzleAim, " Direct nozzle aim (expert)");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy values"))
            {
                GUIUtility.systemCopyBuffer = JsonUtility.ToJson(c, true);
                Toast("Config JSON copied to clipboard");
            }

            if (GUILayout.Button("Defaults"))
            {
                var defaults = ScriptableObject.CreateInstance<SimConfig>();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(defaults), c);
                Destroy(defaults);
                Toast("Restored default tuning");
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>A soft-edged white disc for drawing the touch sticks and colour chips.</summary>
        static Texture2D CircleTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            var centre = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var inside = centre - new Vector2(x - centre, y - centre).magnitude; // pixels inside the rim
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(inside) * 255));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static float Slider(string label, float value, float min, float max, string format = "0.00")
        {
            GUILayout.Label($"{label}: {value.ToString(format)}");
            return GUILayout.HorizontalSlider(value, min, max);
        }

        void Toast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 2.5f;
        }
    }
}
