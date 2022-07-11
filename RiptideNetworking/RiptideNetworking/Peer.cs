// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Riptide
{
    /// <summary>The reason for a disconnection.</summary>
    public enum DisconnectReason : byte
    {
        /// <summary>No connection was ever established.</summary>
        neverConnected,
        /// <summary>The active transport detected a problem with the connection.</summary>
        transportError,
        /// <summary>The connection timed out.</summary>
        /// <remarks>
        ///   This also acts as the fallback reason—if a client disconnects and the message containing the <i>real</i> reason is lost
        ///   in transmission, it can't be resent as the connection will have already been closed. As a result, the other end will time
        ///   out the connection after a short period of time and this will be used as the reason.
        /// </remarks>
        timedOut,
        /// <summary>The client was forcibly disconnected by the server.</summary>
        kicked,
        /// <summary>The server shut down.</summary>
        serverStopped,
        /// <summary>The disconnection was initiated by the client.</summary>
        disconnected
    }

    /// <summary>Provides base functionality for <see cref="Server"/> and <see cref="Client"/>.</summary>
    public abstract class Peer
    {
        /// <summary>The name to use when logging messages via <see cref="RiptideLogger"/>.</summary>
        public readonly string LogName;
        /// <summary>The time (in milliseconds) after which to disconnect if no heartbeats are received.</summary>
        public ushort TimeoutTime { get; set; } = 5000;
        /// <summary>The interval (in milliseconds) at which to send and expect heartbeats to be received.</summary>
        public ushort HeartbeatInterval { get; set; } = 1000;

        /// <summary>The number of currently active <see cref="Server"/> and <see cref="Client"/> instances.</summary>
        internal static int ActiveCount { get; private set; }

        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.neverConnected"/>.</summary>
        protected const string ReasonNeverConnected = "Never connected";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.transportError"/>.</summary>
        protected const string ReasonTransportError = "Transport error";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.timedOut"/>.</summary>
        protected const string ReasonTimedOut = "Timed out";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.kicked"/>.</summary>
        protected const string ReasonKicked = "Kicked";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.serverStopped"/>.</summary>
        protected const string ReasonServerStopped = "Server stopped";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.disconnected"/>.</summary>
        protected const string ReasonDisconnected = "Disconnected";
        /// <summary>The text to log when disconnected due to an unknown reason.</summary>
        protected const string ReasonUnknown = "Unknown reason";

        /// <summary>The stopwatch used to determine when it's time to send the next heartbeat.</summary>
        private readonly System.Diagnostics.Stopwatch heartbeatSW = new System.Diagnostics.Stopwatch();
        /// <summary>The time at which to send the next heartbeat.</summary>
        private long nextHeartbeat;
        /// <summary>Received messages which need to be handled.</summary>
        private readonly Queue<MessageToHandle> messagesToHandle = new Queue<MessageToHandle>();

        /// <summary>Initializes the peer.</summary>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public Peer(string logName)
        {
            LogName = logName;
        }

        /// <summary>Retrieves methods marked with <see cref="MessageHandlerAttribute"/>.</summary>
        /// <returns>An array containing message handler methods.</returns>
        protected MethodInfo[] FindMessageHandlers()
        {
            string thisAssemblyName = Assembly.GetExecutingAssembly().GetName().FullName;
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a
                    .GetReferencedAssemblies()
                    .Any(n => n.FullName == thisAssemblyName)) // Get only assemblies that reference this assembly
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)) // Include instance methods in the search so we can show the developer an error instead of silently not adding instance methods to the dictionary
                .Where(m => m.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0)
                .ToArray();
        }

        /// <summary>Builds a dictionary of message IDs and their corresponding message handler methods.</summary>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to include in the dictionary.</param>
        protected abstract void CreateMessageHandlersDictionary(byte messageHandlerGroupId);

        /// <summary>Starts the heart.</summary>
        protected void StartHeartbeat()
        {
            heartbeatSW.Start();
        }

        /// <summary>Stops the heart.</summary>
        /// <remarks><see href="https://tenor.com/view/johnny-depp-captain-jack-sparrow-pirates-of-the-caribbean-at-worlds-end-potc-gif-17032484">More info.</see></remarks>
        protected void StopHeartbeat()
        {
            heartbeatSW.Stop();
        }

        /// <summary>Beats the heart.</summary>
        protected abstract void Heartbeat();

        /// <summary>Calls <see cref="Heartbeat"/> every <see cref="HeartbeatInterval"/> milliseconds.</summary>
        public virtual void Tick()
        {
            if (heartbeatSW.ElapsedMilliseconds > nextHeartbeat)
            {
                nextHeartbeat += HeartbeatInterval;
                Heartbeat();
            }
        }

        /// <summary>Handles all queued messages.</summary>
        protected void HandleMessages()
        {
            while (messagesToHandle.Count > 0)
            {
                MessageToHandle handle = messagesToHandle.Dequeue();
                Handle(handle.Message, handle.MessageHeader, handle.FromConnection);
            }
        }

        /// <summary>Handles data received by the transport.</summary>
        protected void HandleData(object _, DataReceivedEventArgs e)
        {
            HeaderType messageHeader = (HeaderType)e.DataBuffer[0];

            Message message = Message.CreateRaw();
            message.PrepareForUse(messageHeader, (ushort)e.Amount); // Subtract 2 for reliable messages because length will include the 2 bytes used by the sequence ID that don't actually get copied to the message's byte array

            if (message.SendMode == MessageSendMode.reliable)
            {
                if (e.Amount < 3) // Reliable messages have a 3 byte header, if there aren't that many bytes in the packet don't handle it
                    return;

                if (e.FromConnection.ReliableHandle(Converter.ToUShort(e.DataBuffer, 1)))
                {
                    if (e.Amount > 3) // Only bother with the array copy if there are more than 3 bytes in the packet (just 3 means no payload for a reliably sent packet)
                        Array.Copy(e.DataBuffer, 3, message.Bytes, 1, e.Amount - 3);

                    messagesToHandle.Enqueue(new MessageToHandle(message, messageHeader, e.FromConnection));
                }
            }
            else
            {
                if (e.Amount > 1) // Only bother with the array copy if there is more than 1 byte in the packet (1 or less means no payload for a reliably sent packet)
                    Array.Copy(e.DataBuffer, 1, message.Bytes, 1, e.Amount - 1);

                messagesToHandle.Enqueue(new MessageToHandle(message, messageHeader, e.FromConnection));
            }
        }

        /// <summary>Handles a message.</summary>
        /// <param name="message">The message to handle.</param>
        /// <param name="messageHeader">The message's header type.</param>
        /// <param name="connection">The connection which the message was received on.</param>
        protected abstract void Handle(Message message, HeaderType messageHeader, Connection connection);

        /// <summary>Increases <see cref="ActiveCount"/>. For use when a new <see cref="Server"/> or <see cref="Client"/> is started.</summary>
        protected static void IncreaseActiveCount()
        {
            ActiveCount++;
        }

        /// <summary>Decreases <see cref="ActiveCount"/>. For use when a <see cref="Server"/> or <see cref="Client"/> is stopped.</summary>
        protected static void DecreaseActiveCount()
        {
            ActiveCount--;
            if (ActiveCount < 0)
                ActiveCount = 0;
        }
    }

    /// <summary>Stores information about a message that needs to be handled.</summary>
    internal struct MessageToHandle
    {
        /// <summary>The message that needs to be handled.</summary>
        internal readonly Message Message;
        /// <summary>The message's header type.</summary>
        internal readonly HeaderType MessageHeader;
        /// <summary>The connection on which the message was received.</summary>
        internal readonly Connection FromConnection;

        /// <summary>Handles initialization.</summary>
        /// <param name="message">The message that needs to be handled.</param>
        /// <param name="messageHeader">The message's header type.</param>
        /// <param name="fromConnection">The connection on which the message was received.</param>
        public MessageToHandle(Message message, HeaderType messageHeader, Connection fromConnection)
        {
            Message = message;
            MessageHeader = messageHeader;
            FromConnection = fromConnection;
        }
    }
}
