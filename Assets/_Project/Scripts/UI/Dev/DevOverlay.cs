using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Developer tools: a readout (frame rate, speed, lean, network) and the live tuning sliders, which write straight
    /// into the <see cref="SimConfig"/> asset (in the editor they persist; on a phone "Copy values" puts the tuned config
    /// on the clipboard as JSON). Turned on in Settings, or with <c>-rushahead-dev</c>. IMGUI keeps it out of the game's
    /// own screens.
    /// </summary>
    public sealed class DevOverlay : MonoBehaviour
    {
        const float PanelWidth = 340f;

        public GameController Game;
        bool tuning;
        Vector2 scroll;
        float fps = 60f;
        string toast;
        float toastUntil;

        static float Scale => Mathf.Max(1f, Screen.height / 720f);

        void Update() => fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.001f, Time.unscaledDeltaTime), 0.05f);

        void OnGUI()
        {
            if (Game == null) return;
            var scale = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var height = Screen.height / scale;

            var boat = Game.ViewedBoat;
            GUILayout.BeginArea(new Rect(10f, height - 150f, 320f, 140f), GUI.skin.box);
            GUILayout.Label($"{fps:0} fps   {Game.Session} {Game.Phase}");
            if (boat != null)
            {
                GUILayout.Label($"Speed {boat.Velocity.magnitude:0.0} m/s   Capsizes {boat.CapsizeCount}   Lean {boat.TiltDanger:0.00}");
            }

            if (Game.NetStatus.Length > 0) GUILayout.Label(Game.NetStatus);
            if (GUILayout.Button(tuning ? "Close tuning" : "Tune")) tuning = !tuning;
            GUILayout.EndArea();

            if (tuning) DrawTuning(width, height);
            if (Time.unscaledTime < toastUntil) GUI.Box(new Rect(width * 0.5f - 150f, height - 60f, 300f, 34f), toast);
        }

        void DrawTuning(float width, float height)
        {
            var c = Game.Config;
            GUILayout.BeginArea(new Rect(width - PanelWidth - 10f, 60f, PanelWidth, height - 70f), GUI.skin.box);
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
