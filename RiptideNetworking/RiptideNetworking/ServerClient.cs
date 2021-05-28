using System;
using System.Net;

namespace RiptideNetworking
{
    /// <summary>Represents a server's connection to a client.</summary>
    public class ServerClient
    {
        /// <summary>The numeric ID.</summary>
        public ushort Id { get; private set; }
        /// <summary>The round trip time of the connection. -1 if not calculated yet.</summary>
        public short RTT => Rudp.RTT;
        /// <summary>The smoothed round trip time of the connection. -1 if not calculated yet.</summary>
        public short SmoothRTT => Rudp.SmoothRTT;
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        public bool IsConnecting => connectionState == ConnectionState.connecting;
        /// <summary>Whether or not the client is currently connected.</summary>
        public bool IsConnected => connectionState == ConnectionState.connected;
        /// <summary>The connetion's remote endpoint.</summary>
        public readonly IPEndPoint remoteEndPoint;

        /// <summary>The client's Rudp instance.</summary>
        internal Rudp Rudp { get; private set; }
        /// <summary>The lockable values which are used to inform the other end of which messages we've received.</summary>
        internal SendLockables SendLockables => Rudp.SendLockables;
        /// <summary>Whether or not the client has timed out.</summary>
        internal bool HasTimedOut => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > server.ClientTimeoutTime;

        /// <summary>The time at which the last heartbeat was received from the client.</summary>
        private DateTime lastHeartbeat;
        /// <summary>The server instance the client is associated with.</summary>
        private readonly Server server;
        /// <summary>The client's current connection state.</summary>
        private ConnectionState connectionState = ConnectionState.notConnected;

        /// <summary>Initializes a ServerClient instance.</summary>
        /// <param name="server">The server this client is associated with.</param>
        /// <param name="endPoint">The remote endpoint of the client.</param>
        /// <param name="id">The ID of the client.</param>
        internal ServerClient(Server server, IPEndPoint endPoint, ushort id)
        {
            this.server = server;
            remoteEndPoint = endPoint;
            Id = id;
            Rudp = new Rudp(server.Send, this.server.LogName);
            lastHeartbeat = DateTime.UtcNow;

            connectionState = ConnectionState.connecting;
            SendWelcome();
        }

        /// <summary>Cleans up local objects on disconnection.</summary>
        internal void Disconnect()
        {
            connectionState = ConnectionState.notConnected;
        }

        #region Messages
        /// <summary>Sends an ack message for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        internal void SendAck(ushort forSeqId)
        {
            Message message = Message.CreateInternal(forSeqId == Rudp.SendLockables.LastReceivedSeqId ? HeaderType.ack : HeaderType.ackExtra);
            message.Add(Rudp.SendLockables.LastReceivedSeqId); // Last remote sequence ID
            message.Add(Rudp.SendLockables.AcksBitfield); // Acks

            if (forSeqId == Rudp.SendLockables.LastReceivedSeqId)
                server.Send(message, this);
            else
            {
                message.Add(forSeqId);
                server.Send(message, this);
            }
        }

        /// <summary>Handles an ack message.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            Rudp.AckMessage(remoteLastReceivedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            Rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Handles an ack message for a sequence ID other than the last received one.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            Rudp.AckMessage(ackedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            Rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Sends a heartbeat message.</summary>
        internal void SendHeartbeat(byte pingId)
        {
            Message message = Message.CreateInternal(HeaderType.heartbeat);
            message.Add(pingId);

            server.Send(message, this);
        }

        /// <summary>Handles a heartbeat message.</summary>
        /// <param name="message">The heartbeat message to handle.</param>
        internal void HandleHeartbeat(Message message)
        {
            SendHeartbeat(message.GetByte());

            Rudp.RTT = message.GetShort();
            lastHeartbeat = DateTime.UtcNow;
        }

        /// <summary>Sends a welcome message.</summary>
        internal void SendWelcome()
        {
            Message message = Message.CreateInternal(HeaderType.welcome);
            message.Add(Id);

            server.Send(message, this, 5);
        }

        /// <summary>Handles a welcome message.</summary>
        /// <param name="message">The welcome message to handle.</param>
        internal void HandleWelcomeReceived(Message message)
        {
            if (connectionState == ConnectionState.connected)
                return;

            ushort id = message.GetUShort();

            if (Id != id)
                RiptideLogger.Log(server.LogName, $"Client has assumed incorrect ID: {id}");

            connectionState = ConnectionState.connected;
            server.OnClientConnected(new ServerClientConnectedEventArgs(this));
        }
        #endregion
    }
}
