// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Udp
{
    /// <summary>A server which can accept connections from <see cref="UdpClient"/>s.</summary>
    public class UdpServer : UdpPeer, IServer
    {
        /// <inheritdoc/>
        public event EventHandler<ConnectingEventArgs> Connecting;
        /// <inheritdoc/>
        public event EventHandler<ConnectedEventArgs> Connected;
        /// <inheritdoc/>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <inheritdoc/>
        public ushort Port { get; private set; }

        /// <summary>The currently open connections, accessible by their endpoints.</summary>
        private Dictionary<IPEndPoint, Connection> connections;

        /// <inheritdoc/>
        public UdpServer(int socketBufferSize = DefaultSocketBufferSize) : base(socketBufferSize) { }

        /// <inheritdoc/>
        public void Start(ushort port)
        {
            Port = port;
            connections = new Dictionary<IPEndPoint, Connection>();

            OpenSocket(port);
        }

        /// <inheritdoc/>
        public void Accept(Connection connection)
        {
            if (connection is UdpConnection udpConnection)
            {
                connections[udpConnection.RemoteEndPoint] = udpConnection;
                OnConnected(connection);
            }
        }

        /// <inheritdoc/>
        public void Reject(Connection connection) { }

        /// <inheritdoc/>
        public void Close(Connection connection)
        {
            if (connection is UdpConnection udpConnection)
                connections.Remove(udpConnection.RemoteEndPoint);
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            CloseSocket();
            connections.Clear();
        }

        /// <summary>Invokes the <see cref="Connecting"/> event.</summary>
        /// <param name="connection">The pending connection.</param>
        protected virtual void OnConnecting(UdpConnection connection)
        {
            Connecting?.Invoke(this, new ConnectingEventArgs(connection));
        }

        /// <summary>Invokes the <see cref="Connected"/> event.</summary>
        /// <param name="connection">The successfully established connection.</param>
        protected virtual void OnConnected(Connection connection)
        {
            Connected?.Invoke(this, new ConnectedEventArgs(connection));
        }

        /// <inheritdoc/>
        protected override void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint)
        {
            if (connections.TryGetValue(fromEndPoint, out Connection connection))
                DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, connection));
            else if ((HeaderType)dataBuffer[0] == HeaderType.connect)
                OnConnecting(new UdpConnection(fromEndPoint, this)); // TODO: consider pooling UdpConnection instances to mitigate the consequences of someone spamming fake connection attempts?
        }
    }
}
