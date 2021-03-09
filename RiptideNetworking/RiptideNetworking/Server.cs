using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace RiptideNetworking
{
    /// <summary>Represents a server which can accept connections from clients.</summary>
    public class Server : RudpSocket
    {
        /// <summary>Currently connected clients, accessible by their IPEndPoint.</summary>
        public Dictionary<IPEndPoint, ServerClient> Clients { get; private set; }
        /// <summary>The local port that the server is running on.</summary>
        public ushort Port { get; private set; }
        /// <summary>The maximum number of clients that can be connected at any time.</summary>
        public ushort MaxClientCount { get; private set; }
        /// <summary>The number of currently connected clients.</summary>
        public int ClientCount { get => Clients.Count; }

        private ActionQueue receiveActionQueue;
        private List<ushort> availableClientIds;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name of this server instance. Used when logging messages.</param>
        public Server(string logName = "SERVER") : base(logName) { }

        /// <summary>Starts the server.</summary>
        /// <param name="port">The local port on which to start the server.</param>
        /// <param name="maxClientCount">The maximum number of concurrent connections to allow.</param>
        /// <param name="receiveActionQueue">The action queue to add messages to. Passing null will cause messages to be handled immediately on the same thread on which they were received.</param>
        public void Start(ushort port, ushort maxClientCount, ActionQueue receiveActionQueue = null)
        {
            Port = port;
            MaxClientCount = maxClientCount;
            Clients = new Dictionary<IPEndPoint, ServerClient>(this.MaxClientCount);

            InitializeClientIds();

            this.receiveActionQueue = receiveActionQueue;

            StartListening(port);

            RiptideLogger.Log(logName, $"Started on port {port}.");
            //Timer tickTimer = new Timer(Tick, null, 0, 1000);
        }

        //private void Tick(object state)
        //{
        //    lock (Clients)
        //    {
        //        foreach (ServerClient client in Clients.Values)
        //        {
        //            client.SendHeartbeat();
        //        }
        //    }
        //}

        /// <summary>Whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="firstByte">The first byte of the message.</param>
        protected override bool ShouldHandleMessageFrom(IPEndPoint endPoint, byte firstByte)
        {
            if (Clients.ContainsKey(endPoint))
            {
                if ((HeaderType)firstByte != HeaderType.connect)
                    return true;
            }
            else if (Clients.Count < MaxClientCount)
            {
                if ((HeaderType)firstByte == HeaderType.connect)
                    Clients.Add(endPoint, new ServerClient(this, endPoint, GetAvailableClientId()));
            }
            return false;
        }

        internal override void Handle(Message message, IPEndPoint fromEndPoint, HeaderType headerType)
        {
#if DETAILED_LOGGING
            if (headerType != HeaderType.reliable && headerType != HeaderType.unreliable)
                RiptideLogger.Log(logName, $"Received {headerType} message from {fromEndPoint}."); 
#endif
            switch (headerType)
            {
                case HeaderType.unreliable:
                case HeaderType.reliable:
                    if (receiveActionQueue == null)
                    {
#if DETAILED_LOGGING
                        ushort messageId = message.PeekUShort();
                        if (headerType == HeaderType.reliable)
                            RiptideLogger.Log(logName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                        else if (headerType == HeaderType.unreliable)
                            RiptideLogger.Log(logName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif
                        OnMessageReceived(new ServerMessageReceivedEventArgs(Clients[fromEndPoint], message));
                    }
                    else
                    {
                        receiveActionQueue.Add(() =>
                        {
#if DETAILED_LOGGING
                            ushort messageId = message.PeekUShort();
                            if (headerType == HeaderType.reliable)
                                RiptideLogger.Log(logName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                            else if (headerType == HeaderType.unreliable)
                                RiptideLogger.Log(logName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif
                            if (Clients.TryGetValue(fromEndPoint, out ServerClient client))
                                OnMessageReceived(new ServerMessageReceivedEventArgs(client, message));
#if DETAILED_LOGGING
                            else
                                RiptideLogger.Log(logName, $"Aborted handling of message (ID: {messageId}) because client is no longer connected.");
#endif
                        });
                    }
                    break;
                case HeaderType.ack:
                    Clients[fromEndPoint].HandleAck(message);
                    break;
                case HeaderType.ackExtra:
                    Clients[fromEndPoint].HandleAckExtra(message);
                    break;
                case HeaderType.connect:
                    // Handled in ShouldHandleMessageFrom method
                    break;
                case HeaderType.heartbeat:
                    Clients[fromEndPoint].HandleHeartbeat(message);
                    break;
                case HeaderType.welcome:
                    Clients[fromEndPoint].HandleWelcomeReceived(message);
                    break;
                case HeaderType.clientConnected:
                case HeaderType.clientDisconnected:
                    break;
                case HeaderType.disconnect:
                    HandleDisconnect(fromEndPoint);
                    break;
                default:
                    throw new Exception($"Unknown message header type: {headerType}");
            }
        }

        internal override void ReliableHandle(Message message, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            ReliableHandle(message, fromEndPoint, headerType, Clients[fromEndPoint].SendLockables);
        }

        /// <summary>Sends an acknowledgement for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        /// <param name="toEndPoint">The endpoint to send the acknowledgement to.</param>
        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            Clients[toEndPoint].SendAck(forSeqId);
        }

        private void InitializeClientIds()
        {
            availableClientIds = new List<ushort>(MaxClientCount);
            for (ushort i = 1; i <= MaxClientCount; i++)
            {
                availableClientIds.Add(i);
            }
        }

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
                RiptideLogger.Log(logName, "No available client IDs! Assigned 0.");
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
                Send(message.ToArray(), toClient.remoteEndPoint);
            else
                SendReliable(message, toClient.remoteEndPoint, toClient.Rudp, maxSendAttempts);
        }

        /// <summary>Sends a message to all conected clients.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        public void SendToAll(Message message, byte maxSendAttempts = 3)
        {
            if (message.SendMode == MessageSendMode.unreliable)
            {
                foreach (IPEndPoint clientEndPoint in Clients.Keys)
                    Send(message.ToArray(), clientEndPoint);
            }
            else
            {
                foreach (ServerClient client in Clients.Values)
                    SendReliable(message, client.remoteEndPoint, client.Rudp, maxSendAttempts);
            }
        }

        /// <summary>Sends a message to all connected clients except one.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="exceptToClient">The client NOT to send the message to.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        public void SendToAll(Message message, ServerClient exceptToClient, byte maxSendAttempts = 3)
        {
            if (message.SendMode == MessageSendMode.unreliable)
            {
                foreach (IPEndPoint clientEndPoint in Clients.Keys)
                    if (!clientEndPoint.Equals(exceptToClient.remoteEndPoint))
                        Send(message.ToArray(), clientEndPoint);
            }
            else
            {
                foreach (ServerClient client in Clients.Values)
                    if (!client.remoteEndPoint.Equals(exceptToClient.remoteEndPoint))
                        SendReliable(message, client.remoteEndPoint, client.Rudp, maxSendAttempts);
            }
        }

        /// <summary>Kicks a specific client.</summary>
        /// <param name="client">The client to kick.</param>
        public void DisconnectClient(ServerClient client)
        {
            if (Clients.ContainsKey(client.remoteEndPoint))
            {
                SendDisconnect(client);
                client.Disconnect();
                Clients.Remove(client.remoteEndPoint);

                RiptideLogger.Log(logName, $"Kicked {client.remoteEndPoint}.");
                OnClientDisconnected(new ClientDisconnectedEventArgs(client.Id));

                availableClientIds.Add(client.Id);
            }
            else
            {
                RiptideLogger.Log(logName, $"Failed to kick {client.remoteEndPoint} because they weren't connected.");
            }
        }
        
        /// <summary>Stops the server.</summary>
        public void Stop()
        {
            byte[] disconnectBytes = { (byte)HeaderType.disconnect };
            foreach (IPEndPoint clientEndPoint in Clients.Keys)
            {
                Send(disconnectBytes, clientEndPoint);
            }

            StopListening();
            Clients.Clear();
            RiptideLogger.Log(logName, "Server stopped.");
        }

        #region Messages
        private void SendDisconnect(ServerClient client)
        {
            Send(new Message(HeaderType.disconnect), client, 5);
        }

        private void HandleDisconnect(IPEndPoint fromEndPoint)
        {
            if (Clients.TryGetValue(fromEndPoint, out ServerClient client))
            {
                client.Disconnect();
                Clients.Remove(fromEndPoint);
                OnClientDisconnected(new ClientDisconnectedEventArgs(client.Id));

                availableClientIds.Add(client.Id);
            }
        }

        private void SendClientConnected(IPEndPoint endPoint, ushort id)
        {
            Message message = new Message(HeaderType.clientConnected, 2);
            message.Add(id);

            foreach (ServerClient client in Clients.Values)
            {
                if (!client.remoteEndPoint.Equals(endPoint))
                    Send(message, client, 5);
            }
        }

        private void SendClientDisconnected(ushort id)
        {
            Message message = new Message(HeaderType.clientDisconnected, 2);
            message.Add(id);

            foreach (ServerClient client in Clients.Values)
            {
                Send(message, client, 5);
            }
        }
        #endregion

        #region Events
        /// <summary>Invoked when a new client connects.</summary>
        public event EventHandler<ServerClientConnectedEventArgs> ClientConnected;
        internal void OnClientConnected(ServerClientConnectedEventArgs e)
        {
            RiptideLogger.Log(logName, $"{e.Client.remoteEndPoint} connected successfully! Client ID: {e.Client.Id}");

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
            RiptideLogger.Log(logName, $"Client {e.Id} has disconnected.");

            if (receiveActionQueue == null)
                ClientDisconnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));
            
            SendClientDisconnected(e.Id);
        }
        #endregion
    }
}
