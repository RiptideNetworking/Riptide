// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Net;
using System.Net.Sockets;

namespace Riptide.Transports.Udp
{
    /// <summary>Provides base send &#38; receive functionality for <see cref="UdpServer"/> and <see cref="UdpClient"/>.</summary>
    public abstract class UdpPeer
    {
        /// <inheritdoc cref="IPeer.Disconnected"/>
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>The default size used for the socket's send and receive buffers.</summary>
        protected const int DefaultSocketBufferSize = 1024 * 1024; // 1MB
        /// <summary>The minimum size that may be used for the socket's send and receive buffers.</summary>
        private const int MinSocketBufferSize = 256 * 1024; // 256KB
        /// <summary>How long to wait for a packet, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds
        /// <summary>The size to use for the socket's send and receive buffers.</summary>
        private readonly int socketBufferSize;
        /// <summary>The array that incoming data is received into.</summary>
        private readonly byte[] receivedData;
        /// <summary>The socket to use for sending and receiving.</summary>
        private Socket socket;
        /// <summary>Whether or not the transport is running.</summary>
        private bool isRunning = false;
        /// <summary>A reusable endpoint.</summary>
        private EndPoint remoteEndPoint;

        /// <summary>Initializes the transport.</summary>
        /// <param name="socketBufferSize">How big the socket's send and receive buffers should be.</param>
        protected UdpPeer(int socketBufferSize = DefaultSocketBufferSize)
        {
            if (socketBufferSize < MinSocketBufferSize)
                throw new ArgumentOutOfRangeException(nameof(socketBufferSize), $"The minimum socket buffer size is {MinSocketBufferSize}!");

            this.socketBufferSize = socketBufferSize;
            receivedData = new byte[Message.MaxSize + sizeof(ushort)];
        }

        /// <inheritdoc cref="IPeer.Tick"/>
        public void Tick()
        {
            Receive();
        }

        /// <summary>Opens the socket and starts the transport.</summary>
        /// <param name="port">The port to bind the socket to.</param>
        protected void OpenSocket(ushort port = 0)
        {
            if (isRunning)
                CloseSocket();

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp)
            {
                SendBufferSize = socketBufferSize,
                ReceiveBufferSize = socketBufferSize,
            };
            socket.Bind(localEndPoint);

            remoteEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            isRunning = true;
        }

        /// <summary>Closes the socket and stops the transport.</summary>
        protected void CloseSocket()
        {
            if (!isRunning)
                return;

            isRunning = false;
            socket.Close();
        }

        /// <summary>Polls the socket and checks if any data was received.</summary>
        private void Receive()
        {
            if (!isRunning)
                return;

            bool tryReceiveMore = true;
            while (tryReceiveMore)
            {
                int byteCount = 0;
                try
                {
                    if (socket.Available > 0 && socket.Poll(ReceivePollingTime, SelectMode.SelectRead))
                        byteCount = socket.ReceiveFrom(receivedData, SocketFlags.None, ref remoteEndPoint);
                    else
                        tryReceiveMore = false;
                }
                catch (SocketException ex)
                {
                    tryReceiveMore = false;
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                        case SocketError.NotSocket:
                            isRunning = false;
                            break;
                        case SocketError.ConnectionReset:
                        case SocketError.MessageSize:
                        case SocketError.TimedOut:
                            break;
                        default:
                            break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    tryReceiveMore = false;
                    isRunning = false;
                }
                catch (NullReferenceException)
                {
                    tryReceiveMore = false;
                    isRunning = false;
                }

                if (byteCount > 0)
                    OnDataReceived(receivedData, byteCount, (IPEndPoint)remoteEndPoint);
            }
        }

        /// <summary>Sends data to a given endpoint.</summary>
        /// <param name="dataBuffer">The array containing the data.</param>
        /// <param name="numBytes">The number of bytes in the array which should be sent.</param>
        /// <param name="toEndPoint">The endpoint to send the data to.</param>
        internal void Send(byte[] dataBuffer, int numBytes, IPEndPoint toEndPoint)
        {
            if (isRunning)
                socket.SendTo(dataBuffer, numBytes, SocketFlags.None, toEndPoint);
        }

        /// <summary>Handles received data.</summary>
        /// <param name="dataBuffer">A byte array containing the received data.</param>
        /// <param name="amount">The number of bytes in <paramref name="dataBuffer"/> used by the received data.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        protected abstract void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint);

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        /// <param name="connection">The closed connection.</param>
        /// <param name="reason">The reason for the disconnection.</param>
        protected virtual void OnDisconnected(Connection connection, DisconnectReason reason)
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, reason));
        }
    }
}
