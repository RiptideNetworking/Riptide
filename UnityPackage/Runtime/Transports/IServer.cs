
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace RiptideNetworking.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's server must implement.</summary>
    public interface IServer : ICommon
    {
        /// <summary>Invoked when a new client connects.</summary>
        event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a message is received from a client.</summary>
        event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when a client disconnects.</summary>
        event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>The local port that the server is running on.</summary>
        ushort Port { get; }
        /// <summary>The maximum number of clients that can be connected at any time.</summary>
        ushort MaxClientCount { get; }
        /// <summary>The number of currently connected clients.</summary>
        int ClientCount { get; }
        /// <summary>An array of all the currently connected clients.</summary>
        /// <remarks>The position of each <see cref="IConnectionInfo"/> instance in the array does <i>not</i> correspond to that client's numeric ID (except by coincidence).</remarks>
        IConnectionInfo[] Clients { get; }
        /// <summary>Whether or not to allow messages to be automatically sent to all other connected clients.</summary>
        /// <remarks>This should never be enabled if you want to maintain server authority, as it theoretically allows hacked clients to tell your <see cref="Server"/> instance to automatically distribute any message to other clients.
        /// However, it's extremely handy when building client-authoritative games where the <see cref="Server"/> instance acts mostly as a relay and is directly forwarding most messages to other clients anyways.</remarks>
        bool AllowAutoMessageRelay { get; set; }

        /// <summary>Starts the server.</summary>
        /// <param name="port">The local port on which to start the server.</param>
        /// <param name="maxClientCount">The maximum number of concurrent connections to allow.</param>
        void Start(ushort port, ushort maxClientCount);
        /// <summary>Sends a message to a specific client.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="toClientId">The numeric ID of the client to send the message to.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        void Send(Message message, ushort toClientId, bool shouldRelease);
        /// <summary>Sends a message to all conected clients.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        void SendToAll(Message message, bool shouldRelease);
        /// <summary>Sends a message to all connected clients except one.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="exceptToClientId">The numeric ID of the client to <i>not</i> send the message to.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        void SendToAll(Message message, ushort exceptToClientId, bool shouldRelease);
        /// <summary>Kicks a specific client.</summary>
        /// <param name="clientId">The numeric ID of the client to kick.</param>
        void DisconnectClient(ushort clientId);
        /// <summary>Disconnects all clients and stops listening for new connections.</summary>
        void Shutdown();
    }
}
