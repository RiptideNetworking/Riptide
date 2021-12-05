<div align="center">
  <img src="https://user-images.githubusercontent.com/51303091/119734159-690afc00-be2f-11eb-9673-c1f998025a3e.png" width="20%" height="auto">
</div>
<h1 align="center">RiptideNetworking</h1>

RiptideNetworking is a lightweight C# networking library primarily designed for use in multiplayer games. It can be used in Unity as well as in other .NET environments such as console applications.

Riptide provides functionality for establishing connections and sending data back and forth. Being a rather low-level solution, it leaves it up to you to decide what data you want to send and when, which is ideal if you like to be in control of your code and know what's going on under the hood.

Riptide is 100% free to use and only funded by [donations](https://ko-fi.com/Y8Y21O02J).

## Getting Started
### Unity
Using Unity's Package Manager:
1. Open the Package Manager (Window > Package Manager)
2. Click the '+' (plus) button in the top left corner of the window
3. Select the 'Add package from git URL...' option
4. Enter the following URL: `https://github.com/tom-weiland/RiptideNetworking.git?path=/UnityPackage`
5. Click 'Add' and wait for Riptide to be installed

Alternatively, you can grab the RiptideNetworking.dll file (and RiptideNetworking.xml file for intellisense documentation) from the [latest release](https://github.com/tom-weiland/RiptideNetworking/releases/latest) and simply drop it into your Unity project.

### Other
If you aren't using Unity, you can add the RiptideNetworking.dll file to your project as a project reference:
1. Download RiptideNetworking.dll file from the [latest release](https://github.com/tom-weiland/RiptideNetworking/releases/latest)
2. Right click your project in Visual Studio's Solution Explorer
3. Select 'Add' and then select the 'Project Reference...' option
4. Click 'Browse' in the left sidebar of the window
5. Click the 'Browse' button in the bottom right corner of the window
6. Navigate to the folder where you saved the RiptideNetworking.dll file and add it

## Usage
### Project Considerations
If you are making a player-hosted game (often incorrectly referred to as "peer-to-peer") where one player acts as the server, you'll want all your code in a single project.

However, if you intend to build a standalone server, having separate projects for the server and client may be helpful.

Riptide supports either of those (and more) architecture choices.

### Initial Setup
Set up a `NetworkManager` class like in the [Unity demo projects](https://github.com/tom-weiland/RiptideNetworking/tree/main/Demos/Unity). To run your own logic when certain things happen, you can subscribe to the following events:
- Server
  - `ClientConnected` - invoked when a new client connects
  - `MessageReceived` - invoked when a message is received from a client (useful if you need to run custom logic for _every_ message that is received)
  - `ClientDisconnected` - invoked when a client disconnects
- Client
  - `Connected` - invoked when a connection to the server is established
  - `ConnectionFailed` - invoked when a connection to the server fails to be established
  - `MessageReceived` - invoked when a message is received from a client (useful if you need to run custom logic for _every_ message that is received)
  - `Disconnected` - invoked when disconnected by the server
  - `ClientConnected` - invoked when another client connects
  - `ClientDisonnected` - invoked when another client disconnects

Additionally, you can set up a `MessageExtensions` class to extend the functionality of the `Message` class. This is helpful if you wish to send custom objects over the network—being able to directly Add/Get a `Vector3` from a message is much more convenient than having to call the Add/GetFloat method 3 times in a row. Refer to the [Unity demos](https://github.com/tom-weiland/RiptideNetworking/tree/main/Demos/Unity) for an example.

### Creating and Sending Messages
Messages are created like this:
```
Message message = Message.Create(<messageSendMode>, <messageId>);
```
`<messageSendMode>` should be set to either `MessageSendMode.reliable` or `MessageSendMode.unreliable`, depending on how you want your message to be sent.

`<messageId>` should be set to the message's ID (a ushort). This ID will allow the other end to determine how to handle the message upon receiving it.

To add data to your message, use:
```
message.Add(someValue);
```
`someValue` can be any of the following types (or an array of that type): `byte`, `bool`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `string`

Any custom types you may want to send usually consist of combinations of these primitive types. For example, a `Vector3` consists of 3 `float`s, so instead of literally sending the `Vector3` itself, you would send the x, y, and z, components as 3 separate `float`s (as mentioned above, you can extend the `Message` class to make this less cumbersome).

To send your message, use one of the following:
```
Server.Send(message, <toClientId>); // Sends message from server to 1 client
Server.SendToAll(message); // Sends message from server to all clients
Server.SendToAll(message, <toClientId>); // Sends message from server to all clients except one
Client.Send(message); // Sends message from client to server
```
`<toClientId>` should be set to the ID (a ushort) of the client you want the server to send—or not send, in the case of the SendToAll method—the message to. 

### Handling Messages
To handle messages, simply create a static method and give it the MessageHandler attribute:
```
[MessageHandler(<someMessageFromServerID>)]
private static void HandleSomeMessageFromServer(Message message)
{
    int someInt = message.GetInt();
    bool someBool = message.GetBool();
    float[] someFloats = message.GetFloats();
    
    // Do stuff with the retrieved data here
}
```
`<someMessageFromServerID>` should be set to the ID (a ushort) of the message you want this method to handle.

A few things to note:
- to ensure that messages are actually handled, you _must_ regularly call the Tick method of the Server/Client instance you're using. If you're using Unity, FixedUpdate is a good place to do this
- when retrieving values from messages, ***ORDER MATTERS!*** If you added a `short` followed by a `float` when creating/sending the message, you must call `message.GetShort` and `message.GetFloat` _in that same order_ when handling the message, otherwise you won't get the same values out that you put in
- message handler methods need to be `static`

## Low-Level Transports
- [RUDP Transport](https://github.com/tom-weiland/RiptideNetworking/tree/main/RiptideNetworking/RiptideNetworking/Transports/RudpTransport) (built-in)
- [Steam Transport](https://github.com/tom-weiland/RiptideSteamTransport) (in development)

## License
Distributed under the MIT license. See [LICENSE.md](https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md) for more information.

---

<p align="center">
  <a href="https://tomweiland.net/youtube">YouTube</a>&emsp;<b>•</b>&emsp;<a href="https://discord.com/invite/tomweiland">Discord</a><br>
  <a href="https://ko-fi.com/Y8Y21O02J">
    <img src="https://www.ko-fi.com/img/githubbutton_sm.svg">
  </a>
</p>