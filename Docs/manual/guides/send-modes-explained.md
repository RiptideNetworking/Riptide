---
title: Message Send Modes Explained
_description: How Riptide's various message send modes work and how to use them.
---

# Message Send Modes Explained

Riptide has three different message send modes (defined in the <code><xref:Riptide.MessageSendMode></code> enum) that you can use to send data. This article explains the differences between them and how & when to use them.

The table below provides a quick functionality comparison:

Send Mode  | Guaranteed Delivery |  Ordered   | Delivery Feedback | Duplicate Filtering
---------- | :-----------------: | :--------: | :---------------: | :-----------------:
Unreliable | &cross;             | &cross;    | &cross;           | &cross;
Reliable   | &check;             | &cross;    | &check;           | &check;
Notify     | &cross;             | &check;    | &check;           | &check;

## Unreliable Send Mode

The most basic of the three send modes is the `Unreliable` send mode. It guarantees neither delivery nor order. When you send a message unreliably it is sent once and effectively forgotten about—nothing further is done to ensure order *or* delivery.

Due to how the internet works, a message you send may sometimes get duplicated in transmission. Since unreliably sent messages aren't given a sequence ID, Riptide can't use said sequence ID to determine if a messages was already received once before.

However, even if the unreliable send mode had a duplicate filtering system, it would *not* be able to catch duplicates which are sent maliciously with a unique sequence ID, so your server should be capable of receiving the same data more than once anyways.

### <a name="unreliable-usage"></a>Usage

The unreliable send mode is primarily useful for sending data which changes frequently and is being updated continually, such as a player's position. It doesn't really matter if a position update goes missing here and there due to packet loss as the next update is probably already in transmission, and resending the update that was lost to ensure its delivery wouldn't make much sense since it's already outdated information.

Unreliable messages have message IDs built in, so you should use the <code><xref:Riptide.Message.Create(Riptide.MessageSendMode,System.UInt16)></code> or <code><xref:Riptide.Message.Create(Riptide.MessageSendMode,System.Enum)></code> overloads when creating your message (replace `<messageId>` with your message ID):
```cs
Message.Create(MessageSendMode.Unreliable, <messageId>);
```
You can handle unreliable messages using static methods with <code>[<xref:Riptide.MessageHandlerAttribute?text=MessageHandler>]</code> attributes attached, or via the <code><xref:Riptide.Server.MessageReceived?displayProperty=nameWithType></code> & <code><xref:Riptide.Client.MessageReceived?displayProperty=nameWithType></code> events.

## Reliable Send Mode

The `Reliable` send mode guarantees delivery but not order. When you send a message reliably, it is resent repeatedly under the hood until the other end responds and acknowledges that it received the message in question.

Reliable mode assigns each message a sequence ID and uses that to (among other things) filter out duplicate messages on the receiving end. However, this duplicate filtering system will *not* catch duplicates which are sent maliciously with a unique sequence ID, so your server should be capable of receiving the same data more than once without breaking. 

### <a name="reliable-usage"></a>Usage

The reliable send mode is primarily useful for sending "one-off" data and data which changes less often, but whose delivery is important. Player chat messages are a good example of this.

While reliable mode doesn't inherently guarantee order, you *can* manually ensure data arrives in the correct order by using the <code><xref:Riptide.Connection.ReliableDelivered?displayProperty=nameWithType></code> event to wait for the previous message to be delivered before sending the next one. This obviously comes at the cost of some added latency since you're sending messages and awaiting their delivery one at a time, but for something like player chat messages that likely wouldn't be an issue.

You can identify messages by tracking the sequence ID returned by the `Send` method and comparing it to the one provided by the `ReliableDelivered` event.

Reliable messages have message IDs built in, so you should use the <code><xref:Riptide.Message.Create(Riptide.MessageSendMode,System.UInt16)></code> or <code><xref:Riptide.Message.Create(Riptide.MessageSendMode,System.Enum)></code> overloads when creating your message (replace `<messageId>` with your message ID):
```cs
Message.Create(MessageSendMode.Reliable, <messageId>);
```
You can handle reliable messages using static methods with <code>[<xref:Riptide.MessageHandlerAttribute?text=MessageHandler>]</code> attributes attached, or via the <code><xref:Riptide.Server.MessageReceived?displayProperty=nameWithType></code> & <code><xref:Riptide.Client.MessageReceived?displayProperty=nameWithType></code> events.

## Notify Send Mode

Added in v2.1.0, the `Notify` send mode is the newest—but arguably the most powerful and versatile—of the three modes. It guarantees order but not delivery, and provides actionable feedback to the sender about what happened to each message.

Notify mode guarantees order by simply having the receiver discard any out of order messages it receives. No packet buffering or reordering takes place on the receiving end.

The sender invokes the <code><xref:Riptide.Connection.NotifyLost?displayProperty=nameWithType></code> or <code><xref:Riptide.Connection.NotifyDelivered?displayProperty=nameWithType></code> event depending on whether the message was lost or delivered, allowing you to determine what to do with that information. Messages discarded due to being received out of order are considered lost.

> [!IMPORTANT]
> The notify send mode includes its "acks" in the headers of other notify messages. This is more bandwidth-efficient than the reliable send mode (which sends separate unreliable ack packets for each message), but it means that *both* ends of the connection ***must*** send notify messages at a similar rate in order for it to work properly!

Notify mode assigns each message a sequence ID and uses that to (among other things) filter out duplicate messages on the receiving end. However, this duplicate filtering system will *not* catch duplicates which are sent maliciously with a unique sequence ID, so your server should be capable of receiving the same data more than once without breaking. 

### <a name="notify-usage"></a>Usage

The notify send mode gives you complete control by allowing you to decide what actions to take based on what happened to which data. For example, if a player's health changes twice in quick succession and the message containing the first health update is lost, you can avoid resending that data because you know you already sent a more recent health update.

You can identify messages by tracking the sequence ID returned by the `Send` method and comparing it to the one provided by the `NotifyLost` and `NotifyDelivered` events.

This level of control unlocks the ability to balance speed, reliability, and bandwidth-efficiency as necessary and makes it capable of replacing the other two send modes in the vast majority of situations.

Notify messages do *not* have message IDs built in, so you should use the <code><xref:Riptide.Message.Create(Riptide.MessageSendMode)></code> overload when creating your message:
```cs
Message.Create(MessageSendMode.Reliable);
```
You can handle notify messages via the <code><xref:Riptide.Connection.NotifyReceived?displayProperty=nameWithType></code> event.
