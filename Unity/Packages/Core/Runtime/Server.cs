// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Riptide
{
    /// <summary>A server that can accept connections from <see cref="Client"/>s.</summary>
    public class Server : Peer
    {
        /// <summary>Invoked when a client connects.</summary>
        public event EventHandler<ServerConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a message is received.</summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ServerDisconnectedEventArgs> ClientDisconnected;

        /// <summary>Whether or not the server is currently running.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>The local port that the server is running on.</summary>
        public ushort Port => transport.Port;
        /// <summary>The maximum number of concurrent connections.</summary>
        public ushort MaxClientCount { get; private set; }
        /// <summary>The number of currently connected clients.</summary>
        public int ClientCount => clients.Count;
        /// <summary>An array of all the currently connected clients.</summary>
        /// <remarks>The position of each <see cref="Connection"/> instance in the array does <i>not</i> correspond to that client's numeric ID (except by coincidence).</remarks>
        public Connection[] Clients => clients.Values.ToArray();
        /// <summary>Encapsulates a method that handles a message from a client.</summary>
        /// <param name="fromClientId">The numeric ID of the client from whom the message was received.</param>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(ushort fromClientId, Message message);
        /// <summary>Encapsulates a method that determines whether or not to accept a client's connection attempt.</summary>
        public delegate void ConnectionAttemptHandler(Connection pendingConnection, Message connectMessage);
        /// <summary>An optional method which determines whether or not to accept a client's connection attempt.</summary>
        /// <remarks>The <see cref="Connection"/> parameter is the pending connection and the <see cref="Message"/> parameter is a message containing any additional data the client included with the connection attempt.</remarks>
        public ConnectionAttemptHandler HandleConnection;
        /// <summary>Stores which message IDs have auto relaying enabled. Relaying is disabled entirely when this is <see langword="null"/>.</summary>
        public MessageRelayFilter RelayFilter;

        /// <summary>Currently pending connections which are waiting to be accepted or rejected.</summary>
        private List<Connection> pendingConnections;
        /// <summary>Currently connected clients.</summary>
        private Dictionary<ushort, Connection> clients;
        /// <summary>Clients that have timed out and need to be removed from <see cref="clients"/>.</summary>
        private List<Connection> timedOutClients;
        /// <summary>Methods used to handle messages, accessible by their corresponding message IDs.</summary>
        private Dictionary<ushort, MessageHandler> messageHandlers;
        /// <summary>The underlying transport's server that is used for sending and receiving data.</summary>
        private IServer transport;
        /// <summary>All currently unused client IDs.</summary>
        private Queue<ushort> availableClientIds;

        /// <summary>Handles initial setup.</summary>
        /// <param name="transport">The transport to use for sending and receiving data.</param>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public Server(IServer transport, string logName = "SERVER") : base(logName)
        {
            this.transport = transport;
        }

        /// <summary>Handles initial setup using the built-in UDP transport.</summary>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public Server(string logName = "SERVER") : base(logName)
        {
            transport = new Transports.Udp.UdpServer();
        }

        /// <summary>Stops the server if it's running and swaps out the transport it's using.</summary>
        /// <param name="newTransport">The new underlying transport server to use for sending and receiving data.</param>
        /// <remarks>This method does not automatically restart the server. To continue accepting connections, <see cref="Start(ushort, ushort, byte)"/> must be called again.</remarks>
        public void ChangeTransport(IServer newTransport)
        {
            Stop();
            transport = newTransport;
        }

        /// <summary>Starts the server.</summary>
        /// <param name="port">The local port on which to start the server.</param>
        /// <param name="maxClientCount">The maximum number of concurrent connections to allow.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        public void Start(ushort port, ushort maxClientCount, byte messageHandlerGroupId = 0)
        {
            Stop();

            IncreaseActiveCount();
            CreateMessageHandlersDictionary(messageHandlerGroupId);
            MaxClientCount = maxClientCount;
            pendingConnections = new List<Connection>();
            clients = new Dictionary<ushort, Connection>(maxClientCount);
            timedOutClients = new List<Connection>();
            InitializeClientIds();

            SubToTransportEvents();
            transport.Start(port);

            StartTime();
            Heartbeat();
            IsRunning = true;
            RiptideLogger.Log(LogType.Info, LogName, $"Started on port {port}.");
        }

        /// <summary>Subscribes appropriate methods to the transport's events.</summary>
        private void SubToTransportEvents()
        {
            transport.Connected += HandleConnectionAttempt;
            transport.DataReceived += HandleData;
            transport.Disconnected += TransportDisconnected;
        }

        /// <summary>Unsubscribes methods from all of the transport's events.</summary>
        private void UnsubFromTransportEvents()
        {
            transport.Connected -= HandleConnectionAttempt;
            transport.DataReceived -= HandleData;
            transport.Disconnected -= TransportDisconnected;
        }

        /// <inheritdoc/>
        protected override void CreateMessageHandlersDictionary(byte messageHandlerGroupId)
        {
            MethodInfo[] methods = FindMessageHandlers();

            messageHandlers = new Dictionary<ushort, MessageHandler>(methods.Length);
            foreach (MethodInfo method in methods)
            {
                MessageHandlerAttribute attribute = method.GetCustomAttribute<MessageHandlerAttribute>();
                if (attribute.GroupId != messageHandlerGroupId)
                    continue;

                if (!method.IsStatic)
                    throw new NonStaticHandlerException(method.DeclaringType, method.Name);

                Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), method, false);
                if (serverMessageHandler != null)
                {
                    // It's a message handler for Server instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                    {
                        MethodInfo otherMethodWithId = messageHandlers[attribute.MessageId].GetMethodInfo();
                        throw new DuplicateHandlerException(attribute.MessageId, method, otherMethodWithId);
                    }
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)serverMessageHandler);
                }
                else
                {
                    // It's not a message handler for Server instances, but it might be one for Client instances
                    Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(Client.MessageHandler), method, false);
                    if (clientMessageHandler == null)
                        throw new InvalidHandlerSignatureException(method.DeclaringType, method.Name);
                }
            }
        }

        /// <summary>Handles an incoming connection attempt.</summary>
        private void HandleConnectionAttempt(object _, ConnectedEventArgs e)
        {
            e.Connection.Peer = this;
        }

        /// <summary>Handles a connect message.</summary>
        /// <param name="connection">The client that sent the connect message.</param>
        /// <param name="connectMessage">The connect message.</param>
        private void HandleConnect(Connection connection, Message connectMessage)
        {
            if (HandleConnection == null)
                AcceptConnection(connection);
            else if (!clients.ContainsValue(connection))
            {
                if (!pendingConnections.Contains(connection))
                {
                    if (ClientCount < MaxClientCount)
                    {
                        pendingConnections.Add(connection);
                        Send(Message.Create(MessageHeader.Connect), connection); // Inform the client we've received the connection attempt
                        HandleConnection(connection, connectMessage); // Externally determines whether to accept
                    }
                    else
                        Reject(connection, RejectReason.ServerFull);
                }
                else
                    Reject(connection, RejectReason.Pending);
            }
            else
                Reject(connection, RejectReason.AlreadyConnected);
        }

        /// <summary>Accepts the given pending connection.</summary>
        /// <param name="connection">The connection to accept.</param>
        public void Accept(Connection connection)
        {
            if (pendingConnections.Remove(connection))
                AcceptConnection(connection);
            else
                RiptideLogger.Log(LogType.Warning, LogName, $"Couldn't accept connection from {connection} because no such connection was pending!");
        }

        /// <summary>Rejects the given pending connection.</summary>
        /// <param name="connection">The connection to reject.</param>
        /// <param name="message">Data that should be sent to the client being rejected. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        public void Reject(Connection connection, Message message = null)
        {
            if (pendingConnections.Remove(connection))
                Reject(connection, RejectReason.Rejected, message);
            else
                RiptideLogger.Log(LogType.Warning, LogName, $"Couldn't reject connection from {connection} because no such connection was pending!");
        }

        /// <summary>Accepts the given pending connection.</summary>
        /// <param name="connection">The connection to accept.</param>
        private void AcceptConnection(Connection connection)
        {
            if (!clients.ContainsValue(connection))
            {
                if (ClientCount < MaxClientCount)
                {
                    ushort clientId = GetAvailableClientId();
                    connection.Id = clientId;
                    clients.Add(clientId, connection);
                    connection.ResetTimeout();
                    connection.SendWelcome();
                    return;
                }
                else
                    Reject(connection, RejectReason.ServerFull);
            }
            else
                Reject(connection, RejectReason.AlreadyConnected);
        }

        /// <summary>Rejects the given pending connection.</summary>
        /// <param name="connection">The connection to reject.</param>
        /// <param name="reason">The reason why the connection is being rejected.</param>
        /// <param name="rejectMessage">Data that should be sent to the client being rejected</param>
        private void Reject(Connection connection, RejectReason reason, Message rejectMessage = null)
        {
            if (reason != RejectReason.AlreadyConnected)
            {
                Message message = Message.Create(MessageHeader.Reject);
                if (rejectMessage != null)
                {
                    message.AddByte((byte)RejectReason.Custom);
                    message.AddBytes(rejectMessage.GetBytes(rejectMessage.WrittenLength), false);
                }
                else
                    message.AddByte((byte)reason);

                connection.Send(message);
            }

            // Need to close the connection on the server since we're dealing with a separate Connection instance
            // even if the client connecting already has a connection pending or is already connected
            connection.LocalDisconnect();
            transport.Close(connection);

            string reasonString;
            switch (reason)
            {
                case RejectReason.ServerFull:
                    reasonString = CRServerFull;
                    break;
                case RejectReason.Rejected:
                    reasonString = CRRejected;
                    break;
                case RejectReason.Custom:
                    reasonString = CRCustom;
                    break;
                default:
                    reasonString = UnknownReason;
                    break;
            }
            RiptideLogger.Log(LogType.Info, LogName, $"Rejected connection from {connection}: {reasonString}.");
        }

        /// <summary>Checks if clients have timed out.</summary>
        internal override void Heartbeat()
        {
            foreach (Connection connection in clients.Values)
                if (connection.HasTimedOut)
                    timedOutClients.Add(connection);

            foreach (Connection connection in timedOutClients)
                LocalDisconnect(connection, DisconnectReason.TimedOut);

            timedOutClients.Clear();

            ExecuteLater(HeartbeatInterval, new HeartbeatEvent(this));
        }

        /// <inheritdoc/>
        public override void Update()
        {
            base.Update();
            transport.Poll();
            HandleMessages();
        }

        /// <inheritdoc/>
        protected override void Handle(Message message, MessageHeader header, Connection connection)
        {
            switch (header)
            {
                // User messages
                case MessageHeader.Unreliable:
                case MessageHeader.Reliable:
                    OnMessageReceived(message, connection);
                    break;

                // Internal messages
                case MessageHeader.Ack:
                    connection.HandleAck(message);
                    break;
                case MessageHeader.AckExtra:
                    connection.HandleAckExtra(message);
                    break;
                case MessageHeader.Connect:
                    HandleConnect(connection, message);
                    break;
                case MessageHeader.Heartbeat:
                    connection.HandleHeartbeat(message);
                    break;
                case MessageHeader.Disconnect:
                    LocalDisconnect(connection, DisconnectReason.Disconnected);
                    break;
                case MessageHeader.Welcome:
                    if (connection.IsConnecting)
                    {
                        connection.HandleWelcomeResponse(message);
                        OnClientConnected(connection);
                    }
                    break;
                default:
                    RiptideLogger.Log(LogType.Warning, LogName, $"Unexpected message header '{header}'! Discarding {message.WrittenLength} bytes received from {connection}.");
                    break;
            }

            message.Release();
        }

        /// <summary>Sends a message to a given client.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="toClient">The numeric ID of the client to send the message to.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <remarks><inheritdoc cref="Connection.Send(Message, bool)"/></remarks>
        public void Send(Message message, ushort toClient, bool shouldRelease = true)
        {
            if (clients.TryGetValue(toClient, out Connection connection))
                Send(message, connection, shouldRelease);
        }
        /// <summary>Sends a message to a given client.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="toClient">The client to send the message to.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <remarks><inheritdoc cref="Connection.Send(Message, bool)"/></remarks>
        public void Send(Message message, Connection toClient, bool shouldRelease = true) => toClient.Send(message, shouldRelease);

        /// <summary>Sends a message to all connected clients.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <remarks><inheritdoc cref="Connection.Send(Message, bool)"/></remarks>
        public void SendToAll(Message message, bool shouldRelease = true)
        {
            foreach (Connection client in clients.Values)
                client.Send(message, false);

            if (shouldRelease)
                message.Release();
        }
        /// <summary>Sends a message to all connected clients except the given one.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="exceptToClientId">The numeric ID of the client to <i>not</i> send the message to.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <remarks><inheritdoc cref="Connection.Send(Message, bool)"/></remarks>
        public void SendToAll(Message message, ushort exceptToClientId, bool shouldRelease = true)
        {
            foreach (Connection client in clients.Values)
                if (client.Id != exceptToClientId)
                    client.Send(message, false);

            if (shouldRelease)
                message.Release();
        }

        /// <summary>Retrieves the client with the given ID, if a client with that ID is currently connected.</summary>
        /// <param name="id">The ID of the client to retrieve.</param>
        /// <param name="client">The retrieved client.</param>
        /// <returns><see langword="true"/> if a client with the given ID was connected; otherwise <see langword="false"/>.</returns>
        public bool TryGetClient(ushort id, out Connection client) => clients.TryGetValue(id, out client);

        /// <summary>Disconnects a specific client.</summary>
        /// <param name="id">The numeric ID of the client to disconnect.</param>
        /// <param name="message">Data that should be sent to the client being disconnected. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        public void DisconnectClient(ushort id, Message message = null)
        {
            if (clients.TryGetValue(id, out Connection client))
            {
                SendDisconnect(client, DisconnectReason.Kicked, message);
                LocalDisconnect(client, DisconnectReason.Kicked);
            }
            else
                RiptideLogger.Log(LogType.Warning, LogName, $"Couldn't disconnect client {id} because it wasn't connected!");
        }

        /// <summary>Disconnects the given client.</summary>
        /// <param name="client">The client to disconnect.</param>
        /// <param name="message">Data that should be sent to the client being disconnected. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        public void DisconnectClient(Connection client, Message message = null)
        {
            if (clients.ContainsKey(client.Id))
            {
                SendDisconnect(client, DisconnectReason.Kicked, message);
                LocalDisconnect(client, DisconnectReason.Kicked);
            }
            else
                RiptideLogger.Log(LogType.Warning, LogName, $"Couldn't disconnect client {client.Id} because it wasn't connected!");
        }

        /// <summary>Cleans up the local side of the given connection.</summary>
        /// <param name="client">The client to disconnect.</param>
        /// <param name="reason">The reason why the client is being disconnected.</param>
        private void LocalDisconnect(Connection client, DisconnectReason reason)
        {
            if (client.Peer != this)
                return; // Client does not belong to this Server instance

            transport.Close(client);

            if (clients.Remove(client.Id))
                availableClientIds.Enqueue(client.Id);

            if (client.IsConnected)
                OnClientDisconnected(client, reason); // Only run if the client was ever actually connected

            client.LocalDisconnect();
        }

        /// <summary>What to do when the transport disconnects a client.</summary>
        private void TransportDisconnected(object sender, Transports.DisconnectedEventArgs e)
        {
            LocalDisconnect(e.Connection, e.Reason);
        }

        /// <summary>Stops the server.</summary>
        public void Stop()
        {
            if (!IsRunning)
                return;

            pendingConnections.Clear(); 
            byte[] disconnectBytes = { (byte)MessageHeader.Disconnect, (byte)DisconnectReason.ServerStopped };
            foreach (Connection client in clients.Values)
                client.Send(disconnectBytes, disconnectBytes.Length);
            clients.Clear();

            transport.Shutdown();
            UnsubFromTransportEvents();

            DecreaseActiveCount();

            StopTime();
            IsRunning = false;
            RiptideLogger.Log(LogType.Info, LogName, "Server stopped.");
        }

        /// <summary>Initializes available client IDs.</summary>
        private void InitializeClientIds()
        {
            availableClientIds = new Queue<ushort>(MaxClientCount);
            for (ushort i = 1; i <= MaxClientCount; i++)
                availableClientIds.Enqueue(i);
        }

        /// <summary>Retrieves an available client ID.</summary>
        /// <returns>The client ID. 0 if none were available.</returns>
        private ushort GetAvailableClientId()
        {
            if (availableClientIds.Count > 0)
                return availableClientIds.Dequeue();
            
            RiptideLogger.Log(LogType.Error, LogName, "No available client IDs, assigned 0!");
            return 0;
        }

        #region Messages
        /// <summary>Sends a disconnect message.</summary>
        /// <param name="client">The client to send the disconnect message to.</param>
        /// <param name="reason">Why the client is being disconnected.</param>
        /// <param name="disconnectMessage">Optional custom data that should be sent to the client being disconnected.</param>
        private void SendDisconnect(Connection client, DisconnectReason reason, Message disconnectMessage)
        {
            Message message = Message.Create(MessageHeader.Disconnect);
            message.AddByte((byte)reason);

            if (reason == DisconnectReason.Kicked && disconnectMessage != null)
                message.AddBytes(disconnectMessage.GetBytes(disconnectMessage.WrittenLength), false);

            Send(message, client);
        }

        /// <summary>Sends a client connected message.</summary>
        /// <param name="newClient">The newly connected client.</param>
        private void SendClientConnected(Connection newClient)
        {
            Message message = Message.Create(MessageHeader.ClientConnected);
            message.AddUShort(newClient.Id);

            SendToAll(message, newClient.Id);
        }

        /// <summary>Sends a client disconnected message.</summary>
        /// <param name="id">The numeric ID of the client that disconnected.</param>
        private void SendClientDisconnected(ushort id)
        {
            Message message = Message.Create(MessageHeader.ClientDisconnected);
            message.AddUShort(id);

            SendToAll(message);
        }
        #endregion

        #region Events
        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        /// <param name="client">The newly connected client.</param>
        protected virtual void OnClientConnected(Connection client)
        {
            RiptideLogger.Log(LogType.Info, LogName, $"Client {client.Id} ({client}) connected successfully!");
            SendClientConnected(client);
            ClientConnected?.Invoke(this, new ServerConnectedEventArgs(client));
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        /// <param name="message">The received message.</param>
        /// <param name="fromConnection">The client from which the message was received.</param>
        protected virtual void OnMessageReceived(Message message, Connection fromConnection)
        {
            ushort messageId = message.GetUShort();
            if (RelayFilter != null && RelayFilter.ShouldRelay(messageId))
            {
                // The message should be automatically relayed to clients instead of being handled on the server
                SendToAll(message, fromConnection.Id);
                return;
            }

            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(fromConnection, messageId, message));

            if (messageHandlers.TryGetValue(messageId, out MessageHandler messageHandler))
                messageHandler(fromConnection.Id, message);
            else
                RiptideLogger.Log(LogType.Warning, LogName, $"No message handler method found for message ID {messageId}!");
        }

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        /// <param name="connection">The client that disconnected.</param>
        /// <param name="reason">The reason for the disconnection.</param>
        protected virtual void OnClientDisconnected(Connection connection, DisconnectReason reason)
        {
            SendClientDisconnected(connection.Id);

            string reasonString;
            switch (reason)
            {
                case DisconnectReason.NeverConnected:
                    reasonString = DCNeverConnected;
                    break;
                case DisconnectReason.TransportError:
                    reasonString = DCTransportError;
                    break;
                case DisconnectReason.TimedOut:
                    reasonString = DCTimedOut;
                    break;
                case DisconnectReason.Kicked:
                    reasonString = DCKicked;
                    break;
                case DisconnectReason.ServerStopped:
                    reasonString = DCServerStopped;
                    break;
                case DisconnectReason.Disconnected:
                    reasonString = DCDisconnected;
                    break;
                default:
                    reasonString = UnknownReason;
                    break;
            }

            RiptideLogger.Log(LogType.Info, LogName, $"Client {connection.Id} ({connection}) disconnected: {reasonString}.");
            ClientDisconnected?.Invoke(this, new ServerDisconnectedEventArgs(connection, reason));
        }
        #endregion
    }
}
