// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

namespace Riptide.Transports
{
    /// <summary>Contains event data for when a server's transport successfully establishes a connection to a client.</summary>
    public class ConnectedEventArgs
    {
        /// <summary>The newly established connection.</summary>
        public readonly Connection Connection;

        /// <summary>Initializes event data.</summary>
        /// <param name="connection">The newly established connection.</param>
        public ConnectedEventArgs(Connection connection)
        {
            Connection = connection;
        }
    }

    /// <summary>Contains event data for when a server's or client's transport receives data.</summary>
    public class DataReceivedEventArgs
    {
        /// <summary>An array containing the received data.</summary>
        public readonly byte[] DataBuffer;
        /// <summary>The number of bytes that were received.</summary>
        public readonly int Amount;
        /// <summary>The connection which the data was received from.</summary>
        public readonly Connection FromConnection;

        /// <summary>Initializes event data.</summary>
        /// <param name="dataBuffer">An array containing the received data.</param>
        /// <param name="amount">The number of bytes that were received.</param>
        /// <param name="fromConnection">The connection which the data was received from.</param>
        public DataReceivedEventArgs(byte[] dataBuffer, int amount, Connection fromConnection)
        {
            DataBuffer = dataBuffer;
            Amount = amount;
            FromConnection = fromConnection;
        }
    }

    /// <summary>Contains event data for when a server's or client's transport initiates or detects a disconnection.</summary>
    public class DisconnectedEventArgs
    {
        /// <summary>The closed connection.</summary>
        public readonly Connection Connection;
        /// <summary>The reason for the disconnection.</summary>
        public readonly DisconnectReason Reason;

        /// <summary>Initializes event data.</summary>
        /// <param name="connection">The closed connection.</param>
        /// <param name="reason">The reason for the disconnection.</param>
        public DisconnectedEventArgs(Connection connection, DisconnectReason reason)
        {
            Connection = connection;
            Reason = reason;
        }
    }
}
