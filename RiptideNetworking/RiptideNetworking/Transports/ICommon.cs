// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide.Transports
{
    /// <summary>The header type of a <see cref="Message"/>.</summary>
    public enum HeaderType : byte
    {
        /// <summary>For unreliable user messages.</summary>
        unreliable,
        /// <summary>For unreliable user messages that servers should automatically relay to all other clients.</summary>
        unreliableAutoRelay = unreliable + 1,
        /// <summary>For unreliable internal ack messages.</summary>
        ack,
        /// <summary>For unreliable internal ack messages (when acknowledging a sequence ID other than the last received one).</summary>
        ackExtra,
        /// <summary>For unreliable internal connect messages.</summary>
        connect,
        /// <summary>For unreliable internal heartbeat messages.</summary>
        heartbeat,
        /// <summary>For unreliable internal disconnect messages.</summary>
        disconnect,

        /// <summary>For reliable user messages.</summary>
        reliable,
        /// <summary>For reliable user messages that servers should automatically relay to all other clients.</summary>
        reliableAutoRelay = reliable + 1,
        /// <summary>For reliable internal welcome messages.</summary>
        welcome,
        /// <summary>For reliable internal client connected messages.</summary>
        clientConnected,
        /// <summary>For reliable internal client disconnected messages.</summary>
        clientDisconnected,
    }

    /// <summary>Defines methods, properties, and events which every transport's server and client must implement.</summary>
    public interface ICommon
    {
        event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <summary>Initiates handling of currently queued messages.</summary>
        /// <remarks>This should generally be called from within a regularly executed update loop (like FixedUpdate in Unity). Messages will continue to be received in between calls, but won't be handled fully until this method is executed.</remarks>
        void Tick();
    }
}
