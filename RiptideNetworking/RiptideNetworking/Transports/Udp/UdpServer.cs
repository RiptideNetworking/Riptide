// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Udp
{
    public class UdpServer : UdpPeer, IServer
    {
        public event EventHandler<ConnectingEventArgs> Connecting;
        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        public ushort Port { get; private set; }

        private Dictionary<IPEndPoint, Connection> connections;

        public void Start(ushort port)
        {
            Port = port;
            connections = new Dictionary<IPEndPoint, Connection>();

            OpenSocket(port);
        }

        public void Accept(Connection connection)
        {
            if (connection is UdpConnection udpConnection)
            {
                connections[udpConnection.RemoteEndPoint] = udpConnection;
                OnConnected(connection);
            }
        }

        public void Reject(Connection connection) { }

        public void Close(Connection connection)
        {
            if (connection is UdpConnection udpConnection)
                connections.Remove(udpConnection.RemoteEndPoint);
        }

        public void Shutdown()
        {
            CloseSocket();
            connections.Clear();
        }

        protected void OnConnecting(UdpConnection connection)
        {
            Connecting?.Invoke(this, new ConnectingEventArgs(connection));
        }

        protected void OnConnected(Connection connection)
        {
            Connected?.Invoke(this, new ConnectedEventArgs(connection));
        }

        protected override void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint)
        {
            if ((HeaderType)dataBuffer[0] == HeaderType.connect)
                OnConnecting(new UdpConnection(fromEndPoint, this)); // TODO: consider pooling UdpConnection instances to mitigate the consequences of someone spamming fake connection attempts?
            else if (connections.TryGetValue(fromEndPoint, out Connection connection))
                DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, connection));
        }

        // For when the transport detects/needs to initiate a disconnect - currently not needed
        //protected void OnDisconnected(UdpConnection connection)
        //{
        //    Disconnected?.Invoke(this, new DisconnectedEventArgs(connection, DisconnectReason.disconnected));
        //}
    }
}
