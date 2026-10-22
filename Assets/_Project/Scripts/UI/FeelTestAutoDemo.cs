using System;
using System.IO;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Scripted run for checking the feel test without a human: launch a player build with
    /// <c>-rushahead-demo &lt;output folder&gt;</c> and it drives the boat, saves screenshots, logs state and quits.
    /// Add <c>-rushahead-crew &lt;1-4&gt;</c> to run it with a smaller crew. On a course (<c>-rushahead-course n</c>), it
    /// photographs the course from above first, lets the autopilot (or the host's crew) race it, and quits after the results.
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

        static readonly (float time, string name)[] CourseShots =
        {
            (1.5f, "1-countdown"), (8f, "2-under-way"), (20f, "3"), (35f, "4"), (50f, "5"), (65f, "6"), (80f, "7"), (95f, "8"),
        };

        const float ResultsShotDelay = 2.5f;
        const float BraceOrderAt = 34f;

        FeelTestBootstrap game;
        string outputFolder;
        float elapsed;
        float duration = 10.8f;
        int nextShot;
        bool burstFired;
        bool crewPanelOpened;
        int mapState; // 0 to take, 1 taken this frame (restore next), 2 done
        float finishedAt = -1f;
        bool braceOrdered;
        Vector3 savedPosition;
        Quaternion savedRotation;

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
            else if (game.Venue > 0)
                demo.duration = 240f; // long enough to finish any course; it quits right after the results anyway
            Debug.Log($"[RushAhead] Auto demo writing to {demo.outputFolder}");
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            if (game.Venue > 0)
            {
                UpdateCourseDemo();
                return;
            }

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

        /// <summary>
        /// A course run: a map from above, shots along the way, and the results card. The autopilot drives when asked for
        /// (offline or hosting); a joined copy just watches the host's raft.
        /// </summary>
        void UpdateCourseDemo()
        {
            if (mapState == 1) RestoreCamera();
            if (mapState == 0 && elapsed > 0.3f && game.CurrentCourse != null) MapShot(Path.Combine(outputFolder, "0-map.png"));

            var run = game.Run;
            if (!braceOrdered && elapsed >= BraceOrderAt && game.HasAiCrew && game.Mode != NetMode.Joined)
            {
                braceOrdered = true; // shows the order bar at work in the next shot
                game.GiveOrder(Sim.CrewOrder.Brace);
            }

            if (nextShot < CourseShots.Length && elapsed >= CourseShots[nextShot].time && !run.Finished)
                Shot(CourseShots[nextShot++].name, run);

            if (run.Finished && finishedAt < 0f) finishedAt = elapsed;
            if (finishedAt >= 0f && elapsed - finishedAt >= ResultsShotDelay && nextShot < 100)
            {
                nextShot = 100;
                Shot("9-results", run);
            }

            if ((finishedAt >= 0f && elapsed - finishedAt > ResultsShotDelay + 1f) || elapsed > duration) Application.Quit();
        }

        void Shot(string name, RaceInfo run)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(outputFolder, $"{name}.png"));
            var boat = game.ViewedBoat;
            Debug.Log($"[RushAhead] {name}: t={elapsed:0.0}s mode={game.Mode} pos={boat.RenderPosition} speed={boat.Velocity.magnitude:0.00} " +
                      $"course={run.Course?.displayName} clock={run.Seconds:0.0} gates={run.GatesPassed} finished={run.Finished} stars={run.Stars} " +
                      $"capsizes={run.Capsizes} order={game.CurrentOrder.Order} {game.NetStatus} crew=[{CrewSummary()}]");
        }

        /// <summary>Looks straight down on the whole course for one frame, without the HUD or haze, and saves that frame.</summary>
        void MapShot(string path)
        {
            var camera = Camera.main;
            var min = Vector2.positiveInfinity;
            var max = Vector2.negativeInfinity;
            foreach (var point in game.CurrentCourse.River.Outline())
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            savedPosition = camera.transform.position;
            savedRotation = camera.transform.rotation;
            camera.GetComponent<ChaseCamera>().enabled = false;
            game.GetComponent<FeelTestHud>().enabled = false;
            RenderSettings.fog = false;

            var centre = (min + max) * 0.5f;
            var size = max - min + new Vector2(16f, 16f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(size.y * 0.5f, size.x * 0.5f / camera.aspect);
            camera.transform.SetPositionAndRotation(SimSpace.ToWorld(centre, 150f), Quaternion.Euler(90f, 0f, 0f)); // north (sim +y) at the top
            ScreenCapture.CaptureScreenshot(path); // this frame, once it has rendered from up here
            mapState = 1;
        }

        void RestoreCamera()
        {
            var camera = Camera.main;
            camera.orthographic = false;
            camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            camera.GetComponent<ChaseCamera>().enabled = true;
            game.GetComponent<FeelTestHud>().enabled = true;
            RenderSettings.fog = true;
            mapState = 2;
        }

        /// <summary>Who sits where as this device sees it, e.g. "P1 Skipper(you) Bear/fur0/Leaf Blower", for checking that rosters agree.</summary>
        string CrewSummary()
        {
            var seats = game.Seats;
            var parts = new string[seats.Length];
            for (var i = 0; i < seats.Length; i++)
            {
                var s = seats[i];
                var who = s.Kept ? $"kept-for-{s.Loadout.Name}" : s.Ai ? "AI" : !s.Human ? "spare" : s.Loadout.Name + (s.Yours ? "(you)" : s.Host ? "(host)" : "");
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
