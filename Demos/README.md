<div align="center">
  <img src="https://user-images.githubusercontent.com/51303091/119734159-690afc00-be2f-11eb-9673-c1f998025a3e.png" width="20%" height="auto">
</div>
<div align="center"><a href="https://tomweiland.net/youtube">YouTube</a>&emsp;<b>•</b>&emsp;<a href="https://discord.com/invite/tomweiland">Discord</a></div>
<h1 align="center">Demos</h1>

**Note:** the [Unity demo](Unity) projects are *not* built to work interchangeably with the [console demo](Console) project. As is, they serve different purposes, and if you try to connect the [Unity client](Unity/DedicatedServerDemo/Client/) to the [console server](Console/ConsoleServer/) (or vice versa), you will get unexpected behavior and/or errors.

You *can* build a client in Unity and have it connect to a console server just fine, it's just that these demos aren't set up to work that way.

## Unity

Unity Version: 2020.3.25f1

### Dedicated Server Demo

The dedicated server demo shows how to set up connection, disconnection, and *very* basic server-authoritative player movement in Unity. Using two separate projects is not necessary, but helps visualize the separation between server and client.

### Player-Hosted Demo

The player-hosted demo shows how to set up a single project which allows players to host a game or join someone else's. Player movement is client-authoritative in this demo—the host's server logic doesn't perform any validation on the player positions it receives and exists simply to relay the data to all other clients. This means that players could easily cheat, but that generally isn't a concern in player-hosted (often cooperative, non-competitive) games.

Note that this setup requires you to enter the host's IP and port, which means that players will need to know the IP of the player they're trying to connect to. The host player will need to portforward if they wish to allow others who aren't on the same LAN to join. One way to circumvent the need to portforward is by using a relay server. If you intend to release on Steam, you can even take advantage of their free relay servers by using the [Steam transport](https://github.com/tom-weiland/RiptideSteamTransport).

## Console

Server-client setup using .NET Core console apps. Once connected, clients send several thousand test messages to the server, which—in the case of a two-way test—sends them back as soon as they're received. The tests take around 40 seconds to complete. Note that this demo project has not been extensively tested with more than one client connected at any given time.