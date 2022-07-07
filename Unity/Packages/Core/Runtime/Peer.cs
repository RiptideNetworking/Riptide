// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Linq;
using System.Reflection;

namespace Riptide
{
    /// <summary>The reason for a disconnection.</summary>
    public enum DisconnectReason : byte
    {
        neverConnected,
        transportError,
        /// <summary>For when a client's connection times out. This also acts as the fallback reason—if a client disconnects and the message containing the <i>real</i> reason is lost in transmission, it can't
        /// be resent as the connection will have already been closed. As a result, the other end will time out the connection after a short period of time and this will be used as the reason.</summary>
        timedOut,
        /// <summary>For when a client is forcibly disconnected by the server.</summary>
        kicked,
        /// <summary>For when the server shuts down.</summary>
        serverStopped,
        /// <summary>For when a client voluntarily disconnects.</summary>
        disconnected
    }

    /// <summary>Contains shared functionality for <see cref="Server"/> and <see cref="Client"/>.</summary>
    public abstract class Peer
    {
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

        /// <summary>The number of currently active <see cref="Server"/> and <see cref="Client"/> instances.</summary>
        internal static int ActiveSocketCount { get; private set; }

        public readonly string LogName;
        /// <summary>The time (in milliseconds) after which to disconnect if there's no heartbeat from the server.</summary>
        public ushort TimeoutTime { get; set; } = 5000;
        /// <summary>The interval (in milliseconds) at which to send and expect heartbeats from the server.</summary>
        public ushort HeartbeatInterval { get; set; } = 1000;
        private readonly System.Diagnostics.Stopwatch heartbeatSW = new System.Diagnostics.Stopwatch();
        private long nextHeartbeat;

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

        /// <summary>Searches the given assembly for methods with the <see cref="MessageHandlerAttribute"/> and adds them to the dictionary of handler methods.</summary>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building the message handlers dictionary.</param>
        protected abstract void CreateMessageHandlersDictionary(byte messageHandlerGroupId);

        protected void StartHeartbeat()
        {
            heartbeatSW.Start();
        }

        protected void StopHeartbeat()
        {
            heartbeatSW.Stop();
        }

        protected abstract void Heartbeat();

        /// <inheritdoc cref="IPeer.Tick"/>
        public virtual void Tick()
        {
            if (heartbeatSW.ElapsedMilliseconds > nextHeartbeat)
            {
                nextHeartbeat += HeartbeatInterval;
                Heartbeat();
            }
        }

        protected void HandleData(object sender, DataReceivedEventArgs e)
        {
            HeaderType messageHeader = (HeaderType)e.DataBuffer[0];

            Message message = Message.CreateRaw();
            message.PrepareForUse(messageHeader, (ushort)(messageHeader >= HeaderType.reliable ? e.Amount - 2 : e.Amount)); // Subtract 2 for reliable messages because length will include the 2 bytes used by the sequence ID that don't actually get copied to the message's byte array

            if (message.SendMode == MessageSendMode.reliable)
            {
                if (e.Amount > 3) // Only bother with the array copy if there are more than 3 bytes in the packet (3 or less means no payload for a reliably sent packet)
                    Array.Copy(e.DataBuffer, 3, message.Bytes, 1, e.Amount - 3);
                else if (e.Amount < 3) // Reliable messages have a 3 byte header, if there aren't that many bytes in the packet don't handle it
                    return;

                if (e.FromConnection.ReliableHandle(RiptideConverter.ToUShort(e.DataBuffer, 1)))
                    Handle(message, messageHeader, e.FromConnection);
            }
            else
            {
                if (e.Amount > 1) // Only bother with the array copy if there is more than 1 byte in the packet (1 or less means no payload for a reliably sent packet)
                    Array.Copy(e.DataBuffer, 1, message.Bytes, 1, e.Amount - 1);

                Handle(message, messageHeader, e.FromConnection);
            }
        }

        protected abstract void Handle(Message message, HeaderType messageHeader, Connection connection);

        /// <summary>Increases <see cref="ActiveSocketCount"/>. For use when a new <see cref="Server"/> or <see cref="Client"/> is started.</summary>
        protected static void IncreaseActiveSocketCount()
        {
            ActiveSocketCount++;
        }

        /// <summary>Decreases <see cref="ActiveSocketCount"/>. For use when a <see cref="Server"/> or <see cref="Client"/> is stopped.</summary>
        protected static void DecreaseActiveSocketCount()
        {
            ActiveSocketCount--;
            if (ActiveSocketCount < 0)
                ActiveSocketCount = 0;
        }
    }
}
