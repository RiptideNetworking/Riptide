using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    /// <summary>Contains event data for when a client connects to the server.</summary>
    public class ServerClientConnectedEventArgs : EventArgs
    {
        /// <summary>The newly connected server client.</summary>
        public ServerClient Client { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="client">The newly connected server client.</param>
        public ServerClientConnectedEventArgs(ServerClient client)
        {
            Client = client;
        }
    }

    /// <summary>Contains event data for when the server receives a message from a client.</summary>
    public class ServerMessageReceivedEventArgs : EventArgs
    {
        /// <summary>The client that the message was received from.</summary>
        public ServerClient FromClient { get; private set; }
        /// <summary>The message that was received.</summary>
        public Message Message { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="fromClient">The client that the message was received from.</param>
        /// <param name="message">The message that was received.</param>
        public ServerMessageReceivedEventArgs(ServerClient fromClient, Message message)
        {
            FromClient = fromClient;
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
        public ClientConnectedEventArgs(ushort id)
        {
            Id = id;
        }
    }

    /// <summary>Contains event data for when the client receives a message from the server.</summary>
    public class ClientMessageReceivedEventArgs : EventArgs
    {
        /// <summary>The message that was received.</summary>
        public Message Message { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="message">The message that was received.</param>
        public ClientMessageReceivedEventArgs(Message message)
        {
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
        public ClientDisconnectedEventArgs(ushort id)
        {
            Id = id;
        }
    }

    /// <summary>Contains event data for when the ping is updated.</summary>
    public class PingUpdatedEventArgs : EventArgs
    {
        /// <summary>The round trip time of the latest ping.</summary>
        public ushort RTT { get; private set; }
        /// <summary>The smoothed round trip time of the latest ping.</summary>
        public ushort SmoothRTT { get; private set; }

        /// <summary>Initializes event data.</summary>
        /// <param name="RTT">The round trip time of the latest ping.</param>
        /// <param name="smoothRTT">The smoothed round trip time of the latest ping.</param>
        public PingUpdatedEventArgs(ushort RTT, ushort smoothRTT)
        {
            this.RTT = RTT;
            SmoothRTT = smoothRTT;
        }
    }
}
