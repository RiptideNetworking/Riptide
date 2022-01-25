
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Transports;
using System;

namespace RiptideNetworking
{
    /// <summary>Contains event data for when a client connects to the server.</summary>
    public class ServerClientConnectedEventArgs : EventArgs
    {
        /// <summary>The newly connected client.</summary>
        public IConnectionInfo Client { get; private set; }
        /// <summary>A message containing any custom data the client included when it connected.</summary>
        public Message ConnectMessage { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="client">The newly connected client.</param>
        /// <param name="connectMessage">A message containing any custom data the client included when it connected.</param>
        public ServerClientConnectedEventArgs(IConnectionInfo client, Message connectMessage)
        {
            Client = client;
            ConnectMessage = connectMessage;
        }
    }

    /// <summary>Contains event data for when the server receives a message from a client.</summary>
    public class ServerMessageReceivedEventArgs : EventArgs
    {
        /// <summary>The client that the message was received from.</summary>
        public ushort FromClientId { get; private set; }
        /// <summary>The ID of the message.</summary>
        public ushort MessageId { get; private set; }
        /// <summary>The message that was received.</summary>
        public Message Message { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="fromClientId">The client that the message was received from.</param>
        /// <param name="messageId">The ID of the message.</param>
        /// <param name="message">The message that was received.</param>
        public ServerMessageReceivedEventArgs(ushort fromClientId, ushort messageId, Message message)
        {
            FromClientId = fromClientId;
            MessageId = messageId;
            Message = message;
        }
    }

    /// <summary>Contains event data for when a new client connects.</summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        /// <summary>The numeric ID of the newly connected client.</summary>
        public ushort Id { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="id">The numeric ID of the newly connected client.</param>
        public ClientConnectedEventArgs(ushort id) => Id = id;
    }

    /// <summary>Contains event data for when the client receives a message from the server.</summary>
    public class ClientMessageReceivedEventArgs : EventArgs
    {
        /// <summary>The ID of the message.</summary>
        public ushort MessageId { get; private set; }
        /// <summary>The message that was received.</summary>
        public Message Message { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="messageId">The ID of the message.</param>
        /// <param name="message">The message that was received.</param>
        public ClientMessageReceivedEventArgs(ushort messageId, Message message)
        {
            MessageId = messageId;
            Message = message;
        }
    }

    /// <summary>Contains event data for when a client disconnects from the server.</summary>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>The numeric ID of the client that disconnected.</summary>
        public ushort Id { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="id">The numeric ID of the client that disconnected.</param>
        public ClientDisconnectedEventArgs(ushort id) => Id = id;
    }
}
