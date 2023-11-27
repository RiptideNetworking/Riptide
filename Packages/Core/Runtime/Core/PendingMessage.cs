// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;

namespace Riptide
{
    /// <summary>Represents a currently pending reliably sent message whose delivery has not been acknowledged yet.</summary>
    internal class PendingMessage
    {
        /// <summary>The time of the latest send attempt.</summary>
        internal long LastSendTime { get; private set; }

        /// <summary>The multiplier used to determine how long to wait before resending a pending message.</summary>
        private const float RetryTimeMultiplier = 1.2f;

        /// <summary>A pool of reusable <see cref="PendingMessage"/> instances.</summary>
        private static readonly List<PendingMessage> pool = new List<PendingMessage>();

        /// <summary>The <see cref="Connection"/> to use to send (and resend) the pending message.</summary>
        private Connection connection;
        /// <summary>The contents of the message.</summary>
        private readonly byte[] data;
        /// <summary>The length in bytes of the message.</summary>
        private int size;
        /// <summary>How many send attempts have been made so far.</summary>
        private byte sendAttempts;
        /// <summary>Whether the pending message has been cleared or not.</summary>
        private bool wasCleared;

        /// <summary>Handles initial setup.</summary>
        internal PendingMessage()
        {
            data = new byte[Message.MaxSize];
        }

        #region Pooling
        /// <summary>Retrieves a <see cref="PendingMessage"/> instance and initializes it.</summary>
        /// <param name="sequenceId">The sequence ID of the message.</param>
        /// <param name="message">The message that is being sent reliably.</param>
        /// <param name="connection">The <see cref="Connection"/> to use to send (and resend) the pending message.</param>
        /// <returns>An intialized <see cref="PendingMessage"/> instance.</returns>
        internal static PendingMessage Create(ushort sequenceId, Message message, Connection connection)
        {
            PendingMessage pendingMessage = RetrieveFromPool();
            pendingMessage.connection = connection;

            message.SetBits(sequenceId, sizeof(ushort) * Converter.BitsPerByte, Message.HeaderBits);
            pendingMessage.size = message.BytesInUse;
            Buffer.BlockCopy(message.Data, 0, pendingMessage.data, 0, pendingMessage.size);

            pendingMessage.sendAttempts = 0;
            pendingMessage.wasCleared = false;
            return pendingMessage;
        }

        /// <summary>Retrieves a <see cref="PendingMessage"/> instance from the pool. If none is available, a new instance is created.</summary>
        /// <returns>A <see cref="PendingMessage"/> instance.</returns>
        private static PendingMessage RetrieveFromPool()
        {
            PendingMessage message;
            if (pool.Count > 0)
            {
                message = pool[0];
                pool.RemoveAt(0);
            }
            else
                message = new PendingMessage();

            return message;
        }

        /// <summary>Empties the pool. Does not affect <see cref="PendingMessage"/> instances which are actively pending and therefore not in the pool.</summary>
        public static void ClearPool()
        {
            pool.Clear();
        }

        /// <summary>Returns the <see cref="PendingMessage"/> instance to the pool so it can be reused.</summary>
        private void Release()
        {
            if (!pool.Contains(this))
                pool.Add(this); // Only add it if it's not already in the list, otherwise this method being called twice in a row for whatever reason could cause *serious* issues

            // TODO: consider doing something to decrease pool capacity if there are far more
            //       available instance than are needed, which could occur if a large burst of
            //       messages has to be sent for some reason
        }
        #endregion

        /// <summary>Resends the message.</summary>
        internal void RetrySend()
        {
            if (!wasCleared)
            {
                long time = connection.Peer.CurrentTime;
                if (LastSendTime + (connection.SmoothRTT < 0 ? 25 : connection.SmoothRTT / 2) <= time) // Avoid triggering a resend if the latest resend was less than half a RTT ago
                    TrySend();
                else
                    connection.Peer.ExecuteLater(connection.SmoothRTT < 0 ? 50 : (long)Math.Max(10, connection.SmoothRTT * RetryTimeMultiplier), new ResendEvent(this, time));
            }
        }

        /// <summary>Attempts to send the message.</summary>
        internal void TrySend()
        {
            if (sendAttempts >= connection.MaxSendAttempts && connection.CanQualityDisconnect)
            {
                RiptideLogger.Log(LogType.Info, connection.Peer.LogName, $"Could not guarantee delivery of a {(MessageHeader)(data[0] & Message.HeaderBitmask)} message after {sendAttempts} attempts! Disconnecting...");
                connection.Peer.Disconnect(connection, DisconnectReason.PoorConnection);
                return;
            }

            connection.Send(data, size);
            connection.Metrics.SentReliable(size);

            LastSendTime = connection.Peer.CurrentTime;
            sendAttempts++;

            connection.Peer.ExecuteLater(connection.SmoothRTT < 0 ? 50 : (long)Math.Max(10, connection.SmoothRTT * RetryTimeMultiplier), new ResendEvent(this, connection.Peer.CurrentTime));
        }

        /// <summary>Clears the message.</summary>
        internal void Clear()
        {
            connection.Metrics.RollingReliableSends.Add(sendAttempts);
            wasCleared = true;
            Release();
        }
    }
}
