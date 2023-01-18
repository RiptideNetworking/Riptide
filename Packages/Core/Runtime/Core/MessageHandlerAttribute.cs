// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace Riptide
{
    /// <summary>Specifies a method as the message handler for messages with the given ID.</summary>
    /// <remarks>
    ///   <para>
    ///     In order for a method to qualify as a message handler, it <i>must</i> match a valid message handler method signature. <see cref="Server"/>s
    ///     will only use methods marked with this attribute if they match the <see cref="Server.MessageHandler"/> signature, and <see cref="Client"/>s
    ///     will only use methods marked with this attribute if they match the <see cref="Client.MessageHandler"/> signature.
    ///   </para>
    ///   <para>
    ///     Methods marked with this attribute which match neither of the valid message handler signatures will not be used by <see cref="Server"/>s
    ///     or <see cref="Client"/>s and will cause warnings at runtime.
    ///   </para>
    ///   <para>
    ///     If you want a <see cref="Server"/> or <see cref="Client"/> to only use a subset of all message handler methods, you can do so by setting up
    ///     custom message handler groups. Simply set the group ID in the <see cref="MessageHandlerAttribute(ushort, byte)"/> constructor and pass the
    ///     same value to the <see cref="Server.Start(ushort, ushort, byte)"/> or <see cref="Client.Connect(string, int, byte, Message)"/> method. This
    ///     will make that <see cref="Server"/> or <see cref="Client"/> only use message handlers which have the same group ID.
    ///   </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MessageHandlerAttribute : Attribute
    {
        /// <summary>The ID of the message type which this method is meant to handle.</summary>
        public readonly ushort MessageId;
        /// <summary>The ID of the group of message handlers which this method belongs to.</summary>
        public readonly byte GroupId;

        /// <summary>Initializes a new instance of the <see cref="MessageHandlerAttribute"/> class with the <paramref name="messageId"/> and <paramref name="groupId"/> values.</summary>
        /// <param name="messageId">The ID of the message type which this method is meant to handle.</param>
        /// <param name="groupId">The ID of the group of message handlers which this method belongs to.</param>
        /// <remarks>
        ///   <see cref="Server"/>s will only use this method if its signature matches the <see cref="Server.MessageHandler"/> signature.
        ///   <see cref="Client"/>s will only use this method if its signature matches the <see cref="Client.MessageHandler"/> signature.
        ///   This method will be ignored if its signature matches neither of the valid message handler signatures.
        /// </remarks>
        public MessageHandlerAttribute(ushort messageId, byte groupId = 0)
        {
            MessageId = messageId;
            GroupId = groupId;
        }
    }
}
