// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Riptide.Transports
{
    public class ConnectingEventArgs
    {
        public readonly Connection Connection;

        public ConnectingEventArgs(Connection connection)
        {
            Connection = connection;
        }
    }
    
    public class ConnectedEventArgs
    {
        public readonly Connection Connection;

        public ConnectedEventArgs(Connection connection)
        {
            Connection = connection;
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

    public class DisconnectedEventArgs
    {
        public readonly Connection Connection;
        public readonly DisconnectReason Reason;

        public DisconnectedEventArgs(Connection connection, DisconnectReason reason)
        {
            Connection = connection;
            Reason = reason;
        }
    }
}
