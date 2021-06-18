using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Timer = System.Timers.Timer;

namespace RiptideNetworking
{
    /// <summary>Provides functionality for sending and receiving messages reliably.</summary>
    internal class Rudp
    {
        private int lastSequenceId;
        /// <summary>The next sequence ID to use.</summary>
        internal ushort NextSequenceId => (ushort)Interlocked.Increment(ref lastSequenceId);
        /// <summary>A <see langword="ushort"/> with the left-most bit set to 1.</summary>
        protected const ushort LeftBit = 1 << 15;

        /// <summary>The lockable values which are used to inform the other end of which messages we've received.</summary>
        internal SendLockables SendLockables { get; private set; }
        /// <summary>The lockable values which are used to determine which messages the other end has received.</summary>
        internal ReceiveLockables ReceiveLockables { get; private set; }
        /// <summary>The currently pending reliably sent messages whose delivery has not been acknowledged. Stored by sequence ID.</summary>
        internal Dictionary<ushort, PendingMessage> PendingMessages { get; private set; } = new Dictionary<ushort, PendingMessage>();
        /// <summary>The multiplier used to determine how long to wait before resending a pending message.</summary>
        protected readonly float retryTimeMultiplier = 1.2f;

        /// <summary>Signature for methods used to send data.</summary>
        /// <param name="data">The data to send.</param>
        /// <param name="toEndPoint">The endpoint to send the data to.</param>
        internal delegate void Send(byte[] data, IPEndPoint toEndPoint);
        /// <summary>The method to use when sending data.</summary>
        private readonly Send send;
        /// <summary>The name to use when logging messages via <see cref="RiptideLogger"/>.</summary>
        private readonly string logName;

        private short _rtt = -1;
        /// <summary>The round trip time of the connection. -1 if not calculated yet.</summary>
        internal short RTT
        {
            get => _rtt;
            set
            {
                SmoothRTT = _rtt == -1 ? value : (short)Math.Max(1f, SmoothRTT * 0.7f + value * 0.3f);
                _rtt = value;
            }
        }
        /// <summary>The smoothed round trip time of the connection. -1 if not calculated yet.</summary>
        internal short SmoothRTT { get; set; } = -1;

