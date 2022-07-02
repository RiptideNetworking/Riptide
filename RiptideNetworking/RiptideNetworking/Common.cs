// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using System;
using System.Linq;
using System.Reflection;

namespace Riptide
{
    /// <summary>The reason for a disconnection.</summary>
    public enum DisconnectReason : byte
    {
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
    public abstract class Common
    {
        /// <summary>The number of currently active <see cref="Server"/> and <see cref="Client"/> instances.</summary>
        internal static int ActiveSocketCount { get; private set; }

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

        /// <inheritdoc cref="ICommon.Tick"/>
        public abstract void Tick();
    }
}
