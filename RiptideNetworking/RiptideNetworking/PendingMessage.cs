// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Timers;

namespace Riptide
{
    /// <summary>Represents a currently pending reliably sent message whose delivery has not been acknowledged yet.</summary>
    internal class PendingMessage
    {
        /// <summary>The multiplier used to determine how long to wait before resending a pending message.</summary>
        private const float RetryTimeMultiplier = 1.2f;

        /// <summary>A pool of reusable <see cref="PendingMessage"/> instances.</summary>
        private static readonly List<PendingMessage> pool = new List<PendingMessage>();

        /// <summary>The <see cref="Connection"/> to use to send (and resend) the pending message.</summary>
        private Connection connection;
        /// <summary>The sequence ID of the message.</summary>
        private ushort sequenceId;
        /// <summary>The contents of the message.</summary>
        private readonly byte[] data;
        /// <summary>The length in bytes of the data that has been written to the message.</summary>
        private int writtenLength;
        /// <summary>How often to try sending the message before giving up.</summary>
        private int maxSendAttempts;
        /// <summary>How many send attempts have been made so far.</summary>
        private byte sendAttempts;
        /// <summary>The time of the latest send attempt.</summary>
        private DateTime lastSendTime;
        /// <summary>The timer responsible for triggering a resend, if all else fails (like acks getting lost or redundant acks not being updated fast enough).</summary>
        private readonly Timer retryTimer;
        /// <summary>Whether the pending message has been cleared or not.</summary>
        private bool wasCleared;

        /// <summary>Handles initial setup.</summary>
        internal PendingMessage()
        {
            data = new byte[Message.MaxSize + sizeof(ushort)]; // + ushort length because we need to add the sequence ID bytes

            retryTimer = new Timer();
            retryTimer.Elapsed += (s, e) => RetrySend();
            retryTimer.AutoReset = false;
        }

        #region Pooling
        /// <summary>Retrieves a <see cref="PendingMessage"/> instance, initializes it and then sends it.</summary>
        /// <param name="sequenceId">The sequence ID of the message.</param>
        /// <param name="message">The message that is being sent reliably.</param>
        /// <param name="connection">The <see cref="Connection"/> to use to send (and resend) the pending message.</param>
        internal static void CreateAndSend(ushort sequenceId, Message message, Connection connection)
        {
            PendingMessage pendingMessage = RetrieveFromPool();
            pendingMessage.connection = connection;
            pendingMessage.sequenceId = sequenceId;

            pendingMessage.data[0] = message.Bytes[0]; // Copy message header
            Converter.FromUShort(sequenceId, pendingMessage.data, 1); // Insert sequence ID
            Array.Copy(message.Bytes, 1, pendingMessage.data, 3, message.WrittenLength - 1); // Copy the rest of the message
            pendingMessage.writtenLength = message.WrittenLength + sizeof(ushort);

            pendingMessage.maxSendAttempts = message.MaxSendAttempts;
            pendingMessage.sendAttempts = 0;
            pendingMessage.wasCleared = false;

            lock (connection.PendingMessages)
            {
                connection.PendingMessages.Add(sequenceId, pendingMessage);
                pendingMessage.TrySend();
            }
        }

        /// <summary>Retrieves a <see cref="PendingMessage"/> instance from the pool. If none is available, a new instance is created.</summary>
        /// <returns>A <see cref="PendingMessage"/> instance.</returns>
        private static PendingMessage RetrieveFromPool()
        {
            lock (pool)
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
        }

        /// <summary>Returns the <see cref="PendingMessage"/> instance to the pool so it can be reused.</summary>
        private void Release()
        {
            lock (pool)
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
            lock (data)
            {
                if (!wasCleared)
                {
                    if (lastSendTime.AddMilliseconds(connection.SmoothRTT < 0 ? 25 : connection.SmoothRTT * 0.5f) <= DateTime.UtcNow) // Avoid triggering a resend if the latest resend was less than half a RTT ago
                        TrySend();
                    else
                    {
                        retryTimer.Start();
                        retryTimer.Interval = connection.SmoothRTT < 0 ? 50 : Math.Max(10, connection.SmoothRTT * RetryTimeMultiplier);
                    }
                }
            }
        }

        /// <summary>Attempts to send the message.</summary>
        internal void TrySend()
        {
            if (sendAttempts >= maxSendAttempts)
            {
                // Send attempts exceeds max send attempts, so give up
                if (RiptideLogger.IsWarningLoggingEnabled)
                {
                    HeaderType headerType = (HeaderType)data[0];
                    if (headerType == HeaderType.reliable)
                        RiptideLogger.Log(LogType.warning, connection.Peer.LogName, $"No ack received for {headerType} message (ID: {Converter.ToUShort(data, 3)}) after {sendAttempts} {Helper.CorrectForm(sendAttempts, "attempt")}, delivery may have failed!");
                    else
                        RiptideLogger.Log(LogType.warning, connection.Peer.LogName, $"No ack received for internal {headerType} message after {sendAttempts} {Helper.CorrectForm(sendAttempts, "attempt")}, delivery may have failed!");
                }

                Clear();
                return;
            }

            connection.Send(data, writtenLength);

            lastSendTime = DateTime.UtcNow;
            sendAttempts++;

            retryTimer.Start();
            retryTimer.Interval = connection.SmoothRTT < 0 ? 50 : Math.Max(10, connection.SmoothRTT * RetryTimeMultiplier);
        }

        /// <summary>Clears the message.</summary>
        /// <param name="shouldRemoveFromDictionary">Whether or not to remove the message from <see cref="Connection.PendingMessages"/>.</param>
        internal void Clear(bool shouldRemoveFromDictionary = true)
        {
            lock (data)
            {
                if (shouldRemoveFromDictionary)
                    lock (connection.PendingMessages)
                        connection.PendingMessages.Remove(sequenceId);

                retryTimer.Stop();
                wasCleared = true;
                Release();
            }
        }
    }
}
