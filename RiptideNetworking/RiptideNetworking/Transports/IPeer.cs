// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide.Transports
{
    /// <summary>The header type of a <see cref="Message"/>.</summary>
    public enum MessageHeader : byte
    {
        /// <summary>An unreliable user message.</summary>
        Unreliable,
        /// <summary>An internal unreliable ack message.</summary>
        Ack,
        /// <summary>An internal unreliable ack message, used when acknowledging a sequence ID other than the last received one.</summary>
        AckExtra,
        /// <summary>An internal unreliable connect message.</summary>
        Connect,
        /// <summary>An internal unreliable connection rejection message.</summary>
        Reject,
        /// <summary>An internal unreliable heartbeat message.</summary>
        Heartbeat,
        /// <summary>An internal unreliable disconnect message.</summary>
        Disconnect,

        /// <summary>A reliable user message.</summary>
        Reliable,
        /// <summary>An internal reliable welcome message.</summary>
        Welcome,
        /// <summary>An internal reliable client connected message.</summary>
        ClientConnected,
        /// <summary>An internal reliable client disconnected message.</summary>
        ClientDisconnected,
    }

    /// <summary>Defines methods, properties, and events which every transport's server <i>and</i> client must implement.</summary>
    public interface IPeer
    {
        /// <summary>Invoked when data is received by the transport.</summary>
        event EventHandler<DataReceivedEventArgs> DataReceived;
        /// <summary>Invoked when a disconnection is initiated or detected by the transport.</summary>
        event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>Initiates handling of any received messages.</summary>
        void Poll();
    }
}
