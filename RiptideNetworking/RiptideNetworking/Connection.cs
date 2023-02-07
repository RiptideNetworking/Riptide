// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;

namespace Riptide
{
    /// <summary>The state of a connection.</summary>
    internal enum ConnectionState : byte
    {
        /// <summary>Not connected. No connection has been established or the connection has been closed.</summary>
        NotConnected,
        /// <summary>Connecting. Still trying to establish a connection.</summary>
        Connecting,
        /// <summary>Connection is pending. The server is still determining whether or not the connection should be allowed.</summary>
        Pending,
        /// <summary>Connected. A connection has been established successfully.</summary>
        Connected,
        /// <summary>Not connected. A connection attempt was made but was rejected.</summary>
        Rejected,
    }

    /// <summary>Represents a connection to a <see cref="Server"/> or <see cref="Client"/>.</summary>
    public abstract class Connection
    {
        /// <summary>The connection's numeric ID.</summary>
        public ushort Id { get; internal set; }
        /// <summary>Whether or not the connection is currently <i>not</i> trying to connect, pending, nor actively connected.</summary>
        public bool IsNotConnected => state == ConnectionState.NotConnected || state == ConnectionState.Rejected;
        /// <summary>Whether or not the connection is currently in the process of connecting.</summary>
        public bool IsConnecting => state == ConnectionState.Connecting;
        /// <summary>Whether or not the connection is currently pending (waiting to be accepted/rejected by the server).</summary>
        public bool IsPending => state == ConnectionState.Pending;
        /// <summary>Whether or not the connection is currently connected.</summary>
        public bool IsConnected => state == ConnectionState.Connected;
        /// <summary>The round trip time (ping) of the connection, in milliseconds. -1 if not calculated yet.</summary>
        public short RTT
        {
            get => _rtt;
            private set
            {
                SmoothRTT = _rtt == -1 ? value : (short)Math.Max(1f, SmoothRTT * 0.7f + value * 0.3f);
                _rtt = value;
            }
        }
        private short _rtt = -1;
        /// <summary>The smoothed round trip time (ping) of the connection, in milliseconds. -1 if not calculated yet.</summary>
        /// <remarks>This value is slower to accurately represent lasting changes in latency than <see cref="RTT"/>, but it is less susceptible to changing drastically due to significant—but temporary—jumps in latency.</remarks>
        public short SmoothRTT { get; private set; } = -1;
        /// <summary>Whether or not the connection can time out.</summary>
        public bool CanTimeout
        {
            get => _canTimeout;
            set
            {
                if (value)
                    ResetTimeout();

                _canTimeout = value;
            }
        }
        private bool _canTimeout;

