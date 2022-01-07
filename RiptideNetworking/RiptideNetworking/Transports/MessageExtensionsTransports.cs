
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2022 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

namespace RiptideNetworking.Transports
{
    /// <summary>The header type of a <see cref="Message"/>.</summary>
    public enum HeaderType : byte
    {
        /// <summary>For unreliable user messages.</summary>
        unreliable,
        /// <summary>For unreliable internal ack messages.</summary>
        ack,
        /// <summary>For unreliable internal ack messages (when acknowledging a sequence ID other than the last received one).</summary>
        ackExtra,
        /// <summary>For unreliable internal connect messages.</summary>
        connect,
        /// <summary>For unreliable internal heartbeat messages.</summary>
        heartbeat,
        /// <summary>For unreliable internal disconnect messages.</summary>
        disconnect,
        /// <summary>For reliable user messages.</summary>
        reliable,
        /// <summary>For reliable internal welcome messages.</summary>
        welcome,
        /// <summary>For reliable internal client connected messages.</summary>
        clientConnected,
        /// <summary>For reliable internal client disconnected messages.</summary>
        clientDisconnected,
    }

    /// <summary>Contains extension methods for the <see cref="Message"/> class which are only intended for use by transport-related code.</summary>
    public static class MessageExtensionsTransports
    {
        // These methods aren't required to use Riptide in a project, they're only needed when
        // building custom transports. Having them publicly accessible in the Message class
        // could easily lead to users accidentally using them when they really shouldn't be.
        // Putting them in this class hides them from users unless they have the Transports
        // namespace included, which should prevent most accidents.
        //
        // Sadly C# doesn't support *actual* static extension methods, so transports will have
        // to go through this class to use the Create methods; instead of being able to call
        // Message.Create(), transports will need to use MessageExtensionsTransports.Create() :/

        /// <inheritdoc cref="Message.Create(HeaderType, int)"/>
        public static Message Create(HeaderType messageHeader, int maxSendAttempts = 15) => Message.Create(messageHeader, maxSendAttempts);

        /// <inheritdoc cref="Message.Create()"/>
        public static Message Create() => Message.Create();

        /// <inheritdoc cref="Message.PrepareForUse(ushort)"/>
        public static HeaderType PrepareForUse(this Message message, ushort contentLength) => message.PrepareForUse(contentLength);
    }
}
