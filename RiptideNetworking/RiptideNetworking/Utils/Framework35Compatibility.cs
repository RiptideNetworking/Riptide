// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Riptide.Utils
{
    /// <summary>
    /// Class for compatibility with .NET Framework 3.5
    /// </summary>
    public static class Framework35Compatibility
    {
        /// <summary>
        /// Taken from https://source.dot.net/#System.Net.Primitives/src/libraries/Common/src/System/Net/IPAddressParserStatics.cs
        /// </summary>
        internal static class IPAddressParserStatics
        {
            public const int IPv4AddressBytes = 4;
            public const int IPv6AddressBytes = 16;
            public const int IPv6AddressShorts = IPv6AddressBytes / 2;
        }
        
#if NET35
        /// <inheritdoc cref="Attribute.GetCustomAttribute(System.Reflection.MemberInfo, System.Type)"/>
        public static T GetCustomAttribute<T>(this MethodInfo self, bool inherit = true) where T : Attribute => (T)self.GetCustomAttributes(typeof(T), inherit)[0];

        /// <inheritdoc cref="Delegate.Method"/>
        public static MethodInfo GetMethodInfo(this Delegate self) => self.Method;

        /// <inheritdoc cref="Stopwatch.Restart()"/>
        public static void Restart(this Stopwatch self)
        {
            self.Stop();
            self.Reset();
            self.Start();
        }

        /// <summary>
        /// Taken and adapted from https://github.com/microsoft/referencesource/blob/dae14279dd0672adead5de00ac8f117dcf74c184/System/net/System/Net/IPAddress.cs#L721
        /// </summary>
        /// <inheritdoc cref="IPAddress.MapToIPv6()"/>
        public static IPAddress MapToIPv6(this IPAddress self)
        {
            if (self.AddressFamily == AddressFamily.InterNetworkV6)
                return self;

            //TODO: The byte order here might be an issue!
            byte[] labels = new byte[IPAddressParserStatics.IPv6AddressBytes];
            labels[10] = 0xFF; labels[11] = 0xFF;

            //Am aware self.Address is depreciated, there is no other option
#pragma warning disable CS0618
            labels[12] = (byte)((self.Address & 0x0000FF00) >> 8);
            labels[13] = (byte)(self.Address & 0x000000FF);
            labels[14] = (byte)((self.Address & 0xFF000000) >> 24);
            labels[15] = (byte)((self.Address & 0x00FF0000) >> 16);
#pragma warning restore CS0618

            return new IPAddress(labels, 0);
        }

        /// <summary>
        /// Taken and adapted from https://github.com/microsoft/referencesource/blob/dae14279dd0672adead5de00ac8f117dcf74c184/System/net/System/Net/IPAddress.cs#L738
        /// </summary>
        /// <inheritdoc cref="IPAddress.MapToIPv4()"/>
        public static IPAddress MapToIPv4(this IPAddress self) => self.AddressFamily == AddressFamily.InterNetwork
                                                                      ? self
                                                                      : new IPAddress(self.GetAddressBytes()
                                                                                          .Skip(IPAddressParserStatics.IPv6AddressBytes - IPAddressParserStatics.IPv4AddressBytes)
                                                                                          .ToArray());
        

        /// <summary>
        /// Taken and adapted from https://github.com/microsoft/referencesource/blob/dae14279dd0672adead5de00ac8f117dcf74c184/System/net/System/Net/IPAddress.cs#L624
        /// </summary>
        /// <inheritdoc cref="IPAddress.IsIPv4MappedToIPv6()"/>
        public static bool IsIPv4MappedToIPv6(this IPAddress self)
        {
            if (self.AddressFamily != AddressFamily.InterNetworkV6)
                return false;

            byte[] nums = self.GetAddressBytes();
            for (int i = 0; i < 10; i++)
                if (nums[i] != 0) return false;

            return (nums[10] == 0xFF && nums[11] == 0xFF);
        }
#endif
    }
}
