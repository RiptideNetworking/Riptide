using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace RiptideNetworking
{
    enum ConnectionState : byte
    {
        notConnected,
        connecting,
        connected,
    }

    /// <summary>Base class for all RUDP connections.</summary>
    public abstract class RudpSocket
    {
        /// <summary>The name of this server/client instance. Used when logging messages.</summary>
        public readonly string LogName;

        private const int ReceivePollingTime = 500000; // 0.5 seconds

        private Socket socket;
        private bool isListening = false;
        private ushort maxPacketSize = 4096;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name of this server/client instance. Used when logging messages.</param>
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
                            Receive(null, 0, (IPEndPoint)bufferEndPoint);
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

                Receive(receiveBuffer, byteCount, (IPEndPoint)bufferEndPoint);
            }
        }

        private void Receive(byte[] data, int length, IPEndPoint remoteEndPoint)
        {
            if (data == null || data.Length < 1 || !ShouldHandleMessageFrom(remoteEndPoint, data[0]))
                return;

            byte[] messageData = new byte[length];
            Array.Copy(data, messageData, length);

            HeaderType headerType = (HeaderType)messageData[0];
            if (headerType >= HeaderType.reliable)
                ReliableHandle(messageData, remoteEndPoint, headerType);
            else
                Handle(messageData, remoteEndPoint, headerType);
        }

        /// <summary>Whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="firstByte">The first byte of the message.</param>
        protected abstract bool ShouldHandleMessageFrom(IPEndPoint endPoint, byte firstByte);

        internal abstract void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType);
        
        internal void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType, SendLockables lockables)
        {
            byte[] idBytes = new byte[Message.shortLength];
            Array.Copy(data, 1, idBytes, 0, Message.shortLength);
            ushort sequenceId = BitConverter.ToUInt16(Message.StandardizeEndianness(idBytes), 0);

            lock (lockables)
            {
                // Update acks
                int sequenceGap = Rudp.GetSequenceGap(sequenceId, lockables.LastReceivedSeqId);
                if (sequenceGap > 0)
                {
                    lockables.AcksBitfield <<= sequenceGap; // Shift the bits left to make room for the latest remote sequence ID
                    ushort seqIdBit = (ushort)(1 << sequenceGap - 1);
                    if ((lockables.AcksBitfield & seqIdBit) == 0)
                    {
                        // If we haven't received this packet before
                        lockables.AcksBitfield |= (ushort)(1 << sequenceGap - 1); // Set the bit corresponding to the sequence ID to 1 because we received that ID
                        lockables.LastReceivedSeqId = sequenceId;
                    }
                    else
                        return; // Packet was a duplicate
                }
                else if (sequenceGap < 0)
                {
                    sequenceGap = -sequenceGap; // Make sequenceGap positive
                    ushort seqIdBit = (ushort)(1 << sequenceGap - 1);
                    if ((lockables.AcksBitfield & seqIdBit) == 0) // If we haven't received this packet before
                        lockables.AcksBitfield |= seqIdBit; // Set the bit corresponding to the sequence ID to 1 because we received that ID
                    else
                        return; // Packet was a duplicate
                }
                else
                    return; // Packet was a duplicate

                SendAck(sequenceId, fromEndPoint);
            }

            Handle(data, fromEndPoint, headerType);
        }

        internal abstract void Handle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType);

        internal void Send(byte[] data, IPEndPoint toEndPoint)
        {
            if (socket != null)
            {
#if DETAILED_LOGGING
                if ((HeaderType)data[0] == HeaderType.reliable)
                    RiptideLogger.Log(logName, $"Sending reliable message (ID: {BitConverter.ToInt16(data, 3)}) to {toEndPoint}.");
                else if ((HeaderType)data[0] == HeaderType.unreliable)
                    RiptideLogger.Log(logName, $"Sending message (ID: {BitConverter.ToInt16(data, 1)}) to {toEndPoint}.");
                else
                    RiptideLogger.Log(logName, $"Sending {(HeaderType)data[0]} message to {toEndPoint}.");
#endif
                socket.SendTo(data, toEndPoint);
            }
        }

        internal void SendReliable(Message message, IPEndPoint toEndPoint, Rudp rudp, byte maxSendAttempts)
        {
            if (socket == null)
                return;

            ushort sequenceId = rudp.NextSequenceId;
            message.SetSequenceIdBytes(sequenceId);

            Rudp.PendingMessage pendingMessage = new Rudp.PendingMessage(rudp, sequenceId, message.Bytes, toEndPoint, maxSendAttempts);
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