        /// <summary>Handles initial setup.</summary>
        /// <param name="send">The method to use when sending data.</param>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        internal Rudp(Send send, string logName)
        {
            this.send = send;
            this.logName = logName;
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

        /// <summary>Calculates the (signed) gap between sequence IDs, accounting for wrapping.</summary>
        /// <param name="seqId1">The new sequence ID.</param>
        /// <param name="seqId2">The previous sequence ID.</param>
        /// <returns>The (signed) gap between the two given sequence IDs.</returns>
        internal static int GetSequenceGap(ushort seqId1, ushort seqId2)
        {
            int gap = seqId1 - seqId2;
            if (Math.Abs(gap) <= 32768) // Difference is small, meaning sequence IDs are close together
                return gap;
            else // Difference is big, meaning sequence IDs are far apart
                return (seqId1 <= 32768 ? ushort.MaxValue + 1 + seqId1 : seqId1) - (seqId2 <= 32768 ? ushort.MaxValue + 1 + seqId2 : seqId2);
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

        /// <summary>Represents a currently pending reliably sent message whose delivery has not been acknowledged.</summary>
        internal class PendingMessage
        {
            /// <summary>The Rudp instance to use to send (and resend) the pending message.</summary>
            private readonly Rudp rudp;
            /// <summary>The intended destination endpoint of the message.</summary>
            private readonly IPEndPoint remoteEndPoint;
            /// <summary>The sequence ID of the message.</summary>
            private readonly ushort sequenceId;
            /// <summary>The contents of the message.</summary>
            private readonly byte[] data;
            /// <summary>How often to try sending the message before giving up.</summary>
            private readonly byte maxSendAttempts;
            /// <summary>How many send attempts have been made.</summary>
            private byte sendAttempts;
            /// <summary>The time of the latest send attempt.</summary>
            private DateTime lastSendTime;
            /// <summary>The timer responsible for triggering a resend, if all else fails (like acks getting lost or redundant acks not being updated fast enough).</summary>
            private readonly Timer retryTimer;
            /// <summary>Whether the pending message has been cleared or not.</summary>
            private bool wasCleared;

            /// <summary>Handles initial setup.</summary>
            /// <param name="rudp">The Rudp instance to use to send (and resend) the pending message.</param>
            /// <param name="sequenceId">The sequence ID of the message.</param>
            /// <param name="message">The message that is being sent reliably.</param>
            /// <param name="toEndPoint">The intended destination endpoint of the message.</param>
            /// <param name="maxSendAttempts">How often to try sending the message before giving up.</param>
            internal PendingMessage(Rudp rudp, ushort sequenceId, Message message, IPEndPoint toEndPoint, byte maxSendAttempts)
            {
                this.rudp = rudp;
                this.sequenceId = sequenceId;
                data = new byte[message.WrittenLength];
                Array.Copy(message.Bytes, data, data.Length);
                remoteEndPoint = toEndPoint;
                this.maxSendAttempts = maxSendAttempts;
                sendAttempts = 0;

                retryTimer = new Timer();
                retryTimer.Elapsed += (s, e) => RetrySend();
                retryTimer.AutoReset = false;
            }

            /// <summary>Resends the message.</summary>
            internal void RetrySend()
            {
                lock (this) // Make sure we don't try resending the message while another thread is clearing it because it was delivered
                {
                    if (!wasCleared)
                    {
                        if (lastSendTime.AddMilliseconds(rudp.SmoothRTT < 0 ? 25 : rudp.SmoothRTT * 0.5f) <= DateTime.UtcNow) // Avoid triggering a resend if the latest resend was less than half a RTT ago
                            TrySend();
                        else
                        {
                            retryTimer.Start();
                            retryTimer.Interval = (rudp.SmoothRTT < 0 ? 50 : Math.Max(10, rudp.SmoothRTT * rudp.retryTimeMultiplier));
                        }
                    }
                }
            }

            /// <summary>Attempts to send the message.</summary>
            internal void TrySend()
            {
                if (sendAttempts >= maxSendAttempts)
                {
                    // Send attempts exceeds max send attempts, so give up
                    HeaderType headerType = (HeaderType)data[0];
                    if (headerType == HeaderType.reliable)
                    {
#if BIG_ENDIAN
                        byte[] idBytes = new byte[Message.shortLength];
                        Array.Copy(data, 3, idBytes, 0, Message.shortLength);
                        RiptideLogger.Log(rudp.logName, $"No ack received for {headerType} message (ID: {BitConverter.ToUInt16(Message.StandardizeEndianness(idBytes), 0)}) after {sendAttempts} attempt(s), delivery may have failed!");
#else
                        Console.WriteLine($"[{rudp.logName}] No ack received for {headerType} message (ID: {BitConverter.ToUInt16(data, 3)}) after {sendAttempts} attempt(s), delivery may have failed!"); // TODO: Might need to be rethinked
#endif
                    }
                    else
                        Console.WriteLine($"[{rudp.logName}] No ack received for internal {headerType} message after {sendAttempts} attempt(s), delivery may have failed!"); // TODO: Might need to be rethinked

                    Clear();
                    return;
                }

                rudp.send(data, remoteEndPoint);
                
                lastSendTime = DateTime.UtcNow;
                sendAttempts++;

                retryTimer.Start();
                retryTimer.Interval = rudp.SmoothRTT < 0 ? 50 : Math.Max(10, rudp.SmoothRTT * rudp.retryTimeMultiplier);
            }

            /// <summary>Clears and removes the message from the dictionary of pending messages.</summary>
            internal void Clear()
            {
                lock (this)
                {
                    if (!wasCleared)
                    {
                        lock (rudp.PendingMessages)
                            rudp.PendingMessages.Remove(sequenceId);
                        
                        retryTimer.Stop();
                        retryTimer.Dispose();
                        wasCleared = true;
                    }
                }
            }
        }
    }
}
