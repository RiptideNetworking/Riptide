
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Threading;

namespace RiptideNetworking.Transports.RudpTransport
{
    /// <summary>Provides functionality for sending and receiving messages reliably.</summary>
    internal class RudpPeer
    {
        /// <summary>The next sequence ID to use.</summary>
        internal ushort NextSequenceId => (ushort)Interlocked.Increment(ref lastSequenceId);
        /// <summary>The lockable values which are used to inform the other end of which messages we've received.</summary>
        internal SendLockables SendLockables { get; private set; }
        /// <summary>The lockable values which are used to determine which messages the other end has received.</summary>
        internal ReceiveLockables ReceiveLockables { get; private set; }
        /// <summary>The currently pending reliably sent messages whose delivery has not been acknowledged yet. Stored by sequence ID.</summary>
        internal Dictionary<ushort, PendingMessage> PendingMessages { get; private set; } = new Dictionary<ushort, PendingMessage>();
        /// <inheritdoc cref="IConnectionInfo.RTT"/>
        internal short RTT
        {
            get => _rtt;
            set
            {
                SmoothRTT = _rtt == -1 ? value : (short)Math.Max(1f, SmoothRTT * 0.7f + value * 0.3f);
                _rtt = value;
            }
        }
        private short _rtt = -1;
        /// <inheritdoc cref="IConnectionInfo.SmoothRTT"/>
        internal short SmoothRTT { get; set; } = -1;
        /// <summary>The <see cref="RudpListener"/> whose socket to use when sending data.</summary>
        internal readonly RudpListener Listener;

        /// <summary>The last used sequence ID.</summary>
        private int lastSequenceId;
        /// <summary>A <see cref="ushort"/> with the left-most bit set to 1.</summary>
        private const ushort LeftBit = 1 << 15;

        /// <summary>Handles initial setup.</summary>
        /// <param name="rudpListener">The <see cref="RudpListener"/> whose socket to use when sending data.</param>
        internal RudpPeer(RudpListener rudpListener)
        {
            Listener = rudpListener;
            SendLockables = new SendLockables();
            ReceiveLockables = new ReceiveLockables();
        }

        /// <summary>Updates which messages we've received acks for.</summary>
        /// <param name="remoteLastReceivedSeqId">The latest sequence ID that the other end has received.</param>
        /// <param name="remoteAcksBitField">A redundant list of sequence IDs that the other end has (or has not) received.</param>
        internal void UpdateReceivedAcks(ushort remoteLastReceivedSeqId, ushort remoteAcksBitField)
        {
            lock (ReceiveLockables) lock (PendingMessages)
                {
                    int sequenceGap = GetSequenceGap(remoteLastReceivedSeqId, ReceiveLockables.LastAckedSeqId);
                    if (sequenceGap > 0)
                    {
                        // The latest sequence ID that the other end has received is newer than the previous one
                        for (int i = 1; i < sequenceGap; i++) // NOTE: loop starts at 1, meaning it only runs if the gap in sequence IDs is greater than 1
                        {
                            ReceiveLockables.AckedMessagesBitfield <<= 1; // Shift the bits left to make room for a previous ack
                            CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16 + i), LeftBit); // Check the ack status of the oldest sequence ID in the bitfield (before it's removed)
                        }
                        ReceiveLockables.AckedMessagesBitfield <<= 1; // Shift the bits left to make room for the latest ack
                        ReceiveLockables.AckedMessagesBitfield |= (ushort)(remoteAcksBitField | (1 << sequenceGap - 1)); // Combine the bit fields and ensure that the bit corresponding to the ack is set to 1
                        ReceiveLockables.LastAckedSeqId = remoteLastReceivedSeqId;

                        CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16), LeftBit); // Check the ack status of the oldest sequence ID in the bitfield
                    }
                    else if (sequenceGap < 0)
                    {
                        // TODO: remove? I don't think this case ever executes
                        // The latest sequence ID that the other end has received is older than the previous one (out of order ack)
                        sequenceGap = (ushort)(-sequenceGap - 1); // Because bit shifting is 0-based
                        ushort ackedBit = (ushort)(1 << sequenceGap); // Calculate which bit corresponds to the sequence ID and set it to 1
                        ReceiveLockables.AckedMessagesBitfield |= ackedBit; // Set the bit corresponding to the sequence ID
                        if (PendingMessages.TryGetValue(remoteLastReceivedSeqId, out PendingMessage pendingMessage))
                            pendingMessage.Clear(); // Message was successfully delivered, remove it from the pending messages.
                    }
                    else
                    {
                        // The latest sequence ID that the other end has received is the same as the previous one (duplicate ack)
                        ReceiveLockables.AckedMessagesBitfield |= remoteAcksBitField; // Combine the bit fields
                        CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16), LeftBit); // Check the ack status of the oldest sequence ID in the bitfield
                    }
                }
        }

        /// <summary>Check the ack status of the given sequence ID.</summary>
        /// <param name="sequenceId">The sequence ID whose ack status to check.</param>
        /// <param name="bit">The bit corresponding to the sequence ID's position in the bit field.</param>
        private void CheckMessageAckStatus(ushort sequenceId, ushort bit)
        {
            if ((ReceiveLockables.AckedMessagesBitfield & bit) == 0)
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
            lock (PendingMessages)
            {
                if (PendingMessages.TryGetValue(seqId, out PendingMessage pendingMessage))
                    pendingMessage.Clear();
            }
        }

        /// <summary>Calculates the signed gap between sequence IDs, accounting for wrapping.</summary>
        /// <param name="seqId1">The new sequence ID.</param>
        /// <param name="seqId2">The previous sequence ID.</param>
        /// <returns>The signed gap between the two given sequence IDs. A positive gap means <paramref name="seqId1"/> is newer than <paramref name="seqId2"/>. A negative gap means <paramref name="seqId1"/> is older than <paramref name="seqId2"/>.</returns>
        internal static int GetSequenceGap(ushort seqId1, ushort seqId2)
        {
            int gap = seqId1 - seqId2;
            if (Math.Abs(gap) <= 32768) // Difference is small, meaning sequence IDs are close together
                return gap;
            else // Difference is big, meaning sequence IDs are far apart
                return (seqId1 <= 32768 ? ushort.MaxValue + 1 + seqId1 : seqId1) - (seqId2 <= 32768 ? ushort.MaxValue + 1 + seqId2 : seqId2);
        }
    }
}