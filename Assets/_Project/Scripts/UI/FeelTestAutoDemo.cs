using System;
using System.IO;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Scripted run for checking the feel test without a human: launch a player build with
    /// <c>-rushahead-demo &lt;output folder&gt;</c> and it drives the boat, saves screenshots, logs state and quits.
    /// Add <c>-rushahead-crew &lt;1-4&gt;</c> to run it with a smaller crew.
    /// </summary>
    public sealed class FeelTestAutoDemo : MonoBehaviour
    {
        const string Flag = "-rushahead-demo";
        const string CrewFlag = "-rushahead-crew";
        const string SecondsFlag = "-rushahead-demo-seconds";

        static readonly (float time, string name)[] Shots =
        {
            (2.5f, "1-cruising"), (5.0f, "2-turning"), (6.3f, "3-burst"), (7.4f, "4-heeling"), (9.6f, "5-after"), (10.3f, "6-crew"),
        };

        const float OpenCrewPanelAt = 9.8f; // so the last shot shows the Crew panel

        FeelTestBootstrap game;
        string outputFolder;
        float elapsed;
        float duration = 10.8f;
        int nextShot;
        bool burstFired;
        bool crewPanelOpened;

        public static void StartIfRequested(FeelTestBootstrap game)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, Flag);
            if (index < 0 || index + 1 >= args.Length) return;

            var demo = game.gameObject.AddComponent<FeelTestAutoDemo>();
            demo.game = game;
            demo.outputFolder = args[index + 1];
            Directory.CreateDirectory(demo.outputFolder);
            game.Router.Scripted = true;
            game.AllowSearching = false; // the Crew panel opens near the end; don't make the desktop firewall ask about it

            var crewIndex = Array.IndexOf(args, CrewFlag);
            if (crewIndex >= 0 && crewIndex + 1 < args.Length && int.TryParse(args[crewIndex + 1], out var crew) && crew >= 1 && crew <= CrewSectors.MaxCrew)
                game.SetCrewCount(crew);

            var secondsIndex = Array.IndexOf(args, SecondsFlag);
            if (secondsIndex >= 0 && secondsIndex + 1 < args.Length && float.TryParse(args[secondsIndex + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                demo.duration = seconds;
            Debug.Log($"[RushAhead] Auto demo writing to {demo.outputFolder}");
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            Drive(game.Router, elapsed);
            if (!crewPanelOpened && elapsed >= OpenCrewPanelAt)
            {
                crewPanelOpened = true;
                game.GetComponent<FeelTestHud>().OpenCrewPanel();
            }

            var boat = game.ViewedBoat;
            if (nextShot < Shots.Length && elapsed >= Shots[nextShot].time)
            {
                var (_, name) = Shots[nextShot++];
                ScreenCapture.CaptureScreenshot(Path.Combine(outputFolder, $"{name}.png"));
                var air = new string[boat.CrewCount];
                for (var i = 0; i < air.Length; i++) air[i] = boat.CrewAt(i).AirFraction.ToString("0.00");
                Debug.Log($"[RushAhead] {name}: t={elapsed:0.0}s mode={game.Mode} pos={boat.RenderPosition} heading={boat.RenderAngleRad * Mathf.Rad2Deg:0}deg " +
                          $"speed={boat.Velocity.magnitude:0.00} lean={boat.Tilt.magnitude:0.000} capsized={boat.Capsized} capsizes={boat.CapsizeCount} " +
                          $"air=[{string.Join(",", air)}] {game.NetStatus} crew=[{CrewSummary()}]");
            }

            // A machine-readable timeline for checking network sync: the host logs the real raft at each sim tick,
            // a joined player logs what it drew at its playback tick. Matching ticks across logs gives the error.
            if (game.Mode == NetMode.Hosting)
                Debug.Log(FormattableString.Invariant($"[RushAhead.net] host tick={game.WorldTick} x={game.Boat.Body.Position.x:0.0000} y={game.Boat.Body.Position.y:0.0000}"));
            else if (game.Mode == NetMode.Joined && boat.CrewCount > 0)
                Debug.Log(FormattableString.Invariant($"[RushAhead.net] view tick={game.PlaybackTick:0.000} x={boat.RenderPosition.x:0.0000} y={boat.RenderPosition.y:0.0000}"));

            if (elapsed > duration) Application.Quit();
        }

        /// <summary>Who sits where as this device sees it, e.g. "P1 Skipper(you) Bear/fur0/Leaf Blower", for checking that rosters agree.</summary>
        string CrewSummary()
        {
            var seats = game.Seats;
            var parts = new string[seats.Length];
            for (var i = 0; i < seats.Length; i++)
            {
                var s = seats[i];
                var who = !s.Human ? "spare" : s.Loadout.Name + (s.Yours ? "(you)" : s.Host ? "(host)" : "");
                parts[i] = $"P{i + 1} {who} {game.Catalog.SpeciesOf(s.Loadout).displayName}/fur{s.Loadout.Fur}/{game.Catalog.BlowerOf(s.Loadout).displayName}";
            }

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Sticks are camera-relative, exactly as a player's would be: up pushes away from the camera. The two main
        /// seats are whichever the left and right touch sticks drive at the current crew size.
        /// </summary>
        void Drive(CrewInputRouter router, float t)
        {
            var ahead = Vector2.up;
            var right = Vector2.right;
            var crew = game.ViewedBoat.CrewCount;
            var left = router.SeatForSlot(0);
            var other = router.SeatForSlot(1);
            for (var seat = 0; seat < crew; seat++) router.SetScripted(seat, Vector2.zero);

            if (t < 3.5f)
            {
                // Both main seats push ahead: straight-line cruising.
                router.SetScripted(left, ahead);
                router.SetScripted(other, ahead);
            }
            else if (t < 5.5f)
            {
                // One keeps pushing, the other stops: off-centre thrust turns the raft.
                router.SetScripted(left, ahead);
            }
            else if (t < 6.4f)
            {
                // One burst from both main seats, along their last aim.
                router.SetScripted(left, Vector2.zero, burst: !burstFired);
                router.SetScripted(other, Vector2.zero, burst: !burstFired);
                burstFired = true;
            }
            else if (t < 8.5f)
            {
                // Everyone shoves sideways at once. Only players whose slice faces that way can help.
                for (var seat = 0; seat < crew; seat++) router.SetScripted(seat, right);
            }
        }
    }
}
