using RushAhead.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>Controls, display and the odds and ends, saved on this device as soon as they change.</summary>
    public sealed class SettingsScreen : GameScreen
    {
        Segmented aim;
        Segmented hand;
        Segmented frames;
        Segmented quality;
        Segmented dev;
        Segmented sound;
        Segmented music;
        Segmented buzz;

        public SettingsScreen(GameUi ui) : base(ui)
        {
        }

        GameSettings S => App.Settings;

        public override void Build()
        {
            Root.AddToClassList("center");
            var panel = Ui.El("panel col");
            panel.style.width = 1240;
            panel.Add(Ui.Text("Settings", "h1"));
            panel.Add(Ui.Spacer());

            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("scroll");
            var body = scroll.contentContainer;

            body.Add(Ui.Text("Controls", "h2"));
            aim = Option(body, "Stick aims", "Where the raft goes (your animal blows the other way), or where the air goes (expert).",
                "settings-aim", new[] { "The raft", "The air" }, i => S.directNozzleAim = i == 1);
            hand = Option(body, "Stick on the", "Left-handed puts the stick on the right and Burst and Brace on the left.",
                "settings-hand", new[] { "Left", "Right" }, i => S.leftHanded = i == 1);

            body.Add(Ui.Spacer());
            body.Add(Ui.Text("Sound", "h2"));
            sound = Option(body, "Sound effects", "Blowers, splashes, bumps and the countdown.", "settings-sound", new[] { "Off", "Low", "Full" },
                i => S.soundVolume = i * 0.5f);
            music = Option(body, "Music", "The tunes in the menus and the race.", "settings-music", new[] { "Off", "Low", "Full" },
                i => S.musicVolume = i * 0.5f);
            buzz = Option(body, "Vibration", "A buzz when your raft bumps, capsizes or bursts.", "settings-vibration", new[] { "Off", "On" },
                i => S.vibration = i == 1);

            body.Add(Ui.Spacer());
            body.Add(Ui.Text("Display", "h2"));
            frames = Option(body, "Frame rate", "60 is smoother; 30 saves battery.", "settings-fps", new[] { "30", "60" }, i => S.frameRate = i == 0 ? 30 : 60);
            quality = Option(body, "Graphics", "Lower it if the game stutters on this phone.", "settings-quality", new[] { "Low", "Medium", "High" },
                i => S.quality = i + 1);

            body.Add(Ui.Spacer());
            body.Add(Ui.Text("Advanced", "h2"));
            dev = Option(body, "Developer tools", "Live tuning sliders and a frame-rate readout.", "settings-dev", new[] { "Off", "On" },
                i => S.developerTools = i == 1);
            var reset = Btn("settings-reset-bests", "Reset best times", () => App.Confirm("Forget every best time on this phone?", null, "Reset", () =>
            {
                Records.Clear(Game.Courses);
                App.Toast("Best times cleared.");
            }), "btn btn--small btn--danger");
            reset.style.alignSelf = Align.FlexStart;
            reset.style.marginTop = 12;
            body.Add(reset);
            body.Add(Ui.Spacer());
            body.Add(Ui.Text($"Rush Ahead {Application.version}   Network protocol {Protocol.Version}   Created by Radley", "small muted"));
            panel.Add(scroll);

            panel.Add(Ui.Spacer("spacer-s"));
            panel.Add(Ui.El("row", Ui.Grow(), Btn("settings-done", "Done", Flow.Close, "btn btn--primary")));
            Root.Add(panel);
        }

        Segmented Option(VisualElement body, string title, string help, string name, string[] choices, System.Action<int> picked)
        {
            var text = Ui.El("col grow", Ui.Text(title, "h3"), Ui.Text(help, "small muted"));
            text.style.marginRight = 24;
            text.style.flexShrink = 1;
            text.style.minWidth = 0;
            var choice = new Segmented(App, name, choices, i =>
            {
                picked(i);
                S.Save();
                App.ApplySettings();
            });
            var row = Ui.El("card row", text, choice);
            body.Add(row);
            return choice;
        }

        public override void Refresh()
        {
            aim.Selected = S.directNozzleAim ? 1 : 0;
            hand.Selected = S.leftHanded ? 1 : 0;
            frames.Selected = S.frameRate == 30 ? 0 : 1;
            quality.Selected = S.quality < 0 ? Mathf.Clamp(QualitySettings.GetQualityLevel() - 1, 0, 2) : Mathf.Clamp(S.quality - 1, 0, 2);
            dev.Selected = S.developerTools ? 1 : 0;
            sound.Selected = Mathf.RoundToInt(S.soundVolume * 2f);
            music.Selected = Mathf.RoundToInt(S.musicVolume * 2f);
            buzz.Selected = S.vibration ? 1 : 0;
        }
    }
}
