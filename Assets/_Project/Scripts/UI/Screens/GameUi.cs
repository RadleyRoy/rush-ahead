using System;
using System.Collections.Generic;
using RushAhead.Render;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RushAhead.UI
{
    /// <summary>
    /// Every screen of the game, drawn with UI Toolkit over the 3D world. Each frame it tells the <see cref="AppFlow"/>
    /// what the game is doing, shows the screen the flow asks for, keeps content inside the phone's safe area, points
    /// the camera, and handles the back button. Polling once a frame means nothing runs inside network callbacks.
    /// </summary>
    public sealed class GameUi : MonoBehaviour
    {
        readonly Dictionary<AppScreen, GameScreen> screens = new();
        readonly Dictionary<string, (VisualElement element, Action action)> named = new();
        UIDocument document;
        VisualElement root;
        VisualElement safe;
        VisualElement modalLayer;
        Label toast;
        float toastUntil;
        Rect appliedSafeArea;
        Vector2Int appliedScreen;
        GameScreen shown;
        ShowcaseCamera showcase;
        ChaseCamera chase;
        DressingRoom booth;

        public GameController Game { get; private set; }
        public AppFlow Flow { get; } = new();

        /// <summary>Which seat Your animal dresses: -1 for this player's own animal, or a spare seat the host dresses.</summary>
        public int DressingSeat { get; set; } = -1;

        public GameSettings Settings { get; private set; }

        /// <summary>The race camera, for shaking.</summary>
        public ChaseCamera Chase => chase;

        /// <summary>Nothing steers unless the race is on screen with no pause menu or dialog over it.</summary>
        public bool InputBlocked => Flow.Screen != AppScreen.Race || Flow.Paused || ModalOpen;

        /// <summary>Hides every screen (for a clean shot of the course from above).</summary>
        public bool Visible
        {
            get => root.style.display != DisplayStyle.None;
            set => root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Scripted tours run the game faster than life (only ever set by them).</summary>
        public static float DemoSpeed = 1f;

        /// <summary>A faked notch inset (left, top, right, bottom in screen pixels) for screenshots on a desktop.</summary>
        public static RectOffset FakeInset;

        public static GameUi Create(GameController game, PanelSettings panelSettings)
        {
            var host = new GameObject("UI");
            host.SetActive(false); // so the document has its panel before it first enables
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            var ui = host.AddComponent<GameUi>();
            ui.Game = game;
            ui.document = document;
            host.SetActive(true);
            GameAudio.Create(); // the sound effects take a moment to make: done while the title shows
            Ui.Clicked += () => GameAudio.Instance?.Play(GameAudio.Instance.Bank.Click, 0.5f);
            ui.Settings = GameSettings.Load();
            ui.ApplySettings();
            ui.BuildAll();
            return ui;
        }

        void BuildAll()
        {
            root = document.rootVisualElement;
            root.AddToClassList("root");
            root.pickingMode = PickingMode.Ignore;

            safe = Ui.El("layer");
            safe.pickingMode = PickingMode.Ignore;
            root.Add(safe);
            modalLayer = Ui.El("layer");
            modalLayer.pickingMode = PickingMode.Ignore;
            root.Add(modalLayer);
            toast = Ui.Text("", "toast");
            toast.pickingMode = PickingMode.Ignore;
            Ui.Show(toast, false);
            root.Add(toast);

            Add(AppScreen.Title, new TitleScreen(this));
            Add(AppScreen.MainMenu, new MainMenuScreen(this));
            Add(AppScreen.Host, new HostScreen(this));
            Add(AppScreen.Join, new JoinScreen(this));
            Add(AppScreen.Connecting, new ConnectingScreen(this));
            Add(AppScreen.Lobby, new LobbyScreen(this));
            Add(AppScreen.Race, new RaceHud(this));
            Add(AppScreen.Results, new ResultsScreen(this));
            Add(AppScreen.YourAnimal, new YourAnimalScreen(this));
            Add(AppScreen.Settings, new SettingsScreen(this));

            var camera = Camera.main;
            chase = camera.GetComponent<ChaseCamera>();
            showcase = camera.gameObject.AddComponent<ShowcaseCamera>();
            showcase.enabled = false;
        }

        /// <summary>The developer tools (tuning sliders, every course open): development builds only, never the store's.</summary>
        public bool DeveloperTools => Settings.developerTools && Debug.isDebugBuild;

        /// <summary>Puts the settings into effect, and shows or hides the developer tools.</summary>
        public void ApplySettings()
        {
            Settings.Apply(Game.Router);
            var dev = Game.GetComponent<DevOverlay>();
            var wanted = DeveloperTools || CommandLine.Has("-rushahead-dev");
            if (wanted && dev == null) Game.gameObject.AddComponent<DevOverlay>().Game = Game;
            if (dev != null) dev.enabled = wanted;
        }

        void Add(AppScreen which, GameScreen screen)
        {
            screens[which] = screen;
            screen.Build();
            Ui.Show(screen.Root, false);
            safe.Add(screen.Root);
        }

        void Update()
        {
            Flow.Sync(Game.Session, Game.SessionReady, Game.Phase, Game.SessionsEnded, Game.SessionEndReason);
            var wanted = screens.TryGetValue(Flow.Screen, out var screen) ? screen : null;
            if (wanted != shown)
            {
                if (shown != null)
                {
                    shown.OnHide();
                    Ui.Show(shown.Root, false);
                }

                shown = wanted;
                if (shown != null)
                {
                    Ui.Show(shown.Root, true);
                    shown.OnShow();
                }
            }

            shown?.Refresh();
            Time.timeScale = Flow.Paused && Game.Session == SessionKind.Solo ? 0f : DemoSpeed;
            var audio = GameAudio.Instance;
            if (audio != null)
            {
                audio.SetMusic(Flow.Screen == AppScreen.Race && !Flow.Paused);
                audio.SetAmbience(audio.Bank.AmbienceFor(Game.CurrentCourse != null ? Game.CurrentCourse.Def.scene : null));
            }
            UpdateSafeArea();
            UpdateCamera();
            if (Time.unscaledTime > toastUntil) Ui.Show(toast, false);

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Back(); // also Android's back button
        }

        /// <summary>The back button, wherever the player is.</summary>
        public void Back()
        {
            if (modalLayer.childCount > 0)
            {
                CloseModal();
                return;
            }

            switch (Flow.Back())
            {
                case BackAction.CancelConnecting:
                    Game.EndSession();
                    break;
                case BackAction.ConfirmLeave:
                    ConfirmLeave();
                    break;
            }
        }

        /// <summary>Asks before ending the game: a host ends it for everyone.</summary>
        public void ConfirmLeave()
        {
            var hosting = Game.Session == SessionKind.Hosting;
            Confirm(hosting ? "End the game for everyone?" : Game.Session == SessionKind.Joined ? "Leave this game?" : "Back to the main menu?",
                hosting ? "Everyone who joined goes back to their menus." : null,
                hosting ? "End game" : "Leave", () => Game.EndSession());
        }

        public void Confirm(string title, string body, string yes, Action onYes)
        {
            CloseModal();
            var dialog = Ui.El("panel dialog",
                Ui.Text(title, "h2"),
                Ui.Spacer());
            if (body != null) dialog.Add(Ui.Text(body, "body muted"));
            dialog.Add(Ui.Spacer());
            var buttons = Ui.El("row");
            buttons.Add(Ui.Btn("Cancel", CloseModal, "btn btn--small"));
            buttons.Add(Ui.Grow());
            buttons.Add(Ui.Btn(yes, () =>
            {
                CloseModal();
                onYes();
            }, "btn btn--small btn--primary"));
            dialog.Add(buttons);
            var scrim = Ui.El("layer scrim center", dialog);
            modalLayer.Add(scrim);
        }

        public bool ModalOpen => modalLayer.childCount > 0;

        void CloseModal() => modalLayer.Clear();

        public void Toast(string text, float seconds = 2.5f)
        {
            toast.text = text;
            Ui.Show(toast, true);
            toastUntil = Time.unscaledTime + seconds;
        }

        public void Register(string name, VisualElement element, Action action) => named[name] = (element, action);

        /// <summary>Presses a named control the way a player would, if it's on screen and enabled. For the scripted demo.</summary>
        public bool Press(string name)
        {
            if (!named.TryGetValue(name, out var entry)) return false;
            for (var e = entry.element; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
            if (!entry.element.enabledInHierarchy) return false;
            entry.action();
            return true;
        }

        /// <summary>The stage Your animal shows the animal on, built the first time it's needed.</summary>
        public DressingRoom Booth => booth ??= DressingRoom.Build(Game.Palette, Game.Catalog);

        /// <summary>Keeps every screen clear of notches and rounded corners; backgrounds still run edge to edge.</summary>
        void UpdateSafeArea()
        {
            var area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (area == appliedSafeArea && size == appliedScreen) return;
            appliedSafeArea = area;
            appliedScreen = size;

            float left = area.xMin, right = size.x - area.xMax, bottom = area.yMin, top = size.y - area.yMax;
            if (FakeInset != null)
            {
                left += FakeInset.left;
                right += FakeInset.right;
                top += FakeInset.top;
                bottom += FakeInset.bottom;
            }

            var scale = size.y > 0 ? 1080f / size.y : 1f; // the panel is 1080 tall whatever the screen
            safe.style.left = left * scale;
            safe.style.right = right * scale;
            safe.style.top = top * scale;
            safe.style.bottom = bottom * scale;
        }

        /// <summary>Menus show off the raft from outside; races follow it from behind.</summary>
        void UpdateCamera()
        {
            var shot = Flow.Screen switch
            {
                AppScreen.Race => (ShowcaseCamera.Shot?)null,
                AppScreen.Lobby or AppScreen.Connecting => ShowcaseCamera.Shot.Front,
                AppScreen.YourAnimal => ShowcaseCamera.Shot.Booth,
                _ => ShowcaseCamera.Shot.Orbit,
            };

            if (shot == null)
            {
                if (showcase.enabled)
                {
                    showcase.enabled = false;
                    chase.enabled = true;
                    var boat = Game.ViewedBoat;
                    if (boat != null) chase.SnapTo(boat.RenderPosition, boat.RenderAngleRad);
                }

                return;
            }

            if (!showcase.enabled)
            {
                chase.enabled = false;
                showcase.enabled = true;
            }

            showcase.Mode = shot.Value;
            if (shot == ShowcaseCamera.Shot.Booth)
            {
                showcase.BoothFocus = Booth.Focus;
                showcase.BoothFacing = Booth.Facing;
            }

            // Leave the subject where the screen's panels don't cover it: most screens keep their panels on the right.
            showcase.ScreenOffset = Flow.Screen switch
            {
                AppScreen.MainMenu or AppScreen.Title or AppScreen.Results => -0.42f,
                AppScreen.YourAnimal => -0.5f,
                _ => 0f,
            };
            showcase.Subject = Game.ShowcaseSubject;
        }
    }
}
