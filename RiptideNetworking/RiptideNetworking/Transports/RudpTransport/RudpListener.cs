
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

        /// <summary>The <see cref="ActionQueue"/> to use when invoking events.</summary>
        protected ActionQueue receiveActionQueue;

        /// <summary>How long to wait for a response, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds
        /// <summary>The socket to use for sending and receiving.</summary>
        private Socket socket;
        /// <summary>Whether or not the socket is ready to send and receive data.</summary>
        private bool isRunning = false;
        /// <summary>The buffer that incoming data is received into.</summary>
        private byte[] receiveBuffer;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        protected RudpListener(string logName)
        {
            LogName = logName;
            receiveActionQueue = new ActionQueue();
        }

        /// <inheritdoc cref="ICommon.Tick"/>
        public void Tick() => receiveActionQueue.ExecuteAll();

        /// <summary>Starts listening for incoming packets.</summary>
        /// <param name="port">The local port to listen on.</param>
        protected void StartListening(ushort port = 0)
        {
            lock (receiveActionQueue)
            {
                if (isRunning)
                    StopListening();

                Message.IncreasePoolCount();

                IPEndPoint localEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
                socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(localEndPoint);

                isRunning = true;
            }
            
            new Thread(new ThreadStart(Receive)).Start();
        }

        /// <summary>Stops listening for incoming packets.</summary>
        protected void StopListening()
        {
            lock (receiveActionQueue)
            {
                if (!isRunning)
                    return;

                isRunning = false;
                socket.Close();

                Message.DecreasePoolCount();
            }
        }

        /// <summary>Listens for and receives incoming packets.</summary>
        private void Receive()
        {
            EndPoint bufferEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            receiveBuffer = new byte[Message.MaxMessageSize + RiptideConverter.UShortLength];

            while (isRunning)
            {
                int byteCount;
                
                try
                {
                    if (socket.Available == 0 && !socket.Poll(ReceivePollingTime, SelectMode.SelectRead))
                        continue;

                    byteCount = socket.ReceiveFrom(receiveBuffer, SocketFlags.None, ref bufferEndPoint);

                    if (byteCount < 1)
                        continue;
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

                PrepareToHandle(byteCount, (IPEndPoint)bufferEndPoint);
            }
        }

        /// <summary>Takes a received message and prepares it to be handled.</summary>
        /// <param name="length">The length of the contents of message.</param>
        /// <param name="remoteEndPoint">The endpoint from which the message was received.</param>
        private void PrepareToHandle(int length, IPEndPoint remoteEndPoint)
        {
            HeaderType messageHeader = (HeaderType)receiveBuffer[0];
            if (!ShouldHandleMessageFrom(remoteEndPoint, messageHeader))
                return;

            Message message = Message.CreateRaw();
            message.PrepareForUse(messageHeader, (ushort)(messageHeader >= HeaderType.reliable ? length - 2 : length)); // Subtract 2 for reliable messages because length will include the 2 bytes used by the sequence ID that don't actually get copied to the message's byte array

            if (message.SendMode == MessageSendMode.reliable)
            {
                if (length > 3) // Only bother with the array copy if there are more than 3 bytes in the packet (3 or less means no payload for a reliably sent packet)
                    Array.Copy(receiveBuffer, 3, message.Bytes, 1, length - 3);
                else if (length < 3) // Reliable messages have a 3 byte header, if there aren't that many bytes in the packet don't handle it
                    return;
                
                ReliableHandle(messageHeader, RiptideConverter.ToUShort(receiveBuffer, 1), message, remoteEndPoint);
            }
            else
            {
                if (length > 1) // Only bother with the array copy if there is more than 1 byte in the packet (1 or less means no payload for a reliably sent packet)
                    Array.Copy(receiveBuffer, 1, message.Bytes, 1, length - 1);

                Handle(message, remoteEndPoint, messageHeader);
            }
        }

        /// <summary>Determines whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="messageHeader">The header of the message.</param>
        /// <returns><see langword="true"/> if the message should be handled.</returns>
        protected abstract bool ShouldHandleMessageFrom(IPEndPoint endPoint, HeaderType messageHeader);

        /// <summary>Handles the given reliably sent message.</summary>
        /// <param name="messageHeader">The header of the message.</param>
        /// <param name="sequenceId">The sequence ID of the message.</param>
        /// <param name="message">The message that was received.</param>
        /// <param name="fromEndPoint">The endpoint from which the message was received.</param>
        protected abstract void ReliableHandle(HeaderType messageHeader, ushort sequenceId, Message message, IPEndPoint fromEndPoint);

        /// <summary>Handles the given reliably sent message.</summary>
        /// <param name="messageHeader">The header of the message.</param>
        /// <param name="sequenceId">The sequence ID of the message.</param>
        /// <param name="message">The message that was received.</param>
        /// <param name="fromEndPoint">The endpoint from which the message was received.</param>
        /// <param name="lockables">The lockable values which are used to inform the other end of which messages we've received.</param>
        internal void ReliableHandle(HeaderType messageHeader, ushort sequenceId, Message message, IPEndPoint fromEndPoint, SendLockables lockables)
        {
            bool shouldHandle = true;
            lock (lockables)
            {
                // Update acks
                int sequenceGap = RudpPeer.GetSequenceGap(sequenceId, lockables.LastReceivedSeqId);
                if (sequenceGap > 0)
                {
                    // The received sequence ID is newer than the previous one
                    if (sequenceGap > 64)
                        RiptideLogger.Log(LogType.warning, LogName, $"The gap between received sequence IDs was very large ({sequenceGap})! If the connection's packet loss, latency, or your send rate of reliable messages increases much further, sequence IDs may begin falling outside the bounds of the duplicate filter.");

                    lockables.DuplicateFilterBitfield <<= sequenceGap;
                    if (sequenceGap <= 16)
                    {
                        ulong shiftedBits = (ulong)lockables.AcksBitfield << sequenceGap;
                        lockables.AcksBitfield = (ushort)shiftedBits; // Give the acks bitfield the first 2 bytes of the shifted bits
                        lockables.DuplicateFilterBitfield |= shiftedBits >> 16; // OR the last 6 bytes worth of the shifted bits into the duplicate filter bitfield

                        shouldHandle = UpdateAcksBitfield(sequenceGap, lockables);
                        lockables.LastReceivedSeqId = sequenceId;
                    }
                    else if (sequenceGap <= 80)
                    {
                        ulong shiftedBits = (ulong)lockables.AcksBitfield << (sequenceGap - 16);
                        lockables.AcksBitfield = 0; // Reset the acks bitfield as all its bits are being moved to the duplicate filter bitfield
                        lockables.DuplicateFilterBitfield |= shiftedBits; // OR the shifted bits into the duplicate filter bitfield

                        shouldHandle = UpdateDuplicateFilterBitfield(sequenceGap, lockables);
                    }
                }
                else if (sequenceGap < 0)
                {
                    // The received sequence ID is older than the previous one (out of order message)
                    sequenceGap = -sequenceGap; // Make sequenceGap positive
                    if (sequenceGap <= 16) // If the message's sequence ID still falls within the ack bitfield's value range
                        shouldHandle = UpdateAcksBitfield(sequenceGap, lockables);
                    else if (sequenceGap <= 80) // If it's an "old" message and its sequence ID doesn't fall within the ack bitfield's value range anymore (but it falls in the range of the duplicate filter)
                        shouldHandle = UpdateDuplicateFilterBitfield(sequenceGap, lockables);
                }
                else // The received sequence ID is the same as the previous one (duplicate message)
                    shouldHandle = false;
            }

            SendAck(sequenceId, fromEndPoint);
            if (shouldHandle)
                Handle(message, fromEndPoint, messageHeader);
        }

        /// <summary>Updates the acks bitfield and determines whether or not to handle the message.</summary>
        /// <param name="sequenceGap">The gap between the newly received sequence ID and the previously last received sequence ID.</param>
        /// <param name="lockables">The lockable values which are used to inform the other end of which messages we've received.</param>
        /// <returns>Whether or not the message should be handled, based on whether or not it's a duplicate.</returns>
        private bool UpdateAcksBitfield(int sequenceGap, SendLockables lockables)
        {
            ushort seqIdBit = (ushort)(1 << sequenceGap - 1); // Calculate which bit corresponds to the sequence ID and set it to 1
            if ((lockables.AcksBitfield & seqIdBit) == 0)
            {
                // If we haven't received this message before
                lockables.AcksBitfield |= seqIdBit; // Set the bit corresponding to the sequence ID to 1 because we received that ID
                return true; // Message was "new", handle it
            }
            else // If we have received this message before
                return false; // Message was a duplicate, don't handle it
        }

        /// <summary>Updates the duplicate filter bitfield and determines whether or not to handle the message.</summary>
        /// <param name="sequenceGap">The gap between the newly received sequence ID and the previously last received sequence ID.</param>
        /// <param name="lockables">The lockable values which are used to inform the other end of which messages we've received.</param>
        /// <returns>Whether or not the message should be handled, based on whether or not it's a duplicate.</returns>
        private bool UpdateDuplicateFilterBitfield(int sequenceGap, SendLockables lockables)
        {
            ulong seqIdBit = (ulong)1 << (sequenceGap - 1 - 16); // Calculate which bit corresponds to the sequence ID and set it to 1
            if ((lockables.DuplicateFilterBitfield & seqIdBit) == 0)
            {
                // If we haven't received this message before
                lockables.DuplicateFilterBitfield |= seqIdBit; // Set the bit corresponding to the sequence ID to 1 because we received that ID
                return true; // Message was "new", handle it
            }
            else // If we have received this message before
                return false; // Message was a duplicate, don't handle it
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
            if (isRunning)
            {
#if DETAILED_LOGGING
                if ((HeaderType)data[0] == HeaderType.reliable)
                    RiptideLogger.Log(LogName, $"Sending reliable message (ID: {BitConverter.ToUInt16(data, 3)}) to {toEndPoint}.");
                else if ((HeaderType)data[0] == HeaderType.unreliable)
                    RiptideLogger.Log(LogName, $"Sending message (ID: {BitConverter.ToUInt16(data, 1)}) to {toEndPoint}.");
                else
                    RiptideLogger.Log(LogName, $"Sending {(HeaderType)data[0]} message to {toEndPoint}.");
#endif
                try
                {
                    socket.SendTo(data, toEndPoint);
                }
                catch (ObjectDisposedException)
                {
                    // Literally just eat the exception. This exception should only be thrown if another thread triggers
                    // a disconnect inbetween the if check and the socket.SendTo call executing, so it's extremely rare.
                    // Eating the exception like this may not be ideal, but with it being as rare as it is, acquiring a
                    // lock *every* time data needs to be sent seems quite wasteful, and try catch blocks don't really
                    // slow things down when no exception is actually thrown: https://stackoverflow.com/a/64229258
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.Interrupted) // Also caused by socket being closed while sending
                        throw ex;
                }
            }
        }
        /// <summary>Sends data.</summary>
        /// <param name="data">The data to send.</param>
        /// <param name="numBytes">The number of bytes to send from the given data.</param>
        /// <param name="toEndPoint">The endpoint to send the data to.</param>
        internal void Send(byte[] data, int numBytes, IPEndPoint toEndPoint)
        {
            if (isRunning)
            {
#if DETAILED_LOGGING
                if ((HeaderType)data[0] == HeaderType.reliable)
                    RiptideLogger.Log(LogName, $"Sending reliable message (ID: {BitConverter.ToUInt16(data, 3)}) to {toEndPoint}.");
                else if ((HeaderType)data[0] == HeaderType.unreliable)
                    RiptideLogger.Log(LogName, $"Sending message (ID: {BitConverter.ToUInt16(data, 1)}) to {toEndPoint}.");
                else
                    RiptideLogger.Log(LogName, $"Sending {(HeaderType)data[0]} message to {toEndPoint}.");
#endif
                try
                {
                    socket.SendTo(data, numBytes, SocketFlags.None, toEndPoint);
                }
                catch (ObjectDisposedException)
                {
                    // Literally just eat the exception. This exception should only be thrown if another thread triggers
                    // a disconnect inbetween the if check and the socket.SendTo call executing, so it's extremely rare.
                    // Eating the exception like this may not be ideal, but with it being as rare as it is, acquiring a
                    // lock *every* time data needs to be sent seems quite wasteful, and try catch blocks don't really
                    // slow things down when no exception is actually thrown: https://stackoverflow.com/a/64229258
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.Interrupted) // Also caused by socket being closed while sending
                        throw ex;
                }
            }
        }

        /// <summary>Reliably sends the given message.</summary>
        /// <param name="message">The message to send reliably.</param>
        /// <param name="toEndPoint">The endpoint to send the message to.</param>
        /// <param name="peer">The <see cref="RudpPeer"/> to use to send (and resend) the pending message.</param>
        internal void SendReliable(Message message, IPEndPoint toEndPoint, RudpPeer peer)
        {
            if (!isRunning)
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
