// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

namespace Riptide
{
/// <summary>Represents a currently pending overly reliably sent message whose delivery has not been acknowledged yet.</summary>
internal class QueuedMessage
{
    internal Message Message;

    private QueuedMessage(Message message) {
		Message = message;
	}

	public static QueuedMessage Create(Message message, ushort sequenceId) {
		message = message.MakeQueuedMessageIndependent();
        message.SequenceId = sequenceId;
		return new QueuedMessage(message);
	}
}
}