# Getting Started

For a more complete tutorial on how to set up Riptide in your project, see [this video](https://youtu.be/6kWNZOFcFQw). A lot of things were renamed in v2.0.0, which makes following along with the tutorial more difficult, so it's recommended to use v1.1.0 when watching the video and to use the [upgrade guides](~/manual/updates/guides/updating-to-v2.md) to update your project afterwards.

> [!TIP]
> The video tutorial uses two separate projects (one for the server and one for the client). This makes the separation between server and client very clear, which can be helpful when first getting into multiplayer game development. However, it's not ideal—especially for larger projects—due to the fact that it results in duplicated code and assets. This can be mitigated somewhat by setting up a local package for shared code, but combining server and client in the same project is just as doable and arguably easier to maintain.

## Initial Setup

First of all, we need to tell Riptide how we want information to be logged so that we can see what our code is doing. We can do this using the <code><a href="xref:Riptide.Utils.RiptideLogger.Initialize*">RiptideLogger.Initialize()</a></code> method, which should be called before we do anything else with Riptide. If you're using separate projects for the server and the client, make sure to call it in both.

```cs
RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
```

Obviously this is using Unity's logging methods, so if you're not using Unity for your project you'll need to replace the four log methods with `Console.WriteLine` or your engine's equivalent.

> [!IMPORTANT]
> This article explains the basics and includes various code snippets, but if you'd like to see these snippets in the context of a working demo, take a look at the `NetworkManager` classes in the [dedicated server demo](https://github.com/RiptideNetworking/Riptide/tree/main/Demos/Unity/DedicatedServerDemo)'s [server](https://github.com/RiptideNetworking/Riptide/blob/main/Demos/Unity/DedicatedServerDemo/Server/Assets/Scripts/NetworkManager.cs) and [client](https://github.com/RiptideNetworking/Riptide/blob/main/Demos/Unity/DedicatedServerDemo/Client/Assets/Scripts/NetworkManager.cs) projects.

### Starting a Server

To start a server, we need to create a new <code><a href="xref:Riptide.Server">Server</a></code> instance and then call its <code><a href="xref:Riptide.Server.Start*">Start()</a></code> method, which takes in the port we want it to run on and the maximum number of clients we want to allow to be connected at any given time. You'll likely want to run this code as soon as your server application starts up.

```cs
Server server = new Server();
server.Start(7777, 10);
```

In order for the server to be able to accept connections and process messages, we need to call its <code><a href="xref:Riptide.Server.Update*">Update()</a></code> method on a regular basis. In Unity, this can be done using the provided [FixedUpdate method](https://docs.unity3d.com/ScriptReference/MonoBehaviour.FixedUpdate.html).

```cs
private void FixedUpdate()
{
    server.Update();
}
```

### Connecting a Client

The process of connecting a client is quite similar. First we create a new <code><a href="xref:Riptide.Client">Client</a></code> instance and then we call its <code><a href="xref:Riptide.Client.Connect*">Connect()</a></code> method, which expects a host address as the parameter.

Riptide's default transport requires host addresses to consist of an IP address and a port number, separated by a `:`. Since we're running the server and the client on the same computer right now, we'll use `127.0.0.1` (also known as *localhost*) as the IP.

```cs
Client client = new Client();
client.Connect("127.0.0.1:7777");
```

> [!TIP]
> Connecting to `127.0.0.1` will only work if your server and client applications are running on the same computer. To connect from a computer on a different network you need to connect to your host computer's public IP address instead, and you'll need to portforward to allow traffic from your clients to reach your server.

Finally, we need to call the client's <code><a href="xref:Riptide.Client.Update*">Update()</a></code> method on a regular basis, just like we did with the server.

```cs
private void FixedUpdate()
{
    client.Update();
}
```

At this point, if you run the server and the client you should see log messages informing you that the server started and the client connected!

> [!IMPORTANT]
> Make sure you have the `Run in Background` option enabled (found under Edit > Project Settings > Player > Resolution and Presentation), otherwise your server and client will only be able to communicate with each other when their window is active/in-focus! This used to be enabled by default, but that appears to have changed in newer versions of Unity.

## Hooking Into Events

Riptide's <code><a href="xref:Riptide.Server">Server</a></code> and <code><a href="xref:Riptide.Client">Client</a></code> classes both have several events to allow you to run your own code when various things happen.

For example, you'll likely want your server to spawn a player object when a client connects and destroy it again when they disconnect. You can do this by subscribing your spawn and despawn methods to the <code><a href="xref:Riptide.Server.ClientConnected">ClientConnected</a></code> and <code><a href="xref:Riptide.Server.ClientDisconnected">ClientDisconnected</a></code> events.

The `Client` class's most useful events are probably the <code><a href="xref:Riptide.Client.ConnectionFailed">ConnectionFailed</a></code> and <code><a href="xref:Riptide.Client.Disconnected">Disconnected</a></code> events, which come in handy for things like returning the player to the main menu when their connection attempt fails or they're disconnected.

For a complete list of available events, check out the [server events](xref:Riptide.Server#events) and [client events](xref:Riptide.Client#events).

## Sending Data

In order to send data over the network, it has to be converted to bytes first—you can't just send a string or an int directly. Riptide provides the <code><a href="xref:Riptide.Message">Message</a></code> class to make this process really easy.

### Creating a Message

The first step of sending a message is to get an instance of the class. This is done using the <code><a href="xref:Riptide.Message.Create*">Create()</a></code> method, which requires the message's send mode and an ID as parameters.

```cs
Message message = Message.Create(MessageSendMode.Unreliable, 1);
```

The <code><a href="xref:Riptide.MessageSendMode">MessageSendMode</a></code> can be set to `Reliable` or `Unreliable`. Due to how the internet works, not every packet a computer sends will arrive at its destination. Using the unreliable send mode means Riptide will send the message without doing anything extra to ensure delivery, which may result in some of these messages being lost. Using the reliable send mode will make Riptide track whether or not the message has been successfully delivered, and it will continue to resend it until that is the case.

> [!TIP]
> Your first instinct may be to send everything reliably, but at least in fast-paced games, the opposite is normally true—most information is sent unreliably. Consider the fact that even in an extremely basic setup where you simply send a player's position every tick, a newer, more up-to-date position message will have already been sent by the time a previous one could be detected as lost and be resent, and there's no point in resending outdated information.

Message IDs are used to identify what type of message you're sending, which allows the receiving end to determine how to properly handle it. In the example above, we set the message ID to `1` (in practice you'd probably want to use an enum for message IDs instead of hard-coding the number).

### Adding Data to the Message

To add data to our message, we can simply call the `Add` method for the type we want to add. For example:

```cs
message.AddInt(365);
```

The `Message` class has built-in methods for all primitive data types (`byte`, `bool`, `int`/`uint`, `float`, etc.), `string`s, and structs which implement <code><a href="xref:Riptide.IMessageSerializable">IMessageSerializable</a></code>, as well as arrays of all these types.

Any other types you may want to send should consist of combinations of these supported types. For example, a `Vector3` consists of three `float`s (one for each component), so to add one to your message, you would simply call <code><a href="xref:Riptide.Message.AddFloat*">AddFloat()</a></code> three times, passing in the vector's three different components. Alternatively, you could write a custom extension method to make this easier, just like [the ones included in the Unity package](https://github.com/RiptideNetworking/Riptide/blob/unity-package/Packages/Core/Runtime/UnitySpecific/MessageExtensions.cs).

### Sending the Message

Once you've added the data you want to include in your message, it's time to send it. Clients have only one <code><a href="xref:Riptide.Client.Send*">Send()</a></code> method, while servers have <code><a href="xref:Riptide.Server.Send*">Send()</a></code> and <code><a href="xref:Riptide.Server.SendToAll*">SendToAll()</a></code> (which has an overload as well).

```cs
client.Send(message); // Sends the message to the server

server.Send(message, <toClientId>); // Sends the message to a specific client
server.SendToAll(message); // Sends the message to all connected clients
server.SendToAll(message, <toClientId>); // Sends the message to all connected clients except the specified one
```

Make sure to replace `<toClientId>` with the ID of the client you want to send the message to, or who you *don't* want to sent the message to if you're using the `SendToAll()` method.

### Handling the Message

Messages are handled in "message handler" methods. These are just regular static methods with a <code>[<a href="xref:Riptide.MessageHandlerAttribute">MessageHandler</a>]</code> attribute attached.

```cs
[MessageHandler(1)]
private static void HandleSomeMessageFromServer(Message message)
{
    int someInt = message.GetInt();
    
    // Do stuff with the retrieved data here
}
```

Notice that we've passed `1` to the `[MessageHandler]` attribute. This tells Riptide that this method is meant to handle messages with an ID of `1`, which is what we set our message's ID to in the [creating a message](#creating-a-message) part of this article.

> [!IMPORTANT]
> Whether a message handler method handles messages received from a server or a client is determined by its parameters. In order for a handler method to handle messages from clients, it must have two parameters (a `ushort` and a `Message` instance). In order for a handler method to handle messages from a server, it must have only one parameter (a `Message` instance). The code snippet above shows a client-side message handler, which will only handle messages received from the server.

> [!CAUTION]
> **Data MUST be retrieved in the exact order in which it was added to the message!** If you added an `int`, followed by a `float` and then another `int`, you must retrieve an `int` and a `float` *before* you can retrieve the second `int`. Mixing up the order will result in your retrieved values being completely different from what you added to the message.