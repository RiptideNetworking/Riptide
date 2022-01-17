
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
        /// <summary>The ID of the message type that this method is meant to handle.</summary>
        public ushort MessageId { get; private set; }
        /// <summary>The ID of the group of message handlers this method belongs to.</summary>
        public byte GroupId { get; private set; }

        /// <summary>Initializes a new instance of the <see cref="MessageHandlerAttribute"/> class with the <paramref name="messageId"/> and <paramref name="groupId"/> values.</summary>
        /// <param name="messageId">The ID of the message type that this method is meant to handle.</param>
        /// <param name="groupId">The ID of the group of message handlers this method belongs to.</param>
        /// <remarks>
        ///   <para>
        ///     <see cref="Server"/> instances will include this method in their message handlers if its signature matches that of <see cref="Server.MessageHandler"/>.<br/>
        ///     <see cref="Client"/> instances will include this method in their message handlers if its signature matches that of <see cref="Client.MessageHandler"/>.
        ///   </para>
        /// </remarks>
        public MessageHandlerAttribute(ushort messageId, byte groupId = 0)
        {
            MessageId = messageId;
            GroupId = groupId;
        }
    }
}
