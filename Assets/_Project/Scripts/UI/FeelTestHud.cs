using System.Linq;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Feel-test overlay: status, touch sticks, controls help and a live tuning panel. IMGUI keeps it
    /// free of scene setup, which suits a developer tool. Slider changes write straight into the
    /// <see cref="SimConfig"/> asset; in the editor they persist, and on a phone "Copy values" puts the
    /// tuned config on the clipboard as JSON.
    /// </summary>
    public sealed class FeelTestHud : MonoBehaviour
    {
        const float PanelWidth = 340f;

        public FeelTestBootstrap Game;

        bool tuning;
        bool help = true;
        int pendingCrewCount;
        string joinAddress = "192.168.";
        Vector2 scroll;
        Rect tuneButton, resetButton, helpButton, panel;
        Texture2D circle;
        string toast;
        float toastUntil;

        static float Scale => Mathf.Max(1f, Screen.height / 720f);

        /// <summary>Whether a touch starting at this screen position (y-up) is on an on-screen control.</summary>
        public bool IsOverUi(Vector2 screenPosition)
        {
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y) / Scale;
            return tuneButton.Contains(gui) || resetButton.Contains(gui) || helpButton.Contains(gui) || (tuning && panel.Contains(gui));
        }

        void OnGUI()
        {
            if (Game == null || Game.ViewedBoat == null) return;
            if (circle == null) circle = CircleTexture(64);

            var scale = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            DrawSticks(scale);
            DrawStatus();

            tuneButton = new Rect(width - 90f, 10f, 80f, 40f);
            resetButton = new Rect(width - 180f, 10f, 80f, 40f);
            helpButton = new Rect(width - 270f, 10f, 80f, 40f);
            if (GUI.Button(tuneButton, tuning ? "Close" : "Tune")) tuning = !tuning;
            GUI.enabled = Game.CanEditRaft; // only the host can reset the shared raft
            if (GUI.Button(resetButton, "Reset")) Game.ResetBoat();
            GUI.enabled = true;
            if (GUI.Button(helpButton, help ? "Hide help" : "Help")) help = !help;

            if (help && !tuning) DrawHelp(height);
            if (tuning) DrawTuning(width, height);

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
                "Holding the trigger drains air and blocks recharging. Let go to refill. Tune > Players: 1 to 4.";
            GUI.Box(new Rect(10f, height - 130f, 640f, 120f), text, new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true });
        }

        void DrawTuning(float width, float height)
        {
            var c = Game.Config;
            panel = new Rect(width - PanelWidth - 10f, 60f, PanelWidth, height - 70f);
            GUILayout.BeginArea(panel, GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            DrawPlayTogether();
            if (!Game.CanEditRaft)
            {
                GUILayout.Label("Joined: tuning, players and blowers are set on the host's device.");
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
            var crewCount = Game.Boat.Crew.Length;
            GUILayout.Label($"Players: {crewCount} (each owns {360 / crewCount} degrees)");
            GUILayout.BeginHorizontal();
            for (var count = 1; count <= CrewSectors.MaxCrew; count++)
                if (GUILayout.Toggle(count == crewCount, $" {count}", GUI.skin.button) && count != crewCount)
                    pendingCrewCount = count; // applied in Update: changing it mid-OnGUI would break IMGUI's two-pass layout
            GUILayout.EndHorizontal();

            GUILayout.Label("Blowers (tap to change)");
            foreach (var crew in Game.Boat.Crew)
            {
                if (!GUILayout.Button($"P{crew.Seat + 1} ({SeatName(crew.Seat, crewCount)}): {crew.Blower.displayName}")) continue;
                var blowers = Game.Blowers.ToList();
                crew.Equip(blowers[(blowers.IndexOf(crew.Blower) + 1) % blowers.Count]);
            }

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

        enum NetAction
        {
            None,
            Host,
            Join,
            Leave,
        }

        NetAction pendingNet;

        void DrawPlayTogether()
        {
            GUILayout.Label("Play together (same Wi-Fi or hotspot)");
            if (Game.Mode == NetMode.Offline)
            {
                if (GUILayout.Button("Host a game")) pendingNet = NetAction.Host;
                GUILayout.BeginHorizontal();
                joinAddress = GUILayout.TextField(joinAddress, GUILayout.MinWidth(170f));
                if (GUILayout.Button("Join")) pendingNet = NetAction.Join;
                GUILayout.EndHorizontal();
            }
            else if (GUILayout.Button(Game.Mode == NetMode.Hosting ? "Stop hosting" : "Leave game"))
            {
                pendingNet = NetAction.Leave;
            }

            GUILayout.Space(8f);
        }

        // Network and crew changes are applied here: doing them mid-OnGUI would change the controls between IMGUI's
        // layout and repaint passes.
        void Update()
        {
            switch (pendingNet)
            {
                case NetAction.Host: Game.StartHosting(); break;
                case NetAction.Join: Game.Join(joinAddress); break;
                case NetAction.Leave: Game.LeaveNetwork(); break;
            }

            pendingNet = NetAction.None;
            if (pendingCrewCount == 0) return;
            Game.SetCrewCount(pendingCrewCount);
            pendingCrewCount = 0;
        }

        static string SeatName(int seat, int crewCount) =>
            $"{RaftView.LookFor(seat).ToString().ToLowerInvariant()}, {SliceName(CrewSectors.CentreDeg(seat, crewCount))}";

        static string SliceName(float centreDeg) => Mathf.RoundToInt(centreDeg) switch
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

        /// <summary>A soft-edged white disc for drawing the touch sticks.</summary>
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
