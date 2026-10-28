using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RushAhead.Content;
using UnityEngine;

namespace RushAhead.UI
{
    /// <summary>
    /// Walks every screen without a human, pressing the same named controls a player would, saving a screenshot and a
    /// log line at each: launch a player build with <c>-rushahead-tour &lt;output folder&gt;</c>. Add
    /// <c>-rushahead-ai-all ace</c> so the race finishes by itself, <c>-rushahead-demo-speed 3</c> to get there sooner,
    /// and <c>-rushahead-safe-area l,t,r,b</c> (screen pixels) to fake a phone's notch. The player's look is put back
    /// as it was at the end.
    /// </summary>
    public sealed class ScreenTour : MonoBehaviour
    {
        const string Flag = "-rushahead-tour";

        sealed class Step
        {
            public float Wait;
            public string What;
            public Func<bool> Act;
            public Func<bool> Until;
            public float Timeout;
        }

        readonly List<Step> steps = new();
        GameController game;
        string folder;
        int next;
        float waited;
        Loadout look;
        int ai;

        public static void StartIfRequested(GameController game)
        {
            var folder = CommandLine.After(Flag);
            if (folder == null) return;
            if (CommandLine.After("-rushahead-safe-area") is { } inset)
            {
                var p = Array.ConvertAll(inset.Split(','), s => int.Parse(s, CultureInfo.InvariantCulture));
                GameUi.FakeInset = new RectOffset(p[0], p[2], p[1], p[3]);
            }

            if (float.TryParse(CommandLine.After("-rushahead-demo-speed"), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                GameUi.DemoSpeed = Mathf.Clamp(speed, 1f, 6f);

            Directory.CreateDirectory(folder);
            var tour = game.gameObject.AddComponent<ScreenTour>();
            tour.game = game;
            tour.folder = folder;
            tour.look = game.Local.Profile;
            tour.ai = game.AiSetting;
            game.AllowSearching = false; // listening on every network makes desktop firewalls ask
            if (game.Mode == NetMode.Offline) tour.Plan();
            else tour.PlanTogether(game.Mode == NetMode.Hosting);
        }

        /// <summary>
        /// Two copies on one PC (one with <c>-rushahead-host 127.0.0.1</c>, one with <c>-rushahead-join 127.0.0.1</c>):
        /// the host picks the course and starts; the joiner follows through the lobby, the race and the results, and
        /// sees the game end when the host quits.
        /// </summary>
        void PlanTogether(bool hosting)
        {
            Shot(6f, "net-00-lobby");
            if (hosting)
            {
                Press(2f, "lobby-course");
                Press(0.3f, "venue-1");
                Shot(2f, "net-01-lobby-course");
                Press(4f, "lobby-start");
            }
            else
            {
                Wait("the host's course", () => game.Venue == 1, 30f);
                Shot(1.5f, "net-01-lobby-course");
            }

            Wait("the race", () => game.Ui.Flow.Screen == AppScreen.Race, 60f);
            Shot(6f, "net-02-race");
            Wait("the results", () => game.Ui.Flow.Screen == AppScreen.Results, 300f);
            Shot(1f, "net-03-results");
            if (!hosting)
            {
                Wait("the host to leave", () => game.Ui.Flow.Screen == AppScreen.MainMenu, 60f);
                Shot(1f, "net-04-host-left");
            }

            Do(hosting ? 4f : 1f, "quit", () =>
            {
                Application.Quit();
                return true;
            });
        }

        void Plan()
        {
            Shot(0.6f, "00-title");
            Shot(3.2f, "01-menu");

            Press(0.2f, "menu-animal");
            Shot(1.2f, "02-animal");
            Press(0.2f, "animal-species-4");
            Press(0.2f, "animal-fur-6");
            Press(0.2f, "animal-blower-1");
            Shot(1.2f, "03-animal-changed");
            Press(0.2f, "animal-done");

            Press(0.4f, "menu-settings");
            Shot(0.8f, "04-settings");
            Press(0.2f, "settings-done");

            Press(0.4f, "menu-host");
            Shot(0.6f, "05-host");
            Press(0.2f, "host-back");
            Press(0.3f, "menu-join");
            Shot(0.6f, "06-join");
            Press(0.2f, "join-back");

            Press(0.3f, "play-solo");
            Shot(1.5f, "07-lobby");
            Press(0.2f, "lobby-mode-1");
            Press(0.2f, "lobby-crew-2");
            Shot(1.5f, "08-lobby-team");
            Press(0.2f, "lobby-course");
            Shot(0.5f, "09-course-picker");
            Press(0.2f, "venue-1");
            Press(0.2f, "lobby-mode-0");
            Press(0.2f, "lobby-crew-3");
            Shot(1.5f, "10-lobby-course");

            Press(0.3f, "lobby-start");
            Shot(1.0f, "11-countdown");
            Shot(6f, "12-race");
            Do(0.5f, "pause", () =>
            {
                game.Ui.Flow.Pause();
                return true;
            });
            Shot(0.6f, "13-pause");
            Press(0.2f, "pause-resume");
            Shot(8f, "14-race-later");
            Wait("the results", () => game.Ui.Flow.Screen == AppScreen.Results, 240f);
            Shot(1.0f, "15-results");
            Press(0.5f, "results-lobby");
            Shot(1.5f, "16-lobby-again");

            Do(0.2f, "put the look back", () =>
            {
                game.DressMe(look);
                game.SetAiSetting(ai);
                return true;
            });
            Do(0.5f, "quit", () =>
            {
                Application.Quit();
                return true;
            });
        }

        void Do(float wait, string what, Func<bool> act) => steps.Add(new Step { Wait = wait, What = what, Act = act });

        void Wait(string what, Func<bool> until, float timeout) => steps.Add(new Step { What = $"wait for {what}", Until = until, Timeout = timeout });

        void Press(float wait, string control) => Do(wait, $"press {control}", () => game.Ui.Press(control));

        void Shot(float wait, string name) => Do(wait, $"shot {name}", () =>
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{name}.png"));
            var flow = game.Ui.Flow;
            var run = game.Run;
            Debug.Log($"[RushAhead.ui] shot={name} screen={flow.Screen} paused={flow.Paused} session={game.Session} phase={game.Phase} " +
                      $"venue={game.Venue} rafts={game.Rafts} crew={game.CrewCount} ai={game.AiSetting} clock={run.Seconds:0.0} " +
                      $"gates={run.GatesPassed}/{run.Gates} finished={run.Finished} stars={run.Stars} newBest={run.NewBest} {Screen.width}x{Screen.height}");
            return true;
        });

        void Update()
        {
            if (next >= steps.Count) return;
            waited += Time.unscaledDeltaTime;
            var step = steps[next];
            if (step.Until != null)
            {
                if (step.Until()) Advance();
                else if (waited > step.Timeout)
                {
                    Debug.LogError($"[RushAhead.ui] tour gave up waiting for {step.What} (on {game.Ui.Flow.Screen})");
                    Advance();
                }

                return;
            }

            if (waited < step.Wait) return;
            Advance();
            if (!step.Act()) Debug.LogError($"[RushAhead.ui] tour step failed: {step.What} (on {game.Ui.Flow.Screen})");
        }

        void Advance()
        {
            waited = 0f;
            next++;
        }
    }
}
