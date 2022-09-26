// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Udp
{
    /// <summary>A server which can accept connections from <see cref="UdpClient"/>s.</summary>
    public class UdpServer : UdpPeer, IServer
    {
        /// <inheritdoc/>
        public event EventHandler<ConnectedEventArgs> Connected;
        /// <inheritdoc/>
        public event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <inheritdoc/>
        public ushort Port { get; private set; }

        /// <summary>The currently open connections, accessible by their endpoints.</summary>
        private Dictionary<IPEndPoint, Connection> connections;

        /// <inheritdoc/>
        public UdpServer(SocketMode mode = SocketMode.Both, int socketBufferSize = DefaultSocketBufferSize) : base(mode, socketBufferSize) { }

        /// <inheritdoc/>
        public void Start(ushort port)
        {
            Port = port;
            connections = new Dictionary<IPEndPoint, Connection>();

            OpenSocket(port);
        }

        /// <summary>Decides what to do with a connection attempt.</summary>
        /// <param name="connection">The connection to accept or reject.</param>
        /// <returns>Whether or not the connection attempt was a new connection.</returns>
        private bool HandleConnectionAttempt(UdpConnection connection)
        {
            if (connections.ContainsKey(connection.RemoteEndPoint))
                return false;

            connections.Add(connection.RemoteEndPoint, connection);
            OnConnected(connection);
            return true;
        }

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

        /// <summary>Invokes the <see cref="Connected"/> event.</summary>
        /// <param name="connection">The successfully established connection.</param>
        protected virtual void OnConnected(Connection connection)
        {
            Connected?.Invoke(this, new ConnectedEventArgs(connection));
        }

        /// <inheritdoc/>
        protected override void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint)
        {
            if ((MessageHeader)dataBuffer[0] == MessageHeader.Connect && !HandleConnectionAttempt(new UdpConnection(fromEndPoint, this)))
                return;

            if (connections.TryGetValue(fromEndPoint, out Connection connection))
                DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, connection));
        }
    }
}
