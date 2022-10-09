# Updating to v2

As a major version update, Riptide v2.0.0 includes a number of breaking changes. This means that updating will likely cause errors in your project and may result in your application behaving differently. To help make the update process go smoothly and seem less daunting, this article covers the behavior changes and what has been removed or renamed, as well as what you should use instead!

You can also take a look at all the [changes which were required](https://github.com/RiptideNetworking/SampleFPS/commit/ce215ef2a452790fae53af9df7e378830ff886f7) to update the Sample FPS repo to Riptide v2.0.0.

The git URL for installing Riptide via the Unity Package Manager has changed. See the [installation instructions](~/manual/overview/installation.md#option-1-unity-package-manager) for more information.

## Logic and Behavior Changes

- The <code><a href="xref:Riptide.Client.Disconnected">Client.Disconnected</a></code> event is now invoked any time the client disconnects—including when <code><a href="xref:Riptide.Client.Disconnect*">Client.Disconnect()</a></code> is called—instead of only when the disconnection is caused by something outside the client (such as server shutdown, connection failure, etc).
- The default maximum message payload size has been reduced to 1225 bytes (from 1247 bytes) to ensure messages are smaller than the [MTU](https://en.wikipedia.org/wiki/Maximum_transmission_unit). If you were sending messages which were close to the old size limit, those messages may exceed the new maximum and cause errors.

## Renames and Replacements

Quite a lot was renamed in v2.0.0 with the goal of making names clearer and more intuitive. Things that were replaced but which have a direct equivalent that can be used are also listed below.

Previous | What to Use Instead
--- | ---
`Client.Tick` | `Client.Update`
`ClientMessageReceivedEventArgs` | `MessageReceivedEventArgs`
`ClientDisconnectedEventArgs` | `ServerDisconnectedEventArgs`
`ClientDisconnectedEventArgs.Id` | `ServerDisconnectedEventArgs.Client.Id`
`Common` | `Peer`
`HeaderType` | `MessageHeader`
`ICommon` | `IPeer`
`IConnectionInfo` | `Connection` 
`Message.MaxMessageSize` | `Message.MaxSize`
`MessageSendMode.reliable` | `MessageSendMode.Reliable`
`MessageSendMode.unreliable` | `MessageSendMode.Unreliable`
`RiptideNetworking` | `Riptide`
`RiptideNetworking.Utils` | `Riptide.Utils`
`RiptideNetworking.Transports` | `Riptide.Transports`
`RiptideNetworking.Transports.RudpTransport` | `Riptide.Transports.Udp`
`Server.Tick` | `Server.Update`
`ServerClientConnectedEventArgs` | `ServerConnectedEventArgs`
`ServerMessageReceivedEventArgs` | `MessageReceivedEventArgs`
`ServerMessageReceivedEventArgs.FromClientId` | `MessageReceivedEventArgs.FromConnection.Id`

## Removals

A few things were removed in v2.0.0 for a variety of reasons, some of which have alternatives or replacements but require a bit more explanation than those listed in the table above.

#### `Server.AllowAutoMessageRelay` Property and `shouldAutoRelay` Parameter

**Reason:** Having clients decide which messages are automatically relayed by the server was counter-intuitive, and it meant that as long as the server had `AllowAutoMessageRelay` set to true, any message could be made to be automatically relayed, even if it wasn't intended by the developer.

**Alternative:** Servers that have a <code><a href="xref:Riptide.MessageRelayFilter">MessageRelayFilter</a></code> instance assigned to their <code><a href="xref:Riptide.Server.RelayFilter">RelayFilter</a></code> will automatically relay any messages whose IDs are enabled in the filter.

#### `isBigArray` Parameter

**Reason:** Manually setting the `isBigArray` parameter to true was extremely clunky and error-prone.

**Alternative:** None, as this is done automatically now. Arrays with up to 127 elements will only use a single byte to transmit their length, while anything larger (up to 32,767 elements) will automatically use two bytes.

#### `Message.Bytes` Property

**Reason:** The `Message` class's backing byte array was only ever publicly accessible for use by transports, in case they needed to modify a message's data directly. The transport system's overhaul has eliminated this potential need, and having the byte array remain publicly accessible creates a risk for accidental misuse without providing any real benefit.

**Alternative:** There is no direct alternative, but chances are you can do what you need using the <code><a href="xref:Riptide.Message.AddBytes*">AddBytes()</a></code> and <code><a href="xref:Riptide.Message.GetBytes*">GetBytes()</a></code> methods.

#### `LanDiscovery` Class

**Reason:** It was a mess and in dire need of an overhaul which would have involved breaking changes. By removing it, the removal *is* the breaking change (the necessary changes weren't going to make it into v2.0.0) and this way it can be re-added whenever, instead of having to wait for v3.0.0 to be revamped.

**Alternative:** Currently none—it will be overhauled and re-added in a future update. If you need it for your project in the meantime, you can download the file from the old versions on GitHub and manually add it to your project.

#### `ActionQueue` and `DoubleKeyDictionary` Classes

**Reason:** They were unused.

**Alternative:** None.

#### RUDP Transport Classes

**Reason:** As part of the transport system overhaul, transports are no longer responsible for reliable message delivery. This has been completely decoupled and reliability (among many other features) are now implemented on top of the transport, leading to a more consistent development experience between different transports and requiring less transport-specific implementations of features.

**Alternative:** The `UdpPeer`, `UdpServer`, `UdpClient`, and `UdpConnection` classes which are found in the <code><a href="xref:Riptide.Transports.Udp">Riptide.Transports.Udp</a></code> namespace.