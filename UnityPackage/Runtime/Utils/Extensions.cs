
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System.Net;

namespace RiptideNetworking.Utils
{
    /// <summary>Contains extension methods for various classes.</summary>
    public static class Extensions
    {
        /// <summary>Takes an <see cref="IPEndPoint"/> and returns a string containing its IP address and port number, accounting for whether the address is an IPv4 or IPv6.</summary>
        /// <returns>A string containing the IP address and port number of the endpoint.</returns>
        public static string ToStringBasedOnIPFormat(this IPEndPoint endPoint)
        {
            if (endPoint.Address.IsIPv4MappedToIPv6)
                return $"{endPoint.Address.MapToIPv4()}:{endPoint.Port}";
            else
                return endPoint.ToString();
        }
    }
}
