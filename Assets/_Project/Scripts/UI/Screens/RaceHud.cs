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
        Label best;
        VisualElement teamTrack;
        VisualElement redMarker;
        VisualElement blueMarker;
        Label teamLabel;
        Label centre;
        Label capsized;
        Label watching;
        VisualElement vignette;
        VisualElement burst;
        VisualElement brace;
        VisualElement orderBar;
        VisualElement sticks;
        VisualElement tagLayer;
        VisualElement pushMarker;
        VisualElement pauseMenu;
        Label pauseTitle;
        Button restart;
        Button toLobby;
        Button leave;
        float air = 1f;
        float finishedAt = -1f;
        bool subscribed;

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
            pushMarker = new Swatch(new Color(1f, 0.85f, 0.2f, 0.9f), 44f) { pickingMode = PickingMode.Ignore };
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
            clock.style.fontSize = 88;
            checkpoint = Ui.Text("", "small");
            split = Ui.Text("", "h3");
            clockPanel.Add(clock);
            clockPanel.Add(checkpoint);
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
            foreach (var marker in new[] { redMarker, blueMarker })
            {
                marker.style.position = Position.Absolute;
                marker.style.top = -8;
                lane.Add(marker);
            }

            teamTrack.Add(teamLabel);
            teamTrack.Add(lane);
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

            // Bottom right (left, left-handed): Burst with its air, and Brace.
            burst = Visual("Burst", "btn");
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
            orderBar.style.backgroundColor = new Color(0.035f, 0.086f, 0.19f, 0.8f);
            foreach (var (_, id, label) in Orders)
            {
                var button = Visual(label, "segment");
                orderButtons[id] = button;
                orderBar.Add(button);
            }

            Root.Add(orderBar);

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
            var label = Ui.Text(text, classes);
            label.pickingMode = PickingMode.Ignore;
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
            if (!subscribed) Game.Router.ButtonTapped += Tapped;
            subscribed = true;
        }

        public override void OnHide()
        {
            if (subscribed) Game.Router.ButtonTapped -= Tapped;
            subscribed = false;
            Game.Router.Touch.ClearButtons();
            Game.Router.Touch.CancelCapture();
        }

        void Tapped(string id)
        {
            if (id == "pause")
            {
                Flow.Pause();
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
            var boat = Game.ViewedBoat;
            var touch = Game.Router.Touch;
            var mySeat = Game.Router.SeatForSlot(0);
            var spectator = Game.Session == SessionKind.Joined && mySeat < 0;
            var twoSticks = Game.Router.SeatForSlot(1) >= 0 && Game.Router.SeatForSlot(1) != mySeat;
            touch.TwoSticks = twoSticks;

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
            Ui.Show(best, run.Active && !team && run.Best > 0f);
            if (run.Active) Ui.Set(best, $"Best {Ui.Time(run.Best)}");
            Ui.Show(teamTrack, team);
            if (team)
            {
                var redProgress = run.MyRaft == 0 ? run.MyProgress : run.RivalProgress;
                var blueProgress = run.MyRaft == 0 ? run.RivalProgress : run.MyProgress;
                redMarker.style.left = new Length(redProgress * 92f, LengthUnit.Percent);
                blueMarker.style.left = new Length(blueProgress * 92f, LengthUnit.Percent);
                Ui.Set(teamLabel, $"You're {GameController.TeamName(run.MyRaft)}   Red {Mathf.RoundToInt(redProgress * 100f)}%   Blue {Mathf.RoundToInt(blueProgress * 100f)}%");
            }

            // The big words in the middle.
            if (run.Finished && finishedAt < 0f) finishedAt = Time.unscaledTime;
            if (!run.Finished) finishedAt = -1f;
            var words = !run.Active ? null
                : run.Countdown > 0 ? run.Countdown.ToString()
                : run.SinceStart < 1f ? "GO!"
                : finishedAt >= 0f && Time.unscaledTime - finishedAt < 2.5f ? (run.TeamRace && run.Winner == run.MyRaft ? "YOU WIN!" : "FINISH!")
                : null;
            Ui.Show(centre, words != null);
            if (words != null) Ui.Set(centre, words);
            Ui.Show(capsized, boat != null && boat.Capsized);
            Ui.Show(watching, spectator);

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

            Ui.Show(pauseMenu, Flow.Paused);
            if (Flow.Paused) RefreshPauseMenu();

            UpdateKeys();
            UpdateTags();
            sticks.MarkDirtyRepaint();
            ReportButtons(controls && canBurst, controls, orders);
        }

        void RefreshPauseMenu()
        {
            var session = Game.Session;
            Ui.Set(pauseTitle, session == SessionKind.Solo ? "Paused" : "The race goes on");
            Ui.Show(restart, Game.CanEditRaft && Game.Run.Active);
            Ui.Show(toLobby, Game.CanEditRaft);
            leave.text = session == SessionKind.Solo ? "Main menu" : session == SessionKind.Hosting ? "End the game" : "Leave the game";
        }

        Loadout? MyLook()
        {
            foreach (var seat in Game.Seats)
                if (seat.Yours) return seat.Loadout;
            return null;
        }

        /// <summary>Tells the touch controls where the buttons are now, in screen pixels.</summary>
        void ReportButtons(bool withBurst, bool withBrace, bool withOrders)
        {
            var touch = Game.Router.Touch;
            touch.ClearButtons();
            touch.AddButton("pause", ScreenRect(pauseButton));
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
                    made.style.fontSize = 34;
                    made.style.textShadow = new TextShadow { offset = new Vector2(2f, 3f), color = new Color(0f, 0f, 0f, 0.7f) };
                    tagLayer.Add(made);
                    tags.Add(made);
                }

                var tag = tags[used++];
                var name = string.IsNullOrEmpty(entry.Loadout.Name) ? $"P{seat % crew + 1}" : entry.Loadout.Name;
                Ui.Set(tag, entry.Yours ? "You" : entry.Kept ? $"{name} (away)" : entry.Ai ? "AI" : name);
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
            painter.strokeColor = new Color(1f, 0.79f, 0.24f);
            painter.BeginPath();
            painter.Arc(centre, radius, -90f, -90f + 360f * Mathf.Clamp01(air));
            painter.Stroke();
        }
    }
}
