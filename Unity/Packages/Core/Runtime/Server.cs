// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

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
        public event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when a message is received.</summary>
        public event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

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
        /// <summary>Whether or not to allow messages to be automatically sent to all other connected clients.</summary>
        /// <remarks>This should never be enabled if you want to maintain server authority, as it theoretically allows hacked clients to tell your <see cref="Server"/> instance to automatically distribute any message to other clients. However, it's extremely handy when building client-authoritative games where the <see cref="Server"/> instance acts mostly as a relay and is directly forwarding most messages to other clients anyways.</remarks>
        public bool AllowAutoMessageRelay { get; set; }
        /// <summary>Encapsulates a method that handles a message from a client.</summary>
        /// <param name="fromClientId">The numeric ID of the client from whom the message was received.</param>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(ushort fromClientId, Message message);

        /// <summary>Currently connected clients.</summary>
        private Dictionary<ushort, Connection> clients;
        /// <summary>Clients that have timed out and need to be removed from <see cref="clients"/>.</summary>
        private List<Connection> timedOutClients;
        /// <summary>Methods used to handle messages, accessible by their corresponding message IDs.</summary>
        private Dictionary<ushort, MessageHandler> messageHandlers;
        /// <summary>The underlying transport's server that is used for sending and receiving data.</summary>
        private IServer transport;
        /// <summary>All currently unused client IDs.</summary>
        private List<ushort> availableClientIds;

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
            clients = new Dictionary<ushort, Connection>(maxClientCount);
            timedOutClients = new List<Connection>(maxClientCount);
            InitializeClientIds();

            SubToTransportEvents();
            transport.Start(port);

            StartHeartbeat();
            IsRunning = true;
            RiptideLogger.Log(LogType.info, LogName, $"Started on port {port}.");
        }

        /// <summary>Subscribes appropriate methods to the transport's events.</summary>
        private void SubToTransportEvents()
        {
            transport.Connecting += HandleConnectionAttempt;
            transport.Connected += TransportConnected;
            transport.DataReceived += HandleData;
            transport.Disconnected += TransportDisconnected;
        }

        /// <summary>Unsubscribes methods from all of the transport's events.</summary>
        private void UnsubFromTransportEvents()
        {
            transport.Connecting -= HandleConnectionAttempt;
            transport.Connected -= TransportConnected;
            transport.DataReceived -= HandleData;
            transport.Disconnected -= TransportDisconnected;
        }

        /// <inheritdoc/>
        protected override void CreateMessageHandlersDictionary(byte messageHandlerGroupId)
        {
            MethodInfo[] methods = FindMessageHandlers();

            messageHandlers = new Dictionary<ushort, MessageHandler>(methods.Length);
            for (int i = 0; i < methods.Length; i++)
            {
                MessageHandlerAttribute attribute = methods[i].GetCustomAttribute<MessageHandlerAttribute>();
                if (attribute.GroupId != messageHandlerGroupId)
                    continue;

                if (!methods[i].IsStatic)
                    throw new NonStaticHandlerException(methods[i].DeclaringType, methods[i].Name);

                Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (serverMessageHandler != null)
                {
                    // It's a message handler for Server instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                    {
                        MethodInfo otherMethodWithId = messageHandlers[attribute.MessageId].GetMethodInfo();
                        throw new DuplicateHandlerException(attribute.MessageId, methods[i], otherMethodWithId);
                    }
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)serverMessageHandler);
                }
                else
                {
                    // It's not a message handler for Server instances, but it might be one for Client instances
                    Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(Client.MessageHandler), methods[i], false);
                    if (clientMessageHandler == null)
                        throw new InvalidHandlerSignatureException(methods[i].DeclaringType, methods[i].Name);
                }
            }
        }

        /// <summary>Handles an incoming connection attempt.</summary>
        private void HandleConnectionAttempt(object sender, ConnectingEventArgs e)
        {
            if (ClientCount < MaxClientCount)
            {
                if (!clients.ContainsValue(e.Connection))
                {
                    ushort clientId = GetAvailableClientId();
                    e.Connection.Id = clientId;
                    e.Connection.Peer = this;
                    transport.Accept(e.Connection);
                    return;
                }
                else
                    RiptideLogger.Log(LogType.warning, LogName, $"{e.Connection} is already connected, rejecting connection!");
            }
            else
                RiptideLogger.Log(LogType.info, LogName, $"Server is full! Rejecting connection from {e.Connection}.");

            e.Connection.LocalDisconnect();
            transport.Reject(e.Connection);
        }

        /// <summary>What to do when the transport establishes a connection.</summary>
        private void TransportConnected(object sender, ConnectedEventArgs e)
        {
            e.Connection.SendWelcome();
        }

        /// <summary>Checks if clients have timed out.</summary>
        protected override void Heartbeat()
        {
            foreach (Connection connection in clients.Values)
                if (connection.HasTimedOut)
                    timedOutClients.Add(connection);

            foreach (Connection connection in timedOutClients)
                LocalDisconnect(connection, DisconnectReason.timedOut);

            timedOutClients.Clear();
        }

        /// <summary>Polls the transport for received messages and then handles them.</summary>
        public override void Tick()
        {
            base.Tick();
            transport.Tick();
            HandleMessages();
        }

        /// <inheritdoc/>
        protected override void Handle(Message message, HeaderType messageHeader, Connection connection)
        {
            switch (messageHeader)
            {
                // User messages
                case HeaderType.unreliable:
                case HeaderType.reliable:
                    OnMessageReceived(message, connection);
                    break;
                case HeaderType.unreliableAutoRelay:
                case HeaderType.reliableAutoRelay:
                    if (AllowAutoMessageRelay)
                        SendToAll(message, connection.Id);
                    else
                        OnMessageReceived(message, connection);
                    break;

                // Internal messages
                case HeaderType.ack:
                    connection.HandleAck(message);
                    break;
                case HeaderType.ackExtra:
                    connection.HandleAckExtra(message);
                    break;
                case HeaderType.connect:
                    // Handled by transport, if at all
                    break;
                case HeaderType.heartbeat:
                    connection.HandleHeartbeat(message);
                    break;
                case HeaderType.welcome:
                    if (connection.IsConnecting)
                    {
                        connection.HandleWelcomeResponse(message);
                        clients.Add(connection.Id, connection);
                        OnClientConnected(connection, message);
                    }
                    break;
                case HeaderType.clientConnected:
                case HeaderType.clientDisconnected:
                    break;
                case HeaderType.disconnect:
                    LocalDisconnect(connection, DisconnectReason.disconnected);
                    break;
                default:
                    RiptideLogger.Log(LogType.warning, LogName, $"Unknown message header type '{messageHeader}'! Discarding {message.WrittenLength} bytes received from {this}.");
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
        /// <param name="customMessage">An optional custom message (if any) to inform the client why it was disconnected.</param>
        public void DisconnectClient(ushort id, string customMessage = "")
        {
            if (clients.TryGetValue(id, out Connection client))
            {
                SendDisconnect(client, DisconnectReason.kicked, customMessage);
                LocalDisconnect(client, DisconnectReason.kicked, customMessage);
            }
            else
                RiptideLogger.Log(LogType.warning, LogName, $"Couldn't disconnect client {id} because it wasn't connected!");
        }

        /// <summary>Disconnects the given client.</summary>
        /// <param name="client">The client to disconnect.</param>
        /// <param name="customMessage">An optional custom message (if any) to inform the client why it was disconnected.</param>
        public void DisconnectClient(Connection client, string customMessage = "")
        {
            if (clients.ContainsKey(client.Id))
            {
                SendDisconnect(client, DisconnectReason.kicked, customMessage);
                LocalDisconnect(client, DisconnectReason.kicked, customMessage);
            }
            else
                RiptideLogger.Log(LogType.warning, LogName, $"Couldn't disconnect client {client.Id} because it wasn't connected!");
        }

        /// <summary>Cleans up the local side of the given connection.</summary>
        /// <param name="client">The client to disconnect.</param>
        /// <param name="reason">The reason why the client is being disconnected.</param>
        /// <param name="customMessage">An optional custom message to display for the disconnection reason. Only used when <paramref name="reason"/> is set to <see cref="DisconnectReason.kicked"/>.</param>
        private void LocalDisconnect(Connection client, DisconnectReason reason, string customMessage = "")
        {
            transport.Close(client);
            client.LocalDisconnect();
            if (clients.Remove(client.Id))
            {
                OnClientDisconnected(client.Id);
                availableClientIds.Add(client.Id);

                string reasonString;
                switch (reason)
                {
                    case DisconnectReason.neverConnected:
                        reasonString = ReasonNeverConnected;
                        break;
                    case DisconnectReason.transportError:
                        reasonString = ReasonTransportError;
                        break;
                    case DisconnectReason.timedOut:
                        reasonString = ReasonTimedOut;
                        break;
                    case DisconnectReason.kicked:
                        reasonString = string.IsNullOrEmpty(customMessage) ? ReasonKicked : customMessage;
                        break;
                    case DisconnectReason.serverStopped:
                        reasonString = ReasonServerStopped;
                        break;
                    case DisconnectReason.disconnected:
                        reasonString = ReasonDisconnected;
                        break;
                    default:
                        reasonString = ReasonUnknown;
                        break;
                }
            
                RiptideLogger.Log(LogType.info, LogName, $"Client {client.Id} ({client}) disconnected: {reasonString}.");
            }
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

            byte[] disconnectBytes = { (byte)HeaderType.disconnect, (byte)DisconnectReason.serverStopped };
            foreach (Connection client in clients.Values)
                client.Send(disconnectBytes, disconnectBytes.Length);
            clients.Clear();

            transport.Shutdown();
            UnsubFromTransportEvents();

            DecreaseActiveCount();

            StopHeartbeat();
            IsRunning = false;
            RiptideLogger.Log(LogType.info, LogName, "Server stopped.");
        }

        /// <summary>Initializes available client IDs.</summary>
        private void InitializeClientIds()
        {
            availableClientIds = new List<ushort>(MaxClientCount);
            for (ushort i = 1; i <= MaxClientCount; i++)
                availableClientIds.Add(i);
        }

        /// <summary>Retrieves an available client ID.</summary>
        /// <returns>The client ID. 0 if none were available.</returns>
        private ushort GetAvailableClientId()
        {
            if (availableClientIds.Count > 0)
            {
                ushort id = availableClientIds[0];
                availableClientIds.RemoveAt(0);
                return id;
            }
            else
            {
                RiptideLogger.Log(LogType.error, LogName, "No available client IDs, assigned 0!");
                return 0;
            }
        }

        #region Messages
        /// <summary>Sends a disconnect message.</summary>
        /// <param name="client">The client to send the disconnect message to.</param>
        /// <param name="reason">Why the client is being disconnected.</param>
        /// <param name="customMessage">A custom message which is used to inform clients why they were disconnected.</param>
        private void SendDisconnect(Connection client, DisconnectReason reason, string customMessage)
        {
            Message message = Message.Create(HeaderType.disconnect);
            message.AddByte((byte)reason);
            if (reason == DisconnectReason.kicked && !string.IsNullOrEmpty(customMessage))
                message.AddString(customMessage);

            Send(message, client);
        }

        /// <summary>Sends a client connected message.</summary>
        /// <param name="newClient">The newly connected client.</param>
        private void SendClientConnected(Connection newClient)
        {
            Message message = Message.Create(HeaderType.clientConnected, 25);
            message.AddUShort(newClient.Id);

            SendToAll(message, newClient.Id);
        }

        /// <summary>Sends a client disconnected message.</summary>
        /// <param name="id">The numeric ID of the client that disconnected.</param>
        private void SendClientDisconnected(ushort id)
        {
            Message message = Message.Create(HeaderType.clientDisconnected, 25);
            message.AddUShort(id);

            SendToAll(message);
        }
        #endregion

        #region Events
        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        /// <param name="client">The newly connected client.</param>
        /// <param name="connectMessage">A message containing any custom data the client included when it connected.</param>
        protected virtual void OnClientConnected(Connection client, Message connectMessage)
        {
            RiptideLogger.Log(LogType.info, LogName, $"Client {client.Id} ({client}) connected successfully!");
            SendClientConnected(client);
            ClientConnected?.Invoke(this, new ServerClientConnectedEventArgs(client, connectMessage));
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        /// <param name="message">The received message.</param>
        /// <param name="fromConnection">The client from which the message was received.</param>
        protected virtual void OnMessageReceived(Message message, Connection fromConnection)
        {
            ushort messageId = message.GetUShort();
            MessageReceived?.Invoke(this, new ServerMessageReceivedEventArgs(fromConnection, messageId, message));

            if (messageHandlers.TryGetValue(messageId, out MessageHandler messageHandler))
                messageHandler(fromConnection.Id, message);
            else
                RiptideLogger.Log(LogType.warning, $"No server message handler method found for message ID {messageId}!");
        }

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        /// <param name="clientId">The numeric ID of the client that disconnected.</param>
        protected virtual void OnClientDisconnected(ushort clientId)
        {
            SendClientDisconnected(clientId);
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientId));
        }
        #endregion
    }
}
