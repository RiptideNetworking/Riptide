// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Utils;
using System;
using System.Net;
using System.Net.Sockets;

namespace Riptide.Transports.Udp
{
    public abstract class UdpPeer
    {
        /// <summary>How long to wait for a response, in microseconds.</summary>
        private const int ReceivePollingTime = 500000; // 0.5 seconds
        /// <summary>The socket to use for sending and receiving.</summary>
        private Socket socket;
        /// <summary>Whether or not the socket is ready to send and receive data.</summary>
        private bool isRunning = false;
        /// <summary>The buffer that incoming data is received into.</summary>
        private readonly byte[] receiveBuffer;
        private EndPoint remoteEndPoint;

        protected UdpPeer()
        {
            receiveBuffer = new byte[Message.MaxSize + RiptideConverter.UShortLength];
        }

        public void Tick()
        {
            Receive();
        }

        protected void StartSocket(ushort port = 0)
        {
            if (isRunning)
                StopSocket();

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(localEndPoint);

            remoteEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            isRunning = true;
        }

        protected void StopSocket()
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

            try
            {
                int byteCount;
                while (socket.Available > 0 && socket.Poll(ReceivePollingTime, SelectMode.SelectRead))
                {
                    byteCount = socket.ReceiveFrom(receiveBuffer, SocketFlags.None, ref remoteEndPoint);

                    if (byteCount > 0)
                        OnDataReceived(receiveBuffer, byteCount, (IPEndPoint)remoteEndPoint);
                }
            }
            catch (SocketException ex)
            {
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
                isRunning = false;
            }
            catch (NullReferenceException)
            {
                isRunning = false;
            }
        }

        internal void Send(byte[] dataBuffer, int numBytes, IPEndPoint toEndPoint)
        {
            if (isRunning)
            {
                try
                {
                    socket.SendTo(dataBuffer, numBytes, SocketFlags.None, toEndPoint);
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

        protected abstract void OnDataReceived(byte[] dataBuffer, int amount, IPEndPoint fromEndPoint);
    }
}
