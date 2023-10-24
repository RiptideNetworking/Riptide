// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

namespace Riptide.Utils
{
    /// <summary>Tracks and manages various metrics of a <see cref="Connection"/>.</summary>
    public class ConnectionMetrics
    {
        /// <summary>The total number of bytes received across all send modes since the last <see cref="Reset"/> call, including those in duplicate and, in
        /// the case of notify messages, out-of-order packets. Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int BytesIn => UnreliableBytesIn + NotifyBytesIn + ReliableBytesIn;
        /// <summary>The total number of bytes sent across all send modes since the last <see cref="Reset"/> call, including those in automatic resends.
        /// Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int BytesOut => UnreliableBytesOut + NotifyBytesOut + ReliableBytesOut;
        /// <summary>The total number of messages received across all send modes since the last <see cref="Reset"/> call, including duplicate and out-of-order notify messages.</summary>
        public int MessagesIn => UnreliableIn + NotifyIn + ReliableIn;
        /// <summary>The total number of messages sent across all send modes since the last <see cref="Reset"/> call, including automatic resends.</summary>
        public int MessagesOut => UnreliableOut + NotifyOut + ReliableOut;

        /// <summary>The total number of bytes received in unreliable messages since the last <see cref="Reset"/> call. Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int UnreliableBytesIn { get; private set; }
        /// <summary>The total number of bytes sent in unreliable messages since the last <see cref="Reset"/> call. Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int UnreliableBytesOut { get; internal set; }
        /// <summary>The number of unreliable messages received since the last <see cref="Reset"/> call.</summary>
        public int UnreliableIn { get; private set; }
        /// <summary>The number of unreliable messages sent since the last <see cref="Reset"/> call.</summary>
        public int UnreliableOut { get; internal set; }

        /// <summary>The total number of bytes received in notify messages since the last <see cref="Reset"/> call, including those in duplicate and out-of-order packets.
        /// Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int NotifyBytesIn { get; private set; }
        /// <summary>The total number of bytes sent in notify messages since the last <see cref="Reset"/> call. Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int NotifyBytesOut { get; internal set; }
        /// <summary>The number of notify messages received since the last <see cref="Reset"/> call, including duplicate and out-of-order ones.</summary>
        public int NotifyIn { get; private set; }
        /// <summary>The number of notify messages sent since the last <see cref="Reset"/> call.</summary>
        public int NotifyOut { get; internal set; }
        /// <summary>The number of duplicate or out-of-order notify messages which were received, but discarded (not handled) since the last <see cref="Reset"/> call.</summary>
        public int NotifyDiscarded { get; internal set; }
        /// <summary>The number of notify messages lost since the last <see cref="Reset"/> call.</summary>
        public int NotifyLost { get; private set; }
        /// <summary>The number of notify messages delivered since the last <see cref="Reset"/> call.</summary>
        public int NotifyDelivered { get; private set; }
        /// <summary>The number of notify messages lost of the last 64 notify messages to be lost or delivered.</summary>
        public int RollingNotifyLost { get; private set; }
        /// <summary>The number of notify messages delivered of the last 64 notify messages to be lost or delivered.</summary>
        public int RollingNotifyDelivered { get; private set; }
        /// <summary>The loss rate (0-1) among the last 64 notify messages.</summary>
        public float RollingNotifyLossRate => RollingNotifyLost / 64f;

        /// <summary>The total number of bytes received in reliable messages since the last <see cref="Reset"/> call, including those in duplicate packets.
        /// Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int ReliableBytesIn { get; private set; }
        /// <summary>The total number of bytes sent in reliable messages since the last <see cref="Reset"/> call, including those in automatic resends.
        /// Does <i>not</i> include packet header bytes, which may vary by transport.</summary>
        public int ReliableBytesOut { get; internal set; }
        /// <summary>The number of reliable messages received since the last <see cref="Reset"/> call, including duplicates.</summary>
        public int ReliableIn { get; private set; }
        /// <summary>The number of reliable messages sent since the last <see cref="Reset"/> call, including automatic resends (each resend adds to this value).</summary>
        public int ReliableOut { get; internal set; }
        /// <summary>The number of duplicate reliable messages which were received, but discarded (and not handled) since the last <see cref="Reset"/> call.</summary>
        public int ReliableDiscarded { get; internal set; }
        /// <summary>The number of unique reliable messages sent since the last <see cref="Reset"/> call.
        /// A message only counts towards this the first time it is sent—subsequent resends are not counted.</summary>
        public int ReliableUniques { get; internal set; }
        /// <summary>The number of send attempts that were required to deliver recent reliable messages.</summary>
        public readonly RollingStat RollingReliableSends;

