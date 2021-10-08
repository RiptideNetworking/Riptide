using System;

namespace RiptideNetworking
{
    /// <summary>Specifies a method as the message handler for messages with the given ID.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MessageHandlerAttribute : Attribute
    {
        /// <summary>The ID of the message type that this method is meant to handle.</summary>
        public ushort MessageId => messageId;

        /// <summary>The ID of the message type that this method is meant to handle.</summary>
        private readonly ushort messageId;

        /// <summary>Initializes a new instance of the <see cref="MessageHandlerAttribute"/> class with the <paramref name="messageId"/> value.</summary>
        /// <param name="messageId">The ID of the message type that this method is meant to handle.</param>
        public MessageHandlerAttribute(ushort messageId)
        {
            this.messageId = messageId;
        }
    }
}