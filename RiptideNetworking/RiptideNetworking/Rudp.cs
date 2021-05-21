using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Timer = System.Timers.Timer;

namespace RiptideNetworking
{
    internal class Rudp
    {
        private int lastSequenceId;
        internal ushort NextSequenceId { get => (ushort)Interlocked.Increment(ref lastSequenceId); }
        protected const ushort LeftBit = 1 << 15;

        internal SendLockables SendLockables { get; private set; }
        internal ReceiveLockables ReceiveLockables { get; private set; }
        internal Dictionary<ushort, PendingMessage> PendingMessages { get; private set; } = new Dictionary<ushort, PendingMessage>();
        protected readonly float retryTimeMultiplier = 1.2f;

        internal delegate void Send(byte[] data, IPEndPoint toEndPoint);
        private Send send;
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

        internal Rudp(Send send, string logName)
        {
            this.send = send;
            this.logName = logName;
            SendLockables = new SendLockables();
            ReceiveLockables = new ReceiveLockables();
        }

        internal void UpdateReceivedAcks(ushort remoteLastReceivedSeqId, ushort remoteAcksBitField)
        {
            lock (ReceiveLockables) lock (PendingMessages)
            {
                // Update which messages we've received acks for
                int sequenceGap = GetSequenceGap(remoteLastReceivedSeqId, ReceiveLockables.LastAckedSeqId);
                if (sequenceGap > 0)
                {
                    for (int i = 1; i < sequenceGap; i++)
                    {
                        ReceiveLockables.AckedMessagesBitfield <<= 1;
                        CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16 + i), LeftBit);
                    }
                    ReceiveLockables.AckedMessagesBitfield <<= 1; // Shift the bits left to make room for the latest ack
                    ReceiveLockables.AckedMessagesBitfield |= (ushort)(remoteAcksBitField | (1 << sequenceGap - 1)); // Combine the bit fields and ensure that the bit corresponding to the ack is set to 1
                    ReceiveLockables.LastAckedSeqId = remoteLastReceivedSeqId;

                    CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16), LeftBit);
                }
                else if (sequenceGap < 0)
                {
                    sequenceGap = (ushort)(-sequenceGap - 1); // Because bit shifting is 0-based
                    ushort ackedBit = (ushort)(1 << sequenceGap); // Calculate which bit corresponds to the sequence ID and set it to 1
                    ReceiveLockables.AckedMessagesBitfield |= ackedBit; // Set the bit corresponding to the sequence ID
                    if (PendingMessages.TryGetValue(remoteLastReceivedSeqId, out PendingMessage pendingMessage))
                        pendingMessage.Clear();
                }
                else
                {
                    ReceiveLockables.AckedMessagesBitfield |= remoteAcksBitField; // Combine the bit fields
                    CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16), LeftBit);
                }
            }
        }

        /// <summary>Calculates the (signed) gap between sequence IDs, accounting for wrapping.</summary>
        /// <param name="seqId">The new sequence ID.</param>
        /// <param name="lastReceivedSeqId">The </param>
        /// <returns>The (signed) gap between the two given sequence IDs.</returns>
        internal static int GetSequenceGap(ushort seqId, ushort lastReceivedSeqId)
        {
            int gap = seqId - lastReceivedSeqId;
            if (Math.Abs(gap) <= 32768) // Difference is small, meaning sequence IDs are close together
                return gap;
            else // Difference is big, meaning sequence IDs are far apart
                return (seqId <= 32768 ? ushort.MaxValue + 1 + seqId : seqId) - (lastReceivedSeqId <= 32768 ? ushort.MaxValue + 1 + lastReceivedSeqId : lastReceivedSeqId);
        }

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

        internal void AckMessage(ushort seqId)
        {
            lock (PendingMessages)
            {
                if (PendingMessages.TryGetValue(seqId, out PendingMessage pendingMessage))
                    pendingMessage.Clear();
            }
        }

        internal class PendingMessage
        {
            private Rudp rudp;
            private IPEndPoint remoteEndPoint;
            private ushort sequenceId;
            private byte[] data;
            private byte maxSendAttempts;
            private byte sendAttempts;
            private DateTime lastSendTime;
            private Timer retryTimer;
            private bool wasCleared;

            internal PendingMessage(Rudp rudp, ushort sequenceId, byte[] data, IPEndPoint toEndPoint, byte maxSendAttempts)
            {
                this.rudp = rudp;
                this.sequenceId = sequenceId;
                this.data = data;
                remoteEndPoint = toEndPoint;
                this.maxSendAttempts = maxSendAttempts;
                sendAttempts = 0;

                retryTimer = new Timer();
                retryTimer.Elapsed += (s, e) => RetrySend();
                retryTimer.AutoReset = false;
            }

            internal void RetrySend()
            {
                if (data != null && lastSendTime.AddMilliseconds(rudp.SmoothRTT * 0.75f) <= DateTime.UtcNow)
                    TrySend();
            }

#if SIMULATE_LOSS
            private static Random randomLoss = new Random();
#endif
            internal void TrySend()
            {
                if (sendAttempts >= maxSendAttempts)
                {
                    HeaderType headerType = (HeaderType)data[0];
                    if (headerType == HeaderType.reliable)
                    {
                        byte[] idBytes = new byte[Message.shortLength];
                        Array.Copy(data, 3, idBytes, 0, Message.shortLength);
                        RiptideLogger.Log(rudp.logName, $"No ack received for {headerType} message (ID: {BitConverter.ToUInt16(Message.StandardizeEndianness(idBytes), 0)}) after {sendAttempts} attempt(s), delivery may have failed!");
                    }
                    else
                        RiptideLogger.Log(rudp.logName, $"No ack received for internal {headerType} message after {sendAttempts} attempt(s), delivery may have failed!");

                    Clear();
                    return;
                }

#if SIMULATE_LOSS
                float lossChance = randomLoss.Next(100) / 100f;
                if (lossChance > 0.1f)
                    rudp.send(data, remoteEndPoint);
#else
                rudp.send(data, remoteEndPoint);
#endif

                lastSendTime = DateTime.UtcNow;
                sendAttempts++;

                retryTimer.Stop();
                retryTimer.Interval = Math.Max(10, rudp.SmoothRTT * rudp.retryTimeMultiplier);
                retryTimer.Start();
            }

            internal void Clear()
            {
                lock (this)
                {
                    if (!wasCleared)
                    {
                        rudp.PendingMessages.Remove(sequenceId);

                        data = null;
                        retryTimer.Stop();
                        retryTimer.Dispose();
                        wasCleared = true;
                    }
                }
            }
        }
    }
}
