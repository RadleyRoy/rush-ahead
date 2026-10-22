using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>A short-lived player's view of an online game: its join code, and how to leave it.</summary>
    public sealed class OnlineRoom
    {
        readonly ISession session;

        internal OnlineRoom(ISession session) => this.session = session;

        /// <summary>What other players type to join, e.g. "BQ7K2X".</summary>
        public string Code => session.Code;

        /// <summary>Leaves the session (as host, closes it). Never throws: leaving must always work, even offline.</summary>
        public async Task LeaveAsync()
        {
            try
            {
                if (session.IsHost && session is IHostSession host) await host.DeleteAsync();
                else await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RushAhead] Leaving the online game: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Online play through Unity's Multiplayer Services. The host creates a private session that uses Unity Relay,
    /// which gives a short join code; others join with the code. Nobody needs an address, an open port or an account
    /// (players sign in anonymously). The session only arranges the Relay connection: its network handler hands us the
    /// Relay details and the game starts its own NGO transport with them, so everything above the transport (rosters,
    /// races, AI, kept seats) works exactly as on a LAN.
    /// </summary>
    public static class OnlineRooms
    {
        /// <summary>Four players plus room to watch.</summary>
        const int MaxPlayers = 8;

        /// <summary>
        /// Whether this build is linked to a Unity Cloud project. Without one, the services can't work at all, so the
        /// game says so up front instead of failing deep inside a sign-in.
        /// </summary>
        public static bool Available => !string.IsNullOrEmpty(Application.cloudProjectId);

        /// <summary>
        /// A separate anonymous identity, for running several copies on one PC: they share saved data, so without
        /// this they'd all be the same player, and a player can't join their own game. Set before going online.
        /// </summary>
        public static string Profile { get; set; }

        public static async Task<(NgoTransport transport, OnlineRoom room)> HostAsync()
        {
            await SignInAsync();
            var handler = new RelayHandler();
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true } // private: joined by code only, never listed
                .WithRelayNetwork()
                .WithNetworkHandler(handler);
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            return (handler.Transport ?? throw new InvalidOperationException("The online game started without a connection."), new OnlineRoom(session));
        }

        public static async Task<(NgoTransport transport, OnlineRoom room)> JoinAsync(string code)
        {
            code = CleanCode(code);
            if (code.Length == 0) throw new ArgumentException("Type the join code the host sees.");

            await SignInAsync();
            var handler = new RelayHandler();
            var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions().WithNetworkHandler(handler));
            return (handler.Transport ?? throw new InvalidOperationException("Joined, but no connection was made."), new OnlineRoom(session));
        }

        /// <summary>Codes are read out loud and typed on phones: ignore spaces, dashes and case.</summary>
        public static string CleanCode(string typed)
        {
            if (string.IsNullOrEmpty(typed)) return "";
            var code = new StringBuilder(typed.Length);
            foreach (var c in typed)
                if (char.IsLetterOrDigit(c)) code.Append(char.ToUpperInvariant(c));
            return code.ToString();
        }

        /// <summary>A sentence a player can act on, for anything that went wrong going online.</summary>
        public static string Explain(Exception e)
        {
            switch (e)
            {
                case SessionException { Error: SessionError.SessionNotFound or SessionError.SessionDeleted }:
                    return "No game with that code. Check it with the host; codes stop working once the host leaves.";
                case SessionException { Error: SessionError.RateLimitExceeded }:
                    return "Too many tries just now. Wait a few seconds and try again.";
                case ArgumentException:
                case NotSupportedException:
                    return e.Message;
                case AuthenticationException:
                case RequestFailedException:
                case SessionException:
                    return $"Couldn't reach Unity's online service ({e.Message}). Check the internet connection and try again.";
                default:
                    return $"Online play failed: {e.Message}";
            }
        }

        static async Task SignInAsync()
        {
            if (!Available)
                throw new NotSupportedException("Online play isn't set up in this build yet: it isn't linked to a Unity Cloud project. Play on Wi-Fi for now.");

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                if (!string.IsNullOrEmpty(Profile)) options.SetProfile(Profile);
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Takes the Relay details the session arranged and starts the game's own transport with them, in place of the
        /// SDK's built-in Netcode integration (which would want a scene NetworkManager it controls).
        /// </summary>
        sealed class RelayHandler : INetworkHandler
        {
            public NgoTransport Transport { get; private set; }

            public Task StartAsync(NetworkConfiguration configuration)
            {
                if (configuration.Type != NetworkType.Relay) throw new InvalidOperationException($"Expected a Relay network, got {configuration.Type}.");
                Transport = configuration.Role == NetworkRole.Client
                    ? NgoTransport.StartClient(configuration.RelayServerData)
                    : NgoTransport.StartHost(configuration.RelayServerData);
                return Task.CompletedTask;
            }

            // The game shuts its transport down itself when the player leaves (or the host goes), so there's nothing to do here.
            public Task StopAsync() => Task.CompletedTask;
        }
    }
}
