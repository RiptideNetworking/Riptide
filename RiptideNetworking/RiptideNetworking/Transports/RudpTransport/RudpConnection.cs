
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Utils;
using System;
using System.Net;

namespace RiptideNetworking.Transports.RudpTransport
{
    /// <summary>Represents a server's connection to a client.</summary>
    public class RudpConnection : IConnectionInfo
    {
        /// <inheritdoc/>
        public ushort Id { get; private set; }
        /// <inheritdoc/>
        public short RTT => Peer.RTT;
        /// <inheritdoc/>
        public short SmoothRTT => Peer.SmoothRTT;
        /// <inheritdoc/>
        public bool IsNotConnected => connectionState == ConnectionState.notConnected;
        /// <inheritdoc/>
        public bool IsConnecting => connectionState == ConnectionState.connecting;
        /// <inheritdoc/>
        public bool IsConnected => connectionState == ConnectionState.connected;

        /// <summary>The connection's remote endpoint.</summary>
        public readonly IPEndPoint RemoteEndPoint;

        /// <summary>The client's <see cref="RudpPeer"/> instance.</summary>
        internal RudpPeer Peer { get; private set; }
        /// <inheritdoc cref="RudpPeer.SendLockables"/>
        internal SendLockables SendLockables => Peer.SendLockables;
        /// <summary>Whether or not the client has timed out.</summary>
        internal bool HasTimedOut => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > server.ClientTimeoutTime;

        /// <summary>The time at which the last heartbeat was received from the client.</summary>
        private DateTime lastHeartbeat;
        /// <summary>The <see cref="RudpServer"/> that the client is associated with.</summary>
        private readonly RudpServer server;
        /// <summary>The client's current connection state.</summary>
        private ConnectionState connectionState = ConnectionState.notConnected;

        /// <summary>Handles initial setup.</summary>
        /// <param name="server">The <see cref="RudpServer"/> that the client is associated with.</param>
        /// <param name="endPoint">The connection's remote endpoint.</param>
        /// <param name="id">The numeric ID of the client.</param>
        internal RudpConnection(RudpServer server, IPEndPoint endPoint, ushort id)
        {
            this.server = server;
            RemoteEndPoint = endPoint;
            Id = id;
            Peer = new RudpPeer(server);
            lastHeartbeat = DateTime.UtcNow;

            connectionState = ConnectionState.connecting;
            SendWelcome();
        }

        /// <summary>Cleans up local objects on disconnection.</summary>
        internal void LocalDisconnect()
        {
            connectionState = ConnectionState.notConnected;

            lock (Peer.PendingMessages)
            {
                foreach (PendingMessage pendingMessage in Peer.PendingMessages.Values)
                    pendingMessage.Clear(false);

                Peer.PendingMessages.Clear();
            }
        }

        #region Messages
        /// <summary>Sends an ack message for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        internal void SendAck(ushort forSeqId)
        {
            Message message = Message.Create(forSeqId == Peer.SendLockables.LastReceivedSeqId ? HeaderType.ack : HeaderType.ackExtra);
            message.Add(Peer.SendLockables.LastReceivedSeqId); // Last remote sequence ID
            message.Add(Peer.SendLockables.AcksBitfield); // Acks

            if (forSeqId == Peer.SendLockables.LastReceivedSeqId)
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

            Peer.AckMessage(remoteLastReceivedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            Peer.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Handles an ack message for a sequence ID other than the last received one.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            Peer.AckMessage(ackedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            Peer.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Sends a heartbeat message.</summary>
        internal void SendHeartbeat(byte pingId)
        {
            Message message = Message.Create(HeaderType.heartbeat);
            message.Add(pingId);

            server.Send(message, this);
        }

        /// <summary>Handles a heartbeat message.</summary>
        /// <param name="message">The heartbeat message to handle.</param>
        internal void HandleHeartbeat(Message message)
        {
            SendHeartbeat(message.GetByte());
            Peer.RTT = message.GetShort();

            lastHeartbeat = DateTime.UtcNow;
        }

        /// <summary>Sends a welcome message.</summary>
        internal void SendWelcome()
        {
            Message message = Message.Create(HeaderType.welcome, 25);
            message.Add(Id);

            server.Send(message, this);
        }

        /// <summary>Handles a welcome message.</summary>
        /// <param name="message">The welcome message to handle.</param>
        internal void HandleWelcomeReceived(Message message)
        {
            if (IsConnected)
                return;

            ushort id = message.GetUShort();
            if (Id != id)
                RiptideLogger.Log(LogType.error, server.LogName, $"Client has assumed ID {id} instead of {Id}!");

            connectionState = ConnectionState.connected;
            server.OnClientConnected(RemoteEndPoint, new ServerClientConnectedEventArgs(this, message));
        }
        #endregion
    }
}
