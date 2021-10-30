using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking.Transports
{
    public interface IServer
    {
        /// <summary>The local port that the server is running on.</summary>
        ushort Port { get; }
        /// <summary>The maximum number of clients that can be connected at any time.</summary>
        ushort MaxClientCount { get; }
        /// <summary>The number of currently connected clients.</summary>
        int ClientCount { get; }
        /// <summary>An array of all the currently connected clients.</summary>
        /// <remarks>The position of each <see cref="IServerClient"/> instance in the array does NOT necessarily correspond to that client's numeric ID.</remarks>
        IServerClient[] Clients { get; }
        /// <summary>Whether or not to output informational log messages. Error-related log messages ignore this setting.</summary>
        bool ShouldOutputInfoLogs { get; set; }

        /// <summary>Starts the server.</summary>
        /// <param name="port">The local port on which to start the server.</param>
        /// <param name="maxClientCount">The maximum number of concurrent connections to allow.</param>
        void Start(ushort port, ushort maxClientCount);
        /// <summary>Sends a message to a specific client.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="toClientId">The client to send the message to.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        void Send(Message message, ushort toClientId, byte maxSendAttempts, bool shouldRelease);
        /// <summary>Sends a message to all conected clients.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        void SendToAll(Message message, byte maxSendAttempts, bool shouldRelease);
        /// <summary>Sends a message to all connected clients except one.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="exceptToClientId">The client NOT to send the message to.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        void SendToAll(Message message, ushort exceptToClientId, byte maxSendAttempts, bool shouldRelease);
        /// <summary>Kicks a specific client.</summary>
        /// <param name="clientId">The client to kick.</param>
        void DisconnectClient(ushort clientId);
        void Shutdown();

        /// <summary>Invoked when a new client connects.</summary>
        event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a message is received from a client.</summary>
        event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when a client disconnects.</summary>
        event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
    }
}
