<div align="center">
  <img src="https://user-images.githubusercontent.com/51303091/119734159-690afc00-be2f-11eb-9673-c1f998025a3e.png" width="20%" height="auto">
</div>
<div align="center"><a href="https://tomweiland.net/youtube">YouTube</a>&emsp;<b>•</b>&emsp;<a href="https://discord.com/invite/tomweiland">Discord</a></div>
<h1 align="center">RUDP Transport for RiptideNetworking</h1>

This is RiptideNetworking's built-in transport. It uses UDP but has a reliable layer built on top of it, which works by detecting lost packets and resending them until the other end informs the sender that the packet was received.

One major functional difference between TCP and this implementation of RUDP is that unlike TCP, it _does not_ guarantee that packets will arrive in the same order in which they were sent. The vast majority of data sent in multiplayer games doesn't need to be ordered anyways (and if it does, implementing ordering at the application level is rather trivial), and by not guaranteeing order, Riptide's RUDP transport gains an extra speed advantage over TCP.

For more information about the theory/logic behind RUDP, check out [this article](https://gafferongames.com/post/reliability_ordering_and_congestion_avoidance_over_udp/).<br/>
For a list of other available transports, see the [transports](https://github.com/tom-weiland/RiptideNetworking#low-level-transports) section of Riptide's main README.

## Getting Started
For installation and setup instructions, see the [installation](https://github.com/tom-weiland/RiptideNetworking#installation) and [getting started](https://github.com/tom-weiland/RiptideNetworking#getting-started) sections of Riptide's main README.

## Limitations
As this transport relies on UDP, it does not work in web browsers.

## Donate
Riptide is 100% free to use, but if you'd like to financially support its development as well as the development of its various transports, you can do so via [GitHub sponsors](https://github.com/sponsors/tom-weiland) or on [Ko-fi](https://ko-fi.com/tomweiland).