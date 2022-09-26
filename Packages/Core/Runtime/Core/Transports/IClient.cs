// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's client must implement.</summary>
    public interface IClient : IPeer
    {
        /// <summary>Invoked when a connection is established at the transport level.</summary>
        event EventHandler Connected;
        /// <summary>Invoked when a connection attempt fails at the transport level.</summary>
        event EventHandler ConnectionFailed;

        /// <summary>Starts the transport and attempts to connect to the given host address.</summary>
        /// <param name="hostAddress">The host address to connect to.</param>
        /// <param name="connection">The pending connection. <see langword="null"/> if an issue occurred.</param>
        /// <param name="connectError">The error message associated with the issue that occurred, if any.</param>
        /// <returns><see langword="true"/> if a connection attempt will be made. <see langword="false"/> if an issue occurred (such as <paramref name="hostAddress"/> being in an invalid format) and a connection attempt will <i>not</i> be made.</returns>
        bool Connect(string hostAddress, out Connection connection, out string connectError);

        /// <summary>Closes the connection to the server.</summary>
        void Disconnect();
    }
}
