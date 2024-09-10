using Riptide.Utils;

namespace Riptide
{
/// <summary>Represents a currently pending overly reliably sent message whose delivery has not been acknowledged yet.</summary>
internal class QueuedMessage
{
    internal Message message;

	QueuedMessage(Message message) {
		this.message = message;
	}

	public static QueuedMessage Create(Message message, ushort sequenceId) {
        message.SetBits(sequenceId, sizeof(ushort) * Converter.BitsPerByte, Message.HeaderBits);
		return new QueuedMessage(message);
	}
}
}