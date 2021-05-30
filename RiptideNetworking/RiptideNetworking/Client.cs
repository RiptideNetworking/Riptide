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
        /// <summary>The round trip time of the connection. -1 if not calculated yet.</summary>
        public short RTT => rudp.RTT;
        /// <summary>The smoothed round trip time of the connection. -1 if not calculated yet.</summary>
        public short SmoothRTT => rudp.SmoothRTT;
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        public bool IsConnecting => connectionState == ConnectionState.connecting;
        /// <summary>Whether or not the client is currently connected.</summary>
        public bool IsConnected => connectionState == ConnectionState.connected;
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

        /// <summary>The action queue to use when triggering events. Null if events should be triggered immediately.</summary>
        private ActionQueue receiveActionQueue;
        /// <summary>The connetion's remote endpoint.</summary>
        private IPEndPoint remoteEndPoint;
        /// <summary>The client's Rudp instance.</summary>
        private Rudp rudp;
        /// <summary>The client's current connection state.</summary>
        private ConnectionState connectionState = ConnectionState.notConnected;
        /// <summary>How many connection attempts have been made.</summary>
        private byte connectionAttempts;
        /// <summary>How many connection attempts to make before giving up.</summary>
        private byte maxConnectionAttempts;

        /// <summary>Whether or not the client has timed out.</summary>
        private bool HasTimedOut => (DateTime.UtcNow - lastHeartbeat).TotalMilliseconds > TimeoutTime;
        /// <summary>The timer responsible for sending regular heartbeats.</summary>
        private Timer heartbeatTimer;
        /// <summary>The time at which the last heartbeat was received from the client.</summary>
        private DateTime lastHeartbeat;
        /// <summary>ID of the last ping that was sent.</summary>
        private byte lastPingId = 0;
        /// <summary>The currently pending ping.</summary>
        private (byte id, DateTime sendTime) pendingPing;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name to use when logging messages via RiptideLogger.</param>
        public Client(string logName = "CLIENT") : base(logName) { }

        /// <summary>Attempts to connect to an IP and port.</summary>
        /// <param name="ip">The IP to connect to.</param>
        /// <param name="port">The remote port to connect to.</param>
        /// <param name="receiveActionQueue">The action queue to add messages to. Passing null will cause messages to be handled immediately on the same thread on which they were received.</param>
        /// <param name="heartbeatInterval">The interval (in milliseconds) at which heartbeats should be sent to the server.</param>
        /// <param name="maxConnectionAttempts">How many connection attempts to make before giving up.</param>
        public void Connect(string ip, ushort port, ActionQueue receiveActionQueue = null, ushort heartbeatInterval = 1000, byte maxConnectionAttempts = 5)
        {
            this.receiveActionQueue = receiveActionQueue;
            _heartbeatInterval = heartbeatInterval;
            this.maxConnectionAttempts = maxConnectionAttempts;
            connectionAttempts = 0;
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            rudp = new Rudp(Send, LogName);

            RiptideLogger.Log(LogName, $"Connecting to {remoteEndPoint}...");
            StartListening();
            connectionState = ConnectionState.connecting;

            heartbeatTimer = new Timer(Heartbeat, null, 0, HeartbeatInterval);
        }

        /// <summary>Sends a connnect or heartbeat message. Called by the heartbeat timer.</summary>
        private void Heartbeat(object state)
        {
            if (connectionState == ConnectionState.connecting)
            {
                // If still trying to connect, send connect messages instead of heartbeats
                if (connectionAttempts < maxConnectionAttempts)
                {
                    SendConnect();
                    connectionAttempts++;
                }
                else
                {
                    LocalDisconnect();
                    OnConnectionFailed(EventArgs.Empty);
                }    
            }
            else if (connectionState == ConnectionState.connected)
            {
                // If connected and not timed out, send heartbeats
                if (HasTimedOut)
                {
                    HandleDisconnect();
                    return;
                }

                SendHeartbeat();
            }
        }

        /// <summary>Determines whether or not to handle a message from a specific remote endpoint.</summary>
        /// <param name="endPoint">The endpoint from which the message was sent.</param>
        /// <param name="firstByte">The first byte of the message.</param>
        protected override bool ShouldHandleMessageFrom(IPEndPoint endPoint, byte firstByte)
        {
            return endPoint.Equals(remoteEndPoint);
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
                        OnMessageReceived(new ClientMessageReceivedEventArgs(message));
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
                            OnMessageReceived(new ClientMessageReceivedEventArgs(message));
                        });
                    }
                    break;

                // Internal messages
                case HeaderType.ack:
                    HandleAck(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.ackExtra:
                    HandleAckExtra(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.heartbeat:
                    HandleHeartbeat(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.welcome:
                    HandleWelcome(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.clientConnected:
                    HandleClientConnected(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.clientDisconnected:
                    HandleClientDisconnected(Message.CreateInternal(headerType, data));
                    break;
                case HeaderType.disconnect:
                    HandleDisconnect();
                    break;
                default:
                    throw new Exception($"Unknown message header type '{headerType}'!");
            }
        }

        /// <summary>Handles the given reliably sent data.</summary>
        /// <param name="data">The reliably sent data.</param>
        /// <param name="fromEndPoint">The endpoint from which the data was received.</param>
        /// <param name="headerType">The header type of the data.</param>
        internal override void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            ReliableHandle(data, fromEndPoint, headerType, rudp.SendLockables);
        }

        /// <summary>Sends a message to the server.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        public void Send(Message message, byte maxSendAttempts = 3)
        {
            if (message.SendMode == MessageSendMode.unreliable)
                Send(message.Bytes, message.WrittenLength, remoteEndPoint);
            else
                SendReliable(message, remoteEndPoint, rudp, maxSendAttempts);
        }

        /// <summary>Disconnects from the server.</summary>
        public void Disconnect()
        {
            if (connectionState == ConnectionState.notConnected)
                return;

            SendDisconnect();
            LocalDisconnect();
            RiptideLogger.Log(LogName, "Disconnected.");
        }

        /// <summary>Cleans up local objects on disconnection.</summary>
        private void LocalDisconnect()
        {
            heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
            heartbeatTimer.Dispose();
            StopListening();
            connectionState = ConnectionState.notConnected;
        }

        #region Messages
        /// <summary>Sends a connect message.</summary>
        private void SendConnect()
        {
            Send(Message.CreateInternal(HeaderType.connect));
        }

        /// <summary>Sends an ack message for a sequence ID to a specific endpoint.</summary>
        /// <param name="forSeqId">The sequence ID to acknowledge.</param>
        /// <param name="toEndPoint">The endpoint to send the ack to.</param>
        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            Message message = Message.CreateInternal(forSeqId == rudp.SendLockables.LastReceivedSeqId ? HeaderType.ack : HeaderType.ackExtra);
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

        /// <summary>Handles an ack message.</summary>
        /// <param name="message">The ack message to handle.</param>
        private void HandleAck(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();

            rudp.AckMessage(remoteLastReceivedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Handles an ack message for a sequence ID other than the last received one.</summary>
        /// <param name="message">The ack message to handle.</param>
        private void HandleAckExtra(Message message)
        {
            ushort remoteLastReceivedSeqId = message.GetUShort();
            ushort remoteAcksBitField = message.GetUShort();
            ushort ackedSeqId = message.GetUShort();

            rudp.AckMessage(ackedSeqId); // Immediately mark it as delivered so no resends are triggered while waiting for the sequence ID's bit to reach the end of the bit field
            rudp.UpdateReceivedAcks(remoteLastReceivedSeqId, remoteAcksBitField);
        }

        /// <summary>Sends a heartbeat message.</summary>
        private void SendHeartbeat()
        {
            pendingPing = (lastPingId++, DateTime.UtcNow);

            Message message = Message.CreateInternal(HeaderType.heartbeat);
            message.Add(pendingPing.id);
            message.Add(rudp.RTT);

            Send(message);
        }

        /// <summary>Handles a heartbeat message.</summary>
        /// <param name="message">The heartbeat message to handle.</param>
        private void HandleHeartbeat(Message message)
        {
            byte pingId = message.GetByte();

            if (pendingPing.id == pingId)
            {
                rudp.RTT = (short)Math.Max(1f, (DateTime.UtcNow - pendingPing.sendTime).TotalMilliseconds);
                OnPingUpdated(new PingUpdatedEventArgs(rudp.RTT, rudp.SmoothRTT));
            }

            lastHeartbeat = DateTime.UtcNow;
        }

        /// <summary>Handles a welcome message.</summary>
        /// <param name="message">The welcome message to handle.</param>
        private void HandleWelcome(Message message)
        {
            if (connectionState == ConnectionState.connected)
                return;

            Id = message.GetUShort();
            connectionState = ConnectionState.connected;
            lastHeartbeat = DateTime.UtcNow;

            SendWelcomeReceived();
            OnConnected(EventArgs.Empty);
        }

        /// <summary>Sends a welcome (received) message.</summary>
        internal void SendWelcomeReceived()
        {
            Message message = Message.CreateInternal(HeaderType.welcome);
            message.Add(Id);

            Send(message, 5);
        }

        /// <summary>Handles a client connected message.</summary>
        /// <param name="message">The client connected message to handle.</param>
        private void HandleClientConnected(Message message)
        {
            OnClientConnected(new ClientConnectedEventArgs(message.GetUShort()));
        }

        /// <summary>Handles a client disconnected message.</summary>
        /// <param name="message">The client disconnected message to handle.</param>
        private void HandleClientDisconnected(Message message)
        {
            OnClientDisconnected(new ClientDisconnectedEventArgs(message.GetUShort()));
        }

        /// <summary>Sends a disconnect message.</summary>
        private void SendDisconnect()
        {
            Send(Message.CreateInternal(HeaderType.disconnect));
        }

        /// <summary>Handles a disconnect message.</summary>
        private void HandleDisconnect()
        {
            LocalDisconnect();
            OnDisconnected(EventArgs.Empty);
        }
        #endregion

        #region Events
        /// <summary>Invoked when a connection to the server is established.</summary>
        public event EventHandler Connected;
        private void OnConnected(EventArgs e)
        {
            RiptideLogger.Log(LogName, "Connected successfully!");

            if (receiveActionQueue == null)
                Connected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => Connected?.Invoke(this, e));
        }
        /// <summary>Invoked when a connection to the server fails to be established.</summary>
        public event EventHandler ConnectionFailed;
        private void OnConnectionFailed(EventArgs e)
        {
            RiptideLogger.Log(LogName, "Connection to server failed!");

            if (receiveActionQueue == null)
                ConnectionFailed?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ConnectionFailed?.Invoke(this, e));
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
            RiptideLogger.Log(LogName, "Disconnected from server.");

            if (receiveActionQueue == null)
                Disconnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => Disconnected?.Invoke(this, e));
        }

        /// <summary>Invoked when a new client connects.</summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        private void OnClientConnected(ClientConnectedEventArgs e)
        {
            RiptideLogger.Log(LogName, $"Client {e.Id} connected.");

            if (receiveActionQueue == null)
                ClientConnected?.Invoke(this, e);
            else
                receiveActionQueue.Add(() => ClientConnected?.Invoke(this, e));
        }
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            RiptideLogger.Log(LogName, $"Client {e.Id} disconnected.");

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
