// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide.Transports
{
    /// <summary>The header type of a <see cref="Message"/>.</summary>
    public enum HeaderType : byte
    {
        /// <summary>An unreliable user message.</summary>
        unreliable,
        /// <summary>An unreliable user message which servers should automatically relay to all other clients.</summary>
        unreliableAutoRelay = unreliable + 1,
        /// <summary>An internal unreliable ack message.</summary>
        ack,
        /// <summary>An internal unreliable ack message, used when acknowledging a sequence ID other than the last received one.</summary>
        ackExtra,
        /// <summary>An internal unreliable connect message.</summary>
        connect,
        /// <summary>An internal unreliable heartbeat message.</summary>
        heartbeat,
        /// <summary>An internal unreliable disconnect message.</summary>
        disconnect,

        /// <summary>A reliable user message.</summary>
        reliable,
        /// <summary>A reliable user message which servers should automatically relay to all other clients.</summary>
        reliableAutoRelay = reliable + 1,
        /// <summary>An internal reliable welcome message.</summary>
        welcome,
        /// <summary>An internal reliable client connected message.</summary>
        clientConnected,
        /// <summary>An internal reliable client disconnected message.</summary>
        clientDisconnected,
    }

    /// <summary>Defines methods, properties, and events which every transport's server <i>and</i> client must implement.</summary>
    public interface IPeer
    {
        /// <summary>Invoked when data is received by the transport.</summary>
        event EventHandler<DataReceivedEventArgs> DataReceived;
        /// <summary>Invoked when a disconnection is initiated or detected by the transport.</summary>
        event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>Initiates handling of any received messages.</summary>
        void Tick();
    }
}
