namespace RiptideNetworking
{
    internal class SendLockables
    {
        /// <summary>The sequence ID of the latest message that we want to acknowledge.</summary>
        internal ushort LastReceivedSeqId;
        /// <summary>Messages that we have received and want to acknowledge.</summary>
        internal ushort AcksBitfield;
    }

    internal class ReceiveLockables
    {
        /// <summary>The sequence ID of the latest message that we've received an ack for.</summary>
        internal ushort LastAckedSeqId;
        /// <summary>Messages that we sent which have been acknoweledged.</summary>
        internal ushort AckedMessagesBitfield;
    }
}