        /// <summary>The left-most bit of a <see cref="ulong"/>, used to store the oldest value in the <see cref="notifyLossTracker"/>.</summary>
        private const ulong ULongLeftBit = 1ul << 63;
        /// <summary>Which recent notify messages were lost. Each bit corresponds to a message.</summary>
        private ulong notifyLossTracker;
        /// <summary>How many of the <see cref="notifyLossTracker"/>'s bits are in use.</summary>
        private int notifyBufferCount;

        /// <summary>Initializes metrics.</summary>
        public ConnectionMetrics()
        {
            Reset();
            RollingNotifyDelivered = 0;
            RollingNotifyLost = 0;
            notifyLossTracker = 0;
            notifyBufferCount = 0;
            RollingReliableSends = new RollingStat(64);
        }

        /// <summary>Resets all non-rolling metrics to 0.</summary>
        public void Reset()
        {
            UnreliableBytesIn = 0;
            UnreliableBytesOut = 0;
            UnreliableIn = 0;
            UnreliableOut = 0;

            NotifyBytesIn = 0;
            NotifyBytesOut = 0;
            NotifyIn = 0;
            NotifyOut = 0;
            NotifyDiscarded = 0;
            NotifyLost = 0;
            NotifyDelivered = 0;

            ReliableBytesIn = 0;
            ReliableBytesOut = 0;
            ReliableIn = 0;
            ReliableOut = 0;
            ReliableDiscarded = 0;
            ReliableUniques = 0;
        }

        /// <summary>Updates the metrics associated with receiving an unreliable message.</summary>
        /// <param name="byteCount">The number of bytes that were received.</param>
        internal void ReceivedUnreliable(int byteCount)
        {
            UnreliableBytesIn += byteCount;
            UnreliableIn++;
        }

        /// <summary>Updates the metrics associated with sending an unreliable message.</summary>
        /// <param name="byteCount">The number of bytes that were sent.</param>
        internal void SentUnreliable(int byteCount)
        {
            UnreliableBytesOut += byteCount;
            UnreliableOut++;
        }

        /// <summary>Updates the metrics associated with receiving a notify message.</summary>
        /// <param name="byteCount">The number of bytes that were received.</param>
        internal void ReceivedNotify(int byteCount)
        {
            NotifyBytesIn += byteCount;
            NotifyIn++;
        }

        /// <summary>Updates the metrics associated with sending a notify message.</summary>
        /// <param name="byteCount">The number of bytes that were sent.</param>
        internal void SentNotify(int byteCount)
        {
            NotifyBytesOut += byteCount;
            NotifyOut++;
        }

        /// <summary>Updates the metrics associated with delivering a notify message.</summary>
        internal void DeliveredNotify()
        {
            NotifyDelivered++;
            
            if (notifyBufferCount < 64)
            {
                RollingNotifyDelivered++;
                notifyBufferCount++;
            }
            else if ((notifyLossTracker & ULongLeftBit) == 0)
            {
                // The one being removed from the buffer was not delivered
                RollingNotifyDelivered++;
                RollingNotifyLost--;
            }

            notifyLossTracker <<= 1;
            notifyLossTracker |= 1;
        }

        /// <summary>Updates the metrics associated with losing a notify message.</summary>
        internal void LostNotify()
        {
            NotifyLost++;

            if (notifyBufferCount < 64)
            {
                RollingNotifyLost++;
                notifyBufferCount++;
            }
            else if ((notifyLossTracker & ULongLeftBit) != 0)
            {
                // The one being removed from the buffer was delivered
                RollingNotifyDelivered--;
                RollingNotifyLost++;
            }

            notifyLossTracker <<= 1;
        }

        /// <summary>Updates the metrics associated with receiving a reliable message.</summary>
        /// <param name="byteCount">The number of bytes that were received.</param>
        internal void ReceivedReliable(int byteCount)
        {
            ReliableBytesIn += byteCount;
            ReliableIn++;
        }

        /// <summary>Updates the metrics associated with sending a reliable message.</summary>
        /// <param name="byteCount">The number of bytes that were sent.</param>
        internal void SentReliable(int byteCount)
        {
            ReliableBytesOut += byteCount;
            ReliableOut++;
        }
    }
}
