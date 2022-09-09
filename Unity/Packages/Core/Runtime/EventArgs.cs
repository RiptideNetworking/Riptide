// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide
{
    /// <summary>Contains event data for when a client connects to the server.</summary>
    public class ServerConnectedEventArgs : EventArgs
    {
        /// <summary>The newly connected client.</summary>
        public readonly Connection Client;

        /// <summary>Initializes event data.</summary>
        /// <param name="client">The newly connected client.</param>
        public ServerConnectedEventArgs(Connection client)
        {
            Client = client;
        }
    }

    /// <summary>Contains event data for when a client disconnects from the server.</summary>
    public class ServerDisconnectedEventArgs : EventArgs
    {
        /// <summary>The client that disconnected.</summary>
        public readonly Connection Client;
        /// <summary>The reason for the disconnection.</summary>
        public readonly DisconnectReason Reason;

        /// <summary>Initializes event data.</summary>
        /// <param name="client">The client that disconnected.</param>
        /// <param name="reason">The reason for the disconnection.</param>
        public ServerDisconnectedEventArgs(Connection client, DisconnectReason reason)
        {
            Client = client;
            Reason = reason;
        }
    }

    /// <summary>Contains event data for when a message is received.</summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        /// <summary>The connection from which the message was received.</summary>
        public readonly Connection FromConnection;
        /// <summary>The ID of the message.</summary>
        public readonly ushort MessageId;
        /// <summary>The received message.</summary>
        public readonly Message Message;

        /// <summary>Initializes event data.</summary>
        /// <param name="fromConnection">The connection from which the message was received.</param>
        /// <param name="messageId">The ID of the message.</param>
        /// <param name="message">The received message.</param>
        public MessageReceivedEventArgs(Connection fromConnection, ushort messageId, Message message)
        {
            FromConnection = fromConnection;
            MessageId = messageId;
            Message = message;
        }
    }

    /// <summary>Contains event data for when a connection attempt to a server fails.</summary>
    public class ConnectionFailedEventArgs : EventArgs
    {
        /// <summary>Additional data related to the failed connection attempt (if any).</summary>
        public readonly Message Message;

        /// <summary>Initializes event data.</summary>
        /// <param name="message">Additional data related to the failed connection attempt (if any).</param>
        public ConnectionFailedEventArgs(Message message) => Message = message;
    }

    /// <summary>Contains event data for when the client disconnects from a server.</summary>
    public class DisconnectedEventArgs : EventArgs
    {
        /// <summary>The reason for the disconnection.</summary>
        public readonly DisconnectReason Reason;
        /// <summary>Additional data related to the disconnection (if any).</summary>
        public readonly Message Message;

        /// <summary>Initializes event data.</summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <param name="message">Additional data related to the disconnection (if any).</param>
        public DisconnectedEventArgs(DisconnectReason reason, Message message)
        {
            Reason = reason;
            Message = message;
        }
    }

    /// <summary>Contains event data for when a non-local client connects to the server.</summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        /// <summary>The numeric ID of the client that connected.</summary>
        public readonly ushort Id;

        /// <summary>Initializes event data.</summary>
        /// <param name="id">The numeric ID of the client that connected.</param>
        public ClientConnectedEventArgs(ushort id) => Id = id;
    }

    /// <summary>Contains event data for when a non-local client disconnects from the server.</summary>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>The numeric ID of the client that disconnected.</summary>
        public readonly ushort Id;

        /// <summary>Initializes event data.</summary>
        /// <param name="id">The numeric ID of the client that disconnected.</param>
        public ClientDisconnectedEventArgs(ushort id) => Id = id;
    }
}
