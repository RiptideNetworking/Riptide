using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    public class ServerClientConnectedEventArgs : EventArgs
    {
        public ServerClient Client { get; private set; }

        public ServerClientConnectedEventArgs(ServerClient client)
        {
            Client = client;
        }
    }

    public class ClientConnectedEventArgs : EventArgs
    {
        public ushort Id { get; private set; }

        public ClientConnectedEventArgs(ushort id)
        {
            Id = id;
        }
    }

    public class ClientDisconnectedEventArgs : EventArgs
    {
        public ushort Id { get; private set; }

        public ClientDisconnectedEventArgs(ushort id)
        {
            Id = id;
        }
    }

    public class PingUpdatedEventArgs : EventArgs
    {
        public ushort RTT { get; private set; }
        public ushort SmoothRTT { get; private set; }

        public PingUpdatedEventArgs(ushort RTT, ushort smoothRTT)
        {
            this.RTT = RTT;
            SmoothRTT = smoothRTT;
        }
    }
}
