using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Timer = System.Timers.Timer;

namespace RiptideNetworking
{
    class Rudp
    {
        private int lastSequenceId;
        internal ushort NextSequenceId { get => (ushort)Interlocked.Increment(ref lastSequenceId); }
        protected const ushort LeftBit = 1 << 15;

        internal SendLockables SendLockables { get; private set; }
        internal ReceiveLockables ReceiveLockables { get; private set; }
        internal Dictionary<ushort, PendingMessage> PendingMessages { get; private set; } = new Dictionary<ushort, PendingMessage>();
        protected readonly float retryTimeMultiplier = 1.05f;

        internal delegate void Send(byte[] data, IPEndPoint toEndPoint);
        private Send send;
        private readonly string logName;

        private ushort _rtt = 1;
        internal ushort RTT
        {
            get => _rtt;
            set
            {
                _rtt = value;
                SmoothRTT = (ushort)Math.Max(1f, SmoothRTT * 0.7f + value * 0.3f);
            }
        }
        internal ushort SmoothRTT { get; set; } = 1;

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
                int ackedSequenceGap = remoteLastReceivedSeqId - ReceiveLockables.LastAckedSeqId; // TODO: account for wrapping
                if (ackedSequenceGap > 0)
                {
                    for (int i = 1; i < ackedSequenceGap; i++)
                    {
                        ReceiveLockables.AckedMessagesBitfield <<= 1;
                        CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16 + i), LeftBit);
                    }
                    ReceiveLockables.AckedMessagesBitfield <<= 1; // Shift the bits left to make room for the latest ack
                    ReceiveLockables.AckedMessagesBitfield |= (ushort)(remoteAcksBitField | (1 << ackedSequenceGap - 1)); // Combine the bit fields and ensure that the bit corresponding to the ack is set to 1
                    ReceiveLockables.LastAckedSeqId = remoteLastReceivedSeqId;

                    CheckMessageAckStatus((ushort)(ReceiveLockables.LastAckedSeqId - 16), LeftBit);
                }
                else if (ackedSequenceGap < 0)
                {
                    ackedSequenceGap = (ushort)(-ackedSequenceGap - 1); // Because bit shifting is 0-based
                    ushort ackedBit = (ushort)(1 << ackedSequenceGap); // Calculate which bit corresponds to the sequence ID and set it to 1
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
            private List<ushort> sequenceIds;
            private byte[] data;
            private byte maxSendAttempts;
            private byte sendAttemtpts;
            private Timer retryTimer;
            private bool wasCleared;

            internal PendingMessage(Rudp rudp, ushort sequenceId, byte[] data, IPEndPoint toEndPoint, byte maxSendAttempts)
            {
                this.rudp = rudp;
                sequenceIds = new List<ushort>();
                sequenceIds.Add(sequenceId);
                this.data = data;
                remoteEndPoint = toEndPoint;
                this.maxSendAttempts = maxSendAttempts;
                sendAttemtpts = 0;

                retryTimer = new Timer();
                retryTimer.Elapsed += (s, e) => RetrySend();
                retryTimer.AutoReset = false;
                //retryTimer = new System.Threading.Timer(RetrySend, null, Timeout.Infinite, Timeout.Infinite);
            }

            private void RetrySend(object state)
            {
                RetrySend();
            }
            internal void RetrySend()
            {
                lock (rudp.SendLockables) lock (rudp.PendingMessages)
                {
                    if (data != null)
                    {
                        //rudp.PendingMessages.Remove(sequenceIds);

                        ushort sequenceId = rudp.NextSequenceId;
                        Array.Copy(BitConverter.GetBytes(sequenceId), 0, data, 1, 2); // Overwrite the sequence ID
                        //Array.Copy(BitConverter.GetBytes(rudpSocket.lockables.lastReceivedSeqId), 0, data, 5, 2); // Overwrite the last remote sequence ID
                        //Array.Copy(BitConverter.GetBytes(rudpSocket.lockables.acksBitfield), 0, data, 7, 2); // Overwrite the acks
                        rudp.PendingMessages.Add(sequenceId, this);
                        sequenceIds.Add(sequenceId);
                        TrySend();
                    }
                }
            }

            static Random randomLoss = new Random();
            internal void TrySend()
            {
                if (sendAttemtpts >= maxSendAttempts)
                {
                    RiptideLogger.Log(rudp.logName, $"Failed to deliver {(HeaderType)data[0]} message after {sendAttemtpts} attempt(s)!");
                    Clear();
                    return;
                }

                float lossChance = randomLoss.Next(100) / 100f;
                if (lossChance > 0.1f)
                    rudp.send(data, remoteEndPoint);

                sendAttemtpts++;

                //retryTimer.Change(0, (int)Math.Max(10, rudp.SmoothRTT * rudp.retryTimeMultiplier));
                retryTimer.Interval = Math.Max(50, rudp.SmoothRTT * rudp.retryTimeMultiplier);
                retryTimer.Start();
            }

            internal void Clear()
            {
                lock (data)
                {
                    if (!wasCleared)
                    {
                        foreach (ushort sequenceId in sequenceIds)
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
