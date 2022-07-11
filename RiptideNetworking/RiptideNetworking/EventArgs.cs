// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide
{
    /// <summary>Contains event data for when a client connects to the server.</summary>
    public class ServerClientConnectedEventArgs : EventArgs
    {
        /// <summary>The newly connected client.</summary>
        public readonly Connection Client;
        /// <summary>A message containing any custom data the client included when it connected.</summary>
        public readonly Message ConnectMessage;

        /// <summary>Initializes event data.</summary>
        /// <param name="client">The newly connected client.</param>
        /// <param name="connectMessage">A message containing any custom data the client included when it connected.</param>
        public ServerClientConnectedEventArgs(Connection client, Message connectMessage)
        {
            Client = client;
            ConnectMessage = connectMessage;
        }
    }

    /// <summary>Contains event data for when the server receives a message from a client.</summary>
    public class ServerMessageReceivedEventArgs : EventArgs
    {
        /// <summary>The client from which the message was received.</summary>
        public readonly Connection FromClient;
        /// <summary>The ID of the message.</summary>
        public readonly ushort MessageId;
        /// <summary>The received message.</summary>
        public readonly Message Message;

        /// <summary>Initializes event data.</summary>
        /// <param name="fromClient">The client from which the message was received.</param>
        /// <param name="messageId">The ID of the message.</param>
        /// <param name="message">The received message.</param>
        public ServerMessageReceivedEventArgs(Connection fromClient, ushort messageId, Message message)
        {
            FromClient = fromClient;
            MessageId = messageId;
            Message = message;
        }
    }

    /// <summary>Contains event data for when a client disconnects from the server.</summary>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>The numeric ID of the client that disconnected.</summary>
        public readonly ushort Id;

        /// <summary>Initializes event data.</summary>
        /// <param name="id">The numeric ID of the client that disconnected.</param>
        public ClientDisconnectedEventArgs(ushort id) => Id = id;
    }

    /// <summary>Contains event data for when a new client connects.</summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        /// <summary>The numeric ID of the newly connected client.</summary>
        public readonly ushort Id;

        /// <summary>Initializes event data.</summary>
        /// <param name="id">The numeric ID of the newly connected client.</param>
        public ClientConnectedEventArgs(ushort id) => Id = id;
    }

    /// <summary>Contains event data for when the client receives a message from the server.</summary>
    public class ClientMessageReceivedEventArgs : EventArgs
    {
        /// <summary>The ID of the message.</summary>
        public readonly ushort MessageId;
        /// <summary>The received message.</summary>
        public readonly Message Message;

        /// <summary>Initializes event data.</summary>
        /// <param name="messageId">The ID of the message.</param>
        /// <param name="message">The received message.</param>
        public ClientMessageReceivedEventArgs(ushort messageId, Message message)
        {
            MessageId = messageId;
            Message = message;
        }
    }

    /// <summary>Contains event data for when the client disconnects from a server.</summary>
    public class DisconnectedEventArgs : EventArgs
    {
        /// <summary>The reason for the disconnection.</summary>
        public readonly DisconnectReason Reason;
        /// <summary>The custom reason included with the disconnection information (if any).</summary>
        public readonly string CustomMessage;

        /// <summary>Initializes event data.</summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <param name="customMessage">The custom reason included with the disconnection information (if any).</param>
        public DisconnectedEventArgs(DisconnectReason reason, string customMessage = "")
        {
            Reason = reason;
            CustomMessage = customMessage;
        }
    }
}
