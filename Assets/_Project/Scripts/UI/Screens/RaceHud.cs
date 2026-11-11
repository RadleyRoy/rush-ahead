using System.Collections.Generic;
using RushAhead.Content;
using RushAhead.Net;
using RushAhead.Render;
using RushAhead.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// The race: the clock with checkpoint splits against your best, the other raft in a Team Race, the countdown, a
    /// warning as the raft heels over, your stick, Burst and Brace, the AI crew's orders, and everyone's name.
    /// It only draws: every touch goes through <see cref="CrewInputRouter"/> (see <see cref="TouchControls"/>), which
    /// it tells where its buttons are each frame. The pause menu opens over it.
    /// </summary>
    public sealed class RaceHud : GameScreen
    {
        const float DangerFrom = 0.65f;
        const float HitSlop = 18f; // panel pixels added round each button for thumbs

        static readonly (CrewOrder order, string id, string label)[] Orders =
        {
            (CrewOrder.Go, "order-go", "Go"), (CrewOrder.Hold, "order-hold", "Hold"), (CrewOrder.Brace, "order-brace", "Brace"),
            (CrewOrder.PushHere, "order-push", "Push here"),
        };

        readonly List<Label> tags = new();
        readonly Dictionary<string, VisualElement> orderButtons = new();
        VisualElement pauseButton;
        VisualElement clockPanel;
        Label clock;
        Label checkpoint;
        Label split;
        HeartRow hearts;
        Label best;
        VisualElement teamTrack;
        VisualElement redMarker;
        VisualElement blueMarker;
        VisualElement greenMarker;
        VisualElement yellowMarker;
        Label teamLabel;
        Label rivalLabel;
        HeartRow rivalHearts;
        Label centre;
        Label capsized;
        Label watching;
        VisualElement vignette;
        VisualElement burst;
        VisualElement brace;
        VisualElement look;
        VisualElement gust;
        Label gustLabel;
        VisualElement boss;
        Label bossLabel;
        VisualElement item;
        Label itemLabel;
        PowerUp shownItem;
        VisualElement watch;
        Label watchLabel;
        VisualElement orderBar;
        VisualElement sticks;
        VisualElement tagLayer;
        VisualElement pushMarker;
        VisualElement pauseMenu;
        AheadWarning ahead;
        Label pauseTitle;
        Button restart;
        Button toLobby;
        Button leave;
        float air = 1f;
        float finishedAt = -1f;
        float outAt = -1f;
        float heartsLostAt = -10f;
        int shownLives = Race.MaxLives;
        bool subscribed;
        int heardCountdown;
        int heardGates;
        bool heardGo;
        bool heardFinish;
        RaftView feeling;

        public RaceHud(GameUi ui) : base(ui)
        {
        }

        static float PanelScale => Screen.height / 1080f;

        public override void Build()
        {
            Root.pickingMode = PickingMode.Ignore;
            Root.style.paddingLeft = Root.style.paddingRight = Root.style.paddingTop = Root.style.paddingBottom = 0;

            vignette = Ui.El("layer");
            vignette.pickingMode = PickingMode.Ignore;
            vignette.style.borderTopWidth = vignette.style.borderBottomWidth = vignette.style.borderLeftWidth = vignette.style.borderRightWidth = 70;
            var red = new Color(1f, 0.2f, 0.15f, 0.55f);
            vignette.style.borderTopColor = vignette.style.borderBottomColor = vignette.style.borderLeftColor = vignette.style.borderRightColor = red;
            vignette.style.opacity = 0f;
            Root.Add(vignette);

            tagLayer = Ui.El("layer");
            tagLayer.pickingMode = PickingMode.Ignore;
            Root.Add(tagLayer);
            pushMarker = new Swatch(new Color(1f, 0.416f, 0.169f, 0.9f), 44f) { pickingMode = PickingMode.Ignore };
            pushMarker.style.position = Position.Absolute;
            tagLayer.Add(pushMarker);

            sticks = Ui.El("layer");
            sticks.pickingMode = PickingMode.Ignore;
            sticks.generateVisualContent += DrawSticks;
            Root.Add(sticks);

            // Top left: pause.
            pauseButton = Visual("II", "btn btn--icon");
            Place(pauseButton, left: 36, top: 30);
            Root.Add(pauseButton);

            // Top middle: the clock, the next checkpoint and the last split.
            clockPanel = Ui.El("panel col center");
            clockPanel.pickingMode = PickingMode.Ignore;
            clockPanel.style.position = Position.Absolute;
            clockPanel.style.top = 24;
            clockPanel.style.left = new Length(50, LengthUnit.Percent);
            clockPanel.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            clockPanel.style.paddingTop = clockPanel.style.paddingBottom = 16;
            clockPanel.style.minWidth = 440;
            clock = Ui.Text("0:00.0", "h1");
            clock.style.fontSize = 96;
            clock.style.letterSpacing = 2;
            checkpoint = Ui.Text("", "small");
            split = Ui.Text("", "h3");
            hearts = new HeartRow(38f, Race.MaxLives);
            hearts.style.marginTop = 8;
            clockPanel.Add(clock);
            clockPanel.Add(checkpoint);
            clockPanel.Add(hearts);
            clockPanel.Add(split);
            Root.Add(clockPanel);

            // Top right: your best, or in a Team Race how far each raft has got.
            best = Ui.Text("", "chip");
            best.style.position = Position.Absolute;
            best.style.right = 36;
            best.style.top = 40;
            Root.Add(best);

            teamTrack = Ui.El("panel");
            teamTrack.style.position = Position.Absolute;
            teamTrack.style.right = 36;
            teamTrack.style.top = 30;
            teamTrack.style.width = 520;
            teamTrack.style.paddingTop = teamTrack.style.paddingBottom = 18;
            teamLabel = Ui.Text("", "small");
            var lane = Ui.El();
            lane.style.height = 18;
            lane.style.marginTop = 12;
            lane.style.backgroundColor = new Color(1f, 1f, 1f, 0.15f);
            lane.style.borderTopLeftRadius = lane.style.borderTopRightRadius = lane.style.borderBottomLeftRadius = lane.style.borderBottomRightRadius = 9;
            redMarker = new Swatch(new Color(1f, 0.42f, 0.37f), 34f);
            blueMarker = new Swatch(new Color(0.31f, 0.64f, 1f), 34f);
            greenMarker = new Swatch(new Color(0.36f, 0.88f, 0.48f), 34f);
            yellowMarker = new Swatch(new Color(1f, 0.82f, 0.3f), 34f);
            foreach (var marker in new[] { redMarker, blueMarker, greenMarker, yellowMarker })
            {
                marker.style.position = Position.Absolute;
                marker.style.top = -8;
                lane.Add(marker);
            }

            rivalLabel = Ui.Text("", "small");
            rivalHearts = new HeartRow(26f, Race.MaxLives);
            rivalHearts.style.marginLeft = 12;
            var rivalRow = Ui.El("row", rivalLabel, rivalHearts);
            rivalRow.style.marginTop = 14;
            rivalRow.style.alignItems = Align.Center;
            teamTrack.Add(teamLabel);
            teamTrack.Add(lane);
            teamTrack.Add(rivalRow);
            Root.Add(teamTrack);

            // Middle: 3, 2, 1, GO!, FINISH!, capsized.
            centre = Ui.Text("", "wordmark");
            centre.pickingMode = PickingMode.Ignore;
            centre.style.position = Position.Absolute;
            centre.style.left = 0;
            centre.style.right = 0;
            centre.style.top = new Length(32, LengthUnit.Percent);
            centre.style.unityTextAlign = TextAnchor.MiddleCenter;
            centre.style.fontSize = 190;
            Root.Add(centre);
            capsized = Ui.Text("Capsized! Hang on...", "h2");
            capsized.style.position = Position.Absolute;
            capsized.style.left = 0;
            capsized.style.right = 0;
            capsized.style.top = new Length(56, LengthUnit.Percent);
            capsized.style.unityTextAlign = TextAnchor.MiddleCenter;
            Root.Add(capsized);
            watching = Ui.Text("Watching: the rafts are full", "chip");
            watching.style.position = Position.Absolute;
            watching.style.bottom = 40;
            watching.style.left = 40;
            Root.Add(watching);

            // Once your raft is home (or out): watch the others. A tap switches to the next raft still racing.
            watchLabel = Ui.Text("WATCH", "h3");
            watchLabel.pickingMode = PickingMode.Ignore;
            watch = Ui.El("btn btn--small", watchLabel);
            watch.style.position = Position.Absolute;
            watch.style.top = 210;
            watch.style.right = 36;
            Root.Add(watch);

            // The power-up your raft carries: tap to use it.
            item = Visual("", "btn btn--round");
            item.style.width = item.style.height = 156;
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 78;
            item.style.paddingLeft = item.style.paddingRight = 0;
            item.style.minWidth = 0;
            item.generateVisualContent += DrawItem;
            itemLabel = Ui.Text("", "small");
            itemLabel.pickingMode = PickingMode.Ignore;
            itemLabel.style.position = Position.Absolute;
            itemLabel.style.bottom = 10;
            itemLabel.style.left = 0;
            itemLabel.style.right = 0;
            itemLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            item.Add(itemLabel);
            Root.Add(item);

            // A gust of wind coming (in the rain): which way it'll blow, flashing first, then solid while it blows.
            gustLabel = Ui.Text("", "h3");
            gust = Ui.El("panel", gustLabel);
            gust.pickingMode = PickingMode.Ignore;
            gust.style.paddingLeft = gust.style.paddingRight = 18;
            gust.style.paddingTop = gust.style.paddingBottom = 6;
            var gustRow = Ui.El("row", gust);
            gustRow.pickingMode = PickingMode.Ignore;
            gustRow.style.position = Position.Absolute;
            gustRow.style.left = 0;
            gustRow.style.right = 0;
            gustRow.style.top = 210;
            gustRow.style.justifyContent = Justify.Center;
            Root.Add(gustRow);

            // Old Snapjaw close behind (the boss course).
            bossLabel = Ui.Text("", "h3");
            boss = Ui.El("panel", bossLabel);
            boss.pickingMode = PickingMode.Ignore;
            boss.style.paddingLeft = boss.style.paddingRight = 18;
            boss.style.paddingTop = boss.style.paddingBottom = 6;
            var bossRow = Ui.El("row", boss);
            bossRow.pickingMode = PickingMode.Ignore;
            bossRow.style.position = Position.Absolute;
            bossRow.style.left = 0;
            bossRow.style.right = 0;
            bossRow.style.top = 270;
            bossRow.style.justifyContent = Justify.Center;
            Root.Add(bossRow);

            // The look stick: drag it to swing the camera round the raft.
            look = Ui.El();
            look.pickingMode = PickingMode.Ignore;
            look.style.position = Position.Absolute;
            look.style.width = look.style.height = 132;
            look.generateVisualContent += DrawLook;
            Root.Add(look);

            // Bottom right (left, left-handed): Burst with its air, and Brace.
            burst = Visual("Burst", "btn btn--round");
            burst.style.width = burst.style.height = 220;
            burst.style.borderTopLeftRadius = burst.style.borderTopRightRadius = burst.style.borderBottomLeftRadius = burst.style.borderBottomRightRadius = 110;
            burst.style.paddingLeft = burst.style.paddingRight = 0;
            burst.style.minWidth = 0;
            burst.generateVisualContent += DrawAir;
            Root.Add(burst);
            brace = Visual("Brace", "btn");
            brace.style.width = brace.style.height = 172;
            brace.style.borderTopLeftRadius = brace.style.borderTopRightRadius = brace.style.borderBottomLeftRadius = brace.style.borderBottomRightRadius = 86;
            brace.style.paddingLeft = brace.style.paddingRight = 0;
            brace.style.minWidth = 0;
            brace.style.fontSize = 40;
            Root.Add(brace);

            // Bottom middle: orders for the AI crewmates.
            orderBar = Ui.El("segmented");
            orderBar.style.position = Position.Absolute;
            orderBar.style.bottom = 36;
            orderBar.style.left = new Length(50, LengthUnit.Percent);
            orderBar.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            orderBar.style.backgroundColor = new Color(0.047f, 0.055f, 0.07f, 0.76f);
            foreach (var (_, id, label) in Orders)
            {
                var button = Visual(label, "segment");
                orderButtons[id] = button;
                orderBar.Add(button);
            }

            Root.Add(orderBar);

            ahead = new AheadWarning(Root, tagLayer);
            BuildPauseMenu();
        }

        void BuildPauseMenu()
        {
            pauseMenu = Ui.El("layer scrim center");
            var panel = Ui.El("panel col");
            panel.style.width = 760;
            pauseTitle = Ui.Text("Paused", "h1");
            panel.Add(pauseTitle);
            panel.Add(Ui.Spacer());
            panel.Add(Btn("pause-resume", "Resume", Flow.Resume, "btn btn--primary"));
            restart = Btn("pause-restart", "Restart the race", () =>
            {
                Flow.Resume();
                Game.ResetBoat();
            });
            panel.Add(restart);
            panel.Add(Btn("pause-guide", "Field guide", () => App.OpenGuide()));
            panel.Add(Btn("pause-settings", "Settings", Flow.OpenSettings));
            toLobby = Btn("pause-lobby", "Back to the lobby", () =>
            {
                if (Game.Session == SessionKind.Hosting && OthersAboard())
                    App.Confirm("Stop the race for everyone?", null, "Stop race", () =>
                    {
                        Flow.Resume();
                        Game.BackToLobby();
                    });
                else
                {
                    Flow.Resume();
                    Game.BackToLobby();
                }
            });
            panel.Add(toLobby);
            leave = Btn("pause-leave", "Leave the game", () => App.ConfirmLeave());
            panel.Add(leave);
            pauseMenu.Add(panel);
            Ui.Show(pauseMenu, false);
            Root.Add(pauseMenu);
            BuildPhotoFinish();
        }

        VisualElement photoLayer;
        VisualElement photoFlash;
        Label photoCaption;
        Label photoSkip;
        bool photoCapture;

        /// <summary>The photo finish over everything: black bars top and bottom, its title, the flash, and the margin.</summary>
        void BuildPhotoFinish()
        {
            photoLayer = Ui.El("layer");
            photoLayer.pickingMode = PickingMode.Ignore;
            VisualElement Bar(bool top)
            {
                var bar = Ui.El("row");
                bar.pickingMode = PickingMode.Ignore;
                bar.style.position = Position.Absolute;
                bar.style.left = bar.style.right = 0;
                if (top) bar.style.top = 0;
                else bar.style.bottom = 0;
                bar.style.height = Length.Percent(13f);
                bar.style.backgroundColor = new Color(0f, 0f, 0f, 0.92f);
                bar.style.justifyContent = Justify.Center;
                bar.style.alignItems = Align.Center;
                return bar;
            }

            var topBar = Bar(true);
            topBar.Add(Ui.Text("PHOTO FINISH", "h1 sun"));
            var bottomBar = Bar(false);
            photoCaption = Ui.Text("", "h2");
            bottomBar.Add(photoCaption);
            photoSkip = Ui.Text("TAP TO SKIP", "small muted");
            photoSkip.style.position = Position.Absolute;
            photoSkip.style.right = 48;
            bottomBar.Add(photoSkip);
            photoFlash = Ui.El("layer");
            photoFlash.pickingMode = PickingMode.Ignore;
            photoFlash.style.backgroundColor = Color.white;
            photoFlash.style.opacity = 0f;
            photoLayer.Add(topBar);
            photoLayer.Add(bottomBar);
            photoLayer.Add(photoFlash);
            Ui.Show(photoLayer, false);
            Root.Add(photoLayer);
        }

        /// <summary>While the photo finish plays, it's all that shows; a tap (or Space) skips on.</summary>
        void RefreshPhotoFinish(TouchControls touch)
        {
            var stage = Game.PhotoStage;
            var on = stage != PhotoFinishStage.None;
            Ui.Show(photoLayer, on);
            foreach (var child in Root.Children())
                if (child != photoLayer) child.style.visibility = on ? Visibility.Hidden : StyleKeyword.Null;
            if (!on)
            {
                if (photoCapture) touch.CancelCapture();
                photoCapture = false;
                return;
            }

            Ui.Set(photoCaption, stage == PhotoFinishStage.Frozen ? Game.PhotoCaption ?? "" : "");
            Ui.Show(photoSkip, stage == PhotoFinishStage.Playing);
            photoFlash.style.opacity = Game.PhotoFlash;
            if (!touch.Capturing)
            {
                photoCapture = true;
                touch.CaptureNextTap(_ =>
                {
                    photoCapture = false;
                    Game.SkipPhotoFinish();
                });
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)) Game.SkipPhotoFinish();
        }

        bool OthersAboard()
        {
            var people = 0;
            foreach (var seat in Game.Seats)
                if (seat.Human) people++;
            return people > 1;
        }

        static VisualElement Visual(string text, string classes)
        {
            var label = Ui.Text(classes.Contains("btn") || classes.Contains("segment") ? text.ToUpperInvariant() : text, classes);
            label.pickingMode = PickingMode.Ignore;
            if (classes.Contains("btn") && !classes.Contains("btn--round")) Ui.Slant(label);
            return label;
        }

        static void Place(VisualElement element, float? left = null, float? top = null, float? right = null, float? bottom = null)
        {
            element.style.position = Position.Absolute;
            element.style.left = Edge(left);
            element.style.right = Edge(right);
            element.style.top = Edge(top);
            element.style.bottom = Edge(bottom);
        }

        static StyleLength Edge(float? value) => value.HasValue ? new StyleLength(value.Value) : new StyleLength(StyleKeyword.Auto);


        public override void OnShow()
        {
            finishedAt = -1f;
            heardCountdown = 0;
            heardGates = Game.Run.GatesPassed;
            heardGo = Game.Run.SinceStart >= 0f;
            heardFinish = Game.Run.Finished;
            if (!subscribed) Game.Router.ButtonTapped += Tapped;
            subscribed = true;
        }

        public override void OnHide()
        {
            if (subscribed) Game.Router.ButtonTapped -= Tapped;
            subscribed = false;
            Feel(null);
            if (GameAudio.Instance != null) GameAudio.Instance.Rush = 0f;
            Game.Router.Touch.ClearButtons();
            Game.Router.Touch.CancelCapture();
        }

        void Tapped(string id)
        {
            GameAudio.Instance?.Play(GameAudio.Instance.Bank.Click, 0.5f);
            if (id == "pause")
            {
                Flow.Pause();
                return;
            }

            if (id == "watch")
            {
                Game.WatchNext();
                return;
            }

            foreach (var (order, orderId, _) in Orders)
            {
                if (orderId != id) continue;
                if (order != CrewOrder.PushHere) Game.GiveOrder(order);
                else
                    Game.Router.Touch.CaptureNextTap(screen =>
                    {
                        if (WaterPoint(screen) is { } point) Game.GiveOrder(CrewOrder.PushHere, point);
                    });
            }
        }

        public override void Refresh()
        {
            var run = Game.Run;
            var boat = Game.OwnBoat;
            var touch = Game.Router.Touch;
            var mySeat = Game.Router.SeatForSlot(0);
            var spectator = Game.Session == SessionKind.Joined && mySeat < 0;
            var twoSticks = Game.Router.SeatForSlot(1) >= 0 && Game.Router.SeatForSlot(1) != mySeat;
            touch.TwoSticks = twoSticks;
            RefreshPhotoFinish(touch);

            // Clock, checkpoints and splits.
            Ui.Show(clockPanel, true);
            if (!run.Active)
            {
                Ui.Set(clock, "Practice");
                Ui.Set(checkpoint, "Pause for the lobby");
                Ui.Show(split, false);
            }
            else
            {
                Ui.Set(clock, Ui.Time(Mathf.Max(0.0001f, run.Seconds)));
                Ui.Set(checkpoint, run.Finished ? "Finished" : run.GatesPassed >= run.Gates - 1 ? "Finish next" : $"Checkpoint {run.GatesPassed + 1} of {run.Gates - 1}");
                var showSplit = run.SplitGate > 0 && run.SplitAge < 3f && run.SplitDelta.HasValue;
                Ui.Show(split, showSplit);
                if (showSplit)
                {
                    var delta = run.SplitDelta.Value;
                    Ui.Set(split, Ui.Delta(delta));
                    Ui.SetClass(split, "good", delta <= 0f);
                    Ui.SetClass(split, "bad", delta > 0f);
                }
            }

            var team = run.Active && run.TeamRace;
            var field = run.Active && run.Elimination && run.FieldProgress != null;
            Ui.Show(best, run.Active && !team && !field && run.Best > 0f);
            if (run.Active) Ui.Set(best, $"Best {Ui.Time(run.Best)}");
            Ui.Show(teamTrack, team || field);
            Ui.Show(greenMarker, field && run.Rafts > 2);
            Ui.Show(yellowMarker, field && run.Rafts > 3);
            if (field) ShowField(run);
            else if (team)
            {
                var redProgress = run.MyRaft == 0 ? run.MyProgress : run.RivalProgress;
                var blueProgress = run.MyRaft == 0 ? run.RivalProgress : run.MyProgress;
                redMarker.style.left = new Length(redProgress * 92f, LengthUnit.Percent);
                blueMarker.style.left = new Length(blueProgress * 92f, LengthUnit.Percent);
                Ui.Set(teamLabel, $"You're {GameController.TeamName(run.MyRaft)}   Red {Mathf.RoundToInt(redProgress * 100f)}%   Blue {Mathf.RoundToInt(blueProgress * 100f)}%");
                var rival = GameController.TeamName(1 - run.MyRaft);
                Ui.Set(rivalLabel, run.RivalOut ? $"{rival} is out of lives!" : $"{rival}'s lives");
                rivalHearts.Count = run.RivalLives;
            }

            // Lives: the hearts jump when one goes.
            Ui.Show(hearts, run.Active);
            if (run.Lives < shownLives) heartsLostAt = Time.unscaledTime;
            shownLives = run.Lives;
            hearts.Count = run.Lives;
            var sinceLost = Time.unscaledTime - heartsLostAt;
            var jump = sinceLost < 0.8f ? 1f + 0.3f * Mathf.Sin(sinceLost * 20f) * (1f - sinceLost / 0.8f) : 1f;
            hearts.style.scale = new Scale(new Vector2(jump, jump));

            // The big words in the middle.
            if (run.Finished && finishedAt < 0f) finishedAt = Time.unscaledTime;
            if (!run.Finished) finishedAt = -1f;
            if (run.Out && outAt < 0f) outAt = Time.unscaledTime;
            if (!run.Out) outAt = -1f;
            var words = !run.Active ? null
                : run.Countdown > 0 ? run.Countdown.ToString()
                : run.SinceStart < 1f ? "GO!"
                : finishedAt >= 0f && Time.unscaledTime - finishedAt < 2.5f ? ((run.TeamRace || run.Elimination) && run.Winner == run.MyRaft ? "YOU WIN!" : run.BossCourse ? "ESCAPED!" : "FINISH!")
                : outAt >= 0f && Time.unscaledTime - outAt < 2.5f ? (run.KnockedOut ? "KNOCKED OUT!" : "OUT!")
                : run.Elimination && run.Winner == run.MyRaft && !run.Finished ? "LAST RAFT LEFT!"
                : null;
            Ui.Show(centre, words != null);
            if (words != null) Ui.Set(centre, words);
            var trouble = !run.Active ? null
                : run.KnockedOut ? "Knocked out! Watch the others race on."
                : run.Out ? run.TeamRace || run.Elimination ? "Out of lives! Watch the others race on." : "Out of lives!"
                : run.Fallen ? "Overboard!"
                : boat != null && boat.Capsized ? "Capsized! Hang on..."
                : null;
            Ui.Show(capsized, trouble != null);
            if (trouble != null) Ui.Set(capsized, trouble);
            Ui.Show(watching, spectator && !Game.WatchingOther);
            var canWatch = Game.CanWatch && !Flow.Paused;
            Ui.Show(watch, canWatch);
            if (canWatch)
            {
                var next = Game.NextToWatch;
                Ui.Set(watchLabel, next == Game.MyRaft ? (spectator ? $"WATCH {GameController.TeamName(next).ToUpperInvariant()}  ›" : "BACK TO YOUR RAFT") : $"WATCH {GameController.TeamName(next).ToUpperInvariant()}  ›");
            }

            // Leaning over: a red edge, and Brace pulses.
            var danger = boat == null ? 0f : Mathf.InverseLerp(DangerFrom, 1f, boat.TiltDanger);
            vignette.style.opacity = danger * 0.9f;
            var pulse = danger > 0f ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;
            brace.style.scale = new Scale(new Vector2(pulse, pulse));

            // Controls: one stick with Burst and Brace, or two sticks (driving two seats) with taps to burst.
            var leftHanded = touch.LeftHanded;
            var ownLook = MyLook();
            var canBurst = ownLook.HasValue && Game.Catalog.BlowerOf(ownLook.Value).CanBurst;
            var controls = !spectator && !twoSticks;
            Ui.Show(burst, controls && canBurst);
            Ui.Show(brace, controls);
            Place(burst, left: leftHanded ? 40 : null, right: leftHanded ? null : 40, bottom: 40);
            Place(brace, left: leftHanded ? 290 : null, right: leftHanded ? null : 290, bottom: 34);
            var held = run.Active && !spectator && boat != null ? boat.Item : PowerUp.None;
            Ui.Show(item, held != PowerUp.None && !Flow.Paused);
            Place(item, left: leftHanded ? 310 : null, right: leftHanded ? null : 310, bottom: twoSticks ? 330 : 236);
            Ui.SetClass(item, "btn--on", touch.Held.Contains(TouchControls.ItemButton));
            if (held != shownItem)
            {
                shownItem = held;
                Ui.Set(itemLabel, held switch { PowerUp.Turbo => "TURBO", PowerUp.Shield => "SHIELD", PowerUp.Horn => "HORN", _ => "" });
                item.MarkDirtyRepaint();
            }

            // Old Snapjaw behind the raft: how close, in red when it's about to lunge.
            var hunted = run.Active && run.Phase == RoomPhase.Racing && run.BossBehind >= 0f && run.BossBehind < 40f && !run.Finished && !run.Out;
            Ui.Show(boss, hunted);
            if (hunted)
            {
                Ui.Set(bossLabel, run.BossLunging ? "SNAPJAW!  BLOW!" : $"SNAPJAW  {run.BossBehind:0} M BEHIND");
                bossLabel.style.color = run.BossLunging || run.BossBehind < 12f ? new Color(1f, 0.3f, 0.22f) : new Color(0.75f, 0.9f, 0.55f);
                boss.style.opacity = run.BossLunging && Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.4f ? 0.5f : 1f;
            }

            var windy = run.Active && run.Phase == RoomPhase.Racing && (run.GustStrength > 0.05f || run.GustWarning > 0f);
            Ui.Show(gust, windy);
            if (windy)
            {
                var arrows = run.GustScreenSide > 0 ? "WIND  ›››" : "‹‹‹  WIND";
                Ui.Set(gustLabel, run.GustStrength > 0.05f ? arrows : run.GustScreenSide > 0 ? "GUST COMING  ›››" : "‹‹‹  GUST COMING");
                var flash = run.GustStrength > 0.05f || Mathf.Repeat(Time.unscaledTime * 4f, 1f) < 0.6f;
                gust.style.opacity = flash ? 1f : 0.35f;
                gustLabel.style.color = new Color(1f, 0.62f, 0.25f);
            }

            Ui.Show(look, run.Active && !Flow.Paused);
            var lookOnLeft = leftHanded && !twoSticks;
            Place(look, left: lookOnLeft ? 84 : null, right: lookOnLeft ? null : 84, bottom: controls ? 300 : twoSticks ? 330 : 60);
            look.MarkDirtyRepaint();
            Ui.SetClass(burst, "btn--on", touch.Held.Contains(TouchControls.BurstButton));
            Ui.SetClass(brace, "btn--on", touch.Held.Contains(TouchControls.BraceButton));
            var crew = Mathf.Max(1, Game.CrewCount);
            var newAir = boat != null && mySeat >= 0 && mySeat < boat.CrewCount ? boat.CrewAt(mySeat % crew).AirFraction : 1f;
            if (Mathf.Abs(newAir - air) > 0.01f)
            {
                air = newAir;
                burst.MarkDirtyRepaint();
            }

            var orders = Game.HasAiCrew && !spectator;
            Ui.Show(orderBar, orders);
            var current = Game.CurrentOrder.Order;
            foreach (var (order, id, _) in Orders)
                Ui.SetClass(orderButtons[id], "segment--on", order == current || (order == CrewOrder.PushHere && touch.Capturing));
            ShowPushMarker(current == CrewOrder.PushHere ? Game.CurrentOrder.Point : (Vector2?)null);

            // What's coming, and (during the countdown) a clean slate for what got you.
            if (run.Active && run.Countdown > 0) App.Culprits.Clear();
            var camera = Camera.main;
            ahead.Refresh(Game, run, Game.ViewedBoat, world => camera != null ? PanelPoint(camera, world) : null);

            Ui.Show(pauseMenu, Flow.Paused);
            if (Flow.Paused) RefreshPauseMenu();

            Feel(Game.RaftView);
            if (GameAudio.Instance != null) GameAudio.Instance.Rush = boat != null ? Mathf.InverseLerp(2.5f, 9f, boat.Velocity.magnitude) : 0f;
            UpdateSounds(run);
            UpdateKeys();
            UpdateTags();
            sticks.MarkDirtyRepaint();
            ReportButtons(controls && canBurst, controls, orders, canWatch, run.Active, shownItem != PowerUp.None && !spectator);
        }

        /// <summary>Elimination: every raft's marker along the course, your place, and what the next knockout gate means.</summary>
        void ShowField(RaceInfo run)
        {
            var markers = new[] { redMarker, blueMarker, greenMarker, yellowMarker };
            for (var raft = 0; raft < markers.Length && raft < run.FieldProgress.Length; raft++)
            {
                markers[raft].style.left = new Length(run.FieldProgress[raft] * 92f, LengthUnit.Percent);
                markers[raft].style.opacity = run.FieldOut[raft] ? 0.25f : 1f;
            }

            var left = 0;
            foreach (var gone in run.FieldOut)
                if (!gone) left++;
            Ui.Set(teamLabel, run.Out ? $"You're {GameController.TeamName(run.MyRaft)}: out   {left} left"
                : $"You're {GameController.TeamName(run.MyRaft)}   {Ordinal(run.Place)} of {run.Rafts}   {left} left");
            var knock = run.NextKnockout;
            Ui.Set(rivalLabel, run.Out || run.Finished ? "The river gets faster after every knockout"
                : knock >= run.Gates ? "Finish: first over the line wins!"
                : knock > 0 ? $"Knockout gate: checkpoint {knock}. Last one through is out!" : "");
            rivalHearts.Count = run.Lives;
        }

        static string Ordinal(int place) => place switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{place}th" };

        /// <summary>Your own raft's knocks, capsizes and bursts shake the camera and buzz the phone.</summary>
        void Feel(RaftView view)
        {
            if (view == feeling) return;
            if (feeling != null)
            {
                feeling.Bumped -= OnBumped;
                feeling.Capsized -= OnCapsized;
                feeling.Fell -= OnFell;
                feeling.Burst -= OnBurst;
            }

            feeling = view;
            if (feeling == null) return;
            feeling.Bumped += OnBumped;
            feeling.Capsized += OnCapsized;
            feeling.Fell += OnFell;
            feeling.Burst += OnBurst;
        }

        void OnBumped(float strength)
        {
            App.Chase.Shake(0.2f + 0.55f * strength);
            Haptics.Pulse(Mathf.RoundToInt(25f + 55f * strength), 0.35f + 0.65f * strength);
        }

        void OnCapsized()
        {
            App.Chase.Shake(0.85f);
            Haptics.Pulse(320, 1f);
            Blame(false);
        }

        void OnFell()
        {
            App.Chase.Shake(1f);
            Haptics.Pulse(450, 1f);
            Blame(true);
        }

        /// <summary>Notes what got your raft, for "What hit me?" on the results.</summary>
        void Blame(bool fell)
        {
            var boat = Game.OwnBoat;
            if (boat == null || !Game.Run.Active) return;
            var culprit = AheadWarning.Culprit(Game.CurrentCourse, boat.RenderPosition, fell);
            if (culprit == null) return;
            App.Culprits.Remove(culprit);
            App.Culprits.Add(culprit);
        }

        void OnBurst(int seat)
        {
            if (seat == Game.Router.SeatForSlot(0)) Haptics.Pulse(22, 0.45f);
        }

        /// <summary>3, 2, 1 beeps and GO, a chime at each checkpoint, a fanfare (and confetti) at the finish.</summary>
        void UpdateSounds(RaceInfo run)
        {
            var audio = GameAudio.Instance;
            if (!run.Active) return;
            if (run.Countdown > 0 && run.Countdown != heardCountdown)
            {
                heardCountdown = run.Countdown;
                heardGo = false;
                heardFinish = false;
                heardGates = 0;
                audio?.Play(audio.Bank.Beep, 0.8f);
            }

            if (!heardGo && run.Countdown == 0 && run.SinceStart >= 0f && run.SinceStart < 1f)
            {
                heardGo = true;
                audio?.Play(audio.Bank.Go, 0.9f);
            }

            if (run.GatesPassed > heardGates)
            {
                heardGates = run.GatesPassed;
                if (!run.Finished) audio?.Play(audio.Bank.Chime, 0.7f);
            }

            if (run.Finished && !heardFinish)
            {
                heardFinish = true;
                audio?.Play(audio.Bank.Fanfare, 0.85f);
                if (Game.RaftView != null) Game.RaftView.Celebrate();
                Haptics.Pulse(120, 0.6f);
            }
        }

        void RefreshPauseMenu()
        {
            var session = Game.Session;
            Ui.Set(pauseTitle, session == SessionKind.Solo ? "Paused" : "The race goes on");
            Ui.Show(restart, Game.CanEditRaft && Game.Run.Active);
            Ui.Show(toLobby, Game.CanEditRaft);
            leave.text = (session == SessionKind.Solo ? "Main menu" : session == SessionKind.Hosting ? "End the game" : "Leave the game").ToUpperInvariant();
        }

        Loadout? MyLook()
        {
            foreach (var seat in Game.Seats)
                if (seat.Yours) return seat.Loadout;
            return null;
        }

        /// <summary>Tells the touch controls where the buttons are now, in screen pixels.</summary>
        void ReportButtons(bool withBurst, bool withBrace, bool withOrders, bool withWatch, bool withLook, bool withItem)
        {
            var touch = Game.Router.Touch;
            touch.ClearButtons();
            touch.AddButton("pause", ScreenRect(pauseButton));
            if (withItem) touch.AddButton(TouchControls.ItemButton, ScreenRect(item));
            if (withWatch) touch.AddButton("watch", ScreenRect(watch));
            if (withLook) touch.AddButton(TouchControls.LookButton, ScreenRect(look));
            if (withBurst) touch.AddButton(TouchControls.BurstButton, ScreenRect(burst));
            if (withBrace) touch.AddButton(TouchControls.BraceButton, ScreenRect(brace));
            if (!withOrders) return;
            foreach (var (_, id, _) in Orders) touch.AddButton(id, ScreenRect(orderButtons[id]));
        }

        static Rect ScreenRect(VisualElement element)
        {
            var area = element.worldBound;
            var s = PanelScale;
            return new Rect((area.x - HitSlop) * s, Screen.height - (area.yMax + HitSlop) * s, (area.width + 2f * HitSlop) * s, (area.height + 2f * HitSlop) * s);
        }

        /// <summary>A point on the water from a screen position, for "push here".</summary>
        static Vector2? WaterPoint(Vector2 screen)
        {
            var camera = Camera.main;
            if (camera == null) return null;
            var ray = camera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance)) return null;
            var point = ray.GetPoint(distance);
            return new Vector2(point.x, point.z);
        }

        void UpdateKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Flow.Paused || !Game.HasAiCrew) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Game.GiveOrder(CrewOrder.Go);
            if (keyboard.digit2Key.wasPressedThisFrame) Game.GiveOrder(CrewOrder.Hold);
            if (keyboard.digit3Key.wasPressedThisFrame) Game.GiveOrder(CrewOrder.Brace);
            if (keyboard.digit4Key.wasPressedThisFrame && Mouse.current != null && WaterPoint(Mouse.current.position.ReadValue()) is { } aimed)
                Game.GiveOrder(CrewOrder.PushHere, aimed);
        }

        /// <summary>Each animal carries its player's name (or "AI") in its seat colour, on every raft.</summary>
        void UpdateTags()
        {
            var camera = Camera.main;
            var seats = Game.Seats;
            var crew = Mathf.Max(1, Game.CrewCount);
            var show = camera != null && (Game.Session != SessionKind.Solo || Game.HasAiCrew || Game.TeamRace);
            var used = 0;
            for (var seat = 0; show && seat < seats.Length; seat++)
            {
                var entry = seats[seat];
                if (!entry.Human && !entry.Ai && !entry.Kept) continue;
                var raft = seat / crew;
                var view = Game.ViewOf(raft);
                var state = Game.RaftState(raft);
                if (view == null || state == null || seat % crew >= state.CrewCount) continue;
                if (state.Out && Game.Elimination) continue; // knocked out: the raft has sunk, so no names over empty water
                var at = PanelPoint(camera, view.TagPosition(seat % crew));
                if (!at.HasValue) continue;

                if (used == tags.Count)
                {
                    var made = Ui.Text("", "h3");
                    made.pickingMode = PickingMode.Ignore;
                    made.style.position = Position.Absolute;
                    made.style.width = 300;
                    made.style.marginLeft = -150;
                    made.style.unityTextAlign = TextAnchor.MiddleCenter;
                    made.style.fontSize = 30;
                    made.style.letterSpacing = 2;
                    made.style.textShadow = new TextShadow { offset = new Vector2(0f, 2f), blurRadius = 6f, color = new Color(0f, 0f, 0f, 0.85f) };
                    tagLayer.Add(made);
                    tags.Add(made);
                }

                var tag = tags[used++];
                var name = string.IsNullOrEmpty(entry.Loadout.Name) ? $"P{seat % crew + 1}" : entry.Loadout.Name;
                Ui.Set(tag, (entry.Yours ? "You" : entry.Kept ? $"{name} (away)" : entry.Ai ? "AI" : name).ToUpperInvariant());
                tag.style.color = RaftView.PlayerColor(seat % crew);
                tag.style.left = at.Value.x;
                tag.style.top = at.Value.y - 24f;
                Ui.Show(tag, true);
            }

            for (var i = used; i < tags.Count; i++) Ui.Show(tags[i], false);
        }

        void ShowPushMarker(Vector2? point)
        {
            var camera = Camera.main;
            var at = point.HasValue && camera != null ? PanelPoint(camera, SimSpace.ToWorld(point.Value, 0.1f)) : null;
            Ui.Show(pushMarker, at.HasValue);
            if (!at.HasValue) return;
            pushMarker.style.left = at.Value.x - 22f;
            pushMarker.style.top = at.Value.y - 22f;
        }

        /// <summary>A world point as a position in the tag layer, or null if it's behind the camera.</summary>
        Vector2? PanelPoint(Camera camera, Vector3 world)
        {
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return null;
            var s = PanelScale;
            var origin = tagLayer.worldBound.position;
            return new Vector2(screen.x / s - origin.x, (Screen.height - screen.y) / s - origin.y);
        }

        void DrawSticks(MeshGenerationContext context)
        {
            var touch = Game.Router.Touch;
            var mySeat = Game.Router.SeatForSlot(0);
            if (Flow.Paused || mySeat < 0) return;
            var painter = context.painter2D;
            var s = PanelScale;
            var origin = sticks.worldBound.position;
            var radius = CrewInputRouter.StickRadius / s;
            for (var i = 0; i < (touch.TwoSticks ? 2 : 1); i++)
            {
                Vector2 centre, knob;
                if (touch.StickActive[i])
                {
                    centre = new Vector2(touch.StickOrigin[i].x / s, (Screen.height - touch.StickOrigin[i].y) / s) - origin;
                    knob = new Vector2(touch.StickKnob[i].x / s, (Screen.height - touch.StickKnob[i].y) / s) - origin;
                }
                else
                {
                    // Resting: a faint base where a thumb naturally lands, so new players know where to steer.
                    var onRight = touch.TwoSticks ? i == 1 : touch.LeftHanded;
                    var rect = sticks.contentRect;
                    centre = new Vector2(onRight ? rect.width - 260f : 260f, rect.height - 250f);
                    knob = centre;
                }

                var alpha = touch.StickActive[i] ? 1f : 0.55f;
                painter.fillColor = new Color(1f, 1f, 1f, 0.16f * alpha);
                painter.BeginPath();
                painter.Arc(centre, radius, 0f, 360f);
                painter.Fill();
                painter.strokeColor = new Color(1f, 1f, 1f, 0.35f * alpha);
                painter.lineWidth = 5f;
                painter.BeginPath();
                painter.Arc(centre, radius, 0f, 360f);
                painter.Stroke();
                painter.fillColor = new Color(1f, 1f, 1f, 0.6f * alpha);
                painter.BeginPath();
                painter.Arc(knob, radius * 0.38f, 0f, 360f);
                painter.Fill();
            }
        }

        /// <summary>The power-up's icon: a fizzing can, a bubble, or an air horn.</summary>
        void DrawItem(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var painter = context.painter2D;
            var c = rect.center + new Vector2(0f, -12f);
            var s = Mathf.Min(rect.width, rect.height) / 156f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
            switch (shownItem)
            {
                case PowerUp.Turbo:
                    painter.fillColor = new Color(1f, 0.416f, 0.169f);
                    painter.BeginPath();
                    painter.MoveTo(c + new Vector2(-20f, -36f) * s);
                    painter.LineTo(c + new Vector2(20f, -36f) * s);
                    painter.LineTo(c + new Vector2(20f, 34f) * s);
                    painter.LineTo(c + new Vector2(-20f, 34f) * s);
                    painter.ClosePath();
                    painter.Fill();
                    painter.fillColor = new Color(1f, 0.92f, 0.3f); // a lightning bolt on the can
                    painter.BeginPath();
                    painter.MoveTo(c + new Vector2(4f, -26f) * s);
                    painter.LineTo(c + new Vector2(-10f, 4f) * s);
                    painter.LineTo(c + new Vector2(1f, 4f) * s);
                    painter.LineTo(c + new Vector2(-4f, 26f) * s);
                    painter.LineTo(c + new Vector2(11f, -6f) * s);
                    painter.LineTo(c + new Vector2(0f, -6f) * s);
                    painter.ClosePath();
                    painter.Fill();
                    break;
                case PowerUp.Shield:
                    painter.fillColor = new Color(0.55f, 0.9f, 1f, 0.35f);
                    painter.BeginPath();
                    painter.Arc(c, 36f * s, 0f, 360f);
                    painter.Fill();
                    painter.strokeColor = new Color(0.7f, 0.95f, 1f);
                    painter.lineWidth = 5f * s;
                    painter.BeginPath();
                    painter.Arc(c, 36f * s, 0f, 360f);
                    painter.Stroke();
                    painter.strokeColor = Color.white; // the shine
                    painter.BeginPath();
                    painter.Arc(c, 24f * s, 200f, 250f);
                    painter.Stroke();
                    break;
                case PowerUp.Horn:
                    painter.fillColor = new Color(1f, 0.82f, 0.3f);
                    painter.BeginPath(); // the bell flaring to the right
                    painter.MoveTo(c + new Vector2(-14f, -8f) * s);
                    painter.LineTo(c + new Vector2(30f, -30f) * s);
                    painter.LineTo(c + new Vector2(30f, 30f) * s);
                    painter.LineTo(c + new Vector2(-14f, 8f) * s);
                    painter.ClosePath();
                    painter.Fill();
                    painter.fillColor = new Color(0.9f, 0.25f, 0.2f); // the squeeze bulb
                    painter.BeginPath();
                    painter.Arc(c + new Vector2(-24f, 0f) * s, 14f * s, 0f, 360f);
                    painter.Fill();
                    break;
            }
        }

        /// <summary>The look stick: a ring with an eye, its knob following the drag while looking round.</summary>
        void DrawLook(MeshGenerationContext context)
        {
            var touch = Game.Router.Touch;
            var rect = context.visualElement.contentRect;
            var painter = context.painter2D;
            var centre = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f - 4f;
            var held = touch.Held.Contains(TouchControls.LookButton) || Game.Router.Look.sqrMagnitude > 0.0004f;
            var alpha = held ? 1f : 0.6f;
            painter.fillColor = new Color(0.047f, 0.055f, 0.07f, 0.45f * alpha);
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Fill();
            painter.strokeColor = new Color(1f, 1f, 1f, 0.45f * alpha);
            painter.lineWidth = 4f;
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Stroke();

            // The eye: two arcs and a pupil, nudged the way the view is swung.
            var look = Game.Router.Look;
            var eye = centre + new Vector2(look.x, -look.y) * (radius * 0.45f);
            var w = radius * 0.62f;
            var h = radius * 0.34f;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.85f * alpha);
            painter.lineWidth = 5f;
            painter.BeginPath();
            painter.MoveTo(eye + new Vector2(-w, 0f));
            painter.QuadraticCurveTo(eye + new Vector2(0f, -h * 2f), eye + new Vector2(w, 0f));
            painter.QuadraticCurveTo(eye + new Vector2(0f, h * 2f), eye + new Vector2(-w, 0f));
            painter.Stroke();
            painter.fillColor = held ? new Color(1f, 0.416f, 0.169f) : new Color(1f, 1f, 1f, 0.85f);
            painter.BeginPath();
            painter.Arc(eye, h * 0.75f, 0f, 360f);
            painter.Fill();
        }

        /// <summary>The burst button's ring: how much air is left for bursts.</summary>
        void DrawAir(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var painter = context.painter2D;
            var centre = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f - 8f;
            painter.lineWidth = 12f;
            painter.lineCap = LineCap.Round;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.15f);
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.Stroke();
            if (air <= 0.01f) return;
            painter.strokeColor = new Color(1f, 0.416f, 0.169f);
            painter.BeginPath();
            painter.Arc(centre, radius, -90f, -90f + 360f * Mathf.Clamp01(air));
            painter.Stroke();
        }
    }
}
