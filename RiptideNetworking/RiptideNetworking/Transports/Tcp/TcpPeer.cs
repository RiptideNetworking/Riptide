// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Net.Sockets;

namespace Riptide.Transports.Tcp
{
    /// <summary>Provides base send &#38; receive functionality for <see cref="TcpServer"/> and <see cref="TcpClient"/>.</summary>
    public abstract class TcpPeer
    {
        /// <inheritdoc cref="IPeer.Disconnected"/>
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>The array that incoming data is received into.</summary>
        internal readonly byte[] ReceivedData;

        /// <summary>The default size used for the socket's send and receive buffers.</summary>
        protected const int DefaultSocketBufferSize = 1024 * 1024; // 1MB
        /// <summary>The size to use for the socket's send and receive buffers.</summary>
        protected readonly int socketBufferSize;
        /// <summary>The main socket, either used for listening for connections or for sending and receiving data.</summary>
        protected Socket socket;
        /// <summary>The minimum size that may be used for the socket's send and receive buffers.</summary>
        private const int MinSocketBufferSize = 256 * 1024; // 256KB

        /// <summary>Initializes the transport.</summary>
        /// <param name="socketBufferSize">How big the socket's send and receive buffers should be.</param>
        protected TcpPeer(int socketBufferSize = DefaultSocketBufferSize)
        {
            if (socketBufferSize < MinSocketBufferSize)
                throw new ArgumentOutOfRangeException(nameof(socketBufferSize), $"The minimum socket buffer size is {MinSocketBufferSize}!");

            this.socketBufferSize = socketBufferSize;
            ReceivedData = new byte[Message.MaxSize + sizeof(ushort)];
        }

        /// <summary>Handles received data.</summary>
        /// <param name="amount">The number of bytes that were received.</param>
        /// <param name="fromConnection">The connection from which the data was received.</param>
        protected internal abstract void OnDataReceived(int amount, TcpConnection fromConnection);

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        /// <param name="connection">The closed connection.</param>
        /// <param name="reason">The reason for the disconnection.</param>
        protected internal virtual void OnDisconnected(Connection connection, DisconnectReason reason)
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, reason));
        }
    }
}
