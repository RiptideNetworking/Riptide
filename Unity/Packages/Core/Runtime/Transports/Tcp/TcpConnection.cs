// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Riptide.Transports.Tcp
{
    /// <summary>Represents a connection to a <see cref="TcpServer"/> or <see cref="TcpClient"/>.</summary>
    public class TcpConnection : Connection, IEquatable<TcpConnection>
    {
        /// <summary>The endpoint representing the other end of the connection.</summary>
        public readonly IPEndPoint RemoteEndPoint;

        /// <summary>The socket to use for sending and receiving.</summary>
        private readonly Socket socket;
        /// <summary>The local peer this connection is associated with.</summary>
        private readonly TcpPeer peer;

        /// <summary>Initializes the connection.</summary>
        /// <param name="socket">The socket to use for sending and receiving.</param>
        /// <param name="remoteEndPoint">The endpoint representing the other end of the connection.</param>
        /// <param name="peer">The local peer this connection is associated with.</param>
        internal TcpConnection(Socket socket, IPEndPoint remoteEndPoint, TcpPeer peer)
        {
            RemoteEndPoint = remoteEndPoint;
            this.socket = socket;
            this.peer = peer;
        }

        /// <inheritdoc/>
        protected internal override void Send(byte[] dataBuffer, int amount)
        {
            if (socket.Connected)
                socket.Send(dataBuffer, amount, SocketFlags.None);
        }

        /// <summary>Polls the socket and checks if any data was received.</summary>
        internal void Receive()
        {
            bool tryReceiveMore = true;
            while (tryReceiveMore)
            {
                int byteCount = 0;
                try
                {
                    if (socket.Poll(0, SelectMode.SelectRead))
                        byteCount = socket.Receive(peer.ReceivedData, SocketFlags.None);
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
                            peer.OnDisconnected(this, DisconnectReason.transportError);
                            break;
                        case SocketError.ConnectionReset:
                            peer.OnDisconnected(this, DisconnectReason.disconnected);
                            break;
                        case SocketError.TimedOut:
                            peer.OnDisconnected(this, DisconnectReason.timedOut);
                            break;
                        case SocketError.MessageSize:
                            break;
                        default:
                            break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    tryReceiveMore = false;
                    peer.OnDisconnected(this, DisconnectReason.transportError);
                }
                catch (NullReferenceException)
                {
                    tryReceiveMore = false;
                    peer.OnDisconnected(this, DisconnectReason.transportError);
                }

                // TODO: probably need to address spliced & combined packets here
                if (byteCount > 0)
                    peer.OnDataReceived(byteCount, this);
            }
        }

        /// <summary>Closes the connection.</summary>
        internal void Close()
        {
            socket.Close();
        }

        /// <inheritdoc/>
        public override string ToString() => RemoteEndPoint.ToStringBasedOnIPFormat();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as TcpConnection);
        /// <inheritdoc/>
        public bool Equals(TcpConnection other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return RemoteEndPoint.Equals(other.RemoteEndPoint);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return -288961498 + EqualityComparer<IPEndPoint>.Default.GetHashCode(RemoteEndPoint);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool operator ==(TcpConnection left, TcpConnection right)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (left is null)
            {
                if (right is null)
                    return true;

                return false; // Only the left side is null
            }

            // Equals handles case of null on right side
            return left.Equals(right);
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool operator !=(TcpConnection left, TcpConnection right) => !(left == right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
