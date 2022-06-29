// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

namespace RiptideNetworking.Utils
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
    }
}
