using System;
using System.Net;

namespace RiptideNetworking
{
    /// <summary>Represents a server's connection to a client.</summary>
    public class ServerClient
    {
        /// <summary>The numeric ID.</summary>
        public ushort Id { get; private set; }
        /// <summary>The round trip time of the connection.</summary>
        public ushort RTT { get => Rudp.RTT; }
        /// <summary>The smoothed round trip time of the connection.</summary>
        public ushort SmoothRTT { get => Rudp.SmoothRTT; }
        /// <summary>The remote endpoint.</summary>
        public readonly IPEndPoint remoteEndPoint;

        internal Rudp Rudp { get; private set; }
        internal SendLockables SendLockables { get => Rudp.SendLockables; }

        // Ping and RTT
        internal DateTime lastHeartbeat;

        private readonly Server server;
        private ConnectionState connectionState = ConnectionState.notConnected;

        internal ServerClient(Server server, IPEndPoint endPoint, ushort id)
        {
            this.server = server;
            remoteEndPoint = endPoint;
            Id = id;
            Rudp = new Rudp(server.Send, this.server.logName);

            connectionState = ConnectionState.connecting;
            SendWelcome();
        }

        internal void Disconnect()
        {
            connectionState = ConnectionState.notConnected;
        }

        #region Messages
        internal void SendAck(ushort forSeqId)
        {
            Message message;
            if (forSeqId == Rudp.SendLockables.LastReceivedSeqId)
                message = new Message(HeaderType.ack, 4);
            else
                message = new Message(HeaderType.ackExtra, 6);

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

        internal void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            Rudp.AckMessage(remoteLastReceivedSeqId);
            Rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        internal void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            Rudp.AckMessage(ackedSeqId);
            Rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        internal void SendHeartbeat(byte pingId)
        {
            Message message = new Message(HeaderType.heartbeat, 1);
            message.Add(pingId);

            server.Send(message, this);
        }

        internal void HandleHeartbeat(Message message)
        {
            SendHeartbeat(message.GetByte());

            Rudp.RTT = message.GetUShort();
            lastHeartbeat = DateTime.UtcNow;
        }

        internal void SendWelcome()
        {
            Message message = new Message(HeaderType.welcome, 2);
            message.Add(Id);

            server.Send(message, this, 5);
        }

        internal void HandleWelcomeReceived(Message message)
        {
            if (connectionState == ConnectionState.connected)
                return;

            ushort id = message.GetUShort();

            if (Id != id)
                RiptideLogger.Log(server.logName, $"Client has assumed incorrect ID: {id}");

            connectionState = ConnectionState.connected;
            server.OnClientConnected(new ServerClientConnectedEventArgs(this));
        }
        #endregion
    }
}
