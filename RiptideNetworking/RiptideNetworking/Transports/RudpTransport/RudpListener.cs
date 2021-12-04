
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RiptideNetworking.Transports.RudpTransport
{
    /// <summary>Provides base sending &#38; receiving functionality for <see cref="RudpServer"/> and <see cref="RudpClient"/>.</summary>
    public abstract class RudpListener
    {
        /// <summary>The name to use when logging messages via <see cref="RiptideLogger"/>.</summary>
        public readonly string LogName;

        /// <summary>The <see cref="ActionQueue"/> to use when invoking events. <see langword="null"/> if events should be invoked immediately.</summary>
        protected ActionQueue receiveActionQueue;

        /// <summary>How long to wait for a response, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds
        /// <summary>The socket to use for sending and receiving.</summary>
        private Socket socket;
        /// <summary>Whether or not we are currently listening for incoming data.</summary>
        private bool isListening = false;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        protected RudpListener(string logName)
        {
            LogName = logName;
            receiveActionQueue = new ActionQueue();
        }

        /// <inheritdoc cref="ICommon.Tick"/>
        public void Tick()
        {
            receiveActionQueue.ExecuteAll();
        }

        /// <summary>Starts listening for incoming packets.</summary>
        /// <param name="port">The local port to listen on.</param>
        protected void StartListening(ushort port = 0)
        {
            Message.IncreasePoolCount();

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
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

            Message.DecreasePoolCount();
        }

        /// <summary>Listens for and receives incoming packets.</summary>
        private void Receive()
        {
            EndPoint bufferEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            isListening = true;

            while (isListening)
            {
                int byteCount;
                Message message = null;

                try
                {
                    if (socket.Available == 0 && !socket.Poll(ReceivePollingTime, SelectMode.SelectRead))
                        continue;

                    message = Message.Create();
                    byteCount = socket.ReceiveFrom(message.Bytes, 0, message.Bytes.Length, SocketFlags.None, ref bufferEndPoint);

                    if (byteCount < 1)
                    {
                        message.Release();
                        continue;
                    }
                }
                catch (SocketException ex)
                {
                    if (message != null)
                        message.Release();

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
                            break;
                    }
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    if (message != null)
                        message.Release();

                    return;
                }
                catch (NullReferenceException)
                {
                    if (message != null)
                        message.Release();

                    return;
                }

                PrepareToHandle(message, byteCount, (IPEndPoint)bufferEndPoint);
            }
        }

        /// <summary>Takes a received message and prepares it to be handled.</summary>
        /// <param name="message">The message that was received.</param>
        /// <param name="length">The length of the contents of message.</param>
        /// <param name="remoteEndPoint">The endpoint from which the message was received.</param>
        private void PrepareToHandle(Message message, int length, IPEndPoint remoteEndPoint)
        {
            HeaderType messageHeader = message.PrepareForUse((ushort)length);
            if (!ShouldHandleMessageFrom(remoteEndPoint, messageHeader))
                return;

            if (message.SendMode == MessageSendMode.reliable)
            {
                if (message.UnreadLength >= 2) // Reliable messages have a 3 byte header (one of which we've already read out) so don't handle anything with less than that
                    ReliableHandle(message, remoteEndPoint, messageHeader);
            }
            else
                Handle(message, remoteEndPoint, messageHeader);
        }

        /// <summary>Determines whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="messageHeader">The header of the message.</param>
        /// <returns><see langword="true"/> if the message should be handled.</returns>
        protected abstract bool ShouldHandleMessageFrom(IPEndPoint endPoint, HeaderType messageHeader);

        /// <summary>Handles the given reliably sent message.</summary>
        /// <param name="message">The message that was received.</param>
        /// <param name="fromEndPoint">The endpoint from which the message was received.</param>
        /// <param name="messageHeader">The header of the message.</param>
        protected abstract void ReliableHandle(Message message, IPEndPoint fromEndPoint, HeaderType messageHeader);

        /// <summary>Handles the given reliably sent message.</summary>
        /// <param name="message">The message that was received.</param>
        /// <param name="fromEndPoint">The endpoint from which the message was received.</param>
        /// <param name="messageHeader">The header of the message.</param>
        /// <param name="lockables">The lockable values which are used to inform the other end of which messages we've received.</param>
        internal void ReliableHandle(Message message, IPEndPoint fromEndPoint, HeaderType messageHeader, SendLockables lockables)
        {
            ushort sequenceId = message.GetUShort();

            lock (lockables)
            {
                // Update acks
                int sequenceGap = RudpPeer.GetSequenceGap(sequenceId, lockables.LastReceivedSeqId);
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

            Handle(message, fromEndPoint, messageHeader);
        }

        /// <summary>Handles the given message.</summary>
        /// <param name="message">The message that was received.</param>
        /// <param name="fromEndPoint">The endpoint from which the message was received.</param>
        /// <param name="messageHeader">The header of the message.</param>
        protected abstract void Handle(Message message, IPEndPoint fromEndPoint, HeaderType messageHeader);

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
        /// <param name="peer">The <see cref="RudpPeer"/> to use to send (and resend) the pending message.</param>
        internal void SendReliable(Message message, IPEndPoint toEndPoint, RudpPeer peer)
        {
            if (socket == null)
                return;

            ushort sequenceId = peer.NextSequenceId; // Get the next sequence ID
            PendingMessage.CreateAndSend(peer, sequenceId, message, toEndPoint);
        }

        /// <summary>Sends an acknowledgement for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        /// <param name="toEndPoint">The endpoint to send the acknowledgement to.</param>
        protected abstract void SendAck(ushort forSeqId, IPEndPoint toEndPoint);
    }
}