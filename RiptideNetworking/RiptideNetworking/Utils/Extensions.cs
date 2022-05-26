
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
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

        public static T[] CopySlice<T>(this T[] source, int index, int length, bool padToLength = false)
        {
            int n = length;
            T[] slice = null;

            if (source.Length < index + length)
            {
                n = source.Length - index;
                if (padToLength)
                {
                    slice = new T[length];
                }
            }

            if (slice == null)
                slice = new T[n];
            Array.Copy(source, index, slice, 0, n);
            return slice;
        }

        public static IEnumerable<T[]> Slices<T>(this T[] source, int count, bool padToLength = false)
        {
            for (var i = 0; i < source.Length; i += count)
                yield return source.CopySlice(i, count, padToLength);
        }
    }
}
