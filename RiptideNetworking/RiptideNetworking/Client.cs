// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/tom-weiland/RiptideNetworking/blob/main/LICENSE.md

using Riptide.Transports;
using Riptide.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Riptide
{
    /// <summary>A client that can connect to a <see cref="Server"/>.</summary>
    public class Client : Peer
    {
        /// <summary>Invoked when a connection to the server is established.</summary>
        public event EventHandler Connected;
        /// <summary>Invoked when a connection to the server fails to be established.</summary>
        public event EventHandler<ConnectionFailedEventArgs> ConnectionFailed;
        /// <summary>Invoked when a message is received.</summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        /// <summary>Invoked when disconnected from the server.</summary>
        public event EventHandler<DisconnectedEventArgs> Disconnected;
        /// <summary>Invoked when another <i>non-local</i> client connects.</summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        /// <summary>Invoked when another <i>non-local</i> client disconnects.</summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>The client's numeric ID.</summary>
        public ushort Id => connection.Id;
        /// <inheritdoc cref="Connection.RTT"/>
        public short RTT => connection.RTT;
        /// <summary><inheritdoc cref="Connection.SmoothRTT"/></summary>
        /// <remarks>This value is slower to accurately represent lasting changes in latency than <see cref="RTT"/>, but it is less susceptible to changing drastically due to significant—but temporary—jumps in latency.</remarks>
        public short SmoothRTT => connection.SmoothRTT;
        /// <summary>Whether or not the client is currently <i>not</i> connected nor trying to connect.</summary>
        public bool IsNotConnected => connection is null || connection.IsNotConnected;
        /// <summary>Whether or not the client is currently in the process of connecting.</summary>
        public bool IsConnecting => !(connection is null) && connection.IsConnecting;
        /// <summary>Whether or not the client's connection is currently pending (will only be <see langword="true"/> when a server doesn't immediately accept the connection request).</summary>
        public bool IsPending => !(connection is null) && connection.IsPending;
        /// <summary>Whether or not the client is currently connected.</summary>
        public bool IsConnected => !(connection is null) && connection.IsConnected;
        /// <inheritdoc cref="connection"/>
        // Not an auto property because properties can't be passed as ref/out parameters. Could
        // use a local variable in the Connect method, but that's arguably not any cleaner. This
        // property will also probably only be used rarely from outside the class/library.
        public Connection Connection => connection;
        /// <summary>Encapsulates a method that handles a message from a server.</summary>
        /// <param name="message">The message that was received.</param>
        public delegate void MessageHandler(Message message);

        /// <summary>The client's connection to a server.</summary>
        private Connection connection;
        /// <summary>How many connection attempts have been made so far.</summary>
        private int connectionAttempts;
        /// <summary>How many connection attempts to make before giving up.</summary>
        private int maxConnectionAttempts;
        /// <inheritdoc cref="Server.messageHandlers"/>
        private Dictionary<ushort, MessageHandler> messageHandlers;
        /// <summary>The underlying transport's client that is used for sending and receiving data.</summary>
        private IClient transport;
        /// <summary>Custom data to include when connecting.</summary>
        private byte[] connectBytes;

        /// <inheritdoc cref="Server(IServer, string)"/>
        public Client(IClient transport, string logName = "CLIENT") : base(logName)
        {
            this.transport = transport;
        }

        /// <inheritdoc cref="Server(string)"/>
        public Client(string logName = "CLIENT") : base(logName)
        {
            transport = new Transports.Udp.UdpClient();
        }

        /// <summary>Disconnects the client if it's connected and swaps out the transport it's using.</summary>
        /// <param name="newTransport">The new transport to use for sending and receiving data.</param>
        /// <remarks>This method does not automatically reconnect to the server. To continue communicating with the server, <see cref="Connect(string, int, byte, Message)"/> must be called again.</remarks>
        public void ChangeTransport(IClient newTransport)
        {
            Disconnect();
            transport = newTransport;
        }

        /// <summary>Attempts to connect to a server at the given host address.</summary>
        /// <param name="hostAddress">The host address to connect to.</param>
        /// <param name="maxConnectionAttempts">How many connection attempts to make before giving up.</param>
        /// <param name="messageHandlerGroupId">The ID of the group of message handler methods to use when building <see cref="messageHandlers"/>.</param>
        /// <param name="message">Data that should be sent to the server with the connection attempt. Use <see cref="Message.Create()"/> to get an empty message instance.</param>
        /// <remarks>Riptide's default transport expects the host address to consist of an IP and port, separated by a colon. For example: <c>127.0.0.1:7777</c>. If you are using a different transport, check the relevant documentation for what information it requires in the host address.</remarks>
        /// <returns><see langword="true"/> if a connection attempt will be made. <see langword="false"/> if an issue occurred (such as <paramref name="hostAddress"/> being in an invalid format) and a connection attempt will <i>not</i> be made.</returns>
        public bool Connect(string hostAddress, int maxConnectionAttempts = 5, byte messageHandlerGroupId = 0, Message message = null)
        {
            Disconnect();

            SubToTransportEvents();

            if (!transport.Connect(hostAddress, out connection, out string connectError))
            {
                RiptideLogger.Log(LogType.Error, LogName, connectError);
                UnsubFromTransportEvents();
                return false;
            }

            this.maxConnectionAttempts = maxConnectionAttempts;
            connectionAttempts = 0;
            connection.Peer = this;
            IncreaseActiveCount();
            CreateMessageHandlersDictionary(messageHandlerGroupId);

            if (message != null)
            {
                connectBytes = message.GetBytes(message.WrittenLength);
                message.Release();
            }
            else
                connectBytes = null;

            Heartbeat();
            RiptideLogger.Log(LogType.Info, LogName, $"Connecting to {connection}...");
            return true;
        }

        /// <summary>Subscribes appropriate methods to the transport's events.</summary>
        private void SubToTransportEvents()
        {
            transport.Connected += TransportConnected;
            transport.ConnectionFailed += TransportConnectionFailed;
            transport.DataReceived += HandleData;
            transport.Disconnected += TransportDisconnected;
        }
        
        /// <summary>Unsubscribes methods from all of the transport's events.</summary>
        private void UnsubFromTransportEvents()
        {
            transport.Connected -= TransportConnected;
            transport.ConnectionFailed -= TransportConnectionFailed;
            transport.DataReceived -= HandleData;
            transport.Disconnected -= TransportDisconnected;
        }

        /// <inheritdoc/>
        protected override void CreateMessageHandlersDictionary(byte messageHandlerGroupId)
        {
            MethodInfo[] methods = FindMessageHandlers();

            messageHandlers = new Dictionary<ushort, MessageHandler>(methods.Length);
            foreach (MethodInfo method in methods)
            {
                MessageHandlerAttribute attribute = method.GetCustomAttribute<MessageHandlerAttribute>();
                if (attribute.GroupId != messageHandlerGroupId)
                    continue;

                if (!method.IsStatic)
                    throw new NonStaticHandlerException(method.DeclaringType, method.Name);

                Delegate clientMessageHandler = Delegate.CreateDelegate(typeof(MessageHandler), method, false);
                if (clientMessageHandler != null)
                {
                    // It's a message handler for Client instances
                    if (messageHandlers.ContainsKey(attribute.MessageId))
                    {
                        MethodInfo otherMethodWithId = messageHandlers[attribute.MessageId].GetMethodInfo();
                        throw new DuplicateHandlerException(attribute.MessageId, method, otherMethodWithId);
                    }
                    else
                        messageHandlers.Add(attribute.MessageId, (MessageHandler)clientMessageHandler);
                }
                else
                {
                    // It's not a message handler for Client instances, but it might be one for Server instances
                    Delegate serverMessageHandler = Delegate.CreateDelegate(typeof(Server.MessageHandler), method, false);
                    if (serverMessageHandler == null)
                        throw new InvalidHandlerSignatureException(method.DeclaringType, method.Name);
                }
            }
        }

        /// <inheritdoc/>
        internal override void Heartbeat()
        {
            if (IsConnecting)
            {
                // If still trying to connect, send connect messages instead of heartbeats
                if (connectionAttempts < maxConnectionAttempts)
                {
                    Message message = Message.Create(MessageHeader.Connect);
                    if (connectBytes != null)
                        message.AddBytes(connectBytes, false);

                    Send(message);
                    connectionAttempts++;
                }
                else
                    LocalDisconnect(DisconnectReason.NeverConnected);
            }
            else if (IsPending)
            {
                // If waiting for the server to accept/reject the connection attempt
                if (connection.HasConnectAttemptTimedOut)
                {
                    LocalDisconnect(DisconnectReason.TimedOut);
                    return;
                }
            }
            else if (IsConnected)
            {
                // If connected and not timed out, send heartbeats
                if (connection.HasTimedOut)
                {
                    LocalDisconnect(DisconnectReason.TimedOut);
                    return;
                }

                connection.SendHeartbeat();
            }

            ExecuteLater(HeartbeatInterval, new HeartbeatEvent(this));
        }

        /// <inheritdoc/>
        public override void Update()
        {
            base.Update();
            transport.Poll();
            HandleMessages();
        }

        /// <inheritdoc/>
        protected override void Handle(Message message, MessageHeader header, Connection connection)
        {
            switch (header)
            {
                // User messages
                case MessageHeader.Unreliable:
                case MessageHeader.Reliable:
                    OnMessageReceived(message);
                    break;

                // Internal messages
                case MessageHeader.Ack:
                    connection.HandleAck(message);
                    break;
                case MessageHeader.AckExtra:
                    connection.HandleAckExtra(message);
                    break;
                case MessageHeader.Connect:
                    connection.SetPending();
                    break;
                case MessageHeader.Reject:
                    RejectReason reason = (RejectReason)message.GetByte();
                    if (reason == RejectReason.Pending)
                        connection.SetPending();
                    else if (!IsConnected) // Don't disconnect if we are connected
                        LocalDisconnect(DisconnectReason.ConnectionRejected, message, reason);
                    break;
                case MessageHeader.Heartbeat:
                    connection.HandleHeartbeatResponse(message);
                    break;
                case MessageHeader.Disconnect:
                    LocalDisconnect((DisconnectReason)message.GetByte(), message);
                    break;
                case MessageHeader.Welcome:
                    if (IsConnecting || IsPending)
                    {
                        connection.HandleWelcome(message);
                        OnConnected();
                    }
                    break;
                case MessageHeader.ClientConnected:
                    OnClientConnected(message.GetUShort());
                    break;
                case MessageHeader.ClientDisconnected:
                    OnClientDisconnected(message.GetUShort());
                    break;
                default:
                    RiptideLogger.Log(LogType.Warning, LogName, $"Unexpected message header '{header}'! Discarding {message.WrittenLength} bytes.");
                    break;
            }

            message.Release();
        }

        /// <summary>Sends a message to the server.</summary>
        /// <param name="message">The message to send.</param>
        /// <param name="shouldRelease">Whether or not to return the message to the pool after it is sent.</param>
        /// <remarks><inheritdoc cref="Connection.Send(Message, bool)"/></remarks>
        public void Send(Message message, bool shouldRelease = true) => connection.Send(message, shouldRelease);

        /// <summary>Disconnects from the server.</summary>
        public void Disconnect()
        {
            if (connection == null || IsNotConnected)
                return;

            Send(Message.Create(MessageHeader.Disconnect));
            LocalDisconnect(DisconnectReason.Disconnected);
        }

        /// <summary>Cleans up the local side of the connection.</summary>
        /// <param name="reason">The reason why the client has disconnected.</param>
        /// <param name="message">The disconnection or rejection message, potentially containing extra data to be handled externally.</param>
        /// <param name="rejectReason">TData that should be sent to the client being disconnected. Use <see cref="Message.Create()"/> to get an empty message instance. Unused if the connection wasn't rejected.</param>
        private void LocalDisconnect(DisconnectReason reason, Message message = null, RejectReason rejectReason = RejectReason.NoConnection)
        {
            if (IsNotConnected)
                return;

            UnsubFromTransportEvents();
            DecreaseActiveCount();

            StopTime();
            transport.Disconnect();

            connection.LocalDisconnect();

            if (reason == DisconnectReason.NeverConnected)
                OnConnectionFailed(RejectReason.NoConnection);
            else if (reason == DisconnectReason.ConnectionRejected)
                OnConnectionFailed(rejectReason, message);
            else
                OnDisconnected(reason, message);
        }

        /// <summary>What to do when the transport establishes a connection.</summary>
        private void TransportConnected(object sender, EventArgs e)
        {
            StartTime();
        }

        /// <summary>What to do when the transport fails to connect.</summary>
        private void TransportConnectionFailed(object sender, EventArgs e)
        {
            LocalDisconnect(DisconnectReason.NeverConnected);
        }

        /// <summary>What to do when the transport disconnects.</summary>
        private void TransportDisconnected(object sender, Transports.DisconnectedEventArgs e)
        {
            if (connection == e.Connection)
                LocalDisconnect(e.Reason);
        }

        #region Events
        /// <summary>Invokes the <see cref="Connected"/> event.</summary>
        protected virtual void OnConnected()
        {
            RiptideLogger.Log(LogType.Info, LogName, "Connected successfully!");
            Connected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Invokes the <see cref="ConnectionFailed"/> event.</summary>
        /// <param name="reason">The reason for the connection failure.</param>
        /// <param name="message">Additional data related to the failed connection attempt.</param>
        protected virtual void OnConnectionFailed(RejectReason reason, Message message = null)
        {
            string reasonString;
            switch (reason)
            {
                case RejectReason.NoConnection:
                    reasonString = CRNoConnection;
                    break;
                case RejectReason.ServerFull:
                    reasonString = CRServerFull;
                    break;
                case RejectReason.Rejected:
                    reasonString = CRRejected;
                    break;
                case RejectReason.Custom:
                    reasonString = CRCustom;
                    break;
                default:
                    reasonString = UnknownReason;
                    break;
            }
            
            RiptideLogger.Log(LogType.Info, LogName, $"Connection to server failed: {reasonString}.");
            ConnectionFailed?.Invoke(this, new ConnectionFailedEventArgs(message));
        }

        /// <summary>Invokes the <see cref="MessageReceived"/> event and initiates handling of the received message.</summary>
        /// <param name="message">The received message.</param>
        protected virtual void OnMessageReceived(Message message)
        {
            ushort messageId = message.GetUShort();
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(connection, messageId, message));

            if (messageHandlers.TryGetValue(messageId, out MessageHandler messageHandler))
                messageHandler(message);
            else
                RiptideLogger.Log(LogType.Warning, LogName, $"No message handler method found for message ID {messageId}!");
        }

        /// <summary>Invokes the <see cref="Disconnected"/> event.</summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <param name="message">Additional data related to the disconnection.</param>
        protected virtual void OnDisconnected(DisconnectReason reason, Message message)
        {
            string reasonString;
            switch (reason)
            {
                case DisconnectReason.NeverConnected:
                    reasonString = DCNeverConnected;
                    break;
                case DisconnectReason.TransportError:
                    reasonString = DCTransportError;
                    break;
                case DisconnectReason.TimedOut:
                    reasonString = DCTimedOut;
                    break;
                case DisconnectReason.Kicked:
                    reasonString = DCKicked;
                    break;
                case DisconnectReason.ServerStopped:
                    reasonString = DCServerStopped;
                    break;
                case DisconnectReason.Disconnected:
                    reasonString = DCDisconnected;
                    break;
                default:
                    reasonString = UnknownReason;
                    break;
            }

            RiptideLogger.Log(LogType.Info, LogName, $"Disconnected from server: {reasonString}.");
            Disconnected?.Invoke(this, new DisconnectedEventArgs(reason, message));
        }

        /// <summary>Invokes the <see cref="ClientConnected"/> event.</summary>
        /// <param name="clientId">The numeric ID of the client that connected.</param>
        protected virtual void OnClientConnected(ushort clientId)
        {
            RiptideLogger.Log(LogType.Info, LogName, $"Client {clientId} connected.");
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs(clientId));
        }

        /// <summary>Invokes the <see cref="ClientDisconnected"/> event.</summary>
        /// <param name="clientId">The numeric ID of the client that disconnected.</param>
        protected virtual void OnClientDisconnected(ushort clientId)
        {
            RiptideLogger.Log(LogType.Info, LogName, $"Client {clientId} disconnected.");
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientId));
        }
        #endregion
    }
}
