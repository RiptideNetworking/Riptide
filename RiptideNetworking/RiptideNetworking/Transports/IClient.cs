
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace RiptideNetworking.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's client must implement.</summary>
    public interface IClient : ICommon, IConnectionInfo
    {
        /// <summary>Invoked when a connection to the server is established.</summary>
        event EventHandler Connected;
        /// <summary>Invoked when a connection to the server fails to be established.</summary>
        /// <remarks>This occurs when a connection request fails, either because no server is listening on the expected IP and port, or because something (firewall, antivirus, no/poor internet access, etc.) is preventing the connection.</remarks>
        event EventHandler ConnectionFailed;
        /// <summary>Invoked when a message is received from the server.</summary>
        event EventHandler<ClientMessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when disconnected by the server.</summary>
        event EventHandler Disconnected;
        /// <summary>Invoked when a new client connects.</summary>
        event EventHandler<ClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a client disconnects.</summary>
        event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>Attempts to connect to the given host address.</summary>
        /// <param name="hostAddress">The host address to connect to.</param>
        /// <param name="message">A message containing data that should be sent to the server with the connection attempt. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        void Connect(string hostAddress, Message message);
        /// <summary>Sends a message to the server.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        void Send(Message message, bool shouldRelease);
        /// <summary>Disconnects from the server.</summary>
        void Disconnect();
    }
}
