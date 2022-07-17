// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

namespace Riptide.Utils
{
    /// <summary>Executes an action when invoked.</summary>
    internal abstract class DelayedEvent
    {
        /// <summary>Executes the action.</summary>
        public abstract void Invoke();
    }

    /// <summary>Resends a <see cref="PendingMessage"/> when invoked.</summary>
    internal class PendingMessageResendEvent : DelayedEvent
    {
        /// <summary>The message to resend.</summary>
        private readonly PendingMessage message;
        /// <summary>The time at which the resend event was queued.</summary>
        private readonly long initiatedAtTime;

        /// <summary>Initializes the event.</summary>
        /// <param name="message">The message to resend.</param>
        /// <param name="initiatedAtTime">The time at which the resend event was queued.</param>
        public PendingMessageResendEvent(PendingMessage message, long initiatedAtTime)
        {
            this.message = message;
            this.initiatedAtTime = initiatedAtTime;
        }

        /// <inheritdoc/>
        public override void Invoke()
        {
            if (initiatedAtTime == message.LastSendTime) // If this isn't the case then the message has been resent already
                message.RetrySend();
        }
    }

    /// <summary>Executes a heartbeat when invoked.</summary>
    internal class HeartbeatEvent : DelayedEvent
    {
        /// <summary>The peer whose heart to beat.</summary>
        private readonly Peer peer;

        /// <summary>Initializes the event.</summary>
        /// <param name="peer">The peer whose heart to beat.</param>
        public HeartbeatEvent(Peer peer)
        {
            this.peer = peer;
        }

        /// <inheritdoc/>
        public override void Invoke()
        {
            peer.Heartbeat();
        }
    }
}
