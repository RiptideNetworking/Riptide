// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Net;

namespace Riptide.Transports.Udp
{
    public class UdpConnection : Connection, IEquatable<UdpConnection>
    {
        public readonly IPEndPoint RemoteEndPoint;

        private readonly UdpPeer peer;

        internal UdpConnection(IPEndPoint remoteEndPoint, UdpPeer peer)
        {
            RemoteEndPoint = remoteEndPoint;
            this.peer = peer;
        }

        protected internal override void Send(byte[] dataBuffer, int amount)
        {
            peer.Send(dataBuffer, amount, RemoteEndPoint);
        }

        /// <inheritdoc/>
        public override string ToString() => RemoteEndPoint.ToStringBasedOnIPFormat();

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as UdpConnection);
        /// <inheritdoc/>
        public bool Equals(UdpConnection other)
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
        public static bool operator ==(UdpConnection left, UdpConnection right)
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
        public static bool operator !=(UdpConnection left, UdpConnection right) => !(left == right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
