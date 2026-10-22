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

        static readonly (float time, string name)[] Shots =
        {
            (2.5f, "1-cruising"), (5.0f, "2-turning"), (6.3f, "3-burst"), (7.4f, "4-heeling"), (9.6f, "5-after"),
        };

        FeelTestBootstrap game;
        string outputFolder;
        float elapsed;
        int nextShot;
        bool burstFired;

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

            var crewIndex = Array.IndexOf(args, CrewFlag);
            if (crewIndex >= 0 && crewIndex + 1 < args.Length && int.TryParse(args[crewIndex + 1], out var crew) && crew >= 1 && crew <= CrewSectors.MaxCrew)
                game.SetCrewCount(crew);
            Debug.Log($"[RushAhead] Auto demo writing to {demo.outputFolder}");
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            Drive(game.Router, elapsed);

            if (nextShot < Shots.Length && elapsed >= Shots[nextShot].time)
            {
                var (_, name) = Shots[nextShot++];
                ScreenCapture.CaptureScreenshot(Path.Combine(outputFolder, $"{name}.png"));
                var boat = game.Boat;
                Debug.Log($"[RushAhead] {name}: t={elapsed:0.0}s pos={boat.Body.Position} heading={boat.Body.AngleRad * Mathf.Rad2Deg:0}deg " +
                          $"speed={boat.Body.Velocity.magnitude:0.00} lean={boat.Tilt.Tilt.magnitude:0.000} state={boat.State} capsizes={boat.CapsizeCount} " +
                          $"air=[{string.Join(",", Array.ConvertAll(boat.Crew, c => c.Tank.Fraction.ToString("0.00")))}]");
            }

            if (elapsed > 10.5f) Application.Quit();
        }

        /// <summary>
        /// Sticks are camera-relative, exactly as a player's would be: up pushes away from the camera. The two main
        /// seats are whichever the left and right touch sticks drive at the current crew size.
        /// </summary>
        void Drive(CrewInputRouter router, float t)
        {
            var ahead = Vector2.up;
            var right = Vector2.right;
            var crew = game.Boat.Crew.Length;
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
