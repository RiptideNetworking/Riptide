using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace RiptideNetworking
{
    public class Client : RudpSocket
    {
        public ushort Id { get; private set; }
        public ushort RTT { get => rudp.RTT; }
        public ushort SmoothRTT { get => rudp.SmoothRTT; }

        public delegate void MessageHandler(Message message);

        private Dictionary<ushort, MessageHandler> messageHandlers;
        private ActionQueue receiveActionQueue;
        private IPEndPoint remoteEndPoint;
        private Rudp rudp;
        private ConnectionState connectionState = ConnectionState.notConnected;

        // Ping and RTT
        internal DateTime LastHeartbeat { get; private set; }
        private byte lastPingId = 0;
        private (byte id, DateTime sendTime) pendingPing;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name of this client instance. Used when logging messages.</param>
        public Client(string logName = "CLIENT") : base(logName) { }

        public void Connect(string ip, ushort port, Dictionary<ushort, MessageHandler> messageHandlers, ActionQueue receiveActionQueue = null)
        {
            this.messageHandlers = messageHandlers;
            this.receiveActionQueue = receiveActionQueue;
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            rudp = new Rudp(Send, logName);

            RiptideLogger.Log(logName, $"Connecting to {remoteEndPoint}.");
            StartListening();
            connectionState = ConnectionState.connecting;

            Timer tickTimer = new Timer(Tick, null, 0, 1000);
        }

        private void Tick(object state)
        {
            if (connectionState == ConnectionState.connecting)
                SendConnect();
            else if (connectionState == ConnectionState.connected)
                SendHeartbeat();
        }

        protected override bool ShouldHandleMessageFrom(IPEndPoint endPoint, byte firstByte)
        {
            return endPoint.Equals(remoteEndPoint);
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
                        messageHandlers[messageId](message);
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
                            messageHandlers[messageId](message);
                        });
                    }
                    break;
                case HeaderType.ack:
                    HandleAck(message);
                    break;
                case HeaderType.ackExtra:
                    HandleAckExtra(message);
                    break;
                case HeaderType.heartbeat:
                    HandleHeartbeat(message);
                    break;
                case HeaderType.welcome:
                    HandleWelcome(message);
                    break;
                case HeaderType.clientConnected:
                    HandleClientConnected(message);
                    break;
                case HeaderType.clientDisconnected:
                    HandleClientDisconnected(message);
                    break;
                case HeaderType.disconnect:
                    HandleDisconnect(message);
                    break;
                default:
                    throw new Exception($"Unknown message header type: {headerType}");
            }
        }

        internal override void ReliableHandle(Message message, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            ReliableHandle(message, fromEndPoint, headerType, rudp.SendLockables);
        }

        private void Send(Message message, HeaderType headerType)
        {
            message.PrepareToSend(headerType);
            Send(message.ToArray(), remoteEndPoint);
        }

        public void Send(Message message)
        {
            Send(message, HeaderType.unreliable);
        }

        public void SendReliable(Message message, byte maxSendAttempts = 3)
        {
            SendReliable(message, remoteEndPoint, rudp, maxSendAttempts);
        }

        public void Disconnect()
        {
            if (connectionState == ConnectionState.notConnected)
                return;

            SendDisconnect();
            StopListening();
            connectionState = ConnectionState.notConnected;
            RiptideLogger.Log(logName, "Disconnected.");
        }

        #region Messages
        private void SendConnect()
        {
            Message message = new Message();

            Send(message, HeaderType.connect);
        }

        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            Message message = new Message();
            message.Add(rudp.SendLockables.LastReceivedSeqId); // Last remote sequence ID
            message.Add(rudp.SendLockables.AcksBitfield); // Acks

            if (forSeqId != rudp.SendLockables.LastReceivedSeqId)
            {
                message.Add(forSeqId);
                Send(message, HeaderType.ackExtra);
            }
            else
            {
                Send(message, HeaderType.ack);
            }
        }

        private void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            rudp.AckMessage(remoteLastReceivedSeqId);
            rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        private void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            rudp.AckMessage(ackedSeqId);
            rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        private void SendHeartbeat()
        {
            pendingPing = (lastPingId++, DateTime.UtcNow);

            Message message = new Message();
            message.Add(pendingPing.id);
            message.Add(rudp.RTT);

            Send(message, HeaderType.heartbeat);
        }

        private void HandleHeartbeat(Message message)
        {
            byte pingId = message.GetByte();

            if (pendingPing.id == pingId)
            {
                rudp.RTT = (ushort)Math.Max(1f, (DateTime.UtcNow - pendingPing.sendTime).TotalMilliseconds);
                OnPingUpdated(new PingUpdatedEventArgs(rudp.RTT, rudp.SmoothRTT));
            }

            LastHeartbeat = DateTime.UtcNow;
        }

        private void HandleWelcome(Message message)
        {
            if (connectionState == ConnectionState.connected)
                return;

            Id = message.GetUShort();
            connectionState = ConnectionState.connected;

            SendWelcomeReceived();
            OnConnected(new EventArgs());
        }

        internal void SendWelcomeReceived()
        {
            Message message = new Message();
            message.Add(Id);

            SendReliable(message, remoteEndPoint, rudp, 5, HeaderType.welcome);
        }

        private void HandleClientConnected(Message message)
        {
            OnClientConnected(new ClientConnectedEventArgs(message.GetUShort()));
        }

        private void HandleClientDisconnected(Message message)
        {
            OnClientDisconnected(new ClientDisconnectedEventArgs(message.GetUShort()));
        }

        private void SendDisconnect()
        {
            Message message = new Message();

            Send(message, HeaderType.disconnect);
        }

        private void HandleDisconnect(Message message)
        {
            StopListening();
            connectionState = ConnectionState.notConnected;
            OnDisconnected(new EventArgs());
        }
        #endregion

        #region Events
        public event EventHandler Connected;
        private void OnConnected(EventArgs e)
        {
            RiptideLogger.Log(logName, "Connected successfully!");

            if (receiveActionQueue == null)
                Connected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => Connected?.Invoke(this, e));
        }
        public event EventHandler Disconnected;
        private void OnDisconnected(EventArgs e)
        {
            RiptideLogger.Log(logName, "Disconnected from server.");

            if (receiveActionQueue == null)
                Disconnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => Disconnected?.Invoke(this, e));
        }

        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        private void OnClientConnected(ClientConnectedEventArgs e)
        {
            RiptideLogger.Log(logName, $"Client {e.Id} connected.");

            if (receiveActionQueue == null)
                ClientConnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientConnected?.Invoke(this, e));
        }
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            RiptideLogger.Log(logName, $"Client {e.Id} disconnected.");

            if (receiveActionQueue == null)
                ClientDisconnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));
        }

        public event EventHandler<PingUpdatedEventArgs> PingUpdated;
        private void OnPingUpdated(PingUpdatedEventArgs e)
        {
            if (receiveActionQueue == null)
                PingUpdated?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => PingUpdated?.Invoke(this, e));
        }
        #endregion
    }
}
