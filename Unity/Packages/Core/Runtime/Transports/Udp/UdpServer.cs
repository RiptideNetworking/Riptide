// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Udp
{
    public class UdpServer : UdpPeer, IServer
    {
        public event EventHandler<ClientConnectingEventArgs> ClientConnecting;
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        public ushort Port { get; private set; }

        private Dictionary<IPEndPoint, Connection> connections;

        public void Start(ushort port)
        {
            Port = port;
            connections = new Dictionary<IPEndPoint, Connection>();

            StartSocket(port);
        }

        public void Accept(Connection connection)
        {
            if (connection is UdpConnection udpConnection)
            {
                connections[udpConnection.RemoteEndPoint] = udpConnection;
                OnClientConnected(connection);
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
            StopSocket();
            connections.Clear();
        }

        protected void OnClientConnecting(UdpConnection connection)
        {
            ClientConnecting?.Invoke(this, new ClientConnectingEventArgs(connection));
        }

        protected void OnClientConnected(Connection connection)
        {
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs(connection));
        }

        protected override void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint)
        {
            if ((HeaderType)dataBuffer[0] == HeaderType.connect)
                OnClientConnecting(new UdpConnection(fromEndPoint, this)); // TODO: consider pooling UdpConnection instances to mitigate the consequences of someone spamming fake connection attempts?
            else if (connections.TryGetValue(fromEndPoint, out Connection connection))
                DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, connection));
        }

        // For when the transport detects/needs to initiate a disconnect - currently not needed
        //protected void OnClientDisconnected(UdpConnection connection)
        //{
        //    ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(connection, DisconnectReason.disconnected));
        //}
    }
}
