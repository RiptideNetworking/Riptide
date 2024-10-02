using Riptide.Transports.Udp;
using System;
using System.Net;
using System.Net.Sockets;
using static Riptide.Transports.NanoSockets.NanoSockets;

namespace Riptide.Transports.NanoSockets
{
    /// <summary>Provides base send &#38; receive functionality for <see cref="Udp.UdpServer"/> and <see cref="System.Net.Sockets.UdpClient"/>.</summary>
    public abstract class NanoSocketsPeer
    {
        /// <inheritdoc cref="IPeer.Disconnected"/>
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>The default size used for the socket's send and receive buffers.</summary>
        protected const int DefaultSocketBufferSize = 1024 * 1024; // 1MB
        /// <summary>The minimum size that may be used for the socket's send and receive buffers.</summary>
        private const int MinSocketBufferSize = 256 * 1024; // 256KB
        /// <summary>How long to wait for a packet, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds

        /// <summary>Whether to create an IPv4 only, IPv6 only, or dual-mode socket.</summary>
        protected readonly SocketMode mode;
        /// <summary>The size to use for the socket's send and receive buffers.</summary>
        private readonly int socketBufferSize;
        /// <summary>The array that incoming data is received into.</summary>
        private readonly byte[] receivedData;
        /// <summary>The socket to use for sending and receiving.</summary>
        private long socket;
        /// <summary>Whether or not the transport is running.</summary>
        private bool isRunning;

        /// <summary>Initializes the transport.</summary>
        /// <param name="mode">Whether to create an IPv4 only, IPv6 only, or dual-mode socket.</param>
        /// <param name="socketBufferSize">How big the socket's send and receive buffers should be.</param>
        protected NanoSocketsPeer(SocketMode mode, int socketBufferSize)
        {
            if (socketBufferSize < MinSocketBufferSize)
                throw new ArgumentOutOfRangeException(nameof(socketBufferSize), $"The minimum socket buffer size is {MinSocketBufferSize}!");

            this.mode = mode;
            this.socketBufferSize = socketBufferSize;
            receivedData = new byte[Message.MaxSize];
        }

        /// <inheritdoc cref="IPeer.Poll"/>
        public void Poll()
        {
            Receive();
        }

        /// <summary>Opens the socket and starts the transport.</summary>
        /// <param name="listenAddress">The IP address to bind the socket to, if any.</param>
        /// <param name="port">The port to bind the socket to.</param>
        protected unsafe void OpenSocket(IPAddress listenAddress = null, ushort port = 0)
        {
            if (isRunning)
                CloseSocket();

            string localAddress;
            if (listenAddress != null)
                localAddress = listenAddress.ToString();
            else if (mode == SocketMode.IPv4Only || ! Socket.OSSupportsIPv6)
                localAddress = "0.0.0.0";
            else
                localAddress = "::0";

            nanosockets_initialize();
            socket = nanosockets_socket_create(socketBufferSize, socketBufferSize);
            NanoSocketsIPEndPoint localEndPoint;
            nanosockets_set_ip(&localEndPoint, localAddress);
            localEndPoint.port = port;
            nanosockets_socket_bind(socket, &localEndPoint);

            isRunning = true;
        }

        /// <summary>Closes the socket and stops the transport.</summary>
        protected void CloseSocket()
        {
            if (!isRunning)
                return;

            isRunning = false;
            nanosockets_socket_destroy(ref socket);
            nanosockets_deinitialize();
        }

        /// <summary>Polls the socket and checks if any data was received.</summary>
        private unsafe void Receive()
        {
            if (!isRunning)
                return;

            NanoSocketsIPEndPoint remoteEndPoint;
            while (nanosockets_socket_poll(socket, ReceivePollingTime / 10000) > 0)
            {
                int byteCount;
                fixed (byte* ptr = receivedData)
                {
                    byteCount = nanosockets_socket_receive(socket, &remoteEndPoint, ptr, receivedData.Length);
                }

                if (byteCount > 0)
                    OnDataReceived(receivedData, byteCount, remoteEndPoint);
            }
        }

        /// <summary>Sends data to a given endpoint.</summary>
        /// <param name="dataBuffer">The array containing the data.</param>
        /// <param name="numBytes">The number of bytes in the array which should be sent.</param>
        /// <param name="toEndPoint">The endpoint to send the data to.</param>
        internal unsafe void Send(byte[] dataBuffer, int numBytes, ref NanoSocketsIPEndPoint toEndPoint)
        {
            if (isRunning)
            {
                fixed (byte* ptr = dataBuffer)
                {
                    nanosockets_socket_send(socket, ref toEndPoint, ptr, numBytes);
                }
            }
        }

        /// <summary>Handles received data.</summary>
        /// <param name="dataBuffer">A byte array containing the received data.</param>
        /// <param name="amount">The number of bytes in <paramref name="dataBuffer"/> used by the received data.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        protected abstract void OnDataReceived(byte[] dataBuffer, int amount, NanoSocketsIPEndPoint fromEndPoint);

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        /// <param name="connection">The closed connection.</param>
        /// <param name="reason">The reason for the disconnection.</param>
        protected virtual void OnDisconnected(Connection connection, DisconnectReason reason)
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, reason));
        }
    }
}
