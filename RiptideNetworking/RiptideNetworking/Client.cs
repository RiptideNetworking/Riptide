using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
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

        /// <summary>The connection's remote endpoint.</summary>
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

        /// <summary>Encapsulates a method that handles a message from the server.</summary>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(Message message);
        /// <summary>Methods used to handle messages, accessible by their corresponding message IDs.</summary>
        private Dictionary<ushort, MessageHandler> messageHandlers;

        /// <summary>Handles initial setup.</summary>
        /// <param name="logName">The name to use when logging messages via <see cref="RiptideLogger"/>.</param>
        public Client(string logName = "CLIENT") : base(logName) { }

        /// <summary>Attempts to connect to an IP and port.</summary>
        /// <param name="ip">The IP to connect to.</param>
        /// <param name="port">The remote port to connect to.</param>
        /// <param name="heartbeatInterval">The interval (in milliseconds) at which heartbeats should be sent to the server.</param>
        /// <param name="maxConnectionAttempts">How many connection attempts to make before giving up.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        public void Connect(string ip, ushort port, ushort heartbeatInterval = 1000, byte maxConnectionAttempts = 5, byte messageHandlerGroupId = 0)
        {
            _heartbeatInterval = heartbeatInterval;
            this.maxConnectionAttempts = maxConnectionAttempts;
            connectionAttempts = 0;
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            rudp = new Rudp(this);

            CreateMessageHandlersDictionary(Assembly.GetCallingAssembly(), messageHandlerGroupId);
            StartListening();
            connectionState = ConnectionState.connecting;

            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, $"Connecting to {remoteEndPoint}...");

            heartbeatTimer = new Timer(Heartbeat, null, 0, HeartbeatInterval);
        }

        /// <inheritdoc/>
        protected override void CreateMessageHandlersDictionary(Assembly assembly, byte messageHandlerGroupId)
        {
            MethodInfo[] methods = assembly.GetTypes()
                                           .SelectMany(t => t.GetMethods())
                                           .Where(m => m.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0)
                                           .ToArray();

            messageHandlers = new Dictionary<ushort, MessageHandler>(methods.Length);
            for (int i = 0; i < methods.Length; i++)
            {
                MessageHandlerAttribute attribute = methods[i].GetCustomAttribute<MessageHandlerAttribute>();
                if (attribute.GroupId != messageHandlerGroupId)
                    break;

                Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), methods[i], false);
                if (clientMessageHandler != null)
                {
                    // It's a message handler for Client instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                        RiptideLogger.Log("ERROR", $"Message handler method already exists for message ID {attribute.MessageId}! Only one handler method is allowed per ID!");
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                }
                else
                {
                    // It's not a message handler for Client instances, but it might be one for Server instances
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Server.MessageHandler), methods[i], false);
                    if (serverMessageHandler == null)
                        RiptideLogger.Log("ERROR", $"Method '{methods[i].Name}' didn't match a message handler signature!");
                }
            }
        }

        /// <summary>Sends a connnect or heartbeat message. Called by <see cref="heartbeatTimer"/>.</summary>
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
                    OnConnectionFailed();
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

        /// <inheritdoc/>
        protected override bool ShouldHandleMessageFrom(IPEndPoint endPoint, byte firstByte)
        {
            return endPoint.Equals(remoteEndPoint);
        }

        /// <inheritdoc/>
        internal override void Handle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType)
        {
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
                        ushort messageId = message.GetUShort();
                        OnMessageReceived(new ClientMessageReceivedEventArgs(messageId, message));

                        if (messageHandlers.TryGetValue(messageId, out MessageHandler messageHandler))
                            messageHandler(message);
                        else
                            RiptideLogger.Log("ERROR", $"No handler method found for message ID {messageId}!");

                        message.Release();
                    });
                    return;

                // Internal messages
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
                    RiptideLogger.Log("ERROR", $"Unknown message header type '{headerType}'! Discarding {data.Length} bytes.");
                    return;
            }
            
            message.Release();
        }

        /// <inheritdoc/>
        internal override void ReliableHandle(byte[] data, IPEndPoint fromEndPoint, HeaderType headerType)
        {
            ReliableHandle(data, fromEndPoint, headerType, rudp.SendLockables);
        }

        /// <summary>Sends a message to the server.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="maxSendAttempts">How often to try sending a reliable message before giving up.</param>
        /// <param name="shouldRelease">Whether or not <paramref name="message"/> should be returned to the pool once its data has been sent.</param>
        public void Send(Message message, byte maxSendAttempts = 15, bool shouldRelease = true)
        {
            if (message.SendMode == MessageSendMode.unreliable)
                Send(message.Bytes, message.WrittenLength, remoteEndPoint);
            else
                SendReliable(message, remoteEndPoint, rudp, maxSendAttempts);

            if (shouldRelease)
                message.Release();
        }

        /// <summary>Disconnects from the server.</summary>
        public void Disconnect()
        {
            if (connectionState == ConnectionState.notConnected)
                return;

            SendDisconnect();
            LocalDisconnect();

            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, "Disconnected.");
        }

        /// <summary>Cleans up local objects on disconnection.</summary>
        private void LocalDisconnect()
        {
            heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
            heartbeatTimer.Dispose();
            StopListening();
            connectionState = ConnectionState.notConnected;

            lock (rudp.PendingMessages)
            {
                foreach (Rudp.PendingMessage pendingMessage in rudp.PendingMessages.Values)
                    pendingMessage.Clear();
            }
        }

        #region Messages
        /// <summary>Sends a connect message.</summary>
        private void SendConnect()
        {
            Send(Message.Create(HeaderType.connect));
        }

        /// <inheritdoc/>
        protected override void SendAck(ushort forSeqId, IPEndPoint toEndPoint)
        {
            Message message = Message.Create(forSeqId == rudp.SendLockables.LastReceivedSeqId ? HeaderType.ack : HeaderType.ackExtra);
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

            Message message = Message.Create(HeaderType.heartbeat);
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
            OnConnected();
        }

        /// <summary>Sends a welcome (received) message.</summary>
        internal void SendWelcomeReceived()
        {
            Message message = Message.Create(HeaderType.welcome);
            message.Add(Id);

            Send(message, 25);
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
            Send(Message.Create(HeaderType.disconnect));
        }

        /// <summary>Handles a disconnect message.</summary>
        private void HandleDisconnect()
        {            
            OnDisconnected();
        }
        #endregion

        #region Events
        /// <summary>Invoked when a connection to the server is established.</summary>
        public event EventHandler Connected;
        private void OnConnected()
        {
            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, "Connected successfully!");

            receiveActionQueue.Add(() => Connected?.Invoke(this, EventArgs.Empty));
        }
        /// <summary>Invoked when a connection to the server fails to be established.</summary>
        /// <remarks>This occurs when a connection request times out, either because no server is listening on the expected IP and port, or because something (firewall, antivirus, no/poor internet access, etc.) is blocking the connection.</remarks>
        public event EventHandler ConnectionFailed;
        private void OnConnectionFailed()
        {
            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, "Connection to server failed!");

            receiveActionQueue.Add(() =>
            {
                LocalDisconnect();
                ConnectionFailed?.Invoke(this, EventArgs.Empty);
            });
        }
        /// <summary>Invoked when a message is received from the server.</summary>
        public event EventHandler<ClientMessageReceivedEventArgs> MessageReceived;
        internal void OnMessageReceived(ClientMessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }
        /// <summary>Invoked when disconnected by the server.</summary>
        public event EventHandler Disconnected;
        private void OnDisconnected()
        {
            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, "Disconnected from server.");

            receiveActionQueue.Add(() =>
            {
                LocalDisconnect();
                Disconnected?.Invoke(this, EventArgs.Empty);
            });
        }

        /// <summary>Invoked when a new client connects.</summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        private void OnClientConnected(ClientConnectedEventArgs e)
        {
            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, $"Client {e.Id} connected.");

            receiveActionQueue.Add(() => ClientConnected?.Invoke(this, e));
        }
        /// <summary>Invoked when a client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        private void OnClientDisconnected(ClientDisconnectedEventArgs e)
        {
            if (ShouldOutputInfoLogs)
                RiptideLogger.Log(LogName, $"Client {e.Id} disconnected.");

            receiveActionQueue.Add(() => ClientDisconnected?.Invoke(this, e));
        }

        /// <summary>Invoked when ping is updated.</summary>
        public event EventHandler<PingUpdatedEventArgs> PingUpdated;
        private void OnPingUpdated(PingUpdatedEventArgs e)
        {
            receiveActionQueue.Add(() => PingUpdated?.Invoke(this, e));
        }
        #endregion
    }
}
