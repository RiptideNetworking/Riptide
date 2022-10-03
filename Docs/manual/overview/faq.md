# FAQ

Below are some frequently asked questions as well as common issues which you may face when getting started with Riptide. If you can't find your question or the provided answer/solution is insufficient, feel free to join the [Discord server](https://discord.gg/tomweiland) and ask there!

#### What's the difference between Riptide and other networking solutions?

Riptide is a relatively low level networking solution—it gives you the tools to manage connections and send data, while leaving the rest up to you. This means you have full control over what happens, how it happens, and when it happens, but it also means you have to do more yourself. That might sound scary if you're just getting started with multiplayer game development, but it can actually be hugely beneficial for your learning process.

This is in stark contrast to higher-level, more abstracted solutions, which provide more features and do more for you. However, this typically means you have less control over what your code is doing, and it may make learning how multiplayer games actually work more difficult because you're not exposed to what's going on under the hood.

Whether you should use an abstracted, high-level solution or something lower-level like Riptide depends on what your needs and goals are. Do you want to see and be in charge of what's going on under the hood? Riptide is likely better for you. Do you just want to quickly build a multiplayer game without having to decide what happens and when? Then you may want to consider using something higher-level.

#### Does Riptide cost money to use?

No. Riptide is completely free to use, and it imposes no arbitrary concurrent user limits. You can connect as many players as you like, as long as the hardware your server is running on can handle it and you have sufficient available bandwidth.

However, if you'd like to financially support Riptide's development and get early access to new features, you can do so through [GitHub Sponsors](https://github.com/sponsors/tom-weiland).

#### Can I use Riptide outside of Unity?

Yes! Riptide is not dependent on Unity in any way, meaning you can use it pretty much anywhere you can run C# code. This includes .NET applications such as console apps as well as other engines like Flax Engine and Godot.

#### What platforms can I use Riptide on?

This varies depending on which low level transport you're using. Riptide's default transport uses UDP sockets and works on PC, Mac, Linux, iOS and Android. VR and consoles have not been officially tested to determine whether Riptide works on them, and at this point in time there is no web transport, meaning Riptide does *not* work in browser-based games.

If you end up using Riptide's [Steam transport](https://github.com/RiptideNetworking/SteamTransport), that obviously only works on platforms supported by Steam.

#### How many players does Riptide support?

This *heavily* depends on how much data your game needs to send per second per player and how that compares to the hardware and bandwidth available to your server. A turn-based card game would likely be able to support hundreds of times as many players as a fast-paced shooter while using the same resources.

A big part of developing multiplayer games is choosing what data to send and when to send it. Riptide leaves that part up to you, so you're the one making those much more performance-relevant decisions of *what* and *when*. As a result, Riptide itself is highly unlikely to be your performance bottleneck.

#### Does Riptide support player-hosted and lobby-based games?

Yes! Riptide gives you the tools to manage connections and send data, but doesn't really impose any restrictions on how you do that. You can set up a dedicated server with full authority, a relay server which just passes on data it receives, a client that also acts as the host (server), or pretty much anything inbetween.

The only architecture that would require some tweaks to Riptide's source is true peer-to-peer, where all clients in a lobby are connected to all other clients in the same lobby.

#### Why am I not receiving any messages?

Make sure that you're calling the server's and/or client's `Update()` method regularly.

#### Why am I getting a warning about no server-side/client-side method handler being found?

If you're getting this warning, make sure you actually have a handler method set up for the message ID mentioned in the warning. If that doesn't help, chances are your handler method has the wrong parameters for what you intended it to be used for.

Remember, server-side handler methods (which handle messages coming from clients) should have two parameters—a `ushort` and a `Message` instance. Client-side handler methods (which handles messages coming from a server) should have only one parameter—a `Message` instance.

#### Do I have to use `Debug.Log()` with Riptide's log system?

No. You can use whatever log method you like, including `Console.WriteLine()`, other engine-specific log methods, and your own custom log methods. All you have to do is pass your chosen log method to `RiptideLogger`'s <code><a href="xref:Riptide.Utils.RiptideLogger.Initialize*">Initialize()</a></code> method.