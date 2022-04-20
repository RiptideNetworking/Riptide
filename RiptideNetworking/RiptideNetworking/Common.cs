
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Transports;
using System.Reflection;

namespace RiptideNetworking
{
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

        /// <summary>Searches the given assembly for methods with the <see cref="MessageHandlerAttribute"/> and adds them to the dictionary of handler methods.</summary>
        /// <param name="assembly">The assembly to search for methods with the <see cref="MessageHandlerAttribute"/>.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building the message handlers dictionary.</param>
        protected abstract void CreateMessageHandlersDictionary(Assembly assembly, byte messageHandlerGroupId);

        /// <inheritdoc cref="ICommon.Tick"/>
        public abstract void Tick();
    }
}
