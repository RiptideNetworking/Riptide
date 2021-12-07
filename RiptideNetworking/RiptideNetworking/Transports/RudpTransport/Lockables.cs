
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

namespace RiptideNetworking.Transports.RudpTransport
{
    /// <summary>Contains values that are accessed by multiple threads and are used to inform the other end of which messages we've received.</summary>
    internal class SendLockables
    {
        /// <summary>The sequence ID of the latest message that we want to acknowledge.</summary>
        internal ushort LastReceivedSeqId { get; set; }
        /// <summary>Messages that we have received and want to acknowledge.</summary>
        internal ushort AcksBitfield { get; set; }
        /// <summary>Messages that we have received whose sequence IDs no longer fall into <see cref="AcksBitfield"/>'s range, used to improve duplicate message filtering capabilities.</summary>
        internal ulong DuplicateFilterBitfield { get; set; }
    }

    /// <summary>Contains values that are accessed by multiple threads and are used to determine which messages the other end has received.</summary>
    internal class ReceiveLockables
    {
        /// <summary>The sequence ID of the latest message that we've received an ack for.</summary>
        internal ushort LastAckedSeqId { get; set; }
        /// <summary>Messages that we sent which have been acknoweledged.</summary>
        internal ushort AckedMessagesBitfield { get; set; }
    }
}
