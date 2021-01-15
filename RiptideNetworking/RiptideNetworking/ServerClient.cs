using System;
using System.Net;

namespace RiptideNetworking
{
    public class ServerClient
    {
        public ushort Id { get; private set; }

        private readonly Server server;
        public readonly IPEndPoint remoteEndPoint;
        internal Rudp Rudp { get; private set; }
        internal SendLockables SendLockables { get => Rudp.SendLockables; }

        // Ping and RTT
        internal DateTime lastHeartbeat;

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
            Message message = new Message();
            message.Add(Rudp.SendLockables.LastReceivedSeqId); // Last remote sequence ID
            message.Add(Rudp.SendLockables.AcksBitfield); // Acks

            if (forSeqId != Rudp.SendLockables.LastReceivedSeqId)
            {
                message.Add(forSeqId);
                server.Send(message, remoteEndPoint, HeaderType.ackExtra);
            }
            else
            {
                server.Send(message, remoteEndPoint, HeaderType.ack);
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
            Message message = new Message();
            message.Add(pingId);

            server.Send(message, remoteEndPoint, HeaderType.heartbeat);
        }

        internal void HandleHeartbeat(Message message)
        {
            SendHeartbeat(message.GetByte());

            Rudp.RTT = message.GetUShort();
            lastHeartbeat = DateTime.UtcNow;
        }

        internal void SendWelcome()
        {
            Message message = new Message();
            message.Add(Id);

            server.SendReliable(message, remoteEndPoint, Rudp, 5, HeaderType.welcome);
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
