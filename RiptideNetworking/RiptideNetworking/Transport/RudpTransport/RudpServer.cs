using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

namespace RiptideNetworking.Transports.RudpTransport
{
    public class RudpServer : RudpListener, IServer
    {
        /// <inheritdoc/>
        public event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        /// <inheritdoc/>
        public event EventHandler<ServerMessageReceivedEventArgs> MessageReceived;
        /// <inheritdoc/>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <inheritdoc/>
        public ushort Port { get; private set; }
        /// <inheritdoc/>
        public ushort MaxClientCount { get; private set; }
        /// <inheritdoc/>
        public int ClientCount => clients.Count;
        /// <inheritdoc/>
        public IServerClient[] Clients => clients.Values.ToArray();
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

        /// <summary>Currently connected clients, accessible by their endpoints.</summary>
        private DoubleKeyDictionary<ushort, IPEndPoint, RudpServerClient> clients;
        /// <summary>Endpoints of clients that have timed out and need to be removed from the <see cref="clients"/> dictionary.</summary>
        private List<IPEndPoint> timedOutClients;
        /// <summary>All currently unused client IDs.</summary>
        private List<ushort> availableClientIds;
        /// <summary>The timer responsible for sending regular heartbeats.</summary>
        private Timer heartbeatTimer;

        /// <summary>Handles initial setup.</summary>
        /// <param name="clientTimeoutTime">The time (in milliseconds) after which to disconnect a client without a heartbeat.</param>
        /// <param name="clientHeartbeatInterval">The interval (in milliseconds) at which heartbeats are to be expected from clients.</param>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public RudpServer(ushort clientTimeoutTime = 5000, ushort clientHeartbeatInterval = 1000, string logName = "SERVER") : base(logName)
        {
            ClientTimeoutTime = clientTimeoutTime;
            _clientHeartbeatInterval = clientHeartbeatInterval;
        }

        /// <inheritdoc/>
        public void Start(ushort port, ushort maxClientCount)
        {
            Port = port;
            MaxClientCount = maxClientCount;
            clients = new DoubleKeyDictionary<ushort, IPEndPoint, RudpServerClient>(MaxClientCount);
            timedOutClients = new List<IPEndPoint>(MaxClientCount);

            InitializeClientIds();

            StartListening(port);

            heartbeatTimer = new Timer(Heartbeat, null, 0, ClientHeartbeatInterval);

            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, $"Started on port {port}.");
        }

        /// <inheritdoc/>
        public void Send(Message message, ushort toClientId, byte maxSendAttempts = 15, bool shouldRelease = true)
        {
            if (clients.TryGetValue(toClientId, out RudpServerClient toClient))
                Send(message, toClient, maxSendAttempts, false);

            if (shouldRelease)
                message.Release();
        }
        
        internal void Send(Message message, RudpServerClient toClient, byte maxSendAttempts = 15, bool shouldRelease = true)
        {
            if (message.SendMode == MessageSendMode.unreliable)
                Send(message.Bytes, message.WrittenLength, toClient.remoteEndPoint);
            else
                SendReliable(message, toClient.remoteEndPoint, toClient.Peer, maxSendAttempts);

            if (shouldRelease)
                message.Release();
        }

        /// <inheritdoc/>
        public void SendToAll(Message message, byte maxSendAttempts = 15, bool shouldRelease = true)
        {
            lock (clients)
            {
                if (message.SendMode == MessageSendMode.unreliable)
                {
                    foreach (RudpServerClient client in clients.Values)
                        Send(message.Bytes, message.WrittenLength, client.remoteEndPoint);
                }
                else
                {
                    foreach (RudpServerClient client in clients.Values)
                        SendReliable(message, client.remoteEndPoint, client.Peer, maxSendAttempts);
                }
            }

            if (shouldRelease)
                message.Release();
        }

        /// <inheritdoc/>
        public void SendToAll(Message message, ushort exceptToClientId, byte maxSendAttempts = 15, bool shouldRelease = true)
        {
            lock (clients)
            {
                if (message.SendMode == MessageSendMode.unreliable)
                {
                    foreach (RudpServerClient client in clients.Values)
                        if (client.Id != exceptToClientId)
                            Send(message.Bytes, message.WrittenLength, client.remoteEndPoint);
                }
                else
                {
                    foreach (RudpServerClient client in clients.Values)
                        if (client.Id != exceptToClientId)
                            SendReliable(message, client.remoteEndPoint, client.Peer, maxSendAttempts);
                }
            }

            if (shouldRelease)
                message.Release();
        }

