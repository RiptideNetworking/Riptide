// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Net;
using System.Net.Sockets;

namespace Riptide.Transports.Udp
{
    public abstract class UdpPeer
    {
        protected const int DefaultSocketBufferSize = 1024 * 1024; // 1MB
        private const int MinSocketBufferSize = 256 * 1024; // 256KB
        /// <summary>How long to wait for a response, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds
        private readonly int socketBufferSize;
        /// <summary>The buffer that incoming data is received into.</summary>
        private readonly byte[] receiveBuffer;
        /// <summary>The socket to use for sending and receiving.</summary>
        private Socket socket;
        /// <summary>Whether or not the socket is ready to send and receive data.</summary>
        private bool isRunning = false;
        private EndPoint remoteEndPoint;

        protected UdpPeer(int socketBufferSize = DefaultSocketBufferSize)
        {
            if (socketBufferSize < MinSocketBufferSize)
                throw new ArgumentOutOfRangeException(nameof(socketBufferSize), $"The minimum socket buffer size is {MinSocketBufferSize}!");

            this.socketBufferSize = socketBufferSize;
            receiveBuffer = new byte[Message.MaxSize + sizeof(ushort)];
        }

        public void Tick()
        {
            Receive();
        }

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

        protected void CloseSocket()
        {
            if (!isRunning)
                return;

            isRunning = false;
            socket.Close();
        }

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
                        byteCount = socket.ReceiveFrom(receiveBuffer, SocketFlags.None, ref remoteEndPoint);
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
                    OnDataReceived(receiveBuffer, byteCount, (IPEndPoint)remoteEndPoint);
            }
        }

        internal void Send(byte[] dataBuffer, int numBytes, IPEndPoint toEndPoint)
        {
            if (isRunning)
                socket.SendTo(dataBuffer, numBytes, SocketFlags.None, toEndPoint);
        }

        protected abstract void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint);
    }
}
