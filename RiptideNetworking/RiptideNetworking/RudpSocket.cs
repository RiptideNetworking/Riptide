using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RiptideNetworking
{
    /// <summary>The state of a connection.</summary>
    enum ConnectionState : byte
    {
        /// <summary>Not connected. No connection has been established or the connection has been disconnected again.</summary>
        notConnected,
        /// <summary>Connecting. Still trying to establish a connection.</summary>
        connecting,
        /// <summary>Connected. A connection was successfully established.</summary>
        connected,
    }

    /// <summary>Base class for all RUDP connections.</summary>
    public abstract class RudpSocket
    {
        /// <summary>Whether or not to output informational log messages. Error-related log messages ignore this setting.</summary>
        public bool ShouldOutputInfoLogs { get; set; } = true;
        /// <summary>The name to use when logging messages via <see cref="RiptideLogger"/></summary>
        public readonly string LogName;

        /// <summary>How long to wait for a response, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds
        /// <summary>The socket to use for sending and receiving.</summary>
        private Socket socket;
        /// <summary>Whether or not we are listening for incoming data.</summary>
        private bool isListening = false;
        /// <summary>The maximum amount of data that can be received at once.</summary>
        private readonly ushort maxPacketSize = 4096; // TODO: make smaller? MTU is 1280

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        protected RudpSocket(string logName)
        {
            LogName = logName;
        }

        /// <summary>Starts listening for incoming packets.</summary>
        /// <param name="port">The local port to listen on.</param>
        protected void StartListening(ushort port = 0)
        {
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndPoint);

            new Thread(new ThreadStart(Receive)).Start();
        }

        /// <summary>Stops listening for incoming packets.</summary>
        protected void StopListening()
        {
            isListening = false;

            if (socket == null)
                return;

            socket.Close();
            socket = null;
        }

        /// <summary>Listens for and receives incoming packets.</summary>
        private void Receive()
        {
            EndPoint bufferEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            byte[] receiveBuffer = new byte[maxPacketSize];
            isListening = true;

            while (isListening)
            {
                int byteCount;

                try
                {
                    if (socket.Available == 0 && !socket.Poll(ReceivePollingTime, SelectMode.SelectRead))
                        continue;
                    byteCount = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref bufferEndPoint);
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                        case SocketError.NotSocket:
                            return;
                        case SocketError.ConnectionReset:
                        case SocketError.MessageSize:
                        case SocketError.TimedOut:
                            break;
                        default:
                            PrepareToHandle(null, 0, (IPEndPoint)bufferEndPoint);
                            break;
                    }
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (NullReferenceException)
                {
                    return;
                }

                PrepareToHandle(receiveBuffer, byteCount, (IPEndPoint)bufferEndPoint);
            }
        }

        /// <summary>Takes received data and prepares it to be handled.</summary>
        /// <param name="data">The contents of the packet.</param>
        /// <param name="length">The length of the contents of the packet.</param>
        /// <param name="remoteEndPoint">The endpoint from which the packet was received.</param>
        private void PrepareToHandle(byte[] data, int length, IPEndPoint remoteEndPoint)
        {
            if (data == null || length < 1 || !ShouldHandleMessageFrom(remoteEndPoint, data[0]))
                return;

            byte[] messageData = new byte[length];
            Array.Copy(data, messageData, length);

            HeaderType headerType = (HeaderType)messageData[0];
            if (headerType >= HeaderType.reliable)
            {
                if (messageData.Length >= 3) // Reliable messages have a 3 byte header, so don't handle anything with less than that
                    ReliableHandle(messageData, remoteEndPoint, headerType);
            }
            else
                Handle(messageData, remoteEndPoint, headerType);
        }

        /// <summary>Determines whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="firstByte">The first byte of the message.</param>
        /// <returns><see langword="true"/> if the message should be handled.</returns>
        protected abstract bool ShouldHandleMessageFrom(IPEndPoint endPoint, byte firstByte);

        /// <summary>Handles the given reliably sent data.</summary>
        /// <param name="data">The reliably sent data.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        /// <param name="headerType">The header type of the data.</param>
        internal abstract void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType);

        /// <summary>Handles the given reliably sent data.</summary>
        /// <param name="data">The reliably sent data.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        /// <param name="headerType">The header type of the data.</param>
        /// <param name="lockables">The lockable values which are used to inform the other end of which messages we've received.</param>
        internal void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType, SendLockables lockables)
        {
#if BIG_ENDIAN
            ushort sequenceId = (ushort)(data[2] | (data[1] << 8));
#else
            ushort sequenceId = (ushort)(data[1] | (data[2] << 8));
#endif

            lock (lockables)
            {
                // Update acks
                int sequenceGap = Rudp.GetSequenceGap(sequenceId, lockables.LastReceivedSeqId);
                if (sequenceGap > 0)
                {
                    // The received sequence ID is newer than the previous one
                    lockables.AcksBitfield <<= sequenceGap; // Shift the bits left to make room for the latest remote sequence ID
                    ushort seqIdBit = (ushort)(1 << sequenceGap - 1); // Calculate which bit corresponds to the sequence ID and set it to 1
                    if ((lockables.AcksBitfield & seqIdBit) == 0)
                    {
                        // If we haven't received this packet before
                        lockables.AcksBitfield |= seqIdBit; // Set the bit corresponding to the sequence ID to 1 because we received that ID
                        lockables.LastReceivedSeqId = sequenceId;
                        SendAck(sequenceId, fromEndPoint);
                    }
                    else
                    {
                        SendAck(sequenceId, fromEndPoint);
                        return; // Message was a duplicate, don't handle it
                    }
                }
                else if (sequenceGap < 0)
                {
                    // The received sequence ID is older than the previous one (out of order message)
                    sequenceGap = -sequenceGap; // Make sequenceGap positive
                    if (sequenceGap > 16) // If it's an old packet and its sequence ID doesn't fall within the bitfield's value range anymore
                        SendAck(sequenceId, fromEndPoint); // TODO: store a larger bitfield locally to do a better job of filtering out old duplicates
                    else
                    {
                        ushort seqIdBit = (ushort)(1 << sequenceGap - 1); // Calculate which bit corresponds to the sequence ID and set it to 1
                        if ((lockables.AcksBitfield & seqIdBit) == 0) // If we haven't received this packet before
                        {
                            lockables.AcksBitfield |= seqIdBit; // Set the bit corresponding to the sequence ID to 1 because we received that ID
                            SendAck(sequenceId, fromEndPoint);
                        }
                        else
                        {
                            SendAck(sequenceId, fromEndPoint);
                            return; // Message was a duplicate, don't handle it
                        }
                    }
                }
                else // The received sequence ID is the same as the previous one (duplicate message)
                {
                    SendAck(sequenceId, fromEndPoint);
                    return; // Message was a duplicate, don't handle it
                }
            }

            Handle(data, fromEndPoint, headerType);
        }

        /// <summary>Handles the given data.</summary>
        /// <param name="data">The data to handle.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        /// <param name="headerType">The header type of the data.</param>
        internal abstract void Handle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType);

        /// <summary>Sends data.</summary>
        /// <param name="data">The data to send.</param>
        /// <param name="toEndPoint">The endpoint to send the data to.</param>
        internal void Send(byte[] data, IPEndPoint toEndPoint)
        {
            if (socket != null)
            {
#if DETAILED_LOGGING
                if ((HeaderType)data[0] == HeaderType.reliable)
                    RiptideLogger.Log(LogName, $"Sending reliable message (ID: {BitConverter.ToUInt16(data, 3)}) to {toEndPoint}.");
                else if ((HeaderType)data[0] == HeaderType.unreliable)
                    RiptideLogger.Log(LogName, $"Sending message (ID: {BitConverter.ToUInt16(data, 1)}) to {toEndPoint}.");
                else
                    RiptideLogger.Log(LogName, $"Sending {(HeaderType)data[0]} message to {toEndPoint}.");
#endif
                socket.SendTo(data, toEndPoint);
            }
        }
        /// <summary>Sends data.</summary>
        /// <param name="data">The data to send.</param>
        /// <param name="numBytes">The number of bytes to send from the given data.</param>
        /// <param name="toEndPoint">The endpoint to send the data to.</param>
        internal void Send(byte[] data, int numBytes, IPEndPoint toEndPoint)
        {
            if (socket != null)
            {
#if DETAILED_LOGGING
                if ((HeaderType)data[0] == HeaderType.reliable)
                    RiptideLogger.Log(LogName, $"Sending reliable message (ID: {BitConverter.ToUInt16(data, 3)}) to {toEndPoint}.");
                else if ((HeaderType)data[0] == HeaderType.unreliable)
                    RiptideLogger.Log(LogName, $"Sending message (ID: {BitConverter.ToUInt16(data, 1)}) to {toEndPoint}.");
                else
                    RiptideLogger.Log(LogName, $"Sending {(HeaderType)data[0]} message to {toEndPoint}.");
#endif
                socket.SendTo(data, numBytes, SocketFlags.None, toEndPoint);
            }
        }

        /// <summary>Reliably sends the given message.</summary>
        /// <param name="message">The message to send reliably.</param>
        /// <param name="toEndPoint">The endpoint to send the message to.</param>
        /// <param name="rudp">The Rudp instance to use to send (and resend) the pending message.</param>
        /// <param name="maxSendAttempts">How often to try sending the message before giving up.</param>
        internal void SendReliable(Message message, IPEndPoint toEndPoint, Rudp rudp, byte maxSendAttempts)
        {
            if (socket == null)
                return;

            ushort sequenceId = rudp.NextSequenceId; // Get the next sequence ID
            message.SetSequenceIdBytes(sequenceId); // Set the message's sequence ID bytes

            Rudp.PendingMessage pendingMessage = new Rudp.PendingMessage(rudp, sequenceId, message, toEndPoint, maxSendAttempts);
            lock (rudp.PendingMessages)
            {
                rudp.PendingMessages.Add(sequenceId, pendingMessage);
                pendingMessage.TrySend();
            }
        }

        /// <summary>Sends an acknowledgement for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        /// <param name="toEndPoint">The endpoint to send the acknowledgement to.</param>
        protected abstract void SendAck(ushort forSeqId, IPEndPoint toEndPoint);
    }
}
