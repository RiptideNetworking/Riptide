// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's server must implement.</summary>
    public interface IServer : IPeer
    {
        /// <summary>Invoked when a new connection attempt is received by the transport.</summary>
        event EventHandler<ConnectingEventArgs> Connecting;
        /// <summary>Invoked when a connection is established at the transport level.</summary>
        event EventHandler<ConnectedEventArgs> Connected;

        /// <inheritdoc cref="Server.Port"/>
        ushort Port { get; }
        
        /// <summary>Starts the transport and begins listening for incoming connections.</summary>
        /// <param name="port">The local port on which to listen for connections.</param>
        void Start(ushort port);
        
        /// <summary>Accepts a pending connection.</summary>
        /// <param name="connection">The connection to accept.</param>
        void Accept(Connection connection);

        /// <summary>Rejects a pending connection.</summary>
        /// <param name="connection">The connection to reject.</param>
        void Reject(Connection connection);

        /// <summary>Closes an active connection.</summary>
        /// <param name="connection">The connection to close.</param>
        void Close(Connection connection);

        /// <summary>Closes all existing connections and stops listening for new connections.</summary>
        void Shutdown();
    }
}
