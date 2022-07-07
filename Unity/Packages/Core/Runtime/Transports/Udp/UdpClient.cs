﻿// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Riptide.Transports.Udp
{
    public class UdpClient : UdpPeer, IClient
    {
        public event EventHandler Connected;
        public event EventHandler ConnectionFailed;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<ClientDisconnectedEventArgs> Disconnected;

        private UdpConnection udpConnection;

        /// <inheritdoc/>
        /// <remarks>Expects the host address to consist of an IP and port, separated by a colon. For example: <c>127.0.0.1:7777</c>.</remarks>
        public bool Connect(string hostAddress, out Connection connection, out string connectError)
        {
            connectError = $"Invalid host address '{hostAddress}'! IP and port should be separated by a colon, for example: '127.0.0.1:7777'.";
            if (!ParseHostAddress(hostAddress, out IPAddress ip, out ushort port))
            {
                connection = null;
                return false;
            }

            StartSocket();

            connection = udpConnection = new UdpConnection(new IPEndPoint(ip.MapToIPv6(), port), this);
            OnConnected();
            return true;
        }

        /// <summary>Parses the <paramref name="hostAddress"/> and retrieves its <paramref name="ip"/> and <paramref name="port"/>, if possible.</summary>
        /// <param name="hostAddress">The host address to parse.</param>
        /// <param name="ip">The retrieved IP.</param>
        /// <param name="port">The retrieved port.</param>
        /// <returns>Whether or not the host address is valid.</returns>
        private bool ParseHostAddress(string hostAddress, out IPAddress ip, out ushort port)
        {
            string[] ipAndPort = hostAddress.Split(':');
            string ipString = "";
            string portString = "";
            if (ipAndPort.Length > 2)
            {
                // There was more than one ':' in the host address, might be IPv6
                ipString = string.Join(":", ipAndPort.Take(ipAndPort.Length - 1));
                portString = ipAndPort[ipAndPort.Length - 1];
            }
            else if (ipAndPort.Length == 2)
            {
                // IPv4
                ipString = ipAndPort[0];
                portString = ipAndPort[1];
            }

            port = 0; // Need to make sure a value is assigned in case IP parsing fails
            return IPAddress.TryParse(ipString, out ip) && ushort.TryParse(portString, out port);
        }

        public void Disconnect()
        {
            StopSocket();
        }

        protected void OnConnected()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint)
        {
            if (udpConnection.RemoteEndPoint.Equals(fromEndPoint))
                DataReceived?.Invoke(this, new DataReceivedEventArgs(dataBuffer, amount, udpConnection));
        }
    }
}