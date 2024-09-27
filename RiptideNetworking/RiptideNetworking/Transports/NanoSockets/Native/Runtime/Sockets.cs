using System;
using System.Runtime.InteropServices;
using static Riptide.Transports.NanoSockets.NanoSockets;

#pragma warning disable CS1591

namespace Riptide.Transports.NanoSockets
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public unsafe struct NanoSocketsIPAddress : IEquatable<NanoSocketsIPAddress>
    {
        [FieldOffset(0)] public ulong high;
        [FieldOffset(8)] public ulong low;

        public NanoSocketsIPAddress(ulong high, ulong low)
        {
            this.high = high;
            this.low = low;
        }

        public void CopyTo(byte* buffer)
        {
            *(ulong*)buffer = high;
            *(ulong*)(buffer + 8) = low;
        }

        public bool Equals(NanoSocketsIPAddress other) => high == other.high && low == other.low;
        public override bool Equals(object obj) => obj is NanoSocketsIPAddress other && Equals(other);

        public override int GetHashCode() => ((16337 + (int)high) ^ ((int)(high >> 32) * 31 + (int)low) ^ (int)(low >> 32)) * 31;

        public override string ToString()
        {
            var buffer = stackalloc byte[64];
            _ = nanosockets_get_ip(ref this, buffer, 64);
            return new string((sbyte*)buffer);
        }

        public static bool operator ==(NanoSocketsIPAddress left, NanoSocketsIPAddress right) => left.high == right.high && left.low == right.low;

        public static bool operator !=(NanoSocketsIPAddress left, NanoSocketsIPAddress right) => left.high != right.high || left.low != right.low;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public unsafe struct NanoSocketsIPEndPoint : IEquatable<NanoSocketsIPEndPoint>
    {
        [FieldOffset(0)] public NanoSocketsIPAddress host;
        [FieldOffset(16)] public ushort port;

        public bool Equals(NanoSocketsIPEndPoint other) => host == other.host && port == other.port;
        public override bool Equals(object obj) => obj is NanoSocketsIPEndPoint other && Equals(other);
        public override int GetHashCode() => host.GetHashCode() + port;

        public override string ToString()
        {
            var buffer = stackalloc byte[64];
            _ = nanosockets_get_ip(ref this, buffer, 64);
            return new string((sbyte*)buffer) + ":" + port;
        }

        public static bool operator ==(NanoSocketsIPEndPoint left, NanoSocketsIPEndPoint right) => left.host == right.host && left.port == right.port;

        public static bool operator !=(NanoSocketsIPEndPoint left, NanoSocketsIPEndPoint right) => left.host != right.host || left.port != right.port;
    }
}
