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
    public enum ConnectionState : byte
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
        /// <summary>The connection's numeric ID.</summary>
        public ushort Id { get; internal set; }
        /// <summary>Whether or not the connection is currently <i>not</i> connected nor trying to connect.</summary>
        public bool IsNotConnected => state == ConnectionState.NotConnected;
        /// <summary>Whether or not the connection is currently in the process of connecting.</summary>
        public bool IsConnecting => state == ConnectionState.Connecting;
        /// <summary>Whether or not the connection is currently pending (will only be <see langword="true"/> when a server doesn't immediately accept the connection request).</summary>
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
        internal bool HasConnectAttemptTimedOut => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > Peer.TimeoutTime * 2;
        /// <summary>The currently pending reliably sent messages whose delivery has not been acknowledged yet. Stored by sequence ID.</summary>
        internal Dictionary<ushort, PendingMessage> PendingMessages { get; private set; } = new Dictionary<ushort, PendingMessage>();

        /// <summary>The sequence ID of the latest message that we want to acknowledge.</summary>
        private ushort LastReceivedSeqId { get; set; }
        /// <summary>Messages that we have received and want to acknowledge.</summary>
        private ushort AcksBitfield { get; set; }
        /// <summary>Messages that we have received whose sequence IDs no longer fall into <see cref="AcksBitfield"/>'s range. Used to improve duplicate message filtering capabilities.</summary>
        private ulong DuplicateFilterBitfield { get; set; }

        /// <summary>The sequence ID of the latest message that we've received an ack for.</summary>
        private ushort LastAckedSeqId { get; set; }
        /// <summary>Messages that we sent which have been acknoweledged.</summary>
        private ushort AckedMessagesBitfield { get; set; }
        
        /// <summary>A <see cref="ushort"/> with the left-most bit set to 1.</summary>
        private const ushort LeftBit = 0b_1000_0000_0000_0000;
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
        /// <param name="message">The message to send.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <remarks>
        ///   If you intend to continue using the message instance after calling this method, you <i>must</i> set <paramref name="shouldRelease"/>
        ///   to <see langword="false"/>. <see cref="Message.Release"/> can be used to manually return the message to the pool at a later time.
        /// </remarks>
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
            bool doHandle = true;
            // Update acks
            int sequenceGap = Helper.GetSequenceGap(sequenceId, LastReceivedSeqId);
            if (sequenceGap > 0)
            {
                // The received sequence ID is newer than the previous one
                if (sequenceGap > 64)
                    RiptideLogger.Log(LogType.Warning, Peer.LogName, $"The gap between received sequence IDs was very large ({sequenceGap})! If the connection's packet loss, latency, or your send rate of reliable messages increases much further, sequence IDs may begin falling outside the bounds of the duplicate filter.");

                DuplicateFilterBitfield <<= sequenceGap;
                if (sequenceGap <= 16)
                {
                    ulong shiftedBits = (ulong)AcksBitfield << sequenceGap;
                    AcksBitfield = (ushort)shiftedBits; // Give the acks bitfield the first 2 bytes of the shifted bits
                    DuplicateFilterBitfield |= shiftedBits >> 16; // OR the last 6 bytes worth of the shifted bits into the duplicate filter bitfield

                    doHandle = UpdateAcksBitfield(sequenceGap);
                    LastReceivedSeqId = sequenceId;
                }
                else if (sequenceGap <= 80)
                {
                    ulong shiftedBits = (ulong)AcksBitfield << (sequenceGap - 16);
                    AcksBitfield = 0; // Reset the acks bitfield as all its bits are being moved to the duplicate filter bitfield
                    DuplicateFilterBitfield |= shiftedBits; // OR the shifted bits into the duplicate filter bitfield

                    doHandle = UpdateDuplicateFilterBitfield(sequenceGap);
                }
            }
            else if (sequenceGap < 0)
            {
                // The received sequence ID is older than the previous one (out of order message)
                sequenceGap = -sequenceGap; // Make sequenceGap positive
                if (sequenceGap <= 16) // If the message's sequence ID still falls within the ack bitfield's value range
                    doHandle = UpdateAcksBitfield(sequenceGap);
                else if (sequenceGap <= 80) // If it's an "old" message and its sequence ID doesn't fall within the ack bitfield's value range anymore (but it falls in the range of the duplicate filter)
                    doHandle = UpdateDuplicateFilterBitfield(sequenceGap);
            }
            else // The received sequence ID is the same as the previous one (duplicate message)
                doHandle = false;

            SendAck(sequenceId);
            return doHandle;
        }

        /// <summary>Cleans up the local side of the connection.</summary>
        internal void LocalDisconnect()
        {
            state = ConnectionState.NotConnected;

            foreach (PendingMessage pendingMessage in PendingMessages.Values)
                pendingMessage.Clear(false);

            PendingMessages.Clear();
        }

        /// <summary>Updates the acks bitfield and determines whether or not to handle the message.</summary>
        /// <param name="sequenceGap">The gap between the newly received sequence ID and the previously last received sequence ID.</param>
        /// <returns>Whether or not the message should be handled, based on whether or not it's a duplicate.</returns>
        private bool UpdateAcksBitfield(int sequenceGap)
        {
            ushort seqIdBit = (ushort)(1 << sequenceGap - 1); // Calculate which bit corresponds to the sequence ID and set it to 1
            if ((AcksBitfield & seqIdBit) == 0)
            {
                // If we haven't received this message before
                AcksBitfield |= seqIdBit; // Set the bit corresponding to the sequence ID to 1 because we received that ID
                return true; // Message was "new", handle it
            }
            else // If we have received this message before
                return false; // Message was a duplicate, don't handle it
        }

        /// <summary>Updates the duplicate filter bitfield and determines whether or not to handle the message.</summary>
        /// <param name="sequenceGap">The gap between the newly received sequence ID and the previously last received sequence ID.</param>
        /// <returns>Whether or not the message should be handled, based on whether or not it's a duplicate.</returns>
        private bool UpdateDuplicateFilterBitfield(int sequenceGap)
        {
            ulong seqIdBit = (ulong)1 << (sequenceGap - 1 - 16); // Calculate which bit corresponds to the sequence ID and set it to 1
            if ((DuplicateFilterBitfield & seqIdBit) == 0)
            {
                // If we haven't received this message before
                DuplicateFilterBitfield |= seqIdBit; // Set the bit corresponding to the sequence ID to 1 because we received that ID
                return true; // Message was "new", handle it
            }
            else // If we have received this message before
                return false; // Message was a duplicate, don't handle it
        }

        /// <summary>Updates which messages we've received acks for.</summary>
        /// <param name="remoteLastReceivedSeqId">The latest sequence ID that the other end has received.</param>
        /// <param name="remoteAcksBitField">A redundant list of sequence IDs that the other end has (or has not) received.</param>
        internal void UpdateReceivedAcks(ushort remoteLastReceivedSeqId, ushort remoteAcksBitField)
        {
            int sequenceGap = Helper.GetSequenceGap(remoteLastReceivedSeqId, LastAckedSeqId);
            if (sequenceGap > 0)
            {
                // The latest sequence ID that the other end has received is newer than the previous one
                for (int i = 1; i < sequenceGap; i++) // NOTE: loop starts at 1, meaning it only runs if the gap in sequence IDs is greater than 1
                {
                    AckedMessagesBitfield <<= 1; // Shift the bits left to make room for a previous ack
                    CheckMessageAckStatus((ushort)(LastAckedSeqId - 16 + i), LeftBit); // Check the ack status of the oldest sequence ID in the bitfield (before it's removed)
                }
                AckedMessagesBitfield <<= 1; // Shift the bits left to make room for the latest ack
                AckedMessagesBitfield |= (ushort)(remoteAcksBitField | (1 << sequenceGap - 1)); // Combine the bit fields and ensure that the bit corresponding to the ack is set to 1
                LastAckedSeqId = remoteLastReceivedSeqId;

                CheckMessageAckStatus((ushort)(LastAckedSeqId - 16), LeftBit); // Check the ack status of the oldest sequence ID in the bitfield
            }
            else if (sequenceGap < 0)
            {
                // TODO: remove? I don't think this case ever executes
                // The latest sequence ID that the other end has received is older than the previous one (out of order ack)
                sequenceGap = (ushort)(-sequenceGap - 1); // Because bit shifting is 0-based
                ushort ackedBit = (ushort)(1 << sequenceGap); // Calculate which bit corresponds to the sequence ID and set it to 1
                AckedMessagesBitfield |= ackedBit; // Set the bit corresponding to the sequence ID
                if (PendingMessages.TryGetValue(remoteLastReceivedSeqId, out PendingMessage pendingMessage))
                    pendingMessage.Clear(); // Message was successfully delivered, remove it from the pending messages.
            }
            else
            {
                // The latest sequence ID that the other end has received is the same as the previous one (duplicate ack)
                AckedMessagesBitfield |= remoteAcksBitField; // Combine the bit fields
                CheckMessageAckStatus((ushort)(LastAckedSeqId - 16), LeftBit); // Check the ack status of the oldest sequence ID in the bitfield
            }
        }

        /// <summary>Check the ack status of the given sequence ID.</summary>
        /// <param name="sequenceId">The sequence ID whose ack status to check.</param>
        /// <param name="bit">The bit corresponding to the sequence ID's position in the bit field.</param>
        private void CheckMessageAckStatus(ushort sequenceId, ushort bit)
        {
            if ((AckedMessagesBitfield & bit) == 0)
            {
                // Message was lost
                if (PendingMessages.TryGetValue(sequenceId, out PendingMessage pendingMessage))
                    pendingMessage.RetrySend();
            }
            else
            {
                // Message was successfully delivered
                if (PendingMessages.TryGetValue(sequenceId, out PendingMessage pendingMessage))
                    pendingMessage.Clear();
            }
        }

        /// <summary>Immediately marks the <see cref="PendingMessage"/> of a given sequence ID as delivered.</summary>
        /// <param name="seqId">The sequence ID that was acknowledged.</param>
        internal void AckMessage(ushort seqId)
        {
            if (PendingMessages.TryGetValue(seqId, out PendingMessage pendingMessage))
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
            Message message = Message.Create(forSeqId == LastReceivedSeqId ? MessageHeader.Ack : MessageHeader.AckExtra);
            message.AddUShort(LastReceivedSeqId); // Last remote sequence ID
            message.AddUShort(AcksBitfield); // Acks

            if (forSeqId != LastReceivedSeqId)
                message.AddUShort(forSeqId);
            
            Send(message);
        }

        /// <summary>Handles an ack message.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            AckMessage(remoteLastReceivedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Handles an ack message for a sequence ID other than the last received one.</summary>
        /// <param name="message">The ack message to handle.</param>
        internal void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            AckMessage(ackedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
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
