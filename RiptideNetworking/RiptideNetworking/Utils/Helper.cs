// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide.Utils
{
    /// <summary>Contains miscellaneous helper methods.</summary>
    internal class Helper
    {
        /// <summary>Determines whether <paramref name="singular"/> or <paramref name="plural"/> form should be used based on the <paramref name="amount"/>.</summary>
        /// <param name="amount">The amount that <paramref name="singular"/> and <paramref name="plural"/> refer to.</param>
        /// <param name="singular">The singular form.</param>
        /// <param name="plural">The plural form.</param>
        /// <returns><paramref name="singular"/> if <paramref name="amount"/> is 1; otherwise <paramref name="plural"/>.</returns>
        internal static string CorrectForm(int amount, string singular, string plural = "")
        {
            if (string.IsNullOrEmpty(plural))
                plural = $"{singular}s";

            return amount == 1 ? singular : plural;
        }

        /// <summary>Calculates the signed gap between sequence IDs, accounting for wrapping.</summary>
        /// <param name="seqId1">The new sequence ID.</param>
        /// <param name="seqId2">The previous sequence ID.</param>
        /// <returns>The signed gap between the two given sequence IDs. A positive gap means <paramref name="seqId1"/> is newer than <paramref name="seqId2"/>. A negative gap means <paramref name="seqId1"/> is older than <paramref name="seqId2"/>.</returns>
        internal static int GetSequenceGap(ushort seqId1, ushort seqId2)
        {
            int gap = seqId1 - seqId2;
            if (Math.Abs(gap) <= 32768) // Difference is small, meaning sequence IDs are close together
                return gap;
            else // Difference is big, meaning sequence IDs are far apart
                return (seqId1 <= 32768 ? ushort.MaxValue + 1 + seqId1 : seqId1) - (seqId2 <= 32768 ? ushort.MaxValue + 1 + seqId2 : seqId2);
        }
    }
}
