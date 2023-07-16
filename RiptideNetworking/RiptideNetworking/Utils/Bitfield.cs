// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;

namespace Riptide.Utils
{
    /// <summary>Provides functionality for managing and manipulating a collection of bits.</summary>
    internal class Bitfield
    {
        /// <summary>The first 8 bits stored in the bitfield.</summary>
        internal byte First8 => (byte)segments[0];
        /// <summary>The first 16 bits stored in the bitfield.</summary>
        internal ushort First16 => (ushort)segments[0];

        /// <summary>The number of bits which fit into a single segment.</summary>
        private const int SegmentSize = sizeof(uint) * 8;
        /// <summary>The segments of the bitfield.</summary>
        private readonly List<uint> segments;
        /// <summary>Whether or not the bitfield's capacity should dynamically adjust when shifting.</summary>
        private readonly bool isDynamicCapacity;
        /// <summary>The current number of bits being stored.</summary>
        private int count;
        /// <summary>The current capacity.</summary>
        private int capacity;
        
        /// <summary>Creates a bitfield.</summary>
        /// <param name="isDynamicCapacity">Whether or not the bitfield's capacity should dynamically adjust when shifting.</param>
        internal Bitfield(bool isDynamicCapacity = true)
        {
            segments = new List<uint>(4) { 0 };
            capacity = segments.Count * SegmentSize;
            this.isDynamicCapacity = isDynamicCapacity;
        }

        /// <summary>Checks if the bitfield has capacity for the given number of bits.</summary>
        /// <param name="amount">The number of bits for which to check if there is capacity.</param>
        /// <param name="overflow">The number of bits from <paramref name="amount"/> which there is no capacity for.</param>
        /// <returns>Whether or not there is sufficient capacity.</returns>
        internal bool HasCapacityFor(int amount, out int overflow)
        {
            overflow = count + amount - capacity;
            return overflow < 0;
        }

        /// <summary>Shifts the bitfield by the given amount.</summary>
        /// <param name="amount">How much to shift by.</param>
        internal void ShiftBy(int amount)
        {
            int segmentShift = amount / SegmentSize; // How many WHOLE segments we have to shift by
            int bitShift = amount % SegmentSize; // How many bits we have to shift by

            if (!isDynamicCapacity)
                count = Math.Min(count + amount, SegmentSize);
            else if (!HasCapacityFor(amount, out int _))
            {
                Trim();
                count += amount;

                if (count > capacity)
                {
                    int increaseBy = segmentShift + 1;
                    for (int i = 0; i < increaseBy; i++)
                        segments.Add(0);

                    capacity = segments.Count * SegmentSize;
                }
            }
            else
                count += amount;

            int s = segments.Count - 1;
            segments[s] <<= bitShift;
            s -= 1 + segmentShift;
            while (s > -1)
            {
                ulong shiftedBits = (ulong)segments[s] << bitShift;
                segments[s] = (uint)shiftedBits;

                segments[s + 1 + segmentShift] |= (uint)(shiftedBits >> SegmentSize);
                s--;
            }
        }

        /// <summary>Checks the last bit in the bitfield, and trims it if it is set to 1.</summary>
        /// <param name="checkedPosition">The checked bit's position in the bitfield.</param>
        /// <returns>Whether or not the checked bit was set.</returns>
        internal bool CheckAndTrimLast(out int checkedPosition)
        {
            checkedPosition = count;
            uint bitToCheck = (uint)(1 << ((count - 1) % SegmentSize));
            bool isSet = (segments[segments.Count - 1] & bitToCheck) != 0;
            count--;
            return isSet;
        }

        /// <summary>Trims all bits from the end of the bitfield until an unset bit is encountered.</summary>
        private void Trim()
        {
            while (count > 0 && IsSet(count))
                count--;
        }

        /// <summary>Sets the given bit to 1.</summary>
        /// <param name="bit">The bit to set.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bit"/> is less than 1.</exception>
        internal void Set(int bit)
        {
            if (bit < 1)
                throw new ArgumentOutOfRangeException(nameof(bit), $"'{nameof(bit)}' must be greater than zero!");

            bit--;
            int s = bit / SegmentSize;
            uint bitToSet = (uint)(1 << (bit % SegmentSize));
            if (s < segments.Count)
                segments[s] |= bitToSet;
        }

        /// <summary>Checks if the given bit is set to 1.</summary>
        /// <param name="bit">The bit to check.</param>
        /// <returns>Whether or not the bit is set.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bit"/> is less than 1.</exception>
        internal bool IsSet(int bit)
        {
            if (bit > count)
                return true;

            if (bit < 1)
                throw new ArgumentOutOfRangeException(nameof(bit), $"'{nameof(bit)}' must be greater than zero!");

            bit--;
            int s = bit / SegmentSize;
            uint bitToCheck = (uint)(1 << (bit % SegmentSize));
            if (s < segments.Count)
                return (segments[s] & bitToCheck) != 0;

            return true;
        }

        /// <summary>Combines this bitfield with the given bits.</summary>
        /// <param name="other">The bits to OR into the bitfield.</param>
        internal void Combine(ushort other)
        {
            segments[0] |= other;
        }
    }
}
