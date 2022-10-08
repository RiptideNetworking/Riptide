---
uid: Riptide.Transports.Tcp
summary: Provides a low level transport which communicates via TCP sockets.
---

This transport is intended to act as a fallback for situations where Riptide's default [UDP transport](xref:Riptide.Transports.Udp) can't establish a connection. It primarily exists because Apple's app review center [appears to have issues handling UDP traffic](https://developer.apple.com/forums/thread/133938?answerId=617066022#617066022), with UDP-only applications frequently being rejected due to connection issues. Apple's devices *do* support UDP traffic—it's only their app review center that apparently doesn't allow UDP.

To work around this, you can start by trying to connect with the UDP transport, and then automatically try again with the TCP transport if that connection fails. This way your end users should end up connecting via UDP, and TCP will only be used in cases where UDP doesn't work.

> [!Important]
> Due to its very niche purpose, the TCP transport has undergone limited testing and may have more unresolved issues than usual.