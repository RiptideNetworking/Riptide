<div align="center">
  <img src="https://user-images.githubusercontent.com/51303091/119734159-690afc00-be2f-11eb-9673-c1f998025a3e.png" width="20%" height="auto">
</div>
<div align="center"><a href="https://tomweiland.net/youtube">YouTube</a>&emsp;<b>•</b>&emsp;<a href="https://discord.com/invite/tomweiland">Discord</a></div>
<h1 align="center">Demos</h1>

**Note:** the Unity demo projects are _not_ designed to work interchangeably with the console demo projects. As is, they serve different purposes, and if you try to connect the Unity client to the console server (or vice versa), you will get unexpected behavior and/or errors.

## Unity
Example projects that show how to set up connection, disconnection, and *very* basic server-authoritative player movement in Unity.<br/>
Unity Version: 2020.3.12f1

## Console
Server-client setup using .NET Core console apps. Once connected, clients send several thousand test messages to the server, which—in the case of a two-way test—sends them back as soon as they're received. The tests take around 40 seconds to complete. Note that this demo project has not been extensively tested with more than one client connected at any given time.