        /// <summary>The local peer this connection is associated with.</summary>
        internal Peer Peer { get; set; }
        /// <summary>Whether or not the connection has timed out.</summary>
        internal bool HasTimedOut => _canTimeout && (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > Peer.TimeoutTime;
        /// <summary>Whether or not the connection attempt has timed out. Uses a multiple of <see cref="Peer.TimeoutTime"/> and ignores the value of <see cref="CanTimeout"/>.</summary>
        internal bool HasConnectAttemptTimedOut => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > Peer.ConnectTimeoutTime;
        /// <summary>The currently pending reliably sent messages whose delivery has not been acknowledged yet. Stored by sequence ID.</summary>
        internal Dictionary<ushort, PendingMessage> PendingMessages { get; private set; } = new Dictionary<ushort, PendingMessage>();

        /// <summary>The sequence ID of the latest message that we want to acknowledge.</summary>
        private ushort lastReceivedSeqId;
        /// <summary>Sequence IDs of messages which we have (or have not) received and want to acknowledge.</summary>
        private readonly Bitfield receivedSeqIds = new Bitfield();
        /// <summary>The sequence ID of the latest message that we've received an ack for.</summary>
        private ushort lastAckedSeqId;
        /// <summary>Sequence IDs of messages which we have (or have not) received acks for.</summary>
        private readonly Bitfield ackedSeqIds = new Bitfield(false);
        
        /// <summary>The next sequence ID to use.</summary>
        private ushort NextSequenceId => (ushort)++_lastSequenceId;
        private int _lastSequenceId;
        /// <summary>The connection's current state.</summary>
        private ConnectionState state;
        /// <summary>The time at which the last heartbeat was received from the other end.</summary>
        private DateTime lastHeartbeat;
        /// <summary>The ID of the last ping that was sent.</summary>
        private byte lastPingId;
        /// <summary>The ID of the currently pending ping.</summary>
        private byte pendingPingId;
        /// <summary>The stopwatch that tracks the time since the currently pending ping was sent.</summary>
        private readonly System.Diagnostics.Stopwatch pendingPingStopwatch = new System.Diagnostics.Stopwatch();

        /// <summary>Initializes the connection.</summary>
        protected Connection()
        {
            state = ConnectionState.Connecting;
            CanTimeout = true;
        }

        /// <summary>Resets the connection's timeout time.</summary>
        public void ResetTimeout()
        {
            lastHeartbeat = DateTime.UtcNow;
        }

        /// <summary>Sends a message.</summary>
        /// <inheritdoc cref="Client.Send(Message, bool)"/>
        internal void Send(Message message, bool shouldRelease = true)
        {
            if (message.SendMode == MessageSendMode.Unreliable)
                Send(message.Bytes, message.WrittenLength);
            else
            {
                ushort sequenceId = NextSequenceId; // Get the next sequence ID
                PendingMessage.CreateAndSend(sequenceId, message, this);
            }

            if (shouldRelease)
                message.Release();
        }

        /// <summary>Sends data.</summary>
        /// <param name="dataBuffer">The array containing the data.</param>
        /// <param name="amount">The number of bytes in the array which should be sent.</param>
        protected internal abstract void Send(byte[] dataBuffer, int amount);

        /// <summary>Updates acks and determines whether the message is a duplicate.</summary>
        /// <param name="sequenceId">The message's sequence ID.</param>
        /// <returns>Whether or not the message should be handled.</returns>
        internal bool ReliableHandle(ushort sequenceId)
        {
            bool doHandle = false;
            int sequenceGap = Helper.GetSequenceGap(sequenceId, lastReceivedSeqId);

            if (sequenceGap != 0)
            {
                // The received sequence ID is different from the previous one
                if (sequenceGap > 0)
                {
                    // The received sequence ID is newer than the previous one
                    if (sequenceGap > 64)
                        RiptideLogger.Log(LogType.Warning, Peer.LogName, $"The gap between received sequence IDs was very large ({sequenceGap})!");

                    receivedSeqIds.ShiftBy(sequenceGap);
                    lastReceivedSeqId = sequenceId;
                }
                else // The received sequence ID is older than the previous one (out of order message)
                    sequenceGap = -sequenceGap;

                doHandle = !receivedSeqIds.IsSet(sequenceGap);
                receivedSeqIds.Set(sequenceGap);
            }

            SendAck(sequenceId);
            return doHandle;
        }

        /// <summary>Cleans up the local side of the connection.</summary>
        /// <param name="wasRejected">Whether or not the connection was rejected.</param>
        internal void LocalDisconnect(bool wasRejected = false)
        {
            state = wasRejected ? ConnectionState.Rejected : ConnectionState.NotConnected;

            foreach (PendingMessage pendingMessage in PendingMessages.Values)
                pendingMessage.Clear(false);

            PendingMessages.Clear();
        }

        /// <summary>Updates which messages we've received acks for.</summary>
        /// <param name="remoteLastReceivedSeqId">The latest sequence ID that the other end has received.</param>
        /// <param name="remoteReceivedSeqIds">Sequence IDs which the other end has (or has not) received.</param>
        internal void UpdateReceivedAcks(ushort remoteLastReceivedSeqId, ushort remoteReceivedSeqIds)
        {
            int sequenceGap = Helper.GetSequenceGap(remoteLastReceivedSeqId, lastAckedSeqId);

            if (sequenceGap > 0)
            {
                // The latest sequence ID that the other end has received is newer than the previous one
                for (int i = 0; i < 16; i++)
                {
                    // Clear any messages that have been newly acknowledged
                    if (!ackedSeqIds.IsSet(i + 1) && (remoteReceivedSeqIds & (1 << (sequenceGap + i))) != 0)
                        ClearMessage((ushort)(lastAckedSeqId - (i + 1)));
                }

                if (!ackedSeqIds.HasCapacityFor(sequenceGap, out int overflow))
                {
                    for (int i = 0; i < overflow; i++)
                    {
                        // Resend those messages which haven't been acked and whose sequence IDs are about to be pushed out of the bitfield
                        if (!ackedSeqIds.CheckAndTrimLast(out int checkedPosition))
                            ResendMessage((ushort)(lastAckedSeqId - checkedPosition));
                        else
                            ClearMessage((ushort)(lastAckedSeqId - checkedPosition));
                    }
                }

                ackedSeqIds.ShiftBy(sequenceGap);
                ackedSeqIds.Combine(remoteReceivedSeqIds);
                ackedSeqIds.Set(sequenceGap); // Ensure that the bit corresponding to the previous ack is set
                lastAckedSeqId = remoteLastReceivedSeqId;
                ClearMessage(remoteLastReceivedSeqId);
            }
            else if (sequenceGap < 0)
            {
                // The latest sequence ID that the other end has received is older than the previous one (out of order ack)
                ackedSeqIds.Set(-sequenceGap);
            }
            else
            {
                // The latest sequence ID that the other end has received is the same as the previous one (duplicate ack)
                ackedSeqIds.Combine(remoteReceivedSeqIds);
            }
        }

        /// <summary>Resends the <see cref="PendingMessage"/> with the given sequence ID.</summary>
        /// <param name="sequenceId">The sequence ID of the message to resend.</param>
        private void ResendMessage(ushort sequenceId)
        {
            if (PendingMessages.TryGetValue(sequenceId, out PendingMessage pendingMessage))
                pendingMessage.RetrySend();
        }
        
        /// <summary>Clears the <see cref="PendingMessage"/> with the given sequence ID.</summary>
        /// <param name="sequenceId">The sequence ID that was acknowledged.</param>
        internal void ClearMessage(ushort sequenceId)
        {
            if (PendingMessages.TryGetValue(sequenceId, out PendingMessage pendingMessage))
                pendingMessage.Clear();
        }

        /// <summary>Puts the connection in the pending state.</summary>
        internal void SetPending()
        {
            if (IsConnecting)
            {
                state = ConnectionState.Pending;
                ResetTimeout();
            }
        }

        #region Messages
        /// <summary>Sends an ack message for the given sequence ID.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        private void SendAck(ushort forSeqId)
        {
            Message message = Message.Create(forSeqId == lastReceivedSeqId ? MessageHeader.Ack : MessageHeader.AckExtra);
            message.AddUShort(lastReceivedSeqId);
            message.AddUShort(receivedSeqIds.First16);

            if (forSeqId != lastReceivedSeqId)
                message.AddUShort(forSeqId);
            
            Send(message);
        }

        /// <summary>Handles an ack message.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            ClearMessage(remoteLastReceivedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Handles an ack message for a sequence ID other than the last received one.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            ClearMessage(ackedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        #region Server
        /// <summary>Sends a welcome message.</summary>
        internal void SendWelcome()
        {
            Message message = Message.Create(MessageHeader.Welcome);
            message.AddUShort(Id);

            Send(message);
        }

        /// <summary>Handles a welcome message on the server.</summary>
        /// <param name="message">The welcome message to handle.</param>
        internal void HandleWelcomeResponse(Message message)
        {
            ushort id = message.GetUShort();
            if (Id != id)
                RiptideLogger.Log(LogType.Error, Peer.LogName, $"Client has assumed ID {id} instead of {Id}!");

            state = ConnectionState.Connected;
            ResetTimeout();
        }

        /// <summary>Handles a heartbeat message.</summary>
        /// <param name="message">The heartbeat message to handle.</param>
        internal void HandleHeartbeat(Message message)
        {
            RespondHeartbeat(message.GetByte());
            RTT = message.GetShort();

            ResetTimeout();
        }

        /// <summary>Sends a heartbeat message.</summary>
        private void RespondHeartbeat(byte pingId)
        {
            Message message = Message.Create(MessageHeader.Heartbeat);
            message.AddByte(pingId);

            Send(message);
        }
        #endregion

        #region Client
        /// <summary>Handles a welcome message on the client.</summary>
        /// <param name="message">The welcome message to handle.</param>
        internal void HandleWelcome(Message message)
        {
            Id = message.GetUShort();
            state = ConnectionState.Connected;
            ResetTimeout();

            RespondWelcome();
        }

        /// <summary>Sends a welcome response message.</summary>
        private void RespondWelcome()
        {
            Message message = Message.Create(MessageHeader.Welcome);
            message.AddUShort(Id);

            Send(message);
        }

        /// <summary>Sends a heartbeat message.</summary>
        internal void SendHeartbeat()
        {
            pendingPingId = lastPingId++;
            pendingPingStopwatch.Restart();

            Message message = Message.Create(MessageHeader.Heartbeat);
            message.AddByte(pendingPingId);
            message.AddShort(RTT);

            Send(message);
        }

        /// <summary>Handles a heartbeat message.</summary>
        /// <param name="message">The heartbeat message to handle.</param>
        internal void HandleHeartbeatResponse(Message message)
        {
            byte pingId = message.GetByte();

            if (pendingPingId == pingId)
                RTT = (short)Math.Max(1f, pendingPingStopwatch.ElapsedMilliseconds);

            ResetTimeout();
        }
        #endregion
        #endregion
    }
}
