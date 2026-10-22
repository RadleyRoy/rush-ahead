using System;
using System.IO;
using RushAhead.Sim;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Scripted run for checking the feel test without a human: launch a player build with
    /// <c>-rushahead-demo &lt;output folder&gt;</c> and it drives the boat, saves screenshots, logs state and quits.
    /// </summary>
    public sealed class FeelTestAutoDemo : MonoBehaviour
    {
        const string Flag = "-rushahead-demo";

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

        void Drive(CrewInputRouter router, float t)
        {
            var east = Vector2.right;
            var north = Vector2.up;
            for (var seat = 0; seat < SimConfig.SeatCount; seat++) router.SetScripted(seat, Vector2.zero);

            if (t < 3.5f)
            {
                // Both rear seats push east: straight-line cruising.
                router.SetScripted(2, east);
                router.SetScripted(3, east);
            }
            else if (t < 5.5f)
            {
                // Rear-left keeps pushing, rear-right stops: off-centre thrust turns the boat.
                router.SetScripted(2, east);
            }
            else if (t < 6.4f)
            {
                // One burst from both rear seats, along their last aim.
                router.SetScripted(2, Vector2.zero, burst: !burstFired);
                router.SetScripted(3, Vector2.zero, burst: !burstFired);
                burstFired = true;
            }
            else if (t < 8.5f)
            {
                // Everyone shoves north at once: the boat heels hard.
                for (var seat = 0; seat < SimConfig.SeatCount; seat++) router.SetScripted(seat, north);
            }
        }
    }
}
