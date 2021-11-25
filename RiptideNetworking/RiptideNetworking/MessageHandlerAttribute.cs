
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;

namespace RiptideNetworking
{
    /// <summary>Specifies a method as the message handler for messages with the given ID.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MessageHandlerAttribute : Attribute
    {
        /// <inheritdoc cref="messageId"/>
        public ushort MessageId => messageId;
        /// <inheritdoc cref="groupId"/>
        public byte GroupId => groupId;

        /// <summary>The ID of the message type that this method is meant to handle.</summary>
        private readonly ushort messageId;
        /// <summary>The ID of the group of message handlers this method belongs to.</summary>
        private readonly byte groupId;

        /// <summary>Initializes a new instance of the <see cref="MessageHandlerAttribute"/> class with the <paramref name="messageId"/> and <paramref name="groupId"/> values.</summary>
        /// <param name="messageId">The ID of the message type that this method is meant to handle.</param>
        /// <param name="groupId">The ID of the group of message handlers this method belongs to.</param>
        public MessageHandlerAttribute(ushort messageId, byte groupId = 0)
        {
            this.messageId = messageId;
            this.groupId = groupId;
        }
    }
}