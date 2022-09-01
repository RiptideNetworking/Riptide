// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Linq;

namespace Riptide
{
    /// <summary>Provides functionality for enabling/disabling automatic message relaying by message type.</summary>
    public class MessageRelayFilter
    {
        /// <summary>The number of bits an int consists of.</summary>
        private const int BitsPerInt = sizeof(int) * 8;

        /// <summary>An array storing all the bits which represent whether messages of a given ID should be relayed or not.</summary>
        private int[] filter;

        /// <summary>Creates a filter of a given size.</summary>
        /// <param name="size">How big to make the filter.</param>
        /// <remarks>
        ///     <paramref name="size"/> should be set to the value of the largest message ID, plus 1. For example, if a server will
        ///     handle messages with IDs 1, 2, 3, 7, and 8, <paramref name="size"/> should be set to 9 (8 is the largest possible value,
        ///     and 8 + 1 = 9) despite the fact that there are only 5 unique message IDs the server will ever handle.
        /// </remarks>
        public MessageRelayFilter(int size) => Set(size);
        /// <summary>Creates a filter based on an enum of message IDs.</summary>
        /// <param name="idEnum">The enum type.</param>
        public MessageRelayFilter(Type idEnum) => Set(GetSizeFromEnum(idEnum));
        /// <summary>Creates a filter of a given size and enables relaying for the given message IDs.</summary>
        /// <param name="size">How big to make the filter.</param>
        /// <param name="idsToEnable">Message IDs to enable auto relaying for.</param>
        /// <remarks><inheritdoc cref="MessageRelayFilter(int)"/></remarks>
        public MessageRelayFilter(int size, params ushort[] idsToEnable)
        {
            Set(size);
            EnableIds(idsToEnable);
        }
        /// <summary>Creates a filter based on an enum of message IDs and enables relaying for the given message IDs.</summary>
        /// <param name="idEnum">The enum type.</param>
        /// <param name="idsToEnable">Message IDs to enable relaying for.</param>
        public MessageRelayFilter(Type idEnum, params Enum[] idsToEnable)
        {
            Set(GetSizeFromEnum(idEnum));
            EnableIds(idsToEnable.Cast<ushort>().ToArray());
        }

        /// <summary>Enables auto relaying for the given message IDs.</summary>
        /// <param name="idsToEnable">Message IDs to enable relaying for.</param>
        private void EnableIds(ushort[] idsToEnable)
        {
            for (int i = 0; i < idsToEnable.Length; i++)
                EnableRelay(idsToEnable[i]);
        }

        /// <summary>Calculate the filter size necessary to manage all message IDs in the given enum.</summary>
        /// <param name="idEnum">The enum type.</param>
        /// <returns>The appropriate filter size.</returns>
        /// <exception cref="ArgumentException"><paramref name="idEnum"/> is not an <see cref="Enum"/>.</exception>
        private int GetSizeFromEnum(Type idEnum)
        {
            if (!idEnum.IsEnum)
                throw new ArgumentException($"Parameter '{nameof(idEnum)}' must be an enum type!", nameof(idEnum));

            return Enum.GetValues(idEnum).Cast<ushort>().Max() + 1;
        }

        /// <summary>Sets the filter size.</summary>
        /// <param name="size">How big to make the filter.</param>
        private void Set(int size)
        {
            filter = new int[size / BitsPerInt + (size % BitsPerInt > 0 ? 1 : 0)];
        }

        /// <summary>Enables auto relaying for the given message ID.</summary>
        /// <param name="forMessageId">The message ID to enable relaying for.</param>
        public void EnableRelay(ushort forMessageId)
        {
            filter[forMessageId / BitsPerInt] |= 1 << (forMessageId % BitsPerInt);
        }
        /// <inheritdoc cref="EnableRelay(ushort)"/>
        public void EnableRelay(Enum forMessageId) => EnableRelay((ushort)(object)forMessageId);
        
        /// <summary>Disables auto relaying for the given message ID.</summary>
        /// <param name="forMessageId">The message ID to enable relaying for.</param>
        public void DisableRelay(ushort forMessageId)
        {
            filter[forMessageId / BitsPerInt] &= ~(1 << (forMessageId % BitsPerInt));
        }
        /// <inheritdoc cref="DisableRelay(ushort)"/>
        public void DisableRelay(Enum forMessageId) => DisableRelay((ushort)(object)forMessageId);

        /// <summary>Checks whether or not messages with the given ID should be relayed.</summary>
        /// <param name="forMessageId">The message ID to check.</param>
        /// <returns>Whether or not messages with the given ID should be relayed.</returns>
        internal bool ShouldRelay(ushort forMessageId)
        {
            return (filter[forMessageId / BitsPerInt] & (1 << (forMessageId % BitsPerInt))) != 0;
        }
    }
}
