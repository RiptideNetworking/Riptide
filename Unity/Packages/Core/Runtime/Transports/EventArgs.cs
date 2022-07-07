// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Riptide.Transports
{
    public class ClientConnectingEventArgs
    {
        public readonly Connection NewConnection;

        public ClientConnectingEventArgs(Connection newConnection)
        {
            NewConnection = newConnection;
        }
    }
    
    public class ClientConnectedEventArgs
    {
        public readonly Connection NewConnection;

        public ClientConnectedEventArgs(Connection newConnection)
        {
            NewConnection = newConnection;
        }
    }

    public class DataReceivedEventArgs
    {
        public readonly byte[] DataBuffer;
        public readonly int Amount;
        public readonly Connection FromConnection;

        public DataReceivedEventArgs(byte[] dataBuffer, int amount, Connection fromConnection)
        {
            DataBuffer = dataBuffer;
            Amount = amount;
            FromConnection = fromConnection;
        }
    }

    public class ClientDisconnectedEventArgs
    {
        public readonly Connection ClosedConnection;
        public readonly DisconnectReason Reason;

        public ClientDisconnectedEventArgs(Connection closedConnection, DisconnectReason reason)
        {
            ClosedConnection = closedConnection;
            Reason = reason;
        }
    }
}
