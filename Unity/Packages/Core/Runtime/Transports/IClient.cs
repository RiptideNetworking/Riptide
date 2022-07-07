// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's client must implement.</summary>
    public interface IClient : IPeer
    {
        event EventHandler Connected;
        event EventHandler ConnectionFailed;

        /// <summary>Attempts to connect to the given host address.</summary>
        /// <param name="hostAddress">The host address to connect to.</param>
        /// <param name="message">A message containing data that should be sent to the server with the connection attempt. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        /// <returns><see langword="true"/> if the <paramref name="hostAddress"/> was in a valid format; otherwise <see langword="false"/>.</returns>
        bool Connect(string hostAddress, out Connection connection, out string connectError);
        /// <summary>Disconnects from the server.</summary>
        void Disconnect();
    }
}
