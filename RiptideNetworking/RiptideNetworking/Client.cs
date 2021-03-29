using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace RiptideNetworking
{
    /// <summary>Represents a client connection.</summary>
    public class Client : RudpSocket
    {
        /// <summary>The numeric ID.</summary>
        public ushort Id { get; private set; }
        /// <summary>The round trip time of the connection.</summary>
        public ushort RTT { get => rudp.RTT; }
        /// <summary>The smoothed round trip time of the connection.</summary>
        public ushort SmoothRTT { get => rudp.SmoothRTT; }
        /// <summary>The time (in milliseconds) after which to disconnect if there's no heartbeat from the server.</summary>
        public ushort TimeoutTime { get; set; } = 5000;
        private ushort _heartbeatInterval;
        /// <summary>The interval (in milliseconds) at which heartbeats are to be expected from clients.</summary>
        public ushort HeartbeatInterval
        {
            get => _heartbeatInterval;
            set
            {
                _heartbeatInterval = value;
                if (heartbeatTimer != null)
                    heartbeatTimer.Change(0, value);
            }
        }

        private ActionQueue receiveActionQueue;
        private IPEndPoint remoteEndPoint;
        private Rudp rudp;
        private ConnectionState connectionState = ConnectionState.notConnected;

        private bool HasTimedOut { get => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > TimeoutTime; }
        private Timer heartbeatTimer;
        private DateTime lastHeartbeat;
        private byte lastPingId = 0;
        private (byte id, DateTime sendTime) pendingPing;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name of this client instance. Used when logging messages.</param>
        public Client(string logName = "CLIENT") : base(logName) { }

        /// <summary>Attempts to connect to an IP and port.</summary>
        /// <param name="ip">The IP to connect to.</param>
        /// <param name="port">The remote port to connect to.</param>
        /// <param name="receiveActionQueue">The action queue to add messages to. Passing null will cause messages to be handled immediately on the same thread on which they were received.</param>
        /// /// <param name="heartbeatInterval">The interval (in milliseconds) at which heartbeats should be sent to the server.</param>
        public void Connect(string ip, ushort port, ActionQueue receiveActionQueue = null, ushort heartbeatInterval = 1000)
        {
            this.receiveActionQueue = receiveActionQueue;
            _heartbeatInterval = heartbeatInterval;
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            rudp = new Rudp(Send, logName);

            RiptideLogger.Log(logName, $"Connecting to {remoteEndPoint}.");
            StartListening();
            connectionState = ConnectionState.connecting;

            heartbeatTimer = new Timer(Heartbeat, null, 0, HeartbeatInterval);
        }

        private void Heartbeat(object state)
        {
            if (connectionState == ConnectionState.connecting)
                SendConnect();
            else if (connectionState == ConnectionState.connected)
            {
                if (HasTimedOut)
                {
                    HandleDisconnect();
                    return;
                }

                SendHeartbeat();
            }
        }

        /// <summary>Whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="firstByte">The first byte of the message.</param>
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
#if DETAILED_LOGGING
                        ushort messageId = message.PeekUShort();
                        if (headerType == HeaderType.reliable)
                            RiptideLogger.Log(logName, $"Received reliable message (ID: {messageId}) from {fromEndPoint}.");
                        else if (headerType == HeaderType.unreliable)
                            RiptideLogger.Log(logName, $"Received message (ID: {messageId}) from {fromEndPoint}.");
#endif
                        OnMessageReceived(new ClientMessageReceivedEventArgs(message));
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
                            OnMessageReceived(new ClientMessageReceivedEventArgs(message));
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
                    HandleDisconnect();
                    break;
                default:
                    throw new Exception($"Unknown message header type: {headerType}");
            }
        }

        internal override void ReliableHandle(Message message, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            ReliableHandle(message, fromEndPoint, headerType, rudp.SendLockables);
        }

        /// <summary>Sends a message to the server.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        public void Send(Message message, byte maxSendAttempts = 3)
        {
            if (message.SendMode == MessageSendMode.unreliable)
                Send(message.ToArray(), remoteEndPoint);
            else
                SendReliable(message, remoteEndPoint, rudp, maxSendAttempts);
        }

        /// <summary>Disconnects from the server.</summary>
        public void Disconnect()
        {
            if (connectionState == ConnectionState.notConnected)
                return;

            SendDisconnect();
            heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
            heartbeatTimer.Dispose();
            StopListening();
            connectionState = ConnectionState.notConnected;
            RiptideLogger.Log(logName, "Disconnected.");
        }

        #region Messages
        private void SendConnect()
        {
            Send(new Message(HeaderType.connect));
        }

        /// <summary>Sends an acknowledgement for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        /// <param name="toEndPoint">The endpoint to send the acknowledgement to.</param>
        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            Message message;
            if (forSeqId == rudp.SendLockables.LastReceivedSeqId)
                message = new Message(HeaderType.ack, 4);
            else
                message = new Message(HeaderType.ackExtra, 6);

            message.Add(rudp.SendLockables.LastReceivedSeqId); // Last remote sequence ID
            message.Add(rudp.SendLockables.AcksBitfield); // Acks

            if (forSeqId == rudp.SendLockables.LastReceivedSeqId)
                Send(message);
            else
            {
                message.Add(forSeqId);
                Send(message);
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

            Message message = new Message(HeaderType.heartbeat, 3);
            message.Add(pendingPing.id);
            message.Add(rudp.RTT);

            Send(message);
        }

        private void HandleHeartbeat(Message message)
        {
            byte pingId = message.GetByte();

            if (pendingPing.id == pingId)
            {
                rudp.RTT = (ushort)Math.Max(1f, (DateTime.UtcNow - pendingPing.sendTime).TotalMilliseconds);
                OnPingUpdated(new PingUpdatedEventArgs(rudp.RTT, rudp.SmoothRTT));
            }

            lastHeartbeat = DateTime.UtcNow;
        }

        private void HandleWelcome(Message message)
        {
            if (connectionState == ConnectionState.connected)
                return;

            Id = message.GetUShort();
            connectionState = ConnectionState.connected;
            lastHeartbeat = DateTime.UtcNow;

            SendWelcomeReceived();
            OnConnected(new EventArgs());
        }

        internal void SendWelcomeReceived()
        {
            Message message = new Message(HeaderType.welcome, 2);
            message.Add(Id);

            Send(message, 5);
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
            Message message = new Message(HeaderType.disconnect);

            Send(message);
        }

        private void HandleDisconnect()
        {
            StopListening();
            connectionState = ConnectionState.notConnected;
            OnDisconnected(new EventArgs());
        }
        #endregion

        #region Events
        /// <summary>Invoked when a connection to the server is established.</summary>
        public event EventHandler Connected;
        private void OnConnected(EventArgs e)
        {
            RiptideLogger.Log(logName, "Connected successfully!");

            if (receiveActionQueue == null)
                Connected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => Connected?.Invoke(this, e));
        }
        /// <summary>Invoked when a message is received from the server.</summary>
        public event EventHandler<ClientMessageReceivedEventArgs> MessageReceived;
        internal void OnMessageReceived(ClientMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }
        /// <summary>Invoked when disconnected by the server.</summary>
        public event EventHandler Disconnected;
        private void OnDisconnected(EventArgs e)
        {
            RiptideLogger.Log(logName, "Disconnected from server.");

            if (receiveActionQueue == null)
                Disconnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => Disconnected?.Invoke(this, e));
        }

        /// <summary>Invoked when a new client connects.</summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        private void OnClientConnected(ClientConnectedEventArgs e)
        {
            RiptideLogger.Log(logName, $"Client {e.Id} connected.");

            if (receiveActionQueue == null)
                ClientConnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientConnected?.Invoke(this, e));
        }
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            RiptideLogger.Log(logName, $"Client {e.Id} disconnected.");

            if (receiveActionQueue == null)
                ClientDisconnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));
        }

        /// <summary>Invoked when ping is updated.</summary>
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
