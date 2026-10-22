using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RushAhead.Net
{
    /// <summary>
    /// Real networking over Netcode for GameObjects and Unity Transport (UDP). NGO is used purely as a connection and
    /// delivery layer through one named-message channel; there are no NetworkObjects, so the game's compact snapshot
    /// stays in charge of what goes over the wire.
    /// </summary>
    public sealed class NgoTransport : MonoBehaviour, INetTransport
    {
        const string Channel = "rushahead";
        public const ushort DefaultPort = 7777;

        NetworkManager manager;
        UnityTransport unityTransport;

        public bool IsHost { get; private set; }

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, ArraySegment<byte>> Received;

        /// <param name="listenAddress">0.0.0.0 to accept LAN players; 127.0.0.1 for same-machine testing only.</param>
        public static NgoTransport StartHost(ushort port = DefaultPort, string listenAddress = "0.0.0.0")
        {
            var transport = Create("Network (host)");
            transport.IsHost = true;
            transport.unityTransport.SetConnectionData("127.0.0.1", port, listenAddress);
            if (!transport.manager.StartHost())
            {
                Destroy(transport.gameObject);
                throw new InvalidOperationException($"Could not host on port {port}. Is another game already using it?");
            }

            transport.Listen();
            return transport;
        }

        public static NgoTransport StartClient(string hostAddress, ushort port = DefaultPort)
        {
            var transport = Create("Network (client)");
            transport.unityTransport.SetConnectionData(hostAddress, port);
            if (!transport.manager.StartClient())
            {
                Destroy(transport.gameObject);
                throw new InvalidOperationException($"Could not start connecting to {hostAddress}:{port}.");
            }

            transport.Listen();
            return transport;
        }

        static NgoTransport Create(string name)
        {
            var go = new GameObject(name);
            var transport = go.AddComponent<NgoTransport>();
            transport.unityTransport = go.AddComponent<UnityTransport>();
            transport.manager = go.AddComponent<NetworkManager>();
            transport.manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport.unityTransport,
                EnableSceneManagement = false, // the game builds its own scene; NGO only carries messages
                ConnectionApproval = false,
            };
            return transport;
        }

        void Listen()
        {
            manager.CustomMessagingManager.RegisterNamedMessageHandler(Channel, OnMessage);
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
        }

        void OnConnected(ulong clientId)
        {
            if (!IsHost) PeerConnected?.Invoke(Peers.Host);
            else if (clientId != NetworkManager.ServerClientId) PeerConnected?.Invoke(clientId); // the host's own connection isn't a peer
        }

        void OnDisconnected(ulong clientId)
        {
            if (!IsHost) PeerDisconnected?.Invoke(Peers.Host);
            else if (clientId != NetworkManager.ServerClientId) PeerDisconnected?.Invoke(clientId);
        }

        void OnMessage(ulong sender, FastBufferReader reader)
        {
            var count = reader.Length - reader.Position;
            var bytes = new byte[count];
            reader.ReadBytesSafe(ref bytes, count);
            Received?.Invoke(IsHost ? sender : Peers.Host, new ArraySegment<byte>(bytes));
        }

        public void Send(ulong peer, ArraySegment<byte> message, bool reliable)
        {
            if (manager == null || !manager.IsListening) return;
            using var writer = new FastBufferWriter(message.Count, Allocator.Temp);
            writer.WriteBytesSafe(message.Array, message.Count, message.Offset);
            var target = IsHost ? peer : NetworkManager.ServerClientId;
            var delivery = reliable ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced;
            manager.CustomMessagingManager.SendNamedMessage(Channel, target, writer, delivery);
        }

        public void Shutdown()
        {
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= OnConnected;
                manager.OnClientDisconnectCallback -= OnDisconnected;
                if (manager.IsListening) manager.Shutdown();
            }

            if (this != null) Destroy(gameObject); // when the whole app is closing, Unity may already have destroyed it
        }
    }
}
