
// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) 2021 Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub: https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using RiptideNetworking.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace RiptideNetworking.Transports.RudpTransport
{
    /// <summary>A server that can accept connections from <see cref="RudpClient"/>s.</summary>
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
        public int ClientCount
        {
            get
            {
                lock (clients)
                    return clients != null ? clients.Count : 0;
            }
        }
        /// <inheritdoc/>
        public IConnectionInfo[] Clients
        {
            get
            {
                lock (clients)
                    return clients != null ? clients.Values.ToArray() : new IConnectionInfo[0];
            }
        }
        /// <inheritdoc/>
        public bool AllowAutoMessageRelay { get; set; } = false;
        /// <summary>The time (in milliseconds) after which to disconnect a client without a heartbeat.</summary>
        public ushort ClientTimeoutTime { get; set; } = 5000;
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
        private ushort _clientHeartbeatInterval;

        /// <summary>Currently connected clients, accessible by their endpoints or numeric ID.</summary>
        private DoubleKeyDictionary<ushort, IPEndPoint, RudpConnection> clients;
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
            clients = new DoubleKeyDictionary<ushort, IPEndPoint, RudpConnection>(MaxClientCount);
            timedOutClients = new List<IPEndPoint>(MaxClientCount);

            InitializeClientIds();

            StartListening(port);

            heartbeatTimer = new Timer((o) => Heartbeat(), null, 0, ClientHeartbeatInterval);
            RiptideLogger.Log(LogType.info, LogName, $"Started on port {port}.");
        }

        /// <summary>Checks if clients have timed out. Called by <see cref="heartbeatTimer"/>.</summary>
        private void Heartbeat()
        {
            lock (clients)
            {
                foreach (RudpConnection client in clients.Values)
                    if (client.HasTimedOut)
                        timedOutClients.Add(client.RemoteEndPoint);
            }

            foreach (IPEndPoint clientEndPoint in timedOutClients)
                HandleDisconnect(clientEndPoint); // Disconnect the clients

            timedOutClients.Clear();
        }

        /// <inheritdoc/>
        protected override bool ShouldHandleMessageFrom(IPEndPoint endPoint, HeaderType messageHeader)
        {
            lock (clients)
            {
                if (clients.ContainsKey(endPoint))
                {
                    // Client is already connected
                    if (messageHeader != HeaderType.connect) // It's not a connect message, so handle it
                        return true;
                }
                else if (ClientCount < MaxClientCount)
                {
                    // Client is not yet connected and the server has capacity
                    if (messageHeader == HeaderType.connect) // It's a connect message, which doesn't need to be handled like other messages
                    {
                        ushort id = GetAvailableClientId();
                        clients.Add(id, endPoint, new RudpConnection(this, endPoint, id));
                    }
                }
                else
                {
                    // Server is full
                    RiptideLogger.Log(LogType.info, LogName, $"Server is full! Rejecting connection from {endPoint}.");
                }
                
                return false;
            }
        }

        /// <inheritdoc/>
        protected override void Handle(Message message, IPEndPoint fromEndPoint, HeaderType messageHeader)
        {
            if (!TryGetClient(fromEndPoint, out RudpConnection client))
                return;

#if DETAILED_LOGGING
            if (messageHeader != HeaderType.reliable && messageHeader != HeaderType.unreliable)
                RiptideLogger.Log(LogName, $"Received {messageHeader} message from {fromEndPoint}."); 

            ushort messageId = message.PeekUShort();
            if (messageHeader == HeaderType.reliable)
                RiptideLogger.Log(LogName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
            else if (messageHeader == HeaderType.unreliable)
                RiptideLogger.Log(LogName, $"Received unreliable message (ID: {messageId}) from {fromEndPoint}.");
#endif

            switch (messageHeader)
            {
                // User messages
                case HeaderType.unreliable:
                case HeaderType.reliable:
                    OnMessageReceived(message, fromEndPoint);
                    return;
                case HeaderType.unreliableAutoRelay:
                case HeaderType.reliableAutoRelay:
                    if (AllowAutoMessageRelay)
                        SendToAll(message, client.Id);
                    else
                        OnMessageReceived(message, fromEndPoint);
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
                    return; // Important so the message isn't released immediately
                case HeaderType.clientConnected:
                case HeaderType.clientDisconnected:
                    break;
                case HeaderType.disconnect:
                    HandleDisconnect(fromEndPoint);
                    break;
                default:
                    RiptideLogger.Log(LogType.warning, LogName, $"Unknown message header type '{messageHeader}'! Discarding {message.WrittenLength} bytes received from {fromEndPoint}.");
                    break;
            }

            message.Release();
        }

        /// <inheritdoc/>
        protected override void ReliableHandle(HeaderType messageHeader, ushort sequenceId, Message message, IPEndPoint fromEndPoint)
        {
            if (TryGetClient(fromEndPoint, out RudpConnection client))
                ReliableHandle(messageHeader, sequenceId, message, fromEndPoint, client.SendLockables);
        }

        /// <inheritdoc/>
        public void Send(Message message, ushort toClientId, bool shouldRelease = true)
        {
            if (TryGetClient(toClientId, out RudpConnection toClient))
                Send(message, toClient, false);

            if (shouldRelease)
                message.Release();
        }

        /// <summary>Sends a message to a specific client.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="toClient">The client to send the message to.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        internal void Send(Message message, RudpConnection toClient, bool shouldRelease = true)
        {
            if (message.SendMode == MessageSendMode.unreliable)
                Send(message.Bytes, message.WrittenLength, toClient.RemoteEndPoint);
            else
                SendReliable(message, toClient.RemoteEndPoint, toClient.Peer);

            if (shouldRelease)
                message.Release();
        }

        /// <inheritdoc/>
        public void SendToAll(Message message, bool shouldRelease = true)
        {
            lock (clients)
            {
                if (message.SendMode == MessageSendMode.unreliable)
                {
                    foreach (RudpConnection client in clients.Values)
                        Send(message.Bytes, message.WrittenLength, client.RemoteEndPoint);
                }
                else
                {
                    foreach (RudpConnection client in clients.Values)
                        SendReliable(message, client.RemoteEndPoint, client.Peer);
                }
            }

            if (shouldRelease)
                message.Release();
        }

        /// <inheritdoc/>
        public void SendToAll(Message message, ushort exceptToClientId, bool shouldRelease = true)
        {
            lock (clients)
            {
                if (message.SendMode == MessageSendMode.unreliable)
                {
                    foreach (RudpConnection client in clients.Values)
                        if (client.Id != exceptToClientId)
                            Send(message.Bytes, message.WrittenLength, client.RemoteEndPoint);
                }
                else
                {
                    foreach (RudpConnection client in clients.Values)
                        if (client.Id != exceptToClientId)
                            SendReliable(message, client.RemoteEndPoint, client.Peer);
                }
            }

            if (shouldRelease)
                message.Release();
        }

        /// <inheritdoc/>
        public void DisconnectClient(ushort clientId)
        {
            if (TryGetClient(clientId, out RudpConnection client))
            {
                SendDisconnect(client.Id);
                RiptideLogger.Log(LogType.info, LogName, $"Kicked {client.RemoteEndPoint.ToStringBasedOnIPFormat()} (ID: {client.Id}).");

                LocalDisconnect(client);
            }
            else
                RiptideLogger.Log(LogType.warning, LogName, $"Failed to kick {client.RemoteEndPoint} because they weren't connected!");
        }

        private void LocalDisconnect(RudpConnection client)
        {
            client.LocalDisconnect();
            lock (clients)
            {
                if (clients.Remove(client.Id, client.RemoteEndPoint))
                {
                    OnClientDisconnected(new ClientDisconnectedEventArgs(client.Id));
                    availableClientIds.Add(client.Id);
                }
            }
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            byte[] disconnectBytes = { (byte)HeaderType.disconnect };
            lock (clients)
            {
                foreach (RudpConnection client in clients.Values)
                    Send(disconnectBytes, client.RemoteEndPoint);
                clients.Clear();
            }

            heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
            heartbeatTimer.Dispose();
            StopListening();

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
                RiptideLogger.Log(LogType.error, LogName, "No available client IDs, assigned 0!");
                return 0;
            }
        }

        private bool TryGetClient(ushort clientId, out RudpConnection client)
        {
            lock (clients)
                return clients.TryGetValue(clientId, out client);
        }

        private bool TryGetClient(IPEndPoint fromEndPoint, out RudpConnection client)
        {
            lock (clients)
                return clients.TryGetValue(fromEndPoint, out client);
        }

        #region Messages
        /// <summary>Sends a disconnect message.</summary>
        /// <param name="clientId">The client to send the disconnect message to.</param>
        private void SendDisconnect(ushort clientId)
        {
            Send(Message.Create(HeaderType.disconnect), clientId);
        }

        /// <inheritdoc/>
        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            if (TryGetClient(toEndPoint, out RudpConnection client))
                client.SendAck(forSeqId);
        }

        /// <summary>Handles a disconnect message.</summary>
        /// <param name="fromEndPoint">The endpoint from which the disconnect message was received.</param>
        private void HandleDisconnect(IPEndPoint fromEndPoint)
        {
            if (TryGetClient(fromEndPoint, out RudpConnection client))
                LocalDisconnect(client);
        }

        /// <summary>Sends a client connected message.</summary>
        /// <param name="endPoint">The endpoint of the newly connected client.</param>
        /// <param name="id">The ID of the newly connected client.</param>
        private void SendClientConnected(IPEndPoint endPoint, ushort id)
        {
            if (ClientCount <= 1)
                return; // We don't send this to the newly connected client anyways, so don't even bother creating a message if he is the only one connected

            Message message = Message.Create(HeaderType.clientConnected, 25);
            message.Add(id);

            lock (clients)
                foreach (RudpConnection client in clients.Values)
                    if (!client.RemoteEndPoint.Equals(endPoint))
                        Send(message, client, false);

            message.Release();
        }

        /// <summary>Sends a client disconnected message.</summary>
        /// <param name="id">The numeric ID of the client that disconnected.</param>
        private void SendClientDisconnected(ushort id)
        {
            Message message = Message.Create(HeaderType.clientDisconnected, 25);
            message.Add(id);

            lock (clients)
                foreach (RudpConnection client in clients.Values)
                    Send(message, client, false);

            message.Release();
        }
        #endregion

        #region Events
        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        /// <param name="clientEndPoint">The endpoint of the newly connected client.</param>
        /// <param name="e">The event args to invoke the event with.</param>
        internal void OnClientConnected(IPEndPoint clientEndPoint, ServerClientConnectedEventArgs e)
        {
            RiptideLogger.Log(LogType.info, LogName, $"{clientEndPoint.ToStringBasedOnIPFormat()} connected successfully! Client ID: {e.Client.Id}");

            receiveActionQueue.Add(() =>
            {
                ClientConnected?.Invoke(this, e);
                e.ConnectMessage.Release();
            });
            SendClientConnected(clientEndPoint, e.Client.Id);
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event.</summary>
        /// <param name="message">The received message.</param>
        /// <param name="fromEndPoint">The endpoint from which the message was received.</param>
        private void OnMessageReceived(Message message, IPEndPoint fromEndPoint)
        {
            receiveActionQueue.Add(() =>
            {
                // This block may execute on a different thread, so we double check if the client is still in the dictionary in case they disconnected
                if (TryGetClient(fromEndPoint, out RudpConnection client))
                    MessageReceived?.Invoke(this, new ServerMessageReceivedEventArgs(client.Id, message.GetUShort(), message));

                message.Release();
            });
        }

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        /// <param name="e">The event args to invoke the event with.</param>
        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            RiptideLogger.Log(LogType.info, LogName, $"Client {e.Id} disconnected.");

            receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));
            SendClientDisconnected(e.Id);
        }
        #endregion
    }
}
