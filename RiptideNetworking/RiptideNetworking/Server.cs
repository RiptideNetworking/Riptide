using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace RiptideNetworking
{
    /// <summary>Represents a server which can accept connections from clients.</summary>
    public class Server : RudpSocket
    {
        /// <summary>Whether or not the server is currently running.</summary>
        public bool IsRunning { get; private set; }
        /// <summary>The local port that the server is running on.</summary>
        public ushort Port { get; private set; }
        /// <summary>An array of all the currently connected clients.</summary>
        public ServerClient[] Clients => clients.Values.ToArray();
        /// <summary>The maximum number of clients that can be connected at any time.</summary>
        public ushort MaxClientCount { get; private set; }
        /// <summary>The number of currently connected clients.</summary>
        public int ClientCount => clients.Count;
        /// <summary>The time (in milliseconds) after which to disconnect a client without a heartbeat.</summary>
        public ushort ClientTimeoutTime { get; set; } = 5000;
        private ushort _clientHeartbeatInterval;
        /// <summary>The interval (in milliseconds) at which heartbeats are to be expected from clients.</summary>
        public ushort ClientHeartbeatInterval
        {
            get => _clientHeartbeatInterval;
            set
            {
                _clientHeartbeatInterval = value;
                if (heartbeatTimer != null)
                    heartbeatTimer.Change(0, value);
            }
        }

        /// <summary>Currently connected clients, accessible by their IPEndPoint.</summary>
        private Dictionary<IPEndPoint, ServerClient> clients;
        /// <summary>The action queue to use when triggering events. Null if events should be triggered immediately.</summary>
        private ActionQueue receiveActionQueue;
        /// <summary>All currently unused client IDs.</summary>
        private List<ushort> availableClientIds;
        /// <summary>The timer responsible for sending regular heartbeats.</summary>
        private Timer heartbeatTimer;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name to use when logging messages via RiptideLogger.</param>
        public Server(string logName = "SERVER") : base(logName) { }

        /// <summary>Starts the server.</summary>
        /// <param name="port">The local port on which to start the server.</param>
        /// <param name="maxClientCount">The maximum number of concurrent connections to allow.</param>
        /// <param name="receiveActionQueue">The action queue to add messages to. Passing null will cause messages to be handled immediately on the same thread on which they were received.</param>
        /// <param name="clientHeartbeatInterval">The interval (in milliseconds) at which heartbeats are to be expected from clients.</param>
        public void Start(ushort port, ushort maxClientCount, ActionQueue receiveActionQueue = null, ushort clientHeartbeatInterval = 1000)
        {
            Port = port;
            MaxClientCount = maxClientCount;
            clients = new Dictionary<IPEndPoint, ServerClient>(MaxClientCount);

            InitializeClientIds();

            this.receiveActionQueue = receiveActionQueue;
            _clientHeartbeatInterval = clientHeartbeatInterval;

            StartListening(port);

            RiptideLogger.Log(LogName, $"Started on port {port}.");
            heartbeatTimer = new Timer(Heartbeat, null, 0, ClientHeartbeatInterval);
            IsRunning = true;
        }

        /// <summary>Checks if clients have timed out. Called by the heartbeat timer.</summary>
        private void Heartbeat(object state)
        {
            lock (clients)
            {
                foreach (ServerClient client in clients.Values)
                {
                    if (client.HasTimedOut)
                        HandleDisconnect(client.remoteEndPoint); // Disconnect the client
                }
            }
        }

        /// <summary>Determines whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="firstByte">The first byte of the message.</param>
        protected override bool ShouldHandleMessageFrom(IPEndPoint endPoint, byte firstByte)
        {
            lock (clients)
            {
                if (clients.ContainsKey(endPoint))
                {
                    // Client is already connected
                    if ((HeaderType)firstByte != HeaderType.connect) // It's not a connect message, so handle it
                        return true;
                }
                else if (clients.Count < MaxClientCount)
                {
                    // Client is not yet connected and the server has capacity
                    if ((HeaderType)firstByte == HeaderType.connect) // It's a connect message, which doesn't need to be handled like other messages
                        clients.Add(endPoint, new ServerClient(this, endPoint, GetAvailableClientId()));
                }
                return false;
            }
        }

        /// <summary>Handles the given data.</summary>
        /// <param name="data">The data to handle.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        /// <param name="headerType">The header type of the data.</param>
        internal override void Handle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType)
        {
#if DETAILED_LOGGING
            if (headerType != HeaderType.reliable && headerType != HeaderType.unreliable)
                RiptideLogger.Log(LogName, $"Received {headerType} message from {fromEndPoint}."); 
#endif
            switch (headerType)
            {
                // User messages
                case HeaderType.unreliable:
                case HeaderType.reliable:
                    if (receiveActionQueue == null)
                    {
                        Message message = Message.Create(headerType, data);
#if DETAILED_LOGGING
                        ushort messageId = message.PeekUShort();
                        if (headerType == HeaderType.reliable)
                            RiptideLogger.Log(LogName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                        else if (headerType == HeaderType.unreliable)
                            RiptideLogger.Log(LogName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif
                        OnMessageReceived(new ServerMessageReceivedEventArgs(clients[fromEndPoint], message));
                    }
                    else
                    {
                        receiveActionQueue.Add(() =>
                        {
                            Message message = Message.Create(headerType, data);
#if DETAILED_LOGGING
                            ushort messageId = message.PeekUShort();
                            if (headerType == HeaderType.reliable)
                                RiptideLogger.Log(LogName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                            else if (headerType == HeaderType.unreliable)
                                RiptideLogger.Log(LogName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif
                            if (clients.TryGetValue(fromEndPoint, out ServerClient client))
                                OnMessageReceived(new ServerMessageReceivedEventArgs(client, message));
#if DETAILED_LOGGING
                            else
                                RiptideLogger.Log(LogName, $"Aborted handling of message (ID: {messageId}) because client is no longer connected.");
#endif
                        });
                    }
                    break;

                // Internal messages
                case HeaderType.ack:
                    clients[fromEndPoint].HandleAck(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.ackExtra:
                    clients[fromEndPoint].HandleAckExtra(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.connect:
                    // Handled in ShouldHandleMessageFrom method
                    break;
                case HeaderType.heartbeat:
                    clients[fromEndPoint].HandleHeartbeat(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.welcome:
                    clients[fromEndPoint].HandleWelcomeReceived(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.clientConnected:
                case HeaderType.clientDisconnected:
                    break;
                case HeaderType.disconnect:
                    HandleDisconnect(fromEndPoint);
                    break;
                default:
                    RiptideLogger.Log($"Unknown message header type '{headerType}'! Discarding {data.Length} bytes.");
                    return;
            }
        }

        /// <summary>Handles the given reliably sent data.</summary>
        /// <param name="data">The reliably sent data.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        /// <param name="headerType">The header type of the data.</param>
        internal override void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            ReliableHandle(data, fromEndPoint, headerType, clients[fromEndPoint].SendLockables);
        }

        /// <summary>Sends an acknowledgement for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        /// <param name="toEndPoint">The endpoint to send the acknowledgement to.</param>
        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            clients[toEndPoint].SendAck(forSeqId);
        }

        /// <summary>Initializes available client IDs.</summary>
        private void InitializeClientIds()
        {
            availableClientIds = new List<ushort>(MaxClientCount);
            for (ushort i = 1; i <= MaxClientCount; i++)
                availableClientIds.Add(i);
        }

        /// <summary>Retrieves an available client ID.</summary>
        /// <returns>The client ID. 0 if none available.</returns>
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
                RiptideLogger.Log(LogName, "No available client IDs! Assigned 0.");
                return 0;
            }
        }

        /// <summary>Sends a message to a specific client.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="toClient">The client to send the message to.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        public void Send(Message message, ServerClient toClient, byte maxSendAttempts = 3)
        {
            if (message.SendMode == MessageSendMode.unreliable)
                Send(message.Bytes, message.WrittenLength, toClient.remoteEndPoint);
            else
                SendReliable(message, toClient.remoteEndPoint, toClient.Rudp, maxSendAttempts);
        }

        /// <summary>Sends a message to all conected clients.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        public void SendToAll(Message message, byte maxSendAttempts = 3)
        {
            lock (clients)
            {
                if (message.SendMode == MessageSendMode.unreliable)
                {
                    foreach (IPEndPoint clientEndPoint in clients.Keys)
                        Send(message.Bytes, message.WrittenLength, clientEndPoint);
                }
                else
                {
                    foreach (ServerClient client in clients.Values)
                        SendReliable(message, client.remoteEndPoint, client.Rudp, maxSendAttempts);
                }
            }
        }

        /// <summary>Sends a message to all connected clients except one.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="exceptToClient">The client NOT to send the message to.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        public void SendToAll(Message message, ServerClient exceptToClient, byte maxSendAttempts = 3)
        {
            lock (clients)
            {
                if (message.SendMode == MessageSendMode.unreliable)
                {
                    foreach (IPEndPoint clientEndPoint in clients.Keys)
                        if (!clientEndPoint.Equals(exceptToClient.remoteEndPoint))
                            Send(message.Bytes, message.WrittenLength, clientEndPoint);
                }
                else
                {
                    foreach (ServerClient client in clients.Values)
                        if (!client.remoteEndPoint.Equals(exceptToClient.remoteEndPoint))
                            SendReliable(message, client.remoteEndPoint, client.Rudp, maxSendAttempts);
                }
            }
        }

        /// <summary>Kicks a specific client.</summary>
        /// <param name="client">The client to kick.</param>
        public void DisconnectClient(ServerClient client)
        {
            if (clients.ContainsKey(client.remoteEndPoint))
            {
                SendDisconnect(client);
                client.Disconnect();
                lock (clients)
                    clients.Remove(client.remoteEndPoint);

                RiptideLogger.Log(LogName, $"Kicked {client.remoteEndPoint}.");
                OnClientDisconnected(new ClientDisconnectedEventArgs(client.Id));

                availableClientIds.Add(client.Id);
            }
            else
                RiptideLogger.Log(LogName, $"Failed to kick {client.remoteEndPoint} because they weren't connected.");
        }
        
        /// <summary>Stops the server.</summary>
        public void Stop()
        {
            byte[] disconnectBytes = { (byte)HeaderType.disconnect };
            lock (clients)
            {
                foreach (IPEndPoint clientEndPoint in clients.Keys)
                    Send(disconnectBytes, clientEndPoint);
                clients.Clear();
            }

            IsRunning = false;
            heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
            heartbeatTimer.Dispose();
            StopListening();
            RiptideLogger.Log(LogName, "Server stopped.");
        }

        #region Messages
        /// <summary>Sends a disconnect message.</summary>
        /// <param name="client">The client to send the disconnect message to.</param>
        private void SendDisconnect(ServerClient client)
        {
            Send(Message.CreateInternal(HeaderType.disconnect), client);
        }

        /// <summary>Handles a disconnect message.</summary>
        /// <param name="fromEndPoint">The endpoint from which the disconnect message was received.</param>
        private void HandleDisconnect(IPEndPoint fromEndPoint)
        {
            if (clients.TryGetValue(fromEndPoint, out ServerClient client))
            {
                client.Disconnect();
                lock (clients)
                    clients.Remove(fromEndPoint);
                OnClientDisconnected(new ClientDisconnectedEventArgs(client.Id));

                availableClientIds.Add(client.Id);
            }
        }

        /// <summary>Sends a client connected message.</summary>
        /// <param name="endPoint">The endpoint of the newly connected client.</param>
        /// <param name="id">The ID of the newly connected client.</param>
        private void SendClientConnected(IPEndPoint endPoint, ushort id)
        {
            Message message = Message.CreateInternal(HeaderType.clientConnected);
            message.Add(id);

            lock (clients)
                foreach (ServerClient client in clients.Values)
                    if (!client.remoteEndPoint.Equals(endPoint))
                        Send(message, client, 5);
        }

        /// <summary>Sends a client disconnected message.</summary>
        /// <param name="id">The ID of the client that disconnected.</param>
        private void SendClientDisconnected(ushort id)
        {
            Message message = Message.CreateInternal(HeaderType.clientDisconnected);
            message.Add(id);

            lock (clients)
                foreach (ServerClient client in clients.Values)
                    Send(message, client, 5);
        }
        #endregion

        #region Events
        /// <summary>Invoked when a new client connects.</summary>
        public event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        internal void OnClientConnected(ServerClientConnectedEventArgs e)
        {
            RiptideLogger.Log(LogName, $"{e.Client.remoteEndPoint} connected successfully! Client ID: {e.Client.Id}");

            if (receiveActionQueue == null)
                ClientConnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientConnected?.Invoke(this, e));

            SendClientConnected(e.Client.remoteEndPoint, e.Client.Id);
        }
        /// <summary>Invoked when a message is received from a client.</summary>
        public event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
        internal void OnMessageReceived(ServerMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        internal void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            RiptideLogger.Log(LogName, $"Client {e.Id} has disconnected.");

            if (receiveActionQueue == null)
                ClientDisconnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));
            
            SendClientDisconnected(e.Id);
        }
        #endregion
    }
}