        /// <inheritdoc/>
        public void DisconnectClient(ushort clientId)
        {
            if (clients.TryGetValue(clientId, out RudpServerClient client))
            {
                SendDisconnect(clientId);
                client.Disconnect();
                lock (clients)
                    clients.Remove(clientId, client.remoteEndPoint);

                if (ShouldOutputInfoLogs)
                    RiptideLogger.Log(LogName, $"Kicked {client.remoteEndPoint}.");
                OnClientDisconnected(new ClientDisconnectedEventArgs(clientId));

                availableClientIds.Add(clientId);
            }
            else
                RiptideLogger.Log(LogName, $"Failed to kick {client.remoteEndPoint} because they weren't connected.");
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            byte[] disconnectBytes = { (byte)HeaderType.disconnect };
            lock (clients)
            {
                foreach (RudpServerClient client in clients.Values)
                    Send(disconnectBytes, client.remoteEndPoint);
                clients.Clear();
            }

            heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
            heartbeatTimer.Dispose();
            StopListening();

            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, "Server stopped.");
        }

        internal void OnClientConnected(IPEndPoint clientEndPoint, ServerClientConnectedEventArgs e)
        {
            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, $"{clientEndPoint} connected successfully! Client ID: {e.Client.Id}");

            receiveActionQueue.Add(() => ClientConnected?.Invoke(this, e));

            SendClientConnected(clientEndPoint, e.Client.Id);
        }

        private void OnMessageReceived(ServerMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, $"Client {e.Id} has disconnected.");

            receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));

            SendClientDisconnected(e.Id);
        }

        /// <inheritdoc/>
        internal override void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            ReliableHandle(data, fromEndPoint, headerType, clients[fromEndPoint].SendLockables);
        }

        /// <inheritdoc/>
        internal override void Handle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            lock (clients)
            {
                if (!clients.TryGetValue(fromEndPoint, out RudpServerClient client))
                    return;

                Message message = Message.Create(headerType, data);

#if DETAILED_LOGGING
                if (headerType != HeaderType.reliable && headerType != HeaderType.unreliable)
                    RiptideLogger.Log(LogName, $"Received {headerType} message from {fromEndPoint}."); 

                ushort messageId = message.PeekUShort();
                if (headerType == HeaderType.reliable)
                    RiptideLogger.Log(LogName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                else if (headerType == HeaderType.unreliable)
                    RiptideLogger.Log(LogName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif

                switch (headerType)
                {
                    // User messages
                    case HeaderType.unreliable:
                    case HeaderType.reliable:
                        receiveActionQueue.Add(() =>
                        {
                            // This block may execute on a different thread, so we double check if the client is still in the dictionary in case they disconnected
                            lock (clients)
                            {
                                if (clients.TryGetValue(fromEndPoint, out RudpServerClient client2))
                                {
                                    ushort messageId = message.GetUShort();
                                    OnMessageReceived(new ServerMessageReceivedEventArgs(client2.Id, messageId, message));
                                }
                            }

                            message.Release();
                        });
                        return;

                    // Internal messages
                    case HeaderType.ack:
                        client.HandleAck(message);
                        break;
                    case HeaderType.ackExtra:
                        client.HandleAckExtra(message);
                        break;
                    case HeaderType.connect:
                        // Handled in ShouldHandleMessageFrom method
                        break;
                    case HeaderType.heartbeat:
                        client.HandleHeartbeat(message);
                        break;
                    case HeaderType.welcome:
                        client.HandleWelcomeReceived(message);
                        break;
                    case HeaderType.clientConnected:
                    case HeaderType.clientDisconnected:
                        break;
                    case HeaderType.disconnect:
                        HandleDisconnect(fromEndPoint);
                        break;
                    default:
                        RiptideLogger.Log("ERROR", $"Unknown message header type '{headerType}'! Discarding {data.Length} bytes.");
                        return;
                }

                message.Release();
            }
        }

        /// <inheritdoc/>
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
                    {
                        ushort id = GetAvailableClientId();
                        clients.Add(id, endPoint, new RudpServerClient(this, endPoint, id));
                    }
                }
                return false;
            }
        }

        /// <inheritdoc/>
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
                RiptideLogger.Log(LogName, "No available client IDs, assigned 0!");
                return 0;
            }
        }

        /// <summary>Checks if clients have timed out. Called by <see cref="heartbeatTimer"/>.</summary>
        private void Heartbeat(object state)
        {
            lock (clients)
            {
                foreach (RudpServerClient client in clients.Values)
                {
                    if (client.HasTimedOut)
                        timedOutClients.Add(client.remoteEndPoint);
                }

                foreach (IPEndPoint clientEndPoint in timedOutClients)
                    HandleDisconnect(clientEndPoint); // Disconnect the client

                timedOutClients.Clear();
            }
        }

        #region Messages
        /// <summary>Sends a disconnect message.</summary>
        /// <param name="clientId">The client to send the disconnect message to.</param>
        private void SendDisconnect(ushort clientId)
        {
            Send(Message.Create(HeaderType.disconnect), clientId);
        }

        /// <summary>Handles a disconnect message.</summary>
        /// <param name="fromEndPoint">The endpoint from which the disconnect message was received.</param>
        private void HandleDisconnect(IPEndPoint fromEndPoint)
        {
            if (clients.TryGetValue(fromEndPoint, out RudpServerClient client))
            {
                client.Disconnect();
                lock (clients)
                    clients.Remove(client.Id, fromEndPoint);
                OnClientDisconnected(new ClientDisconnectedEventArgs(client.Id));

                availableClientIds.Add(client.Id);
            }
        }

        /// <summary>Sends a client connected message.</summary>
        /// <param name="endPoint">The endpoint of the newly connected client.</param>
        /// <param name="id">The ID of the newly connected client.</param>
        private void SendClientConnected(IPEndPoint endPoint, ushort id)
        {
            if (clients.Count <= 1)
                return; // We don't send this to the newly connected client anyways, so don't even bother creating a message if he is the only one connected

            Message message = Message.Create(HeaderType.clientConnected);
            message.Add(id);

            lock (clients)
            {
                foreach (RudpServerClient client in clients.Values)
                {
                    if (!client.remoteEndPoint.Equals(endPoint))
                        Send(message, client, 25, false);
                }
            }

            message.Release();
        }

        /// <summary>Sends a client disconnected message.</summary>
        /// <param name="id">The ID of the client that disconnected.</param>
        private void SendClientDisconnected(ushort id)
        {
            Message message = Message.Create(HeaderType.clientDisconnected);
            message.Add(id);

            lock (clients)
                foreach (RudpServerClient client in clients.Values)
                    Send(message, client, 25, false);

            message.Release();
        }
        #endregion
    }
}
