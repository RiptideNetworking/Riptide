// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

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
    }

    /// <summary>Represents a connection to a <see cref="Server"/> or <see cref="Client"/>.</summary>
    public abstract class Connection
    {
        /// <summary>Invoked when the notify message with the given sequence ID is successfully delivered.</summary>
        public Action<ushort> NotifyDelivered;
        /// <summary>Invoked when the notify message with the given sequence ID is lost.</summary>
        public Action<ushort> NotifyLost;
        /// <summary>Invoked when a notify message is received.</summary>
        public Action<Message> NotifyReceived;
        /// <summary>Invoked when the reliable message with the given sequence ID is successfully delivered.</summary>
        public Action<ushort> ReliableDelivered;

        /// <summary>The connection's numeric ID.</summary>
        public ushort Id { get; internal set; }
        /// <summary>Whether or not the connection is currently <i>not</i> trying to connect, pending, nor actively connected.</summary>
        public bool IsNotConnected => state == ConnectionState.NotConnected;
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
        private short _rtt;
        /// <summary>The smoothed round trip time (ping) of the connection, in milliseconds. -1 if not calculated yet.</summary>
        /// <remarks>This value is slower to accurately represent lasting changes in latency than <see cref="RTT"/>, but it is less susceptible to changing drastically due to significant—but temporary—jumps in latency.</remarks>
        public short SmoothRTT { get; private set; }
        /// <summary>The time (in milliseconds) after which to disconnect if no heartbeats are received.</summary>
        public int TimeoutTime { get; set; }
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
        /// <summary>Whether or not the connection can disconnect due to poor connection quality.</summary>
        /// <remarks>When this is set to <see langword="false"/>, <see cref="MaxAvgSendAttempts"/>, <see cref="MaxSendAttempts"/>,
        /// and <see cref="MaxNotifyLoss"/> are ignored and exceeding their values will not trigger a disconnection.</remarks>
        public bool CanQualityDisconnect;
        /// <summary>The connection's metrics.</summary>
        public readonly ConnectionMetrics Metrics;
        /// <summary>The maximum acceptable average number of send attempts it takes to deliver a reliable message. The connection
        /// will be closed if this is exceeded more than <see cref="AvgSendAttemptsResilience"/> times in a row.</summary>
        public int MaxAvgSendAttempts;
        /// <summary>How many consecutive times <see cref="MaxAvgSendAttempts"/> can be exceeded before triggering a disconnect.</summary>
        public int AvgSendAttemptsResilience;
        /// <summary>The absolute maximum number of times a reliable message may be sent. A single message reaching this threshold will cause a disconnection.</summary>
        public int MaxSendAttempts;
        /// <summary>The maximum acceptable loss rate of notify messages. The connection will be closed if this is exceeded more than <see cref="NotifyLossResilience"/> times in a row.</summary>
        public float MaxNotifyLoss;
        /// <summary>How many consecutive times <see cref="MaxNotifyLoss"/> can be exceeded before triggering a disconnect.</summary>
        public int NotifyLossResilience;

        /// <summary>The local peer this connection is associated with.</summary>
        internal Peer Peer { get; private set; }
        /// <summary>Whether or not the connection has timed out.</summary>
        internal bool HasTimedOut => _canTimeout && Peer.CurrentTime - lastHeartbeat > TimeoutTime;
        /// <summary>Whether or not the connection attempt has timed out.</summary>
        internal bool HasConnectAttemptTimedOut => _canTimeout && Peer.CurrentTime - lastHeartbeat > Peer.ConnectTimeoutTime;

        /// <summary>The sequencer for notify messages.</summary>
        private readonly NotifySequencer notify;
        /// <summary>The sequencer for reliable messages.</summary>
        private readonly ReliableSequencer reliable;
        /// <summary>The currently pending reliably sent messages whose delivery has not been acknowledged yet. Stored by sequence ID.</summary>
        private readonly Dictionary<ushort, PendingMessage> pendingMessages;
        /// <summary>The connection's current state.</summary>
        private ConnectionState state;
        /// <summary>The number of consecutive times that the <see cref="MaxAvgSendAttempts"/> threshold was exceeded.</summary>
        private int sendAttemptsViolations;
        /// <summary>The number of consecutive times that the <see cref="MaxNotifyLoss"/> threshold was exceeded.</summary>
        private int lossRateViolations;
        /// <summary>The time at which the last heartbeat was received from the other end.</summary>
        private long lastHeartbeat;
        /// <summary>The ID of the last ping that was sent.</summary>
        private byte lastPingId;
        /// <summary>The ID of the currently pending ping.</summary>
        private byte pendingPingId;
        /// <summary>The time at which the currently pending ping was sent.</summary>
        private long pendingPingSendTime;

        /// <summary>Initializes the connection.</summary>
        protected Connection()
        {
            Metrics = new ConnectionMetrics();
            notify = new NotifySequencer(this);
            reliable = new ReliableSequencer(this);
            state = ConnectionState.Connecting;
            _rtt = -1;
            SmoothRTT = -1;
            _canTimeout = true;
            CanQualityDisconnect = true;
            MaxAvgSendAttempts = 5;
            AvgSendAttemptsResilience = 64;
            MaxSendAttempts = 15;
            MaxNotifyLoss = 0.05f; // 5%
            NotifyLossResilience = 64;
            pendingMessages = new Dictionary<ushort, PendingMessage>();
        }

        /// <summary>Initializes connection data.</summary>
        /// <param name="peer">The <see cref="Riptide.Peer"/> which this connection belongs to.</param>
        /// <param name="timeoutTime">The timeout time.</param>
        internal void Initialize(Peer peer, int timeoutTime)
        {
            Peer = peer;
            TimeoutTime = timeoutTime;
        }

        /// <summary>Resets the connection's timeout time.</summary>
        public void ResetTimeout()
        {
            lastHeartbeat = Peer.CurrentTime;
        }

        /// <summary>Sends a message.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <returns>For reliable and notify messages, the sequence ID that the message was sent with. 0 for unreliable messages.</returns>
        /// <remarks>
        ///   If you intend to continue using the message instance after calling this method, you <i>must</i> set <paramref name="shouldRelease"/>
        ///   to <see langword="false"/>. <see cref="Message.Release"/> can be used to manually return the message to the pool at a later time.
        /// </remarks>
        public ushort Send(Message message, bool shouldRelease = true)
        {
            ushort sequenceId = 0;
            if (message.SendMode == MessageSendMode.Notify)
            {
                sequenceId = notify.InsertHeader(message);
                int byteAmount = message.BytesInUse;
                Buffer.BlockCopy(message.Data, 0, Message.ByteBuffer, 0, byteAmount);
                Send(Message.ByteBuffer, byteAmount);
                Metrics.SentNotify(byteAmount);
            }
            else if (message.SendMode == MessageSendMode.Unreliable)
            {
                int byteAmount = message.BytesInUse;
                Buffer.BlockCopy(message.Data, 0, Message.ByteBuffer, 0, byteAmount);
                Send(Message.ByteBuffer, byteAmount);
                Metrics.SentUnreliable(byteAmount);
            }
            else
            {
                sequenceId = reliable.NextSequenceId;
                PendingMessage pendingMessage = PendingMessage.Create(sequenceId, message, this);
                pendingMessages.Add(sequenceId, pendingMessage);
                pendingMessage.TrySend();
                Metrics.ReliableUniques++;
            }

            if (shouldRelease)
                message.Release();

            return sequenceId;
        }

        /// <summary>Sends data.</summary>
        /// <param name="dataBuffer">The array containing the data.</param>
        /// <param name="amount">The number of bytes in the array which should be sent.</param>
        protected internal abstract void Send(byte[] dataBuffer, int amount);
        
        /// <summary>Processes a notify message.</summary>
        /// <param name="dataBuffer">The received data.</param>
        /// <param name="amount">The number of bytes that were received.</param>
        /// <param name="message">The message instance to use.</param>
        internal void ProcessNotify(byte[] dataBuffer, int amount, Message message)
        {
            notify.UpdateReceivedAcks(Converter.UShortFromBits(dataBuffer, Message.HeaderBits), Converter.ByteFromBits(dataBuffer, Message.HeaderBits + 16));

            Metrics.ReceivedNotify(amount);
            if (notify.ShouldHandle(Converter.UShortFromBits(dataBuffer, Message.HeaderBits + 24)))
            {
                Buffer.BlockCopy(dataBuffer, 1, message.Data, 1, amount - 1); // Copy payload
                NotifyReceived?.Invoke(message);
            }
            else
                Metrics.NotifyDiscarded++;
        }

        /// <summary>Determines if the message with the given sequence ID should be handled.</summary>
        /// <param name="sequenceId">The message's sequence ID.</param>
        /// <returns>Whether or not the message should be handled.</returns>
        internal bool ShouldHandle(ushort sequenceId)
        {
            return reliable.ShouldHandle(sequenceId);
        }

        /// <summary>Cleans up the local side of the connection.</summary>
        internal void LocalDisconnect()
        {
            state = ConnectionState.NotConnected;

            foreach (PendingMessage pendingMessage in pendingMessages.Values)
                pendingMessage.Clear();

            pendingMessages.Clear();
        }

        /// <summary>Resends the <see cref="PendingMessage"/> with the given sequence ID.</summary>
        /// <param name="sequenceId">The sequence ID of the message to resend.</param>
        private void ResendMessage(ushort sequenceId)
        {
            if (pendingMessages.TryGetValue(sequenceId, out PendingMessage pendingMessage))
                pendingMessage.RetrySend();
        }
        
        /// <summary>Clears the <see cref="PendingMessage"/> with the given sequence ID.</summary>
        /// <param name="sequenceId">The sequence ID that was acknowledged.</param>
        internal void ClearMessage(ushort sequenceId)
        {
            if (pendingMessages.TryGetValue(sequenceId, out PendingMessage pendingMessage))
            {
                ReliableDelivered?.Invoke(sequenceId);
                pendingMessage.Clear();
                pendingMessages.Remove(sequenceId);
                UpdateSendAttemptsViolations();
            }
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

        /// <summary>Checks the average send attempts (of reliable messages) and updates <see cref="sendAttemptsViolations"/> accordingly.</summary>
        private void UpdateSendAttemptsViolations()
        {
            if (Metrics.RollingReliableSends.Mean > MaxAvgSendAttempts)
            {
                sendAttemptsViolations++;
                if (sendAttemptsViolations >= AvgSendAttemptsResilience)
                    Peer.Disconnect(this, DisconnectReason.PoorConnection);
            }
            else
                sendAttemptsViolations = 0;
        }

        /// <summary>Checks the loss rate (of notify messages) and updates <see cref="lossRateViolations"/> accordingly.</summary>
        private void UpdateLossViolations()
        {
            if (Metrics.RollingNotifyLossRate > MaxNotifyLoss)
            {
                lossRateViolations++;
                if (lossRateViolations >= NotifyLossResilience)
                    Peer.Disconnect(this, DisconnectReason.PoorConnection);
            }
            else
                lossRateViolations = 0;
        }

        #region Messages
        /// <summary>Sends an ack message for the given sequence ID.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        /// <param name="lastReceivedSeqId">The sequence ID of the latest message we've received.</param>
        /// <param name="receivedSeqIds">Sequence IDs of previous messages that we have (or have not received).</param>
        private void SendAck(ushort forSeqId, ushort lastReceivedSeqId, Bitfield receivedSeqIds)
        {
            Message message = Message.Create(MessageHeader.Ack);
            message.AddUShort(lastReceivedSeqId);
            message.AddUShort(receivedSeqIds.First16);

            if (forSeqId == lastReceivedSeqId)
                message.AddBool(false);
            else
                message.AddBool(true);
                message.AddUShort(forSeqId);
            
            Send(message);
        }

        /// <summary>Handles an ack message.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetBool() ? message.GetUShort() : remoteLastReceivedSeqId;

            ClearMessage(ackedSeqId);
            reliable.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
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
        /// <returns>Whether or not the connection is now connected.</returns>
        internal bool HandleWelcomeResponse(Message message)
        {
            if (!IsPending)
                return false;

            ushort id = message.GetUShort();
            if (Id != id)
                RiptideLogger.Log(LogType.Error, Peer.LogName, $"Client has assumed ID {id} instead of {Id}!");

            state = ConnectionState.Connected;
            ResetTimeout();
            return true;
        }

        /// <summary>Handles a heartbeat message.</summary>
        /// <param name="message">The heartbeat message to handle.</param>
        internal void HandleHeartbeat(Message message)
        {
            if (!IsConnected)
                return; // A client that is not yet fully connected should not be sending heartbeats

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
            pendingPingSendTime = Peer.CurrentTime;

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
                RTT = (short)Math.Max(1, Peer.CurrentTime - pendingPingSendTime);

            ResetTimeout();
        }
        #endregion
        #endregion

        #region Events
        /// <summary>Invokes the <see cref="NotifyDelivered"/> event.</summary>
        /// <param name="sequenceId">The sequence ID of the delivered message.</param>
        protected virtual void OnNotifyDelivered(ushort sequenceId)
        {
            Metrics.DeliveredNotify();
            NotifyDelivered?.Invoke(sequenceId);
            UpdateLossViolations();
        }
        
        /// <summary>Invokes the <see cref="NotifyLost"/> event.</summary>
        /// <param name="sequenceId">The sequence ID of the lost message.</param>
        protected virtual void OnNotifyLost(ushort sequenceId)
        {
            Metrics.LostNotify();
            NotifyLost?.Invoke(sequenceId);
            UpdateLossViolations();
        }
        #endregion

        #region Message Sequencing
        /// <summary>Provides functionality for filtering out duplicate messages and determining delivery/loss status.</summary>
        private abstract class Sequencer
        {
            /// <summary>The next sequence ID to use.</summary>
            internal ushort NextSequenceId => _nextSequenceId++;
            private ushort _nextSequenceId = 1;

            /// <summary>The connection this sequencer belongs to.</summary>
            protected readonly Connection connection;
            /// <summary>The sequence ID of the latest message that we want to acknowledge.</summary>
            protected ushort lastReceivedSeqId;
            /// <summary>Sequence IDs of messages which we have (or have not) received and want to acknowledge.</summary>
            protected readonly Bitfield receivedSeqIds = new Bitfield();
            /// <summary>The sequence ID of the latest message that we've received an ack for.</summary>
            protected ushort lastAckedSeqId;
            /// <summary>Sequence IDs of messages we sent and which we have (or have not) received acks for.</summary>
            protected readonly Bitfield ackedSeqIds = new Bitfield(false);

            /// <summary>Initializes the sequencer.</summary>
            /// <param name="connection">The connection this sequencer belongs to.</param>
            protected Sequencer(Connection connection)
            {
                this.connection = connection;
            }

            /// <summary>Determines whether or not to handle a message with the given sequence ID.</summary>
            /// <param name="sequenceId">The sequence ID in question.</param>
            /// <returns>Whether or not to handle the message.</returns>
            internal abstract bool ShouldHandle(ushort sequenceId);

            /// <summary>Updates which messages we've received acks for.</summary>
            /// <param name="remoteLastReceivedSeqId">The latest sequence ID that the other end has received.</param>
            /// <param name="remoteReceivedSeqIds">Sequence IDs which the other end has (or has not) received.</param>
            internal abstract void UpdateReceivedAcks(ushort remoteLastReceivedSeqId, ushort remoteReceivedSeqIds);
        }

        /// <inheritdoc/>
        private class NotifySequencer : Sequencer
        {
            /// <inheritdoc/>
            internal NotifySequencer(Connection connection) : base(connection) { }

            /// <summary>Inserts the notify header into the given message.</summary>
            /// <param name="message">The message to insert the header into.</param>
            /// <returns>The sequence ID of the message.</returns>
            internal ushort InsertHeader(Message message)
            {
                ushort sequenceId = NextSequenceId;
                ulong notifyBits = lastReceivedSeqId | ((ulong)receivedSeqIds.First8 << (2 * Converter.BitsPerByte)) | ((ulong)sequenceId << (3 * Converter.BitsPerByte));
                message.SetBits(notifyBits, 5 * Converter.BitsPerByte, Message.HeaderBits);
                return sequenceId;
            }

            /// <inheritdoc/>
            /// <remarks>Duplicate and out of order messages are filtered out and not handled.</remarks>
            internal override bool ShouldHandle(ushort sequenceId)
            {
                int sequenceGap = Helper.GetSequenceGap(sequenceId, lastReceivedSeqId);

                if (sequenceGap > 0)
                {
                    // The received sequence ID is newer than the previous one
                    receivedSeqIds.ShiftBy(sequenceGap);
                    lastReceivedSeqId = sequenceId;

                    if (receivedSeqIds.IsSet(sequenceGap))
                        return false;

                    receivedSeqIds.Set(sequenceGap);
                    return true;
                }
                else
                {
                    // The received sequence ID is older than or the same as the previous one (out of order or duplicate message)
                    return false;
                }
            }

            /// <inheritdoc/>
            internal override void UpdateReceivedAcks(ushort remoteLastReceivedSeqId, ushort remoteReceivedSeqIds)
            {
                int sequenceGap = Helper.GetSequenceGap(remoteLastReceivedSeqId, lastAckedSeqId);

                if (sequenceGap > 0)
                {
                    if (sequenceGap > 1)
                    {
                        // Deal with messages in the gap
                        while (sequenceGap > 9) // 9 because a gap of 1 means sequence IDs are consecutive, and notify uses 8 bits for the bitfield. 9 means all 8 bits are in use
                        {
                            lastAckedSeqId++;
                            sequenceGap--;
                            connection.NotifyLost?.Invoke(lastAckedSeqId);
                        }

                        int bitCount = sequenceGap - 1;
                        int bit = 1 << bitCount;
                        for (int i = 0; i < bitCount; i++)
                        {
                            lastAckedSeqId++;
                            bit >>= 1;
                            if ((remoteReceivedSeqIds & bit) == 0)
                                connection.OnNotifyLost(lastAckedSeqId);
                            else
                                connection.OnNotifyDelivered(lastAckedSeqId);
                        }
                    }

                    lastAckedSeqId = remoteLastReceivedSeqId;
                    connection.OnNotifyDelivered(lastAckedSeqId);
                }
            }
        }

        /// <inheritdoc/>
        private class ReliableSequencer : Sequencer
        {
            /// <inheritdoc/>
            internal ReliableSequencer(Connection connection) : base(connection) { }

            /// <inheritdoc/>
            /// <remarks>Duplicate messages are filtered out while out of order messages are handled.</remarks>
            internal override bool ShouldHandle(ushort sequenceId)
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
                            RiptideLogger.Log(LogType.Warning, connection.Peer.LogName, $"The gap between received sequence IDs was very large ({sequenceGap})!");

                        receivedSeqIds.ShiftBy(sequenceGap);
                        lastReceivedSeqId = sequenceId;
                    }
                    else // The received sequence ID is older than the previous one (out of order message)
                        sequenceGap = -sequenceGap;

                    doHandle = !receivedSeqIds.IsSet(sequenceGap);
                    receivedSeqIds.Set(sequenceGap);
                }

                connection.SendAck(sequenceId, lastReceivedSeqId, receivedSeqIds);
                return doHandle;
            }

            /// <summary>Updates which messages we've received acks for.</summary>
            /// <param name="remoteLastReceivedSeqId">The latest sequence ID that the other end has received.</param>
            /// <param name="remoteReceivedSeqIds">Sequence IDs which the other end has (or has not) received.</param>
            internal override void UpdateReceivedAcks(ushort remoteLastReceivedSeqId, ushort remoteReceivedSeqIds)
            {
                int sequenceGap = Helper.GetSequenceGap(remoteLastReceivedSeqId, lastAckedSeqId);

                if (sequenceGap > 0)
                {
                    // The latest sequence ID that the other end has received is newer than the previous one
                    if (!ackedSeqIds.HasCapacityFor(sequenceGap, out int overflow))
                    {
                        for (int i = 0; i < overflow; i++)
                        {
                            // Resend those messages which haven't been acked and whose sequence IDs are about to be pushed out of the bitfield
                            if (!ackedSeqIds.CheckAndTrimLast(out int checkedPosition))
                                connection.ResendMessage((ushort)(lastAckedSeqId - checkedPosition));
                            else
                                connection.ClearMessage((ushort)(lastAckedSeqId - checkedPosition));
                        }
                    }

                    ackedSeqIds.ShiftBy(sequenceGap);
                    lastAckedSeqId = remoteLastReceivedSeqId;

                    for (int i = 0; i < 16; i++)
                    {
                        // Clear any messages that have been newly acknowledged
                        if (!ackedSeqIds.IsSet(i + 1) && (remoteReceivedSeqIds & (1 << i)) != 0)
                            connection.ClearMessage((ushort)(lastAckedSeqId - (i + 1)));
                    }

                    ackedSeqIds.Combine(remoteReceivedSeqIds);
                    ackedSeqIds.Set(sequenceGap); // Ensure that the bit corresponding to the previous acked sequence ID is set
                    connection.ClearMessage(remoteLastReceivedSeqId);
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
        }
        #endregion
    }
}
