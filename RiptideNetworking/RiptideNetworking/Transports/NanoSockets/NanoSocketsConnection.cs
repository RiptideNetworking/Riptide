using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.NanoSockets
{
    /// <summary>Represents a connection to a <see cref="UdpServer"/> or <see cref="UdpClient"/>.</summary>
    public class NanoSocketsConnection : Connection, IEquatable<NanoSocketsConnection>
    {
        /// <summary>The endpoint representing the other end of the connection.</summary>
        public NanoSocketsIPEndPoint RemoteEndPoint => _remoteEndPoint;
        
        private NanoSocketsIPEndPoint _remoteEndPoint;
        
        /// <summary>The local peer this connection is associated with.</summary>
        private readonly NanoSocketsPeer peer;

        /// <summary>Initializes the connection.</summary>
        /// <param name="remoteEndPoint">The endpoint representing the other end of the connection.</param>
        /// <param name="peer">The local peer this connection is associated with.</param>
        internal NanoSocketsConnection(NanoSocketsIPEndPoint remoteEndPoint, NanoSocketsPeer peer)
        {
            _remoteEndPoint = remoteEndPoint;
            this.peer = peer;
        }

        /// <inheritdoc/>
        protected internal override void Send(byte[] dataBuffer, int amount)
        {
            peer.Send(dataBuffer, amount, ref _remoteEndPoint);
        }

        /// <inheritdoc/>
        public override string ToString() => _remoteEndPoint.ToString();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as NanoSocketsConnection);
        /// <inheritdoc/>
        public bool Equals(NanoSocketsConnection other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return _remoteEndPoint.Equals(other._remoteEndPoint);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return -288961498 + _remoteEndPoint.GetHashCode();
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static bool operator ==(NanoSocketsConnection left, NanoSocketsConnection right)
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
        public static bool operator !=(NanoSocketsConnection left, NanoSocketsConnection right) => !(left == right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
