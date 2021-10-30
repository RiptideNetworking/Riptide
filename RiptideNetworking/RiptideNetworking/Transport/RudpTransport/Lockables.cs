namespace RiptideNetworking.Transports.RudpTransport
{
    /// <summary>Contains values that are accessed by multiple threads and are used to inform the other end of which messages we've received.</summary>
    internal class SendLockables
    {
        /// <summary>The sequence ID of the latest message that we want to acknowledge.</summary>
        internal ushort LastReceivedSeqId;
        /// <summary>Messages that we have received and want to acknowledge.</summary>
        internal ushort AcksBitfield;
    }

    /// <summary>Contains values that are accessed by multiple threads and are used to determine which messages the other end has received.</summary>
    internal class ReceiveLockables
    {
        /// <summary>The sequence ID of the latest message that we've received an ack for.</summary>
        internal ushort LastAckedSeqId;
        /// <summary>Messages that we sent which have been acknoweledged.</summary>
        internal ushort AckedMessagesBitfield;
    }
}
