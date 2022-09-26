// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System.Net;

namespace Riptide.Utils
{
    /// <summary>Contains extension methods for various classes.</summary>
    public static class Extensions
    {
        /// <summary>Takes the <see cref="IPEndPoint"/>'s IP address and port number and converts it to a string, accounting for whether the address is an IPv4 or IPv6 address.</summary>
        /// <returns>A string containing the IP address and port number of the endpoint.</returns>
        public static string ToStringBasedOnIPFormat(this IPEndPoint endPoint)
        {
            if (endPoint.Address.IsIPv4MappedToIPv6)
                return $"{endPoint.Address.MapToIPv4()}:{endPoint.Port}";
            
            return endPoint.ToString();
        }
    }
}
