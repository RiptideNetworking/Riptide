// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

namespace Riptide.Transports
{
    /// <summary>Defines methods, properties, and events which every transport's connections must implement.</summary>
    public interface IConnectionInfo
    {
        /// <summary>The numeric ID of the client.</summary>
        ushort Id { get; }
        /// <summary>The round trip time of the connection. -1 if not calculated yet.</summary>
        short RTT { get; }
        /// <summary>The smoothed round trip time of the connection. -1 if not calculated yet.</summary>
        short SmoothRTT { get; }
        /// <summary>Whether or not the client is currently <i>not</i> connected nor trying to connect.</summary>
        bool IsNotConnected { get; }
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        bool IsConnecting { get; }
        /// <summary>Whether or not the client is currently connected.</summary>
        bool IsConnected { get; }
    }
}
