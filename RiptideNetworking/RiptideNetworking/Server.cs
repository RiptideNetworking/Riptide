using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace RiptideNetworking
{
    public class Server : RudpSocket
    {
        public Dictionary<IPEndPoint, ServerClient> Clients { get; private set; }
        public ushort Port { get; private set; }
        public ushort MaxClientCount { get; private set; }
        public int ClientCount { get => Clients.Count; }

        public delegate void MessageHandler(ServerClient fromClient, Message message);

        private Dictionary<ushort, MessageHandler> messageHandlers;
        private ActionQueue receiveActionQueue;
        private List<ushort> availableClientIds;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name of this server instance. Used when logging messages.</param>
        public Server(string logName = "SERVER") : base(logName) { }

        public void Start(ushort port, ushort maxClientCount, Dictionary<ushort, MessageHandler> messageHandlers, ActionQueue receiveActionQueue = null)
        {
            Port = port;
            MaxClientCount = maxClientCount;
            Clients = new Dictionary<IPEndPoint, ServerClient>(this.MaxClientCount);

            this.messageHandlers = messageHandlers;
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
                        ushort messageId = message.GetUShort();
#if DETAILED_LOGGING
                        if (headerType == HeaderType.reliable)
                            RiptideLogger.Log(logName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                        else if (headerType == HeaderType.unreliable)
                            RiptideLogger.Log(logName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif
                        messageHandlers[messageId](Clients[fromEndPoint], message);
                    }
                    else
                    {
                        receiveActionQueue.Add(() =>
                        {
                            ushort messageId = message.GetUShort();
#if DETAILED_LOGGING
                            if (headerType == HeaderType.reliable)
                                RiptideLogger.Log(logName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                            else if (headerType == HeaderType.unreliable)
                                RiptideLogger.Log(logName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif
                            messageHandlers[messageId](Clients[fromEndPoint], message);
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

        internal void Send(Message message, IPEndPoint toEndPoint, HeaderType headerType = HeaderType.unreliable)
        {
            message.PrepareToSend(headerType);
            Send(message.ToArray(), toEndPoint);
        }

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

        public void Send(Message message, ServerClient toClient)
        {
            Send(message, toClient.remoteEndPoint);
        }

        public void SendToAll(Message message)
        {
            message.PrepareToSend(HeaderType.unreliable);

            foreach (IPEndPoint clientEndPoint in Clients.Keys)
            {
                Send(message.ToArray(), clientEndPoint);
            }
        }

        public void SendToAll(Message message, ServerClient exceptToClient)
        {
            message.PrepareToSend(HeaderType.unreliable);

            foreach (IPEndPoint clientEndPoint in Clients.Keys)
            {
                if (!clientEndPoint.Equals(exceptToClient))
                    Send(message.ToArray(), clientEndPoint);
            }
        }

        public void SendReliable(Message message, ServerClient toClient, byte maxSendAttempts = 3)
        {
            SendReliable(message, toClient.remoteEndPoint, toClient.Rudp, maxSendAttempts);
        }

        public void SendReliableToAll(Message message, byte maxSendAttempts = 3)
        {
            foreach (ServerClient client in Clients.Values)
            {
                SendReliable(message, client.remoteEndPoint, client.Rudp, maxSendAttempts);
            }
        }

        public void SendReliableToAll(Message message, ServerClient exceptToClient, byte maxSendAttempts = 3)
        {
            foreach (ServerClient client in Clients.Values)
            {
                if (!client.remoteEndPoint.Equals(exceptToClient))
                    SendReliable(message, client.remoteEndPoint, client.Rudp, maxSendAttempts);
            }
        }

        public void DisconnectClient(IPEndPoint clientEndPoint)
        {
            if (Clients.TryGetValue(clientEndPoint, out ServerClient client))
            {
                SendDisconnect(client);
                client.Disconnect();
                Clients.Remove(clientEndPoint);

                RiptideLogger.Log($"Kicked {clientEndPoint}.");
                OnClientDisconnected(new ClientDisconnectedEventArgs(client.Id));

                availableClientIds.Add(client.Id);
            }
            else
            {
                RiptideLogger.Log($"Failed to kick {clientEndPoint} because they weren't connected.");
            }
        }
        
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
            SendReliable(new Message(), client.remoteEndPoint, client.Rudp, 5, HeaderType.disconnect);
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
            Message message = new Message();
            message.Add(id);

            foreach (ServerClient client in Clients.Values)
            {
                if (!client.remoteEndPoint.Equals(endPoint))
                    SendReliable(message, client.remoteEndPoint, client.Rudp, 5, HeaderType.clientConnected);
            }
        }

        private void SendClientDisconnected(ushort id)
        {
            Message message = new Message();
            message.Add(id);

            foreach (ServerClient client in Clients.Values)
            {
                SendReliable(message, client.remoteEndPoint, client.Rudp, 5, HeaderType.clientDisconnected);
            }
        }
#endregion

#region Events
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
