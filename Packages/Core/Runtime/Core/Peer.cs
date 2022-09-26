// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Riptide
{
    /// <summary>The reason the connection attempt was rejected.</summary>
    public enum RejectReason : byte
    {
        /// <summary>No response was received from the server (because the client has no internet connection, the server is offline, no server is listening on the target endpoint, etc.).</summary>
        NoConnection,
        /// <summary>The client is already connected.</summary>
        AlreadyConnected,
        /// <summary>A connection attempt is already pending.</summary>
        Pending,
        /// <summary>The server is full.</summary>
        ServerFull,
        /// <summary>The connection attempt was rejected.</summary>
        Rejected,
        /// <summary>The connection attempt was rejected and custom data may have been included with the rejection message.</summary>
        Custom
    }

    /// <summary>The reason for a disconnection.</summary>
    public enum DisconnectReason : byte
    {
        /// <summary>No connection was ever established.</summary>
        NeverConnected,
        /// <summary>The connection attempt was rejected by the server.</summary>
        ConnectionRejected,
        /// <summary>The active transport detected a problem with the connection.</summary>
        TransportError,
        /// <summary>The connection timed out.</summary>
        /// <remarks>
        ///   This also acts as the fallback reason—if a client disconnects and the message containing the <i>real</i> reason is lost
        ///   in transmission, it can't be resent as the connection will have already been closed. As a result, the other end will time
        ///   out the connection after a short period of time and this will be used as the reason.
        /// </remarks>
        TimedOut,
        /// <summary>The client was forcibly disconnected by the server.</summary>
        Kicked,
        /// <summary>The server shut down.</summary>
        ServerStopped,
        /// <summary>The disconnection was initiated by the client.</summary>
        Disconnected
    }

    /// <summary>Provides base functionality for <see cref="Server"/> and <see cref="Client"/>.</summary>
    public abstract class Peer
    {
        /// <summary>The name to use when logging messages via <see cref="RiptideLogger"/>.</summary>
        public readonly string LogName;
        /// <summary>The time (in milliseconds) after which to disconnect if no heartbeats are received.</summary>
        public ushort TimeoutTime { get; set; } = 5000;
        /// <summary>The interval (in milliseconds) at which to send and expect heartbeats to be received.</summary>
        /// <remarks>Changes to this value will only take effect after the next heartbeat is executed.</remarks>
        public ushort HeartbeatInterval { get; set; } = 1000;

        /// <summary>The number of currently active <see cref="Server"/> and <see cref="Client"/> instances.</summary>
        internal static int ActiveCount { get; private set; }

        /// <summary>The current time.</summary>
        internal long CurrentTime { get; private set; }

        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.NeverConnected"/>.</summary>
        protected const string DCNeverConnected = "Never connected";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.TransportError"/>.</summary>
        protected const string DCTransportError = "Transport error";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.TimedOut"/>.</summary>
        protected const string DCTimedOut       = "Timed out";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.Kicked"/>.</summary>
        protected const string DCKicked         = "Kicked";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.ServerStopped"/>.</summary>
        protected const string DCServerStopped  = "Server stopped";
        /// <summary>The text to log when disconnected due to <see cref="DisconnectReason.Disconnected"/>.</summary>
        protected const string DCDisconnected   = "Disconnected";
        /// <summary>The text to log when disconnected or rejected due to an unknown reason.</summary>
        protected const string UnknownReason    = "Unknown reason";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.NoConnection"/>.</summary>
        protected const string CRNoConnection   = "No connection";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.ServerFull"/>.</summary>
        protected const string CRServerFull     = "Server is full";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.Rejected"/>.</summary>
        protected const string CRRejected       = "Rejected";
        /// <summary>The text to log when the connection failed due to <see cref="RejectReason.Custom"/>.</summary>
        protected const string CRCustom         = "Rejected with custom reason";

        /// <summary>A stopwatch used to track how much time has passed.</summary>
        private readonly System.Diagnostics.Stopwatch time = new System.Diagnostics.Stopwatch();
        /// <summary>Received messages which need to be handled.</summary>
        private readonly Queue<MessageToHandle> messagesToHandle = new Queue<MessageToHandle>();
        /// <summary>A queue of events to execute, ordered by how soon they need to be executed.</summary>
        private readonly PriorityQueue<DelayedEvent, long> eventQueue = new PriorityQueue<DelayedEvent, long>();

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

        /// <summary>Starts tracking how much time has passed.</summary>
        protected void StartTime()
        {
            time.Start();
        }

        /// <summary>Stops tracking how much time has passed.</summary>
        protected void StopTime()
        {
            time.Reset();
            eventQueue.Clear();
        }

        /// <summary>Beats the heart.</summary>
        internal abstract void Heartbeat();

        /// <summary>Handles any received messages and invokes any delayed events which need to be invoked.</summary>
        public virtual void Update()
        {
            CurrentTime = time.ElapsedMilliseconds;

            while (eventQueue.Count > 0 && eventQueue.PeekPriority() <= CurrentTime)
                eventQueue.Dequeue().Invoke();
        }

        /// <summary>Sets up a delayed event to be executed after the given time has passed.</summary>
        /// <param name="inMS">How long from now to execute the delayed event, in milliseconds.</param>
        /// <param name="delayedEvent">The delayed event to execute later.</param>
        internal void ExecuteLater(long inMS, DelayedEvent delayedEvent)
        {
            eventQueue.Enqueue(delayedEvent, CurrentTime + inMS);
        }

        /// <summary>Handles all queued messages.</summary>
        protected void HandleMessages()
        {
            while (messagesToHandle.Count > 0)
            {
                MessageToHandle handle = messagesToHandle.Dequeue();
                Handle(handle.Message, handle.Header, handle.FromConnection);
            }
        }

        /// <summary>Handles data received by the transport.</summary>
        protected void HandleData(object _, DataReceivedEventArgs e)
        {
            MessageHeader header = (MessageHeader)e.DataBuffer[0];

            Message message = Message.CreateRaw();
            message.PrepareForUse(header, (ushort)e.Amount);

            if (message.SendMode == MessageSendMode.Reliable)
            {
                if (e.Amount < 3) // Reliable messages have a 3 byte header, if there aren't that many bytes in the packet don't handle it
                    return;

                if (e.FromConnection.ReliableHandle(Converter.ToUShort(e.DataBuffer, 1)))
                {
                    Array.Copy(e.DataBuffer, 1, message.Bytes, 1, e.Amount - 1); // We've already established that the packet contains at least 3 bytes, and we always want to copy the sequence ID over
                    messagesToHandle.Enqueue(new MessageToHandle(message, header, e.FromConnection));
                }
            }
            else
            {
                if (e.Amount > 1) // Only bother with the array copy if there is more than 1 byte in the packet (1 or less means no payload for a reliably sent packet)
                    Array.Copy(e.DataBuffer, 1, message.Bytes, 1, e.Amount - 1);

                messagesToHandle.Enqueue(new MessageToHandle(message, header, e.FromConnection));
            }
        }

        /// <summary>Handles a message.</summary>
        /// <param name="message">The message to handle.</param>
        /// <param name="header">The message's header type.</param>
        /// <param name="connection">The connection which the message was received on.</param>
        protected abstract void Handle(Message message, MessageHeader header, Connection connection);

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
        internal readonly MessageHeader Header;
        /// <summary>The connection on which the message was received.</summary>
        internal readonly Connection FromConnection;

        /// <summary>Handles initialization.</summary>
        /// <param name="message">The message that needs to be handled.</param>
        /// <param name="header">The message's header type.</param>
        /// <param name="fromConnection">The connection on which the message was received.</param>
        public MessageToHandle(Message message, MessageHeader header, Connection fromConnection)
        {
            Message = message;
            Header = header;
            FromConnection = fromConnection;
        }
    }
}
