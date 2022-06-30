// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

namespace Riptide.Transports
{
    /// <summary>The header type of a <see cref="Message"/>.</summary>
    public enum HeaderType : byte
    {
        /// <summary>For unreliable user messages.</summary>
        unreliable,
        /// <summary>For unreliable user messages that servers should automatically relay to all other clients.</summary>
        unreliableAutoRelay = unreliable + 1,
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
        /// <summary>For reliable user messages that servers should automatically relay to all other clients.</summary>
        reliableAutoRelay = reliable + 1,
        /// <summary>For reliable internal welcome messages.</summary>
        welcome,
        /// <summary>For reliable internal client connected messages.</summary>
        clientConnected,
        /// <summary>For reliable internal client disconnected messages.</summary>
        clientDisconnected,
    }

    /// <summary>Contains extension methods for the <see cref="Message"/> class which are required by transport-related code but unnecessary in (and generally unintended for) everyday use of Riptide.</summary>
    /// <remarks>Exposing these publicly as part of the <see cref="Message"/> class would make them accessible and show up in intellisense wherever the <see cref="Riptide"/>
    /// namespace is used. By making them extension methods housed in the <see cref="Transports"/> namespace, usage requires explicitly referencing said namespace, which
    /// should help avoid most cases of users accidentally using these methods simply because they are accessible and show up in intellisense.</remarks>
    public static class Extensions
    {
        //                        - Regarding the Create methods -
        // Sadly C# doesn't support *actual* static extension methods, so transports will have
        // to go through this class to use the Create methods; instead of being able to call
        // Message.Create(), transports will need to use MessageExtensionsTransports.Create() :/

        /// <inheritdoc cref="Message.Create(HeaderType, int)"/>
        public static Message Create(HeaderType messageHeader, int maxSendAttempts = 15) => Message.Create(messageHeader, maxSendAttempts);

        /// <inheritdoc cref="Message.CreateRaw()"/>
        public static Message CreateRaw() => Message.CreateRaw();

        /// <inheritdoc cref="Message.PrepareForUse(HeaderType, ushort)"/>
        public static void PrepareForUse(this Message message, HeaderType messageHeader, ushort contentLength) => message.PrepareForUse(messageHeader, contentLength);

        /// <inheritdoc cref="Message.SetHeader(HeaderType)"/>
        public static void SetHeader(this Message message, HeaderType messageHeader) => message.SetHeader(messageHeader);

        /// <summary>Retrieves the message's underlying <see cref="byte"/> array.</summary>
        /// <returns>The message's underlying <see cref="byte"/> array.</returns>
        public static byte[] GetDataBytes(this Message message) => message.Bytes;
    }
}
