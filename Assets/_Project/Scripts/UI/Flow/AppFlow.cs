using System.Collections.Generic;
using RushAhead.Net;

namespace RushAhead.UI
{
    public enum AppScreen
    {
        Title,
        MainMenu,
        Host,
        Join,
        Connecting,
        Lobby,
        Race,
        Results,
        YourAnimal,
        Settings,
    }

    /// <summary>What the back button (or Escape) asks the screens to do, beyond moving between them.</summary>
    public enum BackAction
    {
        /// <summary>Nothing to go back to.</summary>
        None,

        /// <summary>Done: the flow moved.</summary>
        Handled,

        /// <summary>Leaving would end the game for this player (or everyone, for a host): ask first.</summary>
        ConfirmLeave,

        /// <summary>Stop trying to join.</summary>
        CancelConnecting,
    }

    /// <summary>
    /// Which screen this device shows. While a game is on, the room's phase (the host's to decide) picks the lobby, the
    /// race or the results; around that, the player moves between the menus, and screens like Your animal and Settings
    /// open over whatever they came from. A game ending goes back to the main menu with a banner saying why, or, if it
    /// never got going, back to Join with the error.
    /// Pure C#: <see cref="Sync"/> is fed the game's state once a frame, and nothing here touches the network.
    /// </summary>
    public sealed class AppFlow
    {
        readonly Stack<AppScreen> returns = new();
        bool synced;
        SessionKind session;
        bool ready;
        RoomPhase phase;
        int ended;

        public AppScreen Screen { get; private set; } = AppScreen.Title;

        /// <summary>The race's pause menu is open. Solo, the game stops; with others aboard it carries on underneath.</summary>
        public bool Paused { get; private set; }

        /// <summary>Why the last game ended, for the main menu; null when the player left by choice or it's been dismissed.</summary>
        public string Banner { get; private set; }

        /// <summary>Why joining failed, for the Join screen.</summary>
        public string JoinError { get; private set; }

        public bool InGame => session != SessionKind.None;
        public SessionKind Session => session;

        /// <summary>Takes in the game's state; moves to the screen it calls for.</summary>
        /// <param name="sessionsEnded">A count of games ended, so an ending is noticed even if a new game starts the same frame.</param>
        /// <param name="endReason">Why the last game ended, if the player didn't choose it.</param>
        public void Sync(SessionKind nowSession, bool nowReady, RoomPhase nowPhase, int sessionsEnded, string endReason)
        {
            if (!synced)
            {
                synced = true;
                (session, ready, phase, ended) = (nowSession, nowReady, nowPhase, sessionsEnded);
                return;
            }

            if (sessionsEnded != ended)
            {
                ended = sessionsEnded;
                var wasConnecting = Screen == AppScreen.Connecting || (session == SessionKind.Joined && !ready);
                session = SessionKind.None;
                Ended(endReason, wasConnecting);
            }

            var started = session == SessionKind.None && nowSession != SessionKind.None;
            var phaseMoved = nowPhase != phase;
            var changed = started || nowSession != session || nowReady != ready || phaseMoved;
            (session, ready, phase) = (nowSession, nowReady, nowPhase);
            if (!changed || !InGame || Screen == AppScreen.Title) return;

            switch (Screen)
            {
                case AppScreen.YourAnimal:
                case AppScreen.Settings:
                    // Over the lobby these can stay open; a race starting (or ending) takes everyone to it.
                    if (phaseMoved && phase != RoomPhase.Lobby) GoGame();
                    else if (phaseMoved) RetargetReturns();
                    break;

                case AppScreen.MainMenu:
                case AppScreen.Host:
                case AppScreen.Join:
                    if (started) GoGame();
                    break;

                default:
                    GoGame();
                    break;
            }
        }

        public void TitleDone()
        {
            if (Screen != AppScreen.Title) return;
            if (InGame) GoGame();
            else Go(AppScreen.MainMenu);
        }

        public void OpenHost()
        {
            if (Screen == AppScreen.MainMenu) Go(AppScreen.Host);
        }

        public void OpenJoin()
        {
            if (Screen != AppScreen.MainMenu) return;
            JoinError = null;
            Go(AppScreen.Join);
        }

        public void OpenAnimal()
        {
            if (Screen is AppScreen.MainMenu or AppScreen.Lobby) Push(AppScreen.YourAnimal);
        }

        public void OpenSettings()
        {
            if (Screen is AppScreen.MainMenu or AppScreen.Lobby || (Screen == AppScreen.Race && Paused)) Push(AppScreen.Settings);
        }

        /// <summary>Closes Your animal or Settings, back to where they were opened from.</summary>
        public void Close()
        {
            if (Screen is not (AppScreen.YourAnimal or AppScreen.Settings)) return;
            var back = returns.Count > 0 ? returns.Pop() : InGame ? GameScreenNow() : AppScreen.MainMenu;
            if (!InGame && back is AppScreen.Lobby or AppScreen.Race or AppScreen.Results or AppScreen.Connecting) back = AppScreen.MainMenu;
            Screen = back;
        }

        public void Pause()
        {
            if (Screen == AppScreen.Race) Paused = true;
        }

        public void Resume() => Paused = false;

        public void DismissBanner() => Banner = null;

        /// <summary>The back button: closes, pauses or steps back, or says what the screens should ask or cancel.</summary>
        public BackAction Back()
        {
            switch (Screen)
            {
                case AppScreen.Title:
                    TitleDone();
                    return BackAction.Handled;
                case AppScreen.Host:
                case AppScreen.Join:
                    Go(AppScreen.MainMenu);
                    return BackAction.Handled;
                case AppScreen.YourAnimal:
                case AppScreen.Settings:
                    Close();
                    return BackAction.Handled;
                case AppScreen.Race:
                    Paused = !Paused;
                    return BackAction.Handled;
                case AppScreen.Connecting:
                    return BackAction.CancelConnecting;
                case AppScreen.Lobby:
                case AppScreen.Results:
                    return BackAction.ConfirmLeave;
                default:
                    return BackAction.None;
            }
        }

        void Ended(string reason, bool wasConnecting)
        {
            returns.Clear();
            Paused = false;
            if (wasConnecting && reason != null)
            {
                JoinError = reason;
                Banner = null;
                if (Screen != AppScreen.Title) Screen = AppScreen.Join;
                return;
            }

            Banner = reason;
            if (Screen != AppScreen.Title) Screen = wasConnecting ? AppScreen.Join : AppScreen.MainMenu;
        }

        AppScreen GameScreenNow() => !ready ? AppScreen.Connecting : phase switch
        {
            RoomPhase.Racing => AppScreen.Race,
            RoomPhase.Results => AppScreen.Results,
            _ => AppScreen.Lobby,
        };

        void GoGame()
        {
            returns.Clear();
            Go(GameScreenNow());
        }

        /// <summary>A screen open over the game returns to wherever the game is now, not where it was.</summary>
        void RetargetReturns()
        {
            if (returns.Count == 0) return;
            var bottom = returns.ToArray()[^1];
            if (bottom is not (AppScreen.Lobby or AppScreen.Race or AppScreen.Results or AppScreen.Connecting)) return;
            var kept = new List<AppScreen>(returns);
            kept[^1] = GameScreenNow();
            returns.Clear();
            for (var i = kept.Count - 1; i >= 0; i--) returns.Push(kept[i]);
        }

        void Go(AppScreen screen)
        {
            if (screen != AppScreen.Race) Paused = false;
            Screen = screen;
        }

        void Push(AppScreen screen)
        {
            returns.Push(Screen);
            Screen = screen;
        }
    }
}
