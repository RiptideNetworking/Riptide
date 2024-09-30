// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using System;

namespace Riptide.Utils
{
    /// <summary>Contains miscellaneous helper methods.</summary>
    internal class Helper
    {
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.NeverConnected"/>.</summary>
        private const string DCNeverConnected = "Never connected";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.TransportError"/>.</summary>
        private const string DCTransportError = "Transport error";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.TimedOut"/>.</summary>
        private const string DCTimedOut = "Timed out";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.Kicked"/>.</summary>
        private const string DCKicked = "Kicked";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.ServerStopped"/>.</summary>
        private const string DCServerStopped = "Server stopped";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.Disconnected"/>.</summary>
        private const string DCDisconnected = "Disconnected";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.PoorConnection"/>.</summary>
        private const string DCPoorConnection = "Poor connection";
        /// <summary>The text to log when disconnected or rejected due to an unknown reason.</summary>
        private const string UnknownReason = "Unknown reason";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.NoConnection"/>.</summary>
        private const string CRNoConnection = "No connection";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.AlreadyConnected"/>.</summary>
        private const string CRAlreadyConnected = "This client is already connected";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.ServerFull"/>.</summary>
        private const string CRServerFull = "Server is full";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.Rejected"/>.</summary>
        private const string CRRejected = "Rejected";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.Custom"/>.</summary>
        private const string CRCustom = "Rejected (with custom data)";

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

        /// <summary>Retrieves the appropriate reason string for the given <see cref="DisconnectReason"/>.</summary>
        /// <param name="forReason">The <see cref="DisconnectReason"/> to retrieve the string for.</param>
        /// <returns>The appropriate reason string.</returns>
        internal static string GetReasonString(DisconnectReason forReason)
        {
            switch (forReason)
            {
                case DisconnectReason.NeverConnected:
                    return DCNeverConnected;
                case DisconnectReason.TransportError:
                    return DCTransportError;
                case DisconnectReason.TimedOut:
                    return DCTimedOut;
                case DisconnectReason.Kicked:
                    return DCKicked;
                case DisconnectReason.ServerStopped:
                    return DCServerStopped;
                case DisconnectReason.Disconnected:
                    return DCDisconnected;
                case DisconnectReason.PoorConnection:
                    return DCPoorConnection;
                default:
                    return $"{UnknownReason} '{forReason}'";
            }
        }
        /// <summary>Retrieves the appropriate reason string for the given <see cref="RejectReason"/>.</summary>
        /// <param name="forReason">The <see cref="RejectReason"/> to retrieve the string for.</param>
        /// <returns>The appropriate reason string.</returns>
        internal static string GetReasonString(RejectReason forReason)
        {
            switch (forReason)
            {
                case RejectReason.NoConnection:
                    return CRNoConnection;
                case RejectReason.AlreadyConnected:
                    return CRAlreadyConnected;
                case RejectReason.ServerFull:
                    return CRServerFull;
                case RejectReason.Rejected:
                    return CRRejected;
                case RejectReason.Custom:
                    return CRCustom;
                default:
                    return $"{UnknownReason} '{forReason}'";
            }
        }
    }
}